using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Xml;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Writes a .xlsx straight to a stream (a FileStream on disk in production),
    /// appending rows in chunks so the full data set never sits in memory. Rolls
    /// over to a new worksheet at the Excel 1,048,576-row limit.
    ///
    /// Without a layout, columns are inferred from the first appended row's public
    /// properties and the header sits at row 1 — the original behaviour, which every
    /// report that has not opted in still gets. With an <see cref="ExcelReportLayout"/>
    /// the sheet instead opens with the report's title line(s), then a header row of
    /// the layout's own column names, and only the layout's columns are written.
    ///
    /// Usage: append chunks, then Finish(). Disposing without Finish leaves an
    /// incomplete (unreadable) archive — the worker deletes the file on failure.
    /// </summary>
    public sealed class StreamingExcelWriter : IDisposable
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private const int MaxRowsPerSheetDefault = 1_048_576; // includes the title and header rows
        private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        // Style indexes into StylesXml's <cellXfs>. Keep in sync with that constant.
        private const int StyleDefault = 0;
        private const int StyleTitle = 1;
        private const int StyleHeader = 2;
        private const int StyleDate = 3;
        private const int StyleMoney = 4;
        private const int StyleTotalLabel = 5;
        private const int StyleTotalMoney = 6;

        private readonly ZipArchive _archive;
        private readonly string _worksheetBaseName;
        private readonly ExcelReportLayout _layout;
        private readonly int _maxRowsPerSheet;
        private readonly string[] _titleLines;
        private readonly ExcelColumn[]? _columns;   // null → legacy reflection mode
        private readonly int _headerRowIndex;       // 1 when there is no title
        private readonly double[]? _totals;         // one slot per column; null when no totals row

        private PropertyInfo[]? _properties;        // legacy mode only
        private XmlWriter? _sheetWriter;
        private Stream? _sheetStream;
        private int _sheetCount;
        private int _rowInSheet;       // 1-based row number within the current sheet
        private long _totalDataRows;   // also the running ordinal behind the "No" column

        public StreamingExcelWriter(Stream output, string worksheetName)
            : this(output, worksheetName, null, MaxRowsPerSheetDefault)
        {
        }

        public StreamingExcelWriter(Stream output, string worksheetName, ExcelReportLayout? layout)
            : this(output, worksheetName, layout, MaxRowsPerSheetDefault)
        {
        }

        /// <summary>Test seam: a small <paramref name="maxRowsPerSheet"/> exercises sheet rollover.</summary>
        internal StreamingExcelWriter(Stream output, string worksheetName, ExcelReportLayout? layout, int maxRowsPerSheet)
        {
            ArgumentNullException.ThrowIfNull(output);
            _archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
            _worksheetBaseName = SanitizeWorksheetName(worksheetName);
            _layout = layout ?? ExcelReportLayout.None;
            _maxRowsPerSheet = maxRowsPerSheet;
            _titleLines = _layout.TitleLines.ToArray();
            _columns = _layout.HasExplicitColumns ? _layout.Columns.ToArray() : null;
            _headerRowIndex = _titleLines.Length + 1;
            _totals = _layout.TotalsRowLabel != null && _columns != null && _columns.Any(c => c.IncludeInTotals)
                ? new double[_columns.Length]
                : null;
        }

        public int SheetCount => _sheetCount;

        /// <summary>Data rows only — title, header and totals rows are excluded.</summary>
        public long TotalDataRows => _totalDataRows;

        /// <summary>
        /// Appends a chunk of rows. In legacy mode the column set is fixed from the
        /// runtime type of the first non-null row (so callers may pass boxed
        /// <c>object</c> chunks); with a layout the columns are already known.
        /// </summary>
        public void AppendRows<T>(IReadOnlyList<T> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                if (_columns == null)
                {
                    _properties ??= GetExportProperties(row.GetType());
                }

                if (_sheetWriter == null || _rowInSheet >= _maxRowsPerSheet)
                {
                    StartNewSheet();
                }

                _rowInSheet++;
                // Incremented before the write so the "No" column is 1-based, and never
                // reset by a rollover so it keeps counting across sheets.
                _totalDataRows++;
                WriteDataRow(_sheetWriter!, _rowInSheet, row);
            }
        }

        /// <summary>Closes the current sheet and writes the workbook manifest parts.</summary>
        public void Finish()
        {
            // Ensure at least one (title + header only) sheet exists.
            if (_sheetWriter == null)
            {
                if (_columns == null)
                {
                    _properties ??= Array.Empty<PropertyInfo>();
                }

                StartNewSheet();
            }

            WriteTotalsRow();
            CloseCurrentSheet();

            WriteTextEntry("[Content_Types].xml", ContentTypesXml(_sheetCount));
            WriteTextEntry("_rels/.rels", PackageRelationshipsXml);
            WriteTextEntry("xl/workbook.xml", WorkbookXml(_sheetCount, _worksheetBaseName));
            WriteTextEntry("xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml(_sheetCount));
            WriteTextEntry("xl/styles.xml", StylesXml);
        }

        public void Dispose()
        {
            _sheetWriter?.Dispose();
            _sheetStream?.Dispose();
            _archive.Dispose();
        }

        private void StartNewSheet()
        {
            CloseCurrentSheet();

            _sheetCount++;
            var entry = _archive.CreateEntry($"xl/worksheets/sheet{_sheetCount}.xml", CompressionLevel.Optimal);
            _sheetStream = entry.Open();
            _sheetWriter = XmlWriter.Create(_sheetStream, new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                CloseOutput = false,
                Indent = false
            });

            _sheetWriter.WriteStartDocument();
            _sheetWriter.WriteStartElement("worksheet", SpreadsheetNamespace);

            // <cols> must precede <sheetData> in the CT_Worksheet sequence.
            WriteColumnWidths(_sheetWriter);

            _sheetWriter.WriteStartElement("sheetData");

            // Title lines and the header row repeat on every sheet, so a rolled-over
            // file still reads standalone.
            for (var i = 0; i < _titleLines.Length; i++)
            {
                WriteSingleCellRow(_sheetWriter, i + 1, _titleLines[i], StyleTitle);
            }

            WriteHeaderRow(_sheetWriter, _headerRowIndex);

            // Re-base the row cursor past the preamble. Leaving this at 1 would make the
            // first data row re-emit a row number already used, which Excel reports as a
            // corrupt file.
            _rowInSheet = _headerRowIndex;
        }

        private void CloseCurrentSheet()
        {
            if (_sheetWriter == null)
            {
                return;
            }

            _sheetWriter.WriteEndElement(); // sheetData
            WriteMergedTitleCells(_sheetWriter);
            _sheetWriter.WriteEndElement(); // worksheet
            _sheetWriter.WriteEndDocument();
            _sheetWriter.Flush();
            _sheetWriter.Dispose();
            _sheetStream!.Dispose();
            _sheetWriter = null;
            _sheetStream = null;
        }

        private void WriteColumnWidths(XmlWriter writer)
        {
            if (_columns == null || !_columns.Any(c => c.Width is > 0))
            {
                return;
            }

            writer.WriteStartElement("cols");
            for (var i = 0; i < _columns.Length; i++)
            {
                var width = _columns[i].Width is > 0 ? _columns[i].Width!.Value : 18d;

                writer.WriteStartElement("col");
                writer.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("width", width.ToString("0.##", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("customWidth", "1");
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        /// <summary>
        /// Merged title bands. <c>mergeCells</c> follows <c>sheetData</c> in the
        /// CT_Worksheet sequence, so this is emitted while closing the sheet rather
        /// than next to the title rows themselves.
        /// </summary>
        private void WriteMergedTitleCells(XmlWriter writer)
        {
            var columnCount = ColumnCount();
            if (!_layout.MergeTitleAcrossColumns || _titleLines.Length == 0 || columnCount < 2)
            {
                return;
            }

            var lastColumn = GetColumnName(columnCount);

            writer.WriteStartElement("mergeCells");
            writer.WriteAttributeString("count", _titleLines.Length.ToString(CultureInfo.InvariantCulture));
            for (var rowNumber = 1; rowNumber <= _titleLines.Length; rowNumber++)
            {
                var row = rowNumber.ToString(CultureInfo.InvariantCulture);
                writer.WriteStartElement("mergeCell");
                writer.WriteAttributeString("ref", $"A{row}:{lastColumn}{row}");
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        private void WriteHeaderRow(XmlWriter writer, int rowNumber)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));

            if (_columns != null)
            {
                for (var i = 0; i < _columns.Length; i++)
                {
                    WriteCell(writer, GetCellReference(i + 1, rowNumber), _columns[i].Header, ExcelCellFormat.Text, StyleHeader);
                }
            }
            else
            {
                // Legacy: property names at style 0, byte-identical to the original writer.
                var columnIndex = 1;
                foreach (var property in _properties ?? Array.Empty<PropertyInfo>())
                {
                    WriteCell(writer, GetCellReference(columnIndex, rowNumber), property.Name, ExcelCellFormat.General, StyleDefault);
                    columnIndex++;
                }
            }

            writer.WriteEndElement();
        }

        private void WriteDataRow(XmlWriter writer, int rowNumber, object row)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));

            if (_columns != null)
            {
                for (var i = 0; i < _columns.Length; i++)
                {
                    var column = _columns[i];
                    var value = column.GetValue(row, _totalDataRows);

                    if (_totals != null && column.IncludeInTotals)
                    {
                        _totals[i] += ToDoubleOrZero(value);
                    }

                    WriteCell(writer, GetCellReference(i + 1, rowNumber), value, column.Format, StyleFor(column.Format));
                }
            }
            else
            {
                var columnIndex = 1;
                foreach (var property in _properties!)
                {
                    WriteCell(
                        writer,
                        GetCellReference(columnIndex, rowNumber),
                        property.GetValue(row),
                        ExcelCellFormat.General,
                        StyleDefault);
                    columnIndex++;
                }
            }

            writer.WriteEndElement();
        }

        /// <summary>
        /// The grid's footer total, written once after the last data row. Sums are
        /// accumulated while streaming, so they always describe exactly the rows in the
        /// file. Skipped for an empty export.
        /// </summary>
        private void WriteTotalsRow()
        {
            if (_totals == null || _columns == null || _totalDataRows == 0)
            {
                return;
            }

            if (_rowInSheet >= _maxRowsPerSheet)
            {
                StartNewSheet();
            }

            _rowInSheet++;

            // Label sits immediately left of the first totalled column, or in column A
            // when that column is already the first.
            var firstTotalled = Array.FindIndex(_columns, c => c.IncludeInTotals);
            var labelIndex = firstTotalled > 0 ? firstTotalled - 1 : 0;

            _sheetWriter!.WriteStartElement("row");
            _sheetWriter.WriteAttributeString("r", _rowInSheet.ToString(CultureInfo.InvariantCulture));

            for (var i = 0; i < _columns.Length; i++)
            {
                var reference = GetCellReference(i + 1, _rowInSheet);

                if (_columns[i].IncludeInTotals)
                {
                    WriteCell(_sheetWriter, reference, _totals[i], ExcelCellFormat.Money, StyleTotalMoney);
                }
                else if (i == labelIndex)
                {
                    WriteCell(_sheetWriter, reference, _layout.TotalsRowLabel, ExcelCellFormat.Text, StyleTotalLabel);
                }
                else
                {
                    WriteCell(_sheetWriter, reference, null, ExcelCellFormat.Text, StyleTotalLabel);
                }
            }

            _sheetWriter.WriteEndElement();
        }

        private int ColumnCount() => _columns?.Length ?? _properties?.Length ?? 0;

        private static int StyleFor(ExcelCellFormat format) => format switch
        {
            ExcelCellFormat.Date => StyleDate,
            ExcelCellFormat.Money => StyleMoney,
            _ => StyleDefault,
        };

        private static double ToDoubleOrZero(object? value)
        {
            if (value == null)
            {
                return 0d;
            }

            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                return 0d;
            }
        }

        private static PropertyInfo[] GetExportProperties(Type type)
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0)
                .ToArray();
        }

        private static void WriteSingleCellRow(XmlWriter writer, int rowNumber, string text, int styleIndex)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
            WriteCell(writer, GetCellReference(1, rowNumber), text, ExcelCellFormat.Text, styleIndex);
            writer.WriteEndElement();
        }

        private static void WriteCell(
            XmlWriter writer,
            string cellReference,
            object? value,
            ExcelCellFormat format = ExcelCellFormat.General,
            int styleIndex = StyleDefault)
        {
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", cellReference);

            if (styleIndex != StyleDefault)
            {
                writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
            }

            if (value != null)
            {
                if (format == ExcelCellFormat.Date && TryGetDateSerial(value, out var serial))
                {
                    writer.WriteElementString("v", serial);
                }
                else if (format != ExcelCellFormat.Text && TryGetNumericValue(value, out var numericValue))
                {
                    writer.WriteElementString("v", numericValue);
                }
                else
                {
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is");
                    writer.WriteStartElement("t");
                    writer.WriteString(FormatValue(value));
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
            }

            writer.WriteEndElement();
        }

        /// <summary>
        /// A real Excel date serial. Excel cannot render serials before 1900-03-01 (its
        /// deliberate 1900-leap-year quirk), so anything earlier degrades to text rather
        /// than to a row of "#####".
        /// </summary>
        private static bool TryGetDateSerial(object value, out string serial)
        {
            serial = string.Empty;

            var dateTime = value switch
            {
                DateTime dt => dt,
                DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
                _ => (DateTime?)null
            };

            if (dateTime is not { } date || date.Year < 1900)
            {
                return false;
            }

            serial = date.ToOADate().ToString("0.##########", CultureInfo.InvariantCulture);
            return true;
        }

        private static bool TryGetNumericValue(object value, out string numericValue)
        {
            numericValue = string.Empty;
            var type = Nullable.GetUnderlyingType(value.GetType()) ?? value.GetType();

            if (type == typeof(byte)
                || type == typeof(short)
                || type == typeof(int)
                || type == typeof(long)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal))
            {
                numericValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                return !string.IsNullOrEmpty(numericValue)
                    && numericValue != "NaN"
                    && numericValue != "Infinity"
                    && numericValue != "-Infinity";
            }

            return false;
        }

        private static string FormatValue(object value)
        {
            return value switch
            {
                DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeOnly timeOnly => timeOnly.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                bool boolean => boolean ? "TRUE" : "FALSE",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
        }

        private static string GetCellReference(int columnNumber, int rowNumber)
            => GetColumnName(columnNumber) + rowNumber.ToString(CultureInfo.InvariantCulture);

        private static string GetColumnName(int columnNumber)
        {
            var dividend = columnNumber;
            var columnName = string.Empty;

            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                dividend = (dividend - modulo) / 26;
            }

            return columnName;
        }

        private static string SanitizeWorksheetName(string worksheetName)
        {
            var value = string.IsNullOrWhiteSpace(worksheetName) ? "Report" : worksheetName.Trim();
            foreach (var invalidChar in new[] { '[', ']', ':', '*', '?', '/', '\\' })
            {
                value = value.Replace(invalidChar, ' ');
            }

            // Leave room for a " (n)" sheet-number suffix on multi-sheet exports.
            return value.Length > 27 ? value[..27] : value;
        }

        private void WriteTextEntry(string entryName, string content)
        {
            var entry = _archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true);
            writer.Write(content);
        }

        private string WorkbookXml(int sheetCount, string baseName)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append($"<workbook xmlns=\"{SpreadsheetNamespace}\" xmlns:r=\"{RelationshipsNamespace}\"><sheets>");
            for (var i = 1; i <= sheetCount; i++)
            {
                var name = sheetCount == 1 ? baseName : $"{baseName} ({i.ToString(CultureInfo.InvariantCulture)})";
                sb.Append($"<sheet name=\"{SecurityElement.Escape(name)}\" sheetId=\"{i}\" r:id=\"rId{i}\"/>");
            }

            sb.Append("</sheets></workbook>");
            return sb.ToString();
        }

        private static string ContentTypesXml(int sheetCount)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            for (var i = 1; i <= sheetCount; i++)
            {
                sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }

            sb.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            sb.Append("</Types>");
            return sb.ToString();
        }

        private const string PackageRelationshipsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private static string WorkbookRelationshipsXml(int sheetCount)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (var i = 1; i <= sheetCount; i++)
            {
                sb.Append($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
            }

            // Styles relationship id sits after the sheet ids.
            sb.Append($"<Relationship Id=\"rId{sheetCount + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            sb.Append("</Relationships>");
            return sb.ToString();
        }

        // cellXfs indexes must match the Style* constants above, and every count
        // attribute must match its element count or Excel reports a corrupt file.
        private const string StylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"2\">" +
            "<numFmt numFmtId=\"164\" formatCode=\"dd/mm/yyyy\"/>" +
            "<numFmt numFmtId=\"165\" formatCode=\"#,##0.00\"/>" +
            "</numFmts>" +
            "<fonts count=\"3\">" +
            "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><sz val=\"14\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "</fonts>" +
            "<fills count=\"2\">" +
            "<fill><patternFill patternType=\"none\"/></fill>" +
            "<fill><patternFill patternType=\"gray125\"/></fill>" +
            "</fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"7\">" +
            // 0 body
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            // 1 title
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\">" +
            "<alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            // 2 header
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\">" +
            "<alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
            // 3 date
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            // 4 money
            "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            // 5 totals label
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
            // 6 totals amount
            "<xf numFmtId=\"165\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\"/>" +
            "</cellXfs>" +
            "</styleSheet>";
    }
}

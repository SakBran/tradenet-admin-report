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
    /// over to a new worksheet at the Excel 1,048,576-row limit. Columns are
    /// inferred from the first appended row's public properties.
    ///
    /// Usage: append chunks, then Finish(). Disposing without Finish leaves an
    /// incomplete (unreadable) archive — the worker deletes the file on failure.
    /// </summary>
    public sealed class StreamingExcelWriter : IDisposable
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private const int HeaderRow = 1;
        private const int MaxRowsPerSheet = 1_048_576; // includes the header row
        private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        private readonly ZipArchive _archive;
        private readonly string _worksheetBaseName;

        private PropertyInfo[]? _properties;
        private XmlWriter? _sheetWriter;
        private Stream? _sheetStream;
        private int _sheetCount;
        private int _rowInSheet;       // 1-based row number within the current sheet
        private long _totalDataRows;

        public StreamingExcelWriter(Stream output, string worksheetName)
        {
            ArgumentNullException.ThrowIfNull(output);
            _archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
            _worksheetBaseName = SanitizeWorksheetName(worksheetName);
        }

        public int SheetCount => _sheetCount;
        public long TotalDataRows => _totalDataRows;

        /// <summary>Appends a chunk of rows. The first chunk fixes the column set from typeof(T).</summary>
        public void AppendRows<T>(IReadOnlyList<T> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);
            if (_properties == null)
            {
                _properties = GetExportProperties<T>();
            }

            foreach (var row in rows)
            {
                if (_sheetWriter == null || _rowInSheet >= MaxRowsPerSheet)
                {
                    StartNewSheet();
                }

                _rowInSheet++;
                WriteRow(_sheetWriter!, _rowInSheet, _properties!.Select(p => p.GetValue(row)));
                _totalDataRows++;
            }
        }

        /// <summary>Closes the current sheet and writes the workbook manifest parts.</summary>
        public void Finish()
        {
            // Ensure at least one (empty, header-only) sheet exists.
            if (_sheetWriter == null)
            {
                _properties ??= Array.Empty<PropertyInfo>();
                StartNewSheet();
            }

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
            _sheetWriter.WriteStartElement("sheetData");

            // Header row repeats on every sheet.
            WriteRow(_sheetWriter, HeaderRow, (_properties ?? Array.Empty<PropertyInfo>()).Select(p => (object?)p.Name));
            _rowInSheet = HeaderRow;
        }

        private void CloseCurrentSheet()
        {
            if (_sheetWriter == null)
            {
                return;
            }

            _sheetWriter.WriteEndElement(); // sheetData
            _sheetWriter.WriteEndElement(); // worksheet
            _sheetWriter.WriteEndDocument();
            _sheetWriter.Flush();
            _sheetWriter.Dispose();
            _sheetStream!.Dispose();
            _sheetWriter = null;
            _sheetStream = null;
        }

        private static PropertyInfo[] GetExportProperties<T>()
        {
            return typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0)
                .ToArray();
        }

        private static void WriteRow(XmlWriter writer, int rowNumber, IEnumerable<object?> values)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));

            var columnIndex = 1;
            foreach (var value in values)
            {
                WriteCell(writer, GetCellReference(columnIndex, rowNumber), value);
                columnIndex++;
            }

            writer.WriteEndElement();
        }

        private static void WriteCell(XmlWriter writer, string cellReference, object? value)
        {
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", cellReference);

            if (value != null)
            {
                if (TryGetNumericValue(value, out var numericValue))
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
        {
            var dividend = columnNumber;
            var columnName = string.Empty;

            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                dividend = (dividend - modulo) / 26;
            }

            return columnName + rowNumber.ToString(CultureInfo.InvariantCulture);
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
                var name = sheetCount == 1 ? baseName : $"{baseName} ({i})";
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

        private const string StylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
            "</styleSheet>";
    }
}

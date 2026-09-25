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
    /// properties and the header sits at row 1 — the original behaviour, kept only for
    /// callers that pass no layout at all. With an <see cref="ExcelReportLayout"/> the
    /// sheet opens with the report's header block (title, subtitle, From/To, Exported),
    /// then a bold header row of the layout's own column names, then the data, then the
    /// grid's footer rows.
    ///
    /// A layout may also opt into a grouped table (<see cref="ExcelReportLayout.RowGroupKey"/>,
    /// <see cref="ExcelColumn.MergeWithinRowGroup"/>, <see cref="ExcelColumn.GroupHeader"/>):
    /// a two-row banded header, cells merged down over each row group, and row heights
    /// sized to the wrapped text. A layout that uses none of it writes exactly the sheet
    /// XML it always did.
    ///
    /// Usage: append chunks, optionally <see cref="AppendFooterRows"/>, then
    /// <see cref="Finish"/>. Disposing without Finish leaves an incomplete (unreadable)
    /// archive — the worker deletes the file on failure.
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
        private const int StyleMeta = 7;
        private const int StyleTotalNumber = 8;
        private const int StyleDateTime = 9;
        private const int StyleMoney4 = 10;
        private const int StyleInteger = 11;
        private const int StyleTotalInteger = 12;
        private const int StyleDateUs = 13;
        private const int StyleDateTimeUs = 14;
        private const int StyleMoneyPlain = 15;
        private const int StyleNumberPlain = 16;
        private const int StyleTotalMoneyPlain = 17;
        private const int StyleTotalNumberPlain = 18;
        private const int StyleMoney4Plain = 19;
        private const int StyleTotalMoney4Plain = 20;
        private const int StyleMoneyAsStored = 21;
        private const int StyleTotalMoneyAsStored = 22;
        private const int StyleWrapTop = 23;          // wrapped, top-aligned, bordered text
        private const int StyleNumberTop = 24;        // a grouped table's "No": centered, top, bordered
        private const int StyleHeaderBordered = 25;   // a grouped table's header cells

        // Calibri 11's default row height, and the taller line a Myanmar-script run needs.
        private const double LineHeightPoints = 15d;
        private const double MyanmarLineHeightPoints = 22d;

        // A group larger than this (a misused key) is written in slices instead of held in
        // memory; each slice re-prints the group's values, like a sheet rollover does.
        private const int MaxBufferedGroupRows = 5_000;

        private readonly ZipArchive _archive;
        private readonly string _worksheetBaseName;
        private readonly ExcelReportLayout _layout;
        private readonly int _maxRowsPerSheet;
        private readonly string[] _titleLines;
        private readonly ExcelHeaderLine[] _headerBlock;
        private readonly ExcelColumn[]? _columns;   // null → legacy reflection mode
        private readonly ExcelReportSection[] _sections;
        private readonly int _preambleRows;         // title lines + header block, per sheet
        private readonly int _headerRowIndex;       // the column header row; = _preambleRows in section mode
        private readonly double?[] _widths;
        private readonly double[]? _totals;         // one slot per column; null when no totals row
        private readonly Func<object, object?>? _groupKey; // grouped table; null → every row independent
        private readonly bool _twoRowHeader;        // some column carries a GroupHeader band
        private readonly bool _tableMode;           // bordered header: two-row header or row groups
        private readonly int[] _mergeColumns;       // column indexes merged down over each row group

        private ExcelColumn[]? _activeColumns;      // the section currently being written
        private PropertyInfo[]? _properties;        // legacy mode only
        private XmlWriter? _sheetWriter;
        private Stream? _sheetStream;
        private List<string> _mergeRefs = new();    // merged bands on the CURRENT sheet
        private int _sheetCount;
        private int _rowInSheet;       // 1-based row number within the current sheet
        private long _totalDataRows;   // also the running ordinal behind the "No" column
        private long _sectionRows;     // rows in the active section (its own "No" ordinal)
        private int _activeSection = -1;
        private bool _footerWritten;

        // Grouped table state. The current group's rows are buffered so their heights can
        // be set once the group is complete (Excel never auto-fits a merged cell).
        private readonly List<object> _pendingGroup = new();
        private List<(int Start, int End)> _groupRuns = new(); // merged row ranges on the CURRENT sheet
        private object? _currentGroupKey;
        private bool _groupOpen;
        private bool _groupFirstRowWritten;
        private long _groupOrdinal;

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
            _headerBlock = _layout.HeaderBlock.ToArray();
            _columns = _layout.HasExplicitColumns ? _layout.Columns.ToArray() : null;
            _sections = _layout.Sections.ToArray();
            _preambleRows = _titleLines.Length + _headerBlock.Length;
            _groupKey = _layout.RowGroupKey;

            var usesGrouping = _groupKey != null
                || _layout.Columns.Any(c => c.GroupHeader != null || c.MergeWithinRowGroup)
                || _sections.Any(section => section.Columns.Any(c => c.GroupHeader != null || c.MergeWithinRowGroup));
            if (usesGrouping && (_columns == null || HasSections))
            {
                throw new ArgumentException(
                    "Row groups and banded headers need a single-grid layout with explicit Columns (no Sections).",
                    nameof(layout));
            }

            _twoRowHeader = _columns != null && _columns.Any(c => c.GroupHeader != null);
            _tableMode = _twoRowHeader || _groupKey != null;
            _mergeColumns = _groupKey != null && _columns != null
                ? Enumerable.Range(0, _columns.Length).Where(i => _columns[i].MergeWithinRowGroup).ToArray()
                : Array.Empty<int>();

            // A composite sheet writes one header row per section instead of a single
            // global one, so nothing but the preamble is frozen. A banded header is two rows.
            _headerRowIndex = HasSections ? _preambleRows : _preambleRows + (_twoRowHeader ? 2 : 1);
            _activeColumns = _columns;
            _widths = BuildWidths();
            _totals = _layout.TotalsRowLabel != null && _columns != null && _columns.Any(c => c.IncludeInTotals)
                ? new double[_columns.Length]
                : null;
        }

        public int SheetCount => _sheetCount;

        /// <summary>Data rows only — preamble, header, section and footer rows are excluded.</summary>
        public long TotalDataRows => _totalDataRows;

        private bool HasSections => _sections.Length > 0;

        private bool IsLayoutMode => _columns != null || HasSections;

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

                if (!IsLayoutMode)
                {
                    _properties ??= GetExportProperties(row.GetType());
                }
                else if (HasSections && _activeSection < 0)
                {
                    // A composite report that never called BeginSection still gets its
                    // first table's headers instead of a naked block of values.
                    BeginSection(0);
                }

                if (_groupKey != null)
                {
                    AppendGroupedRow(row);
                    continue;
                }

                if (_sheetWriter == null || _rowInSheet >= _maxRowsPerSheet)
                {
                    StartNewSheet();
                }

                _rowInSheet++;
                // Incremented before the write so the "No" column is 1-based, and never
                // reset by a rollover so it keeps counting across sheets.
                _totalDataRows++;
                _sectionRows++;
                WriteDataRow(_sheetWriter!, _rowInSheet, row);
            }
        }

        /// <summary>
        /// Switches to a composite sheet's <paramref name="sectionIndex"/>-th table: writes
        /// a spacer, the section title and the section's own header row, and restarts that
        /// section's row numbering.
        /// </summary>
        public void BeginSection(int sectionIndex)
        {
            if (!HasSections)
            {
                return;
            }

            if (sectionIndex < 0 || sectionIndex >= _sections.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sectionIndex),
                    sectionIndex,
                    $"The layout declares {_sections.Length} section(s); WriteRowsAsync asked for section {sectionIndex}.");
            }

            if (_sheetWriter == null)
            {
                StartNewSheet();
            }
            else if (_activeSection >= 0)
            {
                WriteBlankRow();
            }

            var section = _sections[sectionIndex];
            _activeSection = sectionIndex;
            _activeColumns = section.Columns.ToArray();
            _sectionRows = 0;

            if (!string.IsNullOrWhiteSpace(section.Title))
            {
                _rowInSheet++;
                WriteSingleCellRow(_sheetWriter!, _rowInSheet, section.Title, StyleTitle);
                TrackMerge(_rowInSheet);
            }

            _rowInSheet++;
            WriteHeaderRow(_sheetWriter!, _rowInSheet);
        }

        /// <summary>
        /// A trailing single-cell line under the data, e.g. a composite page's
        /// "Total USD Value: 1,234.5678".
        /// </summary>
        public void AppendNote(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            FlushGroup();

            if (_sheetWriter == null)
            {
                StartNewSheet();
            }

            WriteBlankRow();
            _rowInSheet++;
            WriteSingleCellRow(_sheetWriter!, _rowInSheet, text, StyleTotalLabel);
        }

        /// <summary>
        /// Writes the grid's own footer rows (see <see cref="ExcelFooterBuilder"/>) and
        /// suppresses the layout's summed <c>TotalsRowLabel</c> row — the two would
        /// otherwise disagree, and the grid's numbers are the ones users reconcile against.
        /// Call it after the last chunk and before <see cref="Finish"/>.
        /// </summary>
        public void AppendFooterRows(IReadOnlyList<ExcelFooterRow> footerRows)
        {
            // Claimed even for an EMPTY footer: once the grid's own totals have been
            // consulted they are the answer, and "the grid shows no footer" must not turn
            // into the layout's self-summed row under a different column.
            _footerWritten = true;

            // The last group's rows are still buffered; they go above the footer.
            FlushGroup();

            if (footerRows == null || footerRows.Count == 0)
            {
                return;
            }

            if (_sheetWriter == null)
            {
                StartNewSheet();
            }

            var columnCount = _activeColumns?.Length ?? ColumnCount();

            foreach (var footerRow in footerRows)
            {
                if (_rowInSheet >= _maxRowsPerSheet)
                {
                    StartNewSheet();
                }

                _rowInSheet++;
                _sheetWriter!.WriteStartElement("row");
                _sheetWriter.WriteAttributeString("r", _rowInSheet.ToString(CultureInfo.InvariantCulture));

                for (var i = 0; i < columnCount; i++)
                {
                    var cell = i < footerRow.Cells.Count ? footerRow.Cells[i] : null;
                    var reference = GetCellReference(i + 1, _rowInSheet);

                    if (cell == null)
                    {
                        WriteCell(_sheetWriter, reference, null, ExcelCellFormat.Text, StyleTotalLabel);
                        continue;
                    }

                    WriteCell(_sheetWriter, reference, cell.Value, cell.Format, FooterStyleFor(cell.Format));
                }

                _sheetWriter.WriteEndElement();
            }
        }

        /// <summary>Closes the current sheet and writes the workbook manifest parts.</summary>
        public void Finish()
        {
            FlushGroup();

            // Ensure at least one (header only) sheet exists.
            if (_sheetWriter == null)
            {
                if (!IsLayoutMode)
                {
                    _properties ??= Array.Empty<PropertyInfo>();
                }

                StartNewSheet();
            }

            if (!_footerWritten)
            {
                WriteTotalsRow();
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
            _mergeRefs = new List<string>();
            _groupRuns = new List<(int Start, int End)>();
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

            // CT_Worksheet sequence: sheetViews, cols, sheetData, mergeCells.
            WriteSheetViews(_sheetWriter);
            WriteColumnWidths(_sheetWriter);

            _sheetWriter.WriteStartElement("sheetData");

            // The header block and the header row repeat on every sheet, so a rolled-over
            // file still reads standalone.
            _rowInSheet = 0;

            for (var i = 0; i < _titleLines.Length; i++)
            {
                _rowInSheet++;
                WriteSingleCellRow(_sheetWriter, _rowInSheet, _titleLines[i], StyleTitle);
                TrackMerge(_rowInSheet);
            }

            foreach (var line in _headerBlock)
            {
                _rowInSheet++;
                WriteSingleCellRow(
                    _sheetWriter,
                    _rowInSheet,
                    line.Text,
                    line.Kind == ExcelHeaderLineKind.Meta ? StyleMeta : StyleTitle);

                if (line.IsMerged)
                {
                    TrackMerge(_rowInSheet);
                }
            }

            if (HasSections)
            {
                // Section headers are written by BeginSection; re-emit the active one so a
                // rolled-over sheet is still readable.
                if (_activeSection >= 0)
                {
                    var section = _sections[_activeSection];
                    if (!string.IsNullOrWhiteSpace(section.Title))
                    {
                        _rowInSheet++;
                        WriteSingleCellRow(_sheetWriter, _rowInSheet, section.Title, StyleTitle);
                        TrackMerge(_rowInSheet);
                    }

                    _rowInSheet++;
                    WriteHeaderRow(_sheetWriter, _rowInSheet);
                }

                return;
            }

            if (_twoRowHeader)
            {
                WriteBandedHeaderRows(_sheetWriter, _rowInSheet + 1);
                _rowInSheet += 2;
                return;
            }

            _rowInSheet++;
            WriteHeaderRow(_sheetWriter, _rowInSheet);
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

        /// <summary>
        /// Freezes the header block (and the column header row) so they stay visible
        /// while the user scrolls a 100k-row export.
        /// </summary>
        private void WriteSheetViews(XmlWriter writer)
        {
            if (!_layout.FreezeHeader || !IsLayoutMode || _headerRowIndex <= 0)
            {
                return;
            }

            writer.WriteStartElement("sheetViews");
            writer.WriteStartElement("sheetView");
            writer.WriteAttributeString("workbookViewId", "0");
            writer.WriteStartElement("pane");
            writer.WriteAttributeString("ySplit", _headerRowIndex.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString(
                "topLeftCell",
                "A" + (_headerRowIndex + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("activePane", "bottomLeft");
            writer.WriteAttributeString("state", "frozen");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        /// <summary>Widest declared width per column index across the grid and every section.</summary>
        private double?[] BuildWidths()
        {
            var count = Math.Max(
                _columns?.Length ?? 0,
                _sections.Length == 0 ? 0 : _sections.Max(section => section.Columns.Count));

            if (count == 0)
            {
                return Array.Empty<double?>();
            }

            var widths = new double?[count];

            void Merge(IReadOnlyList<ExcelColumn> columns)
            {
                for (var i = 0; i < columns.Count; i++)
                {
                    var width = columns[i].Width;
                    if (width is > 0 && (widths[i] == null || width > widths[i]))
                    {
                        widths[i] = width;
                    }
                }
            }

            if (_columns != null)
            {
                Merge(_columns);
            }

            foreach (var section in _sections)
            {
                Merge(section.Columns);
            }

            return widths;
        }

        private void WriteColumnWidths(XmlWriter writer)
        {
            if (_widths.Length == 0 || !_widths.Any(width => width is > 0))
            {
                return;
            }

            writer.WriteStartElement("cols");
            for (var i = 0; i < _widths.Length; i++)
            {
                var width = _widths[i] is > 0 ? _widths[i]!.Value : 18d;

                writer.WriteStartElement("col");
                writer.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("width", width.ToString("0.##", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("customWidth", "1");
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        private void TrackMerge(int rowNumber)
        {
            var columnCount = ColumnCount();
            if (!_layout.MergeTitleAcrossColumns || columnCount < 2)
            {
                return;
            }

            var row = rowNumber.ToString(CultureInfo.InvariantCulture);
            _mergeRefs.Add($"A{row}:{GetColumnName(columnCount)}{row}");
        }

        /// <summary>
        /// Merged title/heading bands. <c>mergeCells</c> follows <c>sheetData</c> in the
        /// CT_Worksheet sequence, so this is emitted while closing the sheet rather
        /// than next to the title rows themselves.
        /// </summary>
        private void WriteMergedTitleCells(XmlWriter writer)
        {
            // Group runs are kept as row pairs and only expanded here, so a long sheet of
            // small groups doesn't hold a string per merged cell.
            var count = _mergeRefs.Count + _groupRuns.Count * _mergeColumns.Length;
            if (count == 0)
            {
                return;
            }

            writer.WriteStartElement("mergeCells");
            writer.WriteAttributeString("count", count.ToString(CultureInfo.InvariantCulture));
            foreach (var reference in _mergeRefs)
            {
                writer.WriteStartElement("mergeCell");
                writer.WriteAttributeString("ref", reference);
                writer.WriteEndElement();
            }

            foreach (var (start, end) in _groupRuns)
            {
                foreach (var columnIndex in _mergeColumns)
                {
                    writer.WriteStartElement("mergeCell");
                    writer.WriteAttributeString("ref", MergeReference(columnIndex + 1, start, columnIndex + 1, end));
                    writer.WriteEndElement();
                }
            }

            writer.WriteEndElement();
        }

        private static string MergeReference(int firstColumn, int firstRow, int lastColumn, int lastRow)
            => GetCellReference(firstColumn, firstRow) + ":" + GetCellReference(lastColumn, lastRow);

        private void WriteHeaderRow(XmlWriter writer, int rowNumber)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));

            if (_activeColumns != null)
            {
                for (var i = 0; i < _activeColumns.Length; i++)
                {
                    WriteCell(
                        writer,
                        GetCellReference(i + 1, rowNumber),
                        _activeColumns[i].Header,
                        ExcelCellFormat.Text,
                        _tableMode ? StyleHeaderBordered : StyleHeader);
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

        /// <summary>
        /// The two-row header of a layout with <see cref="ExcelColumn.GroupHeader"/> bands:
        /// each band spans its run of columns on the first row with the leaf titles under
        /// it; every other header cell is merged down across both rows. The covered cells
        /// are written empty but styled, so the borders draw.
        /// </summary>
        private void WriteBandedHeaderRows(XmlWriter writer, int firstRow)
        {
            var columns = _columns!;
            var secondRow = firstRow + 1;

            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", firstRow.ToString(CultureInfo.InvariantCulture));
            for (var i = 0; i < columns.Length; i++)
            {
                var band = columns[i].GroupHeader;
                var reference = GetCellReference(i + 1, firstRow);

                if (band == null)
                {
                    WriteCell(writer, reference, columns[i].Header, ExcelCellFormat.Text, StyleHeaderBordered);
                    _mergeRefs.Add(MergeReference(i + 1, firstRow, i + 1, secondRow));
                }
                else if (i == 0 || !string.Equals(columns[i - 1].GroupHeader, band, StringComparison.Ordinal))
                {
                    var last = i;
                    while (last + 1 < columns.Length
                        && string.Equals(columns[last + 1].GroupHeader, band, StringComparison.Ordinal))
                    {
                        last++;
                    }

                    WriteCell(writer, reference, band, ExcelCellFormat.Text, StyleHeaderBordered);
                    if (last > i)
                    {
                        _mergeRefs.Add(MergeReference(i + 1, firstRow, last + 1, firstRow));
                    }
                }
                else
                {
                    WriteCell(writer, reference, null, ExcelCellFormat.Text, StyleHeaderBordered);
                }
            }

            writer.WriteEndElement();

            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", secondRow.ToString(CultureInfo.InvariantCulture));
            for (var i = 0; i < columns.Length; i++)
            {
                WriteCell(
                    writer,
                    GetCellReference(i + 1, secondRow),
                    columns[i].GroupHeader == null ? null : columns[i].Header,
                    ExcelCellFormat.Text,
                    StyleHeaderBordered);
            }

            writer.WriteEndElement();
        }

        /// <summary>Buffers one row of a grouped table; a new key writes out the previous group.</summary>
        private void AppendGroupedRow(object row)
        {
            var key = _groupKey!(row);
            var sameGroup = _groupOpen && key != null && Equals(key, _currentGroupKey);

            if (!sameGroup)
            {
                FlushGroup();
                _groupOrdinal++;
                _currentGroupKey = key;
                _groupOpen = true;
                _groupFirstRowWritten = false;
            }
            else if (_pendingGroup.Count >= MaxBufferedGroupRows)
            {
                FlushGroup();
            }

            _pendingGroup.Add(row);

            // Counted on arrival: the footer builder reads TotalDataRows before Finish.
            _totalDataRows++;
        }

        /// <summary>
        /// Writes the buffered group, split at sheet boundaries. Every slice prints the
        /// merged columns on its first row, so a group spilling onto a new sheet still
        /// reads standalone there, with the same "No".
        /// </summary>
        private void FlushGroup()
        {
            if (_pendingGroup.Count == 0)
            {
                return;
            }

            var index = 0;
            while (index < _pendingGroup.Count)
            {
                if (_sheetWriter == null || _rowInSheet >= _maxRowsPerSheet)
                {
                    StartNewSheet();
                }

                var take = Math.Max(1, Math.Min(_maxRowsPerSheet - _rowInSheet, _pendingGroup.Count - index));
                WriteGroupSlice(index, take);
                index += take;
            }

            _pendingGroup.Clear();
        }

        private void WriteGroupSlice(int offset, int count)
        {
            var columns = _columns!;
            var writer = _sheetWriter!;

            // Merged columns are read from the slice's first row only.
            var values = new object?[count][];
            for (var r = 0; r < count; r++)
            {
                values[r] = new object?[columns.Length];
                for (var c = 0; c < columns.Length; c++)
                {
                    if (r == 0 || !columns[c].MergeWithinRowGroup)
                    {
                        values[r][c] = columns[c].GetValue(_pendingGroup[offset + r], _groupOrdinal);
                    }
                }
            }

            var heights = GroupRowHeights(columns, values);
            var firstRow = _rowInSheet + 1;

            for (var r = 0; r < count; r++)
            {
                _rowInSheet++;
                writer.WriteStartElement("row");
                writer.WriteAttributeString("r", _rowInSheet.ToString(CultureInfo.InvariantCulture));
                if (heights[r] > LineHeightPoints)
                {
                    writer.WriteAttributeString("ht", heights[r].ToString("0.##", CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("customHeight", "1");
                }

                for (var c = 0; c < columns.Length; c++)
                {
                    var column = columns[c];
                    var style = column.IsRowNumber ? StyleNumberTop : StyleFor(column.Format);
                    var reference = GetCellReference(c + 1, _rowInSheet);

                    if (r > 0 && column.MergeWithinRowGroup)
                    {
                        WriteCell(writer, reference, null, column.Format, style);
                        continue;
                    }

                    // A merged value is one number per group, even when a slice re-prints it.
                    if (_totals != null && column.IncludeInTotals
                        && (!column.MergeWithinRowGroup || !_groupFirstRowWritten))
                    {
                        _totals[c] += ToDoubleOrZero(values[r][c]);
                    }

                    WriteCell(writer, reference, values[r][c], column.Format, style);
                }

                writer.WriteEndElement();
                _groupFirstRowWritten = true;
            }

            if (count > 1 && _mergeColumns.Length > 0)
            {
                _groupRuns.Add((firstRow, firstRow + count - 1));
            }
        }

        /// <summary>
        /// Heights for one slice: each row fits its own wrapped cells, and the merged cells'
        /// text — which Excel will not auto-fit — is spread over the slice's rows.
        /// </summary>
        private double[] GroupRowHeights(ExcelColumn[] columns, object?[][] values)
        {
            var count = values.Length;
            var heights = new double[count];
            var mergedNeed = LineHeightPoints;

            for (var r = 0; r < count; r++)
            {
                heights[r] = LineHeightPoints;
                for (var c = 0; c < columns.Length; c++)
                {
                    var need = EstimateCellHeight(values[r][c], columns[c]);
                    if (columns[c].MergeWithinRowGroup)
                    {
                        mergedNeed = Math.Max(mergedNeed, need);
                    }
                    else
                    {
                        heights[r] = Math.Max(heights[r], need);
                    }
                }
            }

            var shortfall = mergedNeed - heights.Sum();
            if (shortfall > 0)
            {
                for (var r = 0; r < count; r++)
                {
                    heights[r] += shortfall / count;
                }
            }

            for (var r = 0; r < count; r++)
            {
                heights[r] = Math.Ceiling(heights[r] * 4) / 4; // quarter points, like Excel stores them
            }

            return heights;
        }

        /// <summary>
        /// Approximate height of a wrapped cell: lines per "\n"-separated part at the
        /// column's width (a width unit is one '0'; capitals run ~10% wider).
        /// </summary>
        private static double EstimateCellHeight(object? value, ExcelColumn column)
        {
            if (column.Format != ExcelCellFormat.WrappedText || value is not string text || text.Length == 0)
            {
                return LineHeightPoints;
            }

            var charsPerLine = Math.Max(1d, (column.Width ?? 18d) - 1d);
            var height = 0d;

            foreach (var part in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var lines = Math.Max(1d, Math.Ceiling(part.Length * 1.1d / charsPerLine));
                var myanmar = part.Any(ch => ch >= '\u1000' && ch <= '\u109F');
                height += lines * (myanmar ? MyanmarLineHeightPoints : LineHeightPoints);
            }

            return height;
        }

        private void WriteBlankRow()
        {
            if (_rowInSheet >= _maxRowsPerSheet)
            {
                StartNewSheet();
                return;
            }

            _rowInSheet++;
            _sheetWriter!.WriteStartElement("row");
            _sheetWriter.WriteAttributeString("r", _rowInSheet.ToString(CultureInfo.InvariantCulture));
            _sheetWriter.WriteEndElement();
        }

        private void WriteDataRow(XmlWriter writer, int rowNumber, object row)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));

            if (_activeColumns != null)
            {
                var ordinal = HasSections ? _sectionRows : _totalDataRows;

                for (var i = 0; i < _activeColumns.Length; i++)
                {
                    var column = _activeColumns[i];
                    var value = column.GetValue(row, ordinal);

                    if (_totals != null && _columns != null && ReferenceEquals(_activeColumns, _columns) && column.IncludeInTotals)
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
        /// The layout's own summed totals row, written once after the last data row when
        /// no <see cref="AppendFooterRows"/> footer was supplied. Sums are accumulated
        /// while streaming, so they always describe exactly the rows in the file. Skipped
        /// for an empty export.
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
                    // The total prints through the COLUMN's own format, not a hard-coded
                    // "#,##0.00" — otherwise a column of 3000 sits above a total reading
                    // 3,000.00. FooterStyleFor is the same mapping the typed footer uses.
                    var format = _columns[i].Format;
                    WriteCell(_sheetWriter, reference, _totals[i], format, FooterStyleFor(format));
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

        private int ColumnCount()
        {
            if (_activeColumns != null)
            {
                return Math.Max(_activeColumns.Length, _widths.Length);
            }

            return _widths.Length > 0 ? _widths.Length : _properties?.Length ?? 0;
        }

        private static int StyleFor(ExcelCellFormat format) => format switch
        {
            ExcelCellFormat.Date => StyleDate,
            ExcelCellFormat.DateTime => StyleDateTime,
            ExcelCellFormat.Money => StyleMoney,
            ExcelCellFormat.Money4 => StyleMoney4,
            ExcelCellFormat.Integer => StyleInteger,
            ExcelCellFormat.DateUs => StyleDateUs,
            ExcelCellFormat.DateTimeUs => StyleDateTimeUs,
            ExcelCellFormat.MoneyPlain => StyleMoneyPlain,
            ExcelCellFormat.NumberPlain => StyleNumberPlain,
            ExcelCellFormat.Money4Plain => StyleMoney4Plain,
            ExcelCellFormat.MoneyAsStored => StyleMoneyAsStored,
            ExcelCellFormat.WrappedText => StyleWrapTop,
            _ => StyleDefault,
        };

        private static int FooterStyleFor(ExcelCellFormat format) => format switch
        {
            ExcelCellFormat.Money or ExcelCellFormat.Money4 => StyleTotalMoney,
            ExcelCellFormat.Number => StyleTotalNumber,
            ExcelCellFormat.Integer => StyleTotalInteger,
            ExcelCellFormat.MoneyPlain => StyleTotalMoneyPlain,
            ExcelCellFormat.NumberPlain => StyleTotalNumberPlain,
            ExcelCellFormat.Money4Plain => StyleTotalMoney4Plain,
            ExcelCellFormat.MoneyAsStored => StyleTotalMoneyAsStored,
            _ => StyleTotalLabel,
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
                if (format is ExcelCellFormat.Date or ExcelCellFormat.DateTime
                        or ExcelCellFormat.DateUs or ExcelCellFormat.DateTimeUs
                    && TryGetDateSerial(value, out var serial))
                {
                    writer.WriteElementString("v", serial);
                }
                else if (format is not (ExcelCellFormat.Text or ExcelCellFormat.WrappedText)
                    && TryGetNumericValue(value, out var numericValue))
                {
                    writer.WriteElementString("v", numericValue);
                }
                else
                {
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is");
                    writer.WriteStartElement("t");
                    if (format == ExcelCellFormat.WrappedText)
                    {
                        WriteMultilineText(writer, FormatValue(value));
                    }
                    else
                    {
                        writer.WriteString(FormatValue(value));
                    }

                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
            }

            writer.WriteEndElement();
        }

        /// <summary>
        /// Line breaks as "&amp;#xA;" character entities, so they survive whatever newline
        /// handling the XmlWriter or the reading parser applies.
        /// </summary>
        private static void WriteMultilineText(XmlWriter writer, string text)
        {
            writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");

            var parts = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    writer.WriteCharEntity('\n');
                }

                writer.WriteString(parts[i]);
            }
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
            "<numFmts count=\"11\">" +
            "<numFmt numFmtId=\"164\" formatCode=\"dd/mm/yyyy\"/>" +
            "<numFmt numFmtId=\"165\" formatCode=\"#,##0.00\"/>" +
            "<numFmt numFmtId=\"166\" formatCode=\"yyyy-mm-dd hh:mm:ss\"/>" +
            "<numFmt numFmtId=\"167\" formatCode=\"#,##0.0000\"/>" +
            "<numFmt numFmtId=\"168\" formatCode=\"#,##0\"/>" +
            // The Payment reports: US date order, and amounts with no thousands separators.
            "<numFmt numFmtId=\"169\" formatCode=\"mm/dd/yyyy\"/>" +
            "<numFmt numFmtId=\"170\" formatCode=\"mm/dd/yyyy hh:mm:ss\"/>" +
            "<numFmt numFmtId=\"171\" formatCode=\"0.00\"/>" +
            "<numFmt numFmtId=\"172\" formatCode=\"0\"/>" +
            "<numFmt numFmtId=\"173\" formatCode=\"0.0000\"/>" +
            // 2026-09-17, ငွေစာရင်း: as stored, no padding. '#' is Excel's optional digit.
            "<numFmt numFmtId=\"174\" formatCode=\"0.####\"/>" +
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
            "<borders count=\"2\">" +
            "<border><left/><right/><top/><bottom/><diagonal/></border>" +
            // 1 thin all round — the grouped table's grid (styles 23-25)
            "<border><left style=\"thin\"><color auto=\"1\"/></left><right style=\"thin\"><color auto=\"1\"/></right>" +
            "<top style=\"thin\"><color auto=\"1\"/></top><bottom style=\"thin\"><color auto=\"1\"/></bottom><diagonal/></border>" +
            "</borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"26\">" +
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
            // 7 meta (From Date / To Date / Exported) — bold 11, left, unmerged
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\">" +
            "<alignment horizontal=\"left\" vertical=\"center\"/></xf>" +
            // 8 totals plain number
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
            // 9 date + time
            "<xf numFmtId=\"166\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            // 10 money with 4 decimals
            "<xf numFmtId=\"167\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            // 11 whole number with thousands separators (rdlc N0)
            "<xf numFmtId=\"168\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            // 12 totals whole number (rdlc N0, bold)
            "<xf numFmtId=\"168\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\"/>" +
            // 13 date, US order (Payment)
            "<xf numFmtId=\"169\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            // 14 date + time, US order (Payment)
            "<xf numFmtId=\"170\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            // 15 money, no thousands separators (Payment)
            "<xf numFmtId=\"171\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            // 16 whole number, no thousands separators (Payment)
            "<xf numFmtId=\"172\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            // 17 totals money, no thousands separators (Payment, bold)
            "<xf numFmtId=\"171\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\"/>" +
            // 18 totals whole number, no thousands separators (Payment, bold)
            "<xf numFmtId=\"172\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\"/>" +
            // 19 money to 4 decimals, no thousands separators (Payment)
            "<xf numFmtId=\"173\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            // 20 totals money to 4 decimals, no thousands separators (Payment, bold)
            "<xf numFmtId=\"173\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\"/>" +
            // 21 money as stored, no padding, no thousands separators (ငွေစာရင်း)
            "<xf numFmtId=\"174\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            // 22 totals money as stored (ငွေစာရင်း, bold)
            "<xf numFmtId=\"174\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\"/>" +
            // 23 wrapped multi-line text, top-aligned, bordered (grouped table body)
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\">" +
            "<alignment vertical=\"top\" wrapText=\"1\"/></xf>" +
            // 24 a grouped table's "No": centered, top-aligned, bordered
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\">" +
            "<alignment horizontal=\"center\" vertical=\"top\"/></xf>" +
            // 25 a grouped table's header: the header style with a border
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\" applyAlignment=\"1\">" +
            "<alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
            "</cellXfs>" +
            "</styleSheet>";
    }
}

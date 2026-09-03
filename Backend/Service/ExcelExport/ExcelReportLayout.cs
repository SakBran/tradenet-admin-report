using System;
using System.Collections.Generic;
using System.Globalization;

namespace API.Service.ExcelExport
{
    /// <summary>How a cell is typed and formatted in the .xlsx.</summary>
    public enum ExcelCellFormat
    {
        /// <summary>Type inferred from the runtime value — the legacy reflection behaviour.</summary>
        General = 0,

        /// <summary>Always an inline string.</summary>
        Text = 1,

        /// <summary>Numeric cell, general number format.</summary>
        Number = 2,

        /// <summary>Numeric cell displayed as "#,##0.00".</summary>
        Money = 3,

        /// <summary>A real Excel date serial displayed as "dd/mm/yyyy" — sortable and filterable.</summary>
        Date = 4,

        /// <summary>A real Excel date serial displayed as "yyyy-mm-dd hh:mm:ss".</summary>
        DateTime = 5,

        /// <summary>Numeric cell displayed as "#,##0.0000" (the RDLC N4 money columns).</summary>
        Money4 = 6,
    }

    /// <summary>Where one preamble line sits in the sheet's header block.</summary>
    public enum ExcelHeaderLineKind
    {
        /// <summary>The report title — merged across the sheet, bold 14, centered.</summary>
        Title = 0,

        /// <summary>An RDLC heading/subtitle line — same style as the title.</summary>
        Heading = 1,

        /// <summary>A left-aligned bold 11 single-cell line: "From Date: …", "Exported: …".</summary>
        Meta = 2,
    }

    /// <summary>One line of the sheet's header block.</summary>
    public sealed record ExcelHeaderLine(string Text, ExcelHeaderLineKind Kind)
    {
        public static ExcelHeaderLine Title(string text) => new(text, ExcelHeaderLineKind.Title);

        public static ExcelHeaderLine Heading(string text) => new(text, ExcelHeaderLineKind.Heading);

        public static ExcelHeaderLine Meta(string text) => new(text, ExcelHeaderLineKind.Meta);

        /// <summary>Title and Heading lines are merged across the full column width.</summary>
        public bool IsMerged => Kind != ExcelHeaderLineKind.Meta;
    }

    /// <summary>Which column keys carry the per-currency footer label and value.</summary>
    public sealed record ExcelCurrencyTotalsColumns(string LabelColumnKey, string ValueColumnKey);

    /// <summary>
    /// One Excel column: the header text shown to the user, how the cell is typed,
    /// and how to pull the value out of a row.
    /// </summary>
    public sealed class ExcelColumn
    {
        // (row, ordinal) → value. The ordinal is the 1-based running row number across
        // the whole export (every chunk, every sheet), which is what the "No" column emits.
        private readonly Func<object, long, object?> _value;
        private readonly bool _isNumeric;

        private ExcelColumn(
            string header,
            ExcelCellFormat format,
            double? width,
            bool includeInTotals,
            Func<object, long, object?> value,
            bool isRowNumber = false,
            bool? isNumeric = null,
            string? key = null,
            string? dataIndex = null)
        {
            Header = header ?? string.Empty;
            Format = format;
            Width = width;
            IncludeInTotals = includeInTotals;
            _value = value;
            IsRowNumber = isRowNumber;
            _isNumeric = isNumeric ?? IsNumericFormat(format);
            Key = key;
            DataIndex = dataIndex;
        }

        public string Header { get; }

        public ExcelCellFormat Format { get; }

        /// <summary>Column width in Excel character units; null leaves it at the default.</summary>
        public double? Width { get; }

        /// <summary>Summed into the totals row at the bottom of the sheet.</summary>
        public bool IncludeInTotals { get; }

        /// <summary>
        /// The UI config column key. <see cref="ExcelReportLayout.CurrencyTotalsColumns"/>
        /// points at these, so the footer builder can find the label/value columns.
        /// </summary>
        public string? Key { get; }

        /// <summary>
        /// The camelCase JSON property this column reads. <c>ColumnTotals</c> is keyed by
        /// it, so the footer builder places each total under the right column.
        /// </summary>
        public string? DataIndex { get; }

        /// <summary>The grid's "No" column — never a data column, never totalled.</summary>
        public bool IsRowNumber { get; }

        /// <summary>
        /// Mirrors the grid's <c>isNumericColumn</c> (dataType number|money): drives the
        /// right-aligned/numeric footer cells and the fallback currency-totals placement.
        /// </summary>
        public bool IsNumeric => !IsRowNumber && _isNumeric;

        internal object? GetValue(object row, long ordinal) => _value(row, ordinal);

        /// <summary>
        /// Attaches the UI identifiers so footer totals (keyed by <paramref name="dataIndex"/>)
        /// and the currency-totals placement (keyed by <paramref name="key"/>) can be placed.
        /// Returns a copy — <see cref="ExcelColumn"/> is immutable.
        /// </summary>
        public ExcelColumn Bind(string? key, string? dataIndex)
            => new(Header, Format, Width, IncludeInTotals, _value, IsRowNumber, _isNumeric, key, dataIndex);

        /// <summary>
        /// The grid's "No" column: 1..N, continuing across chunk boundaries and sheet
        /// rollovers, matching the old RDLC's <c>=RowNumber(nothing)</c>.
        /// </summary>
        public static ExcelColumn RowNumber(string header = "No", double? width = 6)
            => new(header, ExcelCellFormat.Number, width, false, static (_, ordinal) => ordinal, isRowNumber: true);

        public static ExcelColumn Text<TRow>(string header, Func<TRow, object?> selector, double? width = null)
            => Create(header, ExcelCellFormat.Text, width, false, selector);

        public static ExcelColumn Number<TRow>(
            string header, Func<TRow, object?> selector, double? width = null, bool includeInTotals = false)
            => Create(header, ExcelCellFormat.Number, width, includeInTotals, selector);

        public static ExcelColumn Money<TRow>(
            string header, Func<TRow, object?> selector, double? width = 16, bool includeInTotals = false)
            => Create(header, ExcelCellFormat.Money, width, includeInTotals, selector);

        /// <summary>A money column shown with 4 decimals (RDLC <c>FORMAT(…, "N4")</c>).</summary>
        public static ExcelColumn Money4<TRow>(
            string header, Func<TRow, object?> selector, double? width = 18, bool includeInTotals = false)
            => Create(header, ExcelCellFormat.Money4, width, includeInTotals, selector);

        public static ExcelColumn Date<TRow>(string header, Func<TRow, System.DateTime?> selector, double? width = 12)
        {
            ArgumentNullException.ThrowIfNull(selector);

            return Create<TRow>(header, ExcelCellFormat.Date, width, false, row => selector(row));
        }

        /// <summary>A date + time column ("yyyy-mm-dd hh:mm:ss").</summary>
        public static ExcelColumn Timestamp<TRow>(string header, Func<TRow, System.DateTime?> selector, double? width = 20)
        {
            ArgumentNullException.ThrowIfNull(selector);

            return Create<TRow>(header, ExcelCellFormat.DateTime, width, false, row => selector(row));
        }

        /// <summary>
        /// A column whose value comes from a selector that accepts any row object — what
        /// <see cref="ExcelLayoutBuilder"/> builds from the posted presentation spec, where
        /// the row type is only known at runtime.
        /// </summary>
        public static ExcelColumn Untyped(
            string header,
            ExcelCellFormat format,
            Func<object, long, object?> selector,
            double? width = null,
            bool includeInTotals = false,
            bool? isNumeric = null,
            string? key = null,
            string? dataIndex = null)
        {
            ArgumentNullException.ThrowIfNull(selector);

            return new ExcelColumn(
                header, format, width, includeInTotals, selector,
                isRowNumber: false, isNumeric: isNumeric, key: key, dataIndex: dataIndex);
        }

        /// <summary>
        /// A header with a permanently blank body — RDLC parity for a column that was
        /// never bound to a field (e.g. Account Summary's "Remark").
        /// </summary>
        public static ExcelColumn Blank(string header, double? width = null)
            => new(header, ExcelCellFormat.Text, width, false, static (_, _) => null);

        internal static bool IsNumericFormat(ExcelCellFormat format)
            => format is ExcelCellFormat.Number or ExcelCellFormat.Money or ExcelCellFormat.Money4;

        private static ExcelColumn Create<TRow>(
            string header,
            ExcelCellFormat format,
            double? width,
            bool includeInTotals,
            Func<TRow, object?> selector)
        {
            ArgumentNullException.ThrowIfNull(selector);

            return new ExcelColumn(header, format, width, includeInTotals, (row, _) => selector(Cast<TRow>(row)));
        }

        private static TRow Cast<TRow>(object row)
            => row is TRow typed
                ? typed
                : throw new InvalidOperationException(
                    $"Excel column expected rows of type {typeof(TRow).Name} but got {row.GetType().Name}. " +
                    "The layout's row type must match what WriteRowsAsync appends to the sink.");
    }

    /// <summary>
    /// One table of a composite (multi-table) report sheet. The report switches to it
    /// with <see cref="IExcelRowSink.BeginSection"/> before appending its rows; the
    /// writer emits the section title and its own header row.
    /// </summary>
    public sealed class ExcelReportSection
    {
        public string Title { get; init; } = string.Empty;

        public IReadOnlyList<ExcelColumn> Columns { get; init; } = Array.Empty<ExcelColumn>();
    }

    /// <summary>One footer cell; null cells in a row are left blank.</summary>
    public sealed record ExcelFooterCell(object? Value, ExcelCellFormat Format = ExcelCellFormat.Text);

    /// <summary>
    /// One footer row, positionally aligned with the layout's columns (index 0 = the
    /// leftmost column, including the row-number column when there is one).
    /// </summary>
    public sealed class ExcelFooterRow
    {
        public ExcelFooterRow(IReadOnlyList<ExcelFooterCell?> cells) => Cells = cells;

        public IReadOnlyList<ExcelFooterCell?> Cells { get; }
    }

    /// <summary>
    /// The title line(s) and explicit column list for one export. A report opts in by
    /// implementing <see cref="IExcelReportLayoutProvider"/>; reports that don't declare
    /// one are built from the posted presentation spec instead
    /// (<see cref="ExcelLayoutBuilder.Build"/>).
    /// </summary>
    public sealed class ExcelReportLayout
    {
        /// <summary>No title and no explicit columns — the legacy reflection layout.</summary>
        public static readonly ExcelReportLayout None = new();

        /// <summary>Rendered above the header row, one row each, starting at A1.</summary>
        public IReadOnlyList<string> TitleLines { get; init; } = Array.Empty<string>();

        /// <summary>
        /// The standard header block written after <see cref="TitleLines"/>: title/heading
        /// lines then the From/To (or Date) and Exported meta lines. Filled in for EVERY
        /// export by <see cref="ExcelLayoutBuilder.WithStandardHeaderBlock"/>.
        /// </summary>
        public IReadOnlyList<ExcelHeaderLine> HeaderBlock { get; init; } = Array.Empty<ExcelHeaderLine>();

        /// <summary>Empty → fall back to reflection over the row type's public properties.</summary>
        public IReadOnlyList<ExcelColumn> Columns { get; init; } = Array.Empty<ExcelColumn>();

        /// <summary>Merge each title line across the full column width, RDLC banner style.</summary>
        public bool MergeTitleAcrossColumns { get; init; } = true;

        /// <summary>
        /// Label for the totals row written after the last data row. Null (the default)
        /// means no totals row, even if a column is marked
        /// <see cref="ExcelColumn.IncludeInTotals"/>. Ignored once
        /// <see cref="StreamingExcelWriter.AppendFooterRows"/> has supplied the grid's own
        /// footer rows.
        /// </summary>
        public string? TotalsRowLabel { get; init; }

        /// <summary>
        /// Column keys carrying the per-currency footer rows. Null → the footer builder
        /// falls back to the first non-numeric / first numeric data column.
        /// </summary>
        public ExcelCurrencyTotalsColumns? CurrencyTotalsColumns { get; init; }

        /// <summary>Composite (multi-table) sheets only; empty for the normal one-grid report.</summary>
        public IReadOnlyList<ExcelReportSection> Sections { get; init; } = Array.Empty<ExcelReportSection>();

        /// <summary>Freeze everything above the first data row so the headers stay put while scrolling.</summary>
        public bool FreezeHeader { get; init; } = true;

        internal bool HasExplicitColumns => Columns.Count > 0;

        internal bool HasSections => Sections.Count > 0;

        internal bool HasTitle => TitleLines.Count > 0 || HeaderBlock.Count > 0;

        /// <summary>Copy with the given members replaced; null arguments keep the current value.</summary>
        public ExcelReportLayout With(
            IReadOnlyList<ExcelHeaderLine>? headerBlock = null,
            IReadOnlyList<string>? titleLines = null,
            IReadOnlyList<ExcelColumn>? columns = null,
            ExcelCurrencyTotalsColumns? currencyTotalsColumns = null,
            IReadOnlyList<ExcelReportSection>? sections = null,
            bool? freezeHeader = null,
            string? totalsRowLabel = null,
            bool? mergeTitleAcrossColumns = null)
            => new()
            {
                TitleLines = titleLines ?? TitleLines,
                HeaderBlock = headerBlock ?? HeaderBlock,
                Columns = columns ?? Columns,
                MergeTitleAcrossColumns = mergeTitleAcrossColumns ?? MergeTitleAcrossColumns,
                TotalsRowLabel = totalsRowLabel ?? TotalsRowLabel,
                CurrencyTotalsColumns = currencyTotalsColumns ?? CurrencyTotalsColumns,
                Sections = sections ?? Sections,
                FreezeHeader = freezeHeader ?? FreezeHeader,
            };
    }

    /// <summary>
    /// Builds the report title lines the old Tradenet 2.0 RDLCs passed as the
    /// <c>header1</c> parameter, and that the frontend renders above the grid
    /// (<c>reportDateRangeSubtitle</c> in reportConfigs.ts).
    ///
    /// Always InvariantCulture: in a .NET custom date format "/" is the date-separator
    /// *placeholder* and is replaced by the host culture's separator, so a server whose
    /// culture uses "." would silently emit "31.08.2026".
    /// </summary>
    public static class ExcelReportTitle
    {
        /// <summary>"Account Summary Report (31/08/2026) To (31/08/2026)"</summary>
        public static string DateRange(string reportName, DateTime fromDate, DateTime toDate)
            => string.Format(
                CultureInfo.InvariantCulture,
                "{0} ({1:dd/MM/yyyy}) To ({2:dd/MM/yyyy})",
                reportName,
                fromDate,
                toDate);

        /// <summary>"Import Licence Report From (01/08/2026) To (31/08/2026)"</summary>
        public static string FromDateRange(string reportName, DateTime fromDate, DateTime toDate)
            => string.Format(
                CultureInfo.InvariantCulture,
                "{0} From ({1:dd/MM/yyyy}) To ({2:dd/MM/yyyy})",
                reportName,
                fromDate,
                toDate);
    }
}

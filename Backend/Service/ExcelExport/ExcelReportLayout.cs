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
    }

    /// <summary>
    /// One Excel column: the header text shown to the user, how the cell is typed,
    /// and how to pull the value out of a row.
    /// </summary>
    public sealed class ExcelColumn
    {
        // (row, ordinal) → value. The ordinal is the 1-based running row number across
        // the whole export (every chunk, every sheet), which is what the "No" column emits.
        private readonly Func<object, long, object?> _value;

        private ExcelColumn(
            string header,
            ExcelCellFormat format,
            double? width,
            bool includeInTotals,
            Func<object, long, object?> value)
        {
            Header = header ?? string.Empty;
            Format = format;
            Width = width;
            IncludeInTotals = includeInTotals;
            _value = value;
        }

        public string Header { get; }

        public ExcelCellFormat Format { get; }

        /// <summary>Column width in Excel character units; null leaves it at the default.</summary>
        public double? Width { get; }

        /// <summary>Summed into the totals row at the bottom of the sheet.</summary>
        public bool IncludeInTotals { get; }

        internal object? GetValue(object row, long ordinal) => _value(row, ordinal);

        /// <summary>
        /// The grid's "No" column: 1..N, continuing across chunk boundaries and sheet
        /// rollovers, matching the old RDLC's <c>=RowNumber(nothing)</c>.
        /// </summary>
        public static ExcelColumn RowNumber(string header = "No", double? width = 6)
            => new(header, ExcelCellFormat.Number, width, false, static (_, ordinal) => ordinal);

        public static ExcelColumn Text<TRow>(string header, Func<TRow, object?> selector, double? width = null)
            => Create(header, ExcelCellFormat.Text, width, false, selector);

        public static ExcelColumn Number<TRow>(
            string header, Func<TRow, object?> selector, double? width = null, bool includeInTotals = false)
            => Create(header, ExcelCellFormat.Number, width, includeInTotals, selector);

        public static ExcelColumn Money<TRow>(
            string header, Func<TRow, object?> selector, double? width = 16, bool includeInTotals = false)
            => Create(header, ExcelCellFormat.Money, width, includeInTotals, selector);

        public static ExcelColumn Date<TRow>(string header, Func<TRow, DateTime?> selector, double? width = 12)
        {
            ArgumentNullException.ThrowIfNull(selector);

            return Create<TRow>(header, ExcelCellFormat.Date, width, false, row => selector(row));
        }

        /// <summary>
        /// A header with a permanently blank body — RDLC parity for a column that was
        /// never bound to a field (e.g. Account Summary's "Remark").
        /// </summary>
        public static ExcelColumn Blank(string header, double? width = null)
            => new(header, ExcelCellFormat.Text, width, false, static (_, _) => null);

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
    /// The title line(s) and explicit column list for one export. A report opts in by
    /// implementing <see cref="IExcelReportLayoutProvider"/>; reports that don't keep
    /// the reflection-derived layout (one header row of property names, every public
    /// property as a column).
    /// </summary>
    public sealed class ExcelReportLayout
    {
        /// <summary>No title and no explicit columns — the legacy reflection layout.</summary>
        public static readonly ExcelReportLayout None = new();

        /// <summary>Rendered above the header row, one row each, starting at A1.</summary>
        public IReadOnlyList<string> TitleLines { get; init; } = Array.Empty<string>();

        /// <summary>Empty → fall back to reflection over the row type's public properties.</summary>
        public IReadOnlyList<ExcelColumn> Columns { get; init; } = Array.Empty<ExcelColumn>();

        /// <summary>Merge each title line across the full column width, RDLC banner style.</summary>
        public bool MergeTitleAcrossColumns { get; init; } = true;

        /// <summary>
        /// Label for the totals row written after the last data row. Null (the default)
        /// means no totals row, even if a column is marked
        /// <see cref="ExcelColumn.IncludeInTotals"/>.
        /// </summary>
        public string? TotalsRowLabel { get; init; }

        internal bool HasExplicitColumns => Columns.Count > 0;

        internal bool HasTitle => TitleLines.Count > 0;
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

using System.Collections.Generic;

namespace API.Model.ExcelExport
{
    /// <summary>
    /// What the grid looked like when the user pressed Excel: the report's title, the
    /// exact columns the UI rendered (titles, order, cell types) and where the footer
    /// totals belong. Posted by the frontend alongside the report filters and stored
    /// verbatim in the queued job, so the background worker reproduces the grid instead
    /// of reflecting over the row DTO.
    ///
    /// Serialized with System.Text.Json Web defaults (camelCase, case-insensitive read).
    /// The frontend builds it from ONE object literal so key order — and therefore the
    /// dedup hash — is stable.
    /// </summary>
    public sealed class ExcelPresentationSpec
    {
        /// <summary>Must match <see cref="CurrentFormatVersion"/>'s contract shape.</summary>
        public const int CurrentFormatVersion = 1;

        public int FormatVersion { get; set; }

        /// <summary>reportConfigs key — differs from <see cref="ControllerName"/> for the 4 *HSCodeDetailReport aliases.</summary>
        public string ConfigKey { get; set; } = string.Empty;

        /// <summary>Backend report key (controller class name minus "Controller"). A mismatch is a 400.</summary>
        public string ControllerName { get; set; } = string.Empty;

        /// <summary>config.title — the job's ReportTitle, the worksheet name and the sheet's title line.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>config.excelFileName — the download file name base (a timestamp and .xlsx are appended).</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// reportHeading lines plus the resolved reportSubtitle, exactly as rendered above
        /// the grid. The backend appends the From/To (or Date) and Exported meta lines.
        /// </summary>
        public List<string> HeaderLines { get; set; } = new();

        public bool ShowRowNumber { get; set; }

        /// <summary>'No' | 'No.' (legacy report viewer) | a bespoke label ('Sr.No.', 'စဥ်').</summary>
        public string RowNumberTitle { get; set; } = "No";

        public List<ExcelSpecColumn> Columns { get; set; } = new();

        /// <summary>Where the per-currency footer rows are written. Column KEYS, not dataIndexes.</summary>
        public ExcelCurrencyTotalsPlacement? CurrencyTotalsColumns { get; set; }

        /// <summary>Composite (multi-table) pages only.</summary>
        public List<ExcelSpecSection>? Sections { get; set; }

        /// <summary>Composite pages' trailing single-value lines, e.g. "Total USD Value".</summary>
        public List<ExcelSpecSummaryLine>? SummaryLines { get; set; }
    }

    /// <summary>One UI grid column.</summary>
    public sealed class ExcelSpecColumn
    {
        /// <summary>The config column key — what currencyTotalsColumns points at.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>The camelCase JSON property the grid reads the cell from.</summary>
        public string DataIndex { get; set; } = string.Empty;

        /// <summary>Header text exactly as the grid shows it (already ApplyType-resolved).</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>'string' | 'number' | 'date' | 'dateTime' | 'boolean' | 'money'; null = plain text.</summary>
        public string? DataType { get; set; }

        /// <summary>Used, joined with ", ", when the primary dataIndex is blank.</summary>
        public List<string>? FallbackDataIndexes { get; set; }

        /// <summary>e.g. "#,##0.0000" for the 4-decimal money columns.</summary>
        public string? NumberFormat { get; set; }
    }

    /// <summary>Which columns carry the per-currency footer label and value.</summary>
    public sealed class ExcelCurrencyTotalsPlacement
    {
        public string LabelColumnKey { get; set; } = string.Empty;
        public string ValueColumnKey { get; set; } = string.Empty;
    }

    /// <summary>One table of a composite (multi-table) report page.</summary>
    public sealed class ExcelSpecSection
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        /// <summary>Property on the summary payload holding this section's rows.</summary>
        public string DataPath { get; set; } = string.Empty;

        public bool ShowRowNumber { get; set; }
        public string RowNumberTitle { get; set; } = "Sr.No.";
        public List<ExcelSpecColumn> Columns { get; set; } = new();
    }

    /// <summary>A trailing "label: value" line under a composite page's tables.</summary>
    public sealed class ExcelSpecSummaryLine
    {
        public string Label { get; set; } = string.Empty;
        public string DataPath { get; set; } = string.Empty;
        public string? NumberFormat { get; set; }
    }
}

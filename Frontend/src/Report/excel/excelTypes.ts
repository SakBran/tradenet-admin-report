/**
 * Canonical Excel presentation spec — the frontend half of the contract in
 * docs/ExcelParity/Contract.md §3.
 *
 * Mirror of `Backend/Model/ExcelExport/ExcelPresentationSpec.cs`
 * (System.Text.Json Web defaults: camelCase, case-insensitive read). The spec
 * rides on the Excel POST as `ReportQueryRequest.Excel` and is part of the
 * hashed request JSON, so a column change yields a new cached file. Keep the
 * property names, the property ORDER and the value shapes in sync with the C#
 * DTO; `buildExcelPresentationFromInput` builds the object from one literal so
 * `JSON.stringify` is byte-stable for the dedup hash.
 */

/** `ExcelPresentationSpec.formatVersion`. Bump only with the C# DTO. */
export const EXCEL_PRESENTATION_FORMAT_VERSION = 1;

/** Cell semantics; identical whitelist to the validator's `dataType`. */
export type ExcelSpecDataType =
  | 'string'
  | 'number'
  | 'date'
  | 'dateTime'
  | 'boolean'
  | 'money';

export interface ExcelSpecColumn {
  /** Column key (BasicTable `column.key`) — what `currencyTotalsColumns` refers to. */
  key: string;
  /** camelCase JSON key of the row property to read (== CamelCase(C# property)). */
  dataIndex: string;
  /** Header text, exactly as the grid prints it. */
  title: string;
  dataType?: ExcelSpecDataType;
  /** Read in order when `dataIndex` is blank; non-blank values join with ", ". */
  fallbackDataIndexes?: string[];
  /** Excel number format override (e.g. `#,##0.0000` for 4-decimal money). */
  numberFormat?: string;
}

/** Where the per-currency footer rows put their label and their value. */
export interface ExcelCurrencyTotalsPlacement {
  labelColumnKey: string;
  valueColumnKey: string;
}

/** One table of a composite sheet (the 4 *TotalValueLicencesReport pages). */
export interface ExcelSpecSection {
  key: string;
  title: string;
  /** camelCase path on the summary object holding this section's rows. */
  dataPath: string;
  showRowNumber: boolean;
  rowNumberTitle: string;
  columns: ExcelSpecColumn[];
}

/** A single "label: value" line under a composite sheet's tables. */
export interface ExcelSpecSummaryLine {
  label: string;
  /** camelCase path on the summary object holding the scalar value. */
  dataPath: string;
  numberFormat?: string;
}

export interface ExcelPresentationSpec {
  formatVersion: number;
  /** `reportConfigs` key — differs from `controllerName` for the 4 aliases. */
  configKey: string;
  /** Backend ReportKey (controller class name minus "Controller"); mismatch → 400. */
  controllerName: string;
  /** `config.title` → job.ReportTitle + worksheet name + the sheet's title line. */
  title: string;
  /** `config.excelFileName` → the job's file-name base (sanitized + timestamped). */
  fileName: string;
  /** Heading/subtitle lines; the backend appends From/To (or Date) + Exported. */
  headerLines: string[];
  showRowNumber: boolean;
  /** 'No' | 'No.' (legacy report viewer) | bespoke labels ('Sr.No.', 'စဥ်'). */
  rowNumberTitle: string;
  /** May be empty ONLY when `sections` is non-empty (composite sheets). */
  columns: ExcelSpecColumn[];
  currencyTotalsColumns?: ExcelCurrencyTotalsPlacement;
  sections?: ExcelSpecSection[];
  summaryLines?: ExcelSpecSummaryLine[];
}

/** The Excel endpoint's queue response (`ExcelExportJobService.EnqueueAsync`). */
export interface ExcelEnqueueResult {
  status: 'Ready' | 'Queued' | 'Processing';
  jobId: string;
  fileName?: string;
  downloadUrl?: string;
  message?: string;
}

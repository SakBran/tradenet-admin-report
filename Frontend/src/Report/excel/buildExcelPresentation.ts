/**
 * Builds the Excel presentation spec the backend needs to render a sheet that
 * matches the grid (docs/ExcelParity/Contract.md §3): the same header block, the
 * same columns in the same order with the same header text, and the placement of
 * the currency footer.
 *
 * Pure: no React, no axios. `buildExcelPresentationFromInput` assembles ONE
 * object literal so the key order — and therefore `JSON.stringify` and the
 * backend's dedup hash — is deterministic.
 */
import { ReportColumnConfig, ReportPageConfig } from '../config/reportTypes';
import {
  buildReportHeaderLines,
  getReportConfigKey,
  resolveReportColumns,
  resolveRowNumberTitle,
} from '../reportPresentation';
import {
  EXCEL_PRESENTATION_FORMAT_VERSION,
  ExcelCurrencyTotalsPlacement,
  ExcelPresentationSpec,
  ExcelSpecColumn,
  ExcelSpecSection,
  ExcelSpecSummaryLine,
} from './excelTypes';

/** Everything a spec needs; the format version is added by the builder. */
export interface ExcelPresentationInput {
  configKey: string;
  controllerName: string;
  title: string;
  fileName: string;
  headerLines: string[];
  showRowNumber: boolean;
  rowNumberTitle: string;
  columns: ExcelSpecColumn[];
  currencyTotalsColumns?: ExcelCurrencyTotalsPlacement;
  sections?: ExcelSpecSection[];
  summaryLines?: ExcelSpecSummaryLine[];
}

/**
 * A grid column as the sheet needs it. `drilldown` (a UI-only hyperlink) and
 * `hidden` never reach the spec — a drill-down column exports its underlying
 * value, a hidden column is dropped by `resolveReportColumns`.
 */
export const toPresentationColumn = (
  column: ReportColumnConfig
): ExcelSpecColumn => ({
  key: column.key,
  dataIndex: column.dataIndex,
  title: column.title,
  ...(column.dataType ? { dataType: column.dataType } : {}),
  ...(column.fallbackDataIndexes?.length
    ? { fallbackDataIndexes: [...column.fallbackDataIndexes] }
    : {}),
  ...(column.numberFormat ? { numberFormat: column.numberFormat } : {}),
});

/** One literal → stable key order → deterministic dedup hash. */
export const buildExcelPresentationFromInput = (
  input: ExcelPresentationInput
): ExcelPresentationSpec => ({
  formatVersion: EXCEL_PRESENTATION_FORMAT_VERSION,
  configKey: input.configKey,
  controllerName: input.controllerName,
  title: input.title,
  fileName: input.fileName,
  headerLines: input.headerLines,
  showRowNumber: input.showRowNumber,
  rowNumberTitle: input.rowNumberTitle,
  columns: input.columns,
  ...(input.currencyTotalsColumns
    ? { currencyTotalsColumns: input.currencyTotalsColumns }
    : {}),
  ...(input.sections?.length ? { sections: input.sections } : {}),
  ...(input.summaryLines?.length ? { summaryLines: input.summaryLines } : {}),
});

/**
 * The spec for a config-driven report page, for the filter values it is about
 * to post. `applied` must be the NORMALIZED filters (the request body values,
 * derived FormType/Type included) so `reportSubtitle` and `resolveColumns` see
 * exactly what the grid saw.
 */
export const buildExcelPresentation = (
  config: ReportPageConfig,
  applied: Record<string, unknown>,
  configKey: string = getReportConfigKey(config)
): ExcelPresentationSpec =>
  buildExcelPresentationFromInput({
    configKey,
    controllerName: config.controllerName,
    title: config.title,
    fileName: config.excelFileName,
    headerLines: buildReportHeaderLines(config, applied),
    showRowNumber: config.showRowNumber ?? true,
    rowNumberTitle: resolveRowNumberTitle(config),
    columns: resolveReportColumns(config, applied).map(toPresentationColumn),
    ...(config.currencyTotalsColumns
      ? { currencyTotalsColumns: config.currencyTotalsColumns }
      : {}),
  });

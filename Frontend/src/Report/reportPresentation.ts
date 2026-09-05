/**
 * Shared, pure report-presentation helpers.
 *
 * Everything the grid (`GenericReportPage` + `BasicTable`) and the Excel
 * presentation spec (`Report/excel/buildExcelPresentation.ts`) must agree on
 * lives here, so the sheet can never drift from what the UI shows:
 * the derived (hidden) filter values, the legacy RDLC header lines, the row
 * number label, and the resolved/visible column list.
 *
 * No React, no antd, no axios — importable from a Node/vitest context (the
 * fixture generator does exactly that).
 */
import dayjs from 'dayjs';

import { reportConfigs } from './config/reportConfigs';
import {
  ReportColumnConfig,
  ReportFilterConfig,
  ReportPageConfig,
} from './config/reportTypes';

/**
 * Controller-name prefix → the `FormType` the backend expects. Moved out of
 * GenericReportPage.tsx so the Excel spec builder and the fixture generator
 * derive exactly the same hidden filters the grid posts.
 */
export const formTypePrefixes: Array<[string, string]> = [
  ['BorderExportLicence', 'Border Export Licence'],
  ['BorderImportLicence', 'Border Import Licence'],
  ['BorderExportPermit', 'Border Export Permit'],
  ['BorderImportPermit', 'Border Import Permit'],
  ['ExportLicence', 'Export Licence'],
  ['ImportLicence', 'Import Licence'],
  ['ExportPermit', 'Export Permit'],
  ['ImportPermit', 'Import Permit'],
];

/**
 * Filter values the page fills in from the report identity instead of showing a
 * filter box for them (`FormType`, and `Type` = Border/Oversea).
 */
export const getDerivedFilterValues = (
  controllerName: string,
  filters: ReportFilterConfig[]
): Record<string, string> => {
  const values: Record<string, string> = {};
  const hasFilter = (name: string) =>
    filters.some((filter) => filter.name === name);
  const formType = formTypePrefixes.find(([prefix]) =>
    controllerName.startsWith(prefix)
  )?.[1];

  if (formType && hasFilter('FormType')) {
    values.FormType = formType;
  }

  if (hasFilter('Type')) {
    if (controllerName.startsWith('Border')) {
      values.Type = 'Border';
    } else if (
      formType &&
      [
        'Export Licence',
        'Import Licence',
        'Export Permit',
        'Import Permit',
      ].includes(formType)
    ) {
      values.Type = 'Oversea';
    }
  }

  return values;
};

/** The legacy RDLC header date format (`DD/MM/YYYY`). */
export const formatLegacyReportDate = (value: unknown) => {
  const parsed = dayjs(String(value ?? ''));
  return parsed.isValid() ? parsed.format('DD/MM/YYYY') : String(value ?? '');
};

/**
 * The legacy RDLC in-sheet/in-grid header block: the configured centered
 * heading lines followed by the dynamic subtitle (or the plain title when the
 * report has no subtitle).
 *
 * It deliberately does NOT emit `From Date:` / `To Date:` rows — the grid never
 * had them and the backend derives them from the request DTO
 * (`ExcelLayoutBuilder.WithStandardHeaderBlock`).
 */
export const buildReportHeaderLines = (
  config: ReportPageConfig,
  applied: Record<string, unknown>
): string[] =>
  [
    ...(config.reportHeading ?? []),
    config.reportSubtitle ? config.reportSubtitle(applied) : config.title,
  ].filter((line): line is string => Boolean(line && line.trim() !== ''));

/** Reports rendered in the legacy RDLC ReportViewer shell (row label `No.`). */
export const isLegacyReportViewer = (config: ReportPageConfig) =>
  config.legacyReportViewer ??
  (config.controllerName.startsWith('ImportLicence') ||
    config.controllerName.startsWith('BorderImportPermit'));

/** Header text of the row-number column, exactly as the grid prints it. */
export const resolveRowNumberTitle = (config: ReportPageConfig) =>
  config.rowNumberTitle ?? (isLegacyReportViewer(config) ? 'No.' : 'No');

/**
 * The columns the grid actually renders for these applied filters: the
 * per-ApplyType resolved titles (voucher reports) minus hidden columns.
 */
export const resolveReportColumns = (
  config: ReportPageConfig,
  applied: Record<string, unknown>
): ReportColumnConfig[] =>
  (config.resolveColumns
    ? config.resolveColumns(applied, config.columns)
    : config.columns
  ).filter((column) => !column.hidden);

let configKeysByConfig: Map<ReportPageConfig, string> | null = null;

/**
 * The `reportConfigs` key of a config object (its route segment and the Excel
 * spec's `configKey`). Resolved by object identity because 4 alias configs share
 * one controller with a different column set
 * (e.g. `ExportLicenceHSCodeDetailReport` → `ExportLicenceByHSCodeReport`).
 * Falls back to the controller name for a config that is not in the registry.
 */
export const getReportConfigKey = (config: ReportPageConfig): string => {
  if (!configKeysByConfig) {
    configKeysByConfig = new Map<ReportPageConfig, string>();
    for (const [key, value] of Object.entries(reportConfigs)) {
      if (!configKeysByConfig.has(value)) {
        configKeysByConfig.set(value, key);
      }
    }
  }

  return configKeysByConfig.get(config) ?? config.controllerName;
};

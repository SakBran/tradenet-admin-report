/**
 * Deterministic sample inputs for the Excel presentation spec.
 *
 * The spec depends on the applied filters (a `reportSubtitle` prints the date
 * range; a voucher report's `resolveColumns` rewrites two header texts per
 * ApplyType), so both the parity test and the fixture generator must feed every
 * config the SAME fixed values — otherwise `Backend.Tests/Fixtures/ExcelSpecs`
 * would churn on every run. Values match `docs/ExcelParity/Contract.md` §10 and
 * `Frontend/scripts/excelParityManifest.ts`.
 *
 * Test/tooling support only: nothing in the running app imports this.
 */
import { ReportFilterOption, ReportPageConfig } from '../config/reportTypes';
import { getDerivedFilterValues } from '../reportPresentation';

/** Fixed filter values (never "today") so fixtures are reproducible. */
export const EXCEL_SPEC_SAMPLE_FILTERS: Record<string, unknown> = {
  FromDate: '2026-02-01T00:00:00',
  ToDate: '2026-02-28T23:59:59',
  Date: '2026-02-15T00:00:00',
  ApplyType: 'New',
  Type: '',
};

/**
 * Config keys with no Excel export at all (no streaming controller / no Excel
 * button), excluded from the fixture set — see `Contract.md` §10 and the
 * manifest's `excluded` list.
 */
export const EXCEL_SPEC_EXCLUDED_CONFIG_KEYS = [
  'CardListsByCompanyRegistrationNumber',
  'DataImport',
  'ImportLicenceDataImport',
];

/**
 * The normalized filter object `GenericReportPage` would hand to
 * `reportSubtitle` / `resolveColumns`: filter defaults, then the fixed sample
 * values, then the hidden derived FormType/Type, then explicit overrides (the
 * ApplyType of a variant).
 */
export const buildSampleAppliedFilters = (
  config: ReportPageConfig,
  overrides: Record<string, unknown> = {}
): Record<string, unknown> => {
  const values: Record<string, unknown> = {};

  for (const filter of config.filters) {
    if (filter.excludeFromRequest) {
      continue;
    }

    if (filter.type === 'dateRange') {
      values[filter.fromName ?? 'FromDate'] = EXCEL_SPEC_SAMPLE_FILTERS.FromDate;
      values[filter.toName ?? 'ToDate'] = EXCEL_SPEC_SAMPLE_FILTERS.ToDate;
      continue;
    }

    if (filter.type === 'date') {
      values[filter.name] = EXCEL_SPEC_SAMPLE_FILTERS.Date;
      continue;
    }

    if (filter.type === 'number') {
      values[filter.name] =
        typeof filter.defaultValue === 'number'
          ? filter.defaultValue
          : Number(filter.defaultValue ?? 0);
      continue;
    }

    values[filter.name] = filter.defaultValue ?? '';
  }

  return {
    ...values,
    ...EXCEL_SPEC_SAMPLE_FILTERS,
    ...getDerivedFilterValues(config.controllerName, config.filters),
    ...overrides,
  };
};

/**
 * ApplyType options that change the column header texts, i.e. the options of a
 * config that resolves its columns from the filters (the 5 voucher reports).
 * Each one gets its own fixture so the backend contract test sees every header
 * variant the grid can print.
 */
export const applyTypeVariants = (config: ReportPageConfig): string[] => {
  if (!config.resolveColumns) {
    return [];
  }

  const options: ReportFilterOption[] =
    config.filters.find((filter) => filter.name === 'ApplyType')?.options ?? [];

  return options
    .map((option) => String(option.value))
    .filter((value) => value.trim() !== '');
};

/** Fixture file-name suffix for an ApplyType variant (spaces → underscores). */
export const fixtureVariantSuffix = (applyType: string) =>
  `ApplyType-${applyType.replace(/\s+/g, '_')}`;

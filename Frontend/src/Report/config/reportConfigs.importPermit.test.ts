import { describe, it, expect } from 'vitest';
import { reportConfigs } from './reportConfigs';

// Guards the Import Permit complaint fixes (2026-06) against regression. These are static
// config-integrity checks — no backend, no rendering — so they run fast and catch the exact
// classes of bug the adversarial review found: footer keys that don't exist as columns,
// drill-downs whose target/carry-filters are wrong, and the column/filter shape changes.

const IMPORT_PERMIT_KEYS = [
  'ImportPermitAmendmentReport',
  'ImportPermitByHSCodeReport',
  'ImportPermitBySectionReport',
  'ImportPermitBySellerCountryReport',
  'ImportPermitCancellationReport',
  'ImportPermitCompanyListReport',
  'ImportPermitDailyReportNewPermitReport',
  'ImportPermitDetailReport',
  'ImportPermitExtensionReport',
  'ImportPermitNewReportNewReport',
  'ImportPermitVoucherReport',
];

describe('Import Permit report configs', () => {
  it('all 11 reports exist and carry the Ministry/Directorate heading', () => {
    for (const key of IMPORT_PERMIT_KEYS) {
      const cfg = reportConfigs[key];
      expect(cfg, `${key} should exist`).toBeDefined();
      expect(cfg.reportHeading).toEqual(['Ministry of Commerce', 'Directorate of Trade']);
    }
  });

  it('currencyTotalsColumns reference column keys that actually exist (else the footer cannot render)', () => {
    for (const [key, cfg] of Object.entries(reportConfigs)) {
      if (!cfg.currencyTotalsColumns) continue;
      const colKeys = new Set(cfg.columns.map((c) => c.key));
      const { labelColumnKey, valueColumnKey } = cfg.currencyTotalsColumns;
      expect(colKeys.has(labelColumnKey), `${key}: labelColumnKey '${labelColumnKey}' not a column`).toBe(true);
      expect(colKeys.has(valueColumnKey), `${key}: valueColumnKey '${valueColumnKey}' not a column`).toBe(true);
    }
  });

  it('New / Amendment / Voucher have a currency-totals footer configured', () => {
    for (const key of [
      'ImportPermitNewReportNewReport',
      'ImportPermitAmendmentReport',
      'ImportPermitVoucherReport',
      'ImportPermitExtensionReport',
    ]) {
      expect(reportConfigs[key].currencyTotalsColumns, `${key} currencyTotalsColumns`).toBeDefined();
    }
  });

  it('every drilldown targets a known report and carries only filters that exist on the source', () => {
    for (const [report, cfg] of Object.entries(reportConfigs)) {
      const filterNames = new Set(cfg.filters.map((f) => f.name));
      const dateBounds = new Set<string>();
      for (const f of cfg.filters) {
        if (f.type === 'dateRange') {
          dateBounds.add(f.fromName ?? 'FromDate');
          dateBounds.add(f.toName ?? 'ToDate');
        }
      }
      for (const col of cfg.columns) {
        const drill = col.drilldown;
        if (!drill) continue;
        expect(reportConfigs[drill.targetReportKey], `${report}.${col.key} -> ${drill.targetReportKey}`).toBeDefined();
        for (const name of drill.carryFilters ?? []) {
          const ok = filterNames.has(name) || dateBounds.has(name);
          expect(ok, `${report}.${col.key}: carryFilters '${name}' is not a filter on ${report}`).toBe(true);
        }
      }
    }
  });

  it('Voucher exposes Commodity Type / Total CIF / Exchange Rate columns', () => {
    const indexes = new Set(reportConfigs.ImportPermitVoucherReport.columns.map((c) => c.dataIndex));
    expect(indexes.has('commodityType')).toBe(true);
    expect(indexes.has('totalCIF')).toBe(true);
    expect(indexes.has('exchangeRate')).toBe(true);
  });

  it('Amendment / Cancellation / Extension "No." columns map to oldLicenceNo (not the licence no)', () => {
    const cases: Array<[string, string]> = [
      ['ImportPermitAmendmentReport', 'LicenceAmendmentNo'],
      ['ImportPermitCancellationReport', 'CancellationNo'],
      ['ImportPermitExtensionReport', 'ExtensionNo'],
    ];
    for (const [report, colKey] of cases) {
      const col = reportConfigs[report].columns.find((c) => c.key === colKey);
      expect(col, `${report}.${colKey}`).toBeDefined();
      expect(col!.dataIndex, `${report}.${colKey}`).toBe('oldLicenceNo');
    }
  });

  it('By HS Code drops the Company Name column and offers a Start/End Filter-By select', () => {
    const cfg = reportConfigs.ImportPermitByHSCodeReport;
    expect(cfg.columns.some((c) => c.dataIndex === 'companyName')).toBe(false);
    const filterType = cfg.filters.find((f) => f.name === 'FilterType');
    expect(filterType?.type).toBe('select');
    expect((filterType?.options ?? []).map((o) => o.value).sort()).toEqual(['End', 'Start']);
  });

  it('summary drill-downs pass the id/registration the Detail report filters on', () => {
    const drillOf = (report: string, colKey: string) =>
      reportConfigs[report].columns.find((c) => c.key === colKey)?.drilldown;

    expect(drillOf('ImportPermitBySectionReport', 'Section')?.rowParams).toEqual({
      ExportImportSectionId: 'sectionId',
    });
    expect(drillOf('ImportPermitBySellerCountryReport', 'Country')?.rowParams).toEqual({
      SellerCountryId: 'countryId',
    });
    expect(drillOf('ImportPermitCompanyListReport', 'CompanyName')?.rowParams).toEqual({
      CompanyRegistrationNo: 'companyRegistrationNo',
    });
    for (const report of ['ImportPermitBySectionReport', 'ImportPermitBySellerCountryReport', 'ImportPermitCompanyListReport']) {
      expect(drillOf(report, report === 'ImportPermitBySectionReport' ? 'Section' : report === 'ImportPermitBySellerCountryReport' ? 'Country' : 'CompanyName')?.targetReportKey).toBe('ImportPermitDetailReport');
    }
  });
});

import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';

describe('Import Licence Detail report configs', () => {
  it('keeps the licence-level drill-down distinct from the old detail report', () => {
    const drillDown = reportConfigs.ImportLicenceDetailByLicenceReport;
    const detail = reportConfigs.ImportLicenceDetailReport;

    expect(drillDown.title).toBe('Import Licence Detail (By Licence)');
    expect(drillDown.excelFileName).toBe('ImportLicenceDetailByLicence.xlsx');
    expect(drillDown.reportSubtitle?.({
      FromDate: '2026-06-01',
      ToDate: '2026-06-10',
    })).toBe('List of Import Licences By Licence From (01/06/2026) To (10/06/2026)');
    expect(drillDown.title).not.toBe(detail.title);
  });

  it('keeps the legacy-parity detail report filters and columns unchanged', () => {
    const detail = reportConfigs.ImportLicenceDetailReport;

    expect(detail.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'Type',
      'PaThaKaTypeId',
      'ExportImportSectionId',
      'ExportImportMethodId',
      'ExportImportIncotermId',
    ]);
    expect(detail.columns).toHaveLength(25);
  });
});

import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';

const BORDER_IMPORT_LICENCE_CREATED_REPORTS = [
  'BorderImportLicenceByMethodReport',
  'BorderImportLicenceBySectionReport',
  'BorderImportLicenceBySellerCountryReport',
  'BorderImportLicenceCompanyListReport',
  'BorderImportLicenceDailyReportNewLicenceReport',
  'BorderImportLicenceDetailReport',
];

describe('Border Import Licence report configs', () => {
  it('created reports expose old-admin style report subtitles', () => {
    const filters = { FromDate: '2026-06-01', ToDate: '2026-06-10' };

    for (const key of BORDER_IMPORT_LICENCE_CREATED_REPORTS) {
      const subtitle = reportConfigs[key].reportSubtitle;
      expect(subtitle, `${key} reportSubtitle`).toBeDefined();
      expect(subtitle!(filters), `${key} subtitle`).toContain('(01/06/2026) To (10/06/2026)');
    }
  });

  it('Detail matches the old RDLC date headers and filter shape', () => {
    const cfg = reportConfigs.BorderImportLicenceDetailReport;

    expect(cfg.filters.map((f) => f.name)).toEqual([
      'dateRange',
      'SakhanId',
      'PaThaKaTypeId',
      'ExportImportSectionId',
      'ExportImportMethodId',
      'ExportImportIncotermId',
    ]);

    const dateColumns = cfg.columns
      .filter((c) => ['ApplicationDate', 'LicenceDate', 'ApproveDate'].includes(c.key))
      .map((c) => [c.key, c.dataIndex, c.title]);

    expect(dateColumns).toEqual([
      ['ApplicationDate', 'applicationDate', 'Application Date'],
      ['LicenceDate', 'licenceDate', 'Create Date'],
      ['ApproveDate', 'approveDate', 'Approve Date'],
    ]);
  });
});

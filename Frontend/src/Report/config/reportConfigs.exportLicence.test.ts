import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';

describe('Export Licence report configs', () => {
  it('summary reports match old admin filter boxes and legacy titles', () => {
    const expected = {
      ExportLicenceByMethodReport: {
        filters: ['dateRange', 'PaThaKaTypeId', 'ExportImportSectionId', 'ExportImportMethodId'],
        subtitle: 'List of Export Licences By Method From (01/02/2026) To (03/02/2026)',
      },
      ExportLicenceBySectionReport: {
        filters: ['dateRange', 'PaThaKaTypeId', 'ExportImportSectionId', 'ExportImportMethodId'],
        subtitle: 'List of Export Licences By Section From (01/02/2026) To (03/02/2026)',
      },
      ExportLicenceBySellerCountryReport: {
        filters: [
          'dateRange',
          'PaThaKaTypeId',
          'ExportImportSectionId',
          'ExportImportMethodId',
          'BuyerCountryId',
        ],
        subtitle: 'List of Export Licences By Buyer Country From (01/02/2026) To (03/02/2026)',
      },
      ExportLicenceCompanyListReport: {
        filters: [
          'dateRange',
          'PaThaKaTypeId',
          'ExportImportSectionId',
          'ExportImportMethodId',
          'CompanyRegistrationNo',
          'CompanyName',
        ],
        subtitle: 'List of Export Licences By Company From (01/02/2026) To (03/02/2026)',
      },
      ExportLicenceDailyReportNewLicenceReport: {
        filters: [
          'dateRange',
          'ExportImportSectionId',
          'PaThaKaTypeId',
          'CompanyRegistrationNo',
          'Auto',
          'CompanyName',
        ],
        subtitle: 'List of Export Licences By Daily From (01/02/2026) To (03/02/2026)',
      },
    } as const;

    for (const [key, { filters, subtitle }] of Object.entries(expected)) {
      const cfg = reportConfigs[key];

      expect(
        cfg.filters.map((filter) => filter.name),
        key
      ).toEqual(filters);
      expect(
        cfg.reportSubtitle?.({ FromDate: '2026-02-01', ToDate: '2026-02-03' }),
        key
      ).toBe(subtitle);
    }
  });

  it('list and detail reports open with a data-bearing three-month date range by default', () => {
    for (const key of [
      'ExportLicenceByHSCodeReport',
      'ExportLicenceByMethodReport',
      'ExportLicenceBySectionReport',
      'ExportLicenceBySellerCountryReport',
      'ExportLicenceCompanyListReport',
      'ExportLicenceDailyReportNewLicenceReport',
      'ExportLicenceDetailReport',
      'ExportLicenceTotalValueLicencesReport',
    ]) {
      const cfg = reportConfigs[key];
      const dateRange = cfg.filters.find((filter) => filter.name === 'dateRange');

      expect(dateRange?.type, key).toBe('dateRange');
      expect(dateRange?.fromName, key).toBe('FromDate');
      expect(dateRange?.toName, key).toBe('ToDate');
      expect(dateRange?.defaultDateRangeMonths, key).toBe(3);
    }
  });

  it('Detail columns are bound to backend result fields used by the UI table', () => {
    const indexes = new Set(
      reportConfigs.ExportLicenceDetailReport.columns.map((column) => column.dataIndex)
    );

    for (const field of [
      'sectionName',
      'applicationDate',
      'applicationNo',
      'licenceNo',
      'licenceDate',
      'companyRegistrationNo',
      'buyerName',
      'portofExport',
      'destinationCountry',
      'hsCode',
      'amount',
      'commodityType',
    ]) {
      expect(indexes.has(field), field).toBe(true);
    }
  });

  it('Detail report keeps lazy exact row counts enabled for paged UI totals', () => {
    expect(reportConfigs.ExportLicenceDetailReport.disableLazyTotalCount).not.toBe(true);
  });
});

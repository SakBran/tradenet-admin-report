import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';

describe('Export Licence report configs', () => {
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

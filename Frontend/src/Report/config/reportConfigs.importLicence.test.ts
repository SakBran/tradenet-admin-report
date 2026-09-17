import { describe, expect, it } from 'vitest';

import { reportConfigs } from './reportConfigs';

describe('Import Licence report configs', () => {
  it('Daily report uses global pagination and the standard row-number column', () => {
    const config = reportConfigs.ImportLicenceDailyReportNewLicenceReport;

    expect(config.defaultPageSize).toBeUndefined();
    expect(config.showRowNumber).toBe(true);
    expect(config.columns.map((column) => column.title)).toEqual([
      'Date',
      'No of Licences',
      'Total Value',
      'Currency',
      'Total USD Value',
    ]);
  });

  it('Actual Amendment report keeps the legacy columns and licence mapping', () => {
    const config = reportConfigs.ImportLicenceActualAmendmentReport;

    expect(config.showRowNumber).toBe(true);
    expect(config.columns.map((column) => column.title)).toEqual([
      'Section',
      'Licence No',
      'Licence Amendment No',
      'Amendment Date',
      'Company Registration No',
      'Company Name',
      'Company Address',
      'Curency',
      'HSCode',
      'Total Value',
    ]);
    expect(config.columns.find((column) => column.title === 'Licence No')?.dataIndex).toBe(
      'oldLicenceNo',
    );
  });

  // Complaint 2026-09-17: clicking a company in the Company List opened a detail report with
  // "columns missing, nothing like the old one". All four By-X summaries had been re-pointed
  // at ImportLicenceDetailByLicenceReport (9 columns, no legacy counterpart). In Tradenet 2.0
  // every one of them opened the per-item ImportLicenceDetailReport and its 26 columns:
  // ImportLicence{ByCompany,BySection,ByMethod,BySellerCountry}Report.rdlc wrap the cell in
  // window.open(..., '_blank') and ReportsController.cs (origin/master) builds the url at
  // :5915 / :5101 / :5207 / :5806 as Reports/ImportLicenceDetailReport?...
  it('By-X summaries drill into the 26-column legacy Detail report in a new tab', () => {
    const drills = [
      ['ImportLicenceCompanyListReport', 'CompanyName', { CompanyRegistrationNo: 'companyRegistrationNo' }],
      ['ImportLicenceBySectionReport', 'Section', { ExportImportSectionId: 'sectionId' }],
      ['ImportLicenceByMethodReport', 'Method', { ExportImportMethodId: 'methodId' }],
      ['ImportLicenceBySellerCountryReport', 'Country', { SellerCountryId: 'countryId' }],
    ] as const;

    for (const [reportKey, columnKey, rowParams] of drills) {
      const drilldown = reportConfigs[reportKey].columns.find(
        (column) => column.key === columnKey
      )?.drilldown;

      expect(drilldown?.targetReportKey).toBe('ImportLicenceDetailReport');
      expect(drilldown?.openInNewTab).toBe(true);
      // ImportLicenceDetailReportRequest has no Currency property, and the legacy url passed
      // none either -- carrying one would bind to nothing.
      expect(drilldown?.rowParams).toEqual(rowParams);
      expect(drilldown?.carryFilters).toContain('FromDate');
      expect(drilldown?.carryFilters).toContain('ToDate');
      expect(drilldown?.carryFilters).toContain('PaThaKaTypeId');
    }
  });

  it('the legacy Detail report the drills target still carries all 26 rdlc columns', () => {
    // ImportLicenceDetailReport.rdlc on origin/master: 26 <TablixColumn>, headers at
    // rdlc:365..1800. 'Country of Orign' and 'Decription' are typos in the original.
    expect(reportConfigs.ImportLicenceDetailReport.columns.map((column) => column.title)).toEqual([
      'Section',
      'Application Date',
      'Application No',
      'Licence No',
      'Licence Date',
      'Company Registration No',
      'Company Name',
      'Company Address',
      'Seller Name',
      'Seller Address',
      'Seller Country',
      'Place/Port of Discharge',
      'Last Date',
      'Method',
      'Consigned Country',
      'Country of Orign',
      'hsCode',
      'Decription',
      'A/U',
      'Price',
      'Qty',
      'Value',
      'Currency',
      'Commodity Type',
      'Conditions',
    ]);
    // Sr.No. is the grid's own row-number column, not a config column.
    expect(reportConfigs.ImportLicenceDetailReport.showRowNumber).toBe(true);
  });
});

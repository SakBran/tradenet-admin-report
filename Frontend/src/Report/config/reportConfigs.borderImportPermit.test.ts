import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';

describe('Border Import Permit report configs', () => {
  it('action reports keep the old admin filter shape and totals wiring', () => {
    const expected = {
      BorderImportPermitActualAmendmentReport: [
        'dateRange',
        'FormType',
        'ExportImportSectionId',
        'AmendRemarkId',
        'CompanyRegistrationNo',
        'CompanyName',
        'SakhanId',
      ],
      BorderImportPermitAmendmentReport: [
        'dateRange',
        'FormType',
        'ExportImportSectionId',
        'AmendRemarkId',
        'CompanyRegistrationNo',
        'CompanyName',
        'SakhanId',
      ],
      BorderImportPermitCancellationReport: [
        'dateRange',
        'FormType',
        'ExportImportSectionId',
        'CompanyRegistrationNo',
        'CompanyName',
        'SakhanId',
      ],
      BorderImportPermitExtensionReport: [
        'dateRange',
        'FormType',
        'ExportImportSectionId',
        'CompanyRegistrationNo',
        'CompanyName',
        'SakhanId',
      ],
    } as const;

    for (const [key, filters] of Object.entries(expected)) {
      const cfg = reportConfigs[key];

      expect(cfg.filters.map((filter) => filter.name), key).toEqual(filters);
      expect(
        cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName,
        `${key} should use Border Import Permit sections`
      ).toBe('borderImportPermitSections');
      expect(
        cfg.filters.find((filter) => filter.name === 'CompanyName')?.type,
        `${key} should keep readonly company name`
      ).toBe('readonlyText');
      expect(
        cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName,
        `${key} should keep Sakhan lookup`
      ).toBe('sakhans');
      expect(cfg.currencyTotalsColumns, `${key} footer totals`).toEqual({
        labelColumnKey: 'LicenceNo',
        valueColumnKey: 'TotalValue',
      });
    }
  });

  it('new report keeps the old admin filter shape plus footer totals', () => {
    const cfg = reportConfigs.BorderImportPermitNewReportNewReport;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'FormType',
      'ExportImportSectionId',
      'CompanyRegistrationNo',
      'CompanyName',
      'SakhanId',
    ]);
    expect(
      cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName
    ).toBe('borderImportPermitSections');
    expect(cfg.filters.find((filter) => filter.name === 'CompanyName')?.type).toBe(
      'readonlyText'
    );
    expect(cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe(
      'sakhans'
    );
    expect(cfg.currencyTotalsColumns).toEqual({
      labelColumnKey: 'LicenceNo',
      valueColumnKey: 'TotalValue',
    });
  });

  it('voucher keeps the old admin filter shape, Sakhan, company name, and amount footer totals', () => {
    const cfg = reportConfigs.BorderImportPermitVoucherReport;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'FormType',
      'ExportImportSectionId',
      'PaymentType',
      'ApplyType',
      'CompanyRegistrationNo',
      'CompanyName',
      'SakhanId',
    ]);
    expect(
      cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName
    ).toBe('borderImportPermitSections');
    expect(cfg.filters.find((filter) => filter.name === 'CompanyName')?.type).toBe(
      'readonlyText'
    );
    expect(cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe(
      'sakhans'
    );
    // "Total Amount" is the voucher fee (AccountTransaction.TotalAmount), which the old
    // BorderVoucherReport.rdlc:1399 binds as Fields!Amount -- NOT the permit item/goods value
    // the proc also returns as TotalAmount.
    expect(cfg.columns.at(-1)).toEqual({
      key: 'Amount',
      dataIndex: 'amount',
      title: 'Total Amount',
      dataType: 'number',
    });
    // No per-currency footer: the old rdlc's only aggregate is the single TOTAL row
    // (rdlc:1457 + :1521 =FORMAT(SUM(Fields!Amount.Value),"N0")), served as ColumnTotals.
    expect(cfg.currencyTotalsColumns).toBeUndefined();
  });

  it('HS Code report restores the old Import Section filter and drilldown carries it through', () => {
    const cfg = reportConfigs.BorderImportPermitByHSCodeReport;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'FormType',
      'ExportImportSectionId',
      'FilterType',
      'hsCode',
      'SakhanId',
    ]);
    expect(
      cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName
    ).toBe('borderImportPermitSections');
    expect(cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe(
      'sakhans'
    );
    expect(cfg.columns.find((column) => column.key === 'hsCode')?.drilldown).toEqual({
      targetReportKey: 'BorderImportPermitHSCodeDetailReport',
      carryFilters: ['FromDate', 'ToDate', 'ExportImportSectionId', 'FilterType', 'SakhanId'],
      rowParams: { hsCode: 'hsCode' },
    });
  });

  it('HS Code detail report keeps section and Sakhan filters for the drilldown page', () => {
    const cfg = reportConfigs.BorderImportPermitHSCodeDetailReport;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'FormType',
      'ExportImportSectionId',
      'FilterType',
      'hsCode',
      'SakhanId',
    ]);
    expect(
      cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName
    ).toBe('borderImportPermitSections');
    expect(cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe(
      'sakhans'
    );
  });
  it('summary reports print on one page like the old RDLC', () => {
    // The legacy report viewer scrolled every row on a single page. At the grid's 10-row
    // default, Company List showed 10 of its 13 rows while the .xlsx (which never pages) had
    // all 13 -- reported as "UI and Excel differ". These result sets are a handful of
    // (group, currency) rows, so one page is both faithful and cheap.
    const onePageReports = [
      'BorderImportPermitByHSCodeReport',
      'BorderImportPermitHSCodeDetailReport',
      'BorderImportPermitBySectionReport',
      'BorderImportPermitBySellerCountryReport',
      'BorderImportPermitCompanyListReport',
      'BorderImportPermitDailyReportNewPermitReport',
    ] as const;

    for (const key of onePageReports) {
      expect(reportConfigs[key].defaultPageSize, key).toBe(1000);
    }
  });

  it('By HS Code shows the old rdlc columns, without a company split', () => {
    // BorderHSCodeReport.rdlc groups on (HSCodeId, Currency) and renders no company column
    // (rdlc:1157-1169). Grouping by company as well split one HS code into a row per buyer,
    // each carrying a partial Total Value. Company Name stays on the HS Code DETAIL drill,
    // whose HSCodeDetailReport.rdlc does render it.
    expect(
      reportConfigs.BorderImportPermitByHSCodeReport.columns.map((column) => column.title)
    ).toEqual(['HS Code', 'Description', 'No of Licences', 'Total Value', 'Currency']);

    expect(
      reportConfigs.BorderImportPermitHSCodeDetailReport.columns.map((column) => column.title)
    ).toContain('Company Name');
  });

  it('New Report renders the legacy per-currency TOTAL block', () => {
    // BorderNewReport.rdlc prints a second tablix: "<CUR>: n licence(s)" + summed Total Value
    // per currency, then a grand "Total: n licence(s)". BasicTable renders that from
    // currencyTotals, which the controller populates via sp_ImportPermitListingCurrencyTotals.
    expect(reportConfigs.BorderImportPermitNewReportNewReport.currencyTotalsColumns).toEqual({
      labelColumnKey: 'LicenceNo',
      valueColumnKey: 'TotalValue',
    });
  });
});

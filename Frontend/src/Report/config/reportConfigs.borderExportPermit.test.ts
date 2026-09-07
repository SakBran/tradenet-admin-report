import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';

describe('Border Export Permit report configs', () => {
  it('action reports keep the old admin filter shape and footer totals', () => {
    const expected = {
      BorderExportPermitActualAmendmentReport: [
        'dateRange',
        'FormType',
        'ExportImportSectionId',
        'AmendRemarkId',
        'CompanyRegistrationNo',
        'CompanyName',
        'SakhanId',
      ],
      BorderExportPermitAmendmentReport: [
        'dateRange',
        'FormType',
        'ExportImportSectionId',
        'AmendRemarkId',
        'CompanyRegistrationNo',
        'CompanyName',
        'SakhanId',
      ],
      BorderExportPermitCancellationReport: [
        'dateRange',
        'FormType',
        'ExportImportSectionId',
        'CompanyRegistrationNo',
        'CompanyName',
        'SakhanId',
      ],
      BorderExportPermitExtensionReport: [
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
        `${key} should use Border Export Permit sections`
      ).toBe('borderExportPermitSections');
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

  it('action report subtitles keep the legacy Border Export Permit wording', () => {
    const sample = { FromDate: '2026-06-01', ToDate: '2026-06-10' };

    for (const key of [
      'BorderExportPermitActualAmendmentReport',
      'BorderExportPermitAmendmentReport',
      'BorderExportPermitCancellationReport',
      'BorderExportPermitExtensionReport',
    ]) {
      expect(reportConfigs[key].reportSubtitle?.(sample), key).toBe(
        'List of Border Export Permit Report From (01/06/2026) To (10/06/2026)'
      );
    }
  });

  it('new report keeps old-admin filters plus Wai Phyo Sakhan/search parity', () => {
    const cfg = reportConfigs.BorderExportPermitNewReportNewReport;

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
    ).toBe('borderExportPermitSections');
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

  it('voucher keeps old-admin filter shape, Sakhan, totals, and dynamic headers', () => {
    const cfg = reportConfigs.BorderExportPermitVoucherReport;
    const resolvedForAmend =
      cfg.resolveColumns?.({ ApplyType: 'Amend' }, cfg.columns) ?? cfg.columns;
    const resolvedForCancel =
      cfg.resolveColumns?.({ ApplyType: 'Cancel' }, cfg.columns) ?? cfg.columns;

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
    ).toBe('borderExportPermitSections');
    expect(cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe(
      'sakhans'
    );
    // No per-currency footer: sp_ExportPermitVoucherCurrencyTotals sums
    // BorderExportPermitItem.Amount (the goods value), which printed a foreign-currency total
    // under the fee column. The old rdlc's only aggregate is the single TOTAL row
    // (BorderVoucherReport.rdlc:1457 + :1521), now served as ColumnTotals["amount"].
    expect(cfg.currencyTotalsColumns).toBeUndefined();
    // rdlc:1631 / :1808 print the fee and its TOTAL with FORMAT(..., "N0"); the same
    // numberFormat drives the grid render and the .xlsx cell style, so both say "18,000".
    expect(cfg.columns.at(-1)).toEqual({
      key: 'Amount',
      dataIndex: 'amount',
      title: 'Total Amount',
      dataType: 'number',
      numberFormat: '#,##0',
    });
    expect(cfg.rowNumberTitle).toBe('No.');
    // header2 / header3 exactly as legacy ReportsController.BorderExportPermitVoucherReport sets
    // them per ApplyType: "Licence Amendment No" / "Amendment Date", "Licence Cancel No" /
    // "Cancellation Date" (the old strings keep the "Licence " prefix on the number column).
    expect(
      resolvedForAmend.find((column) => column.key === 'LicenceNo')?.title
    ).toBe('Licence Amendment No');
    expect(
      resolvedForAmend.find((column) => column.key === 'LicenceDate')?.title
    ).toBe('Amendment Date');
    expect(
      resolvedForCancel.find((column) => column.key === 'LicenceNo')?.title
    ).toBe('Licence Cancel No');
    expect(
      resolvedForCancel.find((column) => column.key === 'LicenceDate')?.title
    ).toBe('Cancellation Date');
  });

  it('HS Code report keeps Export Section, Start/End filter, Sakhan lookup, and detail drilldown', () => {
    const cfg = reportConfigs.BorderExportPermitByHSCodeReport;

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
    ).toBe('borderExportPermitSections');
    expect(cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe(
      'sakhans'
    );
    expect(cfg.columns.find((column) => column.key === 'hsCode')?.drilldown).toEqual({
      targetReportKey: 'BorderExportPermitHSCodeDetailReport',
      carryFilters: ['FromDate', 'ToDate', 'ExportImportSectionId', 'FilterType', 'SakhanId'],
      rowParams: { hsCode: 'hsCode' },
      // BorderHSCodeReport.rdlc:581 opens the detail with window.open(..., '_blank').
      openInNewTab: true,
    });
  });

  it('HS Code report prints the old BorderHSCodeReport.rdlc shape', () => {
    // Owner decision (Border Import Permit twin, 2026-09-05; Border Export Permit complaint
    // 2026-09-07): same result as the old screen, which runs the OVERSEA Export Permit query.
    // The row set is the backend's job (BorderExportPermitByHSCodeLegacyParityTests); the page
    // pins the rdlc's presentation: Sr.No., N4 Total Value, one scrolling page.
    const cfg = reportConfigs.BorderExportPermitByHSCodeReport;

    expect(cfg.columns.map((column) => column.title)).toEqual([
      'HS Code',
      'Description',
      'No of Licences',
      'Total Value',
      'Currency',
    ]);
    expect(cfg.columns.find((column) => column.key === 'TotalValue')).toMatchObject({
      dataType: 'money',
      numberFormat: '#,##0.0000',
    });
    expect(cfg.rowNumberTitle).toBe('Sr.No.');
    expect(cfg.defaultPageSize).toBe(1000);
    expect(cfg.reportSubtitle?.({ FromDate: '2025-01-01', ToDate: '2026-09-06' })).toBe(
      'List of Border Export Permit By HS Code From (01/01/2025) To (06/09/2026)'
    );
    // The summary must NOT carry the drill's grouping flag.
    expect(cfg.filters.some((filter) => filter.name === 'GroupBy')).toBe(false);
  });

  it('HS Code detail drill asks the shared controller for the (HS code, company) grouping', () => {
    const cfg = reportConfigs.BorderExportPermitHSCodeDetailReport;

    expect(cfg.controllerName).toBe('BorderExportPermitByHSCodeReport');
    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'FormType',
      'ExportImportSectionId',
      'FilterType',
      'hsCode',
      'SakhanId',
      'GroupBy',
    ]);
    // Shares the summary's controller; this pinned value is how the backend knows to
    // group on (HS code, company) like the old HSCodeDetailReport.rdlc instead of the
    // summary's (HS code, currency).
    expect(cfg.filters.find((filter) => filter.name === 'GroupBy')?.constantValue).toBe(
      'Company'
    );
    expect(cfg.columns.map((column) => column.title)).toEqual([
      'HS Code',
      'Description',
      'Company Name',
      'No of Licences',
    ]);
    expect(cfg.rowNumberTitle).toBe('Sr.No.');
    expect(cfg.defaultPageSize).toBe(1000);
    // Legacy BorderHSCodeDetailReport header1 = "List of " + FormType + "s By HS Code …" with the
    // FormType the summary posted ("Export Permit") -- kept verbatim.
    expect(cfg.reportSubtitle?.({ FromDate: '2025-01-01', ToDate: '2026-09-06' })).toBe(
      'List of Export Permits By HS Code From (01/01/2025) To (06/09/2026)'
    );
  });

  it('Section, Buyer Country, and Company List drilldowns open Detail in a new tab', () => {
    expect(
      reportConfigs.BorderExportPermitBySectionReport.columns.find(
        (column) => column.key === 'Section'
      )?.drilldown
    ).toMatchObject({
      targetReportKey: 'BorderExportPermitDetailReport',
      rowParams: { ExportImportSectionId: 'sectionId' },
      openInNewTab: true,
    });

    expect(
      reportConfigs.BorderExportPermitBySellerCountryReport.columns.find(
        (column) => column.key === 'Country'
      )?.drilldown
    ).toMatchObject({
      targetReportKey: 'BorderExportPermitDetailReport',
      rowParams: { BuyerCountryId: 'countryId' },
      openInNewTab: true,
    });

    expect(
      reportConfigs.BorderExportPermitCompanyListReport.columns.find(
        (column) => column.key === 'CompanyName'
      )?.drilldown
    ).toMatchObject({
      targetReportKey: 'BorderExportPermitDetailReport',
      rowParams: { CompanyRegistrationNo: 'companyRegistrationNo' },
      openInNewTab: true,
    });
  });

  it('Daily and Detail reports match old-admin Border Export Permit filter boxes', () => {
    for (const key of [
      'BorderExportPermitDailyReportNewPermitReport',
      'BorderExportPermitDetailReport',
    ]) {
      const cfg = reportConfigs[key];

      expect(cfg.filters.map((filter) => filter.name), key).toEqual([
        'dateRange',
        'PaThaKaTypeId',
        'ExportImportSectionId',
        'SakhanId',
      ]);
      expect(cfg.filters.find((filter) => filter.name === 'dateRange')?.defaultDateRangeMonths).toBe(
        3
      );
      expect(cfg.filters.find((filter) => filter.name === 'PaThaKaTypeId')?.lookupName).toBe(
        'paThaKaTypes'
      );
      expect(
        cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName
      ).toBe('borderExportPermitSections');
      expect(cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe(
        'sakhans'
      );
    }
  });

  it('Daily and Detail columns match old RDLC column headers', () => {
    expect(
      reportConfigs.BorderExportPermitDailyReportNewPermitReport.columns.map(
        (column) => column.title
      )
    ).toEqual(['Date', 'No of Licences', 'Total Value', 'Currency', 'Total USD Value']);

    expect(
      reportConfigs.BorderExportPermitDetailReport.columns.map((column) => column.title)
    ).toEqual([
      'Section',
      'Permit No',
      'Permit Date',
      'Company Registration No',
      'Company Name',
      'Company Address',
      'Union Citizenship No',
      'Consignee Name',
      'Consignee Address',
      'Buyer Country',
      'Place/Port of Export',
      'Place/Port of Discharge',
      'Last Date',
      'Country of Orign',
      'Consigned Country',
      'Country of Destination',
      'Type of Permit',
      'HSCode',
      'Decription',
      'A/U',
      'Price',
      'Qty',
      'Value',
      'Currency',
      'Conditions',
    ]);
  });

  it('Detail report column bindings match backend API field names', () => {
    expect(reportConfigs.BorderExportPermitDetailReport.currencyTotalsColumns).toEqual({
      labelColumnKey: 'PermitNo',
      valueColumnKey: 'Value',
    });

    expect(
      reportConfigs.BorderExportPermitDetailReport.columns.map((column) => column.dataIndex)
    ).toEqual([
      'sectionName',
      'licenceNo',
      'licenceDate',
      'companyRegistrationNo',
      'companyName',
      'companyAddress',
      'nrcNo',
      'consigneeName',
      'consigneeAddress',
      'buyerCountry',
      'portofExport',
      'portofDischarge',
      'lastDate',
      'countryofOrigin',
      'consignedCountry',
      'destinationCountry',
      'permitType',
      'hsCode',
      'hsDescription',
      'unit',
      'price',
      'quantity',
      'amount',
      'currency',
      'conditions',
    ]);
  });
});

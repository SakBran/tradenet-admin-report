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

  // Both boxes below are LIVE since 2026-09-10: the report reads the Border tables, whose query
  // filters on SakhanId and ExportImportSectionId. Until then it ran the legacy oversea query
  // bug-for-bug and neither box did anything, which is what the customer complained about.
  it('HS Code report keeps the Import Section and Sakhan filters and the drilldown carries them', () => {
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
      'GroupBy',
    ]);
    // Shares the summary's controller; this pinned value is how the backend knows to
    // group on (HS code, company) like the old HSCodeDetailReport.rdlc instead of the
    // summary's (HS code, currency). The summary itself must not carry it.
    expect(cfg.filters.find((filter) => filter.name === 'GroupBy')?.constantValue).toBe(
      'Company'
    );
    expect(
      reportConfigs.BorderImportPermitByHSCodeReport.filters.some(
        (filter) => filter.name === 'GroupBy'
      )
    ).toBe(false);
    expect(
      cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName
    ).toBe('borderImportPermitSections');
    expect(cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe(
      'sakhans'
    );
  });

  it('HS Code drill is titled after the border permits it lists', () => {
    // Legacy BorderHSCodeDetailReport header1 = "List of " + FormType + "s By HS Code …" built from
    // the FormType the summary posts. That rule is kept; the FormType became "Border Import Permit"
    // when the report moved onto the Border tables on 2026-09-10, so the drill no longer carries
    // the old screen's oversea wording.
    const cfg = reportConfigs.BorderImportPermitHSCodeDetailReport;

    expect(cfg.reportSubtitle?.({ FromDate: '2025-01-01', ToDate: '2026-09-06' })).toBe(
      'List of Border Import Permits By HS Code From (01/01/2025) To (06/09/2026)'
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

  it('Detail report is the old BorderImportPermitDetailReport.rdlc, byte for byte', () => {
    // Owner decision 2026-09-06: identical to the old report even where the old code is
    // wrong. Filter box = Views/Reports/BorderImportPermitDetailReport.cshtml:28-62 (From,
    // To, Sakhan, EIR Card Type, Import Section; Type is the old hidden field). Seller Country
    // and Company Registration No have no box there either -- drill-downs still post them.
    const cfg = reportConfigs.BorderImportPermitDetailReport;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'Type',
      'SakhanId',
      'PaThaKaTypeId',
      'ExportImportSectionId',
    ]);
    expect(cfg.filters.find((filter) => filter.name === 'SakhanId')).toMatchObject({
      label: 'Sakhan',
      lookupName: 'sakhans',
    });
    expect(cfg.filters.find((filter) => filter.name === 'PaThaKaTypeId')).toMatchObject({
      label: 'EIR Card Type',
      lookupName: 'paThaKaTypes',
    });
    expect(cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')).toMatchObject({
      label: 'Import Section',
      lookupName: 'borderImportPermitSections',
    });

    // rdlc header cells in order; Sr.No. is the row-number column.
    expect(cfg.columns.map((column) => column.title)).toEqual([
      'Section',
      'Permit No',
      'Permit Date',
      'Company Registration No',
      'Company Name',
      'Company Address',
      'Union Citizenship No',
      'Agent Name',
      'Agent Address',
      'Seller Country',
      'Port of Shipment',
      'Place/Port of Discharge',
      'Last Date',
      'Country of Orign',
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
    expect(cfg.rowNumberTitle).toBe('Sr.No.');

    // rdlc:2705-2864 FORMAT(Price,"N4") / FORMAT(Quantity,"N2") / FORMAT(Amount,"N4"); the
    // model's sLicenceDate / LastDate are .ToString("dd/MM/yyyy").
    const column = (dataIndex: string) =>
      cfg.columns.find((candidate) => candidate.dataIndex === dataIndex);
    expect(column('price')).toMatchObject({ dataType: 'money', numberFormat: '#,##0.0000' });
    expect(column('quantity')).toMatchObject({ dataType: 'money', numberFormat: '#,##0.00' });
    expect(column('amount')).toMatchObject({ dataType: 'money', numberFormat: '#,##0.0000' });
    expect(column('licenceDate')).toMatchObject({ dataType: 'date', dateFormat: 'DD/MM/YYYY' });
    expect(column('lastDate')).toMatchObject({ dataType: 'date', dateFormat: 'DD/MM/YYYY' });
    // Company Address is the old CommonRepository.GetAddress string, now sent by the API.
    expect(column('companyAddress')?.title).toBe('Company Address');

    // Legacy header1 line; one page like the old ReportViewer; the rdlc has no footer.
    expect(
      cfg.reportSubtitle?.({ FromDate: '2025-01-01T00:00:00', ToDate: '2025-12-31T23:59:59' })
    ).toBe('List of Border Import Permit By Detail From (01/01/2025) To (31/12/2025)');
    expect(cfg.defaultPageSize).toBe(1000);
    expect(cfg.currencyTotalsColumns).toBeUndefined();
  });

  it('By Section is the old BorderImportPermitBySectionReport.rdlc, byte for byte', () => {
    // Owner decision 2026-09-06. Filter box = Views/Reports/BorderImportPermitBySectionReport
    // .cshtml:25-59 (From, To, Sakhan, EIR Card Type, Import Section; Type is the old hidden
    // field); Seller Country / Company Registration No have no box there (drill-downs still
    // post them).
    const cfg = reportConfigs.BorderImportPermitBySectionReport;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'Type',
      'SakhanId',
      'PaThaKaTypeId',
      'ExportImportSectionId',
    ]);
    expect(cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe('sakhans');
    expect(cfg.filters.find((filter) => filter.name === 'PaThaKaTypeId')).toMatchObject({
      label: 'EIR Card Type',
      lookupName: 'paThaKaTypes',
    });

    // rdlc header cells in order; Sr.No. is the Code group counter = the row number.
    expect(cfg.columns.map((column) => column.title)).toEqual([
      'Section',
      'No of Licences',
      'Total Value',
      'Currency',
    ]);
    expect(cfg.rowNumberTitle).toBe('Sr.No.');

    // rdlc:688 FORMAT(Sum(Amount),"N4"); the count stays a bare integer.
    expect(cfg.columns.find((column) => column.dataIndex === 'totalValue')).toMatchObject({
      dataType: 'money',
      numberFormat: '#,##0.0000',
    });
    expect(cfg.columns.find((column) => column.dataIndex === 'noOfLicences')?.numberFormat)
      .toBeUndefined();

    // rdlc:610: Section opens the Detail report in a new window, filtered to the row's section
    // with the search's dates, card type and Sakhan carried along.
    expect(cfg.columns.find((column) => column.dataIndex === 'sectionName')?.drilldown).toEqual({
      targetReportKey: 'BorderImportPermitDetailReport',
      carryFilters: ['FromDate', 'ToDate', 'PaThaKaTypeId', 'SakhanId'],
      rowParams: { ExportImportSectionId: 'sectionId' },
      openInNewTab: true,
    });

    // Legacy header1 line; one page like the old ReportViewer; no per-currency footer block.
    expect(
      cfg.reportSubtitle?.({ FromDate: '2025-01-01T00:00:00', ToDate: '2025-12-31T23:59:59' })
    ).toBe('List of Border Import Permit By Section From (01/01/2025) To (31/12/2025)');
    expect(cfg.defaultPageSize).toBe(1000);
    expect(cfg.currencyTotalsColumns).toBeUndefined();
  });

  it('Company List is the old BorderImportPermitByCompanyReport.rdlc, byte for byte', () => {
    // Owner decision 2026-09-06. Filter box = Views/Reports/BorderImportPermitByCompanyReport
    // .cshtml:25-72 (From, To, Sakhan, EIR Card Type, Import Section, Company Registration No,
    // readonly Company Name; Type is the old hidden field). No Seller Country box there.
    const cfg = reportConfigs.BorderImportPermitCompanyListReport;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'Type',
      'SakhanId',
      'PaThaKaTypeId',
      'ExportImportSectionId',
      'CompanyRegistrationNo',
      'CompanyName',
    ]);
    expect(cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe('sakhans');
    expect(cfg.filters.find((filter) => filter.name === 'PaThaKaTypeId')?.lookupName).toBe(
      'paThaKaTypes'
    );
    expect(cfg.filters.find((filter) => filter.name === 'CompanyName')).toMatchObject({
      type: 'readonlyText',
      populateFromCompanyRegistrationNo: true,
    });

    // rdlc header cells in order; Sr.No. is the Code group counter = the row number.
    expect(cfg.columns.map((column) => column.title)).toEqual([
      'Company Name',
      'No of Licences',
      'Total Value',
      'Currency',
    ]);
    expect(cfg.rowNumberTitle).toBe('Sr.No.');
    expect(cfg.columns.find((column) => column.dataIndex === 'totalValue')).toMatchObject({
      dataType: 'money',
      numberFormat: '#,##0.0000',
    });

    // rdlc:608: Company Name opens the Detail report in a new window for that registration
    // number, carrying the search's dates, card type, section and Sakhan.
    expect(cfg.columns.find((column) => column.dataIndex === 'companyName')?.drilldown).toEqual({
      targetReportKey: 'BorderImportPermitDetailReport',
      carryFilters: ['FromDate', 'ToDate', 'PaThaKaTypeId', 'ExportImportSectionId', 'SakhanId'],
      rowParams: { CompanyRegistrationNo: 'companyRegistrationNo' },
      openInNewTab: true,
    });

    // The legacy header1 verbatim: legacy ReportsController.cs:15425 labels this BORDER screen
    // "List of Import Permit By Company (from) To (to)" -- no "Border", no "From". Kept.
    expect(
      cfg.reportSubtitle?.({ FromDate: '2025-01-01T00:00:00', ToDate: '2025-12-31T23:59:59' })
    ).toBe('List of Import Permit By Company (01/01/2025) To (31/12/2025)');
    expect(cfg.defaultPageSize).toBe(1000);
    expect(cfg.currencyTotalsColumns).toBeUndefined();
  });
});

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
    expect(cfg.currencyTotalsColumns).toEqual({
      labelColumnKey: 'LicenceNo',
      valueColumnKey: 'Amount',
    });
    expect(
      resolvedForAmend.find((column) => column.key === 'LicenceNo')?.title
    ).toBe('Amendment No');
    expect(
      resolvedForAmend.find((column) => column.key === 'LicenceDate')?.title
    ).toBe('Amendment Date');
    expect(
      resolvedForCancel.find((column) => column.key === 'LicenceNo')?.title
    ).toBe('Cancellation No');
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
    });
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

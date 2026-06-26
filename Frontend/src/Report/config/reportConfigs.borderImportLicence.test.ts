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
  it('Detail and Pending Detail expose report titles', () => {
    expect(reportConfigs.BorderImportLicenceDetailReport.title).toBe(
      'Border Import Licence Detail Report'
    );
    expect(reportConfigs.BorderImportLicenceDetailReport.reportSubtitle?.({
      FromDate: '2026-06-01',
      ToDate: '2026-06-10',
    })).toBe('List of Border Import Licences By Detail From (01/06/2026) To (10/06/2026)');

    expect(reportConfigs.BorderImportLicenceDetailReportPending.title).toBe(
      'Border Import Licence Detail Report (Pending)'
    );
    expect(reportConfigs.BorderImportLicenceDetailReportPending.reportSubtitle?.({
      FromDate: '2026-06-01',
      ToDate: '2026-06-10',
    })).toBe(
      'List of Border Import Licences By Detail From (01/06/2026) To (10/06/2026)'
    );
  });

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

  it('Pending Detail matches the old RDLC date headers and filter shape', () => {
    const cfg = reportConfigs.BorderImportLicenceDetailReportPending;

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

  it('Total Value & Licences matches old Border Import filter scope', () => {
    const cfg = reportConfigs.BorderImportLicenceTotalValueLicencesReport;

    expect(cfg.reportSubtitle?.({
      FromDate: '2026-06-01',
      ToDate: '2026-06-10',
    })).toBe('Border Import Licences Total Value & Licences (01/06/2026) To (10/06/2026)');

    expect(cfg.filters.map((f) => f.name)).toEqual([
      'dateRange',
      'SakhanId',
      'PaThaKaTypeId',
      'ExportImportSectionId',
    ]);

    expect(cfg.filters.find((f) => f.name === 'ExportImportSectionId')?.lookupName).toBe(
      'borderImportLicenceSections'
    );
  });

  it('action reports keep the Border Import Licence filter shape and footer totals', () => {
    const expected = {
      BorderImportLicenceActualAmendmentReport: [
        'dateRange',
        'FormType',
        'SakhanId',
        'ExportImportSectionId',
        'AmendRemarkId',
        'CompanyRegistrationNo',
        'CompanyName',
      ],
      BorderImportLicenceAmendmentReport: [
        'dateRange',
        'FormType',
        'SakhanId',
        'ExportImportSectionId',
        'AmendRemarkId',
        'CompanyRegistrationNo',
        'CompanyName',
      ],
      BorderImportLicenceCancellationReport: [
        'dateRange',
        'FormType',
        'SakhanId',
        'ExportImportSectionId',
        'CompanyRegistrationNo',
        'CompanyName',
      ],
      BorderImportLicenceExtensionReport: [
        'dateRange',
        'FormType',
        'SakhanId',
        'ExportImportSectionId',
        'CompanyRegistrationNo',
        'CompanyName',
      ],
    } as const;

    for (const [key, filters] of Object.entries(expected)) {
      const cfg = reportConfigs[key];

      expect(cfg.filters.map((filter) => filter.name), key).toEqual(filters);
      expect(
        cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName,
        `${key} should use Border Import Licence sections`
      ).toBe('borderImportLicenceSections');
      expect(
        cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName,
        `${key} should keep Sakhan lookup`
      ).toBe('sakhans');
      expect(
        cfg.filters.find((filter) => filter.name === 'CompanyName')?.type,
        `${key} should keep readonly company name`
      ).toBe('readonlyText');
      expect(cfg.currencyTotalsColumns, `${key} footer totals`).toEqual({
        labelColumnKey: 'LicenceNo',
        valueColumnKey: 'TotalValue',
      });
    }
  });

  it('action report subtitles keep the legacy Border Import Licence wording', () => {
    const sample = { FromDate: '2026-06-01', ToDate: '2026-06-10' };

    for (const key of [
      'BorderImportLicenceActualAmendmentReport',
      'BorderImportLicenceAmendmentReport',
      'BorderImportLicenceCancellationReport',
      'BorderImportLicenceExtensionReport',
    ]) {
      expect(reportConfigs[key].reportSubtitle?.(sample), key).toBe(
        'List of Border Import Licence Report From (01/06/2026) To (10/06/2026)'
      );
    }
  });

  it('voucher keeps old-admin filter shape, Sakhan, and dynamic headers', () => {
    const cfg = reportConfigs.BorderImportLicenceVoucherReport;
    const resolvedForAmend =
      cfg.resolveColumns?.({ ApplyType: 'Amend' }, cfg.columns) ?? cfg.columns;
    const resolvedForCancel =
      cfg.resolveColumns?.({ ApplyType: 'Cancel' }, cfg.columns) ?? cfg.columns;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'FormType',
      'ExportImportSectionId',
      'ApplyType',
      'PaymentType',
      'CompanyRegistrationNo',
      'CompanyName',
      'SakhanId',
    ]);
    expect(
      cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName
    ).toBe('borderImportLicenceSections');
    expect(cfg.filters.find((filter) => filter.name === 'PaymentType')?.lookupName).toBe(
      'paymentTypes'
    );
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

  it('HS Code report keeps Import Section, Start/End filter, Sakhan lookup, and detail drilldown', () => {
    const cfg = reportConfigs.BorderImportLicenceByHSCodeReport;

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
    ).toBe('borderImportLicenceSections');
    expect(cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe(
      'sakhans'
    );
    expect(cfg.columns.find((column) => column.key === 'hsCode')?.drilldown).toEqual({
      targetReportKey: 'BorderImportLicenceHSCodeDetailReport',
      carryFilters: ['FromDate', 'ToDate', 'ExportImportSectionId', 'FilterType', 'SakhanId'],
      rowParams: { hsCode: 'hsCode' },
    });
  });

  it('summary reports link to Border Import Licence detail like Import Licence references', () => {
    expect(
      reportConfigs.BorderImportLicenceBySectionReport.columns.find(
        (column) => column.key === 'Section'
      )?.drilldown
    ).toEqual({
      targetReportKey: 'BorderImportLicenceDetailReport',
      carryFilters: [
        'FromDate',
        'ToDate',
        'SakhanId',
        'PaThaKaTypeId',
        'ExportImportMethodId',
      ],
      rowParams: { ExportImportSectionId: 'sectionId', Currency: 'currency' },
      openInNewTab: true,
    });

    expect(
      reportConfigs.BorderImportLicenceByMethodReport.columns.find(
        (column) => column.key === 'Method'
      )?.drilldown
    ).toEqual({
      targetReportKey: 'BorderImportLicenceDetailReport',
      carryFilters: [
        'FromDate',
        'ToDate',
        'SakhanId',
        'PaThaKaTypeId',
        'ExportImportSectionId',
      ],
      rowParams: { ExportImportMethodId: 'methodId', Currency: 'currency' },
    });

    expect(
      reportConfigs.BorderImportLicenceBySellerCountryReport.columns.find(
        (column) => column.key === 'Country'
      )?.drilldown
    ).toEqual({
      targetReportKey: 'BorderImportLicenceDetailReport',
      carryFilters: [
        'FromDate',
        'ToDate',
        'SakhanId',
        'PaThaKaTypeId',
        'ExportImportSectionId',
        'ExportImportMethodId',
      ],
      rowParams: { SellerCountryId: 'countryId', Currency: 'currency' },
    });

    expect(
      reportConfigs.BorderImportLicenceCompanyListReport.columns.find(
        (column) => column.key === 'CompanyName'
      )?.drilldown
    ).toEqual({
      targetReportKey: 'BorderImportLicenceDetailReport',
      carryFilters: [
        'FromDate',
        'ToDate',
        'SakhanId',
        'PaThaKaTypeId',
        'ExportImportSectionId',
        'ExportImportMethodId',
      ],
      rowParams: { CompanyRegistrationNo: 'companyRegistrationNo', Currency: 'currency' },
    });
  });
});

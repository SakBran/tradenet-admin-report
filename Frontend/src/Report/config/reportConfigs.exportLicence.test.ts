import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';

describe('Export Licence report configs', () => {
  it('summary reports match old admin filter boxes and legacy titles', () => {
    const expected = {
      ExportLicenceByMethodReport: {
        filters: [
          'dateRange',
          'PaThaKaTypeId',
          'ExportImportSectionId',
          'ExportImportMethodId',
          'Auto',
        ],
        subtitle: 'List of Export Licences By Method From (01/02/2026) To (03/02/2026)',
      },
      ExportLicenceBySectionReport: {
        filters: [
          'dateRange',
          'PaThaKaTypeId',
          'ExportImportSectionId',
          'ExportImportMethodId',
          'Auto',
        ],
        subtitle: 'List of Export Licences By Section From (01/02/2026) To (03/02/2026)',
      },
      ExportLicenceBySellerCountryReport: {
        filters: [
          'dateRange',
          'PaThaKaTypeId',
          'ExportImportSectionId',
          'ExportImportMethodId',
          'BuyerCountryId',
          'Auto',
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
          'Auto',
          'CompanyName',
        ],
        subtitle: 'List of Export Licences By Company From (01/02/2026) To (03/02/2026)',
      },
      ExportLicenceDailyReportNewLicenceReport: {
        filters: [
          'dateRange',
          'ExportImportSectionId',
          'ExportImportMethodId',
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

  it('Seller Country route displays Buyer Country report title for export licence', () => {
    expect(reportConfigs.ExportLicenceBySellerCountryReport.title).toBe(
      'Export Licence By Buyer Country Report'
    );
  });

  it('Total Value & Licences report keeps the old three-filter box', () => {
    const cfg = reportConfigs.ExportLicenceTotalValueLicencesReport;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'PaThaKaTypeId',
      'ExportImportSectionId',
    ]);
    expect(cfg.filters.find((filter) => filter.name === 'PaThaKaTypeId')?.lookupName).toBe(
      'paThaKaTypes'
    );
    expect(
      cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName
    ).toBe('exportLicenceSections');
  });

  it('summary links drill into Export Licence Detail with clicked values applied', () => {
    const expected = {
      ExportLicenceByMethodReport: {
        columnKey: 'Method',
        drilldown: {
          targetReportKey: 'ExportLicenceDetailReport',
          carryFilters: ['FromDate', 'ToDate', 'PaThaKaTypeId', 'ExportImportSectionId', 'Auto'],
          rowParams: { ExportImportMethodId: 'methodId' },
        },
      },
      ExportLicenceBySectionReport: {
        columnKey: 'Section',
        drilldown: {
          targetReportKey: 'ExportLicenceDetailReport',
          carryFilters: ['FromDate', 'ToDate', 'PaThaKaTypeId', 'ExportImportMethodId', 'Auto'],
          rowParams: { ExportImportSectionId: 'sectionId' },
        },
      },
      ExportLicenceBySellerCountryReport: {
        columnKey: 'Country',
        drilldown: {
          targetReportKey: 'ExportLicenceDetailReport',
          carryFilters: ['FromDate', 'ToDate', 'PaThaKaTypeId', 'ExportImportSectionId', 'ExportImportMethodId', 'Auto'],
          rowParams: { BuyerCountryId: 'countryId' },
        },
      },
      ExportLicenceCompanyListReport: {
        columnKey: 'CompanyName',
        drilldown: {
          targetReportKey: 'ExportLicenceDetailReport',
          carryFilters: ['FromDate', 'ToDate', 'PaThaKaTypeId', 'ExportImportSectionId', 'ExportImportMethodId', 'Auto'],
          rowParams: { CompanyRegistrationNo: 'companyRegistrationNo' },
        },
      },
    };

    for (const [reportKey, { columnKey, drilldown }] of Object.entries(expected)) {
      const column = reportConfigs[reportKey].columns.find((item) => item.key === columnKey);

      expect(column?.drilldown, reportKey).toEqual(drilldown);
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

  it('Detail report shows the legacy date-range report title', () => {
    expect(
      reportConfigs.ExportLicenceDetailReport.reportSubtitle?.({
        FromDate: '2026-02-01',
        ToDate: '2026-02-03',
      })
    ).toBe('List of Export Licences By Detail From (01/02/2026) To (03/02/2026)');
  });

  it('Detail report exposes the Auto / None Auto filter', () => {
    const autoFilter = reportConfigs.ExportLicenceDetailReport.filters.find(
      (filter) => filter.name === 'Auto'
    );

    expect(autoFilter?.label).toBe('Auto / None Auto');
    expect(autoFilter?.type).toBe('select');
    expect(autoFilter?.defaultValue).toBe('');
    expect(autoFilter?.options).toEqual([
      { label: '--- All ---', value: '' },
      { label: 'auto', value: 'auto' },
      { label: 'none-auto', value: 'none-auto' },
    ]);
  });

  it('Detail report renders currency totals under Licence No and Value', () => {
    expect(reportConfigs.ExportLicenceDetailReport.currencyTotalsColumns).toEqual({
      labelColumnKey: 'LicenceNo',
      valueColumnKey: 'Value',
    });
  });

  it('action reports keep the old oversea filter shape and do not expose Sakhan', () => {
    const expected = {
      ExportLicenceActualAmendmentReport: [
        'dateRange',
        'FormType',
        'ExportImportSectionId',
        'AmendRemarkId',
        'CompanyRegistrationNo',
        'CompanyName',
      ],
      ExportLicenceAmendmentReport: [
        'dateRange',
        'FormType',
        'ExportImportSectionId',
        'AmendRemarkId',
        'CompanyRegistrationNo',
        'CompanyName',
        'Auto',
      ],
      ExportLicenceCancellationReport: [
        'dateRange',
        'FormType',
        'ExportImportSectionId',
        'CompanyRegistrationNo',
        'CompanyName',
      ],
      ExportLicenceExtensionReport: [
        'dateRange',
        'FormType',
        'ExportImportSectionId',
        'CompanyRegistrationNo',
        'CompanyName',
      ],
      ExportLicenceNewReportNewReport: [
        'dateRange',
        'FormType',
        'ExportImportSectionId',
        'CompanyRegistrationNo',
        'CompanyName',
        'Auto',
      ],
      ExportLicenceVoucherReport: [
        'dateRange',
        'FormType',
        'ExportImportSectionId',
        'ApplyType',
        'PaymentType',
        'CompanyRegistrationNo',
        'CompanyName',
      ],
    } as const;

    for (const [key, filters] of Object.entries(expected)) {
      const cfg = reportConfigs[key];
      expect(cfg.filters.map((filter) => filter.name), key).toEqual(filters);
      expect(cfg.filters.some((filter) => filter.name === 'SakhanId'), `${key} should not expose Sakhan`).toBe(false);
      expect(
        cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName,
        `${key} should use Export Licence sections`
      ).toBe('exportLicenceSections');
      expect(
        cfg.filters.find((filter) => filter.name === 'CompanyName')?.type,
        `${key} should keep readonly company name lookup`
      ).toBe('readonlyText');
    }
  });

  it('action report subtitles keep the legacy Export Licence wording', () => {
    const sample = { FromDate: '2026-02-01', ToDate: '2026-02-03' };

    expect(
      reportConfigs.ExportLicenceActualAmendmentReport.reportSubtitle?.(sample)
    ).toBe('List of Export Licence Report From (01/02/2026) To (03/02/2026)');
    expect(
      reportConfigs.ExportLicenceAmendmentReport.reportSubtitle?.(sample)
    ).toBe('List of Export Licence Report From (01/02/2026) To (03/02/2026)');
    expect(
      reportConfigs.ExportLicenceCancellationReport.reportSubtitle?.(sample)
    ).toBe('List of Export Licence Report From (01/02/2026) To (03/02/2026)');
    expect(
      reportConfigs.ExportLicenceExtensionReport.reportSubtitle?.(sample)
    ).toBe('List of Export Licence Report From (01/02/2026) To (03/02/2026)');
    expect(
      reportConfigs.ExportLicenceNewReportNewReport.reportSubtitle?.(sample)
    ).toBe('List of Export Licence Report From (01/02/2026) To (03/02/2026)');
    expect(
      reportConfigs.ExportLicenceVoucherReport.reportSubtitle?.(sample)
    ).toBe('Export Licence Voucher List (01/02/2026) To (03/02/2026)');
  });

  it('action reports hide the old HSCode column where the old RDLC did not show it', () => {
    for (const key of [
      'ExportLicenceActualAmendmentReport',
      'ExportLicenceAmendmentReport',
      'ExportLicenceCancellationReport',
    ]) {
      expect(
        reportConfigs[key].columns.some((column) => column.dataIndex === 'hsCode'),
        `${key} should not show hsCode`
      ).toBe(false);
    }
  });

  it('voucher keeps dynamic licence headers and no Sakhan filter', () => {
    const cfg = reportConfigs.ExportLicenceVoucherReport;
    const resolvedForAmend = cfg.resolveColumns?.(
      { ApplyType: 'Amend' },
      cfg.columns
    ) ?? cfg.columns;
    const resolvedForCancel = cfg.resolveColumns?.(
      { ApplyType: 'Cancel' },
      cfg.columns
    ) ?? cfg.columns;

    expect(cfg.filters.some((filter) => filter.name === 'SakhanId')).toBe(false);
    expect(
      cfg.filters.find((filter) => filter.name === 'PaymentType')?.lookupName
    ).toBe('paymentTypes');
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
});

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
      // No FormType, same as the New report: ExportLicenceCancelReport.cshtml keeps it
      // hidden and the controller hardcodes "Export Licence".
      ExportLicenceCancellationReport: [
        'dateRange',
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
      // No FormType: the old view kept it hidden and the controller hardcodes
      // "Export Licence", so the box filtered nothing.
      ExportLicenceNewReportNewReport: [
        'dateRange',
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
    // Cancellation is NOT in this list: CancelReport.rdlc on tradenet-2.0-admin
    // origin/master shows HSCode at rdlc:391 and again at rdlc:955. The earlier
    // exclusion came from the repo's stale 2022 working tree.
    for (const key of [
      'ExportLicenceActualAmendmentReport',
      'ExportLicenceAmendmentReport',
    ]) {
      expect(
        reportConfigs[key].columns.some((column) => column.dataIndex === 'hsCode'),
        `${key} should not show hsCode`
      ).toBe(false);
    }
  });

  it('cancellation matches CancelReport.rdlc: HS Code once, Licence No is the original', () => {
    const cfg = reportConfigs.ExportLicenceCancellationReport;
    const column = (key: string) => cfg.columns.find((c) => c.key === key);

    // rdlc:1229 "Licence No" = OldLicenceNo, rdlc:1282 "Cancellation No" = LicenceNo.
    expect(column('LicenceNo')?.dataIndex).toBe('oldLicenceNo');
    expect(column('CancellationNo')?.dataIndex).toBe('licenceNo');

    // rdlc:391 + rdlc:955 render HSCode twice; we render it once, beside Total Value.
    expect(cfg.columns.filter((c) => c.dataIndex === 'hsCode')).toHaveLength(1);
    expect(cfg.columns.slice(-4).map((c) => c.key)).toEqual([
      'Currency',
      'TotalValue',
      'HSCode',
      'Remark',
    ]);

    // rdlc:1600 is =Fields!Amount.Value with no <Format>, so decimal(18,4) prints 4 dp.
    expect(column('TotalValue')?.numberFormat).toBe('#,##0.0000');

    // The per-currency footer (rdlc Tablix2) must land on real columns.
    expect(column(cfg.currencyTotalsColumns!.labelColumnKey)).toBeDefined();
    expect(column(cfg.currencyTotalsColumns!.valueColumnKey)).toBeDefined();

    // The pager needs a real total, or there is no last page to jump to.
    expect(cfg.eagerTotalCount).toBe(true);
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
    expect(
      cfg.columns.some((column) => column.key === 'CommodityType'),
      'voucher should keep Commodity Type per PM feedback'
    ).toBe(true);
    // Dead config removed: this controller populates no footer at all (no CurrencyTotals /
    // ColumnTotals call), so a currencyTotalsColumns placement pointed at nothing. The old
    // VoucherReport.rdlc's only aggregate is its single TOTAL row (:1709 + :1828
    // =FORMAT(SUM(Fields!Amount.Value),"N0")), which this report still owes.
    expect(cfg.currencyTotalsColumns).toBeUndefined();
  });

  // Extension report Licence No / Extension No bindings live in
  // reportConfigs.extension.test.ts, which covers the whole Extension family at once.

  it('new report keeps PM-requested auto filter and visible business columns', () => {
    const cfg = reportConfigs.ExportLicenceNewReportNewReport;

    // FormType is absent by design: the old ExportLicenceNewReport.cshtml kept it hidden and
    // the controller hardcodes "Export Licence", so a visible box filtered nothing.
    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'ExportImportSectionId',
      'CompanyRegistrationNo',
      'CompanyName',
      'Auto',
    ]);

    expect(cfg.filters.some((filter) => filter.name === 'SakhanId')).toBe(false);

    // NewLicenceReport.rdlc's Currency row-group footer: the controller populates
    // CurrencyTotals, so this placement must stay wired.
    expect(cfg.currencyTotalsColumns).toEqual({
      labelColumnKey: 'LicenceNo',
      valueColumnKey: 'TotalValue',
    });
    expect(
      cfg.columns.map((column) => column.key)
    ).toEqual([
      'Section',
      'LicenceNo',
      'CompanyRegistrationNo',
      'CompanyName',
      'CompanyAddress',
      'Currency',
      'TotalValue',
      'CommodityType',
      'hsCode',
      'Quota',
      'Auto',
    ]);
  });
});

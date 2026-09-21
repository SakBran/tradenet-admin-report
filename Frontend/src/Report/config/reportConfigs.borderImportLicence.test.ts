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

    // Header text per BorderImportLicenceDetailReport.rdlc:643 ("Licence Date", bound to
    // Fields!sLicenceDate). The old RDLC has no "Create Date" column; Approve Date is an
    // extra the new report adds.
    expect(dateColumns).toEqual([
      ['ApplicationDate', 'applicationDate', 'Application Date'],
      ['LicenceDate', 'licenceDate', 'Licence Date'],
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

    // Same RDLC as the Detail report (ReportsController.cs:11335 points the Pending binder
    // at BorderImportLicenceDetailReport.rdlc), so the headers must match it too.
    expect(dateColumns).toEqual([
      ['ApplicationDate', 'applicationDate', 'Application Date'],
      ['LicenceDate', 'licenceDate', 'Licence Date'],
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

  it('New Report matches old filters, headers, and footer totals', () => {
    const cfg = reportConfigs.BorderImportLicenceNewReportNewReport;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'SakhanId',
      'ExportImportSectionId',
      'CompanyRegistrationNo',
      'CompanyName',
    ]);
    expect(
      cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName
    ).toBe('borderImportLicenceSections');
    expect(cfg.filters.find((filter) => filter.name === 'CompanyName')?.type).toBe(
      'readonlyText'
    );
    expect(cfg.columns.find((column) => column.key === 'Currency')?.title).toBe(
      'Curency'
    );
    expect(cfg.columns.find((column) => column.key === 'Auto')?.title).toBe('auto');
    expect(cfg.currencyTotalsColumns).toEqual({
      labelColumnKey: 'LicenceNo',
      valueColumnKey: 'TotalValue',
    });
  });

  it('voucher keeps old-admin filter shape, Sakhan, and dynamic headers', () => {
    const cfg = reportConfigs.BorderImportLicenceVoucherReport;
    const resolvedForNew =
      cfg.resolveColumns?.({ ApplyType: 'New' }, cfg.columns) ?? cfg.columns;
    const resolvedForAmend =
      cfg.resolveColumns?.({ ApplyType: 'Amend' }, cfg.columns) ?? cfg.columns;
    const resolvedForCancel =
      cfg.resolveColumns?.({ ApplyType: 'Cancel' }, cfg.columns) ?? cfg.columns;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
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
    expect(cfg.filters.find((filter) => filter.name === 'ApplyType')).toMatchObject({
      defaultValue: 'New',
      options: [
        { label: 'New', value: 'New' },
        { label: 'Amend', value: 'Amend' },
        { label: 'Extension', value: 'Extension' },
        { label: 'Cancel', value: 'Cancel' },
        { label: 'Actual Amend', value: 'Actual Amend' },
      ],
    });
    expect(cfg.filters.find((filter) => filter.name === 'PaymentType')?.options).toEqual([
      { label: '--- All ---', value: '' },
      { label: 'Cash', value: 'Cash' },
      { label: 'MPU', value: 'MPU' },
      { label: 'Citizen Pay', value: 'Citizen Pay' },
    ]);
    expect(cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe(
      'sakhans'
    );
    expect(cfg.currencyTotalsColumns).toBeUndefined();
    expect(resolvedForNew.filter((column) => column.title === 'Licence No')).toHaveLength(1);
    expect(resolvedForNew.some((column) => column.key === 'LicenceNo')).toBe(false);
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

  it('pending report keeps only the old date and Border Import Section filters', () => {
    const cfg = reportConfigs.BorderImportLicencePendingReport;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'ExportImportSectionId',
    ]);
    expect(
      cfg.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName
    ).toBe('borderImportLicenceSections');
    expect(cfg.columns.find((column) => column.key === 'HSCode')).toMatchObject({
      dataIndex: 'hsCode',
      title: 'HSCode',
    });
    expect(cfg.columns.find((column) => column.key === 'Currency')?.title).toBe(
      'Curency'
    );
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
    // The old HS Code cell opens Reports/BorderHSCodeDetailReport in a new window
    // (BorderHSCodeReport.rdlc:581 window.open(...,'_blank'); ReportsController.cs:12430 on
    // origin/master) -- the (HS code, company) list, not the Border Import Licence Detail report.
    expect(cfg.columns.find((column) => column.key === 'hsCode')?.drilldown).toEqual({
      targetReportKey: 'BorderImportLicenceHSCodeDetailReport',
      carryFilters: ['FromDate', 'ToDate', 'ExportImportSectionId', 'FilterType', 'SakhanId'],
      rowParams: { hsCode: 'hsCode' },
      openInNewTab: true,
    });
  });

  it('By HS Code prints the old rdlc shape, one row per (HS code, currency)', () => {
    // Complaint 2026-09-14: one HS code applied for 3 times in USD printed 3 rows, the old
    // report one row with the values summed. BorderHSCodeReport.rdlc has no company column
    // (rdlc:232-452) and groups on HSCodeId + Currency only (rdlc:1160-1161); the backend had
    // added the buyer company to that key (9213 rows against the old report's 2881 for 2025).
    const cfg = reportConfigs.BorderImportLicenceByHSCodeReport;

    // BorderHSCodeReport.rdlc:232/287/342/397/452.
    expect(cfg.columns.map((column) => column.title)).toEqual([
      'HS Code',
      'Description',
      'No of Licences',
      'Total Value',
      'Currency',
    ]);
    // BorderHSCodeReport.rdlc:177 prints "Sr.No.", :713 prints Total Value as FORMAT(...,"N4").
    expect(cfg.rowNumberTitle).toBe('Sr.No.');
    expect(cfg.columns.find((column) => column.key === 'TotalValue')).toMatchObject({
      dataType: 'money',
      numberFormat: '#,##0.0000',
    });
    expect(cfg.columns.find((column) => column.key === 'NoOfLicences')?.dataType).toBe(
      'number'
    );
    // The RDLC scrolled every row on one page.
    expect(cfg.defaultPageSize).toBe(1000);
    // Legacy header verbatim, plural "Licences" (ReportsController.cs:12432 on origin/master).
    expect(
      cfg.reportSubtitle?.({ FromDate: '2025-01-01', ToDate: '2025-12-31' })
    ).toBe('List of Border Import Licences By HS Code From (01/01/2025) To (31/12/2025)');

    // The old form offers Start / End only, Start first, no All option.
    const filterType = cfg.filters.find((filter) => filter.name === 'FilterType');
    expect(filterType?.defaultValue).toBe('Start');
    expect(filterType?.options).toEqual([
      { label: 'Start', value: 'Start' },
      { label: 'End', value: 'End' },
    ]);
  });

  it('HS Code detail drill pins the company grouping its controller cannot infer', () => {
    const detail = reportConfigs.BorderImportLicenceHSCodeDetailReport;

    // Same endpoint as the summary, so it must stay out of the menu (createReportItem keys
    // the menu off controllerName).
    expect(detail.hideInMenu).toBe(true);
    expect(detail.controllerName).toBe('BorderImportLicenceByHSCodeReport');
    expect(detail.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'FormType',
      'ExportImportSectionId',
      'FilterType',
      'hsCode',
      'SakhanId',
      'GroupBy',
    ]);
    // HSCodeDetailReport.rdlc groups on (HS code, company) (rdlc:1263-1264); the summary's
    // BorderHSCodeReport.rdlc on (HS code, currency), and both post the same parameters.
    expect(detail.filters.find((filter) => filter.name === 'GroupBy')?.constantValue).toBe(
      'Company'
    );
    expect(
      reportConfigs.BorderImportLicenceByHSCodeReport.filters.some(
        (filter) => filter.name === 'GroupBy'
      )
    ).toBe(false);
    expect(
      detail.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName
    ).toBe('borderImportLicenceSections');
    expect(detail.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe(
      'sakhans'
    );
    // HSCodeDetailReport.rdlc:500/555/610/665 -- Company Name, and no Currency / Total Value.
    expect(detail.columns.map((column) => column.title)).toEqual([
      'HS Code',
      'Description',
      'Company Name',
      'No of Licences',
    ]);
    // HSCodeDetailReport.rdlc:445 prints "Sr.No."; the RDLC scrolled every row on one page.
    expect(detail.rowNumberTitle).toBe('Sr.No.');
    expect(detail.defaultPageSize).toBe(1000);
    // Old BorderHSCodeDetailReport header: "List of " + FormType + "s By HS Code From (..) To
    // (..)" (ReportsController.cs:10589 on origin/master).
    expect(
      detail.reportSubtitle?.({ FromDate: '2025-01-01', ToDate: '2025-12-31' })
    ).toBe('List of Border Import Licences By HS Code From (01/01/2025) To (31/12/2025)');
  });

  it('summary reports link to Border Import Licence detail like Import Licence references', () => {
    // All four drills open a NEW TAB: every old By-X rdlc wraps the cell in
    // window.open(..., '_blank') -- BorderImportLicence{ByCompany,BySection,ByMethod,
    // BySellerCountry}Report.rdlc on origin/master. Only By Section carried the flag until
    // the 2026-09-17 complaint ("link တွေကို click လိုက်ရင် new tab နဲ့သွားပေးပါရန်").
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
      openInNewTab: true,
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
      openInNewTab: true,
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
      openInNewTab: true,
    });
  });
});

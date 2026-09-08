import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';

describe('Border Export Licence report configs', () => {
  it('By HS Code prints the old rdlc shape, one row per (HS code, currency)', () => {
    // Complaint 2026-09-08: 31/08-01/09/2026 gave 76 rows against the old report's 33 because the
    // backend also grouped on the buyer company -- invisibly, since BorderHSCodeReport.rdlc has no
    // company column (rdlc:177-452) and groups on HSCodeId + Currency only (rdlc:1158-1168).
    const cfg = reportConfigs.BorderExportLicenceByHSCodeReport;

    expect(cfg.columns.map((column) => column.title)).toEqual([
      'HS Code',
      'Description',
      'No of Licences',
      'Total Value',
      'Currency',
    ]);
    // BorderHSCodeReport.rdlc:177 prints "Sr.No.", :713 prints Total Value as FORMAT(...,"N4").
    expect(cfg.rowNumberTitle).toBe('Sr.No.');
    expect(cfg.columns.find((column) => column.key === 'TotalValue')?.numberFormat).toBe(
      '#,##0.0000'
    );
    // The RDLC scrolled every row on one page.
    expect(cfg.defaultPageSize).toBe(1000);
    expect(
      cfg.reportSubtitle?.({ FromDate: '2026-02-01', ToDate: '2026-02-03' })
    ).toBe('List of Border Export Licences By HS Code From (01/02/2026) To (03/02/2026)');
  });

  it('By HS Code keeps the old filter box, Start-first with no All option', () => {
    // Views/Reports/BorderExportLicenceByHSCodeReport.cshtml:57 offers Start / End only. An empty
    // value also put the procedure on its unanchored LIKE '%'+@HSCode arm.
    const filterType = reportConfigs.BorderExportLicenceByHSCodeReport.filters.find(
      (filter) => filter.name === 'FilterType'
    );

    expect(filterType?.defaultValue).toBe('Start');
    expect(filterType?.options?.map((option) => option.value)).toEqual(['Start', 'End']);
  });

  it('By HS Code drills to the HS Code detail report the old screen opened', () => {
    // The old HS Code cell opens Reports/BorderHSCodeDetailReport in a new window
    // (ReportsController.cs:10489, BorderHSCodeReport.rdlc:578-584) -- the (HS code, company)
    // list, not the Border Export Licence Detail report it used to point at.
    expect(
      reportConfigs.BorderExportLicenceByHSCodeReport.columns.find(
        (column) => column.key === 'hsCode'
      )?.drilldown
    ).toEqual({
      targetReportKey: 'BorderExportLicenceHSCodeDetailReport',
      carryFilters: ['FromDate', 'ToDate', 'ExportImportSectionId', 'FilterType', 'SakhanId'],
      rowParams: { hsCode: 'hsCode' },
      openInNewTab: true,
    });
  });

  it('HS Code detail drill pins the company grouping its controller cannot infer', () => {
    const detail = reportConfigs.BorderExportLicenceHSCodeDetailReport;

    expect(detail.controllerName).toBe('BorderExportLicenceByHSCodeReport');
    expect(detail.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'FormType',
      'ExportImportSectionId',
      'FilterType',
      'hsCode',
      'SakhanId',
      'GroupBy',
    ]);
    expect(detail.filters.find((filter) => filter.name === 'GroupBy')?.constantValue).toBe(
      'Company'
    );
    expect(
      reportConfigs.BorderExportLicenceByHSCodeReport.filters.some(
        (filter) => filter.name === 'GroupBy'
      )
    ).toBe(false);
    expect(
      detail.filters.find((filter) => filter.name === 'ExportImportSectionId')?.lookupName
    ).toBe('borderExportLicenceSections');
    expect(detail.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe(
      'sakhans'
    );
    // HSCodeDetailReport.rdlc:445-665 -- Company Name, and no Currency / Total Value.
    expect(detail.columns.map((column) => column.title)).toEqual([
      'HS Code',
      'Description',
      'Company Name',
      'No of Licences',
    ]);
    expect(detail.rowNumberTitle).toBe('Sr.No.');
    expect(detail.defaultPageSize).toBe(1000);
  });
});

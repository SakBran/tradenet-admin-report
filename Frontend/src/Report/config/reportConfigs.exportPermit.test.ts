import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';
import { resolveReportColumns } from '../reportPresentation';

/**
 * Column parity for the non-Border Export Permit reports, pinned against the PRODUCTION
 * Tradenet 2.0 RDLCs (`tradenet-2.0-admin` **origin/master**, 2026-05-04).
 *
 * This suite exists because the old-admin working tree on the development machine sits on
 * branch `OGA_Terminate` (last commit 2022-09-12), and every earlier audit — including
 * `docs/ReportColumnComparison.md` — was written from those stale files. The three shared
 * listing templates all gained columns between 2022 and production:
 *
 *   VoucherReport.rdlc  13 -> 17  (+ Application Date, Commodity Type, Total CIF, Exchange Rate)
 *   CancelReport.rdlc   11 -> 13  (+ HSCode, twice)
 *   AmendReport.rdlc    10 -> 11  (+ HSCode)
 *
 * which is exactly what the 2026-09-05 customer complaints reported as missing. Read the
 * RDLC with `git show origin/master:TradenetAdmin/ReportControl/<name>.rdlc` — never from
 * the working tree — before changing anything here.
 */
describe('Export Permit report configs (production RDLC parity)', () => {
  it('Voucher renders VoucherReport.rdlc\'s full 17-column layout', () => {
    const cfg = reportConfigs.ExportPermitVoucherReport;

    // rdlc:285..1208, in order. "No." is the grid's own row number (showRowNumber).
    expect(cfg.showRowNumber).toBe(true);
    expect(cfg.columns.map((column) => column.key)).toEqual([
      'OriginalLicenceNo', // rdlc:340  Licence No  =IIF(ApplyType="New",LicenceNo,OldLicenceNo)
      'ApplicationDate', // rdlc:395
      'LicenceNo', // rdlc:450  =Parameters!header2.Value
      'ApplicationNo', // rdlc:505
      'LicenceDate', // rdlc:560  =Parameters!header3.Value
      'CompanyRegistrationNo', // rdlc:615
      'CompanyName', // rdlc:684
      'LicValue', // rdlc:739
      'Currency', // rdlc:808
      'VoucherNo', // rdlc:877
      'VoucherDate', // rdlc:932
      'ApprovedUser', // rdlc:987
      'CommodityType', // rdlc:1042
      'TotalCIF', // rdlc:1098
      'ExchangeRate', // rdlc:1153
      'Amount', // rdlc:1208  Total Amount
    ]);

    // Total CIF / Exchange Rate have no counterpart on the ExportPermit table; the old app
    // leaves the two non-nullable decimals unassigned, so the report prints 0. They are
    // present for layout parity only.
    for (const key of ['TotalCIF', 'ExchangeRate'] as const) {
      const column = cfg.columns.find((candidate) => candidate.key === key);
      expect(column?.dataType, key).toBe('number');
    }
  });

  it('Voucher retitles header2/header3 by Apply Type and hides header2 for New', () => {
    const cfg = reportConfigs.ExportPermitVoucherReport;
    expect(cfg.resolveColumns, 'Voucher uses the shared ApplyType header resolver').toBeDefined();

    // rdlc:2510 <Hidden>=IIF(Fields!ApplyType.Value="New",True,False)</Hidden> on header2 only.
    const forNew = resolveReportColumns(cfg, { ApplyType: 'New' });
    expect(forNew.map((column) => column.key)).not.toContain('LicenceNo');

    const forCancel = resolveReportColumns(cfg, { ApplyType: 'Cancel' });
    expect(forCancel.find((column) => column.key === 'LicenceNo')?.title).toBe(
      'Licence Cancel No'
    );
    expect(forCancel.find((column) => column.key === 'LicenceDate')?.title).toBe(
      'Cancellation Date'
    );
  });

  it('Amendment and Actual Amendment carry HS Code between Currency and Total Value', () => {
    // AmendReport.rdlc (shared by both): ... Curency (:667) | HSCode (:723) | Total Value (:778)
    for (const key of [
      'ExportPermitAmendmentReport',
      'ExportPermitActualAmendmentReport',
    ] as const) {
      const keys = reportConfigs[key].columns.map((column) => column.key);
      expect(keys.slice(-3), key).toEqual(['Currency', 'HSCode', 'TotalValue']);

      const hsCode = reportConfigs[key].columns.find((column) => column.key === 'HSCode');
      expect(hsCode?.dataIndex, key).toBe('hsCode');
    }
  });

  it('Cancellation carries HS Code beside Total Value, exactly once', () => {
    // CancelReport.rdlc renders HSCode at column 2 (:391) AND column 12 (:955); the
    // duplicate is an old layout bug, so we render it once.
    const columns = reportConfigs.ExportPermitCancellationReport.columns;
    expect(columns.filter((column) => column.key === 'HSCode')).toHaveLength(1);
    expect(columns.map((column) => column.key).slice(-4)).toEqual([
      'Currency',
      'TotalValue',
      'HSCode',
      'Remark',
    ]);
  });

  it('By Section drill-down reads the section id the aggregate row actually carries', () => {
    // ReportAggregateResult exposes SectionId -> `sectionId`; `exportImportSectionId`
    // matched nothing, so the drill silently carried undefined.
    const section = reportConfigs.ExportPermitBySectionReport.columns.find(
      (column) => column.key === 'Section'
    );

    expect(section?.drilldown?.rowParams).toEqual({ ExportImportSectionId: 'sectionId' });
    expect(section?.drilldown?.targetReportKey).toBe('ExportPermitDetailReport');
  });

  it('the three By-X summary reports keep the old 5-column layout', () => {
    // ExportPermitBy{Section,BuyerCountry,Company}Report.rdlc:241..461 -- Sr.No. is the
    // grid's row number, then the dimension, No of Licences, Total Value, Currency.
    const expected: Record<string, string> = {
      ExportPermitBySectionReport: 'Section',
      ExportPermitBySellerCountryReport: 'Country',
      ExportPermitCompanyListReport: 'CompanyName',
    };

    for (const [key, dimension] of Object.entries(expected)) {
      expect(reportConfigs[key].showRowNumber, key).toBe(true);
      expect(reportConfigs[key].columns.map((column) => column.key), key).toEqual([
        dimension,
        'NoOfLicences',
        'TotalValue',
        'Currency',
      ]);
      // The footer is payload-driven (BasicTable keys ColumnTotals by dataIndex), so the
      // "No of Licences only" parity lives in the controllers; nothing to assert here
      // beyond the column keys the footer has to line up with.
      expect(
        reportConfigs[key].currencyTotalsColumns,
        `${key}: the old rdlc's only aggregate is the grand TOTAL row`
      ).toBeUndefined();
    }
  });
});

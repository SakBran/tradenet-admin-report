import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';

/**
 * Regression guard for the 2026-09 customer complaint on the Border Export Licence Voucher
 * report: searching by an EIR (Company Registration No) returned the right row count but a
 * "Total Amount" of `CNY:4,305,000.0000` where Tradenet 2.0 showed `100,000`.
 *
 * `dbo.sp_VoucherReport[_pagination]` returns two money columns whose names collide:
 *   Amount      = AccountTransaction.TotalAmount   -> the MMK voucher fee
 *   TotalAmount = SUM(<Doc>Item.Amount)            -> the goods value, in the doc's own currency
 *
 * The old RDLCs bind and total the FIRST one and never render the second:
 *   BorderVoucherReport.rdlc:1399 =FORMAT(Fields!Amount.Value,"N0")
 *   BorderVoucherReport.rdlc:1457 TOTAL (ColSpan 10) + :1521 =FORMAT(SUM(Fields!Amount.Value),"N0")
 *   VoucherReport.rdlc:1709 / :1828 -- same shape
 * Neither RDLC has a per-currency footer, and neither totals Lic Value.
 */
describe('voucher report money column + footer parity (BorderVoucherReport.rdlc)', () => {
  const VOUCHER_REPORTS = [
    'BorderExportLicenceVoucherReport',
    'BorderImportPermitVoucherReport',
    'BorderExportPermitVoucherReport',
    'ExportPermitVoucherReport',
  ] as const;

  it('bind "Total Amount" to the voucher fee (amount), not the item/goods value', () => {
    for (const key of VOUCHER_REPORTS) {
      const cfg = reportConfigs[key];
      const totalAmount = cfg.columns.find((column) => column.title === 'Total Amount');

      expect(totalAmount, `${key} has a Total Amount column`).toBeDefined();
      expect(totalAmount, `${key} Total Amount binds the voucher fee`).toEqual({
        key: 'Amount',
        dataIndex: 'amount',
        title: 'Total Amount',
        dataType: 'number',
      });
      // The item/goods value may still appear, but only under its own heading: the
      // non-border VoucherReport.rdlc:652 shows it as "Lic Value" next to a Currency column.
      // It must never be the column labelled "Total Amount".
      for (const column of cfg.columns.filter(
        (candidate) => candidate.dataIndex === 'totalAmount'
      )) {
        expect(column.title, `${key} goods-value column heading`).toBe('Lic Value');
      }
    }
  });

  it('carry no per-currency footer -- the legacy footer is a single ColumnTotals row', () => {
    for (const key of VOUCHER_REPORTS) {
      expect(
        reportConfigs[key].currencyTotalsColumns,
        `${key}: the old rdlc's only aggregate is the single TOTAL row (rdlc:1457/1521)`
      ).toBeUndefined();
    }
  });

  it('Border Export Licence Voucher keeps the old-admin filter box', () => {
    const cfg = reportConfigs.BorderExportLicenceVoucherReport;

    // Views/Reports/BorderExportLicenceVoucherReport.cshtml:25-81 -- FromDate, ToDate,
    // Export Section, Payment Type, Apply Type, Company Registration No, readonly Company
    // Name, Sakhan (plus the hidden FormType the controller hard-codes).
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
    ).toBe('borderExportLicenceSections');
    expect(cfg.filters.find((filter) => filter.name === 'CompanyName')?.type).toBe(
      'readonlyText'
    );
    expect(cfg.filters.find((filter) => filter.name === 'SakhanId')?.lookupName).toBe(
      'sakhans'
    );
  });

  it('hide the duplicate header2 Licence No column for ApplyType=New only', () => {
    const cfg = reportConfigs.BorderExportLicenceVoucherReport;
    const resolve = (applyType: string) =>
      cfg.resolveColumns?.({ ApplyType: applyType }, cfg.columns) ?? cfg.columns;

    // BorderVoucherReport.rdlc:1578 -- <Hidden>=IIF(Fields!ApplyType.Value="New",True,False)
    // on the header2 column only. Without this, ApplyType='New' renders two adjacent
    // "Licence No" columns with identical values (OriginalLicenceNo falls back to licenceNo).
    const forNew = resolve('New');
    expect(forNew.find((column) => column.key === 'LicenceNo')?.hidden).toBe(true);
    expect(forNew.find((column) => column.key === 'LicenceDate')?.hidden).toBeFalsy();

    for (const applyType of ['Amend', 'Extension', 'Cancel', 'Actual Amend']) {
      const resolved = resolve(applyType);
      expect(
        resolved.find((column) => column.key === 'LicenceNo')?.hidden,
        `${applyType} keeps the header2 column`
      ).toBe(false);
    }

    // header2 / header3 titles, ReportsController.cs:9957-9981.
    const forAmend = resolve('Amend');
    expect(forAmend.find((column) => column.key === 'LicenceNo')?.title).toBe(
      'Licence Amendment No'
    );
    expect(forAmend.find((column) => column.key === 'LicenceDate')?.title).toBe(
      'Amendment Date'
    );
  });
});

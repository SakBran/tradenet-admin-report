import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';
import { buildReportHeaderLines } from '../reportPresentation';
import { formatNumberWithFormat } from '../numberFormat';

/**
 * 2026-09-25, BSA Registration By Voucher: "excel ကော UI မှာကော ခေါင်းစဉ်(New/Amend)
 * ထုတ်ရင် မပါလာလို့ပါ ... ဒသမနောက်က သုညလေးလုံး ဖြုတ်ပေးပါရန်".
 *
 * The old report's title line is header1 =
 * "Business Service Agency " + ApplyType + " List (From) To (To)"
 * (ReportsController.cs:2956 on origin/master), and its RDLC prints TotalAmount with no
 * Format. The same header lines feed the grid and the Excel spec, so pinning them here
 * covers both.
 */
const cfg = reportConfigs.BusinessServiceAgencyRegistrationByVoucher;

const applied = (ApplyType: string) => ({
  FromDate: '2026-01-01T00:00:00',
  ToDate: '2026-01-31T23:59:59',
  ApplyType,
  PaymentType: '',
});

describe('BusinessServiceAgencyRegistrationByVoucher — legacy parity', () => {
  it.each(['New', 'Amend', 'Actual Amend'])(
    'the title line names the Apply Type (%s)',
    (applyType) => {
      expect(buildReportHeaderLines(cfg, applied(applyType))).toEqual([
        'Ministry of Commerce',
        'Directorate of Trade',
        `Business Service Agency ${applyType} List (01/01/2026) To (31/01/2026)`,
      ]);
    }
  );

  it('Total Amount prints as stored, with no padded decimals', () => {
    const totalAmount = cfg.columns.find((column) => column.key === 'TotalAmount');

    expect(totalAmount?.numberFormat).toBe('0.####');
    expect(formatNumberWithFormat(260000, totalAmount!.numberFormat!)).toBe('260000');
    expect(formatNumberWithFormat(2500.5, totalAmount!.numberFormat!)).toBe('2500.5');
  });

  it('keeps the old filter order and row-number header', () => {
    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'ApplyType',
      'PaymentType',
    ]);
    expect(cfg.rowNumberTitle).toBe('No.');
  });

  it('keeps the old column set', () => {
    expect(cfg.columns.map((column) => column.title)).toEqual([
      'Date',
      'Company Registration No',
      'Company Name',
      'Company Address',
      'BSA No',
      'Agent of Authorize Company',
      'Total Amount',
      'Payment Type',
      'Voucher No',
      'Voucher Date',
    ]);
  });

  // The fix is BSA-only by the owner's decision; the siblings share paymentColumns
  // and voucherFilters, so a change to those shared pieces would show up here.
  it('leaves the sibling voucher reports untouched', () => {
    const sibling = reportConfigs.DutyFreeShopRegistrationByVoucher;

    expect(
      sibling.columns.find((column) => column.key === 'TotalAmount')?.numberFormat
    ).toBe('#,##0.0000');
    expect(sibling.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'PaymentType',
      'ApplyType',
    ]);
    expect(buildReportHeaderLines(sibling, applied('New')).at(-1)).toBe(
      '(01/01/2026) To (31/01/2026)'
    );
  });
});

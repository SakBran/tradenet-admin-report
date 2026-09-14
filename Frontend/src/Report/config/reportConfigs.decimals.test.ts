import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';

/**
 * 2026-09-14, customer: every column that carries a decimal point shows FOUR places,
 * in every report, whether or not the value has anything after the point — 2000 reads
 * "2,000.0000" and 2000.1 reads "2,000.1000".
 *
 * The Payment reports print theirs without thousands separators (see
 * reportConfigs.payment.test.ts), so they use '0.0000' and everything else
 * '#,##0.0000'. Both are 4 decimals; only the grouping differs.
 */
const PAYMENT = new Set([
  'AccountSummaryReport',
  'ChequeNoDetailReport',
  'ChequeNoReport',
  'MPUReport',
  'MPUReportV3',
  'OnlineFeesReport',
]);

/** Amount-bearing columns. */
const MONEY_DATA_INDEXES = new Set([
  'amount', 'amountDiff', 'capital', 'exchangeRate', 'imAmount', 'mocAmount',
  'mpuAmount', 'price', 'quantity', 'totalAmount', 'totalCIF', 'totalUSDValue',
  'totalValue', 'transactionAmount',
]);

/**
 * Counts and identifiers. These carry no decimal point in the old reports either,
 * so "5 licences" must never become "5.0000".
 */
const COUNTS = new Set([
  'chequeId', 'companyCount', 'extensionCount', 'noOfLicences', 'terms',
]);

/**
 * The MMK voucher fee. `amount` on a Voucher report is the fee the office collected
 * — whole kyat, pinned to the old RDLC's FORMAT(...,"N0") by earlier complaint fixes
 * — NOT the goods value, which is the sibling `LicValue` (totalAmount) and does get
 * four places. It has no decimal point to widen.
 */
const isVoucherFee = (reportKey: string, dataIndex?: string) =>
  reportKey.includes('Voucher') && dataIndex === 'amount';

describe('decimal columns show four places', () => {
  it('every amount column asks for 4 decimals', () => {
    const wrong: string[] = [];

    for (const [key, config] of Object.entries(reportConfigs)) {
      for (const column of config.columns) {
        if (
          !MONEY_DATA_INDEXES.has(column.dataIndex ?? '') ||
          isVoucherFee(key, column.dataIndex)
        ) {
          continue;
        }

        const expected = PAYMENT.has(key) ? '0.0000' : '#,##0.0000';
        if (column.numberFormat !== expected) {
          wrong.push(
            `${key}.${column.key}: ${String(column.numberFormat)} (want ${expected})`
          );
        }
      }
    }

    expect(wrong).toEqual([]);
  });

  it('counts and identifiers keep no decimals at all', () => {
    const wrong: string[] = [];

    for (const [key, config] of Object.entries(reportConfigs)) {
      for (const column of config.columns) {
        if (COUNTS.has(column.dataIndex ?? '') && column.numberFormat) {
          wrong.push(`${key}.${column.key}: ${column.numberFormat}`);
        }
      }
    }

    expect(wrong).toEqual([]);
  });

  it('a 4-decimal format is never asked for with fewer places', () => {
    const formats = new Set<string>();

    for (const config of Object.values(reportConfigs)) {
      for (const column of config.columns) {
        if (column.numberFormat) {
          formats.add(column.numberFormat);
        }
      }
    }

    // '#,##0' is the voucher fee's N0; everything else prints four places.
    expect([...formats].sort()).toEqual(['#,##0', '#,##0.0000', '0.0000']);
  });
});

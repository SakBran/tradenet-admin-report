import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';

/**
 * The Payment group (ငွေစာရင်းဌာန). Two behaviours in GenericReportPage key off
 * `showTime`, and both are meant for these reports ONLY:
 *
 *  - the date filter renders as two separate From Date / To Date boxes, the shape
 *    the old Tradenet 2.0 screens had (the staff rejected one combined range box);
 *  - the To edge is widened to :59, because the picker offers no seconds.
 *
 * So `showTime` is not a cosmetic flag — it is the switch that says "this is a
 * Payment report". Adding it to any other config silently changes that report's
 * filter box, which is why the set is pinned here.
 */
const PAYMENT_REPORT_KEYS = [
  'AccountSummaryReport',
  'ChequeNoDetailReport',
  'ChequeNoReport',
  'MPUReport',
  'MPUReportV3',
  'OnlineFeesReport',
];

describe('Payment report configs', () => {
  it('showTime marks exactly the Payment reports, and nothing else', () => {
    const withShowTime = Object.entries(reportConfigs)
      .filter(([, config]) =>
        config.filters.some(
          (filter) => filter.type === 'dateRange' && filter.showTime
        )
      )
      .map(([key]) => key)
      .sort();

    expect(withShowTime).toEqual(PAYMENT_REPORT_KEYS);
  });

  it('every Payment date column prints mm/dd/yyyy and every amount is 4 decimals, comma-less', () => {
    for (const key of PAYMENT_REPORT_KEYS) {
      const config = reportConfigs[key];

      for (const column of config.columns) {
        if (column.dataType === 'date') {
          expect(column.dateFormat, `${key}.${column.key}`).toBe('MM/DD/YYYY');
        }

        if (column.dataType === 'dateTime') {
          expect(column.dateFormat, `${key}.${column.key}`).toBe(
            'MM/DD/YYYY HH:mm:ss'
          );
        }

        // The money columns, plus the 'number' columns that hold an amount.
        // A plain identifier (ChequeNoReport's Cheque Id) keeps no format.
        // '0.####' prints the value as stored — the department asked for no
        // padding at all on 2026-09-17 — and carries no comma, as ever.
        if (column.dataType === 'money' || column.dataIndex === 'amount') {
          expect(column.numberFormat, `${key}.${column.key}`).toBe('0.####');
          expect(column.numberFormat).not.toContain(',');
        }
      }
    }
  });
});

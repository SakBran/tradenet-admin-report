import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';
import { reportNavItems } from '../reportNavItems';

const collectMenuKeys = (items: NonNullable<typeof reportNavItems>): string[] =>
  items.flatMap((item) => {
    if (!item || typeof item !== 'object' || !('key' in item)) {
      return [];
    }

    const key = String(item.key);
    const children = 'children' in item && Array.isArray(item.children)
      ? collectMenuKeys(item.children as NonNullable<typeof reportNavItems>)
      : [];

    return [key, ...children];
  });

describe('Cheque No report configs', () => {
  it('summary Cheque No column drills into the detail report with the clicked cheque id', () => {
    const column = reportConfigs.ChequeNoReport.columns.find(
      (item) => item.key === 'ChequeNo'
    );

    expect(column?.drilldown).toEqual({
      targetReportKey: 'ChequeNoDetailReport',
      carryFilters: ['FromDate', 'ToDate'],
      rowParams: { ChequeNoId: 'chequeId' },
      openInNewTab: true,
    });
  });

  it('detail report columns match the old RDLC visible headers', () => {
    const cfg = reportConfigs.ChequeNoDetailReport;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'dateRange',
      'ChequeNoId',
    ]);
    expect(cfg.columns.map((column) => column.title)).toEqual([
      'Cheque No',
      'Trxn Ref No.',
      'Trxn Date',
      'Form Type',
      'Licence/Permit/Card No',
      'Amount',
      'Company Registration No',
      'Company Name',
      'Company Address',
    ]);
    expect(cfg.showRowNumber).toBe(true);
    expect(cfg.disableLazyTotalCount).toBe(true);
  });

  it('detail report is route-only and does not appear in the sitemap menu', () => {
    expect(collectMenuKeys(reportNavItems)).not.toContain('ChequeNoDetailReport');
  });
});

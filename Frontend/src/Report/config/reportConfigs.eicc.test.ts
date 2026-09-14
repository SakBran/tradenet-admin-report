import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';
import { reportNavItems } from '../reportNavItems';
import { reportRoutes } from '../reportRoutes';

const EICC_KEYS = [
  'EICCCertificateReport',
  'EICCLicencePermitReport',
  'EICCBorderLicencePermitReport',
] as const;

// EICCReport.rdlc:205-756 on origin/master, in tablix column order. The RDLC wraps four
// headers over two cells; each pair is one phrase.
const RDLC_COLUMN_TITLES = [
  'EICC No',
  'Date',
  'Status',
  'Form Type',
  'Application Type',
  'Company Registration No',
  'Company Name',
  'Company Address',
  'Remark',
];

const collectMenuKeys = (
  items: NonNullable<typeof reportNavItems>
): string[] =>
  items.flatMap((item) => {
    if (!item || typeof item !== 'object' || !('key' in item)) {
      return [];
    }

    const key = String(item.key);
    const children =
      'children' in item && Array.isArray(item.children)
        ? collectMenuKeys(item.children as NonNullable<typeof reportNavItems>)
        : [];

    return [key, ...children];
  });

describe('EICC report configs', () => {
  it.each(EICC_KEYS)('%s prints the ten EICCReport.rdlc columns', (key) => {
    const cfg = reportConfigs[key];

    expect(cfg.columns.map((column) => column.title)).toEqual(
      RDLC_COLUMN_TITLES
    );
    // The tenth column is the RDLC's row number, printed as "No".
    expect(cfg.showRowNumber).toBe(true);
    expect(cfg.rowNumberTitle).toBe('No');
  });

  it.each(EICC_KEYS)('%s has no footer totals, as the RDLC had none', (key) => {
    const cfg = reportConfigs[key];

    expect(cfg.currencyTotalsColumns).toBeUndefined();
    expect(cfg.columns.some((column) => column.drilldown)).toBe(false);
  });

  it.each(EICC_KEYS)('%s keeps the EICC date and status boxes', (key) => {
    const cfg = reportConfigs[key];
    const date = cfg.filters.find((filter) => filter.name === 'Date');
    const status = cfg.filters.find((filter) => filter.name === 'EICCStatus');

    // EICCReport.cshtml:26 — required, and posted as `Date` so the Excel header block
    // finds it and prints "Date: dd/MM/yyyy".
    expect(date?.type).toBe('date');
    expect(date?.label).toBe('EICC Date');
    expect(date?.required).toBe(true);

    // EICCReport.cshtml:32-49 — Pending/Approved only, defaulting to Pending. There is
    // no "all" option: the procedure compares Status with `=`.
    expect(status?.defaultValue).toBe('Pending');
    expect(status?.options?.map((option) => option.value)).toEqual([
      'Pending',
      'Approved',
    ]);
  });

  it('the Certificate screen reads its card types from the lookup and has no product boxes', () => {
    const cfg = reportConfigs.EICCCertificateReport;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'Date',
      'EICCStatus',
      'FormType',
    ]);
    expect(
      cfg.filters.find((filter) => filter.name === 'FormType')?.lookupName
    ).toBe('eiccCardTypes');
    expect(cfg.reportSubtitle?.({ Date: '2026-02-15T00:00:00' })).toBe(
      'EICC Certificate Report (15/02/2026)'
    );
  });

  it('the Licence/Permit screen offers the four oversea card types and cascades the product boxes', () => {
    const cfg = reportConfigs.EICCLicencePermitReport;

    expect(cfg.filters.map((filter) => filter.name)).toEqual([
      'Date',
      'EICCStatus',
      'FormType',
      'ProductGroupId',
      'ProductItemId',
    ]);
    // Hardcoded in the old controller (EICCController.cs:400 on origin/master).
    expect(
      cfg.filters.find((filter) => filter.name === 'FormType')?.options
    ).toEqual([
      { label: '--- All ---', value: '' },
      { label: 'Export Licence', value: 'Export Licence' },
      { label: 'Import Licence', value: 'Import Licence' },
      { label: 'Export Permit', value: 'Export Permit' },
      { label: 'Import Permit', value: 'Import Permit' },
    ]);
    expect(
      cfg.filters.find((filter) => filter.name === 'ProductGroupId')?.dependsOn
    ).toBe('FormType');
    expect(
      cfg.filters.find((filter) => filter.name === 'ProductItemId')?.dependsOn
    ).toBe('ProductGroupId');
    // The legacy header1 keeps the raw AppConfig type word, unspaced.
    expect(cfg.reportSubtitle?.({ Date: '2026-02-15T00:00:00' })).toBe(
      'EICC LicencePermit Report (15/02/2026)'
    );
  });

  it('the Border screen offers the four border card types', () => {
    const cfg = reportConfigs.EICCBorderLicencePermitReport;

    // Hardcoded in the old controller (EICCController.cs:455 on origin/master).
    expect(
      cfg.filters
        .find((filter) => filter.name === 'FormType')
        ?.options?.map((option) => option.value)
    ).toEqual([
      '',
      'Border Export Licence',
      'Border Import Licence',
      'Border Export Permit',
      'Border Import Permit',
    ]);
    expect(cfg.reportSubtitle?.({ Date: '2026-02-15T00:00:00' })).toBe(
      'EICC BorderLicencePermit Report (15/02/2026)'
    );
  });

  it('all three screens are routed and exported, but kept out of the sidebar', () => {
    const menuKeys = collectMenuKeys(reportNavItems);
    const routePaths = reportRoutes.map((route) => route.path);

    for (const key of EICC_KEYS) {
      // Owner's call 2026-09-14: hidden from the menu, still reachable at /Report/<key>.
      expect(reportConfigs[key].hideInMenu).toBe(true);
      expect(menuKeys).not.toContain(key);

      expect(routePaths).toContain(key);
      expect(reportConfigs[key].apiRoute).toBe(key);
      expect(reportConfigs[key].excelRoute).toBe(`${key}/Excel`);
    }

    // The empty EICC group must not render as a dead, childless sidebar row.
    expect(menuKeys).not.toContain('report-eicc');
  });
});

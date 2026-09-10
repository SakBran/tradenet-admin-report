import { describe, expect, it } from 'vitest';
import { reportConfigs } from './config/reportConfigs';
import { reportNavItems } from './reportNavItems';

type NavItems = NonNullable<typeof reportNavItems>;

/** Every key in the menu tree, groups included. */
const collectMenuKeys = (items: NavItems): string[] =>
  items.flatMap((item) => {
    if (!item || typeof item !== 'object' || !('key' in item)) {
      return [];
    }

    const key = String(item.key);
    const children =
      'children' in item && Array.isArray(item.children)
        ? collectMenuKeys(item.children as NavItems)
        : [];

    return [key, ...children];
  });

/** Only the leaves — the rows that navigate to a report. */
const collectLeafKeys = (items: NavItems): string[] =>
  items.flatMap((item) => {
    if (!item || typeof item !== 'object' || !('key' in item)) {
      return [];
    }

    if ('children' in item && Array.isArray(item.children)) {
      return collectLeafKeys(item.children as NavItems);
    }

    return [String(item.key)];
  });

describe('report sidebar menu', () => {
  // createReportItem keys the menu row off controllerName, and SideNav marks every row
  // whose key matches the current route as selected. Two leaves sharing a key therefore
  // render two identical, both-highlighted rows pointing at the same page — which is what
  // the "HS Code Detail Report menu အခွဲတစ်ခု ထပ်ပါနေတယ်" complaint was.
  it('has no two leaves sharing a key', () => {
    const leaves = collectLeafKeys(reportNavItems);
    const duplicates = leaves.filter(
      (key, index) => leaves.indexOf(key) !== index
    );

    expect(duplicates).toEqual([]);
  });

  // Drill-only targets: reachable by route and from their summary's drilldown, never
  // listed in the sidebar.
  it.each([
    'BorderExportLicenceHSCodeDetailReport',
    'BorderExportPermitHSCodeDetailReport',
    'BorderImportLicenceHSCodeDetailReport',
    'BorderImportPermitHSCodeDetailReport',
    'ExportLicenceHSCodeDetailReport',
    'ImportLicenceDetailByLicenceReport',
  ])('%s is hidden from the menu', (configKey) => {
    expect(reportConfigs[configKey].hideInMenu).toBe(true);
  });

  it('lists no "HS Code Detail Report" row', () => {
    const hidden = Object.values(reportConfigs).filter(
      (config) => config.title === 'HS Code Detail Report'
    );

    // All five families have one, and every one of them must be hidden.
    expect(hidden).toHaveLength(5);
    expect(hidden.every((config) => config.hideInMenu)).toBe(true);
  });

  // The sitemap has exactly one "Import Licence Detail Report" — the per-item
  // ImportLicenceDetailReport. ImportLicenceDetailByLicenceReport carries the same title
  // and used to render a second, identically labelled row next to it.
  it('lists "Import Licence Detail Report" exactly once', () => {
    const visible = Object.values(reportConfigs).filter(
      (config) =>
        config.title === 'Import Licence Detail Report' && !config.hideInMenu
    );

    expect(visible.map((config) => config.controllerName)).toEqual([
      'ImportLicenceDetailReport',
    ]);
  });

  it('keeps the hidden drill targets out of the rendered menu but leaves their summaries in', () => {
    const keys = collectMenuKeys(reportNavItems);

    expect(keys).not.toContain('ImportLicenceDetailByLicenceReport');
    expect(keys).toContain('ImportLicenceDetailReport');

    // The five HS Code detail configs alias their summary's controllerName, so the
    // summary key must still be present — exactly once (asserted above).
    expect(keys).toContain('BorderImportLicenceByHSCodeReport');
  });
});

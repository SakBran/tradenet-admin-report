import { describe, expect, it } from 'vitest';
import { reportCategoryKeys } from './reportNavItems';

describe('report navigation categories', () => {
  it('keeps licence and permit groups in the requested sitemap order', () => {
    expect(reportCategoryKeys).toEqual(
      expect.arrayContaining([
        'report-import-licence',
        'report-import-permit',
        'report-export-licence',
        'report-export-permit',
        'report-border-import-licence',
        'report-border-import-permit',
        'report-border-export-licence',
        'report-border-export-permit',
      ])
    );

    const orderedKeys = reportCategoryKeys.filter((key) =>
      [
        'report-import-licence',
        'report-import-permit',
        'report-export-licence',
        'report-export-permit',
        'report-border-import-licence',
        'report-border-import-permit',
        'report-border-export-licence',
        'report-border-export-permit',
      ].includes(key)
    );

    expect(orderedKeys).toEqual([
      'report-import-licence',
      'report-import-permit',
      'report-export-licence',
      'report-export-permit',
      'report-border-import-licence',
      'report-border-import-permit',
      'report-border-export-licence',
      'report-border-export-permit',
    ]);
  });
});

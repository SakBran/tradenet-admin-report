import { describe, expect, it } from 'vitest';

import { reportConfigs } from './reportConfigs';

describe('Import Licence report configs', () => {
  it('Daily report uses global pagination and the standard row-number column', () => {
    const config = reportConfigs.ImportLicenceDailyReportNewLicenceReport;

    expect(config.defaultPageSize).toBeUndefined();
    expect(config.showRowNumber).toBe(true);
    expect(config.columns.map((column) => column.title)).toEqual([
      'Date',
      'No of Licences',
      'Total Value',
      'Currency',
      'Total USD Value',
    ]);
  });
});

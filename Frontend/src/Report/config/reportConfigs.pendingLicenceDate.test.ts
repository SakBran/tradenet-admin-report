import { describe, expect, it } from 'vitest';

import { reportConfigs } from './reportConfigs';

describe('Pending report Licence Date columns', () => {
  const reportKeys = [
    'ImportLicencePendingReport',
    'BorderImportLicencePendingReport',
    'ImportLicenceDetailReportPending',
    'BorderImportLicenceDetailReportPending',
  ] as const;

  it.each(reportKeys)('%s exposes one Licence Date column', (reportKey) => {
    const licenceDateColumns = reportConfigs[reportKey].columns.filter(
      (column) => column.key === 'LicenceDate'
    );

    expect(licenceDateColumns).toEqual([
      expect.objectContaining({
        dataIndex: 'licenceDate',
        title: 'Licence Date',
        dataType: 'date',
      }),
    ]);
  });

  it.each([
    'ImportLicencePendingReport',
    'BorderImportLicencePendingReport',
  ] as const)('%s places Licence Date after Application Date', (reportKey) => {
    const columnKeys = reportConfigs[reportKey].columns.map((column) => column.key);

    expect(columnKeys.indexOf('LicenceDate')).toBe(
      columnKeys.indexOf('ApplicationDate') + 1
    );
  });
});

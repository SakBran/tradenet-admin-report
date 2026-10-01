import { describe, expect, it } from 'vitest';

import { reportConfigs } from './reportConfigs';

const ALL_APPLICATION_NEW_REPORTS = [
  'ImportLicenceNewReportNewReport',
  'ImportPermitNewReportNewReport',
  'ExportLicenceNewReportNewReport',
  'ExportPermitNewReportNewReport',
  'BorderImportLicenceNewReportNewReport',
  'BorderImportPermitNewReportNewReport',
  'BorderExportLicenceNewReportNewReport',
  'BorderExportPermitNewReportNewReport',
] as const;

const REQUESTED_DATE_FIELD_TITLES = [
  'Online No',
  'Online Date',
  'Licence Date',
  'Remark',
];

describe('All application New Report licence date', () => {
  it.each(ALL_APPLICATION_NEW_REPORTS)(
    '%s exposes Licence Date immediately after Licence No',
    (configKey) => {
      const columns = reportConfigs[configKey].columns;
      const licenceNoIndex = columns.findIndex((column) => column.title === 'Licence No');

      expect(licenceNoIndex).toBeGreaterThanOrEqual(0);
      expect(columns[licenceNoIndex + 1]).toMatchObject({
        key: 'LicenceDate',
        dataIndex: 'licenceDate',
        title: 'Licence Date',
        dataType: 'date',
        dateFormat: 'DD/MM/YYYY',
      });
    }
  );

  it.each(ALL_APPLICATION_NEW_REPORTS)(
    '%s keeps only Licence Date from the requested date-field set',
    (configKey) => {
      const requestedColumns = reportConfigs[configKey].columns
        .map((column) => column.title)
        .filter((title) => REQUESTED_DATE_FIELD_TITLES.includes(String(title)));

      expect(requestedColumns).toEqual(['Licence Date']);
    }
  );
});

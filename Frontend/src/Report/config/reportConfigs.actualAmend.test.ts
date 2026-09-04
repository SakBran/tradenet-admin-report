import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';

// The eight Actual Amendment listing reports render the old AmendReport.rdlc /
// BorderAmendReport.rdlc layout: a per-currency footer, "Licence No" bound to the ORIGINAL
// licence number (Fields!OldLicenceNo) and "Licence Amendment No" to the amendment number
// (Fields!LicenceNo). Neither old RDLC has an HSCode column.
const ACTUAL_AMEND_REPORTS = [
  'ExportLicenceActualAmendmentReport',
  'ImportLicenceActualAmendmentReport',
  'ExportPermitActualAmendmentReport',
  'ImportPermitActualAmendmentReport',
  'BorderExportLicenceActualAmendmentReport',
  'BorderImportLicenceActualAmendmentReport',
  'BorderExportPermitActualAmendmentReport',
  'BorderImportPermitActualAmendmentReport',
];

describe('Actual Amendment report configs', () => {
  it('every Actual Amendment report declares the per-currency footer', () => {
    for (const key of ACTUAL_AMEND_REPORTS) {
      expect(reportConfigs[key].currencyTotalsColumns, key).toEqual({
        labelColumnKey: 'LicenceNo',
        valueColumnKey: 'TotalValue',
      });
    }
  });

  it('Licence No shows the original number and Licence Amendment No the amendment number', () => {
    for (const key of ACTUAL_AMEND_REPORTS) {
      const columns = reportConfigs[key].columns;
      const licenceNo = columns.find((column) => column.key === 'LicenceNo');
      const amendmentNo = columns.find((column) => column.key === 'LicenceAmendmentNo');

      expect(licenceNo?.dataIndex, `${key} Licence No`).toBe('oldLicenceNo');
      expect(amendmentNo?.dataIndex, `${key} Licence Amendment No`).toBe('licenceNo');
      // The two columns must not render the same value (the old RDLC prints both).
      expect(licenceNo?.dataIndex, key).not.toBe(amendmentNo?.dataIndex);
    }
  });

  it('Border Import Licence Actual Amendment drops the stray hsCode column', () => {
    expect(
      reportConfigs.BorderImportLicenceActualAmendmentReport.columns.some(
        (column) => column.dataIndex === 'hsCode'
      )
    ).toBe(false);
  });
});

import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';

// Legacy ground truth, shared by every Extension report:
//   ExtensionReport.rdlc:303 "Licence No"   -> :868 =Fields!OldLicenceNo.Value  (the parent)
//   ExtensionReport.rdlc:358 "Extension No" -> :921 =Fields!LicenceNo.Value     (the E-number)
//   BorderExtensionReport.rdlc:374/993 and :429/1046 say the same for the Sakhan variants.
// An extension is the same row (ApplyType='Extension') carrying both numbers, so every branch
// of sp_ExtensionReport_pagination projects OldLicenceNo alongside LicenceNo.
describe('Extension report Licence No / Extension No bindings', () => {
  const reports = [
    'BorderExportLicenceExtensionReport',
    'BorderExportPermitExtensionReport',
    'BorderImportLicenceExtensionReport',
    'BorderImportPermitExtensionReport',
    'ExportLicenceExtensionReport',
    'ExportPermitExtensionReport',
    'ImportLicenceExtensionReport',
  ];

  it.each(reports)('%s shows the parent licence, not the extension number, under "Licence No"', (key) => {
    const { columns } = reportConfigs[key];
    const licenceNo = columns.find((column) => column.key === 'LicenceNo');
    const extensionNo = columns.find((column) => column.key === 'ExtensionNo');

    expect(licenceNo?.title, key).toBe('Licence No');
    expect(licenceNo?.dataIndex, `${key}: "Licence No" must be the parent licence`).toBe(
      'oldLicenceNo'
    );

    expect(extensionNo?.title, key).toBe('Extension No');
    expect(extensionNo?.dataIndex, `${key}: "Extension No" must be the extension`).toBe(
      'licenceNo'
    );

    // The duplicate binding this guards against: both columns pointing at the same field made
    // the grid print the extension number twice (customer complaint, 2026-09-05).
    expect(licenceNo?.dataIndex, key).not.toBe(extensionNo?.dataIndex);
  });

  // ImportPermitExtensionReport is deliberately excluded: it is bound the other way round and
  // reportConfigs.importPermit.test.ts:90-100 asserts that inversion. It contradicts the RDLC
  // and the seven reports above, but it may encode a customer decision -- confirm before
  // flipping it, then fold it into `reports` here and delete this block.
  it('ImportPermitExtensionReport is the known, still-unresolved exception', () => {
    const { columns } = reportConfigs.ImportPermitExtensionReport;

    expect(columns.find((column) => column.key === 'LicenceNo')?.dataIndex).toBe('licenceNo');
    expect(columns.find((column) => column.key === 'ExtensionNo')?.dataIndex).toBe(
      'oldLicenceNo'
    );
  });
});

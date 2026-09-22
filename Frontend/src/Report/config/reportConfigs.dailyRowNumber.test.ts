import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';
import { resolveRowNumberTitle } from '../reportPresentation';

/**
 * Customer complaint 2026-09-22 against Border Import Licence Daily Report (New Licence
 * Report): "ထိပ်ဆုံးမှာ Sr No column မပါလာလို့ပါတဲ့" -- the leading serial column was gone.
 *
 * Every legacy *ByDailyReport.rdlc prints one, as a per-group counter variable
 * (=Variables!GroupCountValue.Value, backed by Code.getGroupCounter), but its header
 * textbox is EMPTY. The 2026-05-29 bulk column-compare pass (tools/apply-old-report-ui.mjs,
 * commit 39a3a0fd) read the blank header as "no column" and set showRowNumber: false on
 * seven of the eight Daily configs -- docs/ReportColumnComparison.md:372-378 still records
 * the same false negative. These assertions exist so the next bulk pass cannot repeat it.
 */
const DAILY_REPORTS_WITH_A_LEGACY_SERIAL_COLUMN: Record<
  string,
  { rdlc: string; serialCell: number; blankHeader: number }
> = {
  BorderExportLicenceDailyReportNewLicenceReport: {
    rdlc: 'BorderExportLicenceByDailyReport.rdlc',
    serialCell: 675,
    blankHeader: 285,
  },
  BorderExportPermitDailyReportNewPermitReport: {
    rdlc: 'BorderExportPermitByDailyReport.rdlc',
    serialCell: 616,
    blankHeader: 281,
  },
  BorderImportLicenceDailyReportNewLicenceReport: {
    rdlc: 'BorderImportLicenceByDailyReport.rdlc',
    serialCell: 608,
    blankHeader: 273,
  },
  BorderImportPermitDailyReportNewPermitReport: {
    rdlc: 'BorderImportPermitByDailyReport.rdlc',
    serialCell: 608,
    blankHeader: 273,
  },
  ExportLicenceDailyReportNewLicenceReport: {
    rdlc: 'ExportLicenceByDailyReport.rdlc',
    serialCell: 616,
    blankHeader: 281,
  },
  ExportPermitDailyReportNewPermitReport: {
    rdlc: 'ExportPermitByDailyReport.rdlc',
    serialCell: 616,
    blankHeader: 281,
  },
  ImportLicenceDailyReportNewLicenceReport: {
    rdlc: 'ImportLicenceByDailyReport.rdlc',
    serialCell: 608,
    blankHeader: 273,
  },
  ImportPermitDailyReportNewPermitReport: {
    rdlc: 'ImportPermitByDailyReport.rdlc',
    serialCell: 608,
    blankHeader: 273,
  },
};

/**
 * The seven fixed on 2026-09-22. The old RDLCs leave the header blank, so there is no
 * legacy string to copy; `Sr.No.` is the family-wide spelling every other Border/Import
 * Licence RDLC uses for that slot (BorderImportLicenceBySectionReport.rdlc:261) and the
 * label the customer asked for.
 */
const LABELLED_SR_NO = Object.keys(DAILY_REPORTS_WITH_A_LEGACY_SERIAL_COLUMN).filter(
  (key) => key !== 'ImportLicenceDailyReportNewLicenceReport'
);

describe('Daily report row-number column', () => {
  it('every Daily report shows the leading serial column its RDLC prints', () => {
    for (const [key, { rdlc, serialCell, blankHeader }] of Object.entries(
      DAILY_REPORTS_WITH_A_LEGACY_SERIAL_COLUMN
    )) {
      expect(
        reportConfigs[key].showRowNumber,
        `${key} must show the row number: ${rdlc}:${serialCell} prints it (header cell is blank at ${rdlc}:${blankHeader})`
      ).toBe(true);
    }
  });

  it('the seven reports fixed for the 2026-09-22 complaint label it Sr.No.', () => {
    for (const key of LABELLED_SR_NO) {
      expect(reportConfigs[key].rowNumberTitle, `${key} rowNumberTitle`).toBe('Sr.No.');
      // The label has to be explicit: resolveRowNumberTitle would otherwise hand back
      // 'No' for the Licence/Export configs and 'No.' for the *ImportPermit* ones.
      expect(resolveRowNumberTitle(reportConfigs[key]), `${key} resolved title`).toBe(
        'Sr.No.'
      );
    }
  });

  it('Import Licence Daily keeps the No. it has printed since the port', () => {
    // The one Daily config the bulk pass missed, so it never lost the column and has been
    // live with the legacy-viewer default. Its RDLC header is blank too, so there is no
    // parity argument either way -- left alone rather than changed under a complaint about
    // a different report. Aligning it to 'Sr.No.' is a separate call.
    const config = reportConfigs.ImportLicenceDailyReportNewLicenceReport;
    expect(config.rowNumberTitle).toBeUndefined();
    expect(resolveRowNumberTitle(config)).toBe('No.');
  });
});

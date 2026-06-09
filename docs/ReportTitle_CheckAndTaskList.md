# Report Title Check And Task List

Created: 2026-06-09
Updated: 2026-06-09 after adding report subtitles

Scope:

- `MPUReport`
- `MPUReportV3`
- `AccountSummaryReport`
- `ChequeNoReport`
- `OnlineFeesReport`

Reference pattern:

- Import Licence reports use `title` for the page/header/navigation title.
- Import Licence reports also use `reportSubtitle: importLicenceRangeSubtitle(...)` for the old RDLC-style report title printed above the table after filters are applied.
- `GenericReportPage` renders `PageHeader title={config.title}` always.
- `GenericReportPage` only sends `reportHeaderLines` to `BasicTable` when `reportHeading` or `reportSubtitle` exists.

## First Task

- [x] Add RDLC-style `reportSubtitle` to the five checked reports so each printed table title matches the old Tradenet 2.0 `header1` value.

Expected implementation pattern:

```ts
reportSubtitle: reportDateRangeSubtitle('Account Summary Report')
```

or, for MPU reports that use date + time:

```ts
reportSubtitle: reportDateTimeRangeSubtitle('MPU Report')
```

If existing helper names are changed, keep the behavior equivalent to the old `header1` strings below.

## Current Result Summary

| Report | New page/nav `title` exists? | New RDLC-style `reportSubtitle` exists? | Old RDLC uses `header1`? | Status |
| --- | --- | --- | --- | --- |
| `MPUReport` | Yes: `MPU Report` | Yes | Yes | Fixed |
| `MPUReportV3` | Yes: `MPU Report V3` | Yes | Yes, same `MPUReport.rdlc` | Fixed |
| `AccountSummaryReport` | Yes: `Account Summary Report` | Yes | Yes | Fixed |
| `ChequeNoReport` | Yes: `Cheque No Report` | Yes | Yes | Fixed |
| `OnlineFeesReport` | Yes: `Online Fees Report` | Yes | Yes | Fixed |

## Old Title Source Of Truth

Old project:

`C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin`

| Report | Old RDLC | Old controller `header1` text |
| --- | --- | --- |
| `MPUReport` | `ReportControl\MPUReport.rdlc` | `MPU Report ({FromDate} {FromTime}) To ({ToDate} {ToTime})` |
| `MPUReportV3` | Uses `ReportControl\MPUReport.rdlc` | `MPU Report ({FromDate} {FromTime}) To ({ToDate} {ToTime})` |
| `AccountSummaryReport` | `ReportControl\AccountSummaryReport.rdlc` | `Account Summary Report ({FromDate}) To ({ToDate})` |
| `ChequeNoReport` | `ReportControl\ChequeNoReport.rdlc` | `Cheque No Report ({FromDate}) To ({ToDate})` |
| `OnlineFeesReport` | `ReportControl\OnlineFeesReport.rdlc` | `Online Fees Report ({FromTime}) To ({ToTime})` |

Notes:

- `MPUReportV3` does not have a separate `MPUReportV3.rdlc` in the old project; the old controller renders `MPUReport.rdlc`.
- `OnlineFeesReport` old model names are `FromTime` / `ToTime`, but the displayed values are date-like filter values in the old report screen.

## New Config Evidence

Frontend config:

`Frontend\src\Report\config\reportConfigs.ts`

| Report | Config title | `reportSubtitle` found? |
| --- | --- | --- |
| `AccountSummaryReport` | `Account Summary Report` | Yes: `reportDateRangeSubtitle('Account Summary Report')` |
| `ChequeNoReport` | `Cheque No Report` | Yes: `reportDateRangeSubtitle('Cheque No Report')` |
| `MPUReport` | `MPU Report` | Yes: `reportDateRangeSubtitle('MPU Report')` |
| `MPUReportV3` | `MPU Report V3` | Yes: `reportDateRangeSubtitle('MPU Report')` |
| `OnlineFeesReport` | `Online Fees Report` | Yes: `reportDateRangeSubtitle('Online Fees Report')` |

Renderer evidence:

- `Frontend\src\Report\Page\GenericReportPage.tsx` renders the page title from `config.title`.
- The printable report title is only rendered through `reportHeaderLines`, which requires `config.reportHeading` or `config.reportSubtitle`.

## Fix Checklist

- [x] Add a reusable date range subtitle helper for reports that use `FromDate` / `ToDate`.
- [x] Add a reusable date-time range subtitle helper for reports that use date + time inputs, or confirm normalized filter values already contain full date-time strings.
- [x] Add `reportSubtitle` to `AccountSummaryReport`.
- [x] Add `reportSubtitle` to `ChequeNoReport`.
- [x] Add `reportSubtitle` to `OnlineFeesReport`.
- [x] Add `reportSubtitle` to `MPUReport`.
- [x] Add `reportSubtitle` to `MPUReportV3`.
- [ ] Verify after clicking Filter that the title appears above the table, like Import Licence reports.
- [ ] Verify the title does not appear before filters are applied, matching current `GenericReportPage` behavior.
- [ ] Verify Excel/export file names are unchanged.
- [x] Run `npm run build` from `Frontend`.

## Manual Browser Test List

For each report:

- [ ] Open report page.
- [ ] Confirm the page header still shows the existing `title`.
- [ ] Enter a small date range.
- [ ] Click Filter.
- [ ] Confirm printed/table title appears above the grid.
- [ ] Confirm date format in title matches old report expectation.
- [ ] Confirm title text matches old `header1` exactly, except for any approved modern date formatting.

Expected examples:

- `MPU Report (01/06/2026 00:00) To (03/06/2026 23:59)`
- `MPU Report (01/06/2026 00:00) To (03/06/2026 23:59)` for V3
- `Account Summary Report (01/06/2026) To (03/06/2026)`
- `Cheque No Report (01/06/2026) To (03/06/2026)`
- `Online Fees Report (01/06/2026) To (03/06/2026)`

## Open Questions Before Code Fix

- [x] Should `MPUReportV3` display `MPU Report` like old RDLC `header1`, or `MPU Report V3` to match the new page title? Decision: use `MPU Report` for old RDLC printed-title parity.
- [x] Should `OnlineFeesReport` title use `FromDate` / `ToDate` wording in the new UI, or preserve old `FromTime` / `ToTime` behavior only in generated text? Decision: use current normalized `FromDate` / `ToDate` values and old label text `Online Fees Report`.
- [ ] Should these five reports also enable `legacyReportViewer` if the goal is full RDLC visual parity, or only add the printed title line?

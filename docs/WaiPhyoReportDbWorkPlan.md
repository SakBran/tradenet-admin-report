# Wai Phyo Report DB Work Plan

Updated: 2026-06-25

## Scope After Meeting

Only work on report tasks owned by Wai Phyo, or shared with Wai Phyo.

Ko Htet-only tasks are removed from the active work list. Do not change those controllers, stored procedures, or indexes unless the user explicitly asks again.

## Working Rules

1. Pull and inspect the senior update before continuing DB work.
2. Work one stored procedure/report target at a time.
3. First make sure data returns. Then fix API/frontend mapping. Then tune performance.
4. Update this markdown before and after each report target.
5. Deploy SQL only to the correct `TradeNetDB` database.
6. Do not write database passwords into docs, commits, screenshots, or summaries.
7. For report parity/customer complaints, compare against old Tradenet 2.0 Admin first.

Old admin reference:

- Path: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin`
- RDLC columns source: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\*.rdlc`
- Old filters source: old report ASPX/page/code-behind files under the same project.

Target:

- One-day filters should return in under 30 seconds.
- Prefer under 10 seconds when practical.

## Current Summary Dashboard

Last updated: 2026-06-25

### PM Sheet Feedback Triage - 2026-06-25

Source checked:

- Google Sheet: `Stored Procedure Tradenet 2.0`, visible `External` tab, public HTML view.
- Sheet URL: `https://docs.google.com/spreadsheets/d/1paKyNjI_5bKekx9a7XZl3cNnfcrPk26NxjkkZ3sczSo/edit?gid=0#gid=0`
- Scope: rows assigned to `Wai Phyo` or shared with `Wai Phyo`. Ko Htet-only rows remain out of scope unless reassigned.
- Rule: for each customer complaint, compare against old Tradenet 2.0 Admin first before changing behavior.

Correction:

- The first pass accidentally copied the original complaint text.
- Active work should be based on the second-round/retest feedback only.
- Original requirement text remains useful for parity checks, but it is not the PM retest blocker list.

Detailed Wai Phyo feedback currently visible:

| Active priority | Report | Owner in sheet | Latest visible status / second feedback | Detailed feedback still visible in sheet | What to check first |
|---|---|---|---|---|---|
| P1 | Border Import Permit Amendment Report | Wai Phyo, Bran | `No data`; `Production တွင် data မထွက် နေ ပါ` | Production still has no data. | API request values and production-like DB source rows, then `sp_AmendReport_pagination.sql`. |
| P1 | Border Import Permit Extension Report | Wai Phyo, Bran | `Failed to load table data.` | Page/API still fails to load. | Controller request mapping, backend exception, then `sp_ExtensionReport_pagination.sql`. |
| P1 | Border Import Permit Cancellation Report | Wai Phyo, Bran | `No Data`; `Production တွင် data မထွက် နေ ပါ` | Need to know whether this is valid no-data or a production filter/query problem. | Source row query in target DB first, then `sp_CancelReport_pagination.sql`. |
| P1 | Border Import Permit Actual Amendment Report | Wai Phyo, Bran | `Still No Data` | Need to confirm whether approved actual-amend source rows exist. | Source row query first, then `sp_ActualAmendReport_pagination.sql`. |
| P1 | Export Licence Voucher Report | Wai Phyo | `NO Data (data ရှိပါသည်ထွက်ပါသည်။ Apply Type, Payment Type တို့မှာ blank နဲ့ရှာမရပါ။)` | Blank Apply Type/Payment Type cannot search; Total Amount total needed; Commodity Type and Total Amount columns have no data; Apply Type dropdown must be New/Amend/Extension/Actual Amend/Cancel; Payment Type dropdown must be Cash/MPU/Citizen; Export License Section dropdown; raw `=Parameters!header2.Value` and `=Parameters!header3.Value` headers still show; no Sakhan dropdown. | Old voucher parity, `reportConfigs.ts`, `sp_VoucherReport.cs`, then Export Licence branch in `sp_VoucherReport_pagination.sql`. |
| P2 | Border Import Permit By HS Code Report | Wai Phyo, Bran | `OK` with visible requirements | Total No of License; Import Permit Section dropdown; HSCode click to HSCode Detail; HSCode Detail needs Total No of License. | Frontend config and HSCode detail route/config. |
| P2 | Border Import Permit New Report | Wai Phyo, Bran | `OK` with visible requirements | Report title; Total No of License; currency-wise Total Value. | Frontend totals/footer config first. |
| P2 | Border Import Licence Extension Report | Wai Phyo | `OK` with visible requirements | Report title; Total No of License; currency-wise Total Value; Import Section dropdown for Import License sections. | Frontend config/totals first unless PM retest fails data. |
| P2 | Border Import Licence Cancellation Report | Wai Phyo | `OK` with visible requirements | Report title; Total No of License; currency-wise Total Value; Import Section dropdown for Import License sections. | Frontend config/totals first unless PM retest fails data. |
| P2 | Border Import Licence By HS Code Report | Wai Phyo | `OK` with visible requirements | Report title; Total No of License; remove Company Name column; Import Section dropdown for Import License sections; HSCode click to HSCode Detail; Start/End filter dropdown. | Frontend config and HSCode detail route/config. |
| P2 | Border Import Licence Voucher Report | Wai Phyo | `OK No data` | Current visible latest note says no data. | Source row query/API retest if PM says this is still active. |
| P2 | Border Import Licence Actual Amendment Report | Wai Phyo | `OK` with visible requirements | Report title; Total No of License; currency-wise Total Value; Import Section dropdown for Import License sections. | Frontend config/totals first unless PM retest fails data. |
| P2 | Border Import Licence New Report | Wai Phyo | `OK ok` | Latest visible note says ok. | No action unless PM retests as failed. |
| P2 | Border Export Permit Amendment Report | Wai Phyo | `OK` with visible requirements | Report title; Live has no data; Export Section dropdown must show Export Permit sections only. | Source row/API retest if still active; otherwise frontend section dropdown. |
| P2 | Border Export Permit Extension Report | Wai Phyo | `OK` with visible requirements | Report title; Total No of License; currency-wise Total Value; Export Section dropdown must show Export Permit sections only. | Frontend config/totals first unless PM retest fails data. |
| P2 | Border Export Permit Cancellation Report | Wai Phyo | `OK` with visible requirements | Report title; Total No of License; currency-wise Total Value; Export Section dropdown must show Export Permit sections only. | Frontend config/totals first unless PM retest fails data. |
| P2 | Border Export Permit By HS Code Report | Wai Phyo | `OK` with visible requirements | Report title; Total No of License; remove Company Name column; section dropdown text in sheet says Import Section but report is Border Export Permit so verify with old/admin PM expectation; HSCode click to HS Code Detail; Start/End filter dropdown. | Frontend config and HSCode detail route/config. |
| P2 | Border Export Permit Voucher Report | Wai Phyo | `OK` with visible requirements | Report title; Total Amount total; raw `=Parameters!header2.Value` and `=Parameters!header3.Value` headers; Licence No column exists and Licence Date column name must be fixed; Export Permit Section dropdown. | Frontend dynamic voucher header resolver and totals config. |
| P2 | Border Export Permit Actual Amendment Report | Wai Phyo | `OK Live တွင် data မထွက်ပါ` | Live has no data. | Source row query/API retest if still active. |
| P2 | Border Export Permit New Report | Wai Phyo | `OK` with visible requirements | Report title; Total No of License; currency-wise Total Value; Export Permit Section dropdown; All Sakhan returns data but selected Sakhan does not; remove Auto filter. | `sp_NewReport_pagination.sql` Sakhan filter and frontend config. |
| P2 | Export Licence Amendment Report | Wai Phyo | `OK` with visible requirements | Report title; Total No of License; currency-wise Total Value; Export License Section dropdown; Auto/None-Auto dropdown; remove Sakhan dropdown. | Frontend config/totals first, then API retest. |
| P2 | Export Licence Extension Report | Wai Phyo | `OK` with visible requirements | Report title; Export License Section dropdown; remove Sakhan dropdown. | Frontend config first. |
| P2 | Export Licence Cancellation Report | Wai Phyo | `OK` with visible requirements | Report title; Total No of License; currency-wise Total Value; Export License Section dropdown; remove Sakhan dropdown. | Frontend config/totals first. |
| P2 | Export Licence By HS Code Report | Wai Phyo | `OK` with visible requirements | Report title; Total No of License; HSCode click to HS Code Detail; HS Code Detail needs Total No of License; Export License Section dropdown; remove Sakhan dropdown. | Frontend config and HSCode detail route/config. |

Ko Htet-only rows with visible second/retest notes but not active Wai Phyo scope:

- Export Licence Daily Report: UAT no data, slow, later says fixed/old-admin filter.
- Export Licence Detail Report: earlier failed, later says data returns for May 1-31 2025 under 10 sec.
- Export Licence By Section / By Method / By Seller Country / Company List / Total Value: many have `all fixed - June 16 2026 - Updated` or slow/filter notes, but owners are Ko Htet in the sheet.
- Border Export Permit Daily/Detail/By Section/By Seller Country/Company List: owners are Ko Htet in the sheet.

Corrected recommended work order:

1. Reproduce the five P1 second-round failures first.
2. For no-data reports, first check source rows in the same database/environment PM tested.
3. For Export Licence Voucher, compare old admin voucher output, then fix blank-filter behavior and field/header mapping.
4. After P1 passes, handle P2 visible-output complaints in frontend batches: totals, section dropdowns, Sakhan removal, HSCode drilldowns, and voucher dynamic headers.

Current frontend target after the Border Export Permit / Border Import Licence passes:

- Focused live controller smoke coverage for the remaining Wai Phyo Export Licence reports whose SQL has already been fixed but still need API/UAT confidence.
- First concrete gaps found in current `reportConfigs.ts` on 2026-06-25:
  - `BorderImportPermitActualAmendmentReport` and `BorderImportPermitAmendmentReport` are missing the old readonly `CompanyName` filter.
  - Several Border Import Permit report filters still define `SakhanId` without `lookupName: 'sakhans'`.
  - `BorderImportPermitByHSCodeReport` is missing the old `Import Section` dropdown entirely in the new filter box, even though the old view has it.
- Fix plan for this pass:
  1. Add opt-in live DB controller smoke tests for `ExportLicenceVoucherReport`, `ExportLicenceNewReportNewReport`, and `ExportLicenceActualAmendmentReport`.
  2. Run them against `TradeNetDB` using the existing test env-var pattern.
  3. Record which reports return rows through the real API path and how long they take.

Result:

- Added `Backend.Tests/ExportLicenceWaiPhyoLiveDbSmokeTests.cs`.
- Live controller smoke run against `TradeNetDB` passed for:
  - `ExportLicenceVoucherReportController` with blank `ApplyType` / blank `PaymentType`
  - `ExportLicenceNewReportNewReportController`
  - `ExportLicenceActualAmendmentReportController`
- Measured controller times from the live test run:
  - Export Licence Voucher Report: 467 ms
  - Export Licence New Report (New Report): 2 s
  - Export Licence Actual Amendment Report: 760 ms
- Meaning: the real API/controller path now returns rows for these three reports on the tested data-bearing dates, not just the raw stored procedures in SSMS.

- Added `Backend.Tests/BorderImportLicenceWaiPhyoLiveDbSmokeTests.cs`.
- Live controller smoke run against `TradeNetDB` also passed for:
  - `BorderImportLicenceAmendmentReportController`
  - `BorderImportLicenceVoucherReportController`
  - `BorderImportLicenceNewReportNewReportController`
- Measured controller times from the live test run:
  - Border Import Licence Amendment Report: 746 ms
  - Border Import Licence New Report (New Report): 500 ms
  - Border Import Licence Voucher Report: 34 s on the tested broad range (`2026-05-01` to `2026-06-30`)
- Meaning: these three Border Import Licence reports do return rows through the real API path, but the voucher path is still materially slower than the others on a broader range and should stay on the performance-watch list even though data-show is no longer blocked.

- Added `Backend.Tests/BorderExportPermitWaiPhyoLiveDbSmokeTests.cs`.
- Live controller smoke run against `TradeNetDB` also passed for:
  - `BorderExportPermitAmendmentReportController`
  - `BorderExportPermitVoucherReportController`
  - `BorderExportPermitActualAmendmentReportController`
- Measured controller times from the live test run:
  - Border Export Permit Amendment Report: 355 ms
  - Border Export Permit Voucher Report: 1 s
  - Border Export Permit Actual Amendment Report: 651 ms
- Meaning:
  - Amendment and Voucher now prove row-return through the real API/controller path.
  - Actual Amendment now has a repeatable proof that the controller returns an empty result cleanly when the DB has no matching rows, which matches the earlier source-row audit instead of indicating a backend exception.

P1 progress:

| Report | Check date | Result |
|---|---:|---|
| Border Import Permit Amendment Report | 2026-06-25 | Redeployed `StoredProcedureMigrations/sp_AmendReport_pagination.sql` to `TradeNetDB`. Source-row check found 1 approved amend row in the PM range (`2026-05-22`). `EXEC dbo.sp_AmendReport_pagination @FormType=N'Border Import Permit'` returned that row with `TotalCount=1` in about 0.8s. Current DB/procedure is OK; if Production UI still shows no data, next check is deployed API/frontend request values or whether UAT/Production points at a different DB/procedure version. |
| Border Import Permit Extension Report | 2026-06-25 | Redeployed `StoredProcedureMigrations/sp_ExtensionReport_pagination.sql` to `TradeNetDB`. Source-row check found 19 approved extension rows in the PM range (`2024-02-29` to `2026-05-25`). `EXEC dbo.sp_ExtensionReport_pagination @FormType=N'Border Import Permit'` returned 19 rows with `TotalCount=19` in about 0.9s. `EXEC dbo.sp_ExtensionReportCurrencyTotals @FormType=N'Border Import Permit'` returned THB/USD/CNY totals in about 1.5s. Current DB/procedure and totals path are OK. |
| Border Import Permit Cancellation Report | 2026-06-25 | Redeployed `StoredProcedureMigrations/sp_CancelReport_pagination.sql` to `TradeNetDB`. Source-row check found 0 approved cancel rows in the PM range. `EXEC dbo.sp_CancelReport_pagination @FormType=N'Border Import Permit'` completed in about 1.3s and returned an empty result set without SQL error. This is a valid no-data DB result for the checked range; if the UI shows `Failed to load table data`, check API/frontend request handling or deployed procedure version. |
| Border Import Permit Actual Amendment Report | 2026-06-25 | Redeployed `StoredProcedureMigrations/sp_ActualAmendReport_pagination.sql` to `TradeNetDB`. Source-row check found 0 approved actual-amend rows in the PM range, and a grouped `ApplyType` check showed only `Amend`, `Extension`, and `New` rows for Border Import Permit in that range. `EXEC dbo.sp_ActualAmendReport_pagination @FormType=N'Border Import Permit'` completed in about 1.1s and returned an empty result set without SQL error. This is a valid no-data DB result for the checked range. |
| Export Licence Voucher Report | 2026-06-25 | Compared old admin `ExportLicenceVoucherReport.cshtml` / `VoucherReport.rdlc` behavior: filters are date range, Export Section, Apply Type, Payment Type, Company Registration No, Company Name, with no Sakhan. New config already matches that filter shape and resolves the old RDLC dynamic Licence No/Licence Date headers. Fixed `StoredProcedureMigrations/sp_VoucherReport_pagination.sql` Export Licence branch so blank `@ApplyType` no longer filters out every row. Deployed to `TradeNetDB`. Exact total-count queries for this branch still time out, so `ExportLicenceVoucherReportController` now uses fast paging to show rows first. DB tests for `2023-04-03` returned rows with blank Apply/Payment filters in about 1.1s and returned selected `ApplyType=N'Amend'` rows in about 1.2s. Backend build passed. |
| Export Licence New Report (New Report) | 2026-06-25 | Compared old admin `ExportLicenceNewReport.cshtml` / `NewLicenceReport.rdlc`: old filters are date range, Export Section, Company Registration No, and readonly Company Name; new keeps those and also has the PM-requested Auto/None-Auto filter, with no Sakhan filter. Old/new columns both include `quota`, but the live `TradeNetDB.dbo.ExportLicence` table has no `quota` column; only import licence tables expose that field. Kept the paginated procedure returning a typed blank quota so the report loads instead of failing on a non-existent column. Updated `ExportLicenceNewReportNewReportController` to use fast paging because wide exact total-count queries previously timed out before data showed. Redeployed `StoredProcedureMigrations/sp_NewReport_pagination.sql` to `TradeNetDB`. DB test for `2023-04-03` with blank filters returned rows in about 0.8s. Backend build passed with existing warnings only. |

### Git Integration - 2026-06-22

- Pulled and fast-forwarded `origin/main` to `cc4bfeb`.
- Reapplied the local Wai Phyo report work after the pull; `reportConfigs.ts` merged automatically with no unresolved conflict markers.
- Preserved the senior update from `a1c1bca`, including `openInNewTab: true` on the Import Licence detail drilldown.
- Re-ran config syntax/static checks and targeted backend verification before commit and push.

### Old Admin Parity Audit - 2026-06-22

Source of truth checked directly:

- Old project: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin`
- Old filters: `Views/Reports/*.cshtml` plus `ReportsController.cs` dropdown sources.
- Old columns: `ReportControl/*.rdlc`.
- New filters/columns: `Frontend/src/Report/config/reportConfigs.ts` and generated row numbers from `BasicTable.tsx`.

No report behavior was changed during this audit. Diffs are recorded first as required by `AGENTS.md`.

| Wai Phyo report | Old/new parity | Difference found |
|---|---|---|
| Border Import Licence Actual Amendment | Same | Filters and visible columns match old admin. |
| Border Import Licence Amendment | Same | Filters and visible columns match old admin. |
| Border Import Licence Cancellation | Same | Filters and visible columns match old admin. |
| Border Import Licence Extension | Same | Filters and visible columns match old admin. |
| Border Import Licence Voucher | Same | Filters and voucher columns match; dynamic licence headers are resolved at runtime. |
| Border Import Licence By HS Code | Intentional difference | New report removes old `Company Name` column per newer customer feedback. |
| Border Import Licence New Report | Different | New is missing old readonly `Company Name` filter and adds an `Auto` filter that old admin did not have. Columns match. |
| Border Export Permit Actual Amendment | Different | New is missing old readonly `Company Name` filter; header is `HS Code` while old RDLC text is `HSCode`. |
| Border Export Permit Amendment | Different | New is missing old readonly `Company Name` filter and the old `HSCode` column. |
| Border Export Permit Cancellation | Different | New is missing old readonly `Company Name` filter; header is `HS Code` while old RDLC text is `HSCode`. |
| Border Export Permit Extension | Different | New is missing old readonly `Company Name` filter. Columns match. |
| Border Export Permit By HS Code | Intentional difference | New removes old `Company Name` column per newer customer feedback. |
| Border Export Permit Voucher | Same at runtime | Filters match, including readonly Company Name. Dynamic header resolver replaces old RDLC parameter expressions with the selected Apply Type labels. |
| Border Export Permit New Report | Different | New is missing old readonly `Company Name` filter and old `auto` output column. Old admin did not have an Auto search filter. |
| Export Licence Actual Amendment | Different | New is missing old `HSCode` output column. Filters match old admin. |
| Export Licence Amendment | Different / feedback conflict | New is missing old `HSCode` column and adds Auto/None-Auto requested by newer feedback; old admin had no Auto filter. |
| Export Licence Cancellation | Different | New is missing old `HSCode` output column. Filters match old admin. |
| Export Licence Extension | Same | Filters and visible columns match old admin. |
| Export Licence By HS Code | Intentional difference | New removes old `Company Name` column per newer customer feedback. |
| Export Licence Voucher | Same at runtime | Filters and columns match; dynamic licence headers are resolved at runtime. |
| Export Licence New Report | Different / feedback conflict | Columns match old admin, but new adds Auto/None-Auto requested by newer feedback; old admin had no Auto filter. |

Important decision rule for the fix pass:

- Restore unintentional missing old fields such as HSCode and readonly Company Name.
- Do not automatically undo intentional customer-feedback differences (Company Name removal on HS Code reports and Auto/None-Auto additions) until confirming whether the latest sheet feedback overrides strict old-admin parity.
- After UI parity decisions, compare old stored procedure and new pagination procedure output using identical parameters: row count, grouping, currency totals, and representative field values.

DB parity test status on 2026-06-22:

- Attempted a read-only metadata connection using `TradeNetDBTest` from `Backend/appsettings.json`; credentials were not printed or copied into this document.
- The remote connection did not complete and returned no metadata before the attempt was stopped after more than two minutes.
- Therefore row-by-row old procedure versus pagination procedure comparison is still pending; the completed results above cover old-view/RDLC versus new frontend parity only.

### Next Target

Next target: next Wai Phyo PM-feedback item after Border Export Permit Voucher verification.

Reason: Border Export Permit Voucher was rechecked against old Tradenet 2.0 Admin, patched for date-only frontend filters, redeployed to `TradeNetDB`, and DB-tested for page rows plus footer totals.

Current frontend target: Export Licence complaint group.

Why: DB data-show was already fixed for the main Export Licence Wai Phyo reports. The remaining complaints are mostly frontend parity/customer-feedback issues: report subtitles, totals wiring, Export Licence section dropdowns, removing Sakhan from non-border reports, and adding Auto/None-Auto where the sheet asks for it.

2026-06-18 restore note:

- Before pulling senior changes, `Frontend/src/Report/config/reportConfigs.ts` was backed up to `Frontend/src/Report/config/reportConfigs.backup-20260618-203139.ts`.
- After restoring/pulling the senior version, only the Wai Phyo/customer-complaint config blocks were restored from the backup.
- Restored areas: HS Code complaint configs, voucher complaint configs, Export Licence action/new configs, Border Export Permit action/new configs, and Border Import Licence action/voucher/HS Code configs.
- Extra fix after restore: `BorderImportLicenceVoucherReport` now has footer total wiring with `currencyTotalsColumns: { labelColumnKey: 'LicenceNo', valueColumnKey: 'Amount' }`.
- Static complaint restore check passed after the merge.
- `Frontend/src/Report/config/reportConfigs.ts` TypeScript transpile syntax check passed.
- `dotnet build Backend/API.csproj --no-restore` passed with warnings only.
- Targeted backend report tests passed: 178/178 for `ReportQueryTranslationTests`, `ReportControllerBranchDefaultsTests`, and `BorderImportLicenceParityTests`.
- Full frontend `npm run build` is still blocked by the local dependency tree before app bundling: missing type definitions for `chai` and `deep-eql`.

Completed Priority 1 API/controller retests:

1. Border Import Licence Amendment Report
2. Border Import Licence Voucher Report
3. Border Import Licence New Report (New Report)
4. Border Export Permit New Report (New Report)
5. Export Licence By HS Code Report

Still pending from Priority 1: none.

### Done / DB Fixed

| Report | Procedure | DB/API status | Last measured time | What was fixed |
|---|---|---|---:|---|
| Border Import Permit Extension Report | `dbo.sp_ExtensionReport_pagination` | DB + API smoke passed | 83 ms DB / 105 ms API | Senior SQL verified after pull; endpoint executes. |
| Border Import Permit Cancellation Report | `dbo.sp_CancelReport_pagination` | DB + API smoke passed, valid no-data | 61 ms DB | Procedure works; DB has no approved cancel rows for tested range. |
| Border Import Permit Voucher Report | `dbo.sp_VoucherReport_pagination` | DB + API smoke passed | 129 ms DB | Senior SQL verified after pull; returns voucher rows. |
| Border Import Permit Actual Amendment Report | `dbo.sp_ActualAmendReport_pagination` | DB + API smoke passed, valid no-data | 76 ms DB | Procedure works; DB has no approved actual-amend rows for tested range. |
| Border Import Permit New Report | `dbo.sp_NewReport_pagination` | DB + API smoke passed | 85 ms DB | Senior SQL verified after pull; returns New/Approved rows. |
| Border Export Permit New Report | `dbo.sp_NewReport_pagination` | API/controller retest passed | 1,060 ms API | DB returns rows and controller returns `totalCount=42`, `pageCount=20`. |
| Export Licence Voucher Report | `dbo.sp_VoucherReport_pagination` | DB data-show + visible value fields passed | 888 ms blank-filter fast page / 718 ms selected ApplyType fast page | Blank ApplyType/PaymentType returns rows; Currency and TotalAmount now populate from paged-row item lookups. |
| Export Licence New Report | `dbo.sp_NewReport_pagination` | DB data-show passed | 10,246 ms fast page | Date-only frontend filters now include the full selected day and return rows. |
| Border Import Licence Amendment Report | `dbo.sp_AmendReport_pagination` | API/controller retest passed | 1,618 ms API | Fixed wrong Sakhan filter comparing section id to Sakhan id; controller returns `totalCount=4`, `pageCount=4`. |
| Border Import Licence Voucher Report | `dbo.sp_VoucherReport_pagination` | API/controller retest passed | 4,394 ms API / 3,114 ms DB | Empty ApplyType now means all; ToDate includes whole day; exact count moved outside dynamic page query; added `TransactionFormType` filter and existing index hint. |
| Border Import Licence New Report | `dbo.sp_NewReport_pagination` | API/controller retest passed | 1,345 ms API | ToDate includes whole day; empty Auto means all; controller returns `totalCount=824`, `pageCount=20`. |
| Export Licence By HS Code Report | `dbo.sp_HSCodeReport_pagination` | DB + API/controller retest passed; frontend drilldown config added | 1,192 ms DB fast page / 948 ms DB exact count / 1 focused test passed | Added `@IncludeTotalCount`; Export Licence branch can now skip `COUNT(*) OVER()` for fast-page requests. HSCode cells now target `ExportLicenceHSCodeDetailReport`, which reuses the same API with old detail columns. |
| Export Licence Total Value & Licences Report | `dbo.sp_ExportLicenceTotalValueReport_Fast_pagination` | DB + API/controller retest passed | 747 ms DB exact count / 1 focused test passed | Replaced the full detail-row aggregation path with a dedicated currency aggregate procedure that materializes filtered licence IDs before joining items. |
| Border Export Licence Detail Report split work | `dbo.sp_BorderExportLicenceDetailReport_Pagination` | Dedicated proc deployed; real code-path smoke passed | 25.99s DB fast page / 1 focused test passed in 38s | Added a dedicated Border-only procedure file, routed `Type="Border"` through it, moved Port/Destination name resolution to cached C# lookup, restored expected result columns, and capped SQL memory grant to survive `RESOURCE_SEMAPHORE` pressure. |
| Border Export Permit Voucher Report | `dbo.sp_VoucherReport_pagination` + `dbo.sp_ExportPermitVoucherCurrencyTotals` | DB data-show + footer totals passed | 106 ms one-day page / 40 ms one-day footer / 107 ms broad page / 45 ms broad footer | Old RDLC footer sums voucher `Amount`; patched Border Export Permit voucher date filters to include the full selected ToDate and kept footer totals aligned. |
| Border Export Permit New Report | `dbo.sp_NewReport_pagination` + `dbo.sp_ExportPermitListingCurrencyTotals` | DB data-show + Sakhan filter + footer totals passed | 61 ms page / 35-589 ms footer | Confirmed selected Sakhan returns rows; patched Border Export Permit currency totals to include the full selected ToDate; frontend now restores old readonly Company Name filter and still has no Auto filter. |
| Border Export Permit Amendment Report | `dbo.sp_AmendReport_pagination` + `dbo.sp_ExportPermitListingCurrencyTotals` | DB data-show + footer totals passed | 62 ms page / 36 ms footer | Deployed shared action-report sort fix so `SortColumn=Date` no longer creates duplicate `ORDER BY [Date]`; one-day test returned 1 row with `TotalCount=1` and USD 1 / 1000 total. |
| Border Export Permit Extension Report | `dbo.sp_ExtensionReport_pagination` | DB data-show passed | 80 ms page | Deployed shared action-report sort fix; one-day `2026-05-25` test returned 1 row with `TotalCount=1`. |
| Border Export Permit Cancellation Report | `dbo.sp_CancelReport_pagination` | DB data-show passed | 58 ms page | Deployed shared action-report sort fix and fixed Border Export/Import Permit cancel date filtering to include the full selected ToDate; one-day `2025-09-19` test returned the previously hidden cancellation row with `TotalCount=1`. |
| Border Export Permit Actual Amendment Report | `dbo.sp_ActualAmendReport_pagination` | DB smoke passed, valid no-data | 55 ms DB | Checked `TradeNetDB` from `2023-01-01` to `2026-06-30`; no `BorderExportPermit` source rows exist with `ApplyType='Actual Amend'`. The procedure returns an empty result set without SQL error, so current DB no-data is valid unless another environment has source rows. |
| HS Code Detail drilldown frontend | existing HSCode report APIs | Config/route smoke + DB samples passed | 33 ms Border Export Permit / 883 ms Border Import Licence / 35 ms Border Import Permit / 179 ms Export Licence | Added detail report configs/routes for `BorderExportPermitHSCodeDetailReport`, `BorderImportLicenceHSCodeDetailReport`, `BorderImportPermitHSCodeDetailReport`, and `ExportLicenceHSCodeDetailReport`. HSCode cells now open a detail-style grid with old RDLC detail columns: HS Code, Description, Company Name, No of Licences. No SQL change needed; it reuses the existing HSCode controllers with the clicked `hsCode` filter. DB-tested `Border Export Permit` on `2023-08-23` with HSCode `5513190000`, `Border Import Licence` on `2026-06-16` with HSCode `9615909300`, `Border Import Permit` on `2026-05-20` with HSCode `3901101200`, and `Export Licence` on `2023-04-03` with HSCode `6203499000`; all returned rows with `TotalCount`. Note: Border Import Permit source rows can have `CreatedDate` one day after `LicenceDate`, but this report filters by `LicenceDate`. Frontend TypeScript transpile check and backend build passed. |

### Not Done / Pending

| Report | Current status | Why it is still pending | Next action |
|---|---|---|---|
| Border Export Permit Amendment Report | DB fixed; frontend retest pending | Page rows and currency footer totals are now verified in `TradeNetDB`; frontend dependency issue blocks full local UI build. | Retest frontend after dependency install / UAT deploy. |
| Border Export Permit Extension Report | DB fixed; frontend retest pending | Page rows are now verified in `TradeNetDB`; frontend dependency issue blocks full local UI build. | Retest frontend after dependency install / UAT deploy. |
| Border Export Permit Cancellation Report | DB fixed; frontend retest pending | Page rows are now verified in `TradeNetDB`; frontend dependency issue blocks full local UI build. | Retest frontend after dependency install / UAT deploy. |
| Border Export Permit By HS Code Report | Data-show + supported parity fixes done | Title, section filter, Start/End dropdown, CompanyName removal, backend section mapping done; DB returns 74 grouped rows in 279 ms. HSCode drilldown now opens `BorderExportPermitHSCodeDetailReport`, which reuses the existing HSCode API with the clicked HSCode and detail columns. | Frontend retest after dependency install / UAT deploy. |
| Border Export Permit Actual Amendment Report | Valid no-data + supported parity fixes done | `TradeNetDB` has 0 approved Border Export Permit `Actual Amend` rows; section/title/extra HSCode fixed | Retest frontend after dependency install; no SQL fix unless source data is expected. |
| Export Licence Voucher Report | Data shows; Currency/TotalAmount restored | Wide exact count is still intentionally skipped in the controller so rows show first | Retest frontend; optimize wide exact totals later only if PM requires exact total count. |
| Export Licence New Report | Data shows + frontend complaint config updated | Performance can still be improved later | Retest frontend; Auto/None-Auto and no-Sakhan config now applied. |
| Export Licence Amendment Report | DB data-show passed + frontend complaint config updated | If frontend still fails, the DB procedure is not the blocker | Retest frontend; totals, Export Section lookup, Auto/None-Auto, no-Sakhan config now applied. |
| Export Licence Extension Report | DB data-show passed + frontend complaint config updated | If frontend still fails, the DB procedure is not the blocker | Retest frontend; Export Section lookup and no-Sakhan config now applied. |
| Export Licence New Report quota | DB output fixed | Real quota source does not exist on `ExportLicence`; old procedure also did not select it | Retest frontend; quota should render blank instead of `N/A`. |
| Border Export Licence Detail dedicated proc | Data-show passed; performance tuning still pending | One-day DB fast page now returns and the real backend code path passes, but the result is still above target and clean benchmarks are distorted by SQL memory pressure and long `KILLED/ROLLBACK` backlog | Re-test one-day / one-month / multi-year after server pressure clears, then tune only this dedicated proc toward the under-10-second target. |

### Customer Complaint Frontend Pass - Export Licence - 2026-06-17

Reference checked:

- Old admin path: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin`
- Live Google Sheet public HTML view checked on 2026-06-17:
  - `https://docs.google.com/spreadsheets/d/1paKyNjI_5bKekx9a7XZl3cNnfcrPk26NxjkkZ3sczSo/edit?gid=0#gid=0`
  - Wai Phyo rows visible around Border Import Licence, Border Export Permit, and Export Licence sections.
- Old Export Licence action report filters use From/To, Export Section, Company Registration No, readonly Company Name, and Remark where applicable.
- Old Export Licence action reports do not use Sakhan because they are oversea Export Licence reports.
- Customer sheet additionally asks for Auto/None-Auto on Export Licence Amendment and Export Licence New Report. That is a feedback-driven addition beyond the old filter form.

Frontend changes applied in `Frontend/src/Report/config/reportConfigs.ts`:

| Report | What changed | Status |
|---|---|---|
| Export Licence Actual Amendment Report | Added report subtitle, currency total footer wiring, Export Licence section lookup, readonly Company Name filter, removed Sakhan filter, removed visible `hsCode` column. | Config updated; frontend retest pending. |
| Export Licence Amendment Report | Added report subtitle, currency total footer wiring, Export Licence section lookup, readonly Company Name filter, Auto/None-Auto filter, removed Sakhan filter, removed visible `hsCode` column. | Config updated; frontend retest pending. |
| Export Licence Cancellation Report | Added report subtitle, currency total footer wiring, Export Licence section lookup, readonly Company Name filter, removed Sakhan filter, removed visible `hsCode` column. | Config updated; frontend retest pending. |
| Export Licence Extension Report | Switched filters to Export Licence section lookup, readonly Company Name filter, and no Sakhan filter. Existing subtitle and currency total wiring kept. | Config updated; frontend retest pending. |
| Export Licence New Report (New Report) | Added report subtitle, currency total footer wiring, Export Licence section lookup, readonly Company Name filter, Auto/None-Auto select filter, removed Sakhan filter. | Config updated; frontend retest pending. |
| Export Licence Voucher Report | Kept old-admin filter shape: hidden derived FormType, Export Licence section lookup, Apply Type, Payment Type, Company Registration No, readonly Company Name, and no Sakhan. Existing dynamic voucher header resolver kept. | Config already aligned; verification guard added, frontend retest pending. |

Verification status:

- No stored procedure or index changed in this frontend pass.
- Static config check passed for the touched Export Licence reports: Sakhan filters are removed, Export Licence section filter wiring exists, and currency total wiring exists.
- Extended `Frontend/src/Report/config/reportConfigs.exportLicence.test.ts` with Wai Phyo complaint guards for Export Licence action/voucher reports: no Sakhan filter, Export Licence section lookup, readonly Company Name, hidden `hsCode` removal on action reports, and dynamic voucher header titles.

### Customer Complaint Frontend Pass - Border Import Licence - 2026-06-25

Reference checked:

- Old admin path: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin`
- Old report filter forms rechecked against:
  - `Views/Reports/BorderImportLicenceExtensionReport.cshtml`
  - `Views/Reports/BorderImportLicenceVoucherReport.cshtml`
- Existing old/new parity notes in this work plan for Border Import Licence action/voucher reports still hold: Sakhan stays on Border Import Licence reports, section lookup must stay `borderImportLicenceSections`, and voucher keeps dynamic licence headers.

What was done in this pass:

- Added explicit frontend regression guards in `Frontend/src/Report/config/reportConfigs.borderImportLicence.test.ts` for:
  - `BorderImportLicenceActualAmendmentReport`
  - `BorderImportLicenceAmendmentReport`
  - `BorderImportLicenceCancellationReport`
  - `BorderImportLicenceExtensionReport`
  - `BorderImportLicenceVoucherReport`
- Locked the Wai Phyo complaint expectations in tests:
  - action reports keep Sakhan + Border Import Licence section + readonly Company Name
  - action reports keep currency footer totals on `TotalValue`
  - action report subtitles keep the legacy `List of Border Import Licence Report ...` wording
  - voucher keeps Sakhan, payment-type lookup, Border Import Licence section lookup, footer totals on `Amount`, and dynamic Amendment/Cancellation column headers

Verification:

- `reportConfigs.borderImportLicence.test.ts` TypeScript transpile check passed.
- `dotnet build Backend/API.csproj --no-restore` passed with 0 warnings and 0 errors.
- No SQL or DB deployment was needed in this pass because this was a frontend parity-lock / regression-guard update only.

### Customer Complaint Frontend Pass - Border Export Permit - 2026-06-25

Reference checked:

- Old admin path: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin`
- Old report filter forms rechecked against:
  - `Views/Reports/BorderExportPermitAmendReport.cshtml`
  - `Views/Reports/BorderExportPermitCancelReport.cshtml`
  - `Views/Reports/BorderExportPermitExtensionReport.cshtml`
  - `Views/Reports/BorderExportPermitNewReport.cshtml`
  - `Views/Reports/BorderExportPermitVoucherReport.cshtml`

Real parity fixes applied in this pass:

- `BorderExportPermitActualAmendmentReport`
  - restored old readonly `CompanyName` filter
  - wired `SakhanId` to `lookupName: 'sakhans'`
- `BorderExportPermitAmendmentReport`
  - restored old readonly `CompanyName` filter
  - wired `SakhanId` to `lookupName: 'sakhans'`
- `BorderExportPermitCancellationReport`
  - restored old readonly `CompanyName` filter
  - wired `SakhanId` to `lookupName: 'sakhans'`
- `BorderExportPermitExtensionReport`
  - restored old readonly `CompanyName` filter
  - wired `SakhanId` to `lookupName: 'sakhans'`
- `BorderExportPermitNewReportNewReport`
  - kept old readonly `CompanyName` filter and explicitly wired `SakhanId` to `lookupName: 'sakhans'`

Regression guards added:

- New test file: `Frontend/src/Report/config/reportConfigs.borderExportPermit.test.ts`
- Locked the Wai Phyo complaint expectations for:
  - Border Export Permit Actual Amendment
  - Border Export Permit Amendment
  - Border Export Permit Cancellation
  - Border Export Permit Extension
  - Border Export Permit New Report
  - Border Export Permit Voucher
- Tests now verify:
  - old-admin filter shape
  - Border Export Permit section lookup
  - readonly `CompanyName`
  - `Sakhan` lookup wiring
  - footer totals wiring
  - legacy subtitle wording
  - voucher dynamic Amendment/Cancellation header titles

Verification:

- `reportConfigs.borderExportPermit.test.ts` TypeScript transpile check passed.
- `reportConfigs.ts` TypeScript transpile check passed after the filter fixes.
- `dotnet build Backend/API.csproj --no-restore` passed with 0 warnings and 0 errors.
- No SQL or DB deployment was needed in this pass because the DB-side Border Export Permit fixes were already in place; this pass corrected frontend parity only.

### Customer Complaint Frontend Pass - Border Import Permit - 2026-06-25

Reference checked:

- Old admin path: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin`
- Old report filter forms rechecked against:
  - `Views/Reports/BorderImportPermitAmendReport.cshtml`
  - `Views/Reports/BorderImportPermitByHSCodeReport.cshtml`
  - `Views/Reports/BorderImportPermitCancelReport.cshtml`
  - `Views/Reports/BorderImportPermitExtensionReport.cshtml`
  - `Views/Reports/BorderImportPermitNewReport.cshtml`
  - `Views/Reports/BorderImportPermitVoucherReport.cshtml`

Real parity fixes applied in this pass:

- `BorderImportPermitActualAmendmentReport`
  - restored old readonly `CompanyName` filter
  - wired `SakhanId` to `lookupName: 'sakhans'`
- `BorderImportPermitAmendmentReport`
  - restored old readonly `CompanyName` filter
  - wired `SakhanId` to `lookupName: 'sakhans'`
- `BorderImportPermitCancellationReport`
  - restored old readonly `CompanyName` filter
  - wired `SakhanId` to `lookupName: 'sakhans'`
- `BorderImportPermitExtensionReport`
  - restored old readonly `CompanyName` filter
  - wired `SakhanId` to `lookupName: 'sakhans'`
- `BorderImportPermitNewReportNewReport`
  - restored old readonly `CompanyName` filter
  - wired `SakhanId` to `lookupName: 'sakhans'`
- `BorderImportPermitVoucherReport`
  - restored old readonly `CompanyName` filter
  - wired `SakhanId` to `lookupName: 'sakhans'`
- `BorderImportPermitByHSCodeReport`
  - restored the missing old `Import Section` dropdown
  - wired `SakhanId` to `lookupName: 'sakhans'`
  - updated HS Code drilldown to carry `ExportImportSectionId`
- `BorderImportPermitHSCodeDetailReport`
  - restored the matching `Import Section` filter on the drilldown page
  - kept the Sakhan lookup wired to `sakhans`

Regression guards added:

- New test file: `Frontend/src/Report/config/reportConfigs.borderImportPermit.test.ts`
- Locked the filter-shape expectations for:
  - Border Import Permit Actual Amendment
  - Border Import Permit Amendment
  - Border Import Permit By HS Code
  - Border Import Permit Cancellation
  - Border Import Permit Extension
  - Border Import Permit HS Code Detail
  - Border Import Permit New Report
  - Border Import Permit Voucher

Verification:

- `reportConfigs.borderImportPermit.test.ts` TypeScript transpile check passed.
- `reportConfigs.ts` TypeScript transpile check passed after the filter fixes.
- `dotnet build Backend/API.csproj --no-restore` passed with 0 warnings and 0 errors.
- No SQL or DB deployment was needed in this pass because the DB-side Border Import Permit work had already been verified earlier; this pass corrected frontend parity only.
- Follow-up finish in the same pass:
  - restored legacy range subtitles on Border Import Permit Actual Amendment / Amendment / Cancellation / Extension / New / Voucher configs
  - extended `reportConfigs.borderImportPermit.test.ts` to assert those subtitle strings explicitly
  - extended `reportConfigs.borderImportPermit.test.ts` again to assert the restored HS Code `Import Section` filter and drilldown/detail carry-through
  - transpile + backend build still passed after the subtitle guard additions
- Extra HS Code cleanup in the same customer-complaint pass:
  - `BorderImportLicenceByHSCodeReport` now wires the visible `Sakhan` filter to `lookupName: 'sakhans'`
  - `BorderExportPermitByHSCodeReport` now wires the visible `Sakhan` filter to `lookupName: 'sakhans'`
  - extended the existing Border Import Licence / Border Export Permit config tests so those HS Code filter and drilldown expectations stay locked
- Re-verified after the extra HS Code cleanup:
  - TypeScript transpile check passed for `reportConfigs.ts`, `reportConfigs.borderImportPermit.test.ts`, `reportConfigs.borderImportLicence.test.ts`, and `reportConfigs.borderExportPermit.test.ts`
  - `dotnet build Backend/API.csproj --no-restore` still passed with 0 warnings and 0 errors
- TypeScript transpile syntax check passed for `Frontend/src/Report/config/reportConfigs.exportLicence.test.ts`.
- `npm run build -- --mode development` was attempted from `Frontend`; it failed before bundling because existing test files import `vitest`, but `Frontend/node_modules/vitest` is missing locally.
- `npm run test -- src/Report/config/reportConfigs.exportLicence.test.ts` was attempted; it is currently blocked because the local `vitest` command/package install is incomplete in this workspace.

### Live Sheet Follow-Up - 2026-06-17

Source checked:

- Google Sheet public HTML view, visible `External` tab.
- Wai Phyo rows found:
  - Border Import Licence: Amendment, Extension, Cancellation, By HS Code, Voucher, Actual Amendment, New Report.
  - Border Export Permit: Amendment, Extension, Cancellation, By HS Code, Voucher, Actual Amendment, New Report.
  - Export Licence: Amendment, Extension, Cancellation, By HS Code, Voucher, Actual Amendment, New Report.

Scope rule:

- Ko Htet-only rows remain out of scope unless reassigned.
- Shared rows with Wai Phyo are in scope only where the complaint is for the Wai Phyo stored-procedure family.

Current next checks:

1. Add HS Code detail drilldown links where the sheet asks for HSCode click-through.
2. Run static config checks after edits.
3. Test the underlying stored procedure with clicked HSCode/date values.

HS Code changes applied:

| Report | What changed | Status |
|---|---|---|
| Border Import Licence By HS Code Report | Added report subtitle, Import Licence section dropdown, Start/End filter dropdown, removed Company Name column, mapped `ExportImportSectionId` through `BorderImportLicenceByHSCodeReportController`, and added HSCode detail drilldown. | Config/backend updated; TypeScript transpile check, backend build, and DB detail sample passed. |
| Border Import Permit By HS Code Report | Added HSCode detail drilldown target for the shared Wai Phyo/Bran complaint. | Config updated; TypeScript transpile check, backend build, and DB detail sample passed. |
| Border Export Permit By HS Code Report | Added Border Export Permit section dropdown, removed Company Name column, and added HSCode detail drilldown. Existing Start/End dropdown and subtitle kept. | Config updated; TypeScript transpile check, backend build, and DB detail sample passed. |
| Export Licence By HS Code Report | Added report subtitle, Export Licence section dropdown, Start/End filter dropdown, removed Company Name column, removed Sakhan filter, and added HSCode detail drilldown. | Config updated; TypeScript transpile check, backend build, and DB detail sample passed. |

HS Code drilldown changes:

- Border Import Licence, Border Import Permit, Border Export Permit, and Export Licence HS Code columns now drill into a detail-style HS Code report with the clicked `hsCode` applied.
- Drilldown carries the active date range, section, Filter By, and Sakhan where that report has Sakhan.
- Static drilldown validation passed for all four new detail targets: config exists, route exists, page file exists, and a source report links to it.
- DB samples passed for all four detail targets with `TotalCount` returned.

Voucher follow-up change:

- Shared voucher Apply Type filter now defaults to blank/all and includes an `--- All ---` option before New/Amend/Extension/Cancel/Actual Amend.
- This is needed for the Export Licence Voucher complaint: data exists but blank Apply Type / Payment Type searches did not work from the frontend.
- Border Export Permit Voucher Apply Type now also defaults to blank/all, and Payment Type now uses the shared Cash/MPU/Citizen Pay option list.

Test status:

- `dotnet build Backend/API.csproj` passed after the Border Import Licence HS Code controller change. Existing warnings remain; no errors.
- `npm install` was attempted in `Frontend` to restore missing test dependencies, but it timed out after 2 minutes and `Frontend/node_modules/vitest` is still missing.
- Frontend build is still blocked by missing local `vitest` dependency in `Frontend/node_modules`.

### Complaint Verification Pass - 2026-06-17

Goal:

- Test whether the customer-complaint fixes work, not just whether code compiles.

Planned checks:

1. Restore frontend dependencies so `vitest` and TypeScript can run.
2. Run targeted frontend report-config tests/static checks for:
   - HS Code section filters, Start/End filter, Company Name removal, and drilldown metadata.
   - Voucher blank/all Apply Type and Payment Type options.
   - Export Licence action reports with no Sakhan filter and correct Export Licence section lookup.
3. Run backend build after the Border Import Licence HS Code controller change.
4. If the app can be started locally with available credentials/auth, do browser/runtime verification for the changed report pages.

Results:

- `npm install` failed because the existing Storybook dependencies conflict (`@storybook/addon-essentials@8.6.14` wants Storybook 8 while the project has Storybook 9).
- `npm install --legacy-peer-deps` also failed with an npm internal `Exit handler never called` error.
- `node_modules/vitest` exists after the partial install, but `node_modules/.bin/vitest` was not created, so `npm run test` cannot launch Vitest.
- `npx vitest ...` failed because npm could not verify the registry certificate (`UNABLE_TO_VERIFY_LEAF_SIGNATURE`).
- `npm run build` now gets past the old `vitest` missing error but fails on missing test type definitions for `chai` and `deep-eql`, confirming the frontend dependency tree is still incomplete.
- `dotnet build Backend/API.csproj --no-restore` passed with 0 warnings and 0 errors.
- Broad `dotnet test Backend.Tests/Backend.Tests.csproj --no-restore --filter "FullyQualifiedName~HSCode|FullyQualifiedName~Voucher|FullyQualifiedName~Report"` was attempted, but it is not a clean verification target for this work: it fails on existing suite issues such as missing stored procedures in the temporary test DB (`sp_ExtensionReport_pagination`, `sp_VoucherReport_pagination`, etc.) and unrelated controller fixture assumptions.
- Targeted non-DB report tests passed: `dotnet test Backend.Tests/Backend.Tests.csproj --no-restore --filter "FullyQualifiedName~ReportQueryTranslationTests|FullyQualifiedName~ReportControllerBranchDefaultsTests|FullyQualifiedName~BorderImportLicenceParityTests"` passed 178/178.
- Custom static complaint verification passed for the touched frontend config:
  - HS Code reports have Start/End filter, no Company Name column, and self-drilldown.
  - Export Licence HS Code has no Sakhan filter.
  - Voucher reports have dynamic header resolver and `Amount` mapped for Total Amount.
  - Shared voucher Apply Type supports blank/all.
  - Export Licence action reports have no Sakhan filter and use Export Licence section wiring.

### Customer Complaint Batch - 2026-06-17

Source:

- User pasted the Wai Phyo rows from the shared Google Sheet.
- Sheet link supplied by user: `https://docs.google.com/spreadsheets/d/1paKyNjI_5bKekx9a7XZl3cNnfcrPk26NxjkkZ3sczSo/edit?gid=0#gid=0`
- Work rule: update this markdown first, then fix one report at a time.
- Parity rule: for each customer complaint, compare the new report against old Tradenet 2.0 Admin columns and filters before changing behavior.

Out of scope unless the user explicitly reassigns it:

| Report | Owner note | Reason |
|---|---|---|
| Border Import Licence Pending Report | Ko Htet | User said only Wai Phyo part; pending report row is Ko Htet-owned. |

Wai Phyo complaint checklist:

| Priority | Report | Procedure | Current sheet status | Customer complaint / remaining work | First action |
|---|---|---|---|---|---|
| P1 | Border Import Licence Voucher Report | `dbo.sp_VoucherReport` | OK / No data | Page still says no data in some case. | Compare old filter defaults, then retest DB/API payload with blank filters. |
| P1 | Export Licence Voucher Report | `dbo.sp_VoucherReport` | OK / complaint says blank filters fail | Blank Apply Type / Payment Type should work; Commodity Type and Total Amount columns missing data; dynamic header labels wrong; no Sakhan dropdown. | Compare old voucher filters/columns, then fix frontend config and stored-procedure branch if needed. |
| P1 | Border Export Permit Voucher Report | `dbo.sp_VoucherReport` | OK | Total Amount footer; dynamic header labels show `=Parameters!header2.Value`; section dropdown must be Export Permit only. | Compare old voucher RDLC/config, then fix frontend config first. |
| P1 | Border Export Permit Actual Amendment Report | `dbo.sp_ActualAmendReport` | OK / Live no data | Live shows no data. | Confirm source rows in DB, then retest API with live-like date range. |
| P2 | Border Import Licence Actual Amendment Report | `dbo.sp_ActualAmendReport` | OK | Report title, total no. of licence, currency-wise total value, Import Licence section dropdown. | Compare old Actual Amend columns/filters, then apply config/summary footer parity. |
| P2 | Border Import Licence Extension Report | `dbo.sp_ExtensionReport` | OK | Report title, total no. of licence, currency-wise total value, Import Licence section dropdown. | Compare old Extension columns/filters, then apply config/summary footer parity. |
| P2 | Border Import Licence Cancellation Report | `dbo.sp_CancelReport` | OK | Report title, total no. of licence, currency-wise total value, Import Licence section dropdown. | Compare old Cancel columns/filters, then apply config/summary footer parity. |
| P2 | Border Export Permit Extension Report | `dbo.sp_ExtensionReport` | OK | Report title, total no. of licence, currency-wise total value, Export Permit section dropdown only. | Compare old Border Export Permit extension filters/columns. |
| P2 | Border Export Permit Cancellation Report | `dbo.sp_CancelReport` | OK | Report title, total no. of licence, currency-wise total value, Export Permit section dropdown only. | Compare old Border Export Permit cancel filters/columns. |
| P2 | Export Licence Amendment Report | `dbo.sp_AmendReport` | OK | Report title, totals, Export Licence section dropdown only, Auto/None-Auto dropdown, remove Sakhan dropdown. | Compare old Export Licence amend filters/columns and Import Permit reference. |
| P2 | Export Licence Cancellation Report | `dbo.sp_CancelReport` | OK | Report title, totals, Export Licence section dropdown only, remove Sakhan dropdown. | Compare old Export Licence cancel filters/columns. |
| P2 | Export Licence Actual Amendment Report | `dbo.sp_ActualAmendReport` | OK | Total amount, currency-wise total value, Export Licence section dropdown only, remove Sakhan dropdown. | Compare old Export Licence actual amend filters/columns. |
| P2 | Export Licence New Report (New Report) | `dbo.sp_NewReport` | OK | Total amount, currency-wise total value, Export Licence section dropdown only, Auto/None-Auto dropdown, remove Sakhan dropdown. | Compare old Export Licence new filters/columns. |
| P3 | Border Import Licence By HS Code Report | `dbo.sp_HSCodeReport` | OK | Report title, total no. of licence, remove Company Name column, Import Licence section dropdown, HSCode drilldown, Start/End filter. | Compare old HSCode RDLC/config and new drilldown route availability. |
| P3 | Border Export Permit By HS Code Report | `dbo.sp_HSCodeReport` | OK | Report title, total no. of licence, remove Company Name column, section dropdown wording, HSCode drilldown, Start/End filter. | Compare old HSCode RDLC/config and new drilldown route availability. |
| P3 | Export Licence By HS Code Report | `dbo.sp_HSCodeReport` | OK | Report title, total no. of licence, HSCode drilldown to detail page with total no. of licence, Export Licence section dropdown only. | Compare old HSCode RDLC/config and drilldown route. |
| P3 | Border Export Permit Amendment Report | `dbo.sp_AmendReport` | OK | Live data did not show earlier; Export Permit section dropdown only. | Reconfirm data on current DB/API and old filter parity. |
| P3 | Border Export Permit New Report (New Report) | `dbo.sp_NewReport` | OK | Totals, Export Permit section dropdown only, Sakhan-specific search, remove Auto filter. | Reconfirm current config after prior fix. |
| P3 | Border Import Licence New Report (New Report) | `dbo.sp_NewReport` | OK | Sheet says ok. | No action unless frontend retest fails. |
| P3 | Border Import Licence Amendment Report | `dbo.sp_AmendReport` | OK | No new complaint text in pasted row. | No action unless frontend retest fails. |
| P3 | Export Licence Extension Report | `dbo.sp_ExtensionReport` | OK | Export Licence section dropdown only, remove Sakhan dropdown. | Compare old filters; likely frontend config only. |

Working order:

1. Fix data-show / blank-filter issues first.
2. Then fix filter dropdown parity.
3. Then fix visible columns / header labels.
4. Then add totals / footer summaries where the current report framework supports it.
5. Drilldown links are last because they may need routes/config beyond the table column change.

### Remaining Wai Phyo Work

This is the current short list after the senior pull and count audit.

Second-feedback status note:

- `Finished` here means one of these is true:
  - old-admin / customer-complaint frontend parity was updated in code and guarded with config tests, or
  - the real API/controller path was smoke-tested against `TradeNetDB`, or
  - both.
- It does **not** mean every item has already been rechecked by PM in UAT after deploy.
- Confidence is highest on backend data-show/API behavior for the reports listed below as controller-smoke passed.
- Confidence is medium on exact visible parity items such as titles, footer totals, dropdown wording, and RDLC-style header text until UAT confirms the deployed frontend matches the PM expectation exactly.

Second-feedback checkpoint:

- `Finished in code` means we changed the repo to match the old-admin / PM feedback as closely as the current source-of-truth allows.
- `Backend-proven` means the live controller path was re-tested against `TradeNetDB` and returned data or a valid empty result.
- `Still left` means one of these is still pending:
  - deployed frontend / UAT confirmation,
  - PM-visible title / footer / dropdown wording confirmation,
  - or performance work that we intentionally postponed while prioritizing "data must show first".

Second-feedback snapshot:

| Status | Reports |
|---|---|
| Finished in code + backend-proven | Border Export Permit Amendment, Border Export Permit Voucher, Border Export Permit New Report, Export Licence By HS Code, Export Licence Voucher, Export Licence New Report, Border Import Licence Amendment, Border Import Licence Voucher, Border Import Licence New Report |
| Backend-proven, still needs UAT confirmation on visible PM items | Export Licence Actual Amendment, Border Export Permit Actual Amendment, Border Import Permit Amendment |
| Still left after second feedback | Deployed frontend/UAT confirmation for footer totals, title text, dropdown wording, and broad-range performance watch on Border Import Licence Voucher |

Priority 1 - done:

| Report | Result | Last check |
|---|---|---|
| Border Import Licence Amendment Report | API/controller retest passed | `totalCount=4`, `pageCount=4`, 1,618 ms API. |
| Border Import Licence Voucher Report | API/controller retest passed | `totalCount=1210`, `pageCount=20`, 4,394 ms API. |
| Border Import Licence New Report (New Report) | API/controller retest passed | `totalCount=824`, `pageCount=20`, 1,345 ms API. |
| Border Export Permit New Report (New Report) | API/controller retest passed | `totalCount=42`, `pageCount=20`, 1,060 ms API. |
| Export Licence By HS Code Report | DB + focused controller retest passed | DB fast page 1,192 ms; DB exact count 948 ms; focused xUnit controller test passed. |

Priority 2 - data-show cleanup:

| Report | Why still left | Next check |
|---|---|---|
| Export Licence Voucher Report | Backend/controller path is now proven; second-feedback visible items like footer total, exact dropdown behavior, and deployed header text still need UAT confirmation | Frontend/UAT retest after deploy. |
| Export Licence New Report (New Report) | Backend/controller path is now proven; second-feedback visible items like totals and deployed frontend display still need UAT confirmation | Frontend/UAT retest after deploy. |
| Border Import Licence New Report (New Report) | Data already shows; count optimization intentionally paused | No action unless frontend fails. |
| Border Import Licence Voucher Report | Backend/controller path is proven, but broad-range call was still slow | Keep on performance watch list; UAT retest after deploy. |

Priority 3 - no-data validation:

| Report | Why still left | Next check |
|---|---|---|
| Export Licence Actual Amendment Report | Source rows exist and live controller smoke returns rows; only deployed frontend/UAT confirmation is still open if PM still reports no data | Frontend/UAT retest if page still shows no data. |
| Border Import Permit Amendment Report | Source row exists and procedure returns row | Frontend/API retest if page still shows no data. |

### DB Deployment Status

Applied to `TradeNetDB`:

- `dbo.sp_ExportLicenceDetailReport_Pagination`
- `dbo.sp_BorderExportLicenceDetailReport_Pagination`
- `dbo.sp_ExportLicenceTotalValueReport_Fast_pagination`
- `dbo.sp_AmendReport_pagination`
- `dbo.sp_VoucherReport_pagination`
- `dbo.sp_NewReport_pagination`
- `dbo.sp_HSCodeReport_pagination`

Index status:

- Created and deployed `StoredProcedureMigrations/Indexes/ExportLicenceHSCodeReport_indexes.sql`.
- Created and deployed `StoredProcedureMigrations/Indexes/BorderExportLicenceDetailReport_indexes.sql`.
- New indexes:
  - `IX_ExportLicence_HSCodeReport_LicenceDate`
  - `IX_ExportLicenceItem_HSCodeReport_Licence`
  - `IX_AccountTransaction_ExportLicenceVoucher`
  - `IX_BorderExportLicence_Report_NewDetail`
  - `IX_BorderExportLicenceItem_Report_Licence`
  - `IX_IndividualTrading_Report_TINNo`
- Export Licence base date filter improved from timeout to 579 ms after the `ExportLicence` index.
- Existing index hints were used where helpful, especially `AccountTransaction` index `NonClusteredIndex-AccountTransaction-125044`.

### Count / Pagination Audit

Started: 2026-06-07 after senior pull `ca0688c`.

Reason: before doing more fixes, check whether total-count logic is wrapping or re-running expensive existing report queries and causing avoidable performance drops.

| Procedure | Branch / report | Count pattern found | Risk | Recommendation |
|---|---|---|---|---|
| `sp_ExportLicenceTotalValueReport_Fast_pagination` | Export Licence Total Value & Licences | Dedicated grouped query over filtered licence IDs only, then joins to `ExportLicenceItem` for currency totals. | Low | Done. One-day exact-count DB test returned in 747 ms and focused controller test passed. |
| `sp_HSCodeReport_pagination` | Export Licence By HS Code | Export Licence branch now supports `@IncludeTotalCount`; fast-page mode skips `COUNT(*) OVER()` and exact-count mode keeps the old result shape. | Low for one-day target / Medium on wide ranges | Done for Priority 1. Other HS Code branches were left unchanged because they were already working. |
| `sp_NewReport_pagination` | Border Import Licence New Report | Always computes `COUNT(*) OVER()` in this branch, even though procedure has `@IncludeTotalCount`. | Medium | Split this branch or make count conditional so `IncludeTotalCount = 0` avoids exact count work. Keep the current exact-count path for frontend pages that need total rows. |
| `sp_NewReport_pagination` | Export Licence New Report | Separate scalar count over base `ExportLicence` rows, then page query. | Medium on wide ranges | One-day target is fast. Wide exact-count timed out before; keep as candidate if business needs wide exact totals. |
| `sp_VoucherReport_pagination` | Border Import Licence Voucher Report | Separate scalar count over ID-only `UNION ALL` plus paged data query. | Medium but currently acceptable | Current one-day exact count is under target after adding `TransactionFormType` and index hint. Avoid wrapping full voucher projection for count. |
| `sp_VoucherReport_pagination` | Export Licence Voucher Report | Separate scalar count over base rows, then page query; item totals are resolved only after paging. | Medium on wide ranges | Good compared with wrapping full detail/item totals. One-day target is fast; wide exact-count remains risky. |
| `sp_AmendReport_pagination` | Border Import Licence Amendment Report | Separate scalar count over ID-only `UNION ALL` plus paged data query. | Medium but currently acceptable | Count does not include item lookups. One-day target is fast; broad range was 12 seconds but still under 30 seconds. |
| `sp_ActualAmendReport_pagination` | Actual Amendment branches | Mostly scalar base counts; border licence branches count over ID-only `UNION ALL`. | Low / Medium | OK for now. For no-data reports, first verify source rows before changing count logic. |

Audit conclusion:

- Do not wrap full existing report projections just to get `TotalCount`.
- Prefer one of these shapes:
  - exact count over base IDs only, then page and decorate rows;
  - grouped query with `COUNT(*) OVER()` only when the grouped result is small;
  - fast-page mode with `PageSize + 1` and no exact count when frontend does not need total rows.
- Highest-value count fix completed: Export Licence branch in `sp_HSCodeReport_pagination` can now skip exact count.
- Next count candidate is the Border Import Licence branch in `sp_NewReport_pagination`, because it computes `COUNT(*) OVER()` even when `@IncludeTotalCount = 0`.

## Git / Senior Update

Latest pull: 2026-06-07

Result:

- Pulled senior commit `ca0688c` / `Fix/import licence feedback fix (#1)`.
- Local Wai Phyo work was stashed first:
  - `stash@{0}: preserve Wai Phyo report work before senior pull`
- Pull fast-forwarded cleanly from `0e481e1` to `ca0688c`.
- Stash reapplied after pull.
- Overlapping files auto-merged without conflict markers:
  - `StoredProcedureMigrations/sp_NewReport_pagination.sql`
  - `StoredProcedureMigrations/sp_VoucherReport_pagination.sql`
- Conflict-marker scan passed:
  - no `<<<<<<<`
  - no `>>>>>>>`

Incoming senior commit touched many Import Licence / frontend pagination files. Continue to avoid Ko Htet-only tasks unless explicitly requested.

Status: senior update pulled successfully after preserving blocking local SQL files.

Reason: senior updated the Border Import Permit area and asked the team to test again. The local branch is behind `origin/main`, so the next step is to pull safely before more stored procedure changes.

Current branch status seen before pull:

```text
main...origin/main [behind 4]
Local SQL/app files have uncommitted changes.
```

Pull attempt result:

```text
git pull --ff-only
Blocked because these local files would be overwritten:
- StoredProcedureMigrations/sp_CancelReport_pagination.sql
- StoredProcedureMigrations/sp_ExtensionReport_pagination.sql
- StoredProcedureMigrations/sp_NewReport_pagination.sql
- StoredProcedureMigrations/sp_VoucherReport_pagination.sql
```

Resolution:

```text
git stash push -m "preserve local pagination SQL before senior pull" -- <4 blocking SQL files>
git pull --ff-only
Fast-forward succeeded to 0e481e1.
```

Preserved local SQL stash:

```text
stash@{0}: On main: preserve local pagination SQL before senior pull
```

Do not blindly apply this stash. Compare file by file and reapply only fixes that still belong to Wai Phyo work.

Incoming senior commits:

```text
0e481e1 Refactor pagination queries in stored procedures to remove redundant UNION ALL statements and optimize filtering conditions
0e164ed Add tests for Border Import Permit report endpoints to ensure error-free execution
29a508c Enhance report configurations and pagination performance by updating filter types, adding options, and optimizing SQL stored procedures with OPTION (RECOMPILE).
3d399c3 Refactor code structure for improved readability and maintainability
```

Senior update touched:

```text
Backend Border Import Permit controllers
Backend.Tests/BorderImportPermitEndpointTests.cs
Frontend report config/table behavior
StoredProcedureMigrations/sp_CancelReport_pagination.sql
StoredProcedureMigrations/sp_ExtensionReport_pagination.sql
StoredProcedureMigrations/sp_HSCodeReport_pagination.sql
StoredProcedureMigrations/sp_NewReport_pagination.sql
StoredProcedureMigrations/sp_VoucherReport_pagination.sql
```

Pull rule:

- Use a safe pull.
- Do not overwrite local changes.
- If pull is blocked by local changes, stop and decide whether to stash or merge manually.

## Active Wai Phyo Task Tracker

## Active Task Log - Border Import Licence Voucher Report Complaint

Started: 2026-06-17

Scope:

- Owner: Wai Phyo
- Procedure: `dbo.sp_VoucherReport_pagination`
- Frontend config: `Frontend/src/Report/config/reportConfigs.ts`
- Controller: `Backend/Controllers/Report/BorderImportLicenceVoucherReportController.cs`
- Old admin reference:
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderImportLicenceVoucherReport.cshtml`
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\VoucherReport.rdlc`
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`

Customer complaint:

- Sheet says `OK / No data`.

Old-vs-new parity findings before editing:

| Area | Old Tradenet 2.0 Admin | New report before fix | Diff |
|---|---|---|---|
| Apply Type filter | Dropdown from `CommonRepository.GetApplyTypeList()` excluding Fine; default behaves like first value / New flow. | Plain text filter with default empty string. | Wrong control and default could send empty ApplyType. |
| Payment Type filter | Dropdown from active payment types, with `--- All ---`. | Plain text filter with default empty string. | Wrong control; user can type values that do not match DB values. |
| Import Section filter | Border Import Licence uses Import Licence sections where `IsBorder == true`. | Number filter without lookup. | Wrong UX and missing section options. |
| Sakhan filter | Dropdown from Sakhan repository, with all option. | Number filter without lookup. | Wrong UX and missing Sakhan options. |
| Company Name | Readonly company name field populated from Company Registration No. | Missing from this report config. | Missing old filter box field. |
| Voucher header columns | RDLC uses `header2`/`header3`, resolved by controller to `Licence No` / `Licence Date` for New, and Amendment/Extension/Cancel/Actual Amendment labels for other apply types. | Table displayed literal `=Parameters!header2.Value` and `=Parameters!header3.Value`. | Visible wrong column titles. |
| Licence value / Total amount mapping | Old RDLC `Lic Value` uses licence/item value and `Total Amount` uses voucher amount. | `Lic Value` pointed to voucher `amount`; `Total Amount` pointed to item `totalAmount`. | Values were swapped. |

Fix applied:

1. Added `borderImportLicenceVoucherFilters`.
2. Switched Border Import Licence Voucher Report to:
   - border import licence section lookup;
   - dropdown Apply Type defaulting to `New`;
   - dropdown Payment Type;
   - readonly Company Name;
   - Sakhan lookup;
   - old-style voucher subtitle.
3. Enabled `resolveImportLicenceVoucherColumns` so `header2` and `header3` labels render as real column names.
4. Added `OriginalLicenceNo` with fallback to current `licenceNo`, matching the old RDLC first licence column behavior.
5. Corrected value mappings:
   - `LicValue` -> `totalAmount`
   - `Total Amount` -> `amount`

Verification:

- Frontend build attempted:
  - command: `npm run build`
  - result: blocked before checking this config because local `node_modules` does not contain `vitest`, while test files import it.
  - `npm ls vitest` shows the dependency is currently missing from the local install.
- Backend focused test attempted:
  - command: `dotnet test Backend.Tests/Backend.Tests.csproj --filter BorderImportLicenceVoucher --no-restore`
  - result: no matching test exists.
- Direct DB retest attempted:
  - procedure: `dbo.sp_VoucherReport_pagination`
  - form type: `Border Import Licence`
  - date: `2023-07-13`
  - apply type: `New`
  - result: command timed out while the remote SQL Server was still under noisy load.
  - follow-up `sys.dm_exec_requests` showed a long-running report select waiting on `ASYNC_NETWORK_IO`.

Current status:

- Frontend parity fix for the visible no-data/filter/header issue: done.
- Clean DB/API retest: still pending because the remote SQL session is noisy.
- Next action: retest frontend/API after dependencies are restored, then continue to Export Licence Voucher Report because it has the same blank-filter/data complaint.

## Active Task Log - Export Licence Voucher Report Complaint

Started: 2026-06-17

Scope:

- Owner: Wai Phyo
- Procedure: `dbo.sp_VoucherReport_pagination`
- Frontend config: `Frontend/src/Report/config/reportConfigs.ts`
- Controller: `Backend/Controllers/Report/ExportLicenceVoucherReportController.cs`
- Old admin reference:
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\ExportLicenceVoucherReport.cshtml`
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\VoucherReport.rdlc`
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`

Customer complaint:

- Blank Apply Type / Payment Type search did not work.
- Report title needed.
- Total Amount should show and sum.
- Commodity Type / Total Amount columns had missing data.
- Apply Type and Payment Type must be dropdowns.
- Export Licence section dropdown must show Export Licence sections only.
- RDLC parameter labels were visible as table column titles.
- Sakhan dropdown should not be shown.

Old-vs-new parity findings before editing:

| Area | Old Tradenet 2.0 Admin | New report before fix | Diff |
|---|---|---|---|
| Export Section filter | Dropdown using `GetAll(AppConfig.ExportLicence)` with `IsOversea == true`. | Number filter without lookup. | Wrong control/options. |
| Apply Type filter | Dropdown from common apply type list excluding Fine. | Plain text with empty default. | Wrong control/default. |
| Payment Type filter | Dropdown from active payment types with all option. | Plain text. | Wrong control/options. |
| Company Name | Readonly company name populated from registration number. | Missing from filter list. | Missing old filter field. |
| Sakhan | Not present in old Export Licence voucher report. | Present as number filter. | Extra wrong filter. |
| Voucher dynamic headers | Old controller resolves `header2`/`header3` based on Apply Type. | Literal `=Parameters!header2.Value` and `=Parameters!header3.Value` could show. | Wrong visible column titles. |
| Licence value / Total amount mapping | Old RDLC `Lic Value` uses licence/item value and `Total Amount` uses voucher amount. | `Lic Value` pointed to voucher `amount`; `Total Amount` pointed to item `totalAmount`. | Values were swapped. |

Fix applied:

1. Added `exportLicenceVoucherFilters`.
2. Switched Export Licence Voucher Report to:
   - Export Licence section lookup;
   - dropdown Apply Type defaulting to `New`;
   - dropdown Payment Type;
   - Company Registration No + readonly Company Name;
   - no Sakhan filter.
3. Enabled `resolveImportLicenceVoucherColumns` for old RDLC `header2` / `header3` behavior.
4. Added old-style report subtitle:
   - `Export Licence Voucher List (From) To (To)`
5. Corrected value mappings:
   - `LicValue` -> `totalAmount`
   - `Total Amount` -> `amount`
6. Added footer placement config for voucher `Amount` total:
   - `currencyTotalsColumns: { labelColumnKey: 'LicenceNo', valueColumnKey: 'Amount' }`

Verification:

- Frontend build is still blocked by missing local `vitest` dependency before this config can be fully typechecked.
- DB/API retest not run cleanly yet because the previous remote SQL direct voucher test timed out and left a noisy session.

Current status:

- Frontend parity fix for filters/header/value mapping: done.
- Clean DB/API retest: pending.
- Next action: once local dependencies/remote DB are usable, retest `ExportLicenceVoucherReport` with default `ApplyType=New`, blank Payment Type, and blank Company Registration No.

## Active Task Log - Border Export Permit Voucher Report Complaint

Started: 2026-06-17

Scope:

- Owner: Wai Phyo
- Procedure: `dbo.sp_VoucherReport_pagination`
- Frontend config: `Frontend/src/Report/config/reportConfigs.ts`
- Controller: `Backend/Controllers/Report/BorderExportPermitVoucherReportController.cs`
- Old admin reference:
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderExportPermitVoucherReport.cshtml`
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\BorderVoucherReport.rdlc`
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`

Customer complaint:

- Report title needed.
- Total Amount sum needed.
- `=Parameters!header2.Value` and `=Parameters!header3.Value` were visible in column names.
- Export Permit section dropdown must show only Export Permit sections.

Old-vs-new parity findings before editing:

| Area | Old Tradenet 2.0 Admin | New report before fix | Diff |
|---|---|---|---|
| Export Section filter | Border Export Permit uses Export Permit sections where `IsBorder == true`. | Already used `borderExportPermitSections`. | OK. |
| Sakhan filter | Dropdown from Sakhan repository. | Number filter with no lookup. | Wrong UX/options. |
| Company Name | Readonly company name populated from registration number. | Missing. | Missing old filter field. |
| Voucher dynamic headers | Old controller resolves `header2`/`header3` based on Apply Type. | Config already had concrete `Licence No` / `Licence Date` and resolver enabled. | No code needed for header issue in current config. |
| Total Amount mapping | Old voucher footer sums voucher `Amount`. | Total Amount column and footer placement pointed at `totalAmount` item/licence value. | Wrong value for voucher Total Amount. |

Fix applied:

1. Added readonly Company Name filter.
2. Added Sakhan lookup to the Sakhan filter.
3. Switched Total Amount column to voucher `amount`.
4. Switched voucher footer placement to sum `Amount`.

Verification:

- Old RDLC `BorderVoucherReport.rdlc` confirms the footer label `TOTAL` and value `SUM(Fields!Amount.Value)`.
- New config already has `currencyTotalsColumns: { labelColumnKey: 'LicenceNo', valueColumnKey: 'Amount' }`, and the controller already calls `ExportPermitListingCurrencyTotals.ExecuteVoucherAsync(...)`.
- DB test before the SQL date fix:
  - Broad range `2023-01-01` to `2026-06-03` returned rows, but one-day `2023-08-23` returned no page rows while the footer totals found 2 rows.
  - Root cause: the Border Export Permit branch still used `PaymentDate <= @ToDate`, so frontend date-only searches could miss rows after midnight.
- SQL fix applied and deployed to `TradeNetDB`:
  - `StoredProcedureMigrations/sp_VoucherReport_pagination.sql`
  - `StoredProcedureMigrations/sp_ExportPermitVoucherCurrencyTotals.sql`
  - Border Export Permit voucher date predicates now use:
    - `PaymentDate >= @FromDate`
    - `PaymentDate < DATEADD(day, 1, @ToDate)`
- DB retest after deploy:
  - One-day page query, `2023-08-23` to `2023-08-23`, `ApplyType = N'New'`: returned 2 rows, `TotalCount = 2`, elapsed about 106 ms.
  - One-day footer totals for same parameters: returned `USD`, `NoOfLicences = 2`, `TotalValue = 30584.4000`, elapsed about 40 ms.
  - Broad page query, `2023-01-01` to `2026-06-03`, `ApplyType = N'New'`: returned rows, `TotalCount = 42`, elapsed about 107 ms.
  - Broad footer totals for same parameters: returned JPY/THB/USD/CNY totals, elapsed about 45 ms.

Current status:

- Frontend parity fix for filters and total amount mapping: done.
- DB procedures deployed and tested: done.
- Clean browser/API retest through the frontend: pending only because local frontend build/test dependencies are still not fully healthy.

## Active Task Log - Border Import Licence Actual / Extension / Cancellation Complaint

Started: 2026-06-17

Scope:

- Owner: Wai Phyo
- Procedures:
  - `dbo.sp_ActualAmendReport_pagination`
  - `dbo.sp_ExtensionReport_pagination`
  - `dbo.sp_CancelReport_pagination`
- Frontend config: `Frontend/src/Report/config/reportConfigs.ts`
- Old admin reference:
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderImportLicenceAmendReport.cshtml`
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderImportLicenceExtensionReport.cshtml`
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderImportLicenceCancelReport.cshtml`
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`

Customer complaint:

- Report title needed.
- Total No of License needed.
- Currency-wise Total Value needed.
- Import Section dropdown must use Import Licence setup values.

Old-vs-new parity findings before editing:

| Area | Old Tradenet 2.0 Admin | New report before fix | Diff |
|---|---|---|---|
| Sakhan filter | Dropdown from Sakhan repository with all option. | Number filter without lookup. | Wrong UX/options. |
| Import Section filter | Border Import Licence uses Import Licence sections where `IsBorder == true`. | Number filter without lookup. | Wrong UX/options. |
| Company Name | Readonly company name populated from Company Registration No. | Missing from these action-report configs. | Missing old filter field. |
| Currency totals | Extension had footer config; Actual Amendment and Cancellation were missing it. | Inconsistent footer config. | Missing currency-wise footer support for two reports. |
| Report subtitle/title | Old RDLC uses a report title/subtitle line. | Extension had subtitle; Actual Amendment and Cancellation were missing subtitle. | Missing title/subtitle support. |

Fix applied:

1. Added shared `borderImportLicenceActionFilters`.
2. Added shared `borderImportLicenceAmendActionFilters`.
3. Updated Border Import Licence Actual Amendment, Amendment, Cancellation, and Extension to use:
   - Sakhan lookup;
   - Border Import Licence section lookup;
   - Company Registration No;
   - readonly Company Name.
4. Added `currencyTotalsColumns` to Actual Amendment, Amendment, and Cancellation.
5. Added old-style report subtitle to Actual Amendment, Amendment, and Cancellation.

Verification:

- Frontend build is still blocked by missing local `vitest`.
- These are frontend config parity fixes using existing backend fields and existing totals framework.

Current status:

- Frontend filter/title/footer parity pass: done.
- Clean frontend/API retest: pending.

## Active Task Log - Border Export Licence Detail Dedicated Procedure

Started: 2026-06-12

Scope:

- Owner: Wai Phyo
- Original mixed procedure left untouched: `dbo.sp_ExportLicenceDetailReport_Pagination`
- New dedicated procedure: `dbo.sp_BorderExportLicenceDetailReport_Pagination`
- New SQL file: `StoredProcedureMigrations/sp_BorderExportLicenceDetailReport_pagination.sql`
- New index file: `StoredProcedureMigrations/Indexes/BorderExportLicenceDetailReport_indexes.sql`
- Backend route switch: `Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReport_Fast.StoredProcedure.cs`

What was changed:

1. Added a dedicated Border-only stored procedure so Border Export Licence detail work no longer requires editing the mixed `Oversea/Border` procedure.
2. Updated the C# wrapper so `request.Type == "Border"` uses the new dedicated procedure name.
3. Updated the Border API paging path so Border requests now go through the stored-procedure paging branch instead of the old LINQ paging branch.
4. Moved Port of Export / Destination Country display resolution out of SQL and back into cached C# lookup resolution for the Border stored-procedure path.
5. Added a dedicated Border Export Licence detail index script.
6. Deployed the new procedure and the narrowed index script to `TradeNetDB`.

Current DB observations:

- The procedure returns real data for Border Export Licence detail.
- Clean timing comparison is currently unreliable because the SQL Server is under memory pressure.
- During testing, `sys.dm_exec_requests` showed:
  - many old sessions stuck in `KILLED/ROLLBACK`
  - current report sessions waiting on `RESOURCE_SEMAPHORE`
- Because of that, one-day tests that were previously around ~10-13 seconds in earlier checks later jumped to ~50-65 seconds while the server was under pressure.

Tests already run:

1. Original mixed procedure before the dedicated split:
   - Procedure: `dbo.sp_ExportLicenceDetailReport_Pagination`
   - Parameters:
     - `@Type = N'Border'`
     - `@FromDate = '2026-01-02'`
     - `@ToDate = '2026-01-02'`
     - `@PageIndex = 0`
     - `@PageSize = 20`
   - Result:
     - returned real rows
     - `IncludeTotalCount = 1`: about `65.03s`
   - Earlier cleaner measurements from the same Border path before the DB got worse:
     - one day + count: about `13.18s`
     - one day fast page: about `10.17s`
     - one month + count: about `34.52s`
     - one month fast page: about `26.24s`
     - wide multi-year range: timed out / did not finish in acceptable time

2. New dedicated Border procedure after split:
   - Procedure: `dbo.sp_BorderExportLicenceDetailReport_Pagination`
   - Parameters:
     - `@FromDate = '2026-01-02'`
     - `@ToDate = '2026-01-02'`
     - `@PageIndex = 0`
     - `@PageSize = 20`
   - Result:
     - returned real rows
     - `IncludeTotalCount = 1`: about `60.26s` on one run
     - `IncludeTotalCount = 0`: about `57.80s` on one run
   - Important note:
     - these are not clean benchmark numbers because the server was under `RESOURCE_SEMAPHORE` pressure and had many old `KILLED/ROLLBACK` sessions still present

3. Code-path retest after switching Border API paging to the dedicated stored procedure:
   - Entry point:
     - `sp_ExportLicenceDetailReport_Fast.CreatePagedResultAsync(...)`
   - Behavior:
     - Border requests now route to `CreateStoredProcedurePagedResultAsync(...)`
   - Focused real-db smoke test:
     - `Backend.Tests/BorderExportLicenceDetailReportStoredProcedureSmokeTests.cs`
   - Test date:
     - `2026-01-02`
   - Result:
     - test hit the dedicated stored-procedure code path
     - test failed at about `31s` with SQL execution timeout
   - Meaning:
     - code routing is active
     - DB execution is still the blocking factor

4. Latest SQL simplification retest:
   - Removed SQL-side XML string building for:
     - Port of Export
     - Destination Country
   - These values are now resolved from cached lookup data in C#.
   - One-day fast-page re-test:
     - elapsed before failure: about `26.09s`
     - still failed with `Msg 8645 ... timeout occurred while waiting for memory resources`

5. Failed or blocked test attempts during rewrite:
   - one rewrite shape failed with:
     - `Ambiguous column name 'BorderExportLicenceId'`
   - one rewrite shape failed with:
     - `Invalid column name 'SortBorderExportLicenceId'`
   - several runs failed with:
     - `Msg 8645 ... timeout occurred while waiting for memory resources`
   - one broad combined timing run hit the local command timeout at 10 minutes

6. Latest successful retest after result-shape fix and memory-grant cap:
   - SQL file redeployed:
     - `StoredProcedureMigrations/sp_BorderExportLicenceDetailReport_pagination.sql`
   - Backend wrapper update kept:
     - `Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReport_Fast.StoredProcedure.cs`
   - Added back lightweight placeholder columns required by EF mapping:
     - `PortofExport`
     - `DestinationCountry`
   - Added `MAX_GRANT_PERCENT = 5` on the dedicated Border procedure query options.
   - Direct DB test:
     - `@FromDate = '2026-01-02'`
     - `@ToDate = '2026-01-02'`
     - `@PageIndex = 0`
     - `@PageSize = 20`
     - `@IncludeTotalCount = 0`
     - elapsed: `25.99s`
     - returned rows successfully
   - Real code-path smoke test:
     - `dotnet test Backend.Tests/Backend.Tests.csproj --filter BorderExportLicenceDetailReportStoredProcedureSmokeTests --no-restore`
     - result: passed
     - duration: `38s` test time
   - Meaning:
     - the dedicated Border stored procedure now returns data through the actual backend paging path
     - data-show is fixed for this dedicated path
     - one-day timing is still above the preferred target and still affected by SQL Server pressure

Interpretation of current tests:

- Data path: confirmed working
- Original Border path: confirmed slow
- Dedicated Border path: structurally wired and deployed
- Border code path now uses the dedicated stored procedure
- Latest one-day direct DB retest returned in about `25.99s`
- Latest real backend code-path smoke passed after the memory-grant cap was added
- Current retests are still being distorted by DB memory pressure and old rollback backlog
- Because the server state is noisy right now, we still cannot honestly call this a clean final performance benchmark

Current status:

- Structural split: done
- Wrapper routing: done
- Border API stored-procedure routing: done
- DB deployment: done
- Data-show through backend code path: done
- Reliable performance validation: still pending until DB pressure clears

Next action:

1. Re-test the dedicated Border procedure after the `KILLED/ROLLBACK` backlog clears.
2. Capture clean one-day / one-month / multi-year timings.
3. Continue tuning only `dbo.sp_BorderExportLicenceDetailReport_Pagination` until one-day runs are comfortably under the target.

## Customer Complaint Intake - Export Licence - 2026-06-11

Source:

- User request: apply the same customer-complaint parity pass to Export Licence.
- Old admin reference: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin`.
- Old RDLC/header sources checked:
  - `ReportControl\AmendReport.rdlc`
  - `ReportControl\ExtensionReport.rdlc`
  - `ReportControl\CancelReport.rdlc`
  - `ReportControl\HSCodeReport.rdlc`
  - `ReportControl\VoucherReport.rdlc`
  - `ReportControl\NewLicenceReport.rdlc`
  - `ReportControl\ExportLicenceByTotalValueLicenceReport.rdlc`
- Old filter sources checked:
  - `Views\Reports\ExportLicenceAmendReport.cshtml`
  - `Views\Reports\ExportLicenceExtensionReport.cshtml`
  - `Views\Reports\ExportLicenceCancelReport.cshtml`
  - `Views\Reports\ExportLicenceByHSCodeReport.cshtml`
  - `Views\Reports\ExportLicenceVoucherReport.cshtml`
  - `Views\Reports\ExportLicenceNewReport.cshtml`
  - `Views\Reports\ExportLicenceByTotalValueLicenceReport.cshtml`
  - matching actions in `Controllers\ReportsController.cs`.

Old admin filter rule confirmed:

- Export Licence section dropdowns use `ExportImportSectionRepository.GetAll(AppConfig.ExportLicence).Where(x => x.IsActive == true && x.IsOversea == true)`.
- New report configs should use a dedicated oversea Export Licence section lookup, not generic section lists and not border section lists.

Parity diffs found before editing:

| Report | Old filters / columns | New diff found | Planned fix |
|---|---|---|---|
| Export Licence Amendment Report | Filters: From/To, Export Section, Company Registration No, readonly Company Name, Remark. RDLC columns: No, Section, Licence No, Licence Amendment No, Amendment Date, Company Registration No, Company Name, Company Address, Curency, Total Value. | New has extra hidden-ish `FormType` and `SakhanId` request fields, and extra visible `hsCode` column that old RDLC does not have. Export Section is numeric without the old oversea Export Licence lookup. | Keep request fields needed by backend; remove visible `hsCode`; add report subtitle and `exportLicenceSections` lookup. |
| Export Licence Actual Amendment Report | Uses the same filter form and `AmendReport.rdlc` as Amendment, title changes to Actual Amendment. | Same diffs as Amendment: extra visible `hsCode`, section lookup missing. | Same fix as Amendment. |
| Export Licence Extension Report | Filters: From/To, Export Section, Company Registration No, readonly Company Name. RDLC columns include Extension No, Extension Last Date, Curency, Total Value. | Section lookup missing. | Add `exportLicenceSections` lookup; existing title/subtitle/currency total setup is kept. |
| Export Licence Cancellation Report | Filters: From/To, Export Section, Company Registration No, readonly Company Name. RDLC columns include Cancellation No, Cancellation Date, Curency, Total Value, Remark. | New has extra visible `hsCode`; section lookup missing. | Remove visible `hsCode`; add subtitle, currency total wiring, and `exportLicenceSections` lookup. |
| Export Licence By HS Code Report | Filters: From/To, Export Section, Filter By Start/End, HS Code. RDLC columns: Sr.No, HS Code, Description, No of Licences, Total Value, Currency. | New is missing Export Section, `FilterType` is plain text default empty, extra visible Company Name column, no subtitle. | Add Export Section with `exportLicenceSections`, convert Filter By to Start/End select, remove Company Name, keep HSCode detail route as later work. |
| Export Licence Voucher Report | Filters: From/To, Export Section, Apply Type, Payment Type, Company Registration No, readonly Company Name. RDLC columns: No, dynamic Licence/Amend/Extension/Cancel/Actual number, Application No, dynamic date, Company Registration No, Company Name, Lic Value, Currency, Voucher No, Voucher Date, Approved User, Total Amount. | New shows raw `=Parameters!header2.Value` and `=Parameters!header3.Value`; section lookup missing. | Add subtitle, dynamic voucher header resolver, and `exportLicenceSections` lookup. |
| Export Licence New Report | Filters: From/To, Export Section, Company Registration No, readonly Company Name. RDLC is `NewLicenceReport.rdlc`. | New has extra Auto filter/column; section lookup missing. Quota exists in new config but old source must be checked in RDLC before removing because user previously complained about quota. | Add subtitle and `exportLicenceSections`; remove Auto filter/column if not in old RDLC. Leave quota until RDLC field check is completed. |
| Export Licence Total Value & Licences Report | Filters: From/To, PaThaKa Type, Export Section. RDLC is `ExportLicenceByTotalValueLicenceReport.rdlc`. | Section lookup missing; other filters are shared with generic import licence helpers. | Add `exportLicenceSections` lookup and subtitle if missing; no DB change unless frontend still fails. |

Implementation applied:

- Added `exportLicenceSections` lookup in `Backend/Controllers/ReportLookupsController.cs`.
- Wired the lookup into these Wai Phyo Export Licence reports:
  - Export Licence Actual Amendment Report
  - Export Licence Amendment Report
  - Export Licence Extension Report
  - Export Licence Cancellation Report
  - Export Licence By HS Code Report
  - Export Licence New Report (New Report)
  - Export Licence Total Value & Licences Report
  - Export Licence Voucher Report
- Export Licence By HS Code:
  - Added Export Section filter.
  - Changed Filter By from text to Start/End dropdown.
  - Removed Company Name column because `HSCodeReport.rdlc` does not include it.
  - Updated `ExportLicenceByHSCodeReportController` so `ExportImportSectionId` is passed to `sp_HSCodeReportRequest`.
- Export Licence Amendment / Actual Amendment:
  - Removed extra visible `hsCode` column because `AmendReport.rdlc` does not include it.
- Export Licence Cancellation:
  - Removed extra visible `hsCode` column because `CancelReport.rdlc` does not include it.
  - Added currency total footer wiring to match the RDLC total behavior already used by similar reports.
- Export Licence Voucher:
  - Added old RDLC-style subtitle.
  - Added dynamic voucher header resolver so `=Parameters!header2.Value` / `=Parameters!header3.Value` no longer render as literal column names.
  - Removed extra Application Date and Commodity Type columns because `VoucherReport.rdlc` does not include them.
  - Converted Payment Type and Apply Type to dropdowns.
- Export Licence New Report:
  - Added old RDLC-style subtitle.
  - Removed Auto filter/column.
  - Removed Commodity Type, HSCode, and Quota columns because `NewLicenceReport.rdlc` does not include them. This fixes the quota `N/A` complaint by restoring old-report column parity instead of inventing a DB value.
- Export Licence Total Value & Licences:
  - Added old RDLC-style subtitle.
  - Added `exportLicenceSections` lookup.

Verification:

- Backend build: passed with existing nullable/migration warnings only.
- Targeted config scan passed:
  - all eight touched Export Licence reports have exactly one `exportLicenceSections` lookup;
  - no raw `=Parameters!...` voucher headers remain in the touched Export Licence voucher config;
  - extra HSCode / Auto / Quota / Commodity columns are absent from the touched reports where old RDLC files do not include them.
- Frontend build was not rerun because local `Frontend/node_modules` is still missing `vitest` from the earlier dependency issue.
- DB stored procedure changes: none for this pass. These fixes are frontend/API parity fixes; current deployed procedures were not changed.

## Customer Complaint Intake - Ma Nge Feedback - 2026-06-10

Source:

- Google Sheet shared by user: `1paKyNjI_5bKekx9a7XZl3cNnfcrPk26NxjkkZ3sczSo`
- Pasted feedback column: Ma Nge / customer complaint notes.

Rules for this pass:

- Do not touch Ko Htet-only rows.
- For each Wai Phyo report, first compare:
  - old admin RDLC columns from `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\*.rdlc`
  - old admin filter/page code under `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin`
  - current frontend config in `Frontend/src/Report/config/reportConfigs.ts`
  - Import Permit reference implementation where requested by the user.
- Report filter/column diffs before code changes.
- Then fix data-show issues first, followed by frontend mapping/titles/totals, then performance.

Active Wai Phyo Border Export Permit complaints:

| Priority | Report | Procedure | Complaint / requested fix | First check before code changes |
|---:|---|---|---|---|
| 1 | Border Export Permit Amendment Report | `dbo.sp_AmendReport` | Report title; live data not showing; Export Section dropdown should show only Export Permit sections. | Compare old `AmendReport` / border amend filters; test live-data source rows and procedure output. |
| 2 | Border Export Permit Actual Amendment Report | `dbo.sp_ActualAmendReport` | Live data not showing. | Verify source rows for approved actual amendment; compare old actual amend report. |
| 3 | Border Export Permit New Report (New Report) | `dbo.sp_NewReport` | Report title; total licence count; currency-wise total value; Export Permit section dropdown; Sakhan-specific search returns no data; remove Auto filter. | Compare old new report and Import Permit New reference; test Sakhan filter in DB. |
| 4 | Border Export Permit Voucher Report | `dbo.sp_VoucherReport` | Report title; sum total amount; bad `=Parameters!header2/header3` column names; section dropdown should be Export Permit section. | Compare old voucher RDLC headers and Import Permit voucher reference. |
| 5 | Border Export Permit Extension Report | `dbo.sp_ExtensionReport` | Report title; total licence count; currency-wise total value; Export Permit-only section dropdown. | Compare old extension RDLC/footer and Import Permit extension reference. |
| 6 | Border Export Permit Cancellation Report | `dbo.sp_CancelReport` | Report title; total licence count; currency-wise total value; Export Permit-only section dropdown. | Compare old cancel RDLC/footer and Import Permit cancellation reference. |
| 7 | Border Export Permit By HS Code Report | `dbo.sp_HSCodeReport` | Report title; total licence count; remove Company Name column; section dropdown note; HSCode drilldown to HS Code Detail; Start/End filter dropdown. | Compare old HSCode report/detail report and current HS Code report config. |

Excluded from this Wai Phyo pass:

| Report | Owner in feedback | Reason excluded |
|---|---|---|
| Border Export Permit Daily Report (New Permit Report) | Ko Htet | User said only Wai Phyo task part after meeting. |
| Border Export Permit Detail Report | Ko Htet | User said only Wai Phyo task part after meeting. |
| Border Export Permit By Section Report | Ko Htet | User said only Wai Phyo task part after meeting. |
| Border Export Permit By Seller Country Report | Ko Htet | User said only Wai Phyo task part after meeting. |
| Border Export Permit Company List Report | Ko Htet | User said only Wai Phyo task part after meeting. |

Next concrete target:

- Start with `Border Export Permit Amendment Report`, because it is Wai Phyo-owned and has a data-show complaint on live.
- Old-admin/current diff is recorded below. First implementation pass is now in progress.

### Parity Diff - Border Export Permit Amendment Report - 2026-06-10

Old admin references checked:

- Controller/action: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`, `BorderExportPermitAmendReport`
- View/filter form: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderExportPermitAmendReport.cshtml`
- RDLC: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\BorderAmendReport.rdlc`

Old filter box:

| Filter | Old admin behavior | New config status | Diff |
|---|---|---|---|
| From Date / To Date | Required text/date fields | Present | OK |
| Sakhan | Dropdown, all sakhans, `--- All ---` | Present as numeric lookup fallback | OK if lookup renders as dropdown |
| Export Section | Dropdown from `GetAll(AppConfig.ExportPermit).Where(IsActive && IsBorder)` | Present but no explicit `lookupName` in this report | Diff: must use Border Export Permit / Export Permit border sections only. |
| Company Registration No | Textbox with company lookup JS | Present | OK |
| Company Name | Readonly textbox auto-filled by company registration no | Not present as visible filter | Diff from old UI, but may be acceptable if new UI intentionally omits readonly display. |
| Remark | Dropdown from active amend remarks | Present as numeric lookup fallback | OK if lookup renders as dropdown |

Old RDLC table/header:

| Old column | New status |
|---|---|
| No. | Present as row number |
| Sakhan | Present |
| Section | Present |
| Licence No | Present |
| Licence Amendment No | Present |
| Amendment Date | Present |
| Company Registration No | Present |
| Company Name | Present |
| Company Address | Present |
| Curency | Present with same misspelling |
| Total Value | Present |

Diffs found before code changes:

- New report has extra `HSCode` column; old `BorderAmendReport.rdlc` does not.
- Old RDLC has footer totals:
  - currency-wise licence count: `Currency: N licence(s)`
  - currency-wise amount total
  - grand total licence count: `Total: N licence(s)`
- New frontend config for this report currently does not explicitly request `borderExportPermitSections`; it falls back to generic `exportImportSections`.
- Old report title parameter is `List of Border Export Permit Report ({FromDate}) To ({ToDate})`; new page title is only static `Border Export Permit Amendment Report` unless generic report header lines are added.
- Customer complaint says live data does not show; this needs DB/source-row verification after this parity diff.

Planned fixes after diff:

1. Fix Export Section lookup to use Border Export Permit / Export Permit border sections only. Done in code; backend build passed.
2. Verify live-data source rows and `dbo.sp_AmendReport_pagination` output for `FormType='Border Export Permit'`. Done; DB has 2 approved amend source rows and the procedure returned 2 rows.
3. Remove or hide the extra `HSCode` column unless user decides to keep the new column despite old RDLC mismatch. Done in code.
4. Add report title/header behavior if the generic report page supports report header lines for this report. Done in code with old RDLC-style date subtitle.
5. Add/verify currency totals and total licence count if this report should match the RDLC footer. Pending, because the current backend amendment response does not yet provide `currencyTotals`.

Implementation applied:

- Added backend lookup endpoint key `borderExportPermitSections` in `Backend/Controllers/ReportLookupsController.cs`.
- The new lookup filters `ExportImportSection` by active, not deleted, `Type = 'Export Permit'`, and `IsBorder = 1`, matching the old admin `GetAll(AppConfig.ExportPermit).Where(IsActive && IsBorder)` behavior.
- Updated `BorderExportPermitAmendmentReport` in `Frontend/src/Report/config/reportConfigs.ts` to use `lookupName: 'borderExportPermitSections'`.
- Added `reportSubtitle: importLicenceRangeSubtitle('List of Border Export Permit Report')` to match the old RDLC title parameter style.
- Removed the extra `hsCode` column from this report because `BorderAmendReport.rdlc` does not include HS Code.

Verification:

- Backend build: passed with existing nullable/migration warnings only.
- DB source-row check on `TradeNetDB`: `BorderExportPermit` has 2 approved amend rows in the tested range (`2026-05-15` to `2026-05-22`).
- DB procedure check: `dbo.sp_AmendReport_pagination` with `FormType = 'Border Export Permit'`, date range `2023-01-01` to `2026-06-30`, page size 5 returned both rows in 246 ms.
- Frontend build: blocked before validating this change because local `Frontend/node_modules` is missing `vitest`; `npm run build` fails at `reportConfigs.importPermit.test.ts` with `Cannot find module 'vitest'`.
- Attempted `npm install` to repair dependencies, but it exceeded 2 minutes and did not install `vitest`; no package-lock change was produced.

### Parity Diff - Border Export Permit Actual Amendment Report - 2026-06-10

Old admin references checked:

- Controller/action: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`, `BorderExportPermitActualAmendReport`
- View/filter form: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderExportPermitAmendReport.cshtml`
- RDLC: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\BorderAmendReport.rdlc`

Old filter box:

| Filter | Old admin behavior | New config status | Diff |
|---|---|---|---|
| From Date / To Date | Required date fields | Present | OK |
| Sakhan | Dropdown, all sakhans, `--- All ---` | Present as numeric lookup fallback | OK if lookup renders as dropdown |
| Export Section | Dropdown from `GetAll(AppConfig.ExportPermit).Where(IsActive && IsBorder)` | Present but no explicit `lookupName` in this report | Diff: must use Border Export Permit / Export Permit border sections only. |
| Company Registration No | Textbox with company lookup JS | Present | OK |
| Company Name | Readonly textbox auto-filled by company registration no | Not present as visible filter | Diff from old UI, same as amendment report. |
| Remark | Dropdown from active amend remarks | Present as numeric lookup fallback | OK if lookup renders as dropdown |

Old RDLC table/header:

| Old column | New status |
|---|---|
| No. | Present as row number |
| Sakhan | Present |
| Section | Present |
| Licence No | Present |
| Licence Amendment No | Present |
| Amendment Date | Present |
| Company Registration No | Present |
| Company Name | Present |
| Company Address | Present |
| Curency | Present with same misspelling |
| Total Value | Present |

Diffs found before code changes:

- New report has extra `HSCode` column; old `BorderAmendReport.rdlc` does not.
- New frontend config falls back to generic `exportImportSections`; old admin uses Export Permit border sections only.
- Old report title parameter is `List of Border Export Permit Report ({FromDate}) To ({ToDate})`.
- Customer complaint says live data does not show; DB validation found this is true because there are no approved actual-amend source rows.

DB verification:

- Source check on `TradeNetDB`: `BorderExportPermit` has 0 rows where `ApplyType = 'Actual Amend'` and `Status = 'Approved'`.
- Procedure check: `dbo.sp_ActualAmendReport_pagination` with `FormType = 'Border Export Permit'`, date range `2023-01-01` to `2026-06-30`, page size 5 returned 0 rows in 60 ms.

Planned fixes after diff:

1. Use the same `borderExportPermitSections` lookup as the amendment report. Done in code.
2. Add the old RDLC-style date subtitle. Done in code.
3. Remove the extra `HSCode` column. Done in code.
4. No SQL data-show fix is required unless the customer expects rows that are missing from source data.

Implementation applied:

- Updated `BorderExportPermitActualAmendmentReport` in `Frontend/src/Report/config/reportConfigs.ts` to use `lookupName: 'borderExportPermitSections'`.
- Added `reportSubtitle: importLicenceRangeSubtitle('List of Border Export Permit Report')`.
- Removed the extra `hsCode` column to match `BorderAmendReport.rdlc`.

### Parity Diff - Border Export Permit New Report (New Report) - 2026-06-10

Old admin references checked:

- Controller/action: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`, `BorderExportPermitNewReport`
- View/filter form: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderExportPermitNewReport.cshtml`
- RDLC: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\BorderNewReport.rdlc`

Old filter box:

| Filter | Old admin behavior | New config status | Diff |
|---|---|---|---|
| From Date / To Date | Required date fields | Present | OK |
| Sakhan | Dropdown, all sakhans, `--- All ---` | Present as numeric lookup fallback | OK if lookup renders as dropdown |
| Export Section | Dropdown from `GetAll(AppConfig.ExportPermit).Where(IsActive && IsBorder)` | Present but no explicit `lookupName` in this report | Diff: must use Border Export Permit / Export Permit border sections only. |
| Company Registration No | Textbox with company lookup JS | Present | OK |
| Company Name | Readonly textbox auto-filled by company registration no | Not present as visible filter | Diff from old UI. |
| Auto | Not present | Present as frontend filter | Diff: remove Auto filter per complaint and old UI. |

Old RDLC table/header:

| Old column | New status |
|---|---|
| No. | Present as row number |
| Sakhan | Present |
| Section | Present |
| Licence No | Present |
| Company Registration No | Present |
| Company Name | Present |
| Company Address | Present |
| Curency | Present with same misspelling |
| Total Value | Present |

Diffs found before code changes:

- New frontend config falls back to generic `exportImportSections`; old admin uses Export Permit border sections only.
- New frontend has an `Auto` filter; old UI does not and customer explicitly requested removing Auto filter.
- New frontend has an `Auto` column; old `BorderNewReport.rdlc` does not show Auto.
- Old report title parameter is `List of Border Export Permit Report ({FromDate}) To ({ToDate})`.
- Old RDLC has footer totals:
  - currency-wise licence count: `Currency: N licence(s)`
  - currency-wise amount total
  - grand total licence count: `Total: N licence(s)`

DB verification:

- Source check on `TradeNetDB`: approved New rows exist for Sakhan values 1, 3, 4, 5, 15, and 24 in the tested range.
- Procedure check: `dbo.sp_NewReport_pagination` with `FormType = 'Border Export Permit'`, date range `2023-01-01` to `2026-06-30`, all Sakhan returned `TotalCount = 42` in 136 ms.
- Procedure check: same request with `SakhanId = 5` returned `TotalCount = 29` in 69 ms.
- Current DB does not reproduce the complaint that Sakhan-specific search returns no data.

Planned fixes after diff:

1. Use `borderExportPermitSections` lookup. Done in code.
2. Add old RDLC-style date subtitle. Done in code.
3. Remove Auto filter. Done in code.
4. Remove Auto column to match `BorderNewReport.rdlc`. Done in code.
5. Footer totals are still pending because the current controller returns only paged rows and total count, not currency grouped totals.

Implementation applied:

- Updated `BorderExportPermitNewReportNewReport` in `Frontend/src/Report/config/reportConfigs.ts` to use `lookupName: 'borderExportPermitSections'`.
- Added `reportSubtitle: importLicenceRangeSubtitle('List of Border Export Permit Report')`.
- Removed the `Auto` filter per customer complaint and old UI parity.
- Removed the `Auto` column because `BorderNewReport.rdlc` does not include it.

### Parity Diff - Border Export Permit Voucher Report - 2026-06-10

Old admin references checked:

- Controller/action: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`, `BorderExportPermitVoucherReport`
- View/filter form: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderExportPermitVoucherReport.cshtml`
- RDLC: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\BorderVoucherReport.rdlc`

Old filter box:

| Filter | Old admin behavior | New config status | Diff |
|---|---|---|---|
| From Date / To Date | Required date fields | Present | OK |
| Sakhan | Dropdown, all sakhans, `--- All ---` | Present as numeric lookup fallback | OK if lookup renders as dropdown |
| Export Section | Dropdown from `GetAll(AppConfig.ExportPermit).Where(IsActive && IsBorder)` | Present but no explicit `lookupName` in this report | Diff: must use Border Export Permit / Export Permit border sections only. |
| Apply Type | Dropdown from common apply types excluding Fine | Present select | OK |
| Payment Type | Dropdown from active payment types, `--- All ---` | Present select, hardcoded values | Possible diff if DB payment types change; leave for now because visible complaint is headers/section. |
| Company Registration No | Textbox with company lookup JS | Present | OK |
| Company Name | Readonly textbox auto-filled by company registration no | Not present as visible filter | Diff from old UI. |

Old RDLC table/header:

| Old column | New status |
|---|---|
| No. | Present as row number |
| Sakhan | Present |
| Licence No | Present |
| Application No | Present |
| Dynamic licence header (`header2`) | Present but currently rendered literally as `=Parameters!header2.Value` |
| Dynamic date header (`header3`) | Present but currently rendered literally as `=Parameters!header3.Value` |
| Company Registration No | Present |
| Company Name | Present |
| Voucher No | Present |
| Voucher Date | Present |
| Total Amount | Present |

Old dynamic header mapping:

| ApplyType | Header 2 | Header 3 |
|---|---|---|
| New | Licence No | Licence Date |
| Amend | Licence Amendment No | Amendment Date |
| Extension | Licence Extension No | Extension Date |
| Cancel | Licence Cancel No | Cancellation Date |
| Actual Amend | Licence Actual Amendment No | Actual Amendment Date |

Diffs found before code changes:

- New frontend config falls back to generic `exportImportSections`; old admin uses Export Permit border sections only.
- New frontend column keys do not use the existing voucher resolver, so RDLC parameter expressions show as literal column titles.
- Old report title parameter is `Border Export Permit Voucher List ({FromDate}) To ({ToDate})`.
- Old RDLC has a total amount footer: `TOTAL` and `SUM(Amount)`.

Planned fixes after diff:

1. Use `borderExportPermitSections` lookup. Done in code.
2. Add old RDLC-style voucher subtitle. Done in code.
3. Wire this report to `resolveImportLicenceVoucherColumns` and rename the dynamic columns to keys `LicenceNo` and `LicenceDate`. Done in code.
4. Footer total amount is pending because current generic table footer support is separate from this config patch.

Implementation applied:

- Updated `BorderExportPermitVoucherReport` in `Frontend/src/Report/config/reportConfigs.ts` to use `lookupName: 'borderExportPermitSections'`.
- Added `reportSubtitle: importLicenceRangeSubtitle('Border Export Permit Voucher List')`.
- Added `resolveColumns: resolveImportLicenceVoucherColumns`.
- Renamed dynamic parameter columns from `ParametersHeader2Value` / `ParametersHeader3Value` to `LicenceNo` / `LicenceDate`, so the resolver displays `Licence No`, `Licence Date`, `Licence Amendment No`, etc. based on Apply Type.

Verification:

- DB procedure check: `dbo.sp_VoucherReport_pagination` with `FormType = 'Border Export Permit'`, date range `2023-01-01` to `2026-06-30`, `ApplyType = 'New'`, page size 3 returned rows with `TotalCount = 42` in 556 ms.

### Parity Diff - Border Export Permit Extension/Cancellation Reports - 2026-06-10

Old admin references checked:

- Extension controller/action: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`, `BorderExportPermitExtensionReport`
- Cancellation controller/action: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`, `BorderExportPermitCancelReport`
- Extension RDLC: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\BorderExtensionReport.rdlc`
- Cancellation RDLC: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\BorderCancelReport.rdlc`

Shared old filter behavior:

| Filter | Old admin behavior | New config status | Diff |
|---|---|---|---|
| From Date / To Date | Required date fields | Present | OK |
| Sakhan | Dropdown, all sakhans, `--- All ---` | Present as numeric lookup fallback | OK if lookup renders as dropdown |
| Export Section | Dropdown from `GetAll(AppConfig.ExportPermit).Where(IsActive && IsBorder)` | Present but no explicit `lookupName` in these reports | Diff: must use Border Export Permit / Export Permit border sections only. |
| Company Registration No | Textbox with company lookup JS | Present | OK |
| Company Name | Readonly textbox auto-filled by company registration no | Not present as visible filter | Diff from old UI. |

Old RDLC table/header:

| Extension old column | New status |
|---|---|
| No., Sakhan, Section, Licence No, Extension No, Extension Last Date, Company Registration No, Company Name, Company Address, Curency, Total Value | Present |

| Cancellation old column | New status |
|---|---|
| No., Sakhan, Section, Licence No, Cancellation No, Cancellation Date, Company Registration No, Company Name, Company Address, Curency, Total Value, Remark | Present |
| HSCode | Extra in new config; not in old `BorderCancelReport.rdlc` |

Shared old RDLC footer:

- currency-wise licence count: `Currency: N licence(s)`
- currency-wise amount total
- grand total licence count: `Total: N licence(s)`

Diffs found before code changes:

- Both reports fall back to generic `exportImportSections`; old admin uses Export Permit border sections only.
- Extension already has `reportSubtitle` and `currencyTotalsColumns` in the current config.
- Cancellation does not yet have `reportSubtitle` / `currencyTotalsColumns`.
- Cancellation has an extra `hsCode` column that is not in old RDLC.

Planned fixes after diff:

1. Add `lookupName: 'borderExportPermitSections'` to Extension and Cancellation. Done in code.
2. Add old RDLC-style date subtitle to Cancellation. Done in code.
3. Add `currencyTotalsColumns` to Cancellation. Done in code.
4. Remove extra `hsCode` column from Cancellation. Done in code.

Implementation applied:

- Updated `BorderExportPermitExtensionReport` in `Frontend/src/Report/config/reportConfigs.ts` to use `lookupName: 'borderExportPermitSections'`.
- Updated `BorderExportPermitCancellationReport` to use `lookupName: 'borderExportPermitSections'`.
- Added cancellation `reportSubtitle: importLicenceRangeSubtitle('List of Border Export Permit Report')`.
- Added cancellation `currencyTotalsColumns: { labelColumnKey: 'LicenceNo', valueColumnKey: 'TotalValue' }`.
- Removed the extra cancellation `hsCode` column because `BorderCancelReport.rdlc` does not include HS Code.

Verification:

- DB procedure check: `dbo.sp_ExtensionReport_pagination` with `FormType = 'Border Export Permit'`, date range `2023-01-01` to `2026-06-30`, page size 3 returned rows with `TotalCount = 3` in 192 ms.
- DB procedure check: `dbo.sp_CancelReport_pagination` with `FormType = 'Border Export Permit'`, date range `2023-01-01` to `2026-06-30`, page size 3 returned rows with `TotalCount = 2` in 60 ms.
- Lookup scan confirmed `borderExportPermitSections` is now only on the Wai Phyo-owned Border Export Permit reports in this pass: Actual Amendment, Amendment, Cancellation, Extension, New, and Voucher.

### Parity Diff - Border Export Permit By HS Code Report - 2026-06-10

Old admin references checked:

- Controller/action: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`, `BorderExportPermitByHSCodeReport`
- View/filter form: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderExportPermitByHSCodeReport.cshtml`
- RDLC: `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\BorderHSCodeReport.rdlc`

Old filter box:

| Filter | Old admin behavior | New config status | Diff |
|---|---|---|---|
| From Date / To Date | Required date fields | Present | OK |
| Sakhan | Dropdown, all sakhans, `--- All ---` | Present as numeric lookup fallback | OK if lookup renders as dropdown |
| Export Section | Dropdown from `GetAll(AppConfig.ExportPermit).Where(IsActive && IsBorder)` | Missing in this report config | Diff: add Export Permit border section filter. |
| Filter By | Dropdown from `CommonRepository.GetFilterType()` | Present as free text | Diff: use Start/End dropdown. |
| HS Code | Textbox | Present | OK |

Old RDLC table/header:

| Old column | New status |
|---|---|
| Sr.No. | Present as row number |
| HS Code | Present |
| Description | Present |
| No of Licences | Present |
| Total Value | Present |
| Currency | Present |
| Company Name | Extra in new config; not in old `BorderHSCodeReport.rdlc` |

Old RDLC / controller behavior:

- Report title parameter: `List of Border Export Permit By HS Code From ({FromDate}) To ({ToDate})`.
- HS Code cell has a hyperlink to `Reports/BorderHSCodeDetailReport` with filters carried in query string.
- RDLC footer shows total distinct licence count.

Diffs found before code changes:

- New config is missing Export Section filter.
- New backend controller does not copy `ExportImportSectionId` into `sp_HSCodeReportRequest`.
- New config uses text input for `FilterType`; old UI uses a dropdown.
- New config includes `CompanyName`; old RDLC does not.
- New frontend currently has no `BorderHSCodeDetailReport` route/config target, so HS Code drilldown cannot be completed as a small config patch.

Planned fixes after diff:

1. Add Export Section filter with `lookupName: 'borderExportPermitSections'`. Done in code.
2. Add `ExportImportSectionId` to `BorderExportPermitByHSCodeReportRequest` and pass it into `sp_HSCodeReportRequest`. Done in code.
3. Change Filter By to Start/End dropdown with default `Start`. Done in code.
4. Remove Company Name column. Done in code.
5. Add old RDLC-style report subtitle. Done in code.
6. HS Code detail drilldown is pending until a new `BorderHSCodeDetailReport` frontend route/config exists.

Implementation applied:

- Updated `BorderExportPermitByHSCodeReport` in `Frontend/src/Report/config/reportConfigs.ts` to add the missing Export Section filter using `lookupName: 'borderExportPermitSections'`.
- Changed `FilterType` from free text to a Start/End dropdown with default `Start`.
- Added `reportSubtitle: importLicenceRangeSubtitle('List of Border Export Permit By HS Code', true)`.
- Removed the extra `CompanyName` column because `BorderHSCodeReport.rdlc` does not include it.
- Updated `Backend/Controllers/Report/BorderExportPermitByHSCodeReportController.cs` to accept `ExportImportSectionId` and pass it into `sp_HSCodeReportRequest`.

Verification:

- Backend build: passed with existing nullable/migration warnings only.
- DB procedure check: `dbo.sp_HSCodeReport_pagination` with `FormType = 'Border Export Permit'`, date range `2023-01-01` to `2026-06-30`, `FilterType = 'Start'`, page size 3 returned rows with `TotalCount = 74` in 279 ms.
- Section-filtered frontend/API behavior uses the existing LINQ path because `sp_HSCodeReport.CreateAggregateResultAsync` deliberately skips the aggregate stored procedure when `ExportImportSectionId != 0`.

## Export Licence New Report Quota Pass - 2026-06-09

Goal:

- Fix `quota` showing `N/A` for every Export Licence New Report row.

Finding:

- `ExportLicence` table does not have a `quota` column.
- DB schema has `quota` only on `ImportLicence` and `BorderImportLicence`.
- The old `dbo.sp_NewReport` Export Licence branch selected `ExportLicence.auto` only, not quota.
- Current frontend config still includes the `quota` column for parity with the RDLC column list.

Fix applied:

- Updated only the Export Licence branch in `StoredProcedureMigrations/sp_NewReport_pagination.sql`.
- Changed the output from `NULL quota` to blank-string quota.
- Deployed `dbo.sp_NewReport_pagination` to `TradeNetDB`.

Test result:

| Report | Test date | Rows shown | Time | Quota result |
|---|---|---:|---:|---|
| Export Licence New Report | 2023-04-03 | 6 fast-page rows | 267 ms | `quota` returned as blank string, not `NULL`. |

Conclusion:

- The DB cannot provide a real Export Licence quota value because the source column does not exist.
- The report should no longer render `N/A`; it should render the quota cell blank.
- If the frontend still shows `N/A`, inspect the API JSON to confirm whether `quota` is arriving as `""` or being transformed back to `null`.

## Extension Data-Show Pass - 2026-06-09

Goal:

- Focus on Export Licence Extension Report.
- Make sure related `dbo.sp_ExtensionReport_pagination` branches still show data.

Problem found:

- The procedure used `CreatedDate <= @ToDate`.
- Frontend date-only filters send values like `2026-05-25`, which means midnight at the start of the day.
- Rows later on the selected day were excluded, so the report could show no data even when DB rows existed.

Fix applied:

- Updated `StoredProcedureMigrations/sp_ExtensionReport_pagination.sql`.
- Updated `StoredProcedureMigrations/sp_ExtensionReportCurrencyTotals.sql`.
- Date filters now use the full selected day:
  - `CreatedDate >= @FromDate`
  - `CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))`
- Deployed both procedures to `TradeNetDB`.

Export Licence result:

| Report | Test date | Rows shown | Time | Note |
|---|---|---:|---:|---|
| Export Licence Extension Report | 2026-05-25 | 2 | 96 ms | Rows returned with `Currency=USD`, `Amount` populated; currency totals returned `USD`, 2 licences, total value 2100. |

Related extension smoke results:

| FormType | Test date | Rows shown | Time | Note |
|---|---|---:|---:|---|
| Import Licence | 2026-02-01 | 1 | 87 ms | Data returns. |
| Import Permit | 2026-05-25 | 1 | 55 ms | Data returns. |
| Export Permit | 2026-05-25 | 1 | 53 ms | Data returns. |
| Border Export Licence | 2026-05-25 | 1 | 258 ms | Data returns. |
| Border Import Licence | 2026-05-25 | 1 | 308 ms | Data returns. |
| Border Export Permit | 2026-05-25 | 1 | 50 ms | Data returns. |
| Border Import Permit | 2026-05-25 | 1 | 76 ms | Data returns. |

Conclusion:

- Export Licence Extension Report is OK at DB level.
- The shared extension pagination procedure returns data for tested one-day filters.
- If the UI still fails, check controller payload, API response, or frontend mapping before changing SQL again.

## Amendment Data-Show Pass - 2026-06-08

Goal:

- Focus on Export Licence Amendment Report first.
- Confirm other `dbo.sp_AmendReport_pagination` branches still return data after the same procedure change.

Fix applied:

- Fixed Border Export Licence amendment Sakhan filter in `StoredProcedureMigrations/sp_AmendReport_pagination.sql`.
- Wrong condition was comparing `ExportImportSectionId` against `SakhanId`.
- Correct condition now filters `BorderExportLicence.SakhanId`.
- Deployed `dbo.sp_AmendReport_pagination` to `TradeNetDB`.

DB smoke results:

| FormType | Test date | Rows shown | Time | Sample fields |
|---|---|---:|---:|---|
| Export Licence | 2026-05-22 | 2 | 58 ms | `HSCode`, `Currency`, and `Amount` populated. |
| Import Licence | 2026-05-11 | 1 | 90 ms | `HSCode`, `Currency`, and `Amount` populated. |
| Export Permit | 2026-05-22 | 1 | 59 ms | `HSCode`, `Currency`, and `Amount` populated. |
| Import Permit | 2026-05-22 | 1 | 35 ms | `HSCode`, `Currency`, and `Amount` populated. |
| Border Export Licence | 2026-05-22 | 2 | 132 ms | Fixed by Sakhan predicate correction; data now returns. |
| Border Import Licence | 2026-05-22 | 1 | 241 ms | Data returns. |
| Border Export Permit | 2026-05-22 | 1 | 35 ms | Data returns. |
| Border Import Permit | 2026-05-22 | 1 | 39 ms | Data returns. |

Conclusion:

- Export Licence Amendment Report is OK at DB level.
- The amend stored procedure family returns rows in the tested one-day filters.
- If the UI still fails, check controller payload, API response, or frontend mapping before changing SQL again.

## Data-Show Pass - 2026-06-07

Goal:

- Pause deeper optimization.
- Confirm leftover Wai Phyo tables can return rows first.

Results:

| Report | DB result | Test date | Time | Note |
|---|---|---|---:|---|
| Export Licence New Report | Returned rows | 2023-04-03 | 10,246 ms | Fixed Export Licence branch date filter to include full selected day for date-only frontend values. |
| Export Licence Voucher Report | Returned rows with Currency/TotalAmount | 2023-04-03 | 888 ms blank filters / 718 ms ApplyType=Amend | Item values are resolved after paging, so visible voucher rows show Currency and Lic Value without full-range item scans. |
| Export Licence Actual Amendment Report | Returned rows | 2026-04-01 | 814 ms | Source rows exist: 5,476 approved actual-amend rows overall; procedure returned 2 rows for 2026-04-01. |
| Border Import Permit Amendment Report | Returned row | 2026-05-22 | 840 ms | Source row exists: 1 approved amend row overall; procedure returned it. |

Current tradeoff:

- Export Licence Voucher rows now show first and include Currency/TotalAmount on the visible page.
- Exact wide total-count remains disabled in `ExportLicenceVoucherReportController` to avoid blocking data display on large ranges.

### Border Export Permit

| Report | Stored procedure | Owner | Deadline | Sheet status | Current note | Next action |
|---|---|---|---|---|---|---|
| Border Export Permit Amendment Report | `dbo.sp_AmendReport` | Wai Phyo | 5.June.2026 | OK | DB smoke passed; live controller smoke now also returns rows in 355 ms | Frontend/UAT retest only if UI still fails |
| Border Export Permit Extension Report | `dbo.sp_ExtensionReport` | Wai Phyo | 5.June.2026 | OK | DB smoke passed after extension date fix | No action unless frontend regression appears |
| Border Export Permit Cancellation Report | `dbo.sp_CancelReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Export Permit By HS Code Report | `dbo.sp_HSCodeReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Export Permit Voucher Report | `dbo.sp_VoucherReport` | Wai Phyo | 5.June.2026 | OK | Live controller smoke returns rows in about 1 s | Frontend/UAT retest only if UI still fails |
| Border Export Permit Actual Amendment Report | `dbo.sp_ActualAmendReport` | Wai Phyo | 5.June.2026 | OK | Live controller smoke confirms valid empty result in 651 ms when DB has no matching rows | No SQL action unless another environment has source rows |
| Border Export Permit New Report (New Report) | `dbo.sp_NewReport` | Wai Phyo | 10.June.2026 | Fixed in DB | DB returns data quickly | Needs frontend retest |

### Export Licence

| Report | Stored procedure | Owner | Deadline | Sheet status | Current note | Next action |
|---|---|---|---|---|---|---|
| Export Licence Amendment Report | `dbo.sp_AmendReport` | Wai Phyo | 5.June.2026 | OK | DB smoke passed; `HSCode`, `Currency`, and `Amount` return | Frontend/UAT retest only if UI still fails |
| Export Licence Extension Report | `dbo.sp_ExtensionReport` | Wai Phyo | 5.June.2026 | OK | DB smoke passed; date-only filters now return rows | Retest API/frontend only if UI still fails |
| Export Licence Cancellation Report | `dbo.sp_CancelReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Export Licence By HS Code Report | `dbo.sp_HSCodeReport` | Wai Phyo | 10.June.2026 | Fixed | DB and focused controller retest passed | Done for Priority 1 |
| Export Licence Total Value & Licences Report | `dbo.sp_ExportLicenceDetailReport` | Wai Phyo | 5.June.2026 | Fixed | DB and focused controller retest passed | Done |
| Export Licence Voucher Report | `dbo.sp_VoucherReport` | Wai Phyo | 10.June.2026 | Fixed for one-day target | Live controller smoke returns rows in 467 ms with blank Apply/Payment filters | Wide exact count still risky |
| Export Licence Actual Amendment Report | `dbo.sp_ActualAmendReport` | Wai Phyo | 5.June.2026 | OK | Live controller smoke returns rows in 760 ms on the tested data-bearing day | Frontend/UAT retest only if UI still fails |
| Export Licence New Report (New Report) | `dbo.sp_NewReport` | Wai Phyo | 10.June.2026 | Fixed for one-day target | Live controller smoke returns rows in 2 s; quota now returns blank instead of `N/A` because source table has no quota column | Frontend retest |

### Border Import Licence

| Report | Stored procedure | Owner | Deadline | Sheet status | Current note | Next action |
|---|---|---|---|---|---|---|
| Border Import Licence Amendment Report | `dbo.sp_AmendReport` | Wai Phyo | 10.June.2026 | Fixed in DB | Live controller smoke returns rows in 746 ms | Frontend/UAT retest only if UI still fails |
| Border Import Licence Extension Report | `dbo.sp_ExtensionReport` | Wai Phyo | 5.June.2026 | OK | DB smoke passed after extension date fix | No action unless frontend regression appears |
| Border Import Licence Cancellation Report | `dbo.sp_CancelReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Import Licence By HS Code Report | `dbo.sp_HSCodeReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Import Licence Voucher Report | `dbo.sp_VoucherReport` | Wai Phyo | 10.June.2026 | Fixed in DB | Live controller smoke returns rows, but broad-range controller test took 34 s | Frontend/UAT retest; keep on performance watch list |
| Border Import Licence Actual Amendment Report | `dbo.sp_ActualAmendReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Import Licence New Report (New Report) | `dbo.sp_NewReport` | Wai Phyo | 10.June.2026 | Fixed in DB | Live controller smoke returns rows in 500 ms | Frontend/UAT retest only if UI still fails |

### Border Import Permit Shared With Bran

Senior note: this area was updated and should be tested again after pulling the latest commit.

| Report | Stored procedure | Owner | Deadline | Sheet status | Current note | Next action |
|---|---|---|---|---|---|---|
| Border Import Permit Amendment Report | `dbo.sp_AmendReport` | Wai Phyo, Bran | 5.June.2026 | OK | DB smoke passed; matching row exists | No SQL action unless frontend fails |
| Border Import Permit Extension Report | `dbo.sp_ExtensionReport` | Wai Phyo, Bran | 10.June.2026 | Fixed in DB/API smoke | DB smoke passed after extension date fix; endpoint previously executed | Done |
| Border Import Permit Cancellation Report | `dbo.sp_CancelReport` | Wai Phyo, Bran | 10.June.2026 | Fixed/no data | Procedure and endpoint execute; no approved cancel rows in DB | Done |
| Border Import Permit By HS Code Report | `dbo.sp_HSCodeReport` | Wai Phyo, Bran | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Import Permit Voucher Report | `dbo.sp_VoucherReport` | Wai Phyo, Bran | 10.June.2026 | Fixed in DB/API smoke | DB returns voucher data; endpoint executes | Done |
| Border Import Permit Actual Amendment Report | `dbo.sp_ActualAmendReport` | Wai Phyo, Bran | 10.June.2026 | Fixed/no data | Procedure executes; no approved actual-amend rows in DB | Done |
| Border Import Permit New Report (New Report) | `dbo.sp_NewReport` | Wai Phyo, Bran | 5.June.2026 | Fixed in DB/API smoke | DB returns New/Approved data; endpoint executes | Done |

## Excluded From Active Scope

These are Ko Htet-only after the meeting. Do not touch them in this work stream.

| Area | Reports |
|---|---|
| Border Export Permit detail family | Daily, Detail, By Section, By Seller Country, Company List |
| Export Licence detail family | Daily, Detail, By Section, By Method, By Seller Country, Company List |
| Border Import Licence detail family | Daily, Detail, By Section, By Method, By Seller Country, Company List, Pending, Pending Detail |
| Border Import Permit detail family | Daily, Detail, By Section, By Seller Country, Company List |

Note: `Export Licence Total Value & Licences Report` stays active because the meeting list assigns it to Wai Phyo.

## Previous Wai Phyo Findings To Keep

These are useful facts from earlier DB checks. Re-test after pulling senior changes.

| Target | Result |
|---|---|
| Border Export Permit New Report / `sp_NewReport_pagination` | DB returned rows quickly for a small date range, but sheet still says frontend failed. Check API/frontend after pull. |
| Export Licence Voucher Report / `sp_VoucherReport_pagination` | DB returned rows quickly after local SQL fix, but sheet still says failed. Check latest senior changes before redeploying. |
| Export Licence New Report / `sp_NewReport_pagination` | Fast page returned rows, exact total count timed out on wider test. Needs careful count/performance handling. |
| Export Licence By HS Code Report / `sp_HSCodeReport_pagination` | Fixed for Priority 1. Fast-page mode returns rows in 1,192 ms and exact-count one-day test returned in 948 ms. |
| Actual Amendment reports | Some DB filters legitimately return no rows. Confirm source rows exist before changing SQL. |

## Next Step

Next target: decide whether wide-range exact counts are required for Export Licence Voucher Report and Export Licence New Report.

Reason: one-day filters now return quickly for the active Wai Phyo reports. The only remaining performance risk is multi-year exact total count on those two Export Licence reports.

## Active Task Log - Border Import Permit Extension Report

Started: 2026-06-07

Scope:

- Owner: Wai Phyo, Bran
- Stored procedure: `dbo.sp_ExtensionReport_pagination`
- Local SQL file: `StoredProcedureMigrations/sp_ExtensionReport_pagination.sql`
- Controller: `Backend/Controllers/Report/BorderImportPermitExtensionReportController.cs`
- FormType: `Border Import Permit`

Current observation:

- Senior pull changed `sp_ExtensionReport_pagination.sql`.
- Controller already calls `sp_ExtensionReport.ExecuteAsync(...)`.
- Wrapper already executes `dbo.sp_ExtensionReport_pagination`.

Next checks:

1. Deploy/current-test the pulled pagination SQL in `TradeNetDB`.
2. Execute the procedure with a small date range.
3. If data returns, test API.
4. If SQL fails, patch only the `Border Import Permit` branch.

Result:

- Deployed `StoredProcedureMigrations/sp_ExtensionReport_pagination.sql` to `TradeNetDB`.
- DB test:
  - `@FormType = N'Border Import Permit'`
  - `@FromDate = '2023-01-01'`
  - `@ToDate = '2026-06-30'`
  - `@PageSize = 20`
  - `@IncludeTotalCount = 1`
- DB returned 19 total rows.
- First page returned rows.
- SQL elapsed time: 83 ms.
- API smoke:
  - `BorderImportPermitExtensionReportController` Post test passed.
  - Endpoint test time: about 105 ms.
- Broader Border Import Permit Post smoke suite passed: 12/12 endpoints.

Status: done.

## Active Task Log - Border Import Permit Cancellation Report

Started: 2026-06-07

Scope:

- Owner: Wai Phyo, Bran
- Stored procedure: `dbo.sp_CancelReport_pagination`
- Local SQL file: `StoredProcedureMigrations/sp_CancelReport_pagination.sql`
- Controller: `Backend/Controllers/Report/BorderImportPermitCancellationReportController.cs`
- FormType: `Border Import Permit`

Next checks:

1. Deploy/current-test the pulled pagination SQL in `TradeNetDB`.
2. Execute the procedure with a controlled date range.
3. If data returns, record performance and API smoke result.
4. If SQL fails, patch only the `Border Import Permit` branch.

Result:

- Deployed `StoredProcedureMigrations/sp_CancelReport_pagination.sql` to `TradeNetDB`.
- DB test:
  - `@FormType = N'Border Import Permit'`
  - `@FromDate = '2023-01-01'`
  - `@ToDate = '2026-06-30'`
  - `@PageSize = 20`
  - `@IncludeTotalCount = 1`
- Procedure executed successfully.
- SQL elapsed time: 61 ms.
- Returned rows: 0.
- Base DB check found:
  - `ApplyType='Cancel' AND Status='Approved'`: 0 rows.
  - Existing cancellation-like records are `Status='Auto Cancel'` with `ApplyType='New'`, not approved cancel records.
- API smoke:
  - `BorderImportPermitCancellationReportController` Post test passed.

Status: done. This is no longer "failed to load"; it is a legitimate no approved-cancel-data result for the tested DB range.

## Active Task Log - Border Import Permit Voucher Report

Started: 2026-06-07

Scope:

- Owner: Wai Phyo, Bran
- Stored procedure: `dbo.sp_VoucherReport_pagination`
- Local SQL file: `StoredProcedureMigrations/sp_VoucherReport_pagination.sql`
- Controller: `Backend/Controllers/Report/BorderImportPermitVoucherReportController.cs`
- FormType: `Border Import Permit`

Next checks:

1. Deploy/current-test the pulled pagination SQL in `TradeNetDB`.
2. Execute the procedure with a controlled date range and `@ApplyType = N'New'`.
3. If data returns, record performance and API smoke result.
4. If SQL fails, patch only the `Border Import Permit` branch.

Result:

- Deployed `StoredProcedureMigrations/sp_VoucherReport_pagination.sql` to `TradeNetDB`.
- DB test:
  - `@FormType = N'Border Import Permit'`
  - `@FromDate = '2023-01-01'`
  - `@ToDate = '2026-06-30'`
  - `@ApplyType = N'New'`
  - `@PageSize = 20`
  - `@IncludeTotalCount = 1`
- DB returned rows.
- TotalCount: 882.
- SQL elapsed time: 129 ms.
- API smoke:
  - `BorderImportPermitVoucherReportController` Post test passed in the broader Border Import Permit suite.

Status: done.

## Active Task Log - Border Import Permit Actual Amendment Report

Started: 2026-06-07

Scope:

- Owner: Wai Phyo, Bran
- Stored procedure: `dbo.sp_ActualAmendReport_pagination`
- Local SQL file: `StoredProcedureMigrations/sp_ActualAmendReport_pagination.sql`
- Controller: `Backend/Controllers/Report/BorderImportPermitActualAmendmentReportController.cs`
- FormType: `Border Import Permit`

Next checks:

1. Deploy/current-test the pagination SQL in `TradeNetDB`.
2. Execute the procedure with a controlled date range.
3. Check whether actual-amend source rows exist in `BorderImportPermit`.
4. If DB has no matching source rows, record as valid no-data.

Result:

- Deployed `StoredProcedureMigrations/sp_ActualAmendReport_pagination.sql` to `TradeNetDB`.
- DB test:
  - `@FormType = N'Border Import Permit'`
  - `@FromDate = '2023-01-01'`
  - `@ToDate = '2026-06-30'`
  - `@PageSize = 20`
  - `@IncludeTotalCount = 1`
- Procedure executed successfully.
- SQL elapsed time: 76 ms.
- Returned rows: 0.
- Base DB check found:
  - `ApplyType='Actual Amend' AND Status='Approved'`: 0 rows.
- API smoke:
  - `BorderImportPermitActualAmendmentReportController` Post test passed in the broader Border Import Permit suite.

Status: done. This is valid no-data for the tested DB range.

## Active Task Log - Border Import Permit New Report

Started: 2026-06-07

Scope:

- Owner: Wai Phyo, Bran
- Stored procedure: `dbo.sp_NewReport_pagination`
- Local SQL file: `StoredProcedureMigrations/sp_NewReport_pagination.sql`
- Controller: `Backend/Controllers/Report/BorderImportPermitNewReportNewReportController.cs`
- FormType: `Border Import Permit`

Next checks:

1. Deploy/current-test the pulled pagination SQL in `TradeNetDB`.
2. Execute the procedure with a controlled date range.
3. Check whether New/Approved source rows exist in `BorderImportPermit`.
4. If data returns, record performance and close the Border Import Permit shared group.

Result:

- Deployed `StoredProcedureMigrations/sp_NewReport_pagination.sql` to `TradeNetDB`.
- DB test:
  - `@FormType = N'Border Import Permit'`
  - `@FromDate = '2023-01-01'`
  - `@ToDate = '2026-06-30'`
  - `@PageSize = 20`
  - `@IncludeTotalCount = 1`
- DB returned rows.
- TotalCount: 441.
- SQL elapsed time: 85 ms.
- API smoke:
  - `BorderImportPermitNewReportNewReportController` Post test passed in the broader Border Import Permit suite.

Status: done.

Border Import Permit shared group result:

- Post endpoint smoke suite passed: 12/12 Border Import Permit controllers.
- Wai Phyo-shared failed items after senior pull are now either:
  - returning data, or
  - executing successfully with confirmed no matching source rows.

## Active Task Log - Border Export Permit New Report

Started: 2026-06-07

Scope:

- Owner: Wai Phyo
- Stored procedure: `dbo.sp_NewReport_pagination`
- Local SQL file: `StoredProcedureMigrations/sp_NewReport_pagination.sql`
- Controller: `Backend/Controllers/Report/BorderExportPermitNewReportNewReportController.cs`
- FormType: `Border Export Permit`

Next checks:

1. Execute the already-deployed `sp_NewReport_pagination` with `@FormType = N'Border Export Permit'`.
2. If DB returns data, test/build API side.
3. If SQL fails, patch only the `Border Export Permit` branch.

Result:

- `sp_NewReport_pagination` was already deployed during the Border Import Permit New test.
- DB test:
  - `@FormType = N'Border Export Permit'`
  - `@FromDate = '2023-01-01'`
  - `@ToDate = '2026-06-30'`
  - `@PageSize = 20`
  - `@IncludeTotalCount = 1`
- DB returned rows.
- TotalCount: 42.
- SQL elapsed time: 62 ms.
- Backend build passed earlier in this work session.

Status: DB fixed. Needs frontend/API retest in browser if the page still says failed.

PM-feedback follow-up on 2026-06-25:

- Rechecked old admin reference before changing behavior:
  - `D:\Job\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderExportPermitNewReport.cshtml`
  - Old filters are From Date, To Date, Sakhan, Export Section, Company Registration No, and readonly Company Name.
  - Old report has no Auto filter.
- New frontend status:
  - Auto filter is already absent, matching PM feedback to remove Auto filter.
  - Added missing readonly `CompanyName` filter via shared `importLicenceCompanyNameFilter`.
  - Existing section lookup is `borderExportPermitSections`.
  - Existing totals config is `currencyTotalsColumns: { labelColumnKey: 'LicenceNo', valueColumnKey: 'TotalValue' }`.
- DB/source checks:
  - Source rows exist for selected Sakhan. Example: `SakhanId = 3` (`CSH`) has a New/Approved row on `2023-08-23`.
  - `sp_NewReport_pagination` with `@SakhanId = 0` and `2023-08-23` returned 2 rows, `TotalCount = 2`, in about 61 ms.
  - `sp_NewReport_pagination` with `@SakhanId = 3` and `2023-08-23` returned 1 row, `TotalCount = 1`, in about 61 ms.
- Problem found:
  - Page rows worked for selected Sakhan, but `dbo.sp_ExportPermitListingCurrencyTotals` returned no footer totals for the same one-day selected-Sakhan request because the Border Export Permit totals branches used `CreatedDate <= @ToDate`.
- Fix applied and deployed:
  - Patched only the Border Export Permit branches in `StoredProcedureMigrations/sp_ExportPermitListingCurrencyTotals.sql`.
  - Date filtering now includes the whole selected ToDate with `CreatedDate < DATEADD(day, 1, @ToDate)`.
  - Redeployed `StoredProcedureMigrations/sp_ExportPermitListingCurrencyTotals.sql` to `TradeNetDB`.
- DB retest after deploy:
  - Selected Sakhan footer, `@SakhanId = 3`, `2023-08-23`: returned `USD`, `NoOfLicences = 1`, `TotalValue = 5500.0000`, elapsed about 589 ms including compile.
  - All Sakhan footer, same date: returned `USD`, `NoOfLicences = 2`, `TotalValue = 30584.4000`, elapsed about 36 ms.
- Frontend check:
  - `tsc --noEmit` is still blocked by existing missing type definitions: `chai` and `deep-eql`.
  - This is the known local dependency blocker, not a syntax error from the config patch.

Current status: DB data-show, selected Sakhan, and currency footer totals are fixed/deployed. Frontend browser retest remains pending.

## Active Task Log - Export Licence Voucher Report

Started: 2026-06-07

Scope:

- Owner: Wai Phyo
- Stored procedure: `dbo.sp_VoucherReport_pagination`
- Local SQL file: `StoredProcedureMigrations/sp_VoucherReport_pagination.sql`
- Controller: `Backend/Controllers/Report/ExportLicenceVoucherReportController.cs`
- FormType: `Export Licence`

Next checks:

1. Re-test the already-deployed voucher procedure for `@FormType = N'Export Licence'`.
2. Confirm the Export Licence branch does not depend on missing DB objects.
3. If DB returns data, record performance.
4. If SQL fails, patch only the `Export Licence` branch.

Problem found:

- The pulled Export Licence branch depended on `dbo.vw_ExportLicenceItemTotalByCurrency`.
- Broad test timed out after 120 seconds.
- Removing only the view dependency was not enough; exact-count/page still timed out on a 3-year range.
- Existing index `NonClusteredIndex-AccountTransaction-125044` made the one-day count fast when used explicitly.

Fix applied:

- Patched only the `Export Licence` branch in `StoredProcedureMigrations/sp_VoucherReport_pagination.sql`.
- Replaced indexed-view item totals with direct `ExportLicenceItem` aggregation after paging.
- Added `MAXDOP 1` on the Export Licence voucher branch to reduce bad parallel `EXECSYNC` plans.
- Redeployed `StoredProcedureMigrations/sp_VoucherReport_pagination.sql` to `TradeNetDB`.

Latest Priority 2 update:

- Added index script:
  - `StoredProcedureMigrations/Indexes/ExportLicenceVoucherReport_indexes.sql`
- Deployed new DB index:
  - `IX_AccountTransaction_ExportLicenceVoucher`
- The index filters payment rows to:
  - `TransactionFormType = N'Export Licence'`
  - `IsPayment = 1`
  - keyed by `PaymentDate` and `TransactionId`
- Updated the Export Licence branch in `sp_VoucherReport_pagination.sql`:
  - added `AccountTransaction.TransactionFormType='Export Licence'`;
  - changed date filtering to include the whole selected ToDate with `< DATEADD(day, 1, @ToDate)`;
  - avoided a hard index-name hint so the procedure will not fail if another environment deploys the procedure before the index script.
- Redeployed `StoredProcedureMigrations/sp_VoucherReport_pagination.sql` to `TradeNetDB`.
- Follow-up field restore on 2026-06-25:
  - Changed the Export Licence branch to resolve `Currency` and `TotalAmount` with `OUTER APPLY` after the base voucher page is selected.
  - Fixed duplicate fallback sort columns in the voucher procedure when sorting by `ApplicationNo` or `LicenceNo`.
  - Redeployed `StoredProcedureMigrations/sp_VoucherReport_pagination.sql` to `TradeNetDB`.

Result:

- Latest fast-page DB retest:
  - `@FormType = N'Export Licence'`
  - `@FromDate = '2023-04-03'`
  - `@ToDate = '2023-04-03'`
  - blank `@ApplyType` / blank `@PaymentType`
  - `@PageSize = 5`
  - `@IncludeTotalCount = 0`
  - returned mixed ApplyType rows with `Currency=USD` and populated `TotalAmount` in about 888 ms.
- Selected ApplyType DB retest:
  - same date/filter shape, `@ApplyType = N'Amend'`
  - returned rows with `Currency=USD` and populated `TotalAmount` in about 718 ms.
- One-day exact-count DB test:
  - `@FormType = N'Export Licence'`
  - `@FromDate = '2023-04-03'`
  - `@ToDate = '2023-04-03 23:59:59'`
  - `@ApplyType = N'New'`
  - `@PageSize = 20`
  - `@IncludeTotalCount = 1`
- DB returned rows.
- TotalCount: 738.
- SQL elapsed time after fix: 247 ms.
- Fast-page mode with `@IncludeTotalCount = 0`: 127 ms.
- Broad 3-year exact-count test still timed out after 120 seconds.
- Broad retest after the index/procedure update is currently blocked:
  - fast-page and exact-count tests both timed out after 60 seconds;
  - `sys.dm_exec_requests` shows session `64` in `KILLED/ROLLBACK`;
  - session `64` is blocking schema-stability work (`LCK_M_SCH_S`) for other report queries;
  - `KILL 64 WITH STATUSONLY` reports rollback in progress.

Status: fixed for the stated one-day performance target. Wide-range retest must wait until the SQL Server rollback blocker clears.

## Next Candidate

Export Licence New Report (New Report).

Reason:

- Wai Phyo-owned.
- Marked `No OK`.
- Uses `dbo.sp_NewReport_pagination`.
- Earlier work showed fast page returns data, but exact total count can timeout on wide filters.

## Active Task Log - Export Licence New Report

Started: 2026-06-07

Scope:

- Owner: Wai Phyo
- Stored procedure: `dbo.sp_NewReport_pagination`
- Local SQL file: `StoredProcedureMigrations/sp_NewReport_pagination.sql`
- Controller: `Backend/Controllers/Report/ExportLicenceNewReportNewReportController.cs`
- FormType: `Export Licence`

Next checks:

1. Test one-day exact count in DB.
2. Test one-day fast page in DB.
3. If exact count is slow, try existing indexes before creating any index.
4. Patch only the `Export Licence` branch if needed.

Result:

- DB test:
  - `@FormType = N'Export Licence'`
  - `@FromDate = '2023-04-03'`
  - `@ToDate = '2023-04-03 23:59:59'`
  - `@PageSize = 20`
  - `@IncludeTotalCount = 1`
- DB returned rows.
- TotalCount: 738.
- SQL elapsed time: 159 ms.
- Wide 3-year exact-count test timed out after 120 seconds.

Status: fixed for the stated one-day target. Wide exact total count still needs separate strategy if required.

## Active Task Log - Export Licence By HS Code Report

Started: 2026-06-07

Scope:

- Owner: Wai Phyo
- Stored procedure: `dbo.sp_HSCodeReport_pagination`
- Local SQL file: `StoredProcedureMigrations/sp_HSCodeReport_pagination.sql`
- Controller: `Backend/Controllers/Report/ExportLicenceByHSCodeReportController.cs`
- FormType: `Export Licence`

Checks:

1. Deploy/current-test `sp_HSCodeReport_pagination` in `TradeNetDB`.
2. Execute one-day Export Licence branch.
3. Add fast-page/no-exact-count support because the frontend table requests `IncludeTotalCount=false` first.
4. Verify the backend/controller path with a focused xUnit test.

Result:

- Deployed `StoredProcedureMigrations/sp_HSCodeReport_pagination.sql` to `TradeNetDB`.
- DB test:
  - `@FormType = N'Export Licence'`
  - `@FromDate = '2023-04-03'`
  - `@ToDate = '2023-04-03 23:59:59'`
  - `@PageSize = 20`
- DB returned rows.
- TotalCount: 641 grouped rows.
- SQL elapsed time: 2459 ms.

Latest fix:

- Added `@IncludeTotalCount bit = 1` to `dbo.sp_HSCodeReport_pagination`.
- Patched only the `@FormType = N'Export Licence'` branch.
- When `@IncludeTotalCount = 0`, the Export Licence branch now:
  - returns grouped rows without `COUNT(*) OVER()`;
  - fetches the requested page size, with the backend sending `PageSize + 1`;
  - returns `TotalCount = NULL` so the C# wrapper uses estimated pagination.
- Updated `Backend/StoredProcedureToLinq/sp_HSCodeReport.StoredProcedure.cs` to pass `@IncludeTotalCount`.
- Updated `Backend/StoredProcedureToLinq/sp_HSCodeReport.cs` to return `CreateFastPageFromRows(...)` when `IncludeTotalCount=false`.
- Deployed updated `StoredProcedureMigrations/sp_HSCodeReport_pagination.sql` to `TradeNetDB`.

Latest retest:

- DB fast-page test:
  - `@IncludeTotalCount = 0`
  - `@PageSize = 21`
  - Returned rows.
  - SQL elapsed time: 1,192 ms.
- DB exact-count test:
  - `@IncludeTotalCount = 1`
  - `@PageSize = 20`
  - Returned rows.
  - TotalCount: 641 grouped rows.
  - SQL elapsed time: 948 ms.
- Focused backend/controller test:
  - Temporary xUnit test called `ExportLicenceByHSCodeReportController.Post(...)`.
  - `IncludeTotalCount = false`
  - Result returned rows.
  - Test passed: 1/1.
  - Temporary test file was removed after verification.

Status: done for Priority 1.

## Active Task Log - Export Licence Total Value & Licences Report

Started: 2026-06-07

Scope:

- Owner: Wai Phyo
- Stored procedure: `dbo.sp_ExportLicenceDetailReport_pagination`
- Local SQL file: `StoredProcedureMigrations/sp_ExportLicenceDetailReport_pagination.sql`
- Controller: `Backend/Controllers/Report/ExportLicenceTotalValueLicencesReportController.cs`

Next checks:

1. Identify controller request shape and FormType/category parameters.
2. Deploy/current-test `sp_ExportLicenceDetailReport_pagination` in `TradeNetDB`.
3. Execute one-day test for the total value/licences report path.
4. If slow, patch only the relevant branch/path.

Current observation:

- Controller uses `Type = "Oversea"` and calls `sp_ExportLicenceDetailReport_Fast.CreateAggregateResultAsync(...)`.
- The aggregate path currently loads all detail rows from `dbo.sp_ExportLicenceDetailReport_Pagination` and groups in C#.
- First check is DB stored procedure speed; if DB is fast but API is slow, the fix should be a targeted SQL-side aggregate path for this Wai Phyo-owned Total Value report.

Result:

- Deployed current `StoredProcedureMigrations/sp_ExportLicenceDetailReport_pagination.sql` to `TradeNetDB`.
- One-day detail procedure test timed out after 120 seconds:
  - `@Type = N'Oversea'`
  - `@FromDate = '2023-04-03'`
  - `@ToDate = '2023-04-03'`
  - `@PageSize = 20`
  - `@IncludeTotalCount = 1`
- Root cause: the detail procedure pulls expensive detail fields that Total Value does not need.
- Direct grouped SQL by `Currency` returned data in 364 ms for the same one-day filter.
- Added dedicated SQL file:
  - `StoredProcedureMigrations/sp_ExportLicenceTotalValueReport_pagination.sql`
- Deployed `dbo.sp_ExportLicenceTotalValueReport_Fast_pagination` to `TradeNetDB`.
- Updated `sp_ExportLicenceDetailReport_Fast` so only this path uses the new stored procedure:
  - `dimension == TotalValue`
  - `Type == "Oversea"`
  - `includeSakhan == false`
- Initial file-deployed procedure timed out inside the stored procedure even though the same query was fast ad hoc.
- Final fix:
  - Rewrote the procedure file to the proven fast body.
  - The procedure first materializes filtered `ExportLicence` IDs into `#LicenceIds`.
  - Then it joins only those IDs to `ExportLicenceItem` and groups by `Currency`.
  - The backend wrapper now calls `dbo.sp_ExportLicenceTotalValueReport_Fast_pagination`.
- Build check:
  - `dotnet build Backend\API.csproj --no-restore -p:UseAppHost=false -o .codex\build-check-totalvalue\api`
  - Passed with existing warnings.
- DB verification:
  - `@Type = N'Oversea'`
  - `@FromDate = '2023-04-03'`
  - `@ToDate = '2023-04-03'`
  - `@PageSize = 20`
  - `@IncludeTotalCount = 1`
  - Returned `USD`, `NoOfLicences = 738`, `TotalValue = 113460828.0406`, `TotalCount = 1`.
  - SQL elapsed time: 747 ms.
- API/controller verification:
  - Temporary focused xUnit test called `ExportLicenceTotalValueLicencesReportController.Post(...)`.
  - Test passed: 1/1.
  - Temporary test file was removed after verification.

Status: done.

## Active Task Log - Border Import Licence Amendment Report

Started: 2026-06-07

Scope:

- Owner: Wai Phyo
- Stored procedure: `dbo.sp_AmendReport_pagination`
- Local SQL file: `StoredProcedureMigrations/sp_AmendReport_pagination.sql`
- Controller: `Backend/Controllers/Report/BorderImportLicenceAmendmentReportController.cs`
- FormType: `Border Import Licence`

Next checks:

1. Execute the pagination procedure for `@FormType = N'Border Import Licence'`.
2. Check whether matching source rows exist in `BorderImportLicence`.
3. If source rows do not exist, record as valid no-data.
4. If source rows exist but procedure returns no rows, patch only the `Border Import Licence` branch.

Result:

- Source DB check found matching rows:
  - `ApplyType = N'Amend'`
  - `Status = N'Approved'`
  - `CardType = N'Pa Tha Ka'`
  - Broad range `2023-01-01` to `2026-06-30`
  - Total source rows: 1,892.
- Problem found in `StoredProcedureMigrations/sp_AmendReport_pagination.sql`:
  - Pa Tha Ka branch used `BorderImportLicence.ExportImportSectionId = ... BorderImportLicence.SakhanId ...`
  - Correct filter is `BorderImportLicence.SakhanId = ...`
- Fixed the typo in both the total-count query and the page query.
- Deployed `StoredProcedureMigrations/sp_AmendReport_pagination.sql` to `TradeNetDB`.
- Broad DB test returned data:
  - TotalCount: 1,892
  - Elapsed: 12,056 ms.
- One-day DB test returned data:
  - `@FromDate = '2023-01-02'`
  - `@ToDate = '2023-01-02'`
  - TotalCount: 4
  - Elapsed: 737 ms.

Status: fixed in DB. Needs frontend/API retest.

## Active Task Log - Border Import Licence Voucher Report

Started: 2026-06-07

Scope:

- Owner: Wai Phyo
- Stored procedure: `dbo.sp_VoucherReport_pagination`
- Local SQL file: `StoredProcedureMigrations/sp_VoucherReport_pagination.sql`
- Controller: `Backend/Controllers/Report/BorderImportLicenceVoucherReportController.cs`
- FormType: `Border Import Licence`

Problem found:

- Frontend config defaults `ApplyType` to an empty string.
- SQL required `ApplyType = @ApplyType`, so an empty request returned no data.
- SQL used `PaymentDate <= @ToDate`, which can miss the selected day when the frontend sends a date at midnight.
- Exact-count mode was slow because the query did not filter `AccountTransaction.TransactionFormType`.

Fix applied:

- Patched only the `Border Import Licence` branch in `StoredProcedureMigrations/sp_VoucherReport_pagination.sql`.
- Empty ApplyType now means all apply types:
  - `(@ApplyType='' OR BorderImportLicence.ApplyType=@ApplyType)`
- Date filtering now includes the whole selected ToDate:
  - `PaymentDate < DATEADD(day, 1, @ToDate)`
- Added existing index hint:
  - `AccountTransaction WITH (INDEX([NonClusteredIndex-AccountTransaction-125044]))`
- Added:
  - `AccountTransaction.TransactionFormType='Border Import Licence'`
- Added `MAXDOP 1` for this branch to avoid expensive parallel plans.
- Deployed `StoredProcedureMigrations/sp_VoucherReport_pagination.sql` to `TradeNetDB`.

Result:

- One-day exact-count DB test:
  - `@FormType = N'Border Import Licence'`
  - `@FromDate = '2023-07-13'`
  - `@ToDate = '2023-07-13'`
  - `@ApplyType = N''`
  - `@PageSize = 20`
  - `@IncludeTotalCount = 1`
- DB returned rows.
- TotalCount: 1,210.
- SQL elapsed time after fix: 2,786 ms.

Status: fixed in DB. Needs frontend/API retest.

## Active Task Log - Border Import Licence New Report

Started: 2026-06-07

Scope:

- Owner: Wai Phyo
- Stored procedure: `dbo.sp_NewReport_pagination`
- Local SQL file: `StoredProcedureMigrations/sp_NewReport_pagination.sql`
- Controller: `Backend/Controllers/Report/BorderImportLicenceNewReportNewReportController.cs`
- FormType: `Border Import Licence`

Next checks:

1. Execute the pagination procedure for `@FormType = N'Border Import Licence'`.
2. Confirm whether the sheet note `ok` is correct after the senior pull.
3. If DB returns data within target, mark this verified.

Problem found:

- Source rows existed for the selected day, but the procedure returned no rows when `@ToDate` was sent as a date at midnight.
- `auto = auto` style filtering can also exclude rows where `auto` is NULL when the frontend sends an empty Auto filter.
- After fixing date/auto, the separate exact-count query timed out; fast-page mode was already fast.

Fix applied:

- Patched only the `Border Import Licence` branch in `StoredProcedureMigrations/sp_NewReport_pagination.sql`.
- Date filtering now includes the whole selected ToDate:
  - `CreatedDate < DATEADD(day, 1, @ToDate)`
- Empty Auto now means all Auto values:
  - `(@auto='' OR BorderImportLicence.auto=@auto)`
- Replaced the separate count query with `COUNT(*) OVER()` on the filtered union to avoid the bad count/page plan interaction.
- Removed the internal helper count column from the final output.
- Deployed `StoredProcedureMigrations/sp_NewReport_pagination.sql` to `TradeNetDB`.

Result:

- One-day exact-count DB test:
  - `@FormType = N'Border Import Licence'`
  - `@FromDate = '2023-08-01'`
  - `@ToDate = '2023-08-01'`
  - `@Auto = N''`
  - `@PageSize = 20`
  - `@IncludeTotalCount = 1`
- DB returned rows.
- TotalCount: 824.
- SQL elapsed time after fix: 558 ms.

Status: fixed in DB. Needs frontend/API retest.

# Wai Phyo Report DB Work Plan

Updated: 2026-06-11

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

Last updated: 2026-06-11

### Next Target

Next target: customer-complaint parity pass for Wai Phyo Export Licence reports.

Reason: the same customer-complaint pass now needs to be applied to Export Licence reports. Per `AGENTS.md`, compare old Tradenet 2.0 Admin filters/columns before changing code.

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
| Export Licence Voucher Report | `dbo.sp_VoucherReport_pagination` | DB data-show passed | 1,856 ms fast page | Data-first branch returns rows for date-only frontend filters; Currency/TotalAmount are temporarily blank for this branch to avoid item-total lookup timeout. |
| Export Licence New Report | `dbo.sp_NewReport_pagination` | DB data-show passed | 10,246 ms fast page | Date-only frontend filters now include the full selected day and return rows. |
| Border Import Licence Amendment Report | `dbo.sp_AmendReport_pagination` | API/controller retest passed | 1,618 ms API | Fixed wrong Sakhan filter comparing section id to Sakhan id; controller returns `totalCount=4`, `pageCount=4`. |
| Border Import Licence Voucher Report | `dbo.sp_VoucherReport_pagination` | API/controller retest passed | 4,394 ms API / 3,114 ms DB | Empty ApplyType now means all; ToDate includes whole day; exact count moved outside dynamic page query; added `TransactionFormType` filter and existing index hint. |
| Border Import Licence New Report | `dbo.sp_NewReport_pagination` | API/controller retest passed | 1,345 ms API | ToDate includes whole day; empty Auto means all; controller returns `totalCount=824`, `pageCount=20`. |
| Export Licence By HS Code Report | `dbo.sp_HSCodeReport_pagination` | DB + API/controller retest passed | 1,192 ms DB fast page / 948 ms DB exact count / 1 focused test passed | Added `@IncludeTotalCount`; Export Licence branch can now skip `COUNT(*) OVER()` for fast-page requests; backend fetches `PageSize + 1` and returns estimated pagination when requested. |
| Export Licence Total Value & Licences Report | `dbo.sp_ExportLicenceTotalValueReport_Fast_pagination` | DB + API/controller retest passed | 747 ms DB exact count / 1 focused test passed | Replaced the full detail-row aggregation path with a dedicated currency aggregate procedure that materializes filtered licence IDs before joining items. |

### Not Done / Pending

| Report | Current status | Why it is still pending | Next action |
|---|---|---|---|
| Border Export Permit Amendment Report | Data-show + supported parity fixes done | Section dropdown/title/extra HSCode fixed; DB returns 2 rows in 246 ms; RDLC footer totals still pending | Retest frontend after dependency install; decide later on amendment currency footer totals. |
| Border Export Permit Extension Report | Data-show + supported parity fixes done | Section dropdown/title/currency footer wiring done; DB returns 3 rows in 192 ms | Frontend retest after dependency install. |
| Border Export Permit Cancellation Report | Data-show + supported parity fixes done | Section dropdown/title/currency footer wiring done; extra HSCode removed; DB returns 2 rows in 60 ms | Frontend retest after dependency install. |
| Border Export Permit By HS Code Report | Data-show + supported parity fixes done | Title, section filter, Start/End dropdown, CompanyName removal, backend section mapping done; DB returns 74 grouped rows in 279 ms | HS Code detail drilldown still pending because the new frontend has no detail route/config. |
| Border Export Permit Voucher Report | Data-show + supported parity fixes done | Title, section dropdown, dynamic header resolver fixed; DB returns 42 rows in 556 ms | Total amount footer still pending. |
| Border Export Permit Actual Amendment Report | Valid no-data + supported parity fixes done | `TradeNetDB` has 0 approved Border Export Permit `Actual Amend` rows; section/title/extra HSCode fixed | Retest frontend after dependency install; no SQL fix unless source data is expected. |
| Border Export Permit New Report (New Report) | Data-show + supported parity fixes done | Sakhan-specific DB search returns data; section/title/Auto filter/Auto column fixed | Currency-wise footer totals still pending. |
| Export Licence Voucher Report | Data shows; item totals intentionally skipped for now | Currency/TotalAmount are blank in data-first mode | Revisit only after all tables show data. |
| Export Licence New Report | Data shows | Performance can still be improved later | Revisit only if frontend still fails. |
| Export Licence Amendment Report | DB data-show passed | If frontend still fails, the DB procedure is not the blocker | Retest API/frontend request payload. |
| Export Licence Extension Report | DB data-show passed | If frontend still fails, the DB procedure is not the blocker | Retest API/frontend request payload. |
| Export Licence New Report quota | DB output fixed | Real quota source does not exist on `ExportLicence`; old procedure also did not select it | Retest frontend; quota should render blank instead of `N/A`. |

### Remaining Wai Phyo Work

This is the current short list after the senior pull and count audit.

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
| Export Licence Voucher Report | Data shows in DB fast-page mode; Currency/TotalAmount are blank for now | Frontend/API retest. |
| Export Licence New Report (New Report) | Data shows in DB fast-page mode | Frontend/API retest. |
| Border Import Licence New Report (New Report) | Data already shows; count optimization intentionally paused | No action unless frontend fails. |

Priority 3 - no-data validation:

| Report | Why still left | Next check |
|---|---|---|
| Export Licence Actual Amendment Report | Source rows exist and procedure returns rows | Frontend/API retest if page still shows no data. |
| Border Import Permit Amendment Report | Source row exists and procedure returns row | Frontend/API retest if page still shows no data. |

### DB Deployment Status

Applied to `TradeNetDB`:

- `dbo.sp_ExportLicenceDetailReport_Pagination`
- `dbo.sp_ExportLicenceTotalValueReport_Fast_pagination`
- `dbo.sp_AmendReport_pagination`
- `dbo.sp_VoucherReport_pagination`
- `dbo.sp_NewReport_pagination`
- `dbo.sp_HSCodeReport_pagination`

Index status:

- Created and deployed `StoredProcedureMigrations/Indexes/ExportLicenceHSCodeReport_indexes.sql`.
- New indexes:
  - `IX_ExportLicence_HSCodeReport_LicenceDate`
  - `IX_ExportLicenceItem_HSCodeReport_Licence`
  - `IX_AccountTransaction_ExportLicenceVoucher`
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
| Export Licence Voucher Report | Returned rows | 2023-04-03 | 1,856 ms | Data-first path skips Currency/TotalAmount item lookups for now so rows load. |
| Export Licence Actual Amendment Report | Returned rows | 2026-04-01 | 814 ms | Source rows exist: 5,476 approved actual-amend rows overall; procedure returned 2 rows for 2026-04-01. |
| Border Import Permit Amendment Report | Returned row | 2026-05-22 | 840 ms | Source row exists: 1 approved amend row overall; procedure returned it. |

Current tradeoff:

- Export Licence Voucher rows now show first.
- Currency/TotalAmount are temporarily blank for Export Licence Voucher until there is enough time to tune item-total lookup safely.

### Border Export Permit

| Report | Stored procedure | Owner | Deadline | Sheet status | Current note | Next action |
|---|---|---|---|---|---|---|
| Border Export Permit Amendment Report | `dbo.sp_AmendReport` | Wai Phyo | 5.June.2026 | OK | DB smoke passed through amend procedure | No action unless frontend regression appears |
| Border Export Permit Extension Report | `dbo.sp_ExtensionReport` | Wai Phyo | 5.June.2026 | OK | DB smoke passed after extension date fix | No action unless frontend regression appears |
| Border Export Permit Cancellation Report | `dbo.sp_CancelReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Export Permit By HS Code Report | `dbo.sp_HSCodeReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Export Permit Voucher Report | `dbo.sp_VoucherReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Export Permit Actual Amendment Report | `dbo.sp_ActualAmendReport` | Wai Phyo | 5.June.2026 | OK | Working / may return no data depending on DB | No action unless frontend fails |
| Border Export Permit New Report (New Report) | `dbo.sp_NewReport` | Wai Phyo | 10.June.2026 | Fixed in DB | DB returns data quickly | Needs frontend retest |

### Export Licence

| Report | Stored procedure | Owner | Deadline | Sheet status | Current note | Next action |
|---|---|---|---|---|---|---|
| Export Licence Amendment Report | `dbo.sp_AmendReport` | Wai Phyo | 5.June.2026 | OK | DB smoke passed; `HSCode`, `Currency`, and `Amount` return | Retest API/frontend only if UI still fails |
| Export Licence Extension Report | `dbo.sp_ExtensionReport` | Wai Phyo | 5.June.2026 | OK | DB smoke passed; date-only filters now return rows | Retest API/frontend only if UI still fails |
| Export Licence Cancellation Report | `dbo.sp_CancelReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Export Licence By HS Code Report | `dbo.sp_HSCodeReport` | Wai Phyo | 10.June.2026 | Fixed | DB and focused controller retest passed | Done for Priority 1 |
| Export Licence Total Value & Licences Report | `dbo.sp_ExportLicenceDetailReport` | Wai Phyo | 5.June.2026 | Fixed | DB and focused controller retest passed | Done |
| Export Licence Voucher Report | `dbo.sp_VoucherReport` | Wai Phyo | 10.June.2026 | Fixed for one-day target | DB returns data; one-day exact count is fast | Wide exact count still risky |
| Export Licence Actual Amendment Report | `dbo.sp_ActualAmendReport` | Wai Phyo | 5.June.2026 | OK | No data | Verify DB has matching rows before changing SQL |
| Export Licence New Report (New Report) | `dbo.sp_NewReport` | Wai Phyo | 10.June.2026 | Fixed for one-day target | DB returns data; quota now returns blank instead of `N/A` because source table has no quota column | Frontend retest |

### Border Import Licence

| Report | Stored procedure | Owner | Deadline | Sheet status | Current note | Next action |
|---|---|---|---|---|---|---|
| Border Import Licence Amendment Report | `dbo.sp_AmendReport` | Wai Phyo | 10.June.2026 | Fixed in DB | DB smoke passed through amend procedure | Needs frontend/API retest only if UI still fails |
| Border Import Licence Extension Report | `dbo.sp_ExtensionReport` | Wai Phyo | 5.June.2026 | OK | DB smoke passed after extension date fix | No action unless frontend regression appears |
| Border Import Licence Cancellation Report | `dbo.sp_CancelReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Import Licence By HS Code Report | `dbo.sp_HSCodeReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Import Licence Voucher Report | `dbo.sp_VoucherReport` | Wai Phyo | 10.June.2026 | Fixed in DB | DB returns data with exact count in target | Needs frontend/API retest |
| Border Import Licence Actual Amendment Report | `dbo.sp_ActualAmendReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Import Licence New Report (New Report) | `dbo.sp_NewReport` | Wai Phyo | 10.June.2026 | Fixed in DB | DB returns data with exact count in target | Needs frontend/API retest |

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

Result:

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

# Wai Phyo Report DB Work Plan

Updated: 2026-06-07

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

Target:

- One-day filters should return in under 30 seconds.
- Prefer under 10 seconds when practical.

## Current Summary Dashboard

Last updated: 2026-06-07

### Next Target

Next target: Priority 2 cleanup - Export Licence Voucher wide-range retest after DB rollback clears.

Reason: Priority 1 is finished, and Export Licence Total Value & Licences now passes DB/API verification. Export Licence Voucher now has a targeted payment index and safer date/form filters, but the broad retest is blocked by a long-running SQL Server rollback session.

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
| Export Licence Voucher Report | `dbo.sp_VoucherReport_pagination` | DB fixed for one-day target; broad retest blocked by DB rollback | 247 ms exact count / 127 ms fast page; wide retest blocked after 60 sec | Replaced slow broad path with direct `ExportLicenceItem` aggregation after paging; added `TransactionFormType='Export Licence'`, inclusive ToDate handling, and targeted payment index script. |
| Export Licence New Report | `dbo.sp_NewReport_pagination` | DB fixed for one-day target | 159 ms DB | One-day exact-count path returns data. Wide exact-count range is still risky. |
| Border Import Licence Amendment Report | `dbo.sp_AmendReport_pagination` | API/controller retest passed | 1,618 ms API | Fixed wrong Sakhan filter comparing section id to Sakhan id; controller returns `totalCount=4`, `pageCount=4`. |
| Border Import Licence Voucher Report | `dbo.sp_VoucherReport_pagination` | API/controller retest passed | 4,394 ms API / 3,114 ms DB | Empty ApplyType now means all; ToDate includes whole day; exact count moved outside dynamic page query; added `TransactionFormType` filter and existing index hint. |
| Border Import Licence New Report | `dbo.sp_NewReport_pagination` | API/controller retest passed | 1,345 ms API | ToDate includes whole day; empty Auto means all; controller returns `totalCount=824`, `pageCount=20`. |
| Export Licence By HS Code Report | `dbo.sp_HSCodeReport_pagination` | DB + API/controller retest passed | 1,192 ms DB fast page / 948 ms DB exact count / 1 focused test passed | Added `@IncludeTotalCount`; Export Licence branch can now skip `COUNT(*) OVER()` for fast-page requests; backend fetches `PageSize + 1` and returns estimated pagination when requested. |
| Export Licence Total Value & Licences Report | `dbo.sp_ExportLicenceTotalValueReport_Fast_pagination` | DB + API/controller retest passed | 747 ms DB exact count / 1 focused test passed | Replaced the full detail-row aggregation path with a dedicated currency aggregate procedure that materializes filtered licence IDs before joining items. |

### Not Done / Pending

| Report | Current status | Why it is still pending | Next action |
|---|---|---|---|
| Export Licence Voucher Report | One-day fixed, wide retest blocked | SQL Server session 64 is in `KILLED/ROLLBACK` and blocking schema stability locks; timings are not trustworthy until it clears | Retest wide fast-page and exact-count after rollback clears. |
| Export Licence New Report | One-day fixed, wide exact-count still risky | Three-year exact-count test timed out after 120 seconds | Use one-day target first; only optimize wide exact count if business needs it. |
| Export Licence Actual Amendment Report | No-data still needs source-row verification | Could be valid no-data or filter mismatch | Check source DB rows before changing SQL. |
| Border Import Permit Amendment Report | No-data still needs source-row verification | Could be valid no-data or filter mismatch | Check source DB rows before changing SQL. |

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

Priority 2 - performance/count cleanup:

| Report | Why still left | Next check |
|---|---|---|
| Export Licence Voucher Report | One-day exact count is fast; wide retest currently blocked by DB rollback | Retest after session 64 rollback clears. |
| Export Licence New Report (New Report) | One-day exact count is fast; multi-year exact count timed out | Only optimize wide exact count if business requires it. |
| Border Import Licence New Report (New Report) | Uses `COUNT(*) OVER()` even when fast-page mode could skip exact count | Make count conditional if frontend/API uses `IncludeTotalCount = false`. |

Priority 3 - no-data validation:

| Report | Why still left | Next check |
|---|---|---|
| Export Licence Actual Amendment Report | Marked OK/no data, but source rows have not been rechecked after latest pull | Check base DB rows before changing SQL. |
| Border Import Permit Amendment Report | Marked OK/no data, but source rows have not been rechecked after latest pull | Check base DB rows before changing SQL. |

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

### Border Export Permit

| Report | Stored procedure | Owner | Deadline | Sheet status | Current note | Next action |
|---|---|---|---|---|---|---|
| Border Export Permit Amendment Report | `dbo.sp_AmendReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Export Permit Extension Report | `dbo.sp_ExtensionReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Export Permit Cancellation Report | `dbo.sp_CancelReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Export Permit By HS Code Report | `dbo.sp_HSCodeReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Export Permit Voucher Report | `dbo.sp_VoucherReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Export Permit Actual Amendment Report | `dbo.sp_ActualAmendReport` | Wai Phyo | 5.June.2026 | OK | Working / may return no data depending on DB | No action unless frontend fails |
| Border Export Permit New Report (New Report) | `dbo.sp_NewReport` | Wai Phyo | 10.June.2026 | Fixed in DB | DB returns data quickly | Needs frontend retest |

### Export Licence

| Report | Stored procedure | Owner | Deadline | Sheet status | Current note | Next action |
|---|---|---|---|---|---|---|
| Export Licence Amendment Report | `dbo.sp_AmendReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Export Licence Extension Report | `dbo.sp_ExtensionReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Export Licence Cancellation Report | `dbo.sp_CancelReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Export Licence By HS Code Report | `dbo.sp_HSCodeReport` | Wai Phyo | 10.June.2026 | Fixed | DB and focused controller retest passed | Done for Priority 1 |
| Export Licence Total Value & Licences Report | `dbo.sp_ExportLicenceDetailReport` | Wai Phyo | 5.June.2026 | Fixed | DB and focused controller retest passed | Done |
| Export Licence Voucher Report | `dbo.sp_VoucherReport` | Wai Phyo | 10.June.2026 | Fixed for one-day target | DB returns data; one-day exact count is fast | Wide exact count still risky |
| Export Licence Actual Amendment Report | `dbo.sp_ActualAmendReport` | Wai Phyo | 5.June.2026 | OK | No data | Verify DB has matching rows before changing SQL |
| Export Licence New Report (New Report) | `dbo.sp_NewReport` | Wai Phyo | 10.June.2026 | Fixed for one-day target | DB returns data; one-day exact count is fast | Wide exact count still risky |

### Border Import Licence

| Report | Stored procedure | Owner | Deadline | Sheet status | Current note | Next action |
|---|---|---|---|---|---|---|
| Border Import Licence Amendment Report | `dbo.sp_AmendReport` | Wai Phyo | 10.June.2026 | Fixed in DB | DB returns data after Sakhan filter fix | Needs frontend/API retest |
| Border Import Licence Extension Report | `dbo.sp_ExtensionReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Import Licence Cancellation Report | `dbo.sp_CancelReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Import Licence By HS Code Report | `dbo.sp_HSCodeReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Import Licence Voucher Report | `dbo.sp_VoucherReport` | Wai Phyo | 10.June.2026 | Fixed in DB | DB returns data with exact count in target | Needs frontend/API retest |
| Border Import Licence Actual Amendment Report | `dbo.sp_ActualAmendReport` | Wai Phyo | 5.June.2026 | OK | Working | No action unless regression appears |
| Border Import Licence New Report (New Report) | `dbo.sp_NewReport` | Wai Phyo | 10.June.2026 | Fixed in DB | DB returns data with exact count in target | Needs frontend/API retest |

### Border Import Permit Shared With Bran

Senior note: this area was updated and should be tested again after pulling the latest commit.

| Report | Stored procedure | Owner | Deadline | Sheet status | Current note | Next action |
|---|---|---|---|---|---|---|
| Border Import Permit Amendment Report | `dbo.sp_AmendReport` | Wai Phyo, Bran | 5.June.2026 | OK | No data | Verify DB has matching rows |
| Border Import Permit Extension Report | `dbo.sp_ExtensionReport` | Wai Phyo, Bran | 10.June.2026 | Fixed in DB/API smoke | DB returns data; endpoint executes | Done |
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

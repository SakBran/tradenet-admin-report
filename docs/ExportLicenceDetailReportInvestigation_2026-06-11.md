# Export Licence Detail Report Investigation

Date: 2026-06-11

## Scope

Checked `ExportLicenceDetailReport` from UI config through controller request mapping and SQL query execution:

- UI page: `Frontend/src/Report/Page/ExportLicenceDetailReport.tsx`
- UI config: `Frontend/src/Report/config/reportConfigs.ts`
- Controller: `Backend/Controllers/Report/ExportLicenceDetailReportController.cs`
- API result/SQL wrapper: `Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReport*.cs`
- Pagination SQL: `StoredProcedureMigrations/sp_ExportLicenceDetailReport_pagination.sql`
- Column parity doc: `docs/ReportColumnComparison.md`

## Result

The current "load failed / not showing data" behavior is reproduced at the database stored-procedure layer, not at the UI column layer.

`dbo.sp_ExportLicenceDetailReport_Pagination` timed out on the live `TradeNetDB` even with:

- `@Type = 'Oversea'`
- date range `2026-04-01` to `2026-05-31 23:59:59`
- `@PageIndex = 0`
- `@PageSize = 5`
- both `@IncludeTotalCount = 1` and `@IncludeTotalCount = 0`

Both executions returned `Timeout expired` after 60 seconds.

## Data Check

The live DB does contain approved oversea export licence data for the tested period.

Sample latest approved/new export licences:

| CreatedDate | IssuedDate | ExportLicenceNo | Status | ApplyType |
|---|---|---|---|---|
| 2026-05-21 16:04:16 | 2026-05-21 16:04:16 | OVSEL12627000005 | Approved | New |
| 2026-05-21 15:57:03 | 2026-05-21 15:57:03 | OVSEL12627000004 | Approved | New |
| 2026-04-30 10:19:39 | 2026-04-30 10:19:39 | OVSEL12627000003 | Approved | New |
| 2026-04-29 13:34:49 | 2026-04-29 13:34:49 | OVSEL12627000002 | Approved | New |
| 2026-04-29 11:02:59 | 2026-04-29 11:02:59 | OVSEL12627000001 | Approved | New |

The equivalent simplified join query for `2026-04-01` to `2026-05-31` returned quickly with `RowCnt = 6`, so this is not "no data".

## Column Contract Check

UI columns match the documented old RDLC parity:

- `docs/ReportColumnComparison.md` says old columns: 28
- New UI columns: 28 including generated `No`
- Missing columns: none
- Extra columns: none

The API DTO also contains the fields used by the UI:

- `applicationDate`
- `applicationNo`
- `licenceNo`
- `licenceDate`
- `companyRegistrationNo`
- `companyName`
- `buyerName`
- `buyerAddress`
- `buyerCountry`
- `portofExport`
- `portofDischarge`
- `lastDate`
- `methodName`
- `consignedCountry`
- `countryofOrigin`
- `destinationCountry`
- `hsCode`
- `hsDescription`
- `unit`
- `price`
- `quantity`
- `amount`
- `currency`
- `commodityType`
- `conditions`

The `Company Address` UI column is not a direct API field, but it correctly uses fallback fields:

- `unitLevel`
- `streetNumberStreetName`
- `quarterCityTownship`
- `state`
- `country`
- `postalCode`

## Request Mapping Check

The UI posts to:

- API route: `ExportLicenceDetailReport`
- Excel route: `ExportLicenceDetailReport/Excel`

Controller maps every relevant UI filter into `sp_ExportLicenceDetailReportRequest`, but it intentionally overrides:

```text
Type = "Oversea"
```

So the UI `Type` text filter does not control this report. It is harmless for loading, but confusing.

## Main Failure Cause

The report load fails because `dbo.sp_ExportLicenceDetailReport_Pagination` does not return within the API timeout window on the live DB.

The current backend path calls:

```text
sp_ExportLicenceDetailReport_Fast.CreatePagedResultAsync
  -> ExecuteAsync
  -> EXEC dbo.sp_ExportLicenceDetailReport_Pagination
```

A timeout in that SQL call will surface in the UI as load failure.

Live DB also showed high pressure during testing:

- 63 active requests in `sys.dm_exec_requests`
- many long-running `KILLED/ROLLBACK` sessions
- top sessions had `EXECSYNC` waits and very high elapsed time

That server condition likely makes the report procedure much more fragile.

## Secondary Issues

These are not the immediate timeout cause, but they should be fixed:

1. `initialSortColumn` is `PaThaKaTypeId`, but the stored procedure allow-list does not include `PaThaKaTypeId`. SQL silently falls back to `LicenceDate, LicenceNo`.
2. `ExportImportSectionId`, `ExportImportMethodId`, and `ExportImportIncotermId` use generic lookups in `GenericReportPage`. Previous parity notes say Export Licence Detail should not leak import/border/non-oversea options.
3. `SakhanId` appears in the oversea Export Licence Detail UI, but the controller forces `Type = "Oversea"` and the oversea SQL branch returns `NULL` Sakhan fields. The old detail form did not need this for oversea.
4. Pagination SQL uses `< DATEADD(day, 1, @ToDate)`. Because the UI sends an end-of-day `ToDate`, this can include almost one extra day. This causes possible extra rows, not load failure.

## Deep Test Added

Added:

```text
Backend.Tests/ExportLicenceDetailReportContractTests.cs
```

The test checks:

- UI `dataIndex` columns are backed by API result fields or declared fallback fields.
- UI filters are accepted by `ExportLicenceDetailReportRequest`.
- Local pagination SQL contains the columns required by `sp_ExportLicenceDetailReportRow`, including `ApplicationNo`, `ApplicationDate`, `CommodityType`, and `TotalCount`.

## Verification

Passed:

```text
dotnet test Backend.Tests\Backend.Tests.csproj --filter ExportLicenceDetailReportContractTests --no-restore -p:BaseOutputPath=C:\Data_D\Projects\Reports\tradenet-admin-report\artifacts\test-bin\ -p:BaseIntermediateOutputPath=C:\Data_D\Projects\Reports\tradenet-admin-report\artifacts\test-obj\
```

Passed:

```text
npm test
```

Frontend result:

```text
2 test files passed
13 tests passed
```

Note: normal backend test output path was blocked because local process `API (30248)` was locking `Backend/bin/Debug/net8.0/API.dll`, so the focused test was run with alternate output/intermediate directories.

## Recommended Fix

Priority 1: replace or optimize `dbo.sp_ExportLicenceDetailReport_Pagination`.

Recommended direction:

1. Avoid dynamic SQL for the normal default sort path.
2. Page first by filtered licence/item keys, then join and resolve display columns for the requested page.
3. Replace per-row XML CSV lookups for `PortofExport` and `DestinationCountry` with the existing C# cached lookup resolver pattern, or use a set-based SQL split/string aggregation strategy.
4. Keep `@IncludeTotalCount = 0` truly fast; it should not scan/sort the full report just to return page 1.
5. Add scoped export licence lookups for section/method/incoterm in the UI config after the query is stable.

Until the stored procedure is fixed, UI changes alone will not make the report load reliably.

## Final Fix Applied

Implemented a separate V2 stored procedure path for the oversea `ExportLicenceDetailReport` page load. The controller name and API route stay unchanged, but the detail report no longer depends on the mixed oversea/border pagination procedure.

- `StoredProcedureMigrations/sp_ExportLicenceDetailReportV2_pagination.sql`
  - Creates `dbo.sp_ExportLicenceDetailReportV2_Pagination`.
  - Handles only oversea export licence detail data.
  - Has no `@Type` parameter and no `BorderExportLicence` branch.
  - Uses key-first pagination: page the matching `ExportLicence` / `ExportLicenceItem` keys first, then join display tables only for the requested page.
  - Keeps `IncludeTotalCount = false` fast by fetching `PageSize + 1` rows instead of counting the full result.
  - Resolves `PortofExport` and `DestinationCountry` CSV ids only after pagination.
  - Preserves requested sort order through a `PageOrder` value.

- `Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReportV2.cs`
  - Originally called only `dbo.sp_ExportLicenceDetailReportV2_Pagination`.
  - Uses explicit `SqlParameter` types for date, int, and registration-number filters.
  - Provides paged API and streaming Excel paths for this report only.

- `Backend/Controllers/Report/ExportLicenceDetailReportController.cs`
  - Keeps the existing controller class name and route.
  - Switches only Export Licence Detail to `sp_ExportLicenceDetailReportV2`.

The shared `sp_ExportLicenceDetailReport_Fast` path remains available to border and aggregate reports, so this fix does not change those report controllers.

Added supporting indexes:

- `StoredProcedureMigrations/Indexes/ExportLicenceDetailReport_indexes.sql`
  - `IX_ExportLicence_Report_NewDetail_Page`
  - `IX_ExportLicenceItem_Report_Licence_Page`

These indexes are idempotent and are designed for the new key-first page shape.

## Additional Test Added

Added:

```text
Backend.Tests/ExportLicenceDetailReportLiveDbTests.cs
```

This live integration test is skipped unless `TRADENET_REPORT_TEST_CONNECTION_STRING` is set. It calls the real controller with:

- `FromDate = 2026-04-01`
- `ToDate = 2026-05-31 23:59:59`
- `PageSize = 5`
- `IncludeTotalCount = false`

## Verification After Fix

Passed:

```text
dotnet build Backend.Tests\Backend.Tests.csproj --no-restore -p:OutDir=C:\Data_D\Projects\Reports\tradenet-admin-report\artifacts\isolated-test\ -v:minimal
```

Passed:

```text
dotnet vstest artifacts\isolated-test\Backend.Tests.dll --Tests:Backend.Tests.ExportLicenceDetailReportContractTests.Ui_columns_are_backed_by_export_licence_detail_api_result_fields,Backend.Tests.ExportLicenceDetailReportContractTests.Ui_filters_are_accepted_by_export_licence_detail_request,Backend.Tests.ExportLicenceDetailReportContractTests.Pagination_stored_procedure_returns_columns_required_by_api_row
```

The contract test now verifies that the V2 procedure file has no `@Type` parameter and no `BorderExportLicence` references.

Live DB test result:

```text
Backend.Tests.ExportLicenceDetailReportLiveDbTests.Detail_page_against_live_db_returns_first_page_without_stored_procedure_timeout
```

Still failed on the live server because the optimized page SQL was blocked by live database schema locks, not because of the report query shape.

Observed active blocker chain:

| Session | Program | Host | State | Wait | Blocking Session |
|---:|---|---|---|---|---:|
| 64 | SQLCMD | NIGHT | KILLED/ROLLBACK | EXECSYNC | 0 |
| 158 | .Net SqlClient Data Provider | TN2UAT | SELECT | LCK_M_SCH_S | 64 |
| 151 | Core Microsoft SqlClient Data Provider | DESKTOP-FMIK0S7 | SELECT | LCK_M_SCH_S | 158 |

Session `151` was the optimized `WITH PageKeys AS (...)` query from this fix. SQL Server blocked it on `LCK_M_SCH_S` behind session `158`, which was itself blocked by long-running `KILLED/ROLLBACK` session `64`.

Important: `NOLOCK` / read-uncommitted cannot bypass schema stability waits. The live DB must finish or clear the rollback/blocker chain before any application query touching the affected objects can reliably load.

## Deployment Notes

1. Deploy the backend fast-path code.
2. Apply `StoredProcedureMigrations/Indexes/ExportLicenceDetailReport_indexes.sql` to `TradeNetDB`.
3. Ask DBA/operator to investigate session `64` / `158` blocker chain if the live UI still times out.
4. Re-run the live integration test after the blocker clears.

## Follow-up Fixes Applied

Additional UI/data-accuracy fixes were applied after the V2 live test path was introduced:

- `Backend/Controllers/ReportLookupsController.cs`
  - Added oversea Export Licence scoped lookup routes:
    - `ReportLookups/exportLicenceSections`
    - `ReportLookups/exportLicenceMethods`
    - `ReportLookups/exportLicenceIncoterms`
  - Sections are filtered to active, not deleted, `Type = 'Export Licence'`, and `IsOversea`.
  - Methods/incoterms are filtered to active, not deleted, `Type = 'Export'`, and `IsOversea`.

- `Frontend/src/Report/config/reportConfigs.ts`
  - `ExportLicenceDetailReport` now uses those scoped lookup routes for Section, Method, and Incoterms.
  - Removed visible oversea-irrelevant/old-form-extra filters from the detail page: `Type`, `BuyerCountryId`, `CompanyRegistrationNo`, and `SakhanId`.
  - Drill-down request parameters remain supported by `GenericReportPage`, so hidden row parameters can still be carried into the API request when another report opens the detail report.
  - Changed the initial sort column to `licenceDate`, matching the old stored procedure's `ORDER BY ExportLicence.LicenceDate` intent.

- `Backend.Tests/ExportLicenceDetailReportContractTests.cs`
  - Added exact visible-filter assertions for the detail report.
  - Added scoped lookup route/source assertions so the generic unscoped lookup does not regress.
  - Added a guard that the runtime page-load query does not require `dbo.sp_ExportLicenceDetailReportV2_Pagination` to be deployed before the UI can load.

- `Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReportV2.cs`
  - The optimized V2 query now runs inline from the backend for UI page loads.
  - This keeps the same key-first SQL shape, but removes the immediate stored-procedure deployment dependency that could still cause `Could not find stored procedure` / load failure in an environment where only the app code was deployed.
  - `StoredProcedureMigrations/sp_ExportLicenceDetailReportV2_pagination.sql` is still useful as a DB-side artifact, but the UI result path no longer depends on it.

## Follow-up Verification

Passed:

```text
dotnet test Backend.Tests\Backend.Tests.csproj --filter ExportLicenceDetailReportContractTests --no-restore -p:BaseOutputPath=C:\Data_D\Projects\Reports\tradenet-admin-report\artifacts\test-bin\ -p:BaseIntermediateOutputPath=C:\Data_D\Projects\Reports\tradenet-admin-report\artifacts\test-obj\
```

Passed:

```text
dotnet build Backend.Tests\Backend.Tests.csproj --no-restore -p:OutDir=C:\Data_D\Projects\Reports\tradenet-admin-report\artifacts\live-isolated-test\ -v:minimal
```

Passed:

```text
npm.cmd test
```

Frontend result:

```text
2 test files passed
13 tests passed
```

Live DB verification now passes:

```text
dotnet vstest artifacts\live-isolated-test\Backend.Tests.dll --Tests:Backend.Tests.ExportLicenceDetailReportLiveDbTests.Detail_page_against_live_db_returns_first_page_without_stored_procedure_timeout
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Duration: 16 s
```

This confirms the configured live `TradeNetDB` can now return the first Export Licence Detail page through the optimized V2 controller path.

After removing the runtime stored-procedure dependency, both UI-style live requests pass:

```text
dotnet vstest artifacts\live-isolated-test\Backend.Tests.dll --Tests:Backend.Tests.ExportLicenceDetailReportLiveDbTests.Detail_page_against_live_db_returns_first_page_without_stored_procedure_timeout,Backend.Tests.ExportLicenceDetailReportLiveDbTests.Detail_page_exact_count_against_live_db_returns_without_stored_procedure_timeout
```

Result:

```text
Passed: 2, Failed: 0, Skipped: 0, Duration: 16 s
```

## Current Blocking Status

- No local `sqlcmd` process was running during the follow-up check.
- The previous blocker chain is not currently preventing the Export Licence Detail report from loading, because the live integration test completed successfully.
- No database session was force-killed in this follow-up. A force kill is only appropriate when a current blocking session is positively identified; killing a remote SQL session without an active blocker target can interrupt unrelated users or leave SQL Server in rollback longer.
- The installed `sqlcmd` client on this machine could not connect to the encrypted SQL Server endpoint (`Encryption not supported on the client`), so the direct DMV blocker snapshot could not be collected through `sqlcmd`. The application/.NET SQL client path was verified by the passing live report test.

# Export Licence Detail Report Performance Task

Date: 2026-06-11

## Scope

Report: `ExportLicenceDetailReport`

Problem: the detail UI can still run too long and fail to load when the result set is large. The performance work must keep UI data and API result data accurate while making first-page loads reliable.

## Check List

- [x] Confirm UI route uses `ExportLicenceDetailReport` and still renders the documented detail columns.
- [x] Confirm controller maps UI filters to oversea export licence detail request fields.
- [x] Keep the detail UI on paged API results instead of loading all matching rows.
- [x] Keep fast paging behavior: when `IncludeTotalCount = false`, request `PageSize + 1` rows and skip the full exact count.
- [x] Keep exact-count behavior available for the table's background count request.
- [x] Keep the table's background exact-count request enabled, but label the first fast-page count as an estimate until the exact count returns.
- [x] Avoid the old mixed oversea/border stored procedure dependency in the UI path.
- [x] Use targeted index hints only on narrow key seeks; avoid the old broad join path that timed out.
- [x] Add/keep indexes for the key-first page query:
  - `IX_ExportLicence_Report_NewDetail_Page`
  - `IX_ExportLicenceItem_Report_Licence_Page`
- [x] Add a live DB test target for a 3-day May 2025 slice:
  - `FromDate = 2025-05-01 00:00:00`
  - `ToDate = 2025-05-03 23:59:59`
  - `PageSize = 10`
  - `IncludeTotalCount = false`
- [x] Add a live DB exact-count and data-accuracy test for May 1-2, 2025:
  - `FromDate = 2025-05-01 00:00:00`
  - `ToDate = 2025-05-02 23:59:59`
  - expected detail row count: `2420`
  - verifies nonblank `Place/Port of Export`, `Country of Destination`, `Unit`, `Currency`, and nonzero `Price`, `Qty`, `Value`

## Implementation Notes

`Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReportV2.cs` now uses a seek-based page path for runtime detail loads. It first gets matching oversea licence keys from `ExportLicence`, then reads item keys licence-by-licence from `ExportLicenceItem`. It stops after `PageSize + 1` rows for fast UI loads, and scans all matching keys only when the UI asks for an exact background count.

The selected page rows are then enriched with licence, item, unit, currency, HS code, port, and destination values. This avoids the old large join that timed out while still returning accurate `Unit`, `Price`, `Qty`, `Value`, `Currency`, `Place/Port of Export`, and `Country of Destination`.

`Frontend/src/Report/config/reportConfigs.ts` keeps lazy exact counts enabled for `ExportLicenceDetailReport`. The first fast response may carry an estimated lower-bound total, and `BasicTable` labels it as "at least N (calculating total)" until the exact count response arrives.

The active UI path is `ExportLicenceDetailReportController` -> `sp_ExportLicenceDetailReportV2.CreatePagedResultAsync`. Excel streaming and export/list aggregate reports still use `sp_ExportLicenceDetailReport_Fast`, so that file is still required. Unused V2 streaming and unreachable old inline SQL blocks were removed from `sp_ExportLicenceDetailReportV2.cs`.

The migration artifact `StoredProcedureMigrations/sp_ExportLicenceDetailReportV2_pagination.sql` remains as a DB-side deployment/testing artifact, but the UI runtime no longer depends on the old stored procedure path.

## Root Causes Found

1. The old report path could join/filter a large detail set before paging, causing query timeouts and UI load failure.
2. The first fast-page API response returned a lower-bound total such as `11`, but the UI was configured to suppress the exact background count, so the pager showed the wrong row count.
3. Item detail fields were blank/zero when the covering item index was not available because the fallback key query intentionally selected placeholders and never re-fetched selected item details.
4. `Place/Port of Export` and `Country of Destination` were blank because the lightweight licence-detail query returned placeholder empty strings for those comma-separated ID fields.
5. Several filters/lookup options did not match the intended oversea export licence detail scope until the config and lookup routes were narrowed.

## Pagination Result and Performance

Pagination was added to `ExportLicenceDetailReport` so the UI no longer loads every matching detail row at once. The first request loads only the requested page plus one extra row to detect whether a next page exists. The exact row count is loaded separately in the background.

Current UI behavior:

- First page request uses `IncludeTotalCount = false`.
- Backend returns page rows quickly and marks total count as estimated when exact count is skipped.
- UI pager shows `of at least N (calculating total)` while exact total is loading.
- Background exact-count request uses `IncludeTotalCount = true`.
- After exact count completes, UI shows the real total count.

Live DB verification for `2025-05-01 00:00:00` to `2025-05-02 23:59:59`:

```text
rows=10, total=2420, exact=True, hasNext=True
sample licence=OVSEL12526003876, port=Yangon, destination=NETHERLANDS, hs=6404199000, unit=2U, price=6.0000, qty=6372.0000, amount=38232.0000, currency=USD
sample licence=OVSEL12526003877, port=Yangon, destination=MALAYSIA, hs=1902304000, unit=KG, price=0.3000, qty=5000.0000, amount=1500.0000, currency=USD
sample licence=OVSEL12526003877, port=Yangon, destination=MALAYSIA, hs=2005999000, unit=KG, price=0.3000, qty=20000.0000, amount=6000.0000, currency=USD
```

Performance result:

- The report now returns paged UI data instead of loading the full detail result into the browser.
- The May 1-2, 2025 live exact-count and data-accuracy test completed successfully in about 14-15 seconds.
- The same date range contains `2420` detail rows across the matching export licences.
- First-page UI load is separated from exact count, so users can see page data before the heavier exact total finishes.
- Item values and lookup fields are now accurate on the paged result: `Unit`, `Price`, `Qty`, `Value`, `Currency`, `Place/Port of Export`, and `Country of Destination`.

## Verification Commands

Focused contract test:

```powershell
dotnet test Backend.Tests\Backend.Tests.csproj --filter ExportLicenceDetailReportContractTests --no-restore -p:BaseOutputPath=C:\Data_D\Projects\Reports\tradenet-admin-report\artifacts\test-bin\ -p:BaseIntermediateOutputPath=C:\Data_D\Projects\Reports\tradenet-admin-report\artifacts\test-obj\
```

Optional live DB May 1-2, 2025 accuracy check:

```powershell
dotnet test Backend.Tests\Backend.Tests.csproj --filter Detail_page_may_1_to_may_2_2025_exact_count_matches_live_db --no-restore -p:BaseOutputPath=C:\Data_D\Projects\Reports\tradenet-admin-report\artifacts\test-bin\ -p:BaseIntermediateOutputPath=C:\Data_D\Projects\Reports\tradenet-admin-report\artifacts\test-obj\
```

The live test runs only when `TRADENET_REPORT_TEST_CONNECTION_STRING` is set.

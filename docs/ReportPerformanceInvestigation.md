# Report Performance Investigation

Updated: 2026-05-29

## Current Finding

Server-side pagination is not fast because the report API does more than read one page. The shared report path calls `ApiResult.CreateAsync`, which runs a full `COUNT` over the filtered report query before it applies `Skip`/`Take`.

For large joined reports, especially item reports such as HS code reports, that count can dominate the request time.

## Evidence

Measured against `ConnectionStrings:TradeNetDBTest` using the database connection stored in `Backend/appsettings.json`.

Narrow probe for Export Licence New Report, November 2023:

- Count matching rows: about 2.3 seconds for 8,488 rows.
- First 10 rows without sort: about 0.06 seconds.
- First 10 rows sorted by date: about 0.4 seconds.

HS code report probe, November 2023:

- Count matching rows: about 50 seconds for 42,913 item rows.
- First 10 rows without sort: about 0.1 seconds after warm cache.
- First 10 rows sorted by HS code: about 0.2 to 0.6 seconds after warm cache.

This means the page read can be quick, but the API still waits for the total count first.

## Root Causes

1. Exact total count runs before every page.
   - File: `Backend/Model/APIResult.cs`
   - `Count()` / `CountAsync()` is executed before `Skip()` and `Take()`.
   - This scans the full filtered report result even when the UI only needs 10 rows.

2. Frontend sends sorting by default.
   - File: `Frontend/src/components/My Components/Table/BasicTable.tsx`
   - The table initializes sorting to the first data column when there is no explicit sort.
   - File: `Frontend/src/Report/config/reportConfigs.ts`
   - Most report configs also define `initialSortColumn`.

3. API-level dynamic sorting adds another database sort.
   - File: `Backend/Model/APIResult.cs`
   - Dynamic `OrderBy(sortColumn sortOrder)` is applied before pagination.
   - Fix applied: `Backend/Service/Reports/ReportQueryService.cs` now passes `null` sort values to report pagination.

4. Many converted LINQ report queries already sort internally.
   - Examples:
     - `Backend/StoredProcedureToLinq/sp_HSCodeReport.cs`
     - `Backend/StoredProcedureToLinq/sp_NewReport.cs`
     - `Backend/StoredProcedureToLinq/sp_AmendReport.cs`
     - `Backend/StoredProcedureToLinq/sp_CancelReport.cs`
   - Removing API-level sorting does not remove these internal `OrderBy` clauses.

5. Some report queries use expensive joins, `Concat`/`UNION ALL`, and correlated subqueries.
   - Border reports often combine PaThaKa and Individual Trading rows, then sort the combined result.
   - Some reports calculate currency and amount with subqueries per result row.
   - `ImportLicenceDetailReport` also projected two country-name collections in one SQL statement, producing `OUTER APPLY` branches and EF's multiple-collection warning.
   - Fix applied for `ImportLicenceDetailReport`: `sp_ImportLicenceDetailReport_Fast.cs` now pages scalar report rows first, caches all country id/name rows in memory for one day, and resolves `ConsignedCountry` / `CountryofOrigin` after the page is loaded. The page API now uses this fast path; Excel still uses the original full query.

6. The shared paginator used synchronous query execution in practice.
   - File: `Backend/Model/APIResult.cs`
   - Previous behavior checked for instance methods named `CountAsync` / `ToListAsync`, but EF async methods are extension methods.
   - Fix applied: the paginator now uses EF async when the provider supports it, with a synchronous fallback for in-memory/non-EF query fragments.

## Next Fixes

Recommended order:

1. Done: keep the diagnostic API sort removal and test report load time.
2. Done: add a fast pagination mode for report pages that avoids exact `COUNT` on every request.
   - Fetch `pageSize + 1` rows.
   - Use that extra row to calculate `hasNextPage`.
   - Only calculate exact total count when the user explicitly needs it.
3. Done: remove frontend default report sorting so the UI does not send sort fields automatically.
4. In progress: review internal `OrderBy` in converted LINQ reports and keep only the order that the stored procedure actually requires.
   - `ImportLicenceDetailReport` page API now orders by `CreatedDate`, the report filter column, because full detail pages ordered by `IssuedDate`/`LicenceDate` timed out in testing while `CreatedDate` returned quickly.
5. Done: use proper EF async calls directly instead of the current reflection check.

# Report Pagination And Stored Procedure Work Summary

Updated: 2026-06-06

## Purpose

This note summarizes the report pagination and stored procedure work done for the TradeNet admin report project, especially the HS code reports, Border Export Permit reports, and Export Licence reports.

The main goal was to move slow report pages away from full LINQ loading and into pagination-aware stored procedures, while keeping the original stored procedure behavior as much as possible.

## Main Problems Found

1. Some report pages loaded too slowly or timed out.
   - The API was preparing rows and totals for too long.
   - Some reports were still doing expensive LINQ grouping/counting before paging.
   - Large date ranges could take more than the SQL command timeout.

2. Some reports returned rows in SQL Server but showed `No data` or `N/A` in the frontend.
   - The database result existed.
   - Some frontend field mappings did not match backend property names.
   - Examples included `HSCode`, `HSDescription`, `TotalUSDValue`, and NRC/Union Citizenship fields.

3. Some Border Export Permit pagination SQL used columns that do not exist for `BorderExportPermit`.
   - Invalid references included:
     - `BorderExportPermit.CardType`
     - `BorderExportPermit.IndividualTradingId`
     - `BorderExportPermit.auto`
   - These caused failed stored procedure execution for some Border Export Permit reports.

4. A senior commit updated count-query performance.
   - Commit checked: `cab341cbff375ae3ba50a27e310387a0e094b7e6`
   - Main change: use `OPTION (RECOMPILE)` on total count queries.
   - This helps SQL Server avoid bad cached plans for different filter combinations.

## Important SQL Pattern

For pagination procedures, total count should use this style:

```sql
DECLARE @__total int;

SELECT @__total = COUNT(*)
FROM ...
WHERE ...
OPTION (RECOMPILE);
```

For paged rows, the query should still use:

```sql
ORDER BY ...
OFFSET @off ROWS
FETCH NEXT @ps ROWS ONLY
OPTION (RECOMPILE);
```

## Files Worked On

### HS Code Report

Stored procedure files:

- `StoredProcedureMigrations/sp_HSCodeReport_pagination.sql`
- `docs/HSCodeReportStoredProcedurePerformance.sql`

Backend files:

- `Backend/StoredProcedureToLinq/sp_HSCodeReport.cs`
- `Backend/StoredProcedureToLinq/sp_HSCodeReport.StoredProcedure.cs`

What changed:

- Added/used pagination-aware stored procedure path.
- Kept the original stored procedure query style where possible.
- Added page size and page index support.
- Added total count support for paginated frontend display.
- Split stored procedure calling code into its own file to keep LINQ and stored procedure execution easier to trace.

### Border Export Permit Reports

Stored procedure files:

- `StoredProcedureMigrations/sp_VoucherReport_pagination.sql`
- `StoredProcedureMigrations/sp_ExtensionReport_pagination.sql`
- `StoredProcedureMigrations/sp_CancelReport_pagination.sql`
- `StoredProcedureMigrations/sp_NewReport_pagination.sql`
- `StoredProcedureMigrations/sp_AmendReport_pagination.sql`
- `StoredProcedureMigrations/sp_ActualAmendReport_pagination.sql`
- `StoredProcedureMigrations/sp_ExportPermitDetailReport_Fast_pagination.sql`

Deployment note:

- `StoredProcedureMigrations/Deploy_BorderExportPermit_Fixes.md`

Index script:

- `StoredProcedureMigrations/Indexes/BorderExportPermitDetailReport_indexes.sql`

What changed:

- Fixed Border Export Permit branches that were using invalid licence-style or individual-trading columns.
- Kept Border Export Permit logic as a direct `BorderExportPermit` query.
- Added `HSCode` output to amendment, cancellation, and new report procedures where the frontend needs it.
- Added `TotalCount` support for pagination.
- Added `OPTION (RECOMPILE)` to count queries following senior commit guidance.
- Added recommended indexes for Border Export Permit detail report performance.

### Export Licence Reports

Stored procedure files:

- `StoredProcedureMigrations/sp_ExportLicenceDetailReport_pagination.sql`
- `StoredProcedureMigrations/sp_NewReport_pagination.sql`
- `StoredProcedureMigrations/sp_CancelReport_pagination.sql`
- `StoredProcedureMigrations/sp_ExtensionReport_pagination.sql`
- `StoredProcedureMigrations/sp_VoucherReport_pagination.sql`
- `StoredProcedureMigrations/sp_AmendReport_pagination.sql`
- `StoredProcedureMigrations/sp_ActualAmendReport_pagination.sql`

Backend files:

- `Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReport_Fast.cs`

Index/view scripts:

- `StoredProcedureMigrations/Indexes/ExportLicenceDetailReport_indexes.sql`
- `StoredProcedureMigrations/Views/vw_ExportLicenceItemTotalByCurrency.sql`

What changed:

- Added faster stored procedure based paging paths where reports were stuck or slow.
- Added missing fields needed by frontend display, including `HSCode`, `CommodityType`, and `TotalUSDValue` related output.
- Added an indexed-view helper for Export Licence voucher/item total lookups.
- Added recommended indexes for Export Licence detail report performance.

## Frontend Mapping Fixes

Frontend config file:

- `Frontend/src/Report/config/reportConfigs.ts`

Mapping issues found:

- Some generated property names had acronym casing mismatches.
- Frontend expected one casing, while backend returned another.

Examples fixed or reviewed:

- `hSCode` should map to `hsCode`
- `hSDescription` should map to `hsDescription`
- `nRCNo` should map to `nrcNo`
- `totalUSDValue` needs backend data or USD conversion handling

Result:

- Reports that returned valid backend data no longer show `N/A` for those fields when the mapping is correct.

## Database Connection Note

Use the project connection string from:

- `Backend/appsettings.json`

Recommended key for report DB testing:

- `ConnectionStrings:TradeNetDBTest`

Do not copy the password into docs, commits, screenshots, or logs. Use SSMS or read the connection string locally from `appsettings.json`.

## SQL Files To Run In MSSQL

Run only the procedures related to the reports you want to update.

For the latest Border Export Permit fixes:

```text
StoredProcedureMigrations/sp_VoucherReport_pagination.sql
StoredProcedureMigrations/sp_ExtensionReport_pagination.sql
StoredProcedureMigrations/sp_CancelReport_pagination.sql
StoredProcedureMigrations/sp_NewReport_pagination.sql
```

For HS Code report pagination:

```text
StoredProcedureMigrations/sp_HSCodeReport_pagination.sql
```

For Export Licence detail/report performance:

```text
StoredProcedureMigrations/sp_ExportLicenceDetailReport_pagination.sql
StoredProcedureMigrations/Views/vw_ExportLicenceItemTotalByCurrency.sql
StoredProcedureMigrations/Indexes/ExportLicenceDetailReport_indexes.sql
```

For Border Export Permit detail/report performance:

```text
StoredProcedureMigrations/sp_ExportPermitDetailReport_Fast_pagination.sql
StoredProcedureMigrations/Indexes/BorderExportPermitDetailReport_indexes.sql
```

## Verification Done

Backend build was run after SQL/code reconciliation:

```powershell
dotnet build Backend\API.csproj
```

Result:

- Build succeeded.
- 0 errors.

Database connectivity was also tested with the project database credentials:

- SQL Server accepted a simple `SELECT 1`.
- This confirmed stored procedure deployment can be done from this machine when needed.

## Suggested Test Flow

After running the SQL scripts in SSMS:

1. Open the report page in the frontend.
2. Use a small date range first.
3. Confirm the table returns rows.
4. Confirm pagination shows the correct total count.
5. Test a larger date range.
6. Confirm the page loads in less than 10 seconds where indexes are applied.
7. Check columns that previously showed `N/A`, especially:
   - HS Code
   - Description
   - Total USD Value
   - Union Citizenship No
   - Commodity Type
   - Quota

## Known Follow-Up

Some reports may still need database data verification instead of code fixes.

Example:

- Border Export Permit Actual Amendment Report can work technically but still return no rows if the database has no matching records for the selected filters.

For slow reports, run the related index script first, then retest the same date range.

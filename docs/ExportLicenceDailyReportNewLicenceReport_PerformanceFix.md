# ExportLicenceDailyReportNewLicenceReport Performance Fix

Date: 2026-06-15

## Request

`ExportLicenceDailyReportNewLicenceReportController` was still taking more than 50 seconds for the date range May 1, 2025 to May 31, 2025. The target is to show the result within 30 seconds.

## Finding

The controller uses:

- `Backend/Controllers/Report/ExportLicenceDailyReportNewLicenceReportController.cs`
- `sp_ExportLicenceDetailReportV2.CreateSummaryResultAsync(...)`
- `StoredProcedureMigrations/sp_ExportLicenceSummaryReport.sql`

The slow part was the `Daily` branch of `sp_ExportLicenceSummaryReport`.

Before the fix, the procedure built `ItemTotals` like this:

```sql
SELECT item.ExportLicenceId, item.CurrencyId, SUM(item.Amount) AS TotalAmount
FROM dbo.ExportLicenceItem AS item
GROUP BY item.ExportLicenceId, item.CurrencyId
```

That shape can aggregate the whole `ExportLicenceItem` table before applying the report date range and licence filters. For a high-volume table, a one-month report can still pay for a much larger scan.

## Change Made

Updated `StoredProcedureMigrations/sp_ExportLicenceSummaryReport.sql` in the `Daily` branch.

The final query flow is:

1. Join `ExportLicence`, `PaThaKa`, `ExportLicenceItem`, and `Currency` directly.
2. Apply these licence filters before grouping:
   - `ApplyType = N'New'`
   - `Status = N'Approved'`
   - `CreatedDate >= @FromDate`
   - `CreatedDate <= @ToDate`
   - company registration number
   - PaThaKa type
   - export section
   - export method
   - export incoterm
   - buyer country
3. Group by issued date and currency.
4. Sum `ExportLicenceItem.Amount` directly for the matching filtered rows.
5. Return the same result columns expected by the C# code and frontend.

## Auto Filter Change

Added an `Auto / None Auto` filter to `ExportLicenceDailyReportNewLicenceReport`.

Options:

- `All`: sends an empty value and does not filter by `ExportLicence.[auto]`.
- `auto`: returns rows where `ExportLicence.[auto] = N'auto'`.
- `none-auto`: returns rows where `ExportLicence.[auto] IS NULL OR ExportLicence.[auto] <> N'auto'`.

The stored procedure parameter is:

```sql
@Auto nvarchar(20) = N''
```

It is appended after `@Dimension` so older positional calls that do not pass `@Auto` keep working.

## Indexes Used

The target database should have these existing report indexes available:

- `IX_ExportLicence_Report_NewDetail_Page`
- `IX_ExportLicenceItem_Report_Licence_Page`

They are defined in:

`StoredProcedureMigrations/Indexes/ExportLicenceDetailReport_indexes.sql`

Before testing in UAT or production, confirm these indexes exist on the database. The final query does not force index hints; SQL Server is allowed to choose the best plan.

## Why This Should Be Faster

The old query shape could process all export licence item rows first, then filter licences later.

The new query shape lets SQL Server join from the filtered licence range to matching item rows and aggregate only those rows. On the target database, the exact direct grouped query for May 1-31, 2025 returned 98 grouped rows in about 0.4 seconds during diagnosis.

This is the main performance improvement needed for this controller. I did not switch the controller back to `_Fast` because the existing contract tests intentionally pin export summary reports to `sp_ExportLicenceDetailReportV2` and `sp_ExportLicenceSummaryReport`.

## Files Changed

- `StoredProcedureMigrations/sp_ExportLicenceSummaryReport.sql`
- `Backend.Tests/ExportLicenceDetailReportContractTests.cs`

## Deployment Steps

1. Run `StoredProcedureMigrations/Indexes/ExportLicenceDetailReport_indexes.sql` on the target database if the indexes are not already present.
2. Run `StoredProcedureMigrations/sp_ExportLicenceSummaryReport.sql` on the target database to update the stored procedure.
3. Test the API with:
   - report: `ExportLicenceDailyReportNewLicenceReport`
   - from date: `2025-05-01`
   - to date: `2025-05-31 23:59:59`
   - page index: `0`
   - page size: `10`
   - include total count: `false`

## Verification Query

To verify the indexes:

```sql
SELECT name
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'dbo.ExportLicence')
  AND name = N'IX_ExportLicence_Report_NewDetail_Page';

SELECT name
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'dbo.ExportLicenceItem')
  AND name = N'IX_ExportLicenceItem_Report_Licence_Page';
```

## Expected Result

The May 1-31, 2025 daily report should return within 30 seconds if the target database keeps the same execution characteristics observed during diagnosis.

If it is still over 30 seconds after this change, the next step is to capture the actual execution plan for `EXEC dbo.sp_ExportLicenceSummaryReport ... @Dimension = 'Daily'`. The likely next improvement would be a dedicated indexed summary path for export licence daily totals.

## Target Database Execution

The updated `StoredProcedureMigrations/sp_ExportLicenceSummaryReport.sql` script was executed against the configured target `TradeNetDB` connection on 2026-06-15.

Pre-check:

- `IX_ExportLicence_Report_NewDetail_Page`: exists
- `IX_ExportLicenceItem_Report_Licence_Page`: exists

Direct stored procedure timing after deployment:

- Procedure: `dbo.sp_ExportLicenceSummaryReport`
- Dimension: `Daily`
- From date: `2025-05-01 00:00:00`
- To date: `2025-05-31 23:59:59`
- Rows returned: 98
- Elapsed time: 0.416 seconds

Direct stored procedure timing after adding the Auto filter:

| Auto filter | Rows | Licence sum | Total value sum | Elapsed |
| --- | ---: | ---: | ---: | ---: |
| All | 98 | 11,879 | 6,775,076,528.8700 | 0.429 sec |
| auto | 50 | 8,998 | 889,172,350.6030 | 0.522 sec |
| none-auto | 86 | 2,881 | 5,885,904,178.2670 | 0.359 sec |

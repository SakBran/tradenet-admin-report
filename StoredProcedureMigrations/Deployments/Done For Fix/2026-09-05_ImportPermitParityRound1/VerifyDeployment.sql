/* =====================================================================================
   Import Permit round-1 parity deployment - 2026-09-05
   Verification. Run section 1 BEFORE deploying (it records what is on the server today)
   and again AFTER; run sections 2-4 after.

   Use 2025 date ranges: this database's report data lives in 2025, and a 2026 window
   comes back empty, which looks exactly like a broken procedure.
   ===================================================================================== */

USE [TradeNetDB];
GO

-- -------------------------------------------------------------------------------------
-- 1. What is deployed right now
--    AFTER deployment: hscode_grouping must read 'by hs code' and cancel_branch 'present'.
--    'by company (stale)' / 'missing (stale)' means the server still carries the older
--    definition, which is exactly what the customer complaints describe.
-- -------------------------------------------------------------------------------------
SELECT
    p.name,
    p.modify_date,
    m.uses_quoted_identifier,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency%'
         THEN 'by hs code' ELSE 'by company (stale)' END AS hscode_grouping,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%@DbApplyType = N''Cancel''%'
         THEN 'present' ELSE 'missing (stale)' END AS cancel_branch
FROM sys.procedures p
    JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name IN ('sp_HSCodeReport_pagination', 'sp_ImportPermitListingCurrencyTotals')
ORDER BY p.name;
GO

-- -------------------------------------------------------------------------------------
-- 2. Import Permit By HS Code: each (HS Code, Currency) pair must appear EXACTLY ONCE.
--    Before the fix the same HS Code came back once per buyer company, each row carrying
--    only that buyer's Total Value. This query must return no rows.
-- -------------------------------------------------------------------------------------
-- Capture the page, then assert no (HS Code, Currency) pair repeats. The INSERT..EXEC
-- needs a table whose shape matches the procedure's result set.
IF OBJECT_ID(N'tempdb..#hs') IS NOT NULL DROP TABLE #hs;
CREATE TABLE #hs (
    HSCode nvarchar(50) NULL, HSDescription nvarchar(1000) NULL,
    CompanyRegistrationNo nvarchar(200) NULL, CompanyName nvarchar(500) NULL,
    Currency nvarchar(50) NULL, NoOfLicences int NULL, TotalValue decimal(38, 6) NULL,
    TotalCount int NULL);

INSERT INTO #hs
EXEC dbo.sp_HSCodeReport_pagination
    @FromDate = '2025-01-01 00:00:00',
    @ToDate   = '2025-12-31 23:59:59',
    @FormType = N'Import Permit',
    @FilterType = N'Start',
    @HSCode   = N'',
    @SakhanId = 0,
    @PageIndex = 0, @PageSize = 1000, @IncludeTotalCount = 1;

-- Must return NO rows. Before the fix, an HS code bought by 3 companies returned 3.
SELECT HSCode, Currency, COUNT(*) AS duplicate_rows
FROM #hs
GROUP BY HSCode, Currency
HAVING COUNT(*) > 1;

-- CompanyRegistrationNo / CompanyName must now be NULL for this FormType: the rows are
-- no longer company-specific.
SELECT TOP 5 * FROM #hs;
DROP TABLE #hs;
GO

-- Same call on its own, for eyeballing the HS Code column in the grid's own order:
EXEC dbo.sp_HSCodeReport_pagination
    @FromDate = '2025-01-01 00:00:00',
    @ToDate   = '2025-12-31 23:59:59',
    @FormType = N'Import Permit',
    @FilterType = N'Start',
    @HSCode   = N'',
    @SakhanId = 0,
    @PageIndex = 0, @PageSize = 100, @IncludeTotalCount = 1;
GO

-- -------------------------------------------------------------------------------------
-- 3. The other seven FormType branches must be UNCHANGED -- their *HSCodeDetailReport
--    reports render Company Name off this procedure. CompanyName must still be populated.
-- -------------------------------------------------------------------------------------
EXEC dbo.sp_HSCodeReport_pagination
    @FromDate = '2025-01-01 00:00:00',
    @ToDate   = '2025-12-31 23:59:59',
    @FormType = N'Border Import Permit',
    @FilterType = N'Start',
    @HSCode   = N'',
    @SakhanId = 0,
    @PageIndex = 0, @PageSize = 10, @IncludeTotalCount = 1;
GO

-- -------------------------------------------------------------------------------------
-- 4. Import Permit Cancellation footer. The per-currency counts must add up to the row
--    count the grid shows for the same window, and each TotalValue must be the sum of the
--    FIRST item's Amount over that currency's permits -- not a sum over every item.
-- -------------------------------------------------------------------------------------
EXEC dbo.sp_ImportPermitListingCurrencyTotals
    @ApplyType = N'Cancel',
    @FromDate = '2025-01-01 00:00:00',
    @ToDate   = '2025-12-31 23:59:59',
    @ExportImportSectionId = 0,
    @CompanyRegistrationNo = N'',
    @AmendRemarkId = 0;
GO

-- The grid the footer must agree with. SUM(NoOfLicences) from section 4 must equal this
-- procedure's TotalCount.
EXEC dbo.sp_CancelReport_pagination
    @FormType = N'Import Permit',
    @FromDate = '2025-01-01 00:00:00',
    @ToDate   = '2025-12-31 23:59:59',
    @PageIndex = 0, @PageSize = 25, @IncludeTotalCount = 1;
GO

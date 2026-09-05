/* =====================================================================================
   Border Import Permit customer-complaint deployment - 2026-09-05
   Verification. Run section 1 BEFORE deploying (it records what is on the server today)
   and again AFTER; run sections 2-5 after.

   Use 2025 date ranges: this database's report data lives in 2025, and a 2026 window
   comes back empty, which looks exactly like a broken procedure. The expected numbers
   below were measured against the live report API over 2025-01-01 .. 2025-12-31 with
   Sakhan = TCL (Sakhan.Id = 4).
   ===================================================================================== */

USE [TradeNetDB];
GO

-- -------------------------------------------------------------------------------------
-- 1. What is deployed right now. Every column must read the "new" value after deploying.
-- -------------------------------------------------------------------------------------
SELECT
    p.name,
    p.modify_date,
    m.uses_quoted_identifier,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%@FetchSize%'
         THEN 'fetch/offset split' ELSE 'inflated offset (stale)' END AS paging,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency%'
         THEN 'has hs-code-only grouping' ELSE 'company grouping only (stale)' END AS hscode_grouping
FROM sys.procedures p
    JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name = 'sp_HSCodeReport_pagination';

SELECT
    p.name,
    p.modify_date,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%DATEADD(day, 1, CONVERT(date, @ToDate))%'
              AND OBJECT_DEFINITION(p.object_id) NOT LIKE '%DATEADD(day,1,@ToDate)%'
         THEN 'calendar date' ELSE 'extra day (stale)' END AS to_date_window
FROM sys.procedures p
WHERE p.name = 'sp_NewReport_pagination';

SELECT
    p.name,
    p.modify_date,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%ELSE IF @DbApplyType = N''New''%'
         THEN 'border new footer present' ELSE 'missing (stale)' END AS border_new_branch
FROM sys.procedures p
WHERE p.name = 'sp_ImportPermitListingCurrencyTotals';
GO

-- -------------------------------------------------------------------------------------
-- 2. By HS Code paging must not lose rows. Page 2 must start on the row after page 1's
--    last, not skip one. Before the fix a 31-row report showed 10 + 10 + 9 = 29.
-- -------------------------------------------------------------------------------------
IF OBJECT_ID(N'tempdb..#page1') IS NOT NULL DROP TABLE #page1;
IF OBJECT_ID(N'tempdb..#all') IS NOT NULL DROP TABLE #all;
CREATE TABLE #page1 (
    HSCode nvarchar(50) NULL, HSDescription nvarchar(1000) NULL,
    CompanyRegistrationNo nvarchar(200) NULL, CompanyName nvarchar(500) NULL,
    Currency nvarchar(50) NULL, NoOfLicences int NULL, TotalValue decimal(38, 6) NULL,
    TotalCount int NULL);
CREATE TABLE #all (
    HSCode nvarchar(50) NULL, HSDescription nvarchar(1000) NULL,
    CompanyRegistrationNo nvarchar(200) NULL, CompanyName nvarchar(500) NULL,
    Currency nvarchar(50) NULL, NoOfLicences int NULL, TotalValue decimal(38, 6) NULL,
    TotalCount int NULL);

INSERT INTO #page1 EXEC dbo.sp_HSCodeReport_pagination
    @FromDate = '2025-01-01', @ToDate = '2025-12-31 23:59:59',
    @FormType = N'Border Import Permit', @FilterType = N'', @HSCode = N'', @SakhanId = 0,
    @PageIndex = 0, @PageSize = 10, @IncludeTotalCount = 0;

INSERT INTO #all EXEC dbo.sp_HSCodeReport_pagination
    @FromDate = '2025-01-01', @ToDate = '2025-12-31 23:59:59',
    @FormType = N'Border Import Permit', @FilterType = N'', @HSCode = N'', @SakhanId = 0,
    @PageIndex = 0, @PageSize = 1000, @IncludeTotalCount = 1;

-- 11 rows on the fast page: 10 shown plus the next-page marker the caller trims.
SELECT 'page 1 rows (expect 11)' AS check_name, COUNT(*) AS value FROM #page1
UNION ALL
-- 16 after the grouping fix, where the company split used to give 31.
SELECT 'total rows (expect 16)', COUNT(*) FROM #all
UNION ALL
-- Company must be NULL on the summary; it belongs only to the HS Code detail drill.
SELECT 'rows carrying a company (expect 0)', COUNT(*) FROM #all WHERE CompanyName IS NOT NULL
UNION ALL
-- Each (HS Code, Currency) pair exactly once.
SELECT 'duplicate hs/currency pairs (expect 0)',
    (SELECT COUNT(*) FROM (SELECT HSCode, Currency FROM #all GROUP BY HSCode, Currency HAVING COUNT(*) > 1) d);
GO

-- -------------------------------------------------------------------------------------
-- 3. New Report date window: @ToDate 2025-01-12 23:59:59 must NOT admit 2025-01-13.
--    Before the fix this returned 2 permits, both dated the 13th.
-- -------------------------------------------------------------------------------------
SELECT 'permits after the window (expect 0)' AS check_name, COUNT(*) AS value
FROM BorderImportPermit
WHERE ApplyType = 'New' AND Status = 'Approved'
    AND CreatedDate >= '2025-01-01'
    AND CreatedDate < DATEADD(day, 1, CONVERT(date, '2025-01-12 23:59:59'))
    AND CreatedDate > '2025-01-12 23:59:59';
GO

-- -------------------------------------------------------------------------------------
-- 4. New Report footer. grand total must equal the grid's row count for the same window:
--    18 permits all-Sakhan, 4 for Sakhan = TCL (Id 4).
-- -------------------------------------------------------------------------------------
EXEC dbo.sp_ImportPermitListingCurrencyTotals
    @ApplyType = N'New', @FromDate = '2025-01-01', @ToDate = '2025-12-31 23:59:59',
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @AmendRemarkId = 0,
    @FormType = N'Border Import Permit', @SakhanId = 0;   -- SUM(NoOfLicences) must be 18

EXEC dbo.sp_ImportPermitListingCurrencyTotals
    @ApplyType = N'New', @FromDate = '2025-01-01', @ToDate = '2025-12-31 23:59:59',
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @AmendRemarkId = 0,
    @FormType = N'Border Import Permit', @SakhanId = 4;   -- SUM(NoOfLicences) must be 4
GO

-- -------------------------------------------------------------------------------------
-- 5. Blast radius. sp_HSCodeReport_pagination serves all eight FormType families and the
--    @FetchSize change touches every one: re-run section 2's page-1 count for each.
--    Expect 11 rows whenever the family has more than 10 groups in the window.
-- -------------------------------------------------------------------------------------
-- @FormType values to sweep: 'Export Licence', 'Import Licence', 'Export Permit',
-- 'Import Permit', 'Border Export Licence', 'Border Import Licence',
-- 'Border Export Permit', 'Border Import Permit'.
GO

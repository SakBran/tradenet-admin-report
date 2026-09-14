/* =====================================================================================
   Border Import Licence By HS Code parity deployment - 2026-09-14
   Verification. Run section 1 BEFORE deploying (it records what is on the server today)
   and again AFTER; run sections 2-4 after. Section 5 is a browser check.

   Window: '2025-01-01 00:00:00' .. '2025-12-31 23:59:59'. 2025 is a CLOSED period, so the
   figures below are stable. They were measured on PROD on 2026-09-14 via the live API
   (reportapi.myanmartradenet.com, FilterType Start, HSCode '', Sakhan 0, Section 0), paging
   the whole result: totalCount 9,213 rows BEFORE the fix collapsing to 2,881 distinct
   (HS code, currency) pairs, which is exactly the old report's row count; footer
   Total No of License 12,435; sum of every Total Value 14,032,670,046.0979.
   An empty window looks exactly like a broken procedure - keep the 2025 window.
   ===================================================================================== */

USE [TradeNetDB];
GO

-- -------------------------------------------------------------------------------------
-- 1. What is deployed right now - inspecting ONLY the 'Border Import Licence' branch.
--    The other branches still (deliberately) group by company - Import Licence, Export
--    Permit and the Border Import Permit drill sub-branch - so a whole-definition LIKE
--    cannot tell this release apart from the previous one. The text between the
--    'Border Import Licence' and 'Border Export Permit' markers is exactly that branch.
--    AFTER deployment both grain and page_order must read 'ok'.
-- -------------------------------------------------------------------------------------
DECLARE @def nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_HSCodeReport_pagination'));
DECLARE @s int = CHARINDEX('(@FormType=''Border Import Licence'')', @def);
DECLARE @e int = CHARINDEX('(@FormType=''Border Export Permit'')', @def);

IF @def IS NULL
    PRINT 'dbo.sp_HSCodeReport_pagination not found in this database - wrong database?';
ELSE IF @s = 0 OR @e = 0 OR @e <= @s
    PRINT 'branch marker not found - the deployed text has neither/only one of the Border Import Licence / Border Export Permit markers; inspect OBJECT_DEFINITION by hand';
ELSE
BEGIN
    DECLARE @branch nvarchar(max) = SUBSTRING(@def, @s, @e - @s);
    SELECT
        p.name,
        p.modify_date,
        m.uses_quoted_identifier,
        CASE WHEN @branch LIKE '%GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency%'
              AND @branch NOT LIKE '%tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency%'
             THEN 'ok' ELSE 'stale: still grouped by company' END AS grain,
        CASE WHEN @branch LIKE '%ORDER BY result.HSCode,result.Currency,result.HSCodeId%'
              AND @branch NOT LIKE '%ORDER BY result.HSCode,result.CompanyName,result.Currency%'
             THEN 'ok' ELSE 'stale: ambiguous company page order' END AS page_order,
        LEN(@branch) AS branch_chars
    FROM sys.procedures p
        JOIN sys.sql_modules m ON m.object_id = p.object_id
    WHERE p.name = 'sp_HSCodeReport_pagination';
END
GO

-- -------------------------------------------------------------------------------------
-- 2. The WHOLE new result for 2025, collected by paging 0..3 at 1000 rows a page
--    (2,881 rows -> pages 0, 1, 2 hold 1000 / 1000 / 881, page 3 is empty).
--    @IncludeTotalCount=1 so no sentinel row is fetched. The temp table has the outer
--    SELECT's 8 columns exactly, or INSERT..EXEC fails. HSDescription is nvarchar(max)
--    because HSCode.Description is unbounded - a fixed width would abort the whole section
--    with Msg 2628 on one long description and read like a broken procedure.
-- -------------------------------------------------------------------------------------
IF OBJECT_ID(N'tempdb..#bil') IS NOT NULL DROP TABLE #bil;
CREATE TABLE #bil (
    HSCode nvarchar(50) NULL, HSDescription nvarchar(max) NULL,
    CompanyRegistrationNo nvarchar(200) NULL, CompanyName nvarchar(500) NULL,
    Currency nvarchar(200) NULL, NoOfLicences int NULL, TotalValue decimal(38, 6) NULL,
    TotalCount int NULL);

DECLARE @page int = 0;
WHILE @page <= 3
BEGIN
    INSERT INTO #bil
    EXEC dbo.sp_HSCodeReport_pagination
        @FromDate = '2025-01-01 00:00:00',
        @ToDate   = '2025-12-31 23:59:59',
        @FormType = N'Border Import Licence',
        @FilterType = N'Start',
        @HSCode   = N'',
        @SakhanId = 0,
        @PageIndex = @page, @PageSize = 1000, @IncludeTotalCount = 1;
    SET @page += 1;
END
GO

-- 2a. Each (HS Code, Currency) pair must appear EXACTLY ONCE. Must return NO rows.
--     Before the fix 1,413 pairs were split; the worst, 3506990000 / THB, came back 95 times.
SELECT HSCode, Currency, COUNT(*) AS duplicate_rows
FROM #bil
GROUP BY HSCode, Currency
HAVING COUNT(*) > 1
ORDER BY duplicate_rows DESC;
GO

-- 2b. Expected:  rows_returned 2881 | total_count 2881 | rows_with_company 0
--                | sum_total_value 14032670046.0979 EXACTLY - Amount is decimal(18,4), so the
--                SUM is exact; any difference means rows are missing or double-counted.
--     CompanyRegistrationNo / CompanyName must be NULL on every row (the columns stay on
--     the result set only so the DTO and the HS Code detail drill keep one shape).
SELECT
    COUNT(*) AS rows_returned,
    MAX(TotalCount) AS total_count,
    SUM(CASE WHEN CompanyName IS NOT NULL OR CompanyRegistrationNo IS NOT NULL THEN 1 ELSE 0 END) AS rows_with_company,
    SUM(TotalValue) AS sum_total_value
FROM #bil;
GO

-- 2c. Per-currency row counts, for the record (before -> after, measured on PROD):
--     THB 6735 -> 1823 | USD 1660 -> 700 | CNY 816 -> 356 | JPY 1 -> 1 | MMK 1 -> 1
SELECT Currency, COUNT(*) AS rows_after FROM #bil GROUP BY Currency ORDER BY rows_after DESC;
GO

-- -------------------------------------------------------------------------------------
-- 3. The LEGACY oracle. dbo.sp_HSCodeReport is what the old admin app calls; it returns
--    raw ITEM rows (no SQL grouping) and BorderHSCodeReport.rdlc groups them on
--    (HSCodeId, Currency). So COUNT(DISTINCT HSCodeId|Currency) over its output IS the old
--    report's row count, and COUNT(DISTINCT LicenceNo) IS its TOTAL footer.
--    Column list in order, per docs/StoredProcedureDefinitions.sql (sp_HSCodeReport,
--    'Border Import Licence' branch): SakhanId, SectionCode, HSCodeId, HSCode,
--    HSDescription, Amount, Currency, LicenceNo, CompanyRegistrationNo, CompanyName.
--    If this INSERT fails on the column count, the legacy procedure has been overwritten
--    by docs/sp_HSCodeReport_AggregatePagination.sql - see CaptureRollback.sql.
-- -------------------------------------------------------------------------------------
IF OBJECT_ID(N'tempdb..#legacy') IS NOT NULL DROP TABLE #legacy;
CREATE TABLE #legacy (
    SakhanId int NULL, SectionCode nvarchar(50) NULL, HSCodeId int NULL, HSCode nvarchar(50) NULL,
    HSDescription nvarchar(max) NULL, Amount decimal(38, 6) NULL, Currency nvarchar(200) NULL,
    LicenceNo nvarchar(100) NULL, CompanyRegistrationNo nvarchar(200) NULL, CompanyName nvarchar(500) NULL);

INSERT INTO #legacy
EXEC dbo.sp_HSCodeReport
    @FromDate = '2025-01-01 00:00:00',
    @ToDate   = '2025-12-31 23:59:59',
    @FormType = N'Border Import Licence',
    @FilterType = N'Start',
    @HSCode   = N'',
    @SakhanId = 0;
GO

-- 3a. Expected:  old_report_rows 2881 | total_no_of_licence 12435
--     (old_report_rows_by_code_string is the same key on the HS code STRING instead of the
--     id; if it differs from old_report_rows, two HSCode rows share one code string and the
--     duplicate check in 2a will list that pair - the id-keyed figure is the authoritative one.)
SELECT
    COUNT(*) AS legacy_item_rows,
    COUNT(DISTINCT CONCAT(HSCodeId, '|', Currency)) AS old_report_rows,
    COUNT(DISTINCT CONCAT(HSCode, '|', Currency)) AS old_report_rows_by_code_string,
    COUNT(DISTINCT LicenceNo) AS total_no_of_licence
FROM #legacy;
GO

-- 3b. Old vs new, row by row. Must return NO rows: every (HS Code, Currency) the old
--     report prints must be in the new result with the same summed Total Value and the
--     same distinct licence count, and vice versa.
SELECT
    COALESCE(o.HSCode, n.HSCode) AS HSCode,
    COALESCE(o.Currency, n.Currency) AS Currency,
    o.TotalValue AS old_total_value, n.TotalValue AS new_total_value,
    o.NoOfLicences AS old_no_of_licences, n.NoOfLicences AS new_no_of_licences,
    CASE WHEN o.HSCode IS NULL THEN 'missing in OLD'
         WHEN n.HSCode IS NULL THEN 'missing in NEW'
         WHEN o.TotalValue <> n.TotalValue THEN 'Total Value differs'
         ELSE 'No of Licences differs' END AS mismatch
FROM (
    SELECT HSCode, Currency, SUM(Amount) AS TotalValue, COUNT(DISTINCT LicenceNo) AS NoOfLicences
    FROM #legacy
    GROUP BY HSCode, Currency
) o
FULL OUTER JOIN #bil n ON n.HSCode = o.HSCode AND n.Currency = o.Currency
WHERE o.HSCode IS NULL OR n.HSCode IS NULL
   OR o.TotalValue <> n.TotalValue
   OR o.NoOfLicences <> n.NoOfLicences
ORDER BY 1, 2;
GO

-- -------------------------------------------------------------------------------------
-- 4. Page-window stability. Paging the whole 2025 result 10 rows at a time must yield
--    exactly 2,881 rows and exactly 2,881 DISTINCT (HS Code, Currency) rows: a smaller
--    distinct count means OFFSET/FETCH is repeating rows across pages while dropping
--    others (the old ORDER BY was not a unique key). Stops at the first empty page; the
--    ceiling of 300 is only a guard. This is 290 executions of the grouping query over a
--    whole year (pages 0..288 plus the empty page 289 that stops the loop) - expect it to
--    take several minutes.
-- -------------------------------------------------------------------------------------
IF OBJECT_ID(N'tempdb..#pages') IS NOT NULL DROP TABLE #pages;
CREATE TABLE #pages (
    PageIndex int NULL,   -- stamped after each INSERT..EXEC; the EXEC cannot supply it
    HSCode nvarchar(50) NULL, HSDescription nvarchar(max) NULL,
    CompanyRegistrationNo nvarchar(200) NULL, CompanyName nvarchar(500) NULL,
    Currency nvarchar(200) NULL, NoOfLicences int NULL, TotalValue decimal(38, 6) NULL,
    TotalCount int NULL);

DECLARE @page int = 0, @got int = 1;
WHILE @page <= 300 AND @got > 0
BEGIN
    INSERT INTO #pages (HSCode, HSDescription, CompanyRegistrationNo, CompanyName, Currency, NoOfLicences, TotalValue, TotalCount)
    EXEC dbo.sp_HSCodeReport_pagination
        @FromDate = '2025-01-01 00:00:00',
        @ToDate   = '2025-12-31 23:59:59',
        @FormType = N'Border Import Licence',
        @FilterType = N'Start',
        @HSCode   = N'',
        @SakhanId = 0,
        @PageIndex = @page, @PageSize = 10, @IncludeTotalCount = 1;
    SET @got = @@ROWCOUNT;

    UPDATE #pages SET PageIndex = @page WHERE PageIndex IS NULL;
    SET @page += 1;
END
GO

-- Expected:  rows_paged 2881 | distinct_rows 2881 | last_page_with_rows 288
--            (2,881 rows over pages of 10 -> pages 0..288, the last one holding 1 row)
SELECT
    COUNT(*) AS rows_paged,
    COUNT(DISTINCT CONCAT(HSCode, '|', Currency)) AS distinct_rows,
    MAX(PageIndex) AS last_page_with_rows
FROM #pages;
GO

-- -------------------------------------------------------------------------------------
-- 5. The HS Code DETAIL drill is NOT served by this procedure -- it posts
--    GroupBy='Company' and runs the LINQ twin (sp_HSCodeReport.AggregateQuery), which
--    groups on (HSCodeId, HSCode, CompanyRegistrationNo) like HSCodeDetailReport.rdlc.
--    Nothing to verify in SQL; check it in the browser after the application deploy: the
--    HS Code cell must open BorderImportLicenceHSCodeDetailReport in a NEW TAB, showing a
--    Company Name column with each company once (no currency split).
-- -------------------------------------------------------------------------------------

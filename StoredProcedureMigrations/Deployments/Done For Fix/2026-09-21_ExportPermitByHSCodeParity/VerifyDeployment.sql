/* =====================================================================================
   Export Permit By HS Code parity deployment - 2026-09-21
   Verification. Run section 1 BEFORE deploying (it records what is on the server today)
   and again AFTER; run sections 2-4 after. Section 5 is a browser check.

   Window: '2025-01-01 00:00:00' .. '2025-12-31 23:59:59'. 2025 is a CLOSED period, so the
   figures below are stable. They were measured on PROD on 2026-09-21 via the live API
   (reportapi.myanmartradenet.com, FilterType Start, HSCode '', Sakhan 0, Section 0), paging
   the whole result: totalCount 605 rows BEFORE the fix collapsing to 353 distinct
   (HS code, currency) pairs, which is exactly the old report's row count; footer
   Total No of License 1,147; sum of every Total Value 144,929,409.1551.
   An empty window looks exactly like a broken procedure - keep the 2025 window.
   ===================================================================================== */

USE [TradeNetDB];
GO

-- -------------------------------------------------------------------------------------
-- 1. What is deployed right now - inspecting ONLY the 'Export Permit' branch. Other
--    branches still (deliberately) group by company - Import Licence, Border Export Permit
--    and the Border Import Permit drill sub-branches - so a whole-definition LIKE cannot
--    tell this release apart from the previous one. The text between the 'Export Permit'
--    and 'Import Permit' markers is exactly that branch.
--    AFTER deployment both grain and page_order must read 'ok'.
-- -------------------------------------------------------------------------------------
DECLARE @def nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_HSCodeReport_pagination'));
DECLARE @s int = CHARINDEX('(@FormType=''Export Permit'')', @def);
DECLARE @e int = CHARINDEX('(@FormType=''Import Permit'')', @def);

IF @def IS NULL
    PRINT 'dbo.sp_HSCodeReport_pagination not found in this database - wrong database?';
ELSE IF @s = 0 OR @e = 0 OR @e <= @s
    PRINT 'branch marker not found - the deployed text has neither/only one of the Export Permit / Import Permit markers; inspect OBJECT_DEFINITION by hand';
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
-- 2. The WHOLE new result for 2025, collected by paging 0..1 at 1000 rows a page
--    (353 rows -> page 0 holds all 353, page 1 is empty). @IncludeTotalCount=1 so no
--    sentinel row is fetched. The temp table has the outer SELECT's 8 columns exactly, or
--    INSERT..EXEC fails. HSDescription is nvarchar(max) because HSCode.Description is
--    unbounded - a fixed width would abort the whole section with Msg 2628 on one long
--    description and read like a broken procedure.
-- -------------------------------------------------------------------------------------
IF OBJECT_ID(N'tempdb..#ep') IS NOT NULL DROP TABLE #ep;
CREATE TABLE #ep (
    HSCode nvarchar(50) NULL, HSDescription nvarchar(max) NULL,
    CompanyRegistrationNo nvarchar(200) NULL, CompanyName nvarchar(500) NULL,
    Currency nvarchar(200) NULL, NoOfLicences int NULL, TotalValue decimal(38, 6) NULL,
    TotalCount int NULL);

DECLARE @page int = 0;
WHILE @page <= 1
BEGIN
    INSERT INTO #ep
    EXEC dbo.sp_HSCodeReport_pagination
        @FromDate = '2025-01-01 00:00:00',
        @ToDate   = '2025-12-31 23:59:59',
        @FormType = N'Export Permit',
        @FilterType = N'Start',
        @HSCode   = N'',
        @SakhanId = 0,
        @PageIndex = @page, @PageSize = 1000, @IncludeTotalCount = 1;
    SET @page += 1;
END
GO

-- 2a. Each (HS Code, Currency) pair must appear EXACTLY ONCE. Must return NO rows.
--     Before the fix 87 pairs were split; the worst, 3702529000 / USD, came back 16 times.
SELECT HSCode, Currency, COUNT(*) AS duplicate_rows
FROM #ep
GROUP BY HSCode, Currency
HAVING COUNT(*) > 1
ORDER BY duplicate_rows DESC;
GO

-- 2b. Expected:  rows_returned 353 | total_count 353 | rows_with_company 0
--                | sum_total_value 144929409.1551 EXACTLY - Amount is decimal(18,4), so the
--                SUM is exact; any difference means rows are missing or double-counted.
--     CompanyRegistrationNo / CompanyName must be NULL on every row (the columns stay on
--     the result set only so the DTO and the HS Code detail drill keep one shape).
SELECT
    COUNT(*) AS rows_returned,
    MAX(TotalCount) AS total_count,
    SUM(CASE WHEN CompanyName IS NOT NULL OR CompanyRegistrationNo IS NOT NULL THEN 1 ELSE 0 END) AS rows_with_company,
    SUM(TotalValue) AS sum_total_value
FROM #ep;
GO

-- 2c. The complaint's own row. Expected exactly ONE row:
--     8807300000 | USD | 6 | 12950.000000   (was three rows of 1/500, 4/3450, 1/9000)
SELECT HSCode, Currency, NoOfLicences, TotalValue
FROM #ep
WHERE HSCode = '8807300000'
ORDER BY Currency;
GO

-- 2d. Per-currency row counts, for the record.
SELECT Currency, COUNT(*) AS rows_after FROM #ep GROUP BY Currency ORDER BY rows_after DESC;
GO

-- -------------------------------------------------------------------------------------
-- 3. The LEGACY oracle. dbo.sp_HSCodeReport is what the old admin app calls; it returns
--    raw ITEM rows (no SQL grouping) and HSCodeReport.rdlc groups them on
--    (HSCodeId, Currency). So COUNT(DISTINCT HSCodeId|Currency) over its output IS the old
--    report's row count, and COUNT(DISTINCT LicenceNo) IS its TOTAL footer.
--    Column list in order, per docs/StoredProcedureDefinitions.sql (sp_HSCodeReport,
--    'Export Permit' branch): SectionCode, HSCodeId, HSCode, HSDescription, Amount,
--    Currency, LicenceNo, CompanyRegistrationNo, CompanyName. NINE columns - the oversea
--    branches carry no SakhanId, unlike the Border ones.
--    If this INSERT fails on the column count, the legacy procedure has been overwritten
--    by docs/sp_HSCodeReport_AggregatePagination.sql - see CaptureRollback.sql.
-- -------------------------------------------------------------------------------------
IF OBJECT_ID(N'tempdb..#legacy') IS NOT NULL DROP TABLE #legacy;
CREATE TABLE #legacy (
    SectionCode nvarchar(50) NULL, HSCodeId int NULL, HSCode nvarchar(50) NULL,
    HSDescription nvarchar(max) NULL, Amount decimal(38, 6) NULL, Currency nvarchar(200) NULL,
    LicenceNo nvarchar(100) NULL, CompanyRegistrationNo nvarchar(200) NULL, CompanyName nvarchar(500) NULL);

INSERT INTO #legacy
EXEC dbo.sp_HSCodeReport
    @FromDate = '2025-01-01 00:00:00',
    @ToDate   = '2025-12-31 23:59:59',
    @FormType = N'Export Permit',
    @FilterType = N'Start',
    @HSCode   = N'',
    @SakhanId = 0;
GO

-- 3a. Expected:  old_report_rows 353 | total_no_of_licence 1147
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
FULL OUTER JOIN #ep n ON n.HSCode = o.HSCode AND n.Currency = o.Currency
WHERE o.HSCode IS NULL OR n.HSCode IS NULL
   OR o.TotalValue <> n.TotalValue
   OR o.NoOfLicences <> n.NoOfLicences
ORDER BY 1, 2;
GO

-- -------------------------------------------------------------------------------------
-- 4. Page-window stability. Paging the whole 2025 result 10 rows at a time must yield
--    exactly 353 rows and exactly 353 DISTINCT (HS Code, Currency) rows: a smaller
--    distinct count means OFFSET/FETCH is repeating rows across pages while dropping
--    others (the old ORDER BY was not a unique key once CompanyName became NULL). Stops at
--    the first empty page; the ceiling of 100 is only a guard.
-- -------------------------------------------------------------------------------------
IF OBJECT_ID(N'tempdb..#pages') IS NOT NULL DROP TABLE #pages;
CREATE TABLE #pages (
    PageIndex int NULL,   -- stamped after each INSERT..EXEC; the EXEC cannot supply it
    HSCode nvarchar(50) NULL, HSDescription nvarchar(max) NULL,
    CompanyRegistrationNo nvarchar(200) NULL, CompanyName nvarchar(500) NULL,
    Currency nvarchar(200) NULL, NoOfLicences int NULL, TotalValue decimal(38, 6) NULL,
    TotalCount int NULL);

DECLARE @page int = 0, @got int = 1;
WHILE @page <= 100 AND @got > 0
BEGIN
    INSERT INTO #pages (HSCode, HSDescription, CompanyRegistrationNo, CompanyName, Currency, NoOfLicences, TotalValue, TotalCount)
    EXEC dbo.sp_HSCodeReport_pagination
        @FromDate = '2025-01-01 00:00:00',
        @ToDate   = '2025-12-31 23:59:59',
        @FormType = N'Export Permit',
        @FilterType = N'Start',
        @HSCode   = N'',
        @SakhanId = 0,
        @PageIndex = @page, @PageSize = 10, @IncludeTotalCount = 1;
    SET @got = @@ROWCOUNT;

    UPDATE #pages SET PageIndex = @page WHERE PageIndex IS NULL;
    SET @page += 1;
END
GO

-- Expected:  rows_paged 353 | distinct_rows 353 | last_page_with_rows 35
--            (353 rows over pages of 10 -> pages 0..35, the last one holding 3 rows)
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
--    HS Code cell must open ExportPermitHSCodeDetailReport in a NEW TAB, showing a Company
--    Name column with each company once (no currency split). For 01-03/01/2025 the
--    8807300000 drill must list 3 companies whose No of Licences add up to the summary's 6.
--    Also new in the same release: the Export Section dropdown (the old .cshtml had it and
--    the new filter box did not) and the removal of the inert Form Type text box.
-- -------------------------------------------------------------------------------------

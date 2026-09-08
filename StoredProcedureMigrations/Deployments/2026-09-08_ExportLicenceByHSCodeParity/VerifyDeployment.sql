/* =====================================================================================
   Export Licence / Border Export Licence By HS Code parity deployment - 2026-09-08
   Verification. Run section 1 BEFORE deploying (it records what is on the server today)
   and again AFTER; run sections 2-5 after.

   The customer's window is 31/08-01/09/2026 and it DOES hold data on the production
   report database (measured on the live API: 961 Export Licences / 41 Border Export
   Licences). If you are verifying against a copy whose data stops in 2025, use
   '2025-01-01' .. '2025-12-31 23:59:59' instead - an empty window looks exactly like a
   broken procedure.
   ===================================================================================== */

USE [TradeNetDB];
GO

-- -------------------------------------------------------------------------------------
-- 1. What is deployed right now
--    AFTER deployment all four columns must read the 'ok' value. A 'stale' value means
--    the server still carries the older definition, which is exactly what the customer
--    complaint describes.
-- -------------------------------------------------------------------------------------
SELECT
    p.name,
    p.modify_date,
    m.uses_quoted_identifier,
    -- 7 sub-branches regrouped: Export Licence 4 + Border Export Licence 3. The other six
    -- FormTypes still group by company on purpose, so this cannot be a global count.
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%ORDER BY result.HSCode,result.Currency,result.HSCodeId%'
         THEN 'unique page order' ELSE 'ambiguous order (stale)' END AS page_order,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%ORDER BY result.HSCode,result.CompanyName,result.Currency%'
         THEN 'company order still present -- see note' ELSE 'no company order' END AS company_order,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id%INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id%WHERE ExportLicence.ApplyType=''New''%'
         THEN 'fast page joins section' ELSE 'fast page missing section (stale)' END AS export_licence_fast_page
FROM sys.procedures p
    JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name = 'sp_HSCodeReport_pagination';
GO
-- NOTE on company_order: Import Licence, Export Permit, Border Import Licence and the
-- Border Import Permit drill sub-branch still order by CompanyName - they still group by
-- company, deliberately. So 'company order still present' is EXPECTED; it is only a
-- problem if section 2 or 3 below returns rows.

-- -------------------------------------------------------------------------------------
-- 2. Export Licence By HS Code: each (HS Code, Currency) pair must appear EXACTLY ONCE.
--    Before the fix the same HS Code came back once per buyer company, each row carrying
--    only that buyer's Total Value. This query must return NO rows.
-- -------------------------------------------------------------------------------------
IF OBJECT_ID(N'tempdb..#hs') IS NOT NULL DROP TABLE #hs;
CREATE TABLE #hs (
    HSCode nvarchar(50) NULL, HSDescription nvarchar(1000) NULL,
    CompanyRegistrationNo nvarchar(200) NULL, CompanyName nvarchar(500) NULL,
    Currency nvarchar(50) NULL, NoOfLicences int NULL, TotalValue decimal(38, 6) NULL,
    TotalCount int NULL);

INSERT INTO #hs
EXEC dbo.sp_HSCodeReport_pagination
    @FromDate = '2026-08-31 00:00:00',
    @ToDate   = '2026-09-01 23:59:59',
    @FormType = N'Export Licence',
    @FilterType = N'Start',
    @HSCode   = N'',
    @SakhanId = 0,
    @PageIndex = 0, @PageSize = 1000, @IncludeTotalCount = 1;

-- Must return NO rows. Before the fix an HS code sold by 3 companies returned 3.
SELECT HSCode, Currency, COUNT(*) AS duplicate_rows
FROM #hs
GROUP BY HSCode, Currency
HAVING COUNT(*) > 1
ORDER BY duplicate_rows DESC;
GO

-- The row count and TotalCount must agree with the old report: 304 for this window.
-- CompanyRegistrationNo / CompanyName must be NULL on every row (the columns stay on the
-- result set only so the DTO and the HS Code detail drill keep one shape).
SELECT
    COUNT(*) AS rows_returned,
    MAX(TotalCount) AS total_count,
    SUM(CASE WHEN CompanyName IS NOT NULL OR CompanyRegistrationNo IS NOT NULL THEN 1 ELSE 0 END) AS rows_with_company
FROM #hs;
GO

-- -------------------------------------------------------------------------------------
-- 3. Border Export Licence By HS Code: same assertions, expected 33 rows.
-- -------------------------------------------------------------------------------------
IF OBJECT_ID(N'tempdb..#bhs') IS NOT NULL DROP TABLE #bhs;
CREATE TABLE #bhs (
    HSCode nvarchar(50) NULL, HSDescription nvarchar(1000) NULL,
    CompanyRegistrationNo nvarchar(200) NULL, CompanyName nvarchar(500) NULL,
    Currency nvarchar(50) NULL, NoOfLicences int NULL, TotalValue decimal(38, 6) NULL,
    TotalCount int NULL);

INSERT INTO #bhs
EXEC dbo.sp_HSCodeReport_pagination
    @FromDate = '2026-08-31 00:00:00',
    @ToDate   = '2026-09-01 23:59:59',
    @FormType = N'Border Export Licence',
    @FilterType = N'Start',
    @HSCode   = N'',
    @SakhanId = 0,
    @PageIndex = 0, @PageSize = 1000, @IncludeTotalCount = 1;

SELECT HSCode, Currency, COUNT(*) AS duplicate_rows
FROM #bhs
GROUP BY HSCode, Currency
HAVING COUNT(*) > 1
ORDER BY duplicate_rows DESC;
GO

SELECT
    COUNT(*) AS rows_returned,
    MAX(TotalCount) AS total_count,
    SUM(CASE WHEN CompanyName IS NOT NULL OR CompanyRegistrationNo IS NOT NULL THEN 1 ELSE 0 END) AS rows_with_company
FROM #bhs;
GO

-- -------------------------------------------------------------------------------------
-- 4. The fast page (what the grid actually renders) must cover the SAME set as the
--    counted branch, or the pager offers pages that have no rows. Paging the whole
--    result 10 rows at a time must yield exactly TotalCount distinct rows.
--    Only 'Export Licence' has a fast page; 'Border Export Licence' always counts.
-- -------------------------------------------------------------------------------------
IF OBJECT_ID(N'tempdb..#fast') IS NOT NULL DROP TABLE #fast;
CREATE TABLE #fast (
    PageIndex int NULL,   -- stamped after each INSERT..EXEC; the EXEC cannot supply it
    HSCode nvarchar(50) NULL, HSDescription nvarchar(1000) NULL,
    CompanyRegistrationNo nvarchar(200) NULL, CompanyName nvarchar(500) NULL,
    Currency nvarchar(50) NULL, NoOfLicences int NULL, TotalValue decimal(38, 6) NULL,
    TotalCount int NULL);

DECLARE @page int = 0;
WHILE @page < 40   -- 31 pages of 10 for 304 rows; the spare pages must come back empty
BEGIN
    INSERT INTO #fast (HSCode, HSDescription, CompanyRegistrationNo, CompanyName, Currency, NoOfLicences, TotalValue, TotalCount)
    EXEC dbo.sp_HSCodeReport_pagination
        @FromDate = '2026-08-31 00:00:00',
        @ToDate   = '2026-09-01 23:59:59',
        @FormType = N'Export Licence',
        @FilterType = N'Start',
        @HSCode   = N'',
        @SakhanId = 0,
        @PageIndex = @page, @PageSize = 10, @IncludeTotalCount = 0;

    UPDATE #fast SET PageIndex = @page WHERE PageIndex IS NULL;
    SET @page += 1;
END

-- rows_paged: the fast page returns one SENTINEL row beyond the page (@FetchSize), which the
-- application trims -- so expect a little more than 304 here. What must hold is
-- distinct_rows = section 2's total_count (304): a smaller number means the page window is
-- still ambiguous and OFFSET/FETCH is repeating rows across pages while dropping others.
SELECT COUNT(*) AS rows_paged, COUNT(DISTINCT CONCAT(HSCode, '|', Currency)) AS distinct_rows
FROM #fast;
GO

-- ...and the pages past the end must be empty: last_page_with_rows should be 30 (0-based,
-- 304 rows over pages of 10), not something beyond it.
SELECT MAX(PageIndex) AS last_page_with_rows FROM #fast;
GO

-- -------------------------------------------------------------------------------------
-- 5. The HS Code DETAIL drill is NOT served by this procedure -- it posts
--    GroupBy='Company' and runs the LINQ twin, which groups on
--    (HSCodeId, HSCode, CompanyRegistrationNo). Nothing to verify in SQL; check it in the
--    browser: the HS Code cell must open the detail report in a new tab, showing a Company
--    Name column with each company once (no currency split).
-- -------------------------------------------------------------------------------------

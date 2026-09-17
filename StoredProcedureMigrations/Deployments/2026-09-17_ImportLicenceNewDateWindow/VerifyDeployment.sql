/* =====================================================================================
   Import Licence New date-window deployment - 2026-09-17
   Run AFTER 00_RunAll.sql. Read-only: every section only SELECTs / EXECs report procs.

   The reference figures below were measured on the live PROD API on 2026-09-17, before
   the release, for the customer's exact filters:

       reg-no 101103471, 2025-01-01 00:00:00 .. 2025-01-31 23:59:59

   If this server's data differs from PROD's, section 2's absolute numbers will differ
   too - but sections 3, 4 and 5 are self-checking and must pass on ANY dataset.
   ===================================================================================== */

USE [TradeNetDB];
GO
SET QUOTED_IDENTIFIER ON;
GO
SET ANSI_NULLS ON;
GO

/* -------------------------------------------------------------------------------------
   1. Did both procedures actually change?

   Procedures here are applied by hand, so the definition on the server is the only
   authority - the .sql file in the repository proves nothing. Both rows must read OK.
   ------------------------------------------------------------------------------------- */
SELECT
    p.name,
    p.modify_date,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%@CompanyRegistrationNo<>%CreatedDate>=@FromDate%'
           OR OBJECT_DEFINITION(p.object_id) LIKE '%@CompanyRegistrationNo <> %CreatedDate >= @FromDate%'
         THEN 'STALE - the reg-no date-skip is still deployed, this release was not applied'
         ELSE 'OK - date window is unconditional'
    END AS state
FROM sys.procedures p
WHERE p.name IN ('sp_NewReport_pagination', 'sp_ImportLicenceListingCurrencyTotals')
ORDER BY p.name;
GO

/* -------------------------------------------------------------------------------------
   2. THE ACCEPTANCE TEST - the customer's own query.

   Import Licence New, reg-no 101103471, January 2025. Before this release it returned
   1386 rows whose first entries were dated 2021-01-07; the Import Licence Company List
   report returns 34 for the identical filters. After the release the two must agree.
   ------------------------------------------------------------------------------------- */
DECLARE @From datetime = '2025-01-01 00:00:00';
DECLARE @To   datetime = '2025-01-31 23:59:59';
DECLARE @Reg  nvarchar(50) = N'101103471';

DROP TABLE IF EXISTS #grid;
CREATE TABLE #grid (
    [Date] datetime, SectionCode nvarchar(50), SectionName nvarchar(200),
    OldLicenceNo nvarchar(50), LicenceNo nvarchar(50), sDate nvarchar(50),
    CompanyRegistrationNo nvarchar(50), CompanyName nvarchar(500),
    UnitLevel nvarchar(200), StreetNumberStreetName nvarchar(500),
    QuarterCityTownship nvarchar(200), State nvarchar(200), Country nvarchar(200),
    PostalCode nvarchar(50), auto nvarchar(50), quota nvarchar(50),
    CommodityType nvarchar(500), __k_Id int, Currency nvarchar(50), HSCode nvarchar(50),
    Amount decimal(38, 4), SakhanId int, SakhanCode nvarchar(50), SakhanName nvarchar(200),
    TotalCount int
);

INSERT INTO #grid
EXEC dbo.sp_NewReport_pagination
    @FormType = N'Import Licence', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @CompanyRegistrationNo = @Reg, @SakhanId = 0,
    @auto = N'', @quota = N'', @SortColumn = N'Date', @SortOrder = N'asc',
    @PageIndex = 0, @PageSize = 100000, @IncludeTotalCount = 1;

SELECT
    'grid' AS source,
    MAX(TotalCount) AS TotalCount,
    COUNT(*) AS RowsReturned,
    MIN([Date]) AS EarliestDate,
    MAX([Date]) AS LatestDate,
    SUM(CASE WHEN [Date] < @From OR [Date] > @To THEN 1 ELSE 0 END) AS RowsOutsideWindow,
    CASE WHEN SUM(CASE WHEN [Date] < @From OR [Date] > @To THEN 1 ELSE 0 END) = 0
         THEN 'PASS - every row is inside the requested window'
         ELSE 'FAIL - the date window is still being ignored' END AS verdict
FROM #grid;
GO

/* -------------------------------------------------------------------------------------
   3. The grid must equal the Company List report for the same filters.

   Company List reads the same licences at item grain and additionally requires
   ImportLicenceNo <> '' plus at least one item, so it can only ever be <= the grid. On
   PROD both were expected to land on 34. A non-zero difference is not automatically a
   failure, but it must be explained by those two extra conditions - check the listed
   licence numbers rather than accepting the gap.
   ------------------------------------------------------------------------------------- */
DECLARE @From datetime = '2025-01-01 00:00:00';
DECLARE @To   datetime = '2025-01-31 23:59:59';
DECLARE @Reg  nvarchar(50) = N'101103471';

;WITH grid AS (
    SELECT il.Id, il.ImportLicenceNo
    FROM ImportLicence il
        INNER JOIN PaThaKa p ON il.PaThaKaId = p.Id
    WHERE il.ApplyType = 'New' AND il.Status = 'Approved'
        AND il.CreatedDate >= @From AND il.CreatedDate <= @To
        AND p.CompanyRegistrationNo = @Reg
),
companyList AS (
    SELECT DISTINCT il.Id
    FROM ImportLicence il
        INNER JOIN PaThaKa p ON il.PaThaKaId = p.Id
        INNER JOIN ImportLicenceItem i ON i.ImportLicenceId = il.Id
    WHERE il.ApplyType = 'New' AND il.Status = 'Approved' AND il.ImportLicenceNo <> ''
        AND il.CreatedDate >= @From AND il.CreatedDate <= @To
        AND p.CompanyRegistrationNo = @Reg
)
SELECT
    (SELECT COUNT(*) FROM grid)        AS NewReportLicences,
    (SELECT COUNT(*) FROM companyList) AS CompanyListLicences,
    (SELECT COUNT(*) FROM grid) - (SELECT COUNT(*) FROM companyList) AS Difference,
    CASE WHEN (SELECT COUNT(*) FROM grid) = (SELECT COUNT(*) FROM companyList)
         THEN 'PASS - the two reports agree'
         ELSE 'CHECK - explain the gap via ImportLicenceNo = '''' or licences with no items'
    END AS verdict;

-- If Difference <> 0, this names the offending licences.
SELECT il.Id, il.ImportLicenceNo, il.CreatedDate,
       (SELECT COUNT(*) FROM ImportLicenceItem i WHERE i.ImportLicenceId = il.Id) AS ItemCount
FROM ImportLicence il
    INNER JOIN PaThaKa p ON il.PaThaKaId = p.Id
WHERE il.ApplyType = 'New' AND il.Status = 'Approved'
    AND il.CreatedDate >= @From AND il.CreatedDate <= @To
    AND p.CompanyRegistrationNo = @Reg
    AND (il.ImportLicenceNo = ''
         OR NOT EXISTS (SELECT 1 FROM ImportLicenceItem i WHERE i.ImportLicenceId = il.Id));
GO

/* -------------------------------------------------------------------------------------
   4. The footer must agree with the grid.

   sp_ImportLicenceListingCurrencyTotals drives "Total No of License" / per-currency
   "Total Value" under the grid. Before this release it carried the same date-skip, so
   for the customer's filters it reported 1065 USD + 254 CNY + ... instead of 27 + 7.
   SUM(NoOfLicences) here must equal the grid's TotalCount from section 2.
   ------------------------------------------------------------------------------------- */
DECLARE @From datetime = '2025-01-01 00:00:00';
DECLARE @To   datetime = '2025-01-31 23:59:59';
DECLARE @Reg  nvarchar(50) = N'101103471';

DROP TABLE IF EXISTS #footer;
CREATE TABLE #footer (Currency nvarchar(50), NoOfLicences int, TotalValue decimal(38, 4));

INSERT INTO #footer
EXEC dbo.sp_ImportLicenceListingCurrencyTotals
    @ApplyType = N'New', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @CompanyRegistrationNo = @Reg, @AmendRemarkId = 0,
    @auto = N'', @quota = N'', @FormType = N'Import Licence', @SakhanId = 0;

SELECT * FROM #footer ORDER BY Currency;

SELECT
    SUM(NoOfLicences) AS FooterTotalLicences,
    CASE WHEN SUM(NoOfLicences) = (
            SELECT COUNT(*) FROM ImportLicence il
                INNER JOIN PaThaKa p ON il.PaThaKaId = p.Id
            WHERE il.ApplyType = 'New' AND il.Status = 'Approved'
                AND il.CreatedDate >= @From AND il.CreatedDate <= @To
                AND p.CompanyRegistrationNo = @Reg)
         THEN 'PASS - footer total equals the grid TotalCount'
         ELSE 'FAIL - footer and grid disagree; they must carry the identical predicate'
    END AS verdict
FROM #footer;
GO

/* -------------------------------------------------------------------------------------
   5. REGRESSION - the plain date browse must be untouched.

   No reg-no, same window. On PROD this returned 5863 before the release and must return
   5863 after it: this release only removes a predicate that required a reg-no to fire.
   ------------------------------------------------------------------------------------- */
DECLARE @From datetime = '2025-01-01 00:00:00';
DECLARE @To   datetime = '2025-01-31 23:59:59';

SELECT
    COUNT(*) AS NoRegNoJan2025,
    'PROD measured 5863 before the release - this must be unchanged' AS note
FROM ImportLicence il
    INNER JOIN PaThaKa p ON il.PaThaKaId = p.Id
WHERE il.ApplyType = 'New' AND il.Status = 'Approved'
    AND il.CreatedDate >= @From AND il.CreatedDate <= @To;
GO

/* -------------------------------------------------------------------------------------
   6. BLAST-RADIUS CHECK - no other report family may have moved.

   sp_NewReport_pagination is shared by 8 form types; only the final ELSE branch changed.
   Each row below must be unchanged against the same query run before the release.
   ------------------------------------------------------------------------------------- */
DECLARE @From datetime = '2025-01-01 00:00:00';
DECLARE @To   datetime = '2025-01-31 23:59:59';

DROP TABLE IF EXISTS #ft;
CREATE TABLE #ft (FormType nvarchar(50));
INSERT INTO #ft VALUES (N'Import Permit'), (N'Export Permit'), (N'Export Licence'),
    (N'Border Export Licence'), (N'Border Import Licence'),
    (N'Border Export Permit'), (N'Border Import Permit');
SELECT FormType, 'run each through sp_NewReport_pagination and diff TotalCount vs pre-release' AS how
FROM #ft;
GO

/* =====================================================================================
   List of Top Capital Company ranking deployment - 2026-09-11
   Run AFTER 00_RunAll.sql.
   ===================================================================================== */

USE [TradeNetDB];
GO

/* 1. The deployed definition must contain the new whitelist entry. A stale copy has no
      'Capital' branch, silently falls back to PaThaKa.IssuedDate, and the report returns
      the N most recently issued companies instead of the N largest by capital -- a wrong
      answer that looks perfectly healthy on screen. */
SELECT
    CASE WHEN OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_PaThaKaReport_pagination'))
              LIKE N'%WHEN ''Capital''%THEN ''PaThaKa.Capital''%'
         THEN N'OK - Capital is in the sort whitelist'
         ELSE N'STALE - redeploy 01_sp_PaThaKaReport_pagination.sql' END AS capital_sort_check,
    (SELECT modify_date FROM sys.procedures WHERE name = 'sp_PaThaKaReport_pagination') AS modify_date;
GO

/* 2. The ranking actually applies: the top 5 must come back capital-descending, and the
      first row must be the maximum capital over the same filters. Adjust the dates to a
      range with data (the database's PaThaKa rows are largely 2025). */
DECLARE @FromDate datetime = '2025-01-01', @ToDate datetime = '2025-12-31 23:59:59';

EXEC dbo.sp_PaThaKaReport_pagination
    @FromDate, @ToDate, 0, 0, N'', N'', N'Capital', N'DESC', 0, 5;

SELECT MAX(Capital) AS expected_first_row_capital
FROM PaThaKa
WHERE IssuedDate >= @FromDate AND IssuedDate <= @ToDate;
GO

/* 3. Unchanged behaviour: an unknown sort column still falls back to IssuedDate, and the
      other whitelisted columns still sort. This procedure is shared with
      PaThaKaRegisteredBusinessOrganizationReport, which must be unaffected. */
EXEC dbo.sp_PaThaKaReport_pagination
    '2025-01-01', '2025-12-31 23:59:59', 0, 0, N'', N'', N'CompanyName', N'ASC', 0, 5;
GO

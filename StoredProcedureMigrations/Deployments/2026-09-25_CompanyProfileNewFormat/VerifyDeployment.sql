/* =====================================================================================
   Company Profile "11 ministries" layout deployment - 2026-09-25
   Run AFTER 00_RunAll.sql.
   ===================================================================================== */

USE [TradeNetDB];
GO

/* 1. The deployed definition must return the two new columns and order the directors by
      SortOrder. A stale copy makes the new application fail with HTTP 500. */
SELECT
    CASE WHEN OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_CompanyProfileReport_pagination')) LIKE N'%AS CapitalCurrency%'
              AND OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_CompanyProfileReport_pagination')) LIKE N'%PaThaKa.StartDate%'
              AND OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_CompanyProfileReport_pagination')) LIKE N'%PaThaKaDirectors.SortOrder%'
         THEN N'OK - StartDate, CapitalCurrency and the director SortOrder are in'
         ELSE N'STALE - redeploy 01_sp_CompanyProfileReport_pagination.sql' END AS company_profile_check,
    (SELECT modify_date FROM sys.procedures WHERE name = 'sp_CompanyProfileReport_pagination') AS modify_date;
GO

/* 2. The customer's sample company 145327946 (EIR 1-8-2026 to 31-7-2031): StartDate must be
      2026-08-01, CapitalCurrency MMK, and the directors should read WANG XINGANG, LI ZHEN,
      DAW YIN THIRI HLAING - the order of the customer's sample. If they do not, report it:
      the directors' SortOrder does not hold the registration order. */
EXEC dbo.sp_CompanyProfileReport_pagination
    '2026-08-01', '2026-08-31 23:59:59', N'145327946', NULL, NULL, 0, 10;

SELECT d.Name, d.SortOrder, d.Id
FROM PaThaKaDirectors d
JOIN PaThaKa p ON p.Id = d.PaThaKaId
WHERE p.CompanyRegistrationNo = N'145327946'
ORDER BY ISNULL(d.SortOrder, 2147483647), d.Id;
GO

/* 3. Unchanged behaviour: the page of companies and TotalCount. TotalCount below must equal
      the distinct-company count, exactly as before this release. */
DECLARE @FromDate datetime = '2026-08-01', @ToDate datetime = '2026-08-31 23:59:59';

EXEC dbo.sp_CompanyProfileReport_pagination @FromDate, @ToDate, N'', NULL, NULL, 0, 10;

SELECT COUNT(*) AS expected_total_count
FROM PaThaKa
WHERE IssuedDate >= @FromDate AND IssuedDate <= @ToDate
  AND EXISTS (SELECT 1 FROM PaThaKaDirectors WHERE PaThaKaDirectors.PaThaKaId = PaThaKa.Id);
GO

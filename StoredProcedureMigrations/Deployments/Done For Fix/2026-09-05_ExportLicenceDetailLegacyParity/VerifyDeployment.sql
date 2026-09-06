/* =====================================================================================
   Export Licence Detail parity deployment - 2026-09-05
   Run section 1 BEFORE deploying (it records what is on the server today) and again
   AFTER; run sections 2 and 3 after. Section 3 is the real test: the new procedure must
   return the SAME rows as the LEGACY dbo.sp_ExportLicenceDetailReport the old admin calls.
   ===================================================================================== */

USE [TradeNetDB];
GO

-- -------------------------------------------------------------------------------------
-- 1. What is deployed right now. AFTER deployment: one row, uses_quoted_identifier = 1
--    (the CSV expanders call XML .value(); a procedure created with QUOTED_IDENTIFIER OFF
--    fails at run time with Msg 1934), params = 12.
-- -------------------------------------------------------------------------------------
SELECT
    p.name,
    p.modify_date,
    m.uses_quoted_identifier,
    (SELECT COUNT(*) FROM sys.parameters prm WHERE prm.object_id = p.object_id) AS params,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%INTO #P%' THEN 'temp-table key paging' ELSE 'unexpected shape' END AS shape
FROM sys.procedures p
    JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name = N'sp_ExportLicenceDetailReportV3_pagination';
GO

-- -------------------------------------------------------------------------------------
-- 2. The report as the customer runs it: a two-day window, first page of 10, then the
--    two filters that "returned nothing" in the old grid. Each call must return rows and
--    a TotalCount at ITEM grain (one row per ExportLicenceItem). Pick a window with data
--    on this server (UAT: 2025 dates; production: the customer's own dates).
-- -------------------------------------------------------------------------------------
DECLARE @f datetime = '2025-08-31 00:00:00', @t datetime = '2025-09-01 23:59:59';

EXEC dbo.sp_ExportLicenceDetailReportV3_pagination @f, @t, 0, 0, 0, 0, 0, N'', 0, 10, 1, N'';        -- all
EXEC dbo.sp_ExportLicenceDetailReportV3_pagination @f, @t, 0, 0, 3, 0, 0, N'', 0, 10, 1, N'';        -- Method of export = CMP (3)
EXEC dbo.sp_ExportLicenceDetailReportV3_pagination @f, @t, 0, 0, 0, 12, 0, N'', 0, 10, 1, N'';       -- Incoterms = CIF (12)
GO

-- -------------------------------------------------------------------------------------
-- 3. Parity against the legacy procedure: same window, every row (@PageSize = 0). The two
--    row counts must be equal and BOTH EXCEPT queries must return no rows. On UAT this
--    measured 2683 = 2683 with an empty diff (2025-08-31..2025-09-01). Record the result
--    in README.md before signing this release off.
-- -------------------------------------------------------------------------------------
DECLARE @f datetime = '2025-08-31 00:00:00', @t datetime = '2025-09-01 23:59:59';

CREATE TABLE #legacy (
    PaThaKaTypeId int, PaThaKaTypeCode nvarchar(50), PaThaKaTypeName nvarchar(200),
    ExportImportSectionId int, ExportImportMethodId int, ExportImportIncotermId int, BuyerCountryId int,
    SectionCode nvarchar(50), SectionName nvarchar(200), LicenceNo nvarchar(100), LicenceDate datetime,
    CompanyRegistrationNo nvarchar(100), CompanyName nvarchar(500), UnitLevel nvarchar(500), StreetNumberStreetName nvarchar(500),
    QuarterCityTownship nvarchar(500), [State] nvarchar(200), Country nvarchar(200), PostalCode nvarchar(50),
    BuyerName nvarchar(500), BuyerAddress nvarchar(max), BuyerCountry nvarchar(200),
    PortofExport varchar(max), PortofDischarge nvarchar(max), LastDate datetime, MethodName nvarchar(200),
    DestinationCountry varchar(max), ConsignedCountry nvarchar(200), CountryofOrigin nvarchar(200),
    HSCode nvarchar(200), HSDescription nvarchar(max), Unit nvarchar(50), Price decimal(18,4), Quantity decimal(18,4), Amount decimal(18,4),
    Currency nvarchar(50), Conditions nvarchar(max), ApproveDate datetime);
INSERT INTO #legacy
EXEC dbo.sp_ExportLicenceDetailReport N'Oversea', @f, @t, 0, 0, 0, 0, 0, N'', 0;

CREATE TABLE #grid (
    PaThaKaTypeId int, PaThaKaTypeCode nvarchar(50), PaThaKaTypeName nvarchar(200),
    SakhanId int, SakhanCode nvarchar(50), SakhanName nvarchar(200),
    ExportImportSectionId int, ExportImportMethodId int, ExportImportIncotermId int, BuyerCountryId int,
    SectionCode nvarchar(50), SectionName nvarchar(200), LicenceNo nvarchar(100), LicenceDate datetime,
    CompanyRegistrationNo nvarchar(100), CompanyName nvarchar(500), UnitLevel nvarchar(500), StreetNumberStreetName nvarchar(500),
    QuarterCityTownship nvarchar(500), [State] nvarchar(200), Country nvarchar(200), PostalCode nvarchar(50),
    BuyerName nvarchar(500), BuyerAddress nvarchar(max), BuyerCountry nvarchar(200),
    PortofExport varchar(max), PortofDischarge nvarchar(max), LastDate datetime, MethodName nvarchar(200),
    DestinationCountry varchar(max), ConsignedCountry nvarchar(200), CountryofOrigin nvarchar(200),
    HSCode nvarchar(200), HSDescription nvarchar(max), Unit nvarchar(50), Price decimal(18,4), Quantity decimal(18,4), Amount decimal(18,4),
    Currency nvarchar(50), Conditions nvarchar(max),
    ApplicationNo nvarchar(100), ApplicationDate datetime, CommodityType nvarchar(400), ApproveDate datetime, TotalCount int);
INSERT INTO #grid
EXEC dbo.sp_ExportLicenceDetailReportV3_pagination @f, @t, 0, 0, 0, 0, 0, N'', 0, 0, 1, N'';

SELECT (SELECT COUNT(*) FROM #legacy) AS legacy_rows,
       (SELECT COUNT(*) FROM #grid)   AS grid_rows,
       (SELECT MAX(TotalCount) FROM #grid) AS grid_TotalCount;   -- all three must be equal

-- rows the old report prints that the grid does not (must be empty)
SELECT LicenceNo, HSCode, Unit, Price, Quantity, Amount, Currency, PortofExport, DestinationCountry FROM #legacy
EXCEPT
SELECT LicenceNo, HSCode, Unit, Price, Quantity, Amount, Currency, PortofExport, DestinationCountry FROM #grid;

-- rows the grid prints that the old report does not (must be empty)
SELECT LicenceNo, HSCode, Unit, Price, Quantity, Amount, Currency, PortofExport, DestinationCountry FROM #grid
EXCEPT
SELECT LicenceNo, HSCode, Unit, Price, Quantity, Amount, Currency, PortofExport, DestinationCountry FROM #legacy;

DROP TABLE #grid; DROP TABLE #legacy;
GO

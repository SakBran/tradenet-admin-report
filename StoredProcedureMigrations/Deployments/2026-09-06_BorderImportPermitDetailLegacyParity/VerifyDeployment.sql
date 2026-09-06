/* =====================================================================================
   Border Import Permit Detail legacy-parity deployment - 2026-09-06
   Run section 1 BEFORE deploying (it records what is on the server today) and again
   AFTER; run sections 2 and 3 after. Section 3 is the real test: the new procedure must
   return the SAME rows, cell for cell, as the LEGACY dbo.sp_ImportPermitDetailReport
   'Border' branch the old admin calls (Tradenet 2.0 ReportsController.cs:14557-14620).
   ===================================================================================== */

USE [TradeNetDB];
GO

-- -------------------------------------------------------------------------------------
-- 1. What is deployed right now. AFTER deployment: one row, uses_quoted_identifier = 1
--    (the CSV expanders call XML .value(); a procedure created with QUOTED_IDENTIFIER OFF
--    fails at run time with Msg 1934), params = 10, shape = 'temp-table key paging'.
-- -------------------------------------------------------------------------------------
SELECT
    p.name,
    p.modify_date,
    m.uses_quoted_identifier,
    (SELECT COUNT(*) FROM sys.parameters prm WHERE prm.object_id = p.object_id) AS params,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%INTO #P%' THEN 'temp-table key paging' ELSE 'unexpected shape' END AS shape,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%dbo.fn_GetNRCNo(BorderImportPermit.NRCType%' THEN 'legacy NRC function' ELSE 'MISSING fn_GetNRCNo' END AS nrc
FROM sys.procedures p
    JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name = N'sp_BorderImportPermitDetailReport_pagination';
GO

-- -------------------------------------------------------------------------------------
-- 2. The report as the customer runs it: the 2025 window, all Sakhan and Sakhan = TCL (4),
--    first page of 10. On the report database the 2025 window is 70 rows / 18 permits all
--    Sakhan and 20 rows for TCL -- the figures the customer quoted from the old report.
--    Every row carries TotalCount (item grain: one row per BorderImportPermitItem).
-- -------------------------------------------------------------------------------------
DECLARE @f datetime = '2025-01-01 00:00:00', @t datetime = '2025-12-31 23:59:59';

EXEC dbo.sp_BorderImportPermitDetailReport_pagination @f, @t, 0, 0, 0, N'', 0, 0, 10, 1;   -- all Sakhan
EXEC dbo.sp_BorderImportPermitDetailReport_pagination @f, @t, 0, 0, 0, N'', 4, 0, 10, 1;   -- Sakhan = TCL (Sakhan.Id 4)
GO

-- -------------------------------------------------------------------------------------
-- 3. Parity against the legacy procedure: same window, every row (@PageSize = 0). The two
--    row counts must be equal and BOTH EXCEPT queries must return no rows -- that is the
--    cell-for-cell proof over all 38 legacy columns, NRCNo and the CSV expanders included.
--    Record the result in README.md before signing this release off. Repeat with the
--    customer's own window if it differs.
-- -------------------------------------------------------------------------------------
DECLARE @f datetime = '2025-01-01 00:00:00', @t datetime = '2025-12-31 23:59:59';

CREATE TABLE #legacy (
    PaThaKaTypeId int, PaThaKaTypeCode nvarchar(max), PaThaKaTypeName nvarchar(max),
    SakhanId int, SakhanCode nvarchar(max), SakhanName nvarchar(max),
    ExportImportSectionId int, SellerCountryId int,
    SectionCode nvarchar(max), SectionName nvarchar(max), LicenceNo nvarchar(max), LicenceDate datetime,
    CompanyRegistrationNo nvarchar(max), CompanyName nvarchar(max), UnitLevel nvarchar(max), StreetNumberStreetName nvarchar(max),
    QuarterCityTownship nvarchar(max), [State] nvarchar(max), Country nvarchar(max), PostalCode nvarchar(max),
    AuthorisedAgentName nvarchar(max), AuthorisedAgentAddress nvarchar(max), SellerCountry nvarchar(max),
    PortofShipment nvarchar(max), PortofDischarge nvarchar(max), CountryofOrigin nvarchar(max), LastDate datetime,
    HSCode nvarchar(max), HSDescription nvarchar(max), Unit nvarchar(max), Price decimal(38,10), Quantity decimal(38,10), Amount decimal(38,10),
    Currency nvarchar(max), NRCNo nvarchar(max), PermitType nvarchar(max), Conditions nvarchar(max), ApproveDate datetime);
INSERT INTO #legacy
EXEC dbo.sp_ImportPermitDetailReport N'Border', @f, @t, 0, 0, 0, N'', 0;

CREATE TABLE #grid (
    PaThaKaTypeId int, PaThaKaTypeCode nvarchar(max), PaThaKaTypeName nvarchar(max),
    SakhanId int, SakhanCode nvarchar(max), SakhanName nvarchar(max),
    ExportImportSectionId int, SellerCountryId int,
    SectionCode nvarchar(max), SectionName nvarchar(max), LicenceNo nvarchar(max), LicenceDate datetime,
    CompanyRegistrationNo nvarchar(max), CompanyName nvarchar(max), UnitLevel nvarchar(max), StreetNumberStreetName nvarchar(max),
    QuarterCityTownship nvarchar(max), [State] nvarchar(max), Country nvarchar(max), PostalCode nvarchar(max),
    AuthorisedAgentName nvarchar(max), AuthorisedAgentAddress nvarchar(max), SellerCountry nvarchar(max),
    PortofShipment nvarchar(max), PortofDischarge nvarchar(max), CountryofOrigin nvarchar(max), LastDate datetime,
    HSCode nvarchar(max), HSDescription nvarchar(max), Unit nvarchar(max), Price decimal(38,10), Quantity decimal(38,10), Amount decimal(38,10),
    Currency nvarchar(max), NRCNo nvarchar(max), PermitType nvarchar(max), Conditions nvarchar(max), ApproveDate datetime, TotalCount int);
INSERT INTO #grid
EXEC dbo.sp_BorderImportPermitDetailReport_pagination @f, @t, 0, 0, 0, N'', 0, 0, 0, 1;

SELECT (SELECT COUNT(*) FROM #legacy) AS legacy_rows,
       (SELECT COUNT(*) FROM #grid)   AS grid_rows,
       (SELECT MAX(TotalCount) FROM #grid) AS grid_TotalCount;   -- all three must be equal

-- Rows the legacy report prints that the new one does not (must be empty).
SELECT PaThaKaTypeId, PaThaKaTypeCode, PaThaKaTypeName, SakhanId, SakhanCode, SakhanName, ExportImportSectionId, SellerCountryId,
           SectionCode, SectionName, LicenceNo, LicenceDate, CompanyRegistrationNo, CompanyName, UnitLevel, StreetNumberStreetName,
           QuarterCityTownship, [State], Country, PostalCode, AuthorisedAgentName, AuthorisedAgentAddress, SellerCountry,
           PortofShipment, PortofDischarge, CountryofOrigin, LastDate, HSCode, HSDescription, Unit, Price, Quantity, Amount,
           Currency, NRCNo, PermitType, Conditions, ApproveDate
FROM #legacy
EXCEPT
SELECT PaThaKaTypeId, PaThaKaTypeCode, PaThaKaTypeName, SakhanId, SakhanCode, SakhanName, ExportImportSectionId, SellerCountryId,
           SectionCode, SectionName, LicenceNo, LicenceDate, CompanyRegistrationNo, CompanyName, UnitLevel, StreetNumberStreetName,
           QuarterCityTownship, [State], Country, PostalCode, AuthorisedAgentName, AuthorisedAgentAddress, SellerCountry,
           PortofShipment, PortofDischarge, CountryofOrigin, LastDate, HSCode, HSDescription, Unit, Price, Quantity, Amount,
           Currency, NRCNo, PermitType, Conditions, ApproveDate
FROM #grid;

-- Rows the new report prints that the legacy one does not (must be empty).
SELECT PaThaKaTypeId, PaThaKaTypeCode, PaThaKaTypeName, SakhanId, SakhanCode, SakhanName, ExportImportSectionId, SellerCountryId,
           SectionCode, SectionName, LicenceNo, LicenceDate, CompanyRegistrationNo, CompanyName, UnitLevel, StreetNumberStreetName,
           QuarterCityTownship, [State], Country, PostalCode, AuthorisedAgentName, AuthorisedAgentAddress, SellerCountry,
           PortofShipment, PortofDischarge, CountryofOrigin, LastDate, HSCode, HSDescription, Unit, Price, Quantity, Amount,
           Currency, NRCNo, PermitType, Conditions, ApproveDate
FROM #grid
EXCEPT
SELECT PaThaKaTypeId, PaThaKaTypeCode, PaThaKaTypeName, SakhanId, SakhanCode, SakhanName, ExportImportSectionId, SellerCountryId,
           SectionCode, SectionName, LicenceNo, LicenceDate, CompanyRegistrationNo, CompanyName, UnitLevel, StreetNumberStreetName,
           QuarterCityTownship, [State], Country, PostalCode, AuthorisedAgentName, AuthorisedAgentAddress, SellerCountry,
           PortofShipment, PortofDischarge, CountryofOrigin, LastDate, HSCode, HSDescription, Unit, Price, Quantity, Amount,
           Currency, NRCNo, PermitType, Conditions, ApproveDate
FROM #legacy;

-- Order. The legacy procedure has no ORDER BY, so its order is plan-dependent; the new one
-- pages by permit CreatedDate, permit Id, ItemNo, item UniqueId. Eyeball the two sequences:
-- with a normal plan they agree (permits in creation order, items in line order).
SELECT TOP 40 'legacy' AS src, LicenceNo, HSCode, Amount FROM #legacy;
SELECT TOP 40 'new' AS src, LicenceNo, HSCode, Amount FROM #grid;

DROP TABLE #grid;
DROP TABLE #legacy;
GO

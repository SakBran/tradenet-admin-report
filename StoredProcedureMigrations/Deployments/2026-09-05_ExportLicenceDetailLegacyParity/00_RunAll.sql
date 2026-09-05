/* =====================================================================================
   Export Licence Detail parity deployment - 2026-09-05
   Run this ONE file (or 01_sp_ExportLicenceDetailReportV3_pagination.sql, it is the same
   procedure). PROCEDURES FIRST, APPLICATION SECOND -- although this release's application
   code falls back to a slower, equally correct query until the procedure exists, so the
   order is about speed, not correctness.

   Target database: TradeNetDB  (NOT ReportTemplateDB - that one only holds the Excel
   export job queue; deploying report procedures into it is a known trap.)

   What this adds: dbo.sp_ExportLicenceDetailReportV3_pagination, a NEW procedure. It is
   the legacy dbo.sp_ExportLicenceDetailReport ('Oversea' branch) query kept verbatim --
   same 13 INNER JOINs, same CASE WHEN @X = 0 filters, same CreatedDate <= @ToDate window,
   same select list -- with item-grain key-first paging wrapped around it (temp-table keys,
   one page of display rows). The Export Licence Detail grid moves onto it; the old grid
   paged licences while counting items, so pages past licences/PageSize were empty and
   the total never matched the old report.

   Nothing existing is altered or dropped. The legacy procedure is untouched: it is the
   oracle VerifyDeployment.sql section 3 compares against.

   QUOTED_IDENTIFIER must be ON when this runs (the CSV expanders call XML .value());
   the SET below and the one inside the procedure file both take care of it.

   Generated from the repository file of the same name; see README.md in this folder.
   ===================================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE [TradeNetDB];
GO

-- Wrong-database check: dbo.sp_ExportLicenceDetailReport is the legacy Tradenet 2.0 procedure
-- and lives only in the report database. This warns (it does not stop) if it is missing.
IF OBJECT_ID(N'dbo.sp_ExportLicenceDetailReport', N'P') IS NULL
    RAISERROR(N'WARNING: dbo.sp_ExportLicenceDetailReport was not found in [%s]. Are you connected to TradeNetDB?', 10, 1, DB_NAME()) WITH NOWAIT;
GO

-- ============================================================================
-- sp_ExportLicenceDetailReportV3_pagination   (file 01_sp_ExportLicenceDetailReportV3_pagination.sql)
-- ============================================================================
PRINT N'Applying sp_ExportLicenceDetailReportV3_pagination ...';
GO

/* =====================================================================================
   sp_ExportLicenceDetailReportV3_pagination

   The Export Licence Detail grid (oversea, non-Border). This is the LEGACY Tradenet 2.0
   procedure dbo.sp_ExportLicenceDetailReport (its 'Oversea' branch) kept VERBATIM
   (same 13 INNER JOINs, same CASE WHEN @X = 0 filters, same CreatedDate window, same
   select list) with item-grain key-first paging wrapped around it, so that:
     - every row the old report prints is returned, and nothing else (one row per
       ExportLicenceItem, exactly the legacy grain);
     - a page of N is N item rows, and TotalCount is the item count -- the previous grid
       paged LICENCES and counted ITEMS, so pages beyond licences/PageSize were empty;
     - the default three-month range answers in seconds. Running the legacy procedure
       itself and paging in C# is not an option: it took 15-17 s for a 2-day window and
       335 s for one month on UAT.

   Shape: #L (licence keys via the hinted filtered index + the licence-level legacy
   joins/predicates) -> #K (item keys via the item-level legacy joins) -> #P (the page,
   ROW_NUMBER over a deterministic order) -> the legacy select list for those keys only.
   The two FOR XML PATH CSV expanders therefore run for one page, not the whole range.
   Materialising #P BEFORE the display join matters: without it, page 0 of a 3-month
   window once took 103 s (plan flip on OFFSET 0 FETCH 10); with it, 4-7 s.

   Order: the legacy ORDER BY ExportLicence.LicenceDate (a date-only column) has no tie
   break, so within a day the old report's order was whatever the plan produced. The
   extra keys (IssuedDate, licence Id, ItemNo, item Id, UniqueId) only make paging
   deterministic; they never reorder across days.

   @Auto is additive (default N'' = no filter) so the By-Section / By-Method summaries,
   which carry their Auto / None-Auto choice into the drill-down, keep count parity.

   NEEDS SET QUOTED_IDENTIFIER ON at CREATE time: the CSV expanders call XML .value().
   ===================================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_ExportLicenceDetailReportV3_pagination]
    @FromDate datetime,
    @ToDate datetime,
    @PaThaKaTypeId int = 0,
    @ExportImportSectionId int = 0,
    @ExportImportMethodId int = 0,
    @ExportImportIncotermId int = 0,
    @BuyerCountryId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @PageIndex int = 0,
    @PageSize int = 10,
    @IncludeTotalCount bit = 1,
    @Auto nvarchar(20) = N''
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

    IF @PageIndex IS NULL OR @PageIndex < 0 SET @PageIndex = 0;
    -- @PageSize <= 0 means "every row" (the parity check against the legacy procedure).
    IF @PageSize IS NULL OR @PageSize <= 0 SET @PageSize = 2147483647;
    IF @CompanyRegistrationNo IS NULL SET @CompanyRegistrationNo = N'';
    IF @Auto IS NULL SET @Auto = N'';

    DECLARE @Offset bigint = CAST(@PageIndex AS bigint) * @PageSize;
    IF @Offset > 2147483647 SET @Offset = 2147483647;

    -- ---------------------------------------------------------------------------------
    -- 1. Licence keys. FROM / JOIN / WHERE are the legacy procedure's licence-level
    --    lines verbatim (incoterm is join-only there too). The filtered index covers
    --    ApplyType = 'New' AND Status = 'Approved' and is keyed on CreatedDate.
    -- ---------------------------------------------------------------------------------
    SELECT
        ExportLicence.Id AS LicenceId,
        ExportLicence.LicenceDate,
        ExportLicence.IssuedDate
    INTO #L
    FROM ExportLicence WITH (INDEX(IX_ExportLicence_Report_NewDetail_Page))
    INNER JOIN PaThaKa ON PaThaKa.Id = ExportLicence.PaThaKaId
    INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
    INNER JOIN ExportImportSection section ON section.Id = ExportLicence.ExportImportSectionId
    INNER JOIN Countries buyerCountry ON buyerCountry.Id = ExportLicence.BuyerCountryId
    INNER JOIN ExportImportMethod method ON method.Id = ExportLicence.ExportImportMethodId
    INNER JOIN Countries consignedCountry ON consignedCountry.Id = ExportLicence.ConsignedCountryId
    INNER JOIN Countries countryofOrigin ON countryofOrigin.Id = ExportLicence.CountryofOriginId
    INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id = ExportLicence.ExportImportIncotermId
    WHERE ApplyType='New'
    AND ExportLicence.Status='Approved'
    AND (ExportLicence.CreatedDate>=@FromDate AND ExportLicence.CreatedDate<=@ToDate)
    AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
    AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
    AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
    AND ExportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then ExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
    AND ExportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then ExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
    AND ExportLicence.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then ExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
    AND (
        @Auto = N''
        OR (@Auto = N'auto' AND ExportLicence.[auto] = N'auto')
        OR (@Auto = N'none-auto' AND (ExportLicence.[auto] IS NULL OR ExportLicence.[auto] <> N'auto'))
    )
    OPTION (RECOMPILE);

    -- ---------------------------------------------------------------------------------
    -- 2. Item keys: the legacy item-level INNER JOINs, so an item the old report drops
    --    (no Unit / Currency / HSCode row) is dropped here as well.
    -- ---------------------------------------------------------------------------------
    SELECT
        l.LicenceId,
        ExportLicenceItem.Id AS ItemId,
        ExportLicenceItem.UniqueId,
        l.LicenceDate,
        l.IssuedDate,
        ExportLicenceItem.ItemNo
    INTO #K
    FROM #L l
    INNER JOIN ExportLicenceItem ON l.LicenceId = ExportLicenceItem.ExportLicenceId
    INNER JOIN Unit unit ON ExportLicenceItem.UnitId = unit.Id
    INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
    INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
    OPTION (RECOMPILE);

    -- The count is a COUNT(*) over the key table that already exists, so it is always
    -- returned; @IncludeTotalCount is accepted for signature parity with the other
    -- pagination procedures.
    DECLARE @Total int = (SELECT COUNT(*) FROM #K);

    -- ---------------------------------------------------------------------------------
    -- 3. The page. Materialised on its own so the display join below is driven by at
    --    most one page of keys (see header).
    -- ---------------------------------------------------------------------------------
    SELECT
        LicenceId,
        ItemId,
        UniqueId,
        ROW_NUMBER() OVER (ORDER BY LicenceDate, IssuedDate, LicenceId, ItemNo, ItemId, UniqueId) AS PageOrder
    INTO #P
    FROM #K
    ORDER BY LicenceDate, IssuedDate, LicenceId, ItemNo, ItemId, UniqueId
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    -- ---------------------------------------------------------------------------------
    -- 4. The legacy select list, verbatim, for the page keys only. The three trailing
    --    Sakhan columns are NULL (oversea has none) and ApplicationNo / ApplicationDate /
    --    CommodityType feed the RDLC columns the old model never populated.
    -- ---------------------------------------------------------------------------------
    SELECT
        paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
        CAST(NULL AS int) AS SakhanId, CAST(NULL AS nvarchar(50)) AS SakhanCode, CAST(NULL AS nvarchar(200)) AS SakhanName,
        ExportLicence.ExportImportSectionId,ExportLicence.ExportImportMethodId,ExportLicence.ExportImportIncotermId,ExportLicence.BuyerCountryId,
        section.Code SectionCode,section.Name SectionName,ExportLicenceNo LicenceNo,ExportLicence.IssuedDate LicenceDate,
        PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,PaThaKa.Country,PaThaKa.PostalCode,
        ExportLicence.BuyerName,ExportLicence.BuyerAddress,buyerCountry.Name BuyerCountry,
        (
            SELECT ','+portofDischarge.Name
            FROM PortOfDischarge portofDischarge
            WHERE ','+ExportLicence.PortofExportId+',' LIKE '%,'+CAST(portofDischarge.Id as nvarchar(20)) +',%'
            for xml path(''), type
        ).value('substring(text()[1], 2)', 'varchar(max)') as PortofExport,ExportLicence.PortofDischarge,
        ExportLicence.LastDate,method.Name MethodName,
        (
            SELECT ','+countries.Name
            FROM Countries countries
            WHERE ','+ExportLicence.DestinationCountryId+',' LIKE '%,'+CAST(countries.Id as nvarchar(20)) +',%'
            for xml path(''), type
        ).value('substring(text()[1], 2)', 'varchar(max)') as DestinationCountry,
        consignedCountry.Name ConsignedCountry,countryofOrigin.Name CountryofOrigin,
        HSCode.Code HSCode,HSCode.Description+' '+ExportLicenceItem.Description HSDescription,
        unit.Code Unit,ExportLicenceItem.Price,ExportLicenceItem.Quantity,ExportLicenceItem.Amount,currency.Code Currency,
        ExportLicence.Remark Conditions,
        ExportLicence.ApplicationNo,ExportLicence.ApplicationDate,ExportLicence.CommodityType,
        ExportLicence.ApproveDate,
        @Total AS TotalCount
    FROM #P k
    INNER JOIN ExportLicence ON ExportLicence.Id = k.LicenceId
    INNER JOIN ExportLicenceItem ON ExportLicenceItem.Id = k.ItemId AND ExportLicenceItem.UniqueId = k.UniqueId
    INNER JOIN PaThaKa ON PaThaKa.Id = ExportLicence.PaThaKaId
    INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
    INNER JOIN Unit unit ON ExportLicenceItem.UnitId = unit.Id
    INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
    INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
    INNER JOIN ExportImportSection section ON section.Id = ExportLicence.ExportImportSectionId
    INNER JOIN Countries buyerCountry ON buyerCountry.Id = ExportLicence.BuyerCountryId
    INNER JOIN ExportImportMethod method ON method.Id = ExportLicence.ExportImportMethodId
    INNER JOIN Countries consignedCountry ON consignedCountry.Id = ExportLicence.ConsignedCountryId
    INNER JOIN Countries countryofOrigin ON countryofOrigin.Id = ExportLicence.CountryofOriginId
    INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id = ExportLicence.ExportImportIncotermId
    ORDER BY k.PageOrder
    OPTION (RECOMPILE);

    DROP TABLE #P;
    DROP TABLE #K;
    DROP TABLE #L;
END
GO

PRINT N'sp_ExportLicenceDetailReportV3_pagination applied. Now run VerifyDeployment.sql (section 3 is the sign-off).';
GO

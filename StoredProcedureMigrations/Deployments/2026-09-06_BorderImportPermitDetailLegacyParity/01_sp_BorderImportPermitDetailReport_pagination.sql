/* =====================================================================================
   sp_BorderImportPermitDetailReport_pagination

   The Border Import Permit Detail grid. This is the LEGACY Tradenet 2.0 procedure
   dbo.sp_ImportPermitDetailReport (its 'Border' branch) kept VERBATIM -- the same ten
   INNER JOINs, the same CASE WHEN @X = 0 filters, the same CreatedDate <= @ToDate window,
   the same select list including dbo.fn_GetNRCNo and the two FOR XML PATH CSV expanders --
   with item-grain key paging wrapped around it, so that the new report prints exactly the
   rows and exactly the cell values BorderImportPermitDetailReport.rdlc printed from that
   procedure (owner's instruction, 2026-09-06: byte-identical to the old report, even where
   the old code is wrong). Nothing in the legacy query is corrected here; do not "fix" it.

   Shape: #K (permit + item keys via the legacy FROM / JOIN / WHERE, unchanged) -> #P (one
   page of keys, ROW_NUMBER over a deterministic order) -> the legacy select list for those
   keys only. TotalCount is the item-grain COUNT(*) of #K on every row.

   Order: the legacy procedure has NO ORDER BY and the RDLC has no SortExpressions, so the
   old report's order was whatever the plan produced -- for this shape, the permits in the
   order they were created and their items in line order. That is the order used here
   (CreatedDate, permit Id, ItemNo, item UniqueId); it exists so paging is stable, and it is
   the closest deterministic reading of the old output. @PageSize <= 0 returns every row
   (the parity check against the legacy procedure and the Excel export use this).

   NEEDS SET QUOTED_IDENTIFIER ON at CREATE time: the CSV expanders call XML .value().
   ===================================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_BorderImportPermitDetailReport_pagination]
    @FromDate datetime,
    @ToDate datetime,
    @PaThaKaTypeId int = 0,
    @ExportImportSectionId int = 0,
    @SellerCountryId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @SakhanId int = 0,
    @PageIndex int = 0,
    @PageSize int = 10,
    @IncludeTotalCount bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

    IF @PageIndex IS NULL OR @PageIndex < 0 SET @PageIndex = 0;
    -- @PageSize <= 0 means "every row" (legacy parity check, Excel export).
    IF @PageSize IS NULL OR @PageSize <= 0 SET @PageSize = 2147483647;
    IF @CompanyRegistrationNo IS NULL SET @CompanyRegistrationNo = N'';
    IF @PaThaKaTypeId IS NULL SET @PaThaKaTypeId = 0;
    IF @ExportImportSectionId IS NULL SET @ExportImportSectionId = 0;
    IF @SellerCountryId IS NULL SET @SellerCountryId = 0;
    IF @SakhanId IS NULL SET @SakhanId = 0;

    DECLARE @Offset bigint = CAST(@PageIndex AS bigint) * @PageSize;
    IF @Offset > 2147483647 SET @Offset = 2147483647;

    -- ---------------------------------------------------------------------------------
    -- 1. Keys. FROM / JOIN / WHERE are the legacy 'Border' branch verbatim, so a row the
    --    old report drops (no Unit / Currency / HSCode / Sakhan row) is dropped here too.
    -- ---------------------------------------------------------------------------------
    SELECT
        BorderImportPermit.Id AS PermitId,
        BorderImportPermitItem.Id AS ItemId,
        BorderImportPermitItem.UniqueId AS ItemUniqueId,
        BorderImportPermit.CreatedDate AS PermitCreatedDate,
        BorderImportPermitItem.ItemNo
    INTO #K
  FROM BorderImportPermit
  INNER JOIN PaThaKa ON PaThaKa.Id = BorderImportPermit.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN BorderImportPermitItem ON BorderImportPermit.Id = BorderImportPermitItem.BorderImportPermitId
  INNER JOIN Unit unit ON BorderImportPermitItem.UnitId = unit.Id
  INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
  INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id
  INNER JOIN ExportImportSection section ON section.Id  = BorderImportPermit.ExportImportSectionId
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = BorderImportPermit.SellerCountryId
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderImportPermit.SakhanId
  WHERE ApplyType='New'
  AND BorderImportPermit.Status='Approved'
  AND (BorderImportPermit.CreatedDate>=@FromDate AND BorderImportPermit.CreatedDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND BorderImportPermit.SellerCountryId=(CASE WHEN @SellerCountryId=0 then BorderImportPermit.SellerCountryId ELSE @SellerCountryId END)
  AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
    OPTION (RECOMPILE);

    -- The count is a COUNT(*) over the key table that already exists, so it is always
    -- returned; @IncludeTotalCount is accepted for signature parity with the other
    -- pagination procedures.
    DECLARE @Total int = (SELECT COUNT(*) FROM #K);

    -- ---------------------------------------------------------------------------------
    -- 2. The page, materialised on its own so the display join below (with its two
    --    correlated FOR XML expanders and the scalar function) runs for one page of rows.
    -- ---------------------------------------------------------------------------------
    SELECT
        PermitId,
        ItemId,
        ItemUniqueId,
        ROW_NUMBER() OVER (ORDER BY PermitCreatedDate, PermitId, ItemNo, ItemUniqueId) AS PageOrder
    INTO #P
    FROM #K
    ORDER BY PermitCreatedDate, PermitId, ItemNo, ItemUniqueId
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    -- ---------------------------------------------------------------------------------
    -- 3. The legacy select list, verbatim, for the page keys only.
    -- ---------------------------------------------------------------------------------
  SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName,ExportImportSectionId,SellerCountryId,
  section.Code SectionCode,section.Name SectionName,ImportPermitNo LicenceNo,BorderImportPermit.IssuedDate LicenceDate,
  CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  AuthorisedAgentName,AuthorisedAgentAddress,sellerCountry.Name SellerCountry,
  (
   SELECT ','+portofShipment.Name
   FROM PortOfDischarge portofShipment
   WHERE ','+BorderImportPermit.PortofShipmentId+',' LIKE '%,'+CAST(portofShipment.Id as nvarchar(20)) +',%'
   for xml path(''), type
  ).value('substring(text()[1], 2)', 'varchar(max)') as PortofShipment,
  PortofDischarge,
  (
   SELECT ','+countries.Name
   FROM Countries countries
   WHERE ','+BorderImportPermit.CountryofOriginId+',' LIKE '%,'+CAST(countries.Id as nvarchar(20)) +',%'
   for xml path(''), type
  ).value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin,LastDate,
  HSCode.Code HSCode,BorderImportPermitItem.Description HSDescription,
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  dbo.fn_GetNRCNo(BorderImportPermit.NRCType,BorderImportPermit.NRCPrefixId,BorderImportPermit.NRCPrefixCodeId,BorderImportPermit.NRCNo) NRCNo,
  PermitType,BorderImportPermit.Remark Conditions,BorderImportPermit.ApproveDate,
        @Total AS TotalCount
    FROM #P k
    INNER JOIN BorderImportPermit ON BorderImportPermit.Id = k.PermitId
    INNER JOIN BorderImportPermitItem ON BorderImportPermitItem.Id = k.ItemId AND BorderImportPermitItem.UniqueId = k.ItemUniqueId
  INNER JOIN PaThaKa ON PaThaKa.Id = BorderImportPermit.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN Unit unit ON BorderImportPermitItem.UnitId = unit.Id
  INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
  INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id
  INNER JOIN ExportImportSection section ON section.Id  = BorderImportPermit.ExportImportSectionId
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = BorderImportPermit.SellerCountryId
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderImportPermit.SakhanId
    ORDER BY k.PageOrder
    OPTION (RECOMPILE);

    DROP TABLE #P;
    DROP TABLE #K;
END
GO

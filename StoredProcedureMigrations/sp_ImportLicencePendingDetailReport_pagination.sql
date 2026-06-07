-- The dynamic SELECT below uses XML data type methods (FOR XML PATH ... .value(...)), which
-- require QUOTED_IDENTIFIER ON. Like indexed views, this is captured at create time, so deploy
-- with this header to avoid a Msg 1934 "SET options ... 'QUOTED_IDENTIFIER' ... XML data type methods".
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_ImportLicencePendingDetailReport_pagination]
    @Type nvarchar(20) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @PaThaKaTypeId int = 0,
    @ExportImportSectionId int = 0,
    @ExportImportMethodId int = 0,
    @ExportImportIncotermId int = 0,
    @SellerCountryId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @SakhanId int = 0,
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL,
    @IncludeTotalCount bit = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ps bigint = CASE
        WHEN ISNULL(@PageSize,0) <= 0 THEN 9223372036854775807
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
        ELSE @PageSize END;
    DECLARE @off bigint = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 0 ELSE ISNULL(@PageIndex,0) * CAST(@PageSize AS bigint) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder,'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'PaThaKaTypeId', N'PaThaKaTypeCode', N'PaThaKaTypeName', N'ExportImportSectionId', N'ExportImportMethodId', N'ExportImportIncotermId', N'SellerCountryId', N'SectionCode', N'SectionName', N'LicenceNo', N'LicenceDate', N'CompanyRegistrationNo', N'CompanyName', N'UnitLevel', N'StreetNumberStreetName', N'QuarterCityTownship', N'State', N'Country', N'PostalCode', N'SellerName', N'SellerAddress', N'SellerCountry', N'PortofDischarge', N'LastDate', N'MethodName', N'HSCode', N'HSDescription', N'Unit', N'Price', N'Quantity', N'Amount', N'Currency', N'Conditions', N'ApplicationNo', N'ApplicationDate', N'FESCNo', N'CommodityType')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir + N', [LicenceDate] ASC, [LicenceNo] ASC';
    ELSE
        SET @ob = N'[LicenceDate] ASC, [LicenceNo] ASC';

    -- TotalCount only when requested, computed over the UN-paged base (no subqueries) as a separate scalar.
    -- OPTION (RECOMPILE) on the COUNT is REQUIRED: the optional-filter predicates below are non-sargable
    -- (col = CASE WHEN @p=0 THEN col ELSE @p END), so without a per-execution recompile this statement
    -- caches one plan keyed only on @IncludeTotalCount and reuses it for every filter/date combination.
    -- A single bad parameter sniff (e.g. an unfiltered or wide-range call) bakes in a scan over the
    -- 6.4M-row ImportLicenceItem join, after which even a one-day filtered query times out. RECOMPILE lets
    -- the optimizer fold the actual parameter values in and seek, matching the main SELECT below.
    DECLARE @cntpart nvarchar(max) = CASE WHEN @IncludeTotalCount = 1
        THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ImportLicence
  INNER JOIN PaThaKa ON PaThaKa.Id = ImportLicence.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN ImportLicenceItem ON ImportLicence.Id = ImportLicenceItem.ImportLicenceId  
  INNER JOIN Unit unit ON ImportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = ImportLicence.ExportImportSectionId  
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = ImportLicence.SellerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = ImportLicence.ExportImportMethodId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = ImportLicence.ExportImportIncotermId  
  WHERE ApplyType=''New'' 
  AND ImportLicence.Status=''Pending''   
  AND (ImportLicence.ApplicationDate>=@FromDate AND ImportLicence.ApplicationDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND ImportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then ImportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND ImportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then ImportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND ImportLicence.SellerCountryId=(CASE WHEN @SellerCountryId=0 then ImportLicence.SellerCountryId ELSE @SellerCountryId END)
  OPTION (RECOMPILE, MAX_GRANT_PERCENT = 20); '
        ELSE N'DECLARE @__total int = NULL; ' END;

    DECLARE @sql nvarchar(max) = @cntpart + N'SELECT pg.*,(  
   SELECT '',''+consignedCountry.Name  
   FROM Countries consignedCountry  
   WHERE '',''+pg.__k_ConsignedCountryId+'','' LIKE ''%,''+CAST(consignedCountry.Id as nvarchar(20)) +'',%''  
   for xml path(''''), type  
  ).value(''substring(text()[1], 2)'', ''varchar(max)'') as ConsignedCountry,
        (  
   SELECT '',''+countryofOrigin.Name  
   FROM Countries countryofOrigin  
   WHERE '',''+pg.__k_CountryofOriginId+'','' LIKE ''%,''+CAST(countryofOrigin.Id as nvarchar(20)) +'',%''  
   for xml path(''''), type  
  ).value(''substring(text()[1], 2)'', ''varchar(max)'') as CountryofOrigin, @__total AS TotalCount
    FROM (
        SELECT paThaKaType.Id PaThaKaTypeId,
paThaKaType.Code PaThaKaTypeCode,
paThaKaType.Description PaThaKaTypeName,
ExportImportSectionId,
ExportImportMethodId,
ExportImportIncotermId,
SellerCountryId,
section.Code SectionCode,
section.Name SectionName,
ImportLicenceNo LicenceNo,
ImportLicence.IssuedDate LicenceDate,
CompanyRegistrationNo,
CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
SellerName,
SellerAddress,
sellerCountry.Name SellerCountry,
PortofDischarge,
LastDate,
method.Name MethodName,
HSCode.Code HSCode,
ImportLicenceItem.Description HSDescription,
unit.Code Unit,
Price,
Quantity,
Amount,
currency.Code Currency,
ImportLicence.Remark Conditions,
ImportLicence.ApplicationNo,
ImportLicence.ApplicationDate,
ImportLicence.FESCNo,
ImportLicence.CommodityType,
ImportLicence.ConsignedCountryId AS __k_ConsignedCountryId, ImportLicence.CountryofOriginId AS __k_CountryofOriginId
        FROM ImportLicence  
  INNER JOIN PaThaKa ON PaThaKa.Id = ImportLicence.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN ImportLicenceItem ON ImportLicence.Id = ImportLicenceItem.ImportLicenceId  
  INNER JOIN Unit unit ON ImportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = ImportLicence.ExportImportSectionId  
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = ImportLicence.SellerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = ImportLicence.ExportImportMethodId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = ImportLicence.ExportImportIncotermId  
  WHERE ApplyType=''New'' 
  AND ImportLicence.Status=''Pending''   
  AND (ImportLicence.ApplicationDate>=@FromDate AND ImportLicence.ApplicationDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND ImportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then ImportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND ImportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then ImportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND ImportLicence.SellerCountryId=(CASE WHEN @SellerCountryId=0 then ImportLicence.SellerCountryId ELSE @SellerCountryId END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    -- MAX_GRANT_PERCENT caps the memory grant so this query can no longer hit Msg 8645
    -- ("timeout waiting for memory resources"): the optimizer over-estimates the grant for the
    -- 6.4M-row ImportLicenceItem join + IssuedDate sort, but the real (filtered) input is tiny.
    OPTION (RECOMPILE, MAX_GRANT_PERCENT = 20);';

    EXEC sp_executesql @sql, N'@Type nvarchar(20), @FromDate datetime, @ToDate datetime, @PaThaKaTypeId int, @ExportImportSectionId int, @ExportImportMethodId int, @ExportImportIncotermId int, @SellerCountryId int, @CompanyRegistrationNo nvarchar(50), @SakhanId int, @off bigint, @ps bigint', @Type=@Type, @FromDate=@FromDate, @ToDate=@ToDate, @PaThaKaTypeId=@PaThaKaTypeId, @ExportImportSectionId=@ExportImportSectionId, @ExportImportMethodId=@ExportImportMethodId, @ExportImportIncotermId=@ExportImportIncotermId, @SellerCountryId=@SellerCountryId, @CompanyRegistrationNo=@CompanyRegistrationNo, @SakhanId=@SakhanId, @off=@off, @ps=@ps;
END

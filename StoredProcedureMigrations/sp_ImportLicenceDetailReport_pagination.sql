CREATE OR ALTER PROCEDURE [dbo].[sp_ImportLicenceDetailReport_pagination]
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
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'PaThaKaTypeId', N'PaThaKaTypeCode', N'PaThaKaTypeName', N'ExportImportSectionId', N'ExportImportMethodId', N'ExportImportIncotermId', N'SellerCountryId', N'SectionCode', N'SectionName', N'LicenceNo', N'LicenceDate', N'CompanyRegistrationNo', N'CompanyName', N'UnitLevel', N'StreetNumberStreetName', N'QuarterCityTownship', N'State', N'Country', N'PostalCode', N'SellerName', N'SellerAddress', N'SellerCountry', N'PortofDischarge', N'LastDate', N'MethodName', N'HSCode', N'HSDescription', N'Unit', N'Price', N'Quantity', N'Amount', N'Currency', N'Conditions', N'ApplicationNo', N'ApplicationDate', N'FESCNo', N'CommodityType', N'ApproveDate')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir
            + CASE WHEN @SortColumn = N'LicenceDate' THEN N'' ELSE N', [LicenceDate] ASC' END
            + CASE WHEN @SortColumn = N'LicenceNo' THEN N'' ELSE N', [LicenceNo] ASC' END;
    ELSE
        SET @ob = N'[LicenceDate] ASC, [LicenceNo] ASC';

    -- TotalCount only when requested. The count grain is ImportLicenceItem, so it only needs the
    -- tables that change cardinality (ImportLicenceItem) or back a filter (PaThaKa). The lookup joins
    -- (PaThaKaType/Unit/Currency/HSCode/Section/Countries/Method/Incoterm) are FK=PK on NOT NULL
    -- columns, so they are 1:1 and do not affect the count -- dropping them avoids materializing the
    -- full join just to count rows. PaThaKaTypeId is filtered directly on PaThaKa.
    DECLARE @cntpart nvarchar(max) = CASE WHEN @IncludeTotalCount = 1
        THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ImportLicence
  INNER JOIN PaThaKa ON PaThaKa.Id = ImportLicence.PaThaKaId
  INNER JOIN ImportLicenceItem ON ImportLicence.Id = ImportLicenceItem.ImportLicenceId
  WHERE ImportLicence.ApplyType=''New''
  AND ImportLicence.Status=''Approved'' AND ImportLicence.ImportLicenceNo <> ''''
  AND ((@FromDate IS NULL) OR ImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ImportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
  AND (@CompanyRegistrationNo='''' OR PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo)
  AND (@PaThaKaTypeId=0 OR PaThaKa.PaThaKaTypeId=@PaThaKaTypeId)
  AND (@ExportImportSectionId=0 OR ImportLicence.ExportImportSectionId=@ExportImportSectionId)
  AND (@ExportImportMethodId=0 OR ImportLicence.ExportImportMethodId=@ExportImportMethodId)
  AND (@ExportImportIncotermId=0 OR ImportLicence.ExportImportIncotermId=@ExportImportIncotermId)
  AND (@SellerCountryId=0 OR ImportLicence.SellerCountryId=@SellerCountryId) OPTION (RECOMPILE); '
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
ImportLicence.ApproveDate,
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
  AND ImportLicence.Status=''Approved'' AND ImportLicence.ImportLicenceNo <> ''''  
  AND ((@FromDate IS NULL) OR ImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ImportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND ImportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then ImportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND ImportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then ImportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND ImportLicence.SellerCountryId=(CASE WHEN @SellerCountryId=0 then ImportLicence.SellerCountryId ELSE @SellerCountryId END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';

    EXEC sp_executesql @sql, N'@Type nvarchar(20), @FromDate datetime, @ToDate datetime, @PaThaKaTypeId int, @ExportImportSectionId int, @ExportImportMethodId int, @ExportImportIncotermId int, @SellerCountryId int, @CompanyRegistrationNo nvarchar(50), @SakhanId int, @off bigint, @ps bigint', @Type=@Type, @FromDate=@FromDate, @ToDate=@ToDate, @PaThaKaTypeId=@PaThaKaTypeId, @ExportImportSectionId=@ExportImportSectionId, @ExportImportMethodId=@ExportImportMethodId, @ExportImportIncotermId=@ExportImportIncotermId, @SellerCountryId=@SellerCountryId, @CompanyRegistrationNo=@CompanyRegistrationNo, @SakhanId=@SakhanId, @off=@off, @ps=@ps;
END

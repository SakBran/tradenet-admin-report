CREATE OR ALTER PROCEDURE [dbo].[sp_ImportPermitDetailReport_Fast_pagination]
    @Type nvarchar(20) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @PaThaKaTypeId int = 0,
    @ImportExportSectionId int = 0,
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

    IF @SortColumn IS NOT NULL AND @SortColumn IN (
        N'PaThaKaTypeId', N'PaThaKaTypeCode', N'PaThaKaTypeName', N'SakhanId', N'SakhanCode', N'SakhanName', N'ImportExportSectionId', N'SellerCountryId',
        N'SectionCode', N'SectionName', N'LicenceNo', N'LicenceDate', N'CompanyRegistrationNo', N'CompanyName', N'UnitLevel', N'StreetNumberStreetName',
        N'QuarterCityTownship', N'State', N'Country', N'PostalCode', N'ConsigneeName', N'ConsigneeAddress', N'SellerCountry', N'PortofShipment',
        N'PortofDischarge', N'CountryofOrigin', N'HSCode', N'HSDescription', N'Unit', N'Price', N'Quantity', N'Amount', N'Currency', N'NRCNo',
        N'PermitType', N'Conditions', N'ApproveDate')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir + N', [LicenceDate] ASC, [LicenceNo] ASC';
    ELSE
        SET @ob = N'[LicenceDate] ASC, [LicenceNo] ASC';

    DECLARE @sql nvarchar(max);

    IF @Type = 'Oversea'
    BEGIN
        SET @sql = N'DECLARE @__total int = NULL; '
            + CASE WHEN @IncludeTotalCount = 1 THEN N'SELECT @__total = COUNT(*) FROM ImportPermit
  INNER JOIN PaThaKa ON PaThaKa.Id = ImportPermit.PaThaKaId
  INNER JOIN ImportPermitItem ON ImportPermit.Id = ImportPermitItem.ImportPermitId
  WHERE ApplyType=''New''
  AND ImportPermit.Status=''Approved''
  AND ((@FromDate IS NULL) OR ImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ImportPermit.CreatedDate < DATEADD(day, 1, @ToDate))
  AND (@CompanyRegistrationNo='''' OR PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo)
  AND (@PaThaKaTypeId=0 OR PaThaKa.PaThaKaTypeId=@PaThaKaTypeId)
  AND (@ImportExportSectionId=0 OR ImportPermit.ImportExportSectionId=@ImportExportSectionId)
  AND (@SellerCountryId=0 OR ImportPermit.SellerCountryId=@SellerCountryId); '
            ELSE N'' END
            + N'SELECT pg.*, @__total AS TotalCount
    FROM (
        SELECT paThaKaType.Id PaThaKaTypeId,
paThaKaType.Code PaThaKaTypeCode,
paThaKaType.Description PaThaKaTypeName,
ImportExportSectionId,SellerCountryId,
section.Code SectionCode,section.Name SectionName,ImportPermitNo LicenceNo,ImportPermit.IssuedDate LicenceDate,
CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
ConsigneeName,ConsigneeAddress,sellerCountry.Name SellerCountry,
(
   SELECT '',''+portofShipment.Name
   FROM PortOfShipment portofShipment
   WHERE '',''+ImportPermit.PortofShipmentId+'','' LIKE ''%,''+CAST(portofShipment.Id as nvarchar(20)) +'',%''
   for xml path(''''), type
  ).value(''substring(text()[1], 2)'', ''varchar(max)'') as PortofShipment,PortofDischarge,
(
   SELECT '',''+countries.Name
   FROM Countries countries
   WHERE '',''+ImportPermit.CountryofOriginId+'','' LIKE ''%,''+CAST(countries.Id as nvarchar(20)) +'',%''
   for xml path(''''), type
  ).value(''substring(text()[1], 2)'', ''varchar(max)'') as CountryofOrigin,
HSCode.Code HSCode,HSCode.Description+'' ''+ImportPermitItem.Description HSDescription,
unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
dbo.fn_GetNRCNo(ImportPermit.NRCType,ImportPermit.NRCPrefixId,ImportPermit.NRCPrefixCodeId,ImportPermit.NRCNo) NRCNo,
PermitType,ImportPermit.Remark Conditions,ImportPermit.ApproveDate
        FROM ImportPermit
  INNER JOIN PaThaKa ON PaThaKa.Id = ImportPermit.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN ImportPermitItem ON ImportPermit.Id = ImportPermitItem.ImportPermitId
  INNER JOIN Unit unit ON ImportPermitItem.UnitId = unit.Id
  INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
  INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id
  INNER JOIN ImportExportSection section ON section.Id  = ImportPermit.ImportExportSectionId
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = ImportPermit.SellerCountryId
        WHERE ApplyType=''New''
  AND ImportPermit.Status=''Approved''
  AND ((@FromDate IS NULL) OR ImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ImportPermit.CreatedDate < DATEADD(day, 1, @ToDate))
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND ImportPermit.ImportExportSectionId=(CASE WHEN @ImportExportSectionId=0 then ImportPermit.ImportExportSectionId ELSE @ImportExportSectionId END)
  AND ImportPermit.SellerCountryId=(CASE WHEN @SellerCountryId=0 then ImportPermit.SellerCountryId ELSE @SellerCountryId END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @Type = 'Border'
    BEGIN
        SET @sql = N'DECLARE @__total int = NULL; '
            + CASE WHEN @IncludeTotalCount = 1 THEN N'SELECT @__total = COUNT(*) FROM BorderImportPermit
  INNER JOIN PaThaKa ON PaThaKa.Id = BorderImportPermit.PaThaKaId
  INNER JOIN BorderImportPermitItem ON BorderImportPermit.Id = BorderImportPermitItem.BorderImportPermitId
  WHERE ApplyType=''New''
  AND BorderImportPermit.Status=''Approved''
  AND ((@FromDate IS NULL) OR BorderImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportPermit.CreatedDate < DATEADD(day, 1, @ToDate))
  AND (@CompanyRegistrationNo='''' OR PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo)
  AND (@PaThaKaTypeId=0 OR PaThaKa.PaThaKaTypeId=@PaThaKaTypeId)
  AND (@ImportExportSectionId=0 OR BorderImportPermit.ImportExportSectionId=@ImportExportSectionId)
  AND (@SellerCountryId=0 OR BorderImportPermit.SellerCountryId=@SellerCountryId)
  AND (@SakhanId=0 OR BorderImportPermit.SakhanId=@SakhanId); '
            ELSE N'' END
            + N'SELECT pg.*, @__total AS TotalCount
    FROM (
        SELECT paThaKaType.Id PaThaKaTypeId,
paThaKaType.Code PaThaKaTypeCode,
paThaKaType.Description PaThaKaTypeName,
sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName,ImportExportSectionId,SellerCountryId,
section.Code SectionCode,section.Name SectionName,ImportPermitNo LicenceNo,BorderImportPermit.IssuedDate LicenceDate,
CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
ConsigneeName,ConsigneeAddress,sellerCountry.Name SellerCountry,
(
   SELECT '',''+portofShipment.Name
   FROM PortOfShipment portofShipment
   WHERE '',''+BorderImportPermit.PortofShipmentId+'','' LIKE ''%,''+CAST(portofShipment.Id as nvarchar(20)) +'',%''
   for xml path(''''), type
  ).value(''substring(text()[1], 2)'', ''varchar(max)'') as PortofShipment,PortofDischarge,
(
   SELECT '',''+countries.Name
   FROM Countries countries
   WHERE '',''+BorderImportPermit.CountryofOriginId+'','' LIKE ''%,''+CAST(countries.Id as nvarchar(20)) +'',%''
   for xml path(''''), type
  ).value(''substring(text()[1], 2)'', ''varchar(max)'') as CountryofOrigin,
HSCode.Code HSCode,HSCode.Description+'' ''+BorderImportPermitItem.Description HSDescription,
unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
dbo.fn_GetNRCNo(BorderImportPermit.NRCType,BorderImportPermit.NRCPrefixId,BorderImportPermit.NRCPrefixCodeId,BorderImportPermit.NRCNo) NRCNo,
PermitType,BorderImportPermit.Remark Conditions,BorderImportPermit.ApproveDate
        FROM BorderImportPermit
  INNER JOIN PaThaKa ON PaThaKa.Id = BorderImportPermit.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN BorderImportPermitItem ON BorderImportPermit.Id = BorderImportPermitItem.BorderImportPermitId
  INNER JOIN Unit unit ON BorderImportPermitItem.UnitId = unit.Id
  INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
  INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id
  INNER JOIN ImportExportSection section ON section.Id  = BorderImportPermit.ImportExportSectionId
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = BorderImportPermit.SellerCountryId
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderImportPermit.SakhanId
        WHERE ApplyType=''New''
  AND BorderImportPermit.Status=''Approved''
  AND ((@FromDate IS NULL) OR BorderImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportPermit.CreatedDate < DATEADD(day, 1, @ToDate))
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND BorderImportPermit.ImportExportSectionId=(CASE WHEN @ImportExportSectionId=0 then BorderImportPermit.ImportExportSectionId ELSE @ImportExportSectionId END)
  AND BorderImportPermit.SellerCountryId=(CASE WHEN @SellerCountryId=0 then BorderImportPermit.SellerCountryId ELSE @SellerCountryId END)
  AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE
    BEGIN
        SET @sql = N'SELECT TOP 0
            NULL AS PaThaKaTypeId,
            NULL AS PaThaKaTypeCode,
            NULL AS PaThaKaTypeName,
            NULL AS SakhanId,
            NULL AS SakhanCode,
            NULL AS SakhanName,
            NULL AS ImportExportSectionId,
            NULL AS SellerCountryId,
            NULL AS SectionCode,
            NULL AS SectionName,
            NULL AS LicenceNo,
            NULL AS LicenceDate,
            NULL AS CompanyRegistrationNo,
            NULL AS CompanyName,
            NULL AS UnitLevel,
            NULL AS StreetNumberStreetName,
            NULL AS QuarterCityTownship,
            NULL AS State,
            NULL AS Country,
            NULL AS PostalCode,
            NULL AS ConsigneeName,
            NULL AS ConsigneeAddress,
            NULL AS SellerCountry,
            NULL AS PortofShipment,
            NULL AS PortofDischarge,
            NULL AS CountryofOrigin,
            NULL AS HSCode,
            NULL AS HSDescription,
            NULL AS Unit,
            NULL AS Price,
            NULL AS Quantity,
            NULL AS Amount,
            NULL AS Currency,
            NULL AS NRCNo,
            NULL AS PermitType,
            NULL AS Conditions,
            NULL AS ApproveDate,
            0 AS TotalCount
        WHERE 1 = 0;';
    END

    EXEC sp_executesql @sql,
        N'@FromDate datetime, @ToDate datetime, @PaThaKaTypeId int, @ImportExportSectionId int, @SellerCountryId int, @CompanyRegistrationNo nvarchar(50), @SakhanId int, @off bigint, @ps bigint',
        @FromDate=@FromDate,
        @ToDate=@ToDate,
        @PaThaKaTypeId=@PaThaKaTypeId,
        @ImportExportSectionId=@ImportExportSectionId,
        @SellerCountryId=@SellerCountryId,
        @CompanyRegistrationNo=@CompanyRegistrationNo,
        @SakhanId=@SakhanId,
        @off=@off,
        @ps=@ps;
END

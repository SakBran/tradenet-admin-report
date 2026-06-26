SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_ExportPermitDetailReport_Fast_pagination]
    @Type nvarchar(20) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @PaThaKaTypeId int = 0,
    @ExportImportSectionId int = 0,
    @BuyerCountryId int = 0,
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
        N'PaThaKaTypeId', N'PaThaKaTypeCode', N'PaThaKaTypeName', N'SakhanId', N'SakhanCode', N'SakhanName', N'ExportImportSectionId', N'BuyerCountryId',
        N'SectionCode', N'SectionName', N'LicenceNo', N'LicenceDate', N'CompanyRegistrationNo', N'CompanyName', N'UnitLevel', N'StreetNumberStreetName',
        N'QuarterCityTownship', N'State', N'Country', N'PostalCode', N'ConsigneeName', N'ConsigneeAddress', N'BuyerCountry', N'PortofExport', N'PortofDischarge',
        N'DestinationCountry', N'LastDate', N'ConsignedCountry', N'CountryofOrigin', N'HSCode', N'HSDescription', N'Unit', N'Price', N'Quantity', N'Amount',
        N'Currency', N'NRCNo', N'PermitType', N'Conditions', N'ApproveDate')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir
            + CASE WHEN @SortColumn = N'LicenceDate' THEN N'' ELSE N', [LicenceDate] ASC' END
            + CASE WHEN @SortColumn = N'LicenceNo' THEN N'' ELSE N', [LicenceNo] ASC' END;
    ELSE
        SET @ob = N'[LicenceDate] ASC, [LicenceNo] ASC';

    DECLARE @sql nvarchar(max);

    IF @Type = 'Oversea'
    BEGIN
        SET @sql = CAST(N'DECLARE @__total int = 0; ' AS nvarchar(max))
            + CASE WHEN @IncludeTotalCount = 1 THEN N'SELECT @__total = COUNT(*) FROM ExportPermit
  INNER JOIN PaThaKa ON PaThaKa.Id = ExportPermit.PaThaKaId
  INNER JOIN ExportPermitItem ON ExportPermit.Id = ExportPermitItem.ExportPermitId
  WHERE ApplyType=''New''
  AND ExportPermit.Status=''Approved''
  AND ((@FromDate IS NULL) OR ExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ExportPermit.CreatedDate < DATEADD(day, 1, @ToDate))
  AND (@CompanyRegistrationNo='''' OR PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo)
  AND (@PaThaKaTypeId=0 OR PaThaKa.PaThaKaTypeId=@PaThaKaTypeId)
  AND (@ExportImportSectionId=0 OR ExportPermit.ExportImportSectionId=@ExportImportSectionId)
  AND (@BuyerCountryId=0 OR ExportPermit.BuyerCountryId=@BuyerCountryId); '
            ELSE N'' END
            + N'SELECT pg.*, @__total AS TotalCount
    FROM (
        SELECT paThaKaType.Id PaThaKaTypeId,
paThaKaType.Code PaThaKaTypeCode,
paThaKaType.Description PaThaKaTypeName,
ExportImportSectionId,BuyerCountryId,
section.Code SectionCode,section.Name SectionName,ExportPermitNo LicenceNo,ExportPermit.IssuedDate LicenceDate,
CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
ConsigneeName,ConsigneeAddress,buyerCountry.Name BuyerCountry,
(
   SELECT '',''+portofDischarge.Name
   FROM PortOfDischarge portofDischarge
   WHERE '',''+ExportPermit.PortofExportId+'','' LIKE ''%,''+CAST(portofDischarge.Id as nvarchar(20)) +'',%''
   for xml path(''''), type
  ).value(''substring(text()[1], 2)'', ''varchar(max)'') as PortofExport,PortofDischarge,
(
   SELECT '',''+countries.Name
   FROM Countries countries
   WHERE '',''+ExportPermit.DestinationCountryId+'','' LIKE ''%,''+CAST(countries.Id as nvarchar(20)) +'',%''
   for xml path(''''), type
  ).value(''substring(text()[1], 2)'', ''varchar(max)'') as DestinationCountry,
LastDate,consignedCountry.Name ConsignedCountry,
(
   SELECT '',''+countries.Name
   FROM Countries countries
   WHERE '',''+ExportPermit.CountryofOriginId+'','' LIKE ''%,''+CAST(countries.Id as nvarchar(20)) +'',%''
   for xml path(''''), type
  ).value(''substring(text()[1], 2)'', ''varchar(max)'') as CountryofOrigin,
HSCode.Code HSCode,HSCode.Description+'' ''+ExportPermitItem.Description HSDescription,
unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
dbo.fn_GetNRCNo(ExportPermit.NRCType,ExportPermit.NRCPrefixId,ExportPermit.NRCPrefixCodeId,ExportPermit.NRCNo) NRCNo,
PermitType,ExportPermit.Remark Conditions,ExportPermit.ApproveDate
        FROM ExportPermit
  INNER JOIN PaThaKa ON PaThaKa.Id = ExportPermit.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN ExportPermitItem ON ExportPermit.Id = ExportPermitItem.ExportPermitId
  INNER JOIN Unit unit ON ExportPermitItem.UnitId = unit.Id
  INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
  INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id
  INNER JOIN ExportImportSection section ON section.Id  = ExportPermit.ExportImportSectionId
  INNER JOIN Countries buyerCountry ON buyerCountry.Id  = ExportPermit.BuyerCountryId
  INNER JOIN Countries consignedCountry ON consignedCountry.Id  = ExportPermit.ConsignedCountryId
        WHERE ApplyType=''New''
  AND ExportPermit.Status=''Approved''
  AND ((@FromDate IS NULL) OR ExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ExportPermit.CreatedDate < DATEADD(day, 1, @ToDate))
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND ExportPermit.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then ExportPermit.BuyerCountryId ELSE @BuyerCountryId END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @Type = 'Border'
    BEGIN
        SET @sql = CAST(N'DECLARE @__total int = 0; ' AS nvarchar(max))
            + CASE WHEN @IncludeTotalCount = 1 THEN N'SELECT @__total = COUNT(*) FROM BorderExportPermit
  INNER JOIN PaThaKa ON PaThaKa.Id = BorderExportPermit.PaThaKaId
  INNER JOIN BorderExportPermitItem ON BorderExportPermit.Id = BorderExportPermitItem.BorderExportPermitId
  WHERE ApplyType=''New''
  AND BorderExportPermit.Status=''Approved''
  AND ((@FromDate IS NULL) OR BorderExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportPermit.CreatedDate < DATEADD(day, 1, @ToDate))
  AND (@CompanyRegistrationNo='''' OR PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo)
  AND (@PaThaKaTypeId=0 OR PaThaKa.PaThaKaTypeId=@PaThaKaTypeId)
  AND (@ExportImportSectionId=0 OR BorderExportPermit.ExportImportSectionId=@ExportImportSectionId)
  AND (@BuyerCountryId=0 OR BorderExportPermit.BuyerCountryId=@BuyerCountryId)
  AND (@SakhanId=0 OR BorderExportPermit.SakhanId=@SakhanId); '
            ELSE N'' END
            + N'SELECT pg.*, @__total AS TotalCount
    FROM (
        SELECT paThaKaType.Id PaThaKaTypeId,
paThaKaType.Code PaThaKaTypeCode,
paThaKaType.Description PaThaKaTypeName,
sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName,ExportImportSectionId,BuyerCountryId,
section.Code SectionCode,section.Name SectionName,ExportPermitNo LicenceNo,BorderExportPermit.IssuedDate LicenceDate,
CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
ConsigneeName,ConsigneeAddress,buyerCountry.Name BuyerCountry,
(
   SELECT '',''+portofDischarge.Name
   FROM PortOfDischarge portofDischarge
   WHERE '',''+BorderExportPermit.PortofExportId+'','' LIKE ''%,''+CAST(portofDischarge.Id as nvarchar(20)) +'',%''
   for xml path(''''), type
  ).value(''substring(text()[1], 2)'', ''varchar(max)'') as PortofExport,PortofDischarge,
(
   SELECT '',''+countries.Name
   FROM Countries countries
   WHERE '',''+BorderExportPermit.DestinationCountryId+'','' LIKE ''%,''+CAST(countries.Id as nvarchar(20)) +'',%''
   for xml path(''''), type
  ).value(''substring(text()[1], 2)'', ''varchar(max)'') as DestinationCountry,
LastDate,consignedCountry.Name ConsignedCountry,
(
   SELECT '',''+countries.Name
   FROM Countries countries
   WHERE '',''+BorderExportPermit.CountryofOriginId+'','' LIKE ''%,''+CAST(countries.Id as nvarchar(20)) +'',%''
   for xml path(''''), type
  ).value(''substring(text()[1], 2)'', ''varchar(max)'') as CountryofOrigin,
HSCode.Code HSCode,HSCode.Description+'' ''+BorderExportPermitItem.Description HSDescription,
unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
dbo.fn_GetNRCNo(BorderExportPermit.NRCType,BorderExportPermit.NRCPrefixId,BorderExportPermit.NRCPrefixCodeId,BorderExportPermit.NRCNo) NRCNo,
PermitType,BorderExportPermit.Remark Conditions,BorderExportPermit.ApproveDate
        FROM BorderExportPermit
  INNER JOIN PaThaKa ON PaThaKa.Id = BorderExportPermit.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN BorderExportPermitItem ON BorderExportPermit.Id = BorderExportPermitItem.BorderExportPermitId
  INNER JOIN Unit unit ON BorderExportPermitItem.UnitId = unit.Id
  INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
  INNER JOIN HSCode ON BorderExportPermitItem.HSCodeId = HSCode.Id
  INNER JOIN ExportImportSection section ON section.Id  = BorderExportPermit.ExportImportSectionId
  INNER JOIN Countries buyerCountry ON buyerCountry.Id  = BorderExportPermit.BuyerCountryId
  INNER JOIN Countries consignedCountry ON consignedCountry.Id  = BorderExportPermit.ConsignedCountryId
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderExportPermit.SakhanId
        WHERE ApplyType=''New''
  AND BorderExportPermit.Status=''Approved''
  AND ((@FromDate IS NULL) OR BorderExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportPermit.CreatedDate < DATEADD(day, 1, @ToDate))
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND BorderExportPermit.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then BorderExportPermit.BuyerCountryId ELSE @BuyerCountryId END)
  AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END)
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
            NULL AS ExportImportSectionId,
            NULL AS BuyerCountryId,
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
            NULL AS BuyerCountry,
            NULL AS PortofExport,
            NULL AS PortofDischarge,
            NULL AS DestinationCountry,
            NULL AS LastDate,
            NULL AS ConsignedCountry,
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
        N'@FromDate datetime, @ToDate datetime, @PaThaKaTypeId int, @ExportImportSectionId int, @BuyerCountryId int, @CompanyRegistrationNo nvarchar(50), @SakhanId int, @off bigint, @ps bigint',
        @FromDate=@FromDate,
        @ToDate=@ToDate,
        @PaThaKaTypeId=@PaThaKaTypeId,
        @ExportImportSectionId=@ExportImportSectionId,
        @BuyerCountryId=@BuyerCountryId,
        @CompanyRegistrationNo=@CompanyRegistrationNo,
        @SakhanId=@SakhanId,
        @off=@off,
        @ps=@ps;
END

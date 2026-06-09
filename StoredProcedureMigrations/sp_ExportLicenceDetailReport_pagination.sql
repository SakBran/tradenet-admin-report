SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_ExportLicenceDetailReport_Pagination]
    @Type                   nvarchar(20),       -- 'Oversea', 'Border'
    @FromDate               datetime,
    @ToDate                 datetime,
    @PaThaKaTypeId          int,
    @ExportImportSectionId  int,
    @ExportImportMethodId   int,
    @ExportImportIncotermId int,
    @BuyerCountryId         int,
    @CompanyRegistrationNo  nvarchar(50),
    @SakhanId               int,
    @SortColumn             nvarchar(128) = NULL,
    @SortOrder              nvarchar(4)   = NULL,
    @PageIndex              int           = NULL,
    @PageSize               int           = NULL,
    @IncludeTotalCount      bit           = 1
AS
BEGIN
    SET NOCOUNT ON;

    -- Page size: 0 or negative means no limit, else use the value.
    DECLARE @ps bigint = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 9223372036854775807
                              WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
                              ELSE @PageSize END;
    DECLARE @off bigint = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 0
                               ELSE ISNULL(@PageIndex,0) * CAST(@PageSize AS bigint) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder,'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    -- Allowed sort columns (match aliases in the SELECT list)
    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL AND @SortColumn IN (
        N'LicenceDate', N'LicenceNo', N'SectionCode', N'SectionName',
        N'CompanyName', N'HSCode', N'PaThaKaTypeCode', N'PaThaKaTypeName',
        N'BuyerCountry', N'MethodName', N'ConsignedCountry', N'CountryofOrigin',
        N'PortofDischarge', N'Currency', N'Price', N'Quantity', N'Amount'
    )
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir + N', [LicenceDate] ASC, [LicenceNo] ASC';
    ELSE
        SET @ob = N'[LicenceDate] ASC, [LicenceNo] ASC';

    DECLARE @cntpart nvarchar(max);
    DECLARE @sql nvarchar(max);

    --------------------------------------------------------------------------
    --  OVERSEA
    --------------------------------------------------------------------------
    IF @Type = N'Oversea'
    BEGIN
        -- Total count (same filters as main query)
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1 THEN
            N'DECLARE @__total int = (
                SELECT COUNT(*)
                FROM ExportLicence
                INNER JOIN PaThaKa ON PaThaKa.Id = ExportLicence.PaThaKaId
                INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
                INNER JOIN ExportLicenceItem ON ExportLicence.Id = ExportLicenceItem.ExportLicenceId
                INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
                INNER JOIN ExportImportSection section ON section.Id = ExportLicence.ExportImportSectionId
                INNER JOIN Countries buyerCountry ON buyerCountry.Id = ExportLicence.BuyerCountryId
                INNER JOIN ExportImportMethod method ON method.Id = ExportLicence.ExportImportMethodId
                INNER JOIN Countries consignedCountry ON consignedCountry.Id = ExportLicence.ConsignedCountryId
                INNER JOIN Countries countryofOrigin ON countryofOrigin.Id = ExportLicence.CountryofOriginId
                INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id = ExportLicence.ExportImportIncotermId
                WHERE ApplyType=''New'' AND ExportLicence.Status=''Approved''
                  AND ((@FromDate IS NULL) OR ExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ExportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
                  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
                  AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                  AND ExportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then ExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
                  AND ExportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then ExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
                  AND ExportLicence.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then ExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
            ); '
        ELSE N'DECLARE @__total int = NULL; ' END;

        -- Main query with OFFSET/FETCH
        SET @sql = @cntpart + N'
            SELECT *, @__total AS TotalCount
            FROM (
                SELECT
                    paThaKaType.Id PaThaKaTypeId, paThaKaType.Code PaThaKaTypeCode, paThaKaType.Description PaThaKaTypeName,
                    CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName,
                    ExportImportSectionId, ExportImportMethodId, ExportImportIncotermId, BuyerCountryId,
                    section.Code SectionCode, section.Name SectionName,
                    ExportLicenceNo LicenceNo, ExportLicence.IssuedDate LicenceDate,
                    CompanyRegistrationNo, CompanyName, UnitLevel, StreetNumberStreetName,
                    QuarterCityTownship, State, Country, PostalCode,
                    BuyerName, BuyerAddress, buyerCountry.Name BuyerCountry,
                    (SELECT '',''+portofDischarge.Name
                     FROM PortOfDischarge portofDischarge
                     WHERE '',''+ExportLicence.PortofExportId+'','' LIKE ''%,''+CAST(portofDischarge.Id as nvarchar(20))+'',%''
                     FOR XML PATH(''''), TYPE).value(''substring(text()[1], 2)'', ''varchar(max)'') as PortofExport,
                    PortofDischarge,
                    LastDate, method.Name MethodName,
                    (SELECT '',''+countries.Name
                     FROM Countries countries
                     WHERE '',''+ExportLicence.DestinationCountryId+'','' LIKE ''%,''+CAST(countries.Id as nvarchar(20))+'',%''
                     FOR XML PATH(''''), TYPE).value(''substring(text()[1], 2)'', ''varchar(max)'') as DestinationCountry,
                    consignedCountry.Name ConsignedCountry, countryofOrigin.Name CountryofOrigin,
                    HSCode.Code HSCode, HSCode.Description+'' ''+ExportLicenceItem.Description HSDescription,
                    unit.Code Unit, Price, Quantity, Amount, currency.Code Currency,
                    ExportLicence.Remark Conditions,
                    ExportLicence.ApplicationNo, ExportLicence.ApplicationDate, ExportLicence.CommodityType,
                    ExportLicence.ApproveDate
                FROM ExportLicence
                INNER JOIN PaThaKa ON PaThaKa.Id = ExportLicence.PaThaKaId
                INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
                INNER JOIN ExportLicenceItem ON ExportLicence.Id = ExportLicenceItem.ExportLicenceId
                INNER JOIN Unit unit ON ExportLicenceItem.UnitId = unit.Id
                INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
                INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
                INNER JOIN ExportImportSection section ON section.Id = ExportLicence.ExportImportSectionId
                INNER JOIN Countries buyerCountry ON buyerCountry.Id = ExportLicence.BuyerCountryId
                INNER JOIN ExportImportMethod method ON method.Id = ExportLicence.ExportImportMethodId
                INNER JOIN Countries consignedCountry ON consignedCountry.Id = ExportLicence.ConsignedCountryId
                INNER JOIN Countries countryofOrigin ON countryofOrigin.Id = ExportLicence.CountryofOriginId
                INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id = ExportLicence.ExportImportIncotermId
                WHERE ApplyType=''New'' AND ExportLicence.Status=''Approved''
                  AND ((@FromDate IS NULL) OR ExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ExportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
                  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
                  AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                  AND ExportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then ExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
                  AND ExportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then ExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
                  AND ExportLicence.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then ExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
                ORDER BY ' + @ob + N'
                OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
            ) pg
            ORDER BY ' + @ob + N'
        OPTION (RECOMPILE);';
    END

    --------------------------------------------------------------------------
    --  BORDER
    --------------------------------------------------------------------------
    ELSE IF @Type = N'Border'
    BEGIN
        -- Total count over the UNION ALL
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1 THEN
            N'DECLARE @__total int = (
                SELECT COUNT(*) FROM (
                    SELECT BorderExportLicence.Id
                    FROM BorderExportLicence
                    INNER JOIN PaThaKa ON PaThaKa.Id = BorderExportLicence.PaThaKaId
                    INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
                    INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
                    INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
                    INNER JOIN ExportImportSection section ON section.Id = BorderExportLicence.ExportImportSectionId
                    INNER JOIN Countries buyerCountry ON buyerCountry.Id = BorderExportLicence.BuyerCountryId
                    INNER JOIN ExportImportMethod method ON method.Id = BorderExportLicence.ExportImportMethodId
                    INNER JOIN Countries consignedCountry ON consignedCountry.Id = BorderExportLicence.ConsignedCountryId
                    INNER JOIN Countries countryofOrigin ON countryofOrigin.Id = BorderExportLicence.CountryofOriginId
                    INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id = BorderExportLicence.ExportImportIncotermId
                    INNER JOIN Sakhan sakhan ON sakhan.Id = BorderExportLicence.SakhanId
                    WHERE ApplyType=''New'' AND BorderExportLicence.Status=''Approved''
                      AND BorderExportLicence.CardType=''Pa Tha Ka''
                      AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
                      AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                      AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
                      AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                      AND BorderExportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then BorderExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
                      AND BorderExportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then BorderExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
                      AND BorderExportLicence.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then BorderExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
                      AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
                    UNION ALL
                    SELECT BorderExportLicence.Id
                    FROM BorderExportLicence
                    INNER JOIN IndividualTrading ON IndividualTrading.Id = BorderExportLicence.IndividualTradingId
                    INNER JOIN PaThaKaType paThaKaType ON IndividualTrading.PaThaKaTypeId = paThaKaType.Id
                    INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
                    INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
                    INNER JOIN ExportImportSection section ON section.Id = BorderExportLicence.ExportImportSectionId
                    INNER JOIN Countries buyerCountry ON buyerCountry.Id = BorderExportLicence.BuyerCountryId
                    INNER JOIN ExportImportMethod method ON method.Id = BorderExportLicence.ExportImportMethodId
                    INNER JOIN Countries consignedCountry ON consignedCountry.Id = BorderExportLicence.ConsignedCountryId
                    INNER JOIN Countries countryofOrigin ON countryofOrigin.Id = BorderExportLicence.CountryofOriginId
                    INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id = BorderExportLicence.ExportImportIncotermId
                    INNER JOIN Sakhan sakhan ON sakhan.Id = BorderExportLicence.SakhanId
                    WHERE ApplyType=''New'' AND BorderExportLicence.Status=''Approved''
                      AND BorderExportLicence.CardType=''Individual Trading''
                      AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
                      AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
                      AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
                      AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                      AND BorderExportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then BorderExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
                      AND BorderExportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then BorderExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
                      AND BorderExportLicence.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then BorderExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
                      AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
                ) tmp
            ); '
        ELSE N'DECLARE @__total int = NULL; ' END;

        -- Main paged query wrapping the UNION ALL
        SET @sql = @cntpart + N'
            SELECT *, @__total AS TotalCount
            FROM (
                SELECT * FROM (
                    SELECT
                        paThaKaType.Id PaThaKaTypeId, paThaKaType.Code PaThaKaTypeCode, paThaKaType.Description PaThaKaTypeName,
                        sakhan.Id SakhanId, sakhan.Code SakhanCode, sakhan.Name SakhanName,
                        ExportImportSectionId, ExportImportMethodId, ExportImportIncotermId, BuyerCountryId,
                        section.Code SectionCode, section.Name SectionName,
                        ExportLicenceNo LicenceNo, BorderExportLicence.IssuedDate LicenceDate,
                        CompanyRegistrationNo, CompanyName, UnitLevel, StreetNumberStreetName,
                        QuarterCityTownship, State, Country, PostalCode,
                        BuyerName, BuyerAddress, buyerCountry.Name BuyerCountry,
                        (SELECT '',''+portofDischarge.Name
                         FROM PortOfDischarge portofDischarge
                         WHERE '',''+BorderExportLicence.PortofExportId+'','' LIKE ''%,''+CAST(portofDischarge.Id as nvarchar(20))+'',%''
                         FOR XML PATH(''''), TYPE).value(''substring(text()[1], 2)'', ''varchar(max)'') as PortofExport,
                        PortofDischarge,
                        LastDate, method.Name MethodName,
                        (SELECT '',''+countries.Name
                         FROM Countries countries
                         WHERE '',''+BorderExportLicence.DestinationCountryId+'','' LIKE ''%,''+CAST(countries.Id as nvarchar(20))+'',%''
                         FOR XML PATH(''''), TYPE).value(''substring(text()[1], 2)'', ''varchar(max)'') as DestinationCountry,
                        consignedCountry.Name ConsignedCountry, countryofOrigin.Name CountryofOrigin,
                        HSCode.Code HSCode, HSCode.Description+'' ''+ISNULL(BorderExportLicenceItem.Description,'''') HSDescription,
                        unit.Code Unit, Price, Quantity, Amount, currency.Code Currency,
                        BorderExportLicence.Remark Conditions,
                        BorderExportLicence.ApplicationNo, BorderExportLicence.ApplicationDate, BorderExportLicence.CommodityType,
                        BorderExportLicence.ApproveDate
                    FROM BorderExportLicence
                    INNER JOIN PaThaKa ON PaThaKa.Id = BorderExportLicence.PaThaKaId
                    INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
                    INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
                    INNER JOIN Unit unit ON BorderExportLicenceItem.UnitId = unit.Id
                    INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
                    INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
                    INNER JOIN ExportImportSection section ON section.Id = BorderExportLicence.ExportImportSectionId
                    INNER JOIN Countries buyerCountry ON buyerCountry.Id = BorderExportLicence.BuyerCountryId
                    INNER JOIN ExportImportMethod method ON method.Id = BorderExportLicence.ExportImportMethodId
                    INNER JOIN Countries consignedCountry ON consignedCountry.Id = BorderExportLicence.ConsignedCountryId
                    INNER JOIN Countries countryofOrigin ON countryofOrigin.Id = BorderExportLicence.CountryofOriginId
                    INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id = BorderExportLicence.ExportImportIncotermId
                    INNER JOIN Sakhan sakhan ON sakhan.Id = BorderExportLicence.SakhanId
                    WHERE ApplyType=''New'' AND BorderExportLicence.Status=''Approved''
                      AND BorderExportLicence.CardType=''Pa Tha Ka''
                      AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
                      AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                      AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
                      AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                      AND BorderExportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then BorderExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
                      AND BorderExportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then BorderExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
                      AND BorderExportLicence.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then BorderExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
                      AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)

                    UNION ALL

                    SELECT
                        paThaKaType.Id PaThaKaTypeId, paThaKaType.Code PaThaKaTypeCode, paThaKaType.Description PaThaKaTypeName,
                        sakhan.Id SakhanId, sakhan.Code SakhanCode, sakhan.Name SakhanName,
                        ExportImportSectionId, ExportImportMethodId, ExportImportIncotermId, BuyerCountryId,
                        section.Code SectionCode, section.Name SectionName,
                        ExportLicenceNo LicenceNo, BorderExportLicence.IssuedDate LicenceDate,
                        IndividualTrading.TINNo CompanyRegistrationNo, IndividualTrading.Name CompanyName,
                        UnitLevel, StreetNumberStreetName, QuarterCityTownship, State, Country, PostalCode,
                        BuyerName, BuyerAddress, buyerCountry.Name BuyerCountry,
                        (SELECT '',''+portofDischarge.Name
                         FROM PortOfDischarge portofDischarge
                         WHERE '',''+BorderExportLicence.PortofExportId+'','' LIKE ''%,''+CAST(portofDischarge.Id as nvarchar(20))+'',%''
                         FOR XML PATH(''''), TYPE).value(''substring(text()[1], 2)'', ''varchar(max)'') as PortofExport,
                        PortofDischarge,
                        LastDate, method.Name MethodName,
                        (SELECT '',''+countries.Name
                         FROM Countries countries
                         WHERE '',''+BorderExportLicence.DestinationCountryId+'','' LIKE ''%,''+CAST(countries.Id as nvarchar(20))+'',%''
                         FOR XML PATH(''''), TYPE).value(''substring(text()[1], 2)'', ''varchar(max)'') as DestinationCountry,
                        consignedCountry.Name ConsignedCountry, countryofOrigin.Name CountryofOrigin,
                        HSCode.Code HSCode, HSCode.Description+'' ''+ISNULL(BorderExportLicenceItem.Description,'''') HSDescription,
                        unit.Code Unit, Price, Quantity, Amount, currency.Code Currency,
                        BorderExportLicence.Remark Conditions,
                        BorderExportLicence.ApplicationNo, BorderExportLicence.ApplicationDate, BorderExportLicence.CommodityType,
                        BorderExportLicence.ApproveDate
                    FROM BorderExportLicence
                    INNER JOIN IndividualTrading ON IndividualTrading.Id = BorderExportLicence.IndividualTradingId
                    INNER JOIN PaThaKaType paThaKaType ON IndividualTrading.PaThaKaTypeId = paThaKaType.Id
                    INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
                    INNER JOIN Unit unit ON BorderExportLicenceItem.UnitId = unit.Id
                    INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
                    INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
                    INNER JOIN ExportImportSection section ON section.Id = BorderExportLicence.ExportImportSectionId
                    INNER JOIN Countries buyerCountry ON buyerCountry.Id = BorderExportLicence.BuyerCountryId
                    INNER JOIN ExportImportMethod method ON method.Id = BorderExportLicence.ExportImportMethodId
                    INNER JOIN Countries consignedCountry ON consignedCountry.Id = BorderExportLicence.ConsignedCountryId
                    INNER JOIN Countries countryofOrigin ON countryofOrigin.Id = BorderExportLicence.CountryofOriginId
                    INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id = BorderExportLicence.ExportImportIncotermId
                    INNER JOIN Sakhan sakhan ON sakhan.Id = BorderExportLicence.SakhanId
                    WHERE ApplyType=''New'' AND BorderExportLicence.Status=''Approved''
                      AND BorderExportLicence.CardType=''Individual Trading''
                      AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
                      AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
                      AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
                      AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                      AND BorderExportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then BorderExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
                      AND BorderExportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then BorderExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
                      AND BorderExportLicence.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then BorderExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
                      AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
                ) u
                ORDER BY ' + @ob + N'
                OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
            ) pg
            ORDER BY ' + @ob + N'
        OPTION (RECOMPILE);';
    END

    EXEC sp_executesql @sql,
        N'@Type nvarchar(20), @FromDate datetime, @ToDate datetime,
          @PaThaKaTypeId int, @ExportImportSectionId int, @ExportImportMethodId int,
          @ExportImportIncotermId int, @BuyerCountryId int, @CompanyRegistrationNo nvarchar(50),
          @SakhanId int, @off bigint, @ps bigint',
        @Type = @Type, @FromDate = @FromDate, @ToDate = @ToDate,
        @PaThaKaTypeId = @PaThaKaTypeId, @ExportImportSectionId = @ExportImportSectionId,
        @ExportImportMethodId = @ExportImportMethodId, @ExportImportIncotermId = @ExportImportIncotermId,
        @BuyerCountryId = @BuyerCountryId, @CompanyRegistrationNo = @CompanyRegistrationNo,
        @SakhanId = @SakhanId, @off = @off, @ps = @ps;
END

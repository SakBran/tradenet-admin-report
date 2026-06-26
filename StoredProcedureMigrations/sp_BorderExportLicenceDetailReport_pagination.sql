SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_BorderExportLicenceDetailReport_Pagination]
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

    DECLARE @ps bigint = CASE
        WHEN ISNULL(@PageSize, 0) <= 0 THEN 9223372036854775807
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
        ELSE @PageSize
    END;
    DECLARE @off bigint = CASE
        WHEN ISNULL(@PageSize, 0) <= 0 THEN 0
        ELSE ISNULL(@PageIndex, 0) * CAST(@PageSize AS bigint)
    END;
    DECLARE @dir nvarchar(4) = CASE
        WHEN UPPER(ISNULL(@SortOrder, 'ASC')) = 'DESC' THEN 'DESC'
        ELSE 'ASC'
    END;

    DECLARE @headerSortColumn nvarchar(128) = CASE
        WHEN @SortColumn IN (
            N'LicenceDate', N'LicenceNo', N'SectionCode', N'SectionName',
            N'CompanyName', N'PaThaKaTypeCode', N'PaThaKaTypeName',
            N'BuyerCountry', N'MethodName', N'ConsignedCountry',
            N'CountryofOrigin'
        ) THEN @SortColumn
        ELSE NULL
    END;

    IF @SortColumn IS NULL OR @headerSortColumn IS NOT NULL
    BEGIN
        DECLARE @headerOrderBy nvarchar(400) = CASE
            WHEN @headerSortColumn IS NOT NULL
                THEN QUOTENAME(@headerSortColumn) + N' ' + @dir
                    + N', [LicenceDate] ASC, [LicenceNo] ASC, src.[BorderExportLicenceId] ASC'
            ELSE N'[LicenceDate] ASC, [LicenceNo] ASC, src.[BorderExportLicenceId] ASC'
        END;

        SELECT
            src.BorderExportLicenceId,
            src.CardType,
            src.PaThaKaTypeId,
            src.PaThaKaTypeCode,
            src.PaThaKaTypeName,
            src.SakhanId,
            src.SakhanCode,
            src.SakhanName,
            src.ExportImportSectionId,
            src.ExportImportMethodId,
            src.ExportImportIncotermId,
            src.BuyerCountryId,
            src.SectionCode,
            src.SectionName,
            src.LicenceNo,
            src.LicenceDate,
            src.CompanyRegistrationNo,
            src.CompanyName,
            src.BuyerCountry,
            src.MethodName,
            src.ConsignedCountry,
            src.CountryofOrigin,
            itemCount.ItemCount
        INTO #HeaderRows
        FROM
        (
            SELECT
                BorderExportLicence.Id BorderExportLicenceId,
                N'Pa Tha Ka' CardType,
                paThaKaType.Id PaThaKaTypeId,
                paThaKaType.Code PaThaKaTypeCode,
                paThaKaType.Description PaThaKaTypeName,
                sakhan.Id SakhanId,
                sakhan.Code SakhanCode,
                sakhan.Name SakhanName,
                BorderExportLicence.ExportImportSectionId,
                BorderExportLicence.ExportImportMethodId,
                BorderExportLicence.ExportImportIncotermId,
                BorderExportLicence.BuyerCountryId,
                section.Code SectionCode,
                section.Name SectionName,
                BorderExportLicence.ExportLicenceNo LicenceNo,
                BorderExportLicence.IssuedDate LicenceDate,
                PaThaKa.CompanyRegistrationNo,
                PaThaKa.CompanyName,
                buyerCountry.Name BuyerCountry,
                method.Name MethodName,
                consignedCountry.Name ConsignedCountry,
                countryofOrigin.Name CountryofOrigin
            FROM BorderExportLicence WITH (INDEX([IX_BorderExportLicence_Report_NewDetail]))
            INNER JOIN PaThaKa ON PaThaKa.Id = BorderExportLicence.PaThaKaId
            INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
            INNER JOIN ExportImportSection section ON section.Id = BorderExportLicence.ExportImportSectionId
            INNER JOIN Countries buyerCountry ON buyerCountry.Id = BorderExportLicence.BuyerCountryId
            INNER JOIN ExportImportMethod method ON method.Id = BorderExportLicence.ExportImportMethodId
            INNER JOIN Countries consignedCountry ON consignedCountry.Id = BorderExportLicence.ConsignedCountryId
            INNER JOIN Countries countryofOrigin ON countryofOrigin.Id = BorderExportLicence.CountryofOriginId
            INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id = BorderExportLicence.ExportImportIncotermId
            INNER JOIN Sakhan sakhan ON sakhan.Id = BorderExportLicence.SakhanId
            WHERE BorderExportLicence.ApplyType = 'New'
              AND BorderExportLicence.Status = 'Approved'
              AND BorderExportLicence.CardType = 'Pa Tha Ka'
              AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
              AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
              AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
              AND paThaKaType.Id = (CASE WHEN @PaThaKaTypeId = 0 THEN paThaKaType.Id ELSE @PaThaKaTypeId END)
              AND BorderExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
              AND BorderExportLicence.ExportImportMethodId = (CASE WHEN @ExportImportMethodId = 0 THEN BorderExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
              AND BorderExportLicence.ExportImportIncotermId = (CASE WHEN @ExportImportIncotermId = 0 THEN BorderExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
              AND BorderExportLicence.BuyerCountryId = (CASE WHEN @BuyerCountryId = 0 THEN BorderExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
              AND BorderExportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)

            UNION ALL

            SELECT
                BorderExportLicence.Id BorderExportLicenceId,
                N'Individual Trading' CardType,
                paThaKaType.Id PaThaKaTypeId,
                paThaKaType.Code PaThaKaTypeCode,
                paThaKaType.Description PaThaKaTypeName,
                sakhan.Id SakhanId,
                sakhan.Code SakhanCode,
                sakhan.Name SakhanName,
                BorderExportLicence.ExportImportSectionId,
                BorderExportLicence.ExportImportMethodId,
                BorderExportLicence.ExportImportIncotermId,
                BorderExportLicence.BuyerCountryId,
                section.Code SectionCode,
                section.Name SectionName,
                BorderExportLicence.ExportLicenceNo LicenceNo,
                BorderExportLicence.IssuedDate LicenceDate,
                IndividualTrading.TINNo CompanyRegistrationNo,
                IndividualTrading.Name CompanyName,
                buyerCountry.Name BuyerCountry,
                method.Name MethodName,
                consignedCountry.Name ConsignedCountry,
                countryofOrigin.Name CountryofOrigin
            FROM BorderExportLicence WITH (INDEX([IX_BorderExportLicence_Report_NewDetail]))
            INNER JOIN IndividualTrading WITH (INDEX([IX_IndividualTrading_Report_TINNo]))
                ON IndividualTrading.Id = BorderExportLicence.IndividualTradingId
            INNER JOIN PaThaKaType paThaKaType ON IndividualTrading.PaThaKaTypeId = paThaKaType.Id
            INNER JOIN ExportImportSection section ON section.Id = BorderExportLicence.ExportImportSectionId
            INNER JOIN Countries buyerCountry ON buyerCountry.Id = BorderExportLicence.BuyerCountryId
            INNER JOIN ExportImportMethod method ON method.Id = BorderExportLicence.ExportImportMethodId
            INNER JOIN Countries consignedCountry ON consignedCountry.Id = BorderExportLicence.ConsignedCountryId
            INNER JOIN Countries countryofOrigin ON countryofOrigin.Id = BorderExportLicence.CountryofOriginId
            INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id = BorderExportLicence.ExportImportIncotermId
            INNER JOIN Sakhan sakhan ON sakhan.Id = BorderExportLicence.SakhanId
            WHERE BorderExportLicence.ApplyType = 'New'
              AND BorderExportLicence.Status = 'Approved'
              AND BorderExportLicence.CardType = 'Individual Trading'
              AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
              AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
              AND IndividualTrading.TINNo = (CASE WHEN @CompanyRegistrationNo = '' THEN IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
              AND paThaKaType.Id = (CASE WHEN @PaThaKaTypeId = 0 THEN paThaKaType.Id ELSE @PaThaKaTypeId END)
              AND BorderExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
              AND BorderExportLicence.ExportImportMethodId = (CASE WHEN @ExportImportMethodId = 0 THEN BorderExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
              AND BorderExportLicence.ExportImportIncotermId = (CASE WHEN @ExportImportIncotermId = 0 THEN BorderExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
              AND BorderExportLicence.BuyerCountryId = (CASE WHEN @BuyerCountryId = 0 THEN BorderExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
              AND BorderExportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
        ) src
        OUTER APPLY
        (
            SELECT COUNT(*) ItemCount
            FROM BorderExportLicenceItem WITH (INDEX([IX_BorderExportLicenceItem_Report_Licence]))
            WHERE BorderExportLicenceId = src.BorderExportLicenceId
        ) itemCount
        WHERE itemCount.ItemCount > 0
        OPTION (MAXDOP 1, RECOMPILE, MAX_GRANT_PERCENT = 5);

        CREATE UNIQUE CLUSTERED INDEX IX_HeaderRows_Id
            ON #HeaderRows (BorderExportLicenceId);

        DECLARE @totalCount int = 0;
        IF @IncludeTotalCount = 1
        BEGIN
            SELECT @totalCount = ISNULL(SUM(ItemCount), 0)
            FROM #HeaderRows;
        END;

        DECLARE @sql nvarchar(max) = N'
            ;WITH OrderedLicences AS
            (
                SELECT
                    src.*,
                    SUM(src.ItemCount) OVER (ORDER BY ' + @headerOrderBy + N') RunningItemCount
                FROM #HeaderRows src
            ),
            NeededLicences AS
            (
                SELECT *
                FROM OrderedLicences
                WHERE RunningItemCount > @off
                  AND RunningItemCount - ItemCount < (@off + @ps)
            ),
            DetailRows AS
            (
                SELECT
                    needed.PaThaKaTypeId,
                    needed.PaThaKaTypeCode,
                    needed.PaThaKaTypeName,
                    needed.SakhanId,
                    needed.SakhanCode,
                    needed.SakhanName,
                    BorderExportLicence.ExportImportSectionId,
                    BorderExportLicence.ExportImportMethodId,
                    BorderExportLicence.ExportImportIncotermId,
                    BorderExportLicence.BuyerCountryId,
                    section.Code SectionCode,
                    section.Name SectionName,
                    BorderExportLicence.ExportLicenceNo LicenceNo,
                    BorderExportLicence.IssuedDate LicenceDate,
                    CASE WHEN needed.CardType = N''Pa Tha Ka'' THEN PaThaKa.CompanyRegistrationNo ELSE IndividualTrading.TINNo END CompanyRegistrationNo,
                    CASE WHEN needed.CardType = N''Pa Tha Ka'' THEN PaThaKa.CompanyName ELSE IndividualTrading.Name END CompanyName,
                    CASE WHEN needed.CardType = N''Pa Tha Ka'' THEN PaThaKa.UnitLevel ELSE IndividualTrading.UnitLevel END UnitLevel,
                    CASE WHEN needed.CardType = N''Pa Tha Ka'' THEN PaThaKa.StreetNumberStreetName ELSE IndividualTrading.StreetNumberStreetName END StreetNumberStreetName,
                    CASE WHEN needed.CardType = N''Pa Tha Ka'' THEN PaThaKa.QuarterCityTownship ELSE IndividualTrading.QuarterCityTownship END QuarterCityTownship,
                    CASE WHEN needed.CardType = N''Pa Tha Ka'' THEN PaThaKa.State ELSE IndividualTrading.State END State,
                    CASE WHEN needed.CardType = N''Pa Tha Ka'' THEN PaThaKa.Country ELSE IndividualTrading.Country END Country,
                    CASE WHEN needed.CardType = N''Pa Tha Ka'' THEN PaThaKa.PostalCode ELSE IndividualTrading.PostalCode END PostalCode,
                    BorderExportLicence.BuyerName,
                    BorderExportLicence.BuyerAddress,
                    buyerCountry.Name BuyerCountry,
                    BorderExportLicence.PortofExportId PortofExportIds,
                    BorderExportLicence.PortofDischarge,
                    BorderExportLicence.LastDate,
                    method.Name MethodName,
                    BorderExportLicence.DestinationCountryId DestinationCountryIds,
                    consignedCountry.Name ConsignedCountry,
                    countryofOrigin.Name CountryofOrigin,
                    HSCode.Code HSCode,
                    HSCode.Description + '' '' + ISNULL(BorderExportLicenceItem.Description, '''') HSDescription,
                    unit.Code Unit,
                    BorderExportLicenceItem.Price,
                    BorderExportLicenceItem.Quantity,
                    BorderExportLicenceItem.Amount,
                    currency.Code Currency,
                    BorderExportLicence.Remark Conditions,
                    BorderExportLicence.ApplicationNo,
                    BorderExportLicence.ApplicationDate,
                    BorderExportLicence.CommodityType,
                    BorderExportLicence.ApproveDate,
                    (needed.RunningItemCount - needed.ItemCount)
                        + ROW_NUMBER() OVER (PARTITION BY needed.BorderExportLicenceId ORDER BY BorderExportLicenceItem.Id ASC) GlobalRowNum
                FROM NeededLicences needed
                INNER JOIN BorderExportLicence
                    ON BorderExportLicence.Id = needed.BorderExportLicenceId
                LEFT JOIN PaThaKa
                    ON PaThaKa.Id = BorderExportLicence.PaThaKaId
                LEFT JOIN IndividualTrading
                    ON IndividualTrading.Id = BorderExportLicence.IndividualTradingId
                INNER JOIN BorderExportLicenceItem WITH (INDEX([IX_BorderExportLicenceItem_Report_Licence]))
                    ON BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id
                INNER JOIN Unit unit
                    ON unit.Id = BorderExportLicenceItem.UnitId
                INNER JOIN Currency currency
                    ON currency.Id = BorderExportLicenceItem.CurrencyId
                INNER JOIN HSCode
                    ON HSCode.Id = BorderExportLicenceItem.HSCodeId
                INNER JOIN ExportImportSection section
                    ON section.Id = BorderExportLicence.ExportImportSectionId
                INNER JOIN Countries buyerCountry
                    ON buyerCountry.Id = BorderExportLicence.BuyerCountryId
                INNER JOIN ExportImportMethod method
                    ON method.Id = BorderExportLicence.ExportImportMethodId
                INNER JOIN Countries consignedCountry
                    ON consignedCountry.Id = BorderExportLicence.ConsignedCountryId
                INNER JOIN Countries countryofOrigin
                    ON countryofOrigin.Id = BorderExportLicence.CountryofOriginId
            )
            SELECT
                detail.PaThaKaTypeId,
                detail.PaThaKaTypeCode,
                detail.PaThaKaTypeName,
                detail.SakhanId,
                detail.SakhanCode,
                detail.SakhanName,
                detail.ExportImportSectionId,
                detail.ExportImportMethodId,
                detail.ExportImportIncotermId,
                detail.BuyerCountryId,
                detail.SectionCode,
                detail.SectionName,
                detail.LicenceNo,
                detail.LicenceDate,
                detail.CompanyRegistrationNo,
                detail.CompanyName,
                detail.UnitLevel,
                detail.StreetNumberStreetName,
                detail.QuarterCityTownship,
                detail.State,
                detail.Country,
                detail.PostalCode,
                detail.BuyerName,
                detail.BuyerAddress,
                detail.BuyerCountry,
                detail.PortofExportIds,
                CAST(NULL AS nvarchar(max)) PortofExport,
                detail.PortofDischarge,
                detail.LastDate,
                detail.MethodName,
                detail.DestinationCountryIds,
                CAST(NULL AS nvarchar(max)) DestinationCountry,
                detail.ConsignedCountry,
                detail.CountryofOrigin,
                detail.HSCode,
                detail.HSDescription,
                detail.Unit,
                detail.Price,
                detail.Quantity,
                detail.Amount,
                detail.Currency,
                detail.Conditions,
                detail.ApplicationNo,
                detail.ApplicationDate,
                detail.CommodityType,
                detail.ApproveDate,
                ISNULL(@__total, 0) TotalCount
            FROM DetailRows detail
            WHERE detail.GlobalRowNum > @off
              AND detail.GlobalRowNum <= (@off + @ps)
            ORDER BY detail.GlobalRowNum
            OPTION (MAXDOP 1, RECOMPILE, MAX_GRANT_PERCENT = 5);';

        EXEC sp_executesql
            @sql,
            N'@off bigint, @ps bigint, @__total int',
            @off = @off,
            @ps = @ps,
            @__total = @totalCount;

        RETURN;
    END;

    ;WITH BorderRows AS
    (
        SELECT
            paThaKaType.Id PaThaKaTypeId,
            paThaKaType.Code PaThaKaTypeCode,
            paThaKaType.Description PaThaKaTypeName,
            sakhan.Id SakhanId,
            sakhan.Code SakhanCode,
            sakhan.Name SakhanName,
            BorderExportLicence.ExportImportSectionId,
            BorderExportLicence.ExportImportMethodId,
            BorderExportLicence.ExportImportIncotermId,
            BorderExportLicence.BuyerCountryId,
            section.Code SectionCode,
            section.Name SectionName,
            BorderExportLicence.ExportLicenceNo LicenceNo,
            BorderExportLicence.IssuedDate LicenceDate,
            PaThaKa.CompanyRegistrationNo,
            PaThaKa.CompanyName,
            PaThaKa.UnitLevel,
            PaThaKa.StreetNumberStreetName,
            PaThaKa.QuarterCityTownship,
            PaThaKa.State,
            PaThaKa.Country,
            PaThaKa.PostalCode,
            BorderExportLicence.BuyerName,
            BorderExportLicence.BuyerAddress,
            buyerCountry.Name BuyerCountry,
            BorderExportLicence.PortofExportId PortofExportIds,
            CAST(NULL AS nvarchar(max)) PortofExport,
            BorderExportLicence.PortofDischarge,
            BorderExportLicence.LastDate,
            method.Name MethodName,
            BorderExportLicence.DestinationCountryId DestinationCountryIds,
            CAST(NULL AS nvarchar(max)) DestinationCountry,
            consignedCountry.Name ConsignedCountry,
            countryofOrigin.Name CountryofOrigin,
            HSCode.Code HSCode,
            HSCode.Description + ' ' + ISNULL(BorderExportLicenceItem.Description, '') HSDescription,
            unit.Code Unit,
            BorderExportLicenceItem.Price,
            BorderExportLicenceItem.Quantity,
            BorderExportLicenceItem.Amount,
            currency.Code Currency,
            BorderExportLicence.Remark Conditions,
            BorderExportLicence.ApplicationNo,
            BorderExportLicence.ApplicationDate,
            BorderExportLicence.CommodityType,
            BorderExportLicence.ApproveDate
        FROM BorderExportLicence WITH (INDEX([IX_BorderExportLicence_Report_NewDetail]))
        INNER JOIN PaThaKa ON PaThaKa.Id = BorderExportLicence.PaThaKaId
        INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
        INNER JOIN BorderExportLicenceItem WITH (INDEX([IX_BorderExportLicenceItem_Report_Licence]))
            ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
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
        WHERE BorderExportLicence.ApplyType = 'New'
          AND BorderExportLicence.Status = 'Approved'
          AND BorderExportLicence.CardType = 'Pa Tha Ka'
          AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
          AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
          AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
          AND paThaKaType.Id = (CASE WHEN @PaThaKaTypeId = 0 THEN paThaKaType.Id ELSE @PaThaKaTypeId END)
          AND BorderExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
          AND BorderExportLicence.ExportImportMethodId = (CASE WHEN @ExportImportMethodId = 0 THEN BorderExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
          AND BorderExportLicence.ExportImportIncotermId = (CASE WHEN @ExportImportIncotermId = 0 THEN BorderExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
          AND BorderExportLicence.BuyerCountryId = (CASE WHEN @BuyerCountryId = 0 THEN BorderExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
          AND BorderExportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)

        UNION ALL

        SELECT
            paThaKaType.Id PaThaKaTypeId,
            paThaKaType.Code PaThaKaTypeCode,
            paThaKaType.Description PaThaKaTypeName,
            sakhan.Id SakhanId,
            sakhan.Code SakhanCode,
            sakhan.Name SakhanName,
            BorderExportLicence.ExportImportSectionId,
            BorderExportLicence.ExportImportMethodId,
            BorderExportLicence.ExportImportIncotermId,
            BorderExportLicence.BuyerCountryId,
            section.Code SectionCode,
            section.Name SectionName,
            BorderExportLicence.ExportLicenceNo LicenceNo,
            BorderExportLicence.IssuedDate LicenceDate,
            IndividualTrading.TINNo CompanyRegistrationNo,
            IndividualTrading.Name CompanyName,
            IndividualTrading.UnitLevel,
            IndividualTrading.StreetNumberStreetName,
            IndividualTrading.QuarterCityTownship,
            IndividualTrading.State,
            IndividualTrading.Country,
            IndividualTrading.PostalCode,
            BorderExportLicence.BuyerName,
            BorderExportLicence.BuyerAddress,
            buyerCountry.Name BuyerCountry,
            BorderExportLicence.PortofExportId PortofExportIds,
            CAST(NULL AS nvarchar(max)) PortofExport,
            BorderExportLicence.PortofDischarge,
            BorderExportLicence.LastDate,
            method.Name MethodName,
            BorderExportLicence.DestinationCountryId DestinationCountryIds,
            CAST(NULL AS nvarchar(max)) DestinationCountry,
            consignedCountry.Name ConsignedCountry,
            countryofOrigin.Name CountryofOrigin,
            HSCode.Code HSCode,
            HSCode.Description + ' ' + ISNULL(BorderExportLicenceItem.Description, '') HSDescription,
            unit.Code Unit,
            BorderExportLicenceItem.Price,
            BorderExportLicenceItem.Quantity,
            BorderExportLicenceItem.Amount,
            currency.Code Currency,
            BorderExportLicence.Remark Conditions,
            BorderExportLicence.ApplicationNo,
            BorderExportLicence.ApplicationDate,
            BorderExportLicence.CommodityType,
            BorderExportLicence.ApproveDate
        FROM BorderExportLicence WITH (INDEX([IX_BorderExportLicence_Report_NewDetail]))
        INNER JOIN IndividualTrading WITH (INDEX([IX_IndividualTrading_Report_TINNo]))
            ON IndividualTrading.Id = BorderExportLicence.IndividualTradingId
        INNER JOIN PaThaKaType paThaKaType ON IndividualTrading.PaThaKaTypeId = paThaKaType.Id
        INNER JOIN BorderExportLicenceItem WITH (INDEX([IX_BorderExportLicenceItem_Report_Licence]))
            ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
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
        WHERE BorderExportLicence.ApplyType = 'New'
          AND BorderExportLicence.Status = 'Approved'
          AND BorderExportLicence.CardType = 'Individual Trading'
          AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
          AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
          AND IndividualTrading.TINNo = (CASE WHEN @CompanyRegistrationNo = '' THEN IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
          AND paThaKaType.Id = (CASE WHEN @PaThaKaTypeId = 0 THEN paThaKaType.Id ELSE @PaThaKaTypeId END)
          AND BorderExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
          AND BorderExportLicence.ExportImportMethodId = (CASE WHEN @ExportImportMethodId = 0 THEN BorderExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
          AND BorderExportLicence.ExportImportIncotermId = (CASE WHEN @ExportImportIncotermId = 0 THEN BorderExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
          AND BorderExportLicence.BuyerCountryId = (CASE WHEN @BuyerCountryId = 0 THEN BorderExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
          AND BorderExportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
    )
    SELECT borderRows.*,
           CASE WHEN @IncludeTotalCount = 1 THEN COUNT(*) OVER () ELSE 0 END TotalCount
    FROM BorderRows borderRows
    ORDER BY
        CASE WHEN @SortColumn = 'HSCode' AND @dir = 'ASC' THEN borderRows.HSCode END ASC,
        CASE WHEN @SortColumn = 'HSCode' AND @dir = 'DESC' THEN borderRows.HSCode END DESC,
        borderRows.LicenceDate ASC,
        borderRows.LicenceNo ASC
    OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    OPTION (MAXDOP 1, RECOMPILE, MAX_GRANT_PERCENT = 5);
END
GO

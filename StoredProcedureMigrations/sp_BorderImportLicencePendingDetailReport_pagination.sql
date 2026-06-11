SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_BorderImportLicencePendingDetailReport_pagination]
    @FromDate datetime,
    @ToDate datetime,
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
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @size int = CASE WHEN ISNULL(@PageSize, 0) <= 0 THEN 2147483647 ELSE @PageSize END;
    DECLARE @start bigint = ISNULL(@PageIndex, 0) * CAST(@size AS bigint) + 1;
    DECLARE @end bigint = @start + @size - 1 + CASE WHEN @IncludeTotalCount = 0 THEN 1 ELSE 0 END;

    CREATE TABLE #Base
    (
        LicenceId char(36) NOT NULL,
        SortDate datetime NULL,
        LicenceNo nvarchar(100) NULL
    );

    INSERT INTO #Base (LicenceId, SortDate, LicenceNo)
    SELECT licence.Id, licence.ApplicationDate, licence.ImportLicenceNo
    FROM dbo.BorderImportLicence licence
    INNER JOIN dbo.PaThaKa paThaKa ON paThaKa.Id = licence.PaThaKaId
    WHERE licence.ApplyType = N'New'
      AND licence.Status = N'Pending'
      AND licence.CardType = N'Pa Tha Ka'
      AND licence.ApplicationDate >= @FromDate
      AND licence.ApplicationDate <= @ToDate
      AND (@CompanyRegistrationNo = N'' OR paThaKa.CompanyRegistrationNo = @CompanyRegistrationNo)
      AND (@PaThaKaTypeId = 0 OR paThaKa.PaThaKaTypeId = @PaThaKaTypeId)
      AND (@ExportImportSectionId = 0 OR licence.ExportImportSectionId = @ExportImportSectionId)
      AND (@ExportImportMethodId = 0 OR licence.ExportImportMethodId = @ExportImportMethodId)
      AND (@ExportImportIncotermId = 0 OR licence.ExportImportIncotermId = @ExportImportIncotermId)
      AND (@SellerCountryId = 0 OR licence.SellerCountryId = @SellerCountryId)
      AND (@SakhanId = 0 OR licence.SakhanId = @SakhanId);

    INSERT INTO #Base (LicenceId, SortDate, LicenceNo)
    SELECT licence.Id, licence.ApplicationDate, licence.ImportLicenceNo
    FROM dbo.BorderImportLicence licence
    INNER JOIN dbo.IndividualTrading individualTrading ON individualTrading.Id = licence.IndividualTradingId
    WHERE licence.ApplyType = N'New'
      AND licence.Status = N'Pending'
      AND licence.CardType = N'Individual Trading'
      AND licence.ApplicationDate >= @FromDate
      AND licence.ApplicationDate <= @ToDate
      AND (@CompanyRegistrationNo = N'' OR individualTrading.TINNo = @CompanyRegistrationNo)
      AND (@PaThaKaTypeId = 0 OR individualTrading.PaThaKaTypeId = @PaThaKaTypeId)
      AND (@ExportImportSectionId = 0 OR licence.ExportImportSectionId = @ExportImportSectionId)
      AND (@ExportImportMethodId = 0 OR licence.ExportImportMethodId = @ExportImportMethodId)
      AND (@ExportImportIncotermId = 0 OR licence.ExportImportIncotermId = @ExportImportIncotermId)
      AND (@SellerCountryId = 0 OR licence.SellerCountryId = @SellerCountryId)
      AND (@SakhanId = 0 OR licence.SakhanId = @SakhanId);

    CREATE CLUSTERED INDEX IX_Base ON #Base (SortDate, LicenceNo, LicenceId);

    CREATE TABLE #PageKeys
    (
        RowNo bigint NOT NULL,
        LicenceId char(36) NOT NULL,
        ItemId char(36) NOT NULL,
        TotalCount int NOT NULL
    );

    ;WITH ItemRows AS
    (
        SELECT
            ROW_NUMBER() OVER (ORDER BY base.SortDate, base.LicenceNo, item.Id) AS RowNo,
            base.LicenceId,
            item.Id AS ItemId,
            COUNT(1) OVER () AS TotalCount
        FROM #Base base
        INNER JOIN dbo.BorderImportLicenceItem item ON item.BorderImportLicenceId = base.LicenceId
    )
    INSERT INTO #PageKeys (RowNo, LicenceId, ItemId, TotalCount)
    SELECT RowNo, LicenceId, ItemId, TotalCount
    FROM ItemRows
    WHERE RowNo BETWEEN @start AND @end;

    SELECT
        paThaKaType.Id PaThaKaTypeId,
        paThaKaType.Code PaThaKaTypeCode,
        paThaKaType.Description PaThaKaTypeName,
        licence.ExportImportSectionId,
        licence.ExportImportMethodId,
        licence.ExportImportIncotermId,
        licence.SellerCountryId,
        section.Code SectionCode,
        section.Name SectionName,
        licence.ImportLicenceNo LicenceNo,
        licence.IssuedDate LicenceDate,
        CASE WHEN licence.CardType = N'Pa Tha Ka' THEN paThaKa.CompanyRegistrationNo ELSE individualTrading.TINNo END CompanyRegistrationNo,
        CASE WHEN licence.CardType = N'Pa Tha Ka' THEN paThaKa.CompanyName ELSE individualTrading.Name END CompanyName,
        CASE WHEN licence.CardType = N'Pa Tha Ka' THEN paThaKa.UnitLevel ELSE individualTrading.UnitLevel END UnitLevel,
        CASE WHEN licence.CardType = N'Pa Tha Ka' THEN paThaKa.StreetNumberStreetName ELSE individualTrading.StreetNumberStreetName END StreetNumberStreetName,
        CASE WHEN licence.CardType = N'Pa Tha Ka' THEN paThaKa.QuarterCityTownship ELSE individualTrading.QuarterCityTownship END QuarterCityTownship,
        CASE WHEN licence.CardType = N'Pa Tha Ka' THEN paThaKa.State ELSE individualTrading.State END State,
        CASE WHEN licence.CardType = N'Pa Tha Ka' THEN paThaKa.Country ELSE individualTrading.Country END Country,
        CASE WHEN licence.CardType = N'Pa Tha Ka' THEN paThaKa.PostalCode ELSE individualTrading.PostalCode END PostalCode,
        licence.SellerName,
        licence.SellerAddress,
        sellerCountry.Name SellerCountry,
        licence.PortofDischarge,
        licence.LastDate,
        method.Name MethodName,
        (
            SELECT ',' + country.Name
            FROM dbo.Countries country
            WHERE ',' + licence.ConsignedCountryId + ',' LIKE '%,' + CAST(country.Id AS nvarchar(20)) + ',%'
            FOR XML PATH(''), TYPE
        ).value('substring(text()[1], 2)', 'varchar(max)') ConsignedCountry,
        (
            SELECT ',' + country.Name
            FROM dbo.Countries country
            WHERE ',' + licence.CountryofOriginId + ',' LIKE '%,' + CAST(country.Id AS nvarchar(20)) + ',%'
            FOR XML PATH(''), TYPE
        ).value('substring(text()[1], 2)', 'varchar(max)') CountryofOrigin,
        hsCode.Code HSCode,
        item.Description HSDescription,
        unit.Code Unit,
        item.Price,
        item.Quantity,
        item.Amount,
        currency.Code Currency,
        licence.Remark Conditions,
        licence.ApplicationNo,
        licence.ApplicationDate,
        licence.FESCNo,
        licence.CommodityType,
        licence.ApproveDate,
        CASE WHEN @IncludeTotalCount = 1 THEN keys.TotalCount ELSE 0 END TotalCount
    FROM #PageKeys keys
    INNER JOIN dbo.BorderImportLicence licence ON licence.Id = keys.LicenceId
    INNER JOIN dbo.BorderImportLicenceItem item ON item.Id = keys.ItemId
    LEFT JOIN dbo.PaThaKa paThaKa ON paThaKa.Id = licence.PaThaKaId AND licence.CardType = N'Pa Tha Ka'
    LEFT JOIN dbo.IndividualTrading individualTrading ON individualTrading.Id = licence.IndividualTradingId AND licence.CardType = N'Individual Trading'
    INNER JOIN dbo.PaThaKaType paThaKaType ON paThaKaType.Id = COALESCE(paThaKa.PaThaKaTypeId, individualTrading.PaThaKaTypeId)
    INNER JOIN dbo.Unit unit ON unit.Id = item.UnitId
    INNER JOIN dbo.Currency currency ON currency.Id = item.CurrencyId
    INNER JOIN dbo.HSCode hsCode ON hsCode.Id = item.HSCodeId
    INNER JOIN dbo.ExportImportSection section ON section.Id = licence.ExportImportSectionId
    INNER JOIN dbo.Countries sellerCountry ON sellerCountry.Id = licence.SellerCountryId
    INNER JOIN dbo.ExportImportMethod method ON method.Id = licence.ExportImportMethodId
    ORDER BY keys.RowNo
    OPTION (RECOMPILE);
END
GO

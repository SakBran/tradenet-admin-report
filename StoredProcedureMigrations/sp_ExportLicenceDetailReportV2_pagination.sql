SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_ExportLicenceDetailReportV2_Pagination]
    @FromDate               datetime,
    @ToDate                 datetime,
    @PaThaKaTypeId          int,
    @ExportImportSectionId  int,
    @ExportImportMethodId   int,
    @ExportImportIncotermId int,
    @BuyerCountryId         int,
    @CompanyRegistrationNo  nvarchar(50),
    @SortColumn             nvarchar(128) = NULL,
    @SortOrder              nvarchar(4)   = NULL,
    @PageIndex              int           = NULL,
    @PageSize               int           = NULL,
    @IncludeTotalCount      bit           = 1,
    @Auto                   nvarchar(20)  = N''
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ps bigint = CASE
        WHEN ISNULL(@PageSize, 0) <= 0 THEN 9223372036854775807
        ELSE @PageSize
    END;
    DECLARE @off bigint = CASE
        WHEN ISNULL(@PageSize, 0) <= 0 THEN 0
        ELSE ISNULL(@PageIndex, 0) * CAST(@PageSize AS bigint)
    END;
    DECLARE @__total int = NULL;

    IF @IncludeTotalCount = 1
    BEGIN
        SELECT @__total = CAST(COUNT_BIG(*) AS int)
        FROM dbo.ExportLicence AS licence WITH (INDEX(IX_ExportLicence_Report_NewDetail_Page))
        INNER JOIN dbo.PaThaKa AS paThaKa ON paThaKa.Id = licence.PaThaKaId
        INNER JOIN dbo.PaThaKaType AS paThaKaType ON paThaKa.PaThaKaTypeId = paThaKaType.Id
        WHERE licence.ApplyType = N'New'
          AND licence.Status = N'Approved'
          AND licence.CreatedDate >= @FromDate
          AND licence.CreatedDate <= @ToDate
          AND (@CompanyRegistrationNo = N'' OR paThaKa.CompanyRegistrationNo = @CompanyRegistrationNo)
          AND (@PaThaKaTypeId = 0 OR paThaKaType.Id = @PaThaKaTypeId)
          AND (@ExportImportSectionId = 0 OR licence.ExportImportSectionId = @ExportImportSectionId)
          AND (@ExportImportMethodId = 0 OR licence.ExportImportMethodId = @ExportImportMethodId)
          AND (@ExportImportIncotermId = 0 OR licence.ExportImportIncotermId = @ExportImportIncotermId)
          AND (@BuyerCountryId = 0 OR licence.BuyerCountryId = @BuyerCountryId)
          /*
              Previous behavior: no ExportLicence.[auto] predicate.
              @Auto = N'' keeps that behavior for All/backward-compatible calls.
          */
          AND (
              @Auto = N''
              OR (@Auto = N'auto' AND licence.[auto] = N'auto')
              OR (@Auto = N'none-auto' AND (licence.[auto] IS NULL OR licence.[auto] <> N'auto'))
          );
    END;

    WITH LicencePage AS
    (
        SELECT
            licence.Id AS LicenceId,
            licence.CreatedDate AS CreatedDate,
            licence.IssuedDate AS LicenceDate,
            licence.ExportLicenceNo AS LicenceNo
        FROM dbo.ExportLicence AS licence WITH (INDEX(IX_ExportLicence_Report_NewDetail_Page))
        INNER JOIN dbo.PaThaKa AS paThaKa ON paThaKa.Id = licence.PaThaKaId
        INNER JOIN dbo.PaThaKaType AS paThaKaType ON paThaKa.PaThaKaTypeId = paThaKaType.Id
        WHERE licence.ApplyType = N'New'
          AND licence.Status = N'Approved'
          AND licence.CreatedDate >= @FromDate
          AND licence.CreatedDate <= @ToDate
          AND (@CompanyRegistrationNo = N'' OR paThaKa.CompanyRegistrationNo = @CompanyRegistrationNo)
          AND (@PaThaKaTypeId = 0 OR paThaKaType.Id = @PaThaKaTypeId)
          AND (@ExportImportSectionId = 0 OR licence.ExportImportSectionId = @ExportImportSectionId)
          AND (@ExportImportMethodId = 0 OR licence.ExportImportMethodId = @ExportImportMethodId)
          AND (@ExportImportIncotermId = 0 OR licence.ExportImportIncotermId = @ExportImportIncotermId)
          AND (@BuyerCountryId = 0 OR licence.BuyerCountryId = @BuyerCountryId)
          AND (
              @Auto = N''
              OR (@Auto = N'auto' AND licence.[auto] = N'auto')
              OR (@Auto = N'none-auto' AND (licence.[auto] IS NULL OR licence.[auto] <> N'auto'))
          )
        ORDER BY licence.CreatedDate, licence.Id
        OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ),
    PageKeys AS
    (
        SELECT
            licence.LicenceId,
            item.Id AS ItemId,
            item.UniqueId AS ItemUniqueId,
            licence.CreatedDate,
            licence.LicenceDate,
            licence.LicenceNo,
            item.HSCode,
            item.ItemNo
        FROM LicencePage AS licence
        INNER JOIN dbo.ExportLicenceItem AS item WITH (INDEX(IX_ExportLicenceItem_Report_Licence_Page))
            ON licence.LicenceId = item.ExportLicenceId
    )
    SELECT
        paThaKaType.Id AS PaThaKaTypeId,
        paThaKaType.Code AS PaThaKaTypeCode,
        paThaKaType.Description AS PaThaKaTypeName,
        CAST(NULL AS int) AS SakhanId,
        CAST(NULL AS nvarchar(50)) AS SakhanCode,
        CAST(NULL AS nvarchar(200)) AS SakhanName,
        licence.ExportImportSectionId,
        licence.ExportImportMethodId,
        licence.ExportImportIncotermId,
        licence.BuyerCountryId,
        section.Code AS SectionCode,
        section.Name AS SectionName,
        licence.ExportLicenceNo AS LicenceNo,
        licence.IssuedDate AS LicenceDate,
        paThaKa.CompanyRegistrationNo,
        paThaKa.CompanyName,
        paThaKa.UnitLevel,
        paThaKa.StreetNumberStreetName,
        paThaKa.QuarterCityTownship,
        paThaKa.State,
        paThaKa.Country,
        paThaKa.PostalCode,
        licence.BuyerName,
        licence.BuyerAddress,
        buyerCountry.Name AS BuyerCountry,
        ISNULL(portNames.PortofExport, N'') AS PortofExport,
        licence.PortofDischarge,
        licence.LastDate,
        method.Name AS MethodName,
        ISNULL(destinationNames.DestinationCountry, N'') AS DestinationCountry,
        consignedCountry.Name AS ConsignedCountry,
        countryofOrigin.Name AS CountryofOrigin,
        hsCode.Code AS HSCode,
        hsCode.Description + N' ' + ISNULL(item.Description, N'') AS HSDescription,
        unit.Code AS Unit,
        item.Price,
        item.Quantity,
        item.Amount,
        currency.Code AS Currency,
        licence.Remark AS Conditions,
        licence.ApplicationNo,
        licence.ApplicationDate,
        licence.CommodityType,
        licence.ApproveDate,
        @__total AS TotalCount
    FROM PageKeys AS pageKey
    INNER JOIN dbo.ExportLicence AS licence ON pageKey.LicenceId = licence.Id
    INNER JOIN dbo.PaThaKa AS paThaKa ON paThaKa.Id = licence.PaThaKaId
    INNER JOIN dbo.PaThaKaType AS paThaKaType ON paThaKa.PaThaKaTypeId = paThaKaType.Id
    INNER JOIN dbo.ExportLicenceItem AS item
        ON pageKey.ItemId = item.Id
        AND pageKey.ItemUniqueId = item.UniqueId
    INNER JOIN dbo.Unit AS unit ON item.UnitId = unit.Id
    INNER JOIN dbo.Currency AS currency ON item.CurrencyId = currency.Id
    INNER JOIN dbo.HSCode AS hsCode ON item.HSCodeId = hsCode.Id
    INNER JOIN dbo.ExportImportSection AS section ON section.Id = licence.ExportImportSectionId
    INNER JOIN dbo.Countries AS buyerCountry ON buyerCountry.Id = licence.BuyerCountryId
    INNER JOIN dbo.ExportImportMethod AS method ON method.Id = licence.ExportImportMethodId
    INNER JOIN dbo.Countries AS consignedCountry ON consignedCountry.Id = licence.ConsignedCountryId
    INNER JOIN dbo.Countries AS countryofOrigin ON countryofOrigin.Id = licence.CountryofOriginId
    OUTER APPLY
    (
        SELECT STUFF((
            SELECT N',' + port.Name
            FROM dbo.PortOfDischarge AS port
            WHERE N',' + ISNULL(licence.PortofExportId, N'') + N','
                LIKE N'%,' + CONVERT(nvarchar(20), port.Id) + N',%'
            ORDER BY port.Id
            FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)')
        , 1, 1, N'') AS PortofExport
    ) AS portNames
    OUTER APPLY
    (
        SELECT STUFF((
            SELECT N',' + country.Name
            FROM dbo.Countries AS country
            WHERE N',' + ISNULL(licence.DestinationCountryId, N'') + N','
                LIKE N'%,' + CONVERT(nvarchar(20), country.Id) + N',%'
            ORDER BY country.Id
            FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)')
        , 1, 1, N'') AS DestinationCountry
    ) AS destinationNames
    ORDER BY pageKey.CreatedDate, pageKey.LicenceId, pageKey.HSCode, pageKey.ItemNo
    OPTION (RECOMPILE, MAXDOP 1);
END
GO

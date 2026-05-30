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
    @PageSize int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #r (
        [PaThaKaTypeId] int NULL,
        [PaThaKaTypeCode] nvarchar(max) NULL,
        [PaThaKaTypeName] nvarchar(max) NULL,
        [ExportImportSectionId] int NULL,
        [ExportImportMethodId] int NULL,
        [ExportImportIncotermId] int NULL,
        [SellerCountryId] int NULL,
        [SectionCode] nvarchar(max) NULL,
        [SectionName] nvarchar(max) NULL,
        [LicenceNo] nvarchar(max) NULL,
        [LicenceDate] datetime NULL,
        [CompanyRegistrationNo] nvarchar(max) NULL,
        [CompanyName] nvarchar(max) NULL,
        [UnitLevel] nvarchar(max) NULL,
        [StreetNumberStreetName] nvarchar(max) NULL,
        [QuarterCityTownship] nvarchar(max) NULL,
        [State] nvarchar(max) NULL,
        [Country] nvarchar(max) NULL,
        [PostalCode] nvarchar(max) NULL,
        [SellerName] nvarchar(max) NULL,
        [SellerAddress] nvarchar(max) NULL,
        [SellerCountry] nvarchar(max) NULL,
        [PortofDischarge] nvarchar(max) NULL,
        [LastDate] datetime NULL,
        [MethodName] nvarchar(max) NULL,
        [ConsignedCountry] nvarchar(max) NULL,
        [CountryofOrigin] nvarchar(max) NULL,
        [HSCode] nvarchar(max) NULL,
        [HSDescription] nvarchar(max) NULL,
        [Unit] nvarchar(max) NULL,
        [Price] decimal(38,6) NULL,
        [Quantity] decimal(38,6) NULL,
        [Amount] decimal(38,6) NULL,
        [Currency] nvarchar(max) NULL,
        [Conditions] nvarchar(max) NULL,
        [ApplicationNo] nvarchar(max) NULL,
        [ApplicationDate] datetime NULL,
        [FESCNo] nvarchar(max) NULL,
        [CommodityType] nvarchar(max) NULL,
        [ApproveDate] datetime NULL,
        [__rn] int IDENTITY(1,1) NOT NULL
    );

    INSERT INTO #r ([PaThaKaTypeId], [PaThaKaTypeCode], [PaThaKaTypeName], [ExportImportSectionId], [ExportImportMethodId], [ExportImportIncotermId], [SellerCountryId], [SectionCode], [SectionName], [LicenceNo], [LicenceDate], [CompanyRegistrationNo], [CompanyName], [UnitLevel], [StreetNumberStreetName], [QuarterCityTownship], [State], [Country], [PostalCode], [SellerName], [SellerAddress], [SellerCountry], [PortofDischarge], [LastDate], [MethodName], [ConsignedCountry], [CountryofOrigin], [HSCode], [HSDescription], [Unit], [Price], [Quantity], [Amount], [Currency], [Conditions], [ApplicationNo], [ApplicationDate], [FESCNo], [CommodityType], [ApproveDate])
    EXEC dbo.sp_ImportLicenceDetailReport @Type, @FromDate, @ToDate, @PaThaKaTypeId, @ExportImportSectionId, @ExportImportMethodId, @ExportImportIncotermId, @SellerCountryId, @CompanyRegistrationNo, @SakhanId;

    DECLARE @ps int = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 2147483647 ELSE @PageSize END;
    DECLARE @pi int = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 0 ELSE ISNULL(@PageIndex,0) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder,'ASC'))='DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @valid bit = 0;
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'PaThaKaTypeIdN','NPaThaKaTypeCodeN','NPaThaKaTypeNameN','NExportImportSectionIdN','NExportImportMethodIdN','NExportImportIncotermIdN','NSellerCountryIdN','NSectionCodeN','NSectionNameN','NLicenceNoN','NLicenceDateN','NCompanyRegistrationNoN','NCompanyNameN','NUnitLevelN','NStreetNumberStreetNameN','NQuarterCityTownshipN','NStateN','NCountryN','NPostalCodeN','NSellerNameN','NSellerAddressN','NSellerCountryN','NPortofDischargeN','NLastDateN','NMethodNameN','NConsignedCountryN','NCountryofOriginN','NHSCodeN','NHSDescriptionN','NUnitN','NPriceN','NQuantityN','NAmountN','NCurrencyN','NConditionsN','NApplicationNoN','NApplicationDateN','NFESCNoN','NCommodityTypeN','NApproveDate') SET @valid = 1;

    DECLARE @orderby nvarchar(300);
    IF @valid = 1 SET @orderby = N'[' + @SortColumn + N'] ' + @dir + N', [__rn] ASC';
    ELSE SET @orderby = N'[__rn] ASC';

    DECLARE @sql nvarchar(max) = N'SELECT [PaThaKaTypeId], [PaThaKaTypeCode], [PaThaKaTypeName], [ExportImportSectionId], [ExportImportMethodId], [ExportImportIncotermId], [SellerCountryId], [SectionCode], [SectionName], [LicenceNo], [LicenceDate], [CompanyRegistrationNo], [CompanyName], [UnitLevel], [StreetNumberStreetName], [QuarterCityTownship], [State], [Country], [PostalCode], [SellerName], [SellerAddress], [SellerCountry], [PortofDischarge], [LastDate], [MethodName], [ConsignedCountry], [CountryofOrigin], [HSCode], [HSDescription], [Unit], [Price], [Quantity], [Amount], [Currency], [Conditions], [ApplicationNo], [ApplicationDate], [FESCNo], [CommodityType], [ApproveDate], COUNT(*) OVER() AS TotalCount FROM #r ORDER BY ' + @orderby +
        N' OFFSET (@pi * @ps) ROWS FETCH NEXT @ps ROWS ONLY;';
    EXEC sp_executesql @sql, N'@pi int, @ps int', @pi=@pi, @ps=@ps;
END

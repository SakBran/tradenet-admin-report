CREATE OR ALTER PROCEDURE [dbo].[sp_CancelReport_pagination]
    @FormType nvarchar(50) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
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
        [Date] datetime NULL,
        [SectionCode] nvarchar(max) NULL,
        [SectionName] nvarchar(max) NULL,
        [OldLicenceNo] nvarchar(max) NULL,
        [LicenceNo] nvarchar(max) NULL,
        [sDate] nvarchar(max) NULL,
        [CompanyRegistrationNo] nvarchar(max) NULL,
        [CompanyName] nvarchar(max) NULL,
        [UnitLevel] nvarchar(max) NULL,
        [StreetNumberStreetName] nvarchar(max) NULL,
        [QuarterCityTownship] nvarchar(max) NULL,
        [State] nvarchar(max) NULL,
        [Country] nvarchar(max) NULL,
        [PostalCode] nvarchar(max) NULL,
        [Currency] nvarchar(max) NULL,
        [Amount] decimal(38,6) NULL,
        [Remark] nvarchar(max) NULL,
        [__rn] int IDENTITY(1,1) NOT NULL
    );

    INSERT INTO #r ([Date], [SectionCode], [SectionName], [OldLicenceNo], [LicenceNo], [sDate], [CompanyRegistrationNo], [CompanyName], [UnitLevel], [StreetNumberStreetName], [QuarterCityTownship], [State], [Country], [PostalCode], [Currency], [Amount], [Remark])
    EXEC dbo.sp_CancelReport @FormType, @FromDate, @ToDate, @ExportImportSectionId, @CompanyRegistrationNo, @SakhanId;

    DECLARE @ps int = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 2147483647 ELSE @PageSize END;
    DECLARE @pi int = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 0 ELSE ISNULL(@PageIndex,0) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder,'ASC'))='DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @valid bit = 0;
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'DateN','NSectionCodeN','NSectionNameN','NOldLicenceNoN','NLicenceNoN','NsDateN','NCompanyRegistrationNoN','NCompanyNameN','NUnitLevelN','NStreetNumberStreetNameN','NQuarterCityTownshipN','NStateN','NCountryN','NPostalCodeN','NCurrencyN','NAmountN','NRemark') SET @valid = 1;

    DECLARE @orderby nvarchar(300);
    IF @valid = 1 SET @orderby = N'[' + @SortColumn + N'] ' + @dir + N', [__rn] ASC';
    ELSE SET @orderby = N'[__rn] ASC';

    DECLARE @sql nvarchar(max) = N'SELECT [Date], [SectionCode], [SectionName], [OldLicenceNo], [LicenceNo], [sDate], [CompanyRegistrationNo], [CompanyName], [UnitLevel], [StreetNumberStreetName], [QuarterCityTownship], [State], [Country], [PostalCode], [Currency], [Amount], [Remark], COUNT(*) OVER() AS TotalCount FROM #r ORDER BY ' + @orderby +
        N' OFFSET (@pi * @ps) ROWS FETCH NEXT @ps ROWS ONLY;';
    EXEC sp_executesql @sql, N'@pi int, @ps int', @pi=@pi, @ps=@ps;
END

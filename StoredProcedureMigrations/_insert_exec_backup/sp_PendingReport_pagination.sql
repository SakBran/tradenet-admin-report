CREATE OR ALTER PROCEDURE [dbo].[sp_PendingReport_pagination]
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @FormType nvarchar(50) = N'',
    @ExportImportSectionId int = 0,
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #r (
        [Status] nvarchar(max) NULL,
        [ApplyType] nvarchar(max) NULL,
        [ApplicationDate] datetime NULL,
        [ApplicationNo] nvarchar(max) NULL,
        [SectionCode] nvarchar(max) NULL,
        [SectionName] nvarchar(max) NULL,
        [CompanyRegistrationNo] nvarchar(max) NULL,
        [CompanyName] nvarchar(max) NULL,
        [Currency] nvarchar(max) NULL,
        [AdditionalDescription] nvarchar(max) NULL,
        [Amount] decimal(38,6) NULL,
        [CommodityType] nvarchar(max) NULL,
        [HSCode] nvarchar(max) NULL,
        [__rn] int IDENTITY(1,1) NOT NULL
    );

    INSERT INTO #r ([Status], [ApplyType], [ApplicationDate], [ApplicationNo], [SectionCode], [SectionName], [CompanyRegistrationNo], [CompanyName], [Currency], [AdditionalDescription], [Amount], [CommodityType], [HSCode])
    EXEC dbo.sp_PendingReport @FromDate, @ToDate, @FormType, @ExportImportSectionId;

    DECLARE @ps int = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 2147483647 ELSE @PageSize END;
    DECLARE @pi int = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 0 ELSE ISNULL(@PageIndex,0) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder,'ASC'))='DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @valid bit = 0;
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'StatusN','NApplyTypeN','NApplicationDateN','NApplicationNoN','NSectionCodeN','NSectionNameN','NCompanyRegistrationNoN','NCompanyNameN','NCurrencyN','NAdditionalDescriptionN','NAmountN','NCommodityTypeN','NHSCode') SET @valid = 1;

    DECLARE @orderby nvarchar(300);
    IF @valid = 1 SET @orderby = N'[' + @SortColumn + N'] ' + @dir + N', [__rn] ASC';
    ELSE SET @orderby = N'[__rn] ASC';

    DECLARE @sql nvarchar(max) = N'SELECT [Status], [ApplyType], [ApplicationDate], [ApplicationNo], [SectionCode], [SectionName], [CompanyRegistrationNo], [CompanyName], [Currency], [AdditionalDescription], [Amount], [CommodityType], [HSCode], COUNT(*) OVER() AS TotalCount FROM #r ORDER BY ' + @orderby +
        N' OFFSET (@pi * @ps) ROWS FETCH NEXT @ps ROWS ONLY;';
    EXEC sp_executesql @sql, N'@pi int, @ps int', @pi=@pi, @ps=@ps;
END

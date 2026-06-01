CREATE OR ALTER PROCEDURE [dbo].[sp_HSCodeReport_pagination]
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @FormType nvarchar(50) = N'',
    @FilterType nvarchar(20) = N'',
    @HSCode nvarchar(50) = N'',
    @SakhanId int = 0,
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #r (
        [SectionCode] nvarchar(max) NULL,
        [HSCodeId] int NULL,
        [HSCode] nvarchar(max) NULL,
        [HSDescription] nvarchar(max) NULL,
        [Amount] decimal(38,6) NULL,
        [Currency] nvarchar(max) NULL,
        [LicenceNo] nvarchar(max) NULL,
        [CompanyRegistrationNo] nvarchar(max) NULL,
        [CompanyName] nvarchar(max) NULL,
        [__rn] int IDENTITY(1,1) NOT NULL
    );

    INSERT INTO #r ([SectionCode], [HSCodeId], [HSCode], [HSDescription], [Amount], [Currency], [LicenceNo], [CompanyRegistrationNo], [CompanyName])
    EXEC dbo.sp_HSCodeReport @FromDate, @ToDate, @FormType, @FilterType, @HSCode, @SakhanId, 0, 2147483647;

    DECLARE @ps int = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 2147483647 ELSE @PageSize END;
    DECLARE @pi int = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 0 ELSE ISNULL(@PageIndex,0) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder,'ASC'))='DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @valid bit = 0;
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'SectionCodeN','NHSCodeIdN','NHSCodeN','NHSDescriptionN','NAmountN','NCurrencyN','NLicenceNoN','NCompanyRegistrationNoN','NCompanyName') SET @valid = 1;

    DECLARE @orderby nvarchar(300);
    IF @valid = 1 SET @orderby = N'[' + @SortColumn + N'] ' + @dir + N', [__rn] ASC';
    ELSE SET @orderby = N'[__rn] ASC';

    DECLARE @sql nvarchar(max) = N'SELECT [SectionCode], [HSCodeId], [HSCode], [HSDescription], [Amount], [Currency], [LicenceNo], [CompanyRegistrationNo], [CompanyName], COUNT(*) OVER() AS TotalCount FROM #r ORDER BY ' + @orderby +
        N' OFFSET (@pi * @ps) ROWS FETCH NEXT @ps ROWS ONLY;';
    EXEC sp_executesql @sql, N'@pi int, @ps int', @pi=@pi, @ps=@ps;
END

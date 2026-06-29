CREATE OR ALTER PROCEDURE [dbo].[sp_ChequeNoDetailReport_pagination]
    @FromDate datetime,
    @ToDate datetime,
    @ChequeNoId int,
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL,
    @IncludeTotalCount bit = 1
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #Rows
    (
        TransactionId nvarchar(128) NULL,
        FormType nvarchar(256) NULL,
        ChequeNo nvarchar(256) NULL,
        SDate nvarchar(32) NULL,
        TransactionRefNo nvarchar(256) NULL,
        TransactionDateTime datetime NULL,
        CardNo nvarchar(256) NULL,
        PaThaKaNo nvarchar(256) NULL,
        CompanyName nvarchar(512) NULL,
        UnitLevel nvarchar(512) NULL,
        StreetNumberStreetName nvarchar(512) NULL,
        QuarterCityTownship nvarchar(512) NULL,
        [State] nvarchar(256) NULL,
        Country nvarchar(256) NULL,
        PostalCode nvarchar(64) NULL,
        Amount float NOT NULL
    );

    INSERT INTO #Rows
    EXEC dbo.sp_ChequeNoDetailReport @FromDate, @ToDate, @ChequeNoId;

    DECLARE @ps bigint = CASE
        WHEN ISNULL(@PageSize, 0) <= 0 THEN 9223372036854775807
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
        ELSE @PageSize END;
    DECLARE @off bigint = CASE WHEN ISNULL(@PageSize, 0) <= 0 THEN 0 ELSE ISNULL(@PageIndex, 0) * CAST(@PageSize AS bigint) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder, 'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL
       AND @SortColumn IN (N'TransactionId', N'FormType', N'ChequeNo', N'SDate', N'TransactionRefNo', N'TransactionDateTime', N'CardNo', N'PaThaKaNo', N'CompanyName', N'CompanyAddress', N'Amount')
        SET @ob = CASE
            WHEN @SortColumn = N'CompanyAddress' THEN
                N'CONCAT_WS('', '', NULLIF(UnitLevel, ''''), NULLIF(StreetNumberStreetName, ''''), NULLIF(QuarterCityTownship, ''''), NULLIF([State], ''''), NULLIF(Country, ''''), NULLIF(PostalCode, '''')) ' + @dir
            ELSE QUOTENAME(@SortColumn) + N' ' + @dir
        END + N', TransactionDateTime ASC, TransactionId ASC';
    ELSE
        SET @ob = N'TransactionDateTime ASC, TransactionId ASC';

    DECLARE @sql nvarchar(max) = N'
    SELECT
        TransactionId,
        FormType,
        ChequeNo,
        SDate,
        TransactionRefNo,
        TransactionDateTime,
        CardNo,
        PaThaKaNo,
        CompanyName,
        UnitLevel,
        StreetNumberStreetName,
        QuarterCityTownship,
        [State],
        Country,
        PostalCode,
        CONCAT_WS('', '', NULLIF(UnitLevel, ''''), NULLIF(StreetNumberStreetName, ''''), NULLIF(QuarterCityTownship, ''''), NULLIF([State], ''''), NULLIF(Country, ''''), NULLIF(PostalCode, '''')) AS CompanyAddress,
        Amount,
        CASE WHEN @IncludeTotalCount = 1 THEN COUNT(*) OVER() ELSE NULL END AS TotalCount
    FROM #Rows
    ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY;';

    EXEC sp_executesql
        @sql,
        N'@IncludeTotalCount bit, @off bigint, @ps bigint',
        @IncludeTotalCount = @IncludeTotalCount,
        @off = @off,
        @ps = @ps;
END

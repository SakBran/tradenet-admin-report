CREATE OR ALTER PROCEDURE dbo.sp_ExportLicenceTotalValueReport_Fast_pagination
    @Type nvarchar(20),
    @FromDate datetime,
    @ToDate datetime,
    @PaThaKaTypeId int,
    @ExportImportSectionId int,
    @ExportImportMethodId int,
    @ExportImportIncotermId int,
    @BuyerCountryId int,
    @CompanyRegistrationNo nvarchar(50),
    @SakhanId int,
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL,
    @IncludeTotalCount bit = 1
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

    IF @Type <> N'Oversea'
    BEGIN
        SELECT
            CAST(NULL AS nvarchar(50)) Currency,
            CAST(0 AS int) NoOfLicences,
            CAST(0 AS decimal(18, 4)) TotalValue,
            CAST(NULL AS int) TotalCount
        WHERE 1 = 0;
        RETURN;
    END;

    DECLARE @FromDateStart datetime = CONVERT(date, @FromDate);
    DECLARE @ToDateExclusive datetime = DATEADD(day, 1, CONVERT(date, @ToDate));

    CREATE TABLE #LicenceIds
    (
        Id char(36) NOT NULL PRIMARY KEY,
        ExportLicenceNo nvarchar(100) NULL
    );

    INSERT INTO #LicenceIds (Id, ExportLicenceNo)
    SELECT
        ExportLicence.Id,
        ExportLicence.ExportLicenceNo
    FROM dbo.ExportLicence AS ExportLicence
    INNER JOIN dbo.PaThaKa AS PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
    WHERE ExportLicence.ApplyType = N'New'
      AND ExportLicence.Status = N'Approved'
      AND ExportLicence.CreatedDate >= @FromDateStart
      AND ExportLicence.CreatedDate < @ToDateExclusive
      AND (@CompanyRegistrationNo = N'' OR PaThaKa.CompanyRegistrationNo = @CompanyRegistrationNo)
      AND (@PaThaKaTypeId = 0 OR PaThaKa.PaThaKaTypeId = @PaThaKaTypeId)
      AND (@ExportImportSectionId = 0 OR ExportLicence.ExportImportSectionId = @ExportImportSectionId)
      AND (@ExportImportMethodId = 0 OR ExportLicence.ExportImportMethodId = @ExportImportMethodId)
      AND (@ExportImportIncotermId = 0 OR ExportLicence.ExportImportIncotermId = @ExportImportIncotermId)
      AND (@BuyerCountryId = 0 OR ExportLicence.BuyerCountryId = @BuyerCountryId)
    OPTION (RECOMPILE);

    ;WITH Groups AS
    (
        SELECT
            currency.Code Currency,
            COUNT(DISTINCT NULLIF(licence.ExportLicenceNo, N'')) NoOfLicences,
            SUM(ExportLicenceItem.Amount) TotalValue
        FROM #LicenceIds licence
        INNER JOIN dbo.ExportLicenceItem AS ExportLicenceItem WITH (INDEX([IX_ExportLicenceItem_HSCodeReport_Licence]))
            ON licence.Id = ExportLicenceItem.ExportLicenceId
        INNER JOIN dbo.Currency AS currency ON ExportLicenceItem.CurrencyId = currency.Id
        GROUP BY currency.Code
    )
    SELECT
        Currency,
        NoOfLicences,
        TotalValue,
        CASE WHEN @IncludeTotalCount = 1 THEN COUNT(*) OVER() ELSE CAST(NULL AS int) END TotalCount
    FROM Groups
    ORDER BY Currency
    OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    OPTION (RECOMPILE, MAXDOP 1);
END

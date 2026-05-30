CREATE OR ALTER PROCEDURE [dbo].[sp_VoucherReport_pagination]
    @FormType nvarchar(50) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @PaymentType nvarchar(50) = N'',
    @ApplyType nvarchar(20) = N'',
    @CompanyRegistrationNo nvarchar(10) = N'',
    @SakhanId int = 0,
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #r (
        [ApplicationNo] nvarchar(max) NULL,
        [ApplicationDate] datetime NULL,
        [ApprovedUser] nvarchar(max) NULL,
        [Date] datetime NULL,
        [sDate] nvarchar(max) NULL,
        [SectionCode] nvarchar(max) NULL,
        [ApplyType] nvarchar(max) NULL,
        [OldLicenceNo] nvarchar(max) NULL,
        [LicenceNo] nvarchar(max) NULL,
        [LicenceDate] datetime NULL,
        [sLicenceDate] nvarchar(max) NULL,
        [CompanyRegistrationNo] nvarchar(max) NULL,
        [CompanyName] nvarchar(max) NULL,
        [VoucherNo] nvarchar(max) NULL,
        [VoucherDate] datetime NULL,
        [sVoucherDate] nvarchar(max) NULL,
        [Amount] int NULL,
        [PaymentType] nvarchar(max) NULL,
        [Currency] nvarchar(max) NULL,
        [TotalAmount] decimal(38,6) NULL,
        [CommodityType] nvarchar(max) NULL,
        [ExchangeRate] decimal(38,6) NULL,
        [TotalCIF] int NULL,
        [__rn] int IDENTITY(1,1) NOT NULL
    );

    INSERT INTO #r ([ApplicationNo], [ApplicationDate], [ApprovedUser], [Date], [sDate], [SectionCode], [ApplyType], [OldLicenceNo], [LicenceNo], [LicenceDate], [sLicenceDate], [CompanyRegistrationNo], [CompanyName], [VoucherNo], [VoucherDate], [sVoucherDate], [Amount], [PaymentType], [Currency], [TotalAmount], [CommodityType], [ExchangeRate], [TotalCIF])
    EXEC dbo.sp_VoucherReport @FormType, @FromDate, @ToDate, @ExportImportSectionId, @PaymentType, @ApplyType, @CompanyRegistrationNo, @SakhanId;

    DECLARE @ps int = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 2147483647 ELSE @PageSize END;
    DECLARE @pi int = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 0 ELSE ISNULL(@PageIndex,0) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder,'ASC'))='DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @valid bit = 0;
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'ApplicationNoN','NApplicationDateN','NApprovedUserN','NDateN','NsDateN','NSectionCodeN','NApplyTypeN','NOldLicenceNoN','NLicenceNoN','NLicenceDateN','NsLicenceDateN','NCompanyRegistrationNoN','NCompanyNameN','NVoucherNoN','NVoucherDateN','NsVoucherDateN','NAmountN','NPaymentTypeN','NCurrencyN','NTotalAmountN','NCommodityTypeN','NExchangeRateN','NTotalCIF') SET @valid = 1;

    DECLARE @orderby nvarchar(300);
    IF @valid = 1 SET @orderby = N'[' + @SortColumn + N'] ' + @dir + N', [__rn] ASC';
    ELSE SET @orderby = N'[__rn] ASC';

    DECLARE @sql nvarchar(max) = N'SELECT [ApplicationNo], [ApplicationDate], [ApprovedUser], [Date], [sDate], [SectionCode], [ApplyType], [OldLicenceNo], [LicenceNo], [LicenceDate], [sLicenceDate], [CompanyRegistrationNo], [CompanyName], [VoucherNo], [VoucherDate], [sVoucherDate], [Amount], [PaymentType], [Currency], [TotalAmount], [CommodityType], [ExchangeRate], [TotalCIF], COUNT(*) OVER() AS TotalCount FROM #r ORDER BY ' + @orderby +
        N' OFFSET (@pi * @ps) ROWS FETCH NEXT @ps ROWS ONLY;';
    EXEC sp_executesql @sql, N'@pi int, @ps int', @pi=@pi, @ps=@ps;
END

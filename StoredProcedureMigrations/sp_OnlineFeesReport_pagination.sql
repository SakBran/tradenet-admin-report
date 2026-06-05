CREATE OR ALTER PROCEDURE [dbo].[sp_OnlineFeesReport_pagination]
    @FromDate datetime,
    @ToDate datetime,
    @FormType nvarchar(50) = N'',
    @SakhanId int = 0,
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
        ELSE @PageSize END;
    DECLARE @off bigint = CASE WHEN ISNULL(@PageSize, 0) <= 0 THEN 0 ELSE ISNULL(@PageIndex, 0) * CAST(@PageSize AS bigint) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder, 'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'SakhanId', N'VoucherDate', N'CompanyRegistrationNo', N'CompanyName', N'FormType', N'Amount', N'Remark')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir
            + CASE WHEN @SortColumn = N'VoucherDate' THEN N'' ELSE N', [VoucherDate] ASC' END
            + CASE WHEN @SortColumn = N'FormType' THEN N'' ELSE N', [FormType] ASC' END
            + CASE WHEN @SortColumn = N'CompanyRegistrationNo' THEN N'' ELSE N', [CompanyRegistrationNo] ASC' END;
    ELSE
        SET @ob = N'[VoucherDate] ASC, [FormType] ASC, [CompanyRegistrationNo] ASC';

    CREATE TABLE #OnlineFeeRows
    (
        TransactionId char(36) NOT NULL,
        VoucherDate datetime NULL,
        FormType nvarchar(50) NOT NULL,
        Amount float NOT NULL
    );

    INSERT INTO #OnlineFeeRows (TransactionId, VoucherDate, FormType, Amount)
    SELECT AccountTransaction.TransactionId,
        AccountTransaction.VoucherDate,
        AccountTransaction.TransactionFormType,
        AccountTransactionDetail.Amount
    FROM AccountTransaction
    INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
    INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
    WHERE AccountTransaction.IsPayment = 1
        AND AccountTitle.FormType = N'Online Fees'
        AND AccountTransaction.VoucherDate >= @FromDate
        AND AccountTransaction.VoucherDate <= @ToDate;

    CREATE INDEX IX_OnlineFeeRows_TransactionId ON #OnlineFeeRows (TransactionId);

    DECLARE @sql nvarchar(max) = N'
    SELECT pg.*
    FROM (
        SELECT tmp.SakhanId,
            tmp.VoucherDate,
            tmp.CompanyRegistrationNo,
            tmp.CompanyName,
            tmp.FormType,
            tmp.Amount,
            tmp.Remark,
            CASE WHEN @IncludeTotalCount = 1 THEN COUNT(*) OVER() ELSE NULL END AS TotalCount
        FROM (
            SELECT 0 AS SakhanId, fee.VoucherDate, MemberRegistration.ApplicationNo AS CompanyRegistrationNo, '''' AS CompanyName,
                fee.FormType, fee.Amount, '''' AS Remark
            FROM #OnlineFeeRows fee
            INNER JOIN MemberRegistration ON fee.TransactionId = MemberRegistration.Id
            UNION ALL
            SELECT 0, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + BusinessServiceAgencyRegistration.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN BusinessServiceAgencyRegistration ON fee.TransactionId = BusinessServiceAgencyRegistration.Id
            INNER JOIN PaThaKa ON BusinessServiceAgencyRegistration.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT 0, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + DutyFreeShopRegistration.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN DutyFreeShopRegistration ON fee.TransactionId = DutyFreeShopRegistration.Id
            INNER JOIN PaThaKa ON DutyFreeShopRegistration.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT 0, fee.VoucherDate, PaThaKaRegistration.CompanyRegistrationNo + ''@'' + PaThaKaRegistration.ApplicationNo, PaThaKaRegistration.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN PaThaKaRegistration ON fee.TransactionId = PaThaKaRegistration.Id
            UNION ALL
            SELECT 0, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + ReExportRegistration.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN ReExportRegistration ON fee.TransactionId = ReExportRegistration.Id
            INNER JOIN PaThaKa ON ReExportRegistration.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT 0, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + SaleCenterRegistration.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN SaleCenterRegistration ON fee.TransactionId = SaleCenterRegistration.Id
            INNER JOIN PaThaKa ON SaleCenterRegistration.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT 0, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + ShowRoomRegistration.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN ShowRoomRegistration ON fee.TransactionId = ShowRoomRegistration.Id
            INNER JOIN PaThaKa ON ShowRoomRegistration.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT 0, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + WholeSaleRetailRegistration.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN WholeSaleRetailRegistration ON fee.TransactionId = WholeSaleRetailRegistration.Id
            INNER JOIN PaThaKa ON WholeSaleRetailRegistration.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT 0, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + WineImportationRegistration.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN WineImportationRegistration ON fee.TransactionId = WineImportationRegistration.Id
            INNER JOIN PaThaKa ON WineImportationRegistration.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT BorderExportLicence.SakhanId, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + BorderExportLicence.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN BorderExportLicence ON fee.TransactionId = BorderExportLicence.Id
            INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT BorderExportPermit.SakhanId, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + BorderExportPermit.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN BorderExportPermit ON fee.TransactionId = BorderExportPermit.Id
            INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT BorderImportLicence.SakhanId, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + BorderImportLicence.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN BorderImportLicence ON fee.TransactionId = BorderImportLicence.Id
            INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT BorderImportPermit.SakhanId, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + BorderImportPermit.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN BorderImportPermit ON fee.TransactionId = BorderImportPermit.Id
            INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT 0, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + ExportLicence.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN ExportLicence ON fee.TransactionId = ExportLicence.Id
            INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT 0, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + ExportPermit.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN ExportPermit ON fee.TransactionId = ExportPermit.Id
            INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT 0, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + ImportLicence.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN ImportLicence ON fee.TransactionId = ImportLicence.Id
            INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
            UNION ALL
            SELECT 0, fee.VoucherDate, PaThaKa.CompanyRegistrationNo + ''@'' + ImportPermit.ApplicationNo, PaThaKa.CompanyName,
                fee.FormType, fee.Amount, ''''
            FROM #OnlineFeeRows fee
            INNER JOIN ImportPermit ON fee.TransactionId = ImportPermit.Id
            INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
        ) tmp
        WHERE tmp.FormType LIKE (CASE WHEN @FormType = '''' THEN tmp.FormType + ''%'' ELSE @FormType + ''%'' END)
            AND tmp.SakhanId = (CASE WHEN @SakhanId = 0 THEN tmp.SakhanId ELSE @SakhanId END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';

    EXEC sp_executesql @sql,
        N'@FormType nvarchar(50), @SakhanId int, @IncludeTotalCount bit, @off bigint, @ps bigint',
        @FormType = @FormType,
        @SakhanId = @SakhanId,
        @IncludeTotalCount = @IncludeTotalCount,
        @off = @off,
        @ps = @ps;
END

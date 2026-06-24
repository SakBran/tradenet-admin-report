CREATE OR ALTER PROCEDURE [dbo].[sp_AccountSummaryReport_pagination]
    @FromDate datetime,
    @ToDate datetime,
    @FormType nvarchar(50),
    @SakhanId int,
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL,
    @IncludeTotalCount bit = 1
AS
BEGIN
    SET NOCOUNT ON;

    SET @FormType = LTRIM(RTRIM(ISNULL(@FormType, N'')));
    SET @SakhanId = ISNULL(@SakhanId, 0);

    CREATE TABLE #payments
    (
        Id nvarchar(36) NOT NULL,
        TransactionId nvarchar(36) NOT NULL,
        VoucherDate datetime NULL,
        PaymentDate datetime NULL,
        VoucherNo nvarchar(max) NULL,
        TransactionTitle nvarchar(max) NULL,
        Amount float NOT NULL,
        AccountTitleCode nvarchar(max) NULL,
        SortOrder int NOT NULL
    );

    INSERT INTO #payments
    (
        Id,
        TransactionId,
        VoucherDate,
        PaymentDate,
        VoucherNo,
        TransactionTitle,
        Amount,
        AccountTitleCode,
        SortOrder
    )
    SELECT
        AccountTransaction.Id,
        AccountTransaction.TransactionId,
        AccountTransaction.VoucherDate,
        AccountTransaction.PaymentDate,
        AccountTransaction.VoucherNo,
        AccountTitle.Description,
        AccountTransactionDetail.Amount,
        AccountTitle.Code,
        AccountTitle.SortOrder
    FROM dbo.AccountTransaction
    INNER JOIN dbo.AccountTransactionDetail
        ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
    INNER JOIN dbo.AccountTitle
        ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
    WHERE AccountTransaction.IsPayment = 1
        AND AccountTransaction.VoucherDate >= @FromDate
        AND AccountTransaction.VoucherDate <= @ToDate
        AND (@FormType = N'' OR AccountTransaction.TransactionFormType = @FormType)
    OPTION (RECOMPILE);

    CREATE INDEX IX_payments_TransactionId ON #payments(TransactionId);
    CREATE INDEX IX_payments_Order ON #payments(PaymentDate, SortOrder, Id) INCLUDE (TransactionId);

    CREATE TABLE #rows
    (
        Id nvarchar(36) NOT NULL,
        VoucherDate datetime NULL,
        PaymentDate datetime NULL,
        CompanyRegistrationNo nvarchar(max) NULL,
        VoucherNo nvarchar(max) NULL,
        CompanyName nvarchar(max) NULL,
        TransactionTitle nvarchar(max) NULL,
        Amount float NOT NULL,
        AccountTitleCode nvarchar(max) NULL,
        SortOrder int NOT NULL,
        SakhanId int NOT NULL,
        LocationCode nvarchar(max) NULL,
        FormType nvarchar(max) NULL
    );

    INSERT INTO #rows
    (
        Id,
        VoucherDate,
        PaymentDate,
        CompanyRegistrationNo,
        VoucherNo,
        CompanyName,
        TransactionTitle,
        Amount,
        AccountTitleCode,
        SortOrder,
        SakhanId,
        LocationCode,
        FormType
    )
    SELECT p.Id, p.VoucherDate, p.PaymentDate, N'', p.VoucherNo, N'', p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', N'Member'
    FROM #payments p
    INNER JOIN dbo.MemberRegistration ON p.TransactionId = MemberRegistration.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR @FormType = N'Member')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKaRegistration.CompanyRegistrationNo,
        p.VoucherNo, PaThaKaRegistration.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', N'Pa Tha Ka'
    FROM #payments p
    INNER JOIN dbo.PaThaKaRegistration ON p.TransactionId = PaThaKaRegistration.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR @FormType = N'Pa Tha Ka')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', N'Business Service Agency'
    FROM #payments p
    INNER JOIN dbo.BusinessServiceAgencyRegistration
        ON p.TransactionId = BusinessServiceAgencyRegistration.Id
    INNER JOIN dbo.PaThaKa ON BusinessServiceAgencyRegistration.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR @FormType = N'Business Service Agency')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', N'Duty Free Shop'
    FROM #payments p
    INNER JOIN dbo.DutyFreeShopRegistration ON p.TransactionId = DutyFreeShopRegistration.Id
    INNER JOIN dbo.PaThaKa ON DutyFreeShopRegistration.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR @FormType = N'Duty Free Shop')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', N'Re-Export'
    FROM #payments p
    INNER JOIN dbo.ReExportRegistration ON p.TransactionId = ReExportRegistration.Id
    INNER JOIN dbo.PaThaKa ON ReExportRegistration.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR @FormType = N'Re-Export')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', SaleCenterRegistration.RegistrationType
    FROM #payments p
    INNER JOIN dbo.SaleCenterRegistration ON p.TransactionId = SaleCenterRegistration.Id
    INNER JOIN dbo.PaThaKa ON SaleCenterRegistration.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR SaleCenterRegistration.RegistrationType = @FormType)

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', ShowRoomRegistration.RegistrationType
    FROM #payments p
    INNER JOIN dbo.ShowRoomRegistration ON p.TransactionId = ShowRoomRegistration.Id
    INNER JOIN dbo.PaThaKa ON ShowRoomRegistration.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR ShowRoomRegistration.RegistrationType = @FormType)

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', EVShowRoomRegistration.RegistrationType
    FROM #payments p
    INNER JOIN dbo.EVShowRoomRegistration ON p.TransactionId = EVShowRoomRegistration.Id
    INNER JOIN dbo.PaThaKa ON EVShowRoomRegistration.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR EVShowRoomRegistration.RegistrationType = @FormType)

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', EVCycleShowRoomRegistration.RegistrationType
    FROM #payments p
    INNER JOIN dbo.EVCycleShowRoomRegistration ON p.TransactionId = EVCycleShowRoomRegistration.Id
    INNER JOIN dbo.PaThaKa ON EVCycleShowRoomRegistration.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR EVCycleShowRoomRegistration.RegistrationType = @FormType)

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', WholeSaleRetailRegistration.RegistrationType
    FROM #payments p
    INNER JOIN dbo.WholeSaleRetailRegistration ON p.TransactionId = WholeSaleRetailRegistration.Id
    INNER JOIN dbo.PaThaKa ON WholeSaleRetailRegistration.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR WholeSaleRetailRegistration.RegistrationType = @FormType)

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', N'Wine Imporation'
    FROM #payments p
    INNER JOIN dbo.WineImportationRegistration ON p.TransactionId = WineImportationRegistration.Id
    INNER JOIN dbo.PaThaKa ON WineImportationRegistration.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR @FormType = N'Wine Imporation')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', N'Import Licence'
    FROM #payments p
    INNER JOIN dbo.DeleteData ON p.TransactionId = DeleteData.TransactionId
    INNER JOIN dbo.PaThaKa ON DeleteData.PaThaKaId = PaThaKa.Id
    WHERE DeleteData.SakhanId = 0
        AND (@SakhanId = 0)
        AND (@FormType = N'' OR @FormType = N'Import Licence')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, Sakhan.Id, Sakhan.Code, N'Border Export Licence'
    FROM #payments p
    INNER JOIN dbo.DeleteData ON p.TransactionId = DeleteData.TransactionId
    INNER JOIN dbo.Sakhan ON DeleteData.SakhanId = Sakhan.Id
    INNER JOIN dbo.PaThaKa ON DeleteData.PaThaKaId = PaThaKa.Id
    WHERE DeleteData.SakhanId <> 0
        AND (@SakhanId = 0 OR Sakhan.Id = @SakhanId)
        AND (@FormType = N'' OR @FormType = N'Border Export Licence')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', N'Export Licence'
    FROM #payments p
    INNER JOIN dbo.ExportLicence ON p.TransactionId = ExportLicence.Id
    INNER JOIN dbo.PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR @FormType = N'Export Licence')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', N'Import Licence'
    FROM #payments p
    INNER JOIN dbo.ImportLicence ON p.TransactionId = ImportLicence.Id
    INNER JOIN dbo.PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR @FormType = N'Import Licence')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', N'Export Permit'
    FROM #payments p
    INNER JOIN dbo.ExportPermit ON p.TransactionId = ExportPermit.Id
    INNER JOIN dbo.PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR @FormType = N'Export Permit')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, 0, N'NPT', N'Import Permit'
    FROM #payments p
    INNER JOIN dbo.ImportPermit ON p.TransactionId = ImportPermit.Id
    INNER JOIN dbo.PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0) AND (@FormType = N'' OR @FormType = N'Import Permit')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, Sakhan.Id, Sakhan.Code, N'Border Export Licence'
    FROM #payments p
    INNER JOIN dbo.BorderExportLicence ON p.TransactionId = BorderExportLicence.Id
    INNER JOIN dbo.Sakhan ON BorderExportLicence.SakhanId = Sakhan.Id
    INNER JOIN dbo.PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
    WHERE BorderExportLicence.CardType = N'Pa Tha Ka'
        AND (@SakhanId = 0 OR Sakhan.Id = @SakhanId)
        AND (@FormType = N'' OR @FormType = N'Border Export Licence')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, IndividualTrading.TINNo,
        p.VoucherNo, IndividualTrading.Name, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, Sakhan.Id, Sakhan.Code, N'Border Export Licence'
    FROM #payments p
    INNER JOIN dbo.BorderExportLicence ON p.TransactionId = BorderExportLicence.Id
    INNER JOIN dbo.Sakhan ON BorderExportLicence.SakhanId = Sakhan.Id
    INNER JOIN dbo.IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
    WHERE BorderExportLicence.CardType = N'Individual Trading'
        AND (@SakhanId = 0 OR Sakhan.Id = @SakhanId)
        AND (@FormType = N'' OR @FormType = N'Border Export Licence')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, Sakhan.Id, Sakhan.Code, N'Border Import Licence'
    FROM #payments p
    INNER JOIN dbo.BorderImportLicence ON p.TransactionId = BorderImportLicence.Id
    INNER JOIN dbo.Sakhan ON BorderImportLicence.SakhanId = Sakhan.Id
    INNER JOIN dbo.PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
    WHERE BorderImportLicence.CardType = N'Pa Tha Ka'
        AND (@SakhanId = 0 OR Sakhan.Id = @SakhanId)
        AND (@FormType = N'' OR @FormType = N'Border Import Licence')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, IndividualTrading.TINNo,
        p.VoucherNo, IndividualTrading.Name, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, Sakhan.Id, Sakhan.Code, N'Border Import Licence'
    FROM #payments p
    INNER JOIN dbo.BorderImportLicence ON p.TransactionId = BorderImportLicence.Id
    INNER JOIN dbo.Sakhan ON BorderImportLicence.SakhanId = Sakhan.Id
    INNER JOIN dbo.IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
    WHERE BorderImportLicence.CardType = N'Individual Trading'
        AND (@SakhanId = 0 OR Sakhan.Id = @SakhanId)
        AND (@FormType = N'' OR @FormType = N'Border Import Licence')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, Sakhan.Id, Sakhan.Code, N'Border Export Permit'
    FROM #payments p
    INNER JOIN dbo.BorderExportPermit ON p.TransactionId = BorderExportPermit.Id
    INNER JOIN dbo.Sakhan ON BorderExportPermit.SakhanId = Sakhan.Id
    INNER JOIN dbo.PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0 OR Sakhan.Id = @SakhanId)
        AND (@FormType = N'' OR @FormType = N'Border Export Permit')

    UNION ALL
    SELECT p.Id, p.VoucherDate, p.PaymentDate, PaThaKa.CompanyRegistrationNo,
        p.VoucherNo, PaThaKa.CompanyName, p.TransactionTitle,
        p.Amount, p.AccountTitleCode, p.SortOrder, Sakhan.Id, Sakhan.Code, N'Border Import Permit'
    FROM #payments p
    INNER JOIN dbo.BorderImportPermit ON p.TransactionId = BorderImportPermit.Id
    INNER JOIN dbo.Sakhan ON BorderImportPermit.SakhanId = Sakhan.Id
    INNER JOIN dbo.PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
    WHERE (@SakhanId = 0 OR Sakhan.Id = @SakhanId)
        AND (@FormType = N'' OR @FormType = N'Border Import Permit')
    OPTION (RECOMPILE);

    CREATE INDEX IX_rows_Order ON #rows(PaymentDate, SortOrder, Id);

    DECLARE @ps int = CASE
        WHEN ISNULL(@PageSize, 0) <= 0 THEN 2147483647
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
        ELSE @PageSize END;
    DECLARE @off int = CASE WHEN ISNULL(@PageSize, 0) <= 0 THEN 0 ELSE ISNULL(@PageIndex, 0) * @PageSize END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder, 'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL AND @SortColumn IN (
        N'Id', N'VoucherDate', N'PaymentDate', N'CompanyRegistrationNo', N'VoucherNo',
        N'CompanyName', N'TransactionTitle', N'Amount', N'AccountTitleCode',
        N'SortOrder', N'SakhanId', N'LocationCode', N'FormType')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir
            + CASE WHEN @SortColumn = N'PaymentDate' THEN N'' ELSE N', [PaymentDate] ASC' END
            + CASE WHEN @SortColumn = N'SortOrder' THEN N'' ELSE N', [SortOrder] ASC' END
            + CASE WHEN @SortColumn = N'Id' THEN N'' ELSE N', [Id] ASC' END;
    ELSE
        SET @ob = N'[PaymentDate] ASC, [SortOrder] ASC, [Id] ASC';

    DECLARE @total int = CASE WHEN @IncludeTotalCount = 1 THEN (SELECT COUNT(*) FROM #rows) ELSE NULL END;
    DECLARE @sql nvarchar(max) = N'
        SELECT
            Id,
            VoucherDate,
            PaymentDate,
            CompanyRegistrationNo,
            VoucherNo,
            CompanyName,
            TransactionTitle,
            Amount,
            AccountTitleCode,
            SortOrder,
            SakhanId,
            LocationCode,
            FormType,
            @total AS TotalCount
        FROM #rows
        ORDER BY ' + @ob + N'
        OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY;';

    EXEC sp_executesql @sql,
        N'@off int, @ps int, @total int',
        @off = @off,
        @ps = @ps,
        @total = @total;
END

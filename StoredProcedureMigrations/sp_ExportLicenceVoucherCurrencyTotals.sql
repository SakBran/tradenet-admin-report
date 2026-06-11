CREATE OR ALTER PROCEDURE [dbo].[sp_ExportLicenceVoucherCurrencyTotals]
    @FormType nvarchar(50) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @PaymentType nvarchar(20) = N'',
    @ApplyType nvarchar(20) = N'',
    @CompanyRegistrationNo nvarchar(50) = N'',
    @SakhanId int = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- Currency-grouped summary footer for the Export Licence / Border Export Licence Voucher
    -- reports: per licence currency, the count of payment vouchers + the summed licence value
    -- (Lic Value / TotalAmount), plus the grand total (added by the C# wrapper). This is the
    -- Licence twin of sp_ExportPermitVoucherCurrencyTotals: one row per payment voucher
    -- (AccountTransaction IsPayment = 1) so COUNT(*) matches the grid's TotalCount. The FROM/WHERE
    -- mirror sp_VoucherReport (ExportLicenceRows / BorderExportLicenceRows) -- PaymentDate range,
    -- PaymentType / section / company catch-all CASE predicates, ApplyType + Approved, the Users
    -- approver join -- so the footer lines up with the rows shown. Currency + licence value come
    -- from the licence items (TOP 1 item currency + SUM of item amounts), matching the grid's
    -- Currency / Total Amount (TotalAmount) columns. Border licences span the Pa Tha Ka
    -- (company = PaThaKa.CompanyRegistrationNo) and Individual Trading (company =
    -- IndividualTrading.TINNo) card types, both UNION ALL'd. @SakhanId only applies to the Border
    -- branch. OPTION (RECOMPILE) dodges the param-sniffing timeout.

    IF @FormType = N'Border Export Licence'
    BEGIN
        SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                (SELECT TOP 1 currency.Code FROM BorderExportLicenceItem
                    INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
                    WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id) AS Currency,
                (SELECT ISNULL(SUM(BorderExportLicenceItem.Amount), 0) FROM BorderExportLicenceItem
                    WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id) AS Amount
            FROM BorderExportLicence
                INNER JOIN AccountTransaction ON BorderExportLicence.Id = AccountTransaction.TransactionId
                INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
                INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
                INNER JOIN Users ON Users.Id = BorderExportLicence.ApproveUserId
            WHERE AccountTransaction.IsPayment = 1
                AND (AccountTransaction.PaymentDate >= @FromDate AND AccountTransaction.PaymentDate <= @ToDate)
                AND BorderExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND AccountTransaction.PaymentType = (CASE WHEN @PaymentType = '' THEN AccountTransaction.PaymentType ELSE @PaymentType END)
                AND BorderExportLicence.ApplyType = @ApplyType AND BorderExportLicence.Status = 'Approved' AND BorderExportLicence.CardType = 'Pa Tha Ka'
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                AND BorderExportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
            UNION ALL
            SELECT
                (SELECT TOP 1 currency.Code FROM BorderExportLicenceItem
                    INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
                    WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id) AS Currency,
                (SELECT ISNULL(SUM(BorderExportLicenceItem.Amount), 0) FROM BorderExportLicenceItem
                    WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id) AS Amount
            FROM BorderExportLicence
                INNER JOIN AccountTransaction ON BorderExportLicence.Id = AccountTransaction.TransactionId
                INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
                INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
                INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
                INNER JOIN Users ON Users.Id = BorderExportLicence.ApproveUserId
            WHERE AccountTransaction.IsPayment = 1
                AND (AccountTransaction.PaymentDate >= @FromDate AND AccountTransaction.PaymentDate <= @ToDate)
                AND BorderExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND AccountTransaction.PaymentType = (CASE WHEN @PaymentType = '' THEN AccountTransaction.PaymentType ELSE @PaymentType END)
                AND BorderExportLicence.ApplyType = @ApplyType AND BorderExportLicence.Status = 'Approved' AND BorderExportLicence.CardType = 'Individual Trading'
                AND IndividualTrading.TINNo = (CASE WHEN @CompanyRegistrationNo = '' THEN IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
                AND BorderExportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE
    BEGIN
        SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                (SELECT TOP 1 currency.Code FROM ExportLicenceItem
                    INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
                    WHERE ExportLicenceItem.ExportLicenceId = ExportLicence.Id) AS Currency,
                (SELECT ISNULL(SUM(ExportLicenceItem.Amount), 0) FROM ExportLicenceItem
                    WHERE ExportLicenceItem.ExportLicenceId = ExportLicence.Id) AS Amount
            FROM ExportLicence
                INNER JOIN AccountTransaction ON ExportLicence.Id = AccountTransaction.TransactionId
                INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
                INNER JOIN Users ON Users.Id = ExportLicence.ApproveUserId
            WHERE AccountTransaction.IsPayment = 1
                AND (AccountTransaction.PaymentDate >= @FromDate AND AccountTransaction.PaymentDate <= @ToDate)
                AND ExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND AccountTransaction.PaymentType = (CASE WHEN @PaymentType = '' THEN AccountTransaction.PaymentType ELSE @PaymentType END)
                AND ExportLicence.ApplyType = @ApplyType AND ExportLicence.Status = 'Approved'
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
END

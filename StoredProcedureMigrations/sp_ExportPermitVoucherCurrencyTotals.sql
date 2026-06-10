CREATE OR ALTER PROCEDURE [dbo].[sp_ExportPermitVoucherCurrencyTotals]
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

    -- Currency-grouped summary footer for the Export Permit / Border Export Permit Voucher
    -- reports: per licence currency, the count of payment vouchers + the summed licence value,
    -- plus the grand total (added by the C# wrapper). One row per payment voucher
    -- (AccountTransaction IsPayment = 1), so COUNT(*) matches the grid's TotalCount. The
    -- FROM/WHERE mirror sp_VoucherReport_pagination (Export Permit + Border Export Permit
    -- branches) -- PaymentDate range, PaymentType / section / company catch-all CASE predicates,
    -- ApplyType + Approved, the Users approver join -- so the footer lines up with the rows shown.
    -- Currency + licence value come from the permit's items (TOP 1 item currency + SUM of item
    -- amounts), matching the grid's Currency / Lic Value (TotalAmount) columns. @SakhanId is only
    -- applied for the Border branch. OPTION (RECOMPILE) dodges the param-sniffing timeout.

    IF @FormType = N'Border Export Permit'
    BEGIN
        SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                (SELECT TOP 1 currency.Code FROM BorderExportPermitItem
                    INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
                    WHERE BorderExportPermitItem.BorderExportPermitId = BorderExportPermit.Id) AS Currency,
                (SELECT ISNULL(SUM(BorderExportPermitItem.Amount), 0) FROM BorderExportPermitItem
                    WHERE BorderExportPermitItem.BorderExportPermitId = BorderExportPermit.Id) AS Amount
            FROM BorderExportPermit
                INNER JOIN AccountTransaction ON BorderExportPermit.Id = AccountTransaction.TransactionId
                INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
                INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
                INNER JOIN Users ON Users.Id = BorderExportPermit.ApproveUserId
            WHERE AccountTransaction.IsPayment = 1
                AND (AccountTransaction.PaymentDate >= @FromDate AND AccountTransaction.PaymentDate <= @ToDate)
                AND BorderExportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND AccountTransaction.PaymentType = (CASE WHEN @PaymentType = '' THEN AccountTransaction.PaymentType ELSE @PaymentType END)
                AND BorderExportPermit.ApplyType = @ApplyType AND BorderExportPermit.Status = 'Approved'
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                AND BorderExportPermit.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportPermit.SakhanId ELSE @SakhanId END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE
    BEGIN
        SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                (SELECT TOP 1 currency.Code FROM ExportPermitItem
                    INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
                    WHERE ExportPermitItem.ExportPermitId = ExportPermit.Id) AS Currency,
                (SELECT ISNULL(SUM(ExportPermitItem.Amount), 0) FROM ExportPermitItem
                    WHERE ExportPermitItem.ExportPermitId = ExportPermit.Id) AS Amount
            FROM ExportPermit
                INNER JOIN AccountTransaction ON ExportPermit.Id = AccountTransaction.TransactionId
                INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
                INNER JOIN Users ON Users.Id = ExportPermit.ApproveUserId
            WHERE AccountTransaction.IsPayment = 1
                AND (AccountTransaction.PaymentDate >= @FromDate AND AccountTransaction.PaymentDate <= @ToDate)
                AND ExportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND AccountTransaction.PaymentType = (CASE WHEN @PaymentType = '' THEN AccountTransaction.PaymentType ELSE @PaymentType END)
                AND ExportPermit.ApplyType = @ApplyType AND ExportPermit.Status = 'Approved'
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
END

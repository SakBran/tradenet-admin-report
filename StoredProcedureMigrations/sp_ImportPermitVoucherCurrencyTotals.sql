CREATE OR ALTER PROCEDURE [dbo].[sp_ImportPermitVoucherCurrencyTotals]
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @PaymentType nvarchar(20) = N'',
    @ApplyType nvarchar(20) = N'',
    @CompanyRegistrationNo nvarchar(50) = N''
AS
BEGIN
    SET NOCOUNT ON;

    -- Currency-grouped summary footer for the Import Permit Voucher report: per licence
    -- currency, the count of payment vouchers + the summed licence value, plus the grand
    -- total (added by the C# wrapper). One row per payment voucher (AccountTransaction
    -- IsPayment = 1), so COUNT(*) matches the grid's TotalCount. The FROM/WHERE mirror
    -- sp_VoucherReport_pagination (Import Permit branch) byte-for-byte -- PaymentDate range,
    -- PaymentType / section / company catch-all CASE predicates, ApplyType + Approved --
    -- so the footer lines up with the rows shown. Currency + licence value come from the
    -- permit's items (TOP 1 item currency + SUM of item amounts), matching the grid's
    -- Currency / Lic Value columns. OPTION (RECOMPILE) dodges the param-sniffing timeout.

    SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
    FROM (
        SELECT
            (SELECT TOP 1 currency.Code FROM ImportPermitItem
                INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
                WHERE ImportPermitItem.ImportPermitId = ImportPermit.Id) AS Currency,
            (SELECT ISNULL(SUM(ImportPermitItem.Amount), 0) FROM ImportPermitItem
                WHERE ImportPermitItem.ImportPermitId = ImportPermit.Id) AS Amount
        FROM ImportPermit
            INNER JOIN AccountTransaction ON ImportPermit.Id = AccountTransaction.TransactionId
            INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
            INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
            INNER JOIN Users ON Users.Id = ImportPermit.ApproveUserId
        WHERE AccountTransaction.IsPayment = 1
            AND (AccountTransaction.PaymentDate >= @FromDate AND AccountTransaction.PaymentDate <= @ToDate)
            AND ImportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
            AND AccountTransaction.PaymentType = (CASE WHEN @PaymentType = '' THEN AccountTransaction.PaymentType ELSE @PaymentType END)
            AND ImportPermit.ApplyType = @ApplyType AND ImportPermit.Status = 'Approved'
            AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
    ) d
    GROUP BY ISNULL(d.Currency, N'')
    OPTION (RECOMPILE);
END

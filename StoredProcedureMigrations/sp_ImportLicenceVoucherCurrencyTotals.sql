CREATE OR ALTER PROCEDURE [dbo].[sp_ImportLicenceVoucherCurrencyTotals]
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @PaymentType nvarchar(20) = N'',
    @ApplyType nvarchar(20) = N'',
    @CompanyRegistrationNo nvarchar(50) = N''
AS
BEGIN
    SET NOCOUNT ON;

    -- Currency-grouped summary footer for the Import Licence Voucher report: per licence
    -- currency, the count of payment vouchers + the summed licence value, plus the grand
    -- total (added by the C# wrapper). One row per payment voucher (AccountTransaction
    -- IsPayment = 1), so COUNT(*) matches the grid's TotalCount. The FROM/WHERE mirror the
    -- Import Licence (ELSE) branch of sp_VoucherReport_pagination -- PaymentDate range,
    -- PaymentType / section / company catch-all CASE predicates, ApplyType (strict, matching
    -- the grid) + Approved -- so the footer lines up with the rows shown. Currency + licence
    -- value come from the licence's items (TOP 1 item currency + SUM of item amounts),
    -- matching the grid's Currency / Lic Value columns.
    -- PERF: AccountTransaction has 2M+ payment rows across all document types; without the
    -- TransactionFormType discriminator the optimizer cold-scans the columnstore over a wide
    -- PaymentDate range (this proc is awaited synchronously before the grid response). The
    -- TransactionFormType = 'Import Licence' predicate + OPTION (RECOMPILE, LOOP JOIN) force a
    -- per-licence TransactionId seek -- identical rows (ImportLicence.Id is a GUID, so the join
    -- is unique and the discriminator drops nothing), ~0.2s. See sibling
    -- sp_ImportPermitVoucherCurrencyTotals.

    SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
    FROM (
        SELECT
            (SELECT TOP 1 currency.Code FROM ImportLicenceItem
                INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
                WHERE ImportLicenceItem.ImportLicenceId = ImportLicence.Id) AS Currency,
            (SELECT ISNULL(SUM(ImportLicenceItem.Amount), 0) FROM ImportLicenceItem
                WHERE ImportLicenceItem.ImportLicenceId = ImportLicence.Id) AS Amount
        FROM ImportLicence
            INNER JOIN AccountTransaction ON ImportLicence.Id = AccountTransaction.TransactionId
            INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
            INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
            INNER JOIN Users ON Users.Id = ImportLicence.ApproveUserId
        WHERE AccountTransaction.IsPayment = 1
            AND AccountTransaction.TransactionFormType = N'Import Licence'
            AND (AccountTransaction.PaymentDate >= @FromDate AND AccountTransaction.PaymentDate <= @ToDate)
            AND ImportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
            AND AccountTransaction.PaymentType = (CASE WHEN @PaymentType = '' THEN AccountTransaction.PaymentType ELSE @PaymentType END)
            AND ImportLicence.ApplyType = @ApplyType AND ImportLicence.Status = 'Approved'
            AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
    ) d
    GROUP BY ISNULL(d.Currency, N'')
    OPTION (RECOMPILE, LOOP JOIN);
END

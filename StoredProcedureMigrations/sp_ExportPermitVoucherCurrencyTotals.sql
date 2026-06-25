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
    -- applied for the Border branch.
    -- PERF: AccountTransaction has 2M+ payment rows shared across ALL document types. Without the
    -- TransactionFormType discriminator the optimizer scans the whole (columnstore) table over a wide
    -- PaymentDate range -- cold that scan took 30-56s and blocked the report (this proc is awaited
    -- synchronously before the grid response). The TransactionFormType = 'Export Permit' /
    -- 'Border Export Permit' predicate makes the AccountTransaction side selective, and OPTION
    -- (RECOMPILE, LOOP JOIN) forces a per-permit TransactionId seek instead of the cold scan -- proven
    -- to return identical rows in ~0.2s. (The Export Licence branch of sp_VoucherReport_pagination
    -- solves the same problem; its filtered index IX_AccountTransaction_ExportLicenceVoucher is scoped
    -- to 'Export Licence' so it cannot be reused here.)

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
                AND AccountTransaction.TransactionFormType = N'Border Export Permit'
                AND ((@FromDate IS NULL) OR AccountTransaction.PaymentDate >= @FromDate)
                AND ((@ToDate IS NULL) OR AccountTransaction.PaymentDate < DATEADD(day, 1, @ToDate))
                AND BorderExportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND AccountTransaction.PaymentType = (CASE WHEN @PaymentType = '' THEN AccountTransaction.PaymentType ELSE @PaymentType END)
                AND BorderExportPermit.ApplyType = @ApplyType AND BorderExportPermit.Status = 'Approved'
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                AND BorderExportPermit.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportPermit.SakhanId ELSE @SakhanId END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE, LOOP JOIN);
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
                AND AccountTransaction.TransactionFormType = N'Export Permit'
                AND (AccountTransaction.PaymentDate >= @FromDate AND AccountTransaction.PaymentDate <= @ToDate)
                AND ExportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND AccountTransaction.PaymentType = (CASE WHEN @PaymentType = '' THEN AccountTransaction.PaymentType ELSE @PaymentType END)
                AND ExportPermit.ApplyType = @ApplyType AND ExportPermit.Status = 'Approved'
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE, LOOP JOIN);
    END
END

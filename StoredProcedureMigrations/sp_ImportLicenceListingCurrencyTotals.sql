CREATE OR ALTER PROCEDURE [dbo].[sp_ImportLicenceListingCurrencyTotals]
    @ApplyType nvarchar(20) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @AmendRemarkId int = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- Currency-grouped summary footer for the Import Licence New / Amendment listing
    -- reports (legacy AmendReport.rdlc "Currency" group: per-currency licence count +
    -- summed value; the C# wrapper adds the grand "Total: N licence(s)" count).
    --
    -- The per-licence projection MUST line up with the grid the customer sees, which is
    -- driven by dbo.sp_AmendReport_pagination (and the untouched dbo.sp_AmendReport):
    --   Currency = (SELECT TOP 1 currency.Code ...)   -- first item's currency
    --   Amount   = (SELECT TOP 1 ISNULL(Amount,0) ...) -- first item's amount (NOT SUM)
    -- so the footer's per-currency "Total Value" equals the sum of the displayed
    -- "Total Value" column, and "No of License" equals the row count per currency
    -- (= CountDistinct(LicenceNo), one grid row per licence).
    --
    -- Date predicate uses < DATEADD(day, 1, @ToDate) to mirror sp_AmendReport_pagination
    -- (inclusive of the whole @ToDate day) so the footer count matches the grid TotalCount.
    --
    --   * New    -> sp_NewReport.ImportLicenceQuery (ApplyType='New'; New licences carry a
    --              NULL AmendRemarkId, so NO AmendRemarkId predicate is applied).
    --   * Amend  -> sp_AmendReport_pagination (ApplyType='Amend' + the AmendRemarkId CASE,
    --              date predicate < DATEADD(day, 1, @ToDate)).
    --   * Cancel -> sp_CancelReport_pagination (ApplyType='Cancel'; NO AmendRemarkId, and the
    --              cancel grid proc uses <= @ToDate, NOT DATEADD -- mirror it exactly).
    -- OPTION (RECOMPILE) avoids the parameter-sniffing timeout the catch-all CASE
    -- predicates cause (see the pagination-count-recompile-timeout note).

    IF @ApplyType = N'Amend'
    BEGIN
        SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                (SELECT TOP 1 currency.Code FROM ImportLicenceItem
                    INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
                    WHERE ImportLicenceItem.ImportLicenceId = ImportLicence.Id) AS Currency,
                (SELECT TOP 1 ISNULL(ImportLicenceItem.Amount, 0) FROM ImportLicenceItem
                    WHERE ImportLicenceItem.ImportLicenceId = ImportLicence.Id) AS Amount
            FROM ImportLicence
                INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
            WHERE ApplyType = 'Amend' AND ImportLicence.Status = 'Approved'
                AND (ImportLicence.CreatedDate >= @FromDate AND ImportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
                AND ImportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                AND ImportLicence.AmendRemarkId = (CASE WHEN @AmendRemarkId = 0 THEN ImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE IF @ApplyType = N'Cancel'
    BEGIN
        -- Cancellation footer -> sp_CancelReport_pagination (ApplyType='Cancel'). The cancel
        -- grid proc filters with <= @ToDate (NOT DATEADD) and has NO AmendRemarkId predicate,
        -- so mirror that here or the footer count diverges from the grid TotalCount.
        SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                (SELECT TOP 1 currency.Code FROM ImportLicenceItem
                    INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
                    WHERE ImportLicenceItem.ImportLicenceId = ImportLicence.Id) AS Currency,
                (SELECT TOP 1 ISNULL(ImportLicenceItem.Amount, 0) FROM ImportLicenceItem
                    WHERE ImportLicenceItem.ImportLicenceId = ImportLicence.Id) AS Amount
            FROM ImportLicence
                INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
            WHERE ApplyType = 'Cancel' AND ImportLicence.Status = 'Approved'
                AND (ImportLicence.CreatedDate >= @FromDate AND ImportLicence.CreatedDate <= @ToDate)
                AND ImportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE
    BEGIN
        SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                (SELECT TOP 1 currency.Code FROM ImportLicenceItem
                    INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
                    WHERE ImportLicenceItem.ImportLicenceId = ImportLicence.Id) AS Currency,
                (SELECT TOP 1 ISNULL(ImportLicenceItem.Amount, 0) FROM ImportLicenceItem
                    WHERE ImportLicenceItem.ImportLicenceId = ImportLicence.Id) AS Amount
            FROM ImportLicence
                INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
            WHERE ApplyType = 'New' AND ImportLicence.Status = 'Approved'
                AND (ImportLicence.CreatedDate >= @FromDate AND ImportLicence.CreatedDate < DATEADD(day, 1, @ToDate))
                AND ImportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
END

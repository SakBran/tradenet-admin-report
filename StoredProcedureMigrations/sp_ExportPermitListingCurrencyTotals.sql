CREATE OR ALTER PROCEDURE [dbo].[sp_ExportPermitListingCurrencyTotals]
    @FormType nvarchar(50) = N'',
    @ApplyType nvarchar(20) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @AmendRemarkId int = 0,
    @SakhanId int = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- Currency-grouped summary footer for the Export Permit / Border Export Permit
    -- New / Amendment / Actual Amendment / Cancellation listing reports (legacy RDLC
    -- "Currency" group: per-currency permit count + summed/first item value, plus the grand
    -- TOTAL licence count the C# wrapper adds). The per-permit projection and WHERE clauses are
    -- kept in step with the grid queries (sp_NewReport / sp_AmendReport / sp_ActualAmendReport /
    -- sp_CancelReport, Export Permit + Border Export Permit branches) so the footer always
    -- matches the rows shown:
    --   * New                 -> grid shows SUM(item.Amount); New permits carry a NULL
    --                            AmendRemarkId so NO AmendRemarkId predicate is applied.
    --   * Amend / ActualAmend -> grid shows the FIRST item's Amount (TOP 1 by item Id) + the
    --                            AmendRemarkId CASE (AmendRemarkId IS NOT NULL when
    --                            @AmendRemarkId = 0). ApplyType is matched via @ApplyType so the
    --                            same branch serves both the Amendment and Actual Amendment grids.
    --   * Cancel              -> grid shows the FIRST item's Amount (TOP 1 by item Id); the Cancel
    --                            grid has NO AmendRemarkId filter, so that predicate is dropped.
    -- @SakhanId is only applied for the Border Export Permit branch (non-border has no Sakhan).
    -- OPTION (RECOMPILE) avoids the parameter-sniffing timeout the catch-all CASE predicates
    -- cause (see the pagination-count-recompile-timeout note).

    IF @FormType = N'Border Export Permit'
    BEGIN
        IF @ApplyType = N'Amend' OR @ApplyType = N'ActualAmend'
        BEGIN
            SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
            FROM (
                SELECT
                    (SELECT TOP 1 currency.Code FROM BorderExportPermitItem
                        INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
                        WHERE BorderExportPermitItem.BorderExportPermitId = BorderExportPermit.Id) AS Currency,
                    (SELECT TOP 1 BorderExportPermitItem.Amount FROM BorderExportPermitItem
                        WHERE BorderExportPermitItem.BorderExportPermitId = BorderExportPermit.Id
                        ORDER BY BorderExportPermitItem.Id) AS Amount
                FROM BorderExportPermit
                    INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
                    INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
                WHERE BorderExportPermit.ApplyType = @ApplyType AND BorderExportPermit.Status = 'Approved'
                    AND ((@FromDate IS NULL) OR BorderExportPermit.CreatedDate >= @FromDate)
                    AND ((@ToDate IS NULL) OR BorderExportPermit.CreatedDate < DATEADD(day, 1, @ToDate))
                    AND BorderExportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND (CASE WHEN @AmendRemarkId = 0 THEN (CASE WHEN BorderExportPermit.AmendRemarkId IS NOT NULL THEN 1 ELSE 0 END) ELSE (CASE WHEN BorderExportPermit.AmendRemarkId = @AmendRemarkId THEN 1 ELSE 0 END) END) = 1
                    AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                    AND BorderExportPermit.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportPermit.SakhanId ELSE @SakhanId END)
            ) d
            GROUP BY ISNULL(d.Currency, N'')
            OPTION (RECOMPILE);
        END
        ELSE IF @ApplyType = N'Cancel'
        BEGIN
            SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
            FROM (
                SELECT
                    (SELECT TOP 1 currency.Code FROM BorderExportPermitItem
                        INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
                        WHERE BorderExportPermitItem.BorderExportPermitId = BorderExportPermit.Id) AS Currency,
                    (SELECT TOP 1 BorderExportPermitItem.Amount FROM BorderExportPermitItem
                        WHERE BorderExportPermitItem.BorderExportPermitId = BorderExportPermit.Id
                        ORDER BY BorderExportPermitItem.Id) AS Amount
                FROM BorderExportPermit
                    INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
                    INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
                WHERE BorderExportPermit.ApplyType = 'Cancel' AND BorderExportPermit.Status = 'Approved'
                    AND ((@FromDate IS NULL) OR BorderExportPermit.CreatedDate >= @FromDate)
                    AND ((@ToDate IS NULL) OR BorderExportPermit.CreatedDate < DATEADD(day, 1, @ToDate))
                    AND BorderExportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
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
                    (SELECT TOP 1 currency.Code FROM BorderExportPermitItem
                        INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
                        WHERE BorderExportPermitItem.BorderExportPermitId = BorderExportPermit.Id) AS Currency,
                    (SELECT ISNULL(SUM(BorderExportPermitItem.Amount), 0) FROM BorderExportPermitItem
                        WHERE BorderExportPermitItem.BorderExportPermitId = BorderExportPermit.Id) AS Amount
                FROM BorderExportPermit
                    INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
                    INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
                WHERE BorderExportPermit.ApplyType = 'New' AND BorderExportPermit.Status = 'Approved'
                    AND ((@FromDate IS NULL) OR BorderExportPermit.CreatedDate >= @FromDate)
                    AND ((@ToDate IS NULL) OR BorderExportPermit.CreatedDate < DATEADD(day, 1, @ToDate))
                    AND BorderExportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                    AND BorderExportPermit.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportPermit.SakhanId ELSE @SakhanId END)
            ) d
            GROUP BY ISNULL(d.Currency, N'')
            OPTION (RECOMPILE);
        END
    END
    ELSE
    BEGIN
        IF @ApplyType = N'Amend' OR @ApplyType = N'ActualAmend'
        BEGIN
            SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
            FROM (
                SELECT
                    (SELECT TOP 1 currency.Code FROM ExportPermitItem
                        INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
                        WHERE ExportPermitItem.ExportPermitId = ExportPermit.Id) AS Currency,
                    (SELECT TOP 1 ExportPermitItem.Amount FROM ExportPermitItem
                        WHERE ExportPermitItem.ExportPermitId = ExportPermit.Id
                        ORDER BY ExportPermitItem.Id) AS Amount
                FROM ExportPermit
                    INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
                WHERE ExportPermit.ApplyType = @ApplyType AND ExportPermit.Status = 'Approved'
                    AND (ExportPermit.CreatedDate >= @FromDate AND ExportPermit.CreatedDate <= @ToDate)
                    AND ExportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND (CASE WHEN @AmendRemarkId = 0 THEN (CASE WHEN ExportPermit.AmendRemarkId IS NOT NULL THEN 1 ELSE 0 END) ELSE (CASE WHEN ExportPermit.AmendRemarkId = @AmendRemarkId THEN 1 ELSE 0 END) END) = 1
                    AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
            ) d
            GROUP BY ISNULL(d.Currency, N'')
            OPTION (RECOMPILE);
        END
        ELSE IF @ApplyType = N'Cancel'
        BEGIN
            SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
            FROM (
                SELECT
                    (SELECT TOP 1 currency.Code FROM ExportPermitItem
                        INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
                        WHERE ExportPermitItem.ExportPermitId = ExportPermit.Id) AS Currency,
                    (SELECT TOP 1 ExportPermitItem.Amount FROM ExportPermitItem
                        WHERE ExportPermitItem.ExportPermitId = ExportPermit.Id
                        ORDER BY ExportPermitItem.Id) AS Amount
                FROM ExportPermit
                    INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
                WHERE ExportPermit.ApplyType = 'Cancel' AND ExportPermit.Status = 'Approved'
                    AND (ExportPermit.CreatedDate >= @FromDate AND ExportPermit.CreatedDate <= @ToDate)
                    AND ExportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
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
                    (SELECT TOP 1 currency.Code FROM ExportPermitItem
                        INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
                        WHERE ExportPermitItem.ExportPermitId = ExportPermit.Id) AS Currency,
                    (SELECT ISNULL(SUM(ExportPermitItem.Amount), 0) FROM ExportPermitItem
                        WHERE ExportPermitItem.ExportPermitId = ExportPermit.Id) AS Amount
                FROM ExportPermit
                    INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
                WHERE ExportPermit.ApplyType = 'New' AND ExportPermit.Status = 'Approved'
                    AND (ExportPermit.CreatedDate >= @FromDate AND ExportPermit.CreatedDate <= @ToDate)
                    AND ExportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
            ) d
            GROUP BY ISNULL(d.Currency, N'')
            OPTION (RECOMPILE);
        END
    END
END

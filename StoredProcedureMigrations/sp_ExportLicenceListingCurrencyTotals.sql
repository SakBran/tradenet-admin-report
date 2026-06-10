CREATE OR ALTER PROCEDURE [dbo].[sp_ExportLicenceListingCurrencyTotals]
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

    -- Currency-grouped summary footer for the Export Licence / Border Export Licence
    -- New / Amendment / Actual Amendment / Cancellation listing reports (legacy RDLC
    -- "Currency" group: per-currency licence count + summed/first item value, plus the grand
    -- TOTAL licence count the C# wrapper adds). This is the Licence twin of
    -- sp_ExportPermitListingCurrencyTotals: same ApplyType branching, but sourcing the
    -- ExportLicence / BorderExportLicence tables (and, for Border, the Pa Tha Ka + Individual
    -- Trading card-type split that sp_NewReport/sp_AmendReport/sp_ActualAmendReport/sp_CancelReport
    -- BorderExportLicenceQuery use). The per-licence projection and WHERE clauses are kept in step
    -- with those grids so the footer always matches the rows shown:
    --   * New                 -> grid shows SUM(item.Amount); New licences carry a NULL
    --                            AmendRemarkId so NO AmendRemarkId predicate is applied.
    --   * Amend / ActualAmend -> grid shows the FIRST item's Amount (TOP 1 by item Id) + the
    --                            AmendRemarkId CASE (AmendRemarkId IS NOT NULL when
    --                            @AmendRemarkId = 0). ApplyType is matched via @ApplyType so the
    --                            same branch serves both the Amendment and Actual Amendment grids.
    --   * Cancel              -> grid shows the FIRST item's Amount (TOP 1 by item Id); the Cancel
    --                            grid has NO AmendRemarkId filter, so that predicate is dropped.
    -- Border licences span two card types: Pa Tha Ka (company = PaThaKa.CompanyRegistrationNo) and
    -- Individual Trading (company = IndividualTrading.TINNo); both are UNION ALL'd. @SakhanId only
    -- applies to the Border branch (non-border Export Licence has no Sakhan). The literal must be
    -- 'Actual Amend' WITH a space. OPTION (RECOMPILE) avoids the parameter-sniffing timeout the
    -- catch-all CASE predicates cause (see the pagination-count-recompile-timeout note).

    IF @FormType = N'Border Export Licence'
    BEGIN
        IF @ApplyType = N'Amend' OR @ApplyType = N'Actual Amend'
        BEGIN
            SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
            FROM (
                SELECT
                    (SELECT TOP 1 currency.Code FROM BorderExportLicenceItem
                        INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
                        WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id) AS Currency,
                    (SELECT TOP 1 BorderExportLicenceItem.Amount FROM BorderExportLicenceItem
                        WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id
                        ORDER BY BorderExportLicenceItem.Id) AS Amount
                FROM BorderExportLicence
                    INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
                    INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
                WHERE BorderExportLicence.ApplyType = @ApplyType AND BorderExportLicence.Status = 'Approved' AND BorderExportLicence.CardType = 'Pa Tha Ka'
                    AND (BorderExportLicence.CreatedDate >= @FromDate AND BorderExportLicence.CreatedDate <= @ToDate)
                    AND BorderExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND (CASE WHEN @AmendRemarkId = 0 THEN (CASE WHEN BorderExportLicence.AmendRemarkId IS NOT NULL THEN 1 ELSE 0 END) ELSE (CASE WHEN BorderExportLicence.AmendRemarkId = @AmendRemarkId THEN 1 ELSE 0 END) END) = 1
                    AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                    AND BorderExportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
                UNION ALL
                SELECT
                    (SELECT TOP 1 currency.Code FROM BorderExportLicenceItem
                        INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
                        WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id) AS Currency,
                    (SELECT TOP 1 BorderExportLicenceItem.Amount FROM BorderExportLicenceItem
                        WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id
                        ORDER BY BorderExportLicenceItem.Id) AS Amount
                FROM BorderExportLicence
                    INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
                    INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
                    INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
                WHERE BorderExportLicence.ApplyType = @ApplyType AND BorderExportLicence.Status = 'Approved' AND BorderExportLicence.CardType = 'Individual Trading'
                    AND (BorderExportLicence.CreatedDate >= @FromDate AND BorderExportLicence.CreatedDate <= @ToDate)
                    AND BorderExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND (CASE WHEN @AmendRemarkId = 0 THEN (CASE WHEN BorderExportLicence.AmendRemarkId IS NOT NULL THEN 1 ELSE 0 END) ELSE (CASE WHEN BorderExportLicence.AmendRemarkId = @AmendRemarkId THEN 1 ELSE 0 END) END) = 1
                    AND IndividualTrading.TINNo = (CASE WHEN @CompanyRegistrationNo = '' THEN IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
                    AND BorderExportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
            ) d
            GROUP BY ISNULL(d.Currency, N'')
            OPTION (RECOMPILE);
        END
        ELSE IF @ApplyType = N'Cancel'
        BEGIN
            SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
            FROM (
                SELECT
                    (SELECT TOP 1 currency.Code FROM BorderExportLicenceItem
                        INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
                        WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id) AS Currency,
                    (SELECT TOP 1 BorderExportLicenceItem.Amount FROM BorderExportLicenceItem
                        WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id
                        ORDER BY BorderExportLicenceItem.Id) AS Amount
                FROM BorderExportLicence
                    INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
                    INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
                WHERE BorderExportLicence.ApplyType = 'Cancel' AND BorderExportLicence.Status = 'Approved' AND BorderExportLicence.CardType = 'Pa Tha Ka'
                    AND (BorderExportLicence.CreatedDate >= @FromDate AND BorderExportLicence.CreatedDate <= @ToDate)
                    AND BorderExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                    AND BorderExportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
                UNION ALL
                SELECT
                    (SELECT TOP 1 currency.Code FROM BorderExportLicenceItem
                        INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
                        WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id) AS Currency,
                    (SELECT TOP 1 BorderExportLicenceItem.Amount FROM BorderExportLicenceItem
                        WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id
                        ORDER BY BorderExportLicenceItem.Id) AS Amount
                FROM BorderExportLicence
                    INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
                    INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
                    INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
                WHERE BorderExportLicence.ApplyType = 'Cancel' AND BorderExportLicence.Status = 'Approved' AND BorderExportLicence.CardType = 'Individual Trading'
                    AND (BorderExportLicence.CreatedDate >= @FromDate AND BorderExportLicence.CreatedDate <= @ToDate)
                    AND BorderExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
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
                    (SELECT TOP 1 currency.Code FROM BorderExportLicenceItem
                        INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
                        WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id) AS Currency,
                    (SELECT ISNULL(SUM(BorderExportLicenceItem.Amount), 0) FROM BorderExportLicenceItem
                        WHERE BorderExportLicenceItem.BorderExportLicenceId = BorderExportLicence.Id) AS Amount
                FROM BorderExportLicence
                    INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
                    INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
                WHERE BorderExportLicence.ApplyType = 'New' AND BorderExportLicence.Status = 'Approved' AND BorderExportLicence.CardType = 'Pa Tha Ka'
                    AND (BorderExportLicence.CreatedDate >= @FromDate AND BorderExportLicence.CreatedDate <= @ToDate)
                    AND BorderExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
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
                    INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
                    INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
                    INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
                WHERE BorderExportLicence.ApplyType = 'New' AND BorderExportLicence.Status = 'Approved' AND BorderExportLicence.CardType = 'Individual Trading'
                    AND (BorderExportLicence.CreatedDate >= @FromDate AND BorderExportLicence.CreatedDate <= @ToDate)
                    AND BorderExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND IndividualTrading.TINNo = (CASE WHEN @CompanyRegistrationNo = '' THEN IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
                    AND BorderExportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
            ) d
            GROUP BY ISNULL(d.Currency, N'')
            OPTION (RECOMPILE);
        END
    END
    ELSE
    BEGIN
        IF @ApplyType = N'Amend' OR @ApplyType = N'Actual Amend'
        BEGIN
            SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
            FROM (
                SELECT
                    (SELECT TOP 1 currency.Code FROM ExportLicenceItem
                        INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
                        WHERE ExportLicenceItem.ExportLicenceId = ExportLicence.Id) AS Currency,
                    (SELECT TOP 1 ExportLicenceItem.Amount FROM ExportLicenceItem
                        WHERE ExportLicenceItem.ExportLicenceId = ExportLicence.Id
                        ORDER BY ExportLicenceItem.Id) AS Amount
                FROM ExportLicence
                    INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
                WHERE ExportLicence.ApplyType = @ApplyType AND ExportLicence.Status = 'Approved'
                    AND (ExportLicence.CreatedDate >= @FromDate AND ExportLicence.CreatedDate <= @ToDate)
                    AND ExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND (CASE WHEN @AmendRemarkId = 0 THEN (CASE WHEN ExportLicence.AmendRemarkId IS NOT NULL THEN 1 ELSE 0 END) ELSE (CASE WHEN ExportLicence.AmendRemarkId = @AmendRemarkId THEN 1 ELSE 0 END) END) = 1
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
                    (SELECT TOP 1 currency.Code FROM ExportLicenceItem
                        INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
                        WHERE ExportLicenceItem.ExportLicenceId = ExportLicence.Id) AS Currency,
                    (SELECT TOP 1 ExportLicenceItem.Amount FROM ExportLicenceItem
                        WHERE ExportLicenceItem.ExportLicenceId = ExportLicence.Id
                        ORDER BY ExportLicenceItem.Id) AS Amount
                FROM ExportLicence
                    INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
                WHERE ExportLicence.ApplyType = 'Cancel' AND ExportLicence.Status = 'Approved'
                    AND (ExportLicence.CreatedDate >= @FromDate AND ExportLicence.CreatedDate <= @ToDate)
                    AND ExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
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
                    (SELECT TOP 1 currency.Code FROM ExportLicenceItem
                        INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
                        WHERE ExportLicenceItem.ExportLicenceId = ExportLicence.Id) AS Currency,
                    (SELECT ISNULL(SUM(ExportLicenceItem.Amount), 0) FROM ExportLicenceItem
                        WHERE ExportLicenceItem.ExportLicenceId = ExportLicence.Id) AS Amount
                FROM ExportLicence
                    INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
                WHERE ExportLicence.ApplyType = 'New' AND ExportLicence.Status = 'Approved'
                    AND (ExportLicence.CreatedDate >= @FromDate AND ExportLicence.CreatedDate <= @ToDate)
                    AND ExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
            ) d
            GROUP BY ISNULL(d.Currency, N'')
            OPTION (RECOMPILE);
        END
    END
END

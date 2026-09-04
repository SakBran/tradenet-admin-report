CREATE OR ALTER PROCEDURE [dbo].[sp_ImportLicenceListingCurrencyTotals]
    @ApplyType nvarchar(20) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @AmendRemarkId int = 0,
    @auto nvarchar(50) = N'',
    @quota nvarchar(50) = N'',
    @FormType nvarchar(50) = N'',
    @SakhanId int = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- Currency-grouped summary footer for the Import Licence / Border Import Licence
    -- New / Amendment / Cancellation / Actual Amendment listing reports (legacy AmendReport.rdlc /
    -- BorderAmendReport.rdlc "Currency" group: per-currency licence count + summed value; the C#
    -- wrapper adds the grand "Total: N licence(s)" count).
    --
    -- The per-licence projection MUST line up with the grid the customer sees:
    --   Currency = (SELECT TOP 1 currency.Code ...)    -- first item's currency
    --   Amount   = (SELECT TOP 1 ISNULL(Amount,0) ...) -- first item's amount (NOT SUM), except New
    -- so the footer's per-currency "Total Value" equals the sum of the displayed "Total Value"
    -- column and "No of License" equals the row count per currency.
    --
    -- Date window: every branch mirrors its grid proc with CreatedDate >= @FromDate AND
    -- CreatedDate <= @ToDate. Callers pass @ToDate as '<day> 23:59:59'. Do NOT use
    -- '< DATEADD(day, 1, @ToDate)' -- it admits the whole next day (see sp_AmendReport_pagination).
    --
    --   * New         -> sp_NewReport_pagination (ApplyType='New'; New licences carry a NULL
    --                   AmendRemarkId, so NO AmendRemarkId predicate; @auto/@quota apply; the date
    --                   window is skipped when a reg-no is supplied, mirroring the grid).
    --   * Amend       -> sp_AmendReport_pagination (ApplyType='Amend' + the AmendRemarkId CASE).
    --   * Cancel      -> sp_CancelReport_pagination (ApplyType='Cancel'; NO AmendRemarkId).
    --   * ActualAmend -> sp_ActualAmendReport_pagination (ApplyType='Actual Amend' -- note the SPACE).
    --   * @FormType = 'Border Import Licence' -> the BorderImportLicence table, which spans two card
    --     types: Pa Tha Ka (company = PaThaKa.CompanyRegistrationNo) and Individual Trading
    --     (company = IndividualTrading.TINNo); both are UNION ALL'd, and @SakhanId applies.
    --     Border New / Cancel footers are not implemented and deliberately return an empty set
    --     rather than falling through to the non-border ImportLicence branches.
    --
    -- Callers name the Actual Amendment branch 'ActualAmend' while the database stores
    -- 'Actual Amend'; @DbApplyType normalises the two spellings so either works.
    -- OPTION (RECOMPILE) avoids the parameter-sniffing timeout the catch-all CASE predicates cause.

    DECLARE @DbApplyType nvarchar(20) = CASE WHEN @ApplyType = N'ActualAmend' THEN N'Actual Amend' ELSE @ApplyType END;

    IF @FormType = N'Border Import Licence'
    BEGIN
        IF @DbApplyType = N'Amend' OR @DbApplyType = N'Actual Amend'
        BEGIN
            -- Mirrors the 'Border Import Licence' branch of sp_AmendReport_pagination /
            -- sp_ActualAmendReport_pagination (Pa Tha Ka UNION ALL Individual Trading).
            SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
            FROM (
                SELECT
                    (SELECT TOP 1 currency.Code FROM BorderImportLicenceItem
                        INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
                        WHERE BorderImportLicenceItem.BorderImportLicenceId = BorderImportLicence.Id) AS Currency,
                    (SELECT TOP 1 ISNULL(BorderImportLicenceItem.Amount, 0) FROM BorderImportLicenceItem
                        WHERE BorderImportLicenceItem.BorderImportLicenceId = BorderImportLicence.Id) AS Amount
                FROM BorderImportLicence
                    INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
                    INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
                WHERE BorderImportLicence.ApplyType = @DbApplyType AND BorderImportLicence.Status = 'Approved' AND BorderImportLicence.CardType = 'Pa Tha Ka'
                    AND (BorderImportLicence.CreatedDate >= @FromDate AND BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
                    AND BorderImportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND BorderImportLicence.AmendRemarkId = (CASE WHEN @AmendRemarkId = 0 THEN BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
                    AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                    AND BorderImportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
                UNION ALL
                SELECT
                    (SELECT TOP 1 currency.Code FROM BorderImportLicenceItem
                        INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
                        WHERE BorderImportLicenceItem.BorderImportLicenceId = BorderImportLicence.Id) AS Currency,
                    (SELECT TOP 1 ISNULL(BorderImportLicenceItem.Amount, 0) FROM BorderImportLicenceItem
                        WHERE BorderImportLicenceItem.BorderImportLicenceId = BorderImportLicence.Id) AS Amount
                FROM BorderImportLicence
                    INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
                    INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
                    INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
                WHERE BorderImportLicence.ApplyType = @DbApplyType AND BorderImportLicence.Status = 'Approved' AND BorderImportLicence.CardType = 'Individual Trading'
                    AND (BorderImportLicence.CreatedDate >= @FromDate AND BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
                    AND BorderImportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND BorderImportLicence.AmendRemarkId = (CASE WHEN @AmendRemarkId = 0 THEN BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
                    AND IndividualTrading.TINNo = (CASE WHEN @CompanyRegistrationNo = '' THEN IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
                    AND BorderImportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
            ) d
            GROUP BY ISNULL(d.Currency, N'')
            OPTION (RECOMPILE);
        END
        ELSE
        BEGIN
            -- No Border New / Cancel footer: return an EMPTY correctly-shaped result set rather than
            -- falling through to a non-border branch (which would count the wrong table).
            SELECT CAST(N'' AS nvarchar(50)) AS Currency, CAST(0 AS int) AS NoOfLicences, CAST(0 AS decimal(18, 4)) AS TotalValue
            WHERE 1 = 0;
        END
    END
    ELSE IF @DbApplyType = N'Amend'
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
                AND (ImportLicence.CreatedDate >= @FromDate AND ImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
                AND ImportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                AND ImportLicence.AmendRemarkId = (CASE WHEN @AmendRemarkId = 0 THEN ImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE IF @DbApplyType = N'Cancel'
    BEGIN
        -- Cancellation footer -> sp_CancelReport_pagination (ApplyType='Cancel'). That grid has NO
        -- AmendRemarkId predicate, so it is dropped here too or the footer count diverges.
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
    ELSE IF @DbApplyType = N'Actual Amend'
    BEGIN
        -- Actual Amendment footer -> sp_ActualAmendReport_pagination Import Licence branch.
        -- Identical to the Amend branch except ApplyType = 'Actual Amend' (note the SPACE).
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
            WHERE ApplyType = 'Actual Amend' AND ImportLicence.Status = 'Approved'
                AND (ImportLicence.CreatedDate >= @FromDate AND ImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
                AND ImportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                AND ImportLicence.AmendRemarkId = (CASE WHEN @AmendRemarkId = 0 THEN ImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE
    BEGIN
        -- New listing footer -> sp_NewReport_pagination (ApplyType='New'). Two differences from the
        -- Amend / ActualAmend / Cancel branches above, both required to line up with the New grid:
        --   * Amount = (SELECT SUM(...)) -- the New grid sums ALL of a licence's items (NOT TOP 1).
        --   * The date window is skipped when @CompanyRegistrationNo is supplied, mirroring the New
        --     grid's reg-no date-skip, so "Total No of License" == the grid TotalCount in both modes.
        SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                (SELECT TOP 1 currency.Code FROM ImportLicenceItem
                    INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
                    WHERE ImportLicenceItem.ImportLicenceId = ImportLicence.Id) AS Currency,
                (SELECT ISNULL(SUM(ImportLicenceItem.Amount), 0) FROM ImportLicenceItem
                    WHERE ImportLicenceItem.ImportLicenceId = ImportLicence.Id) AS Amount
            FROM ImportLicence
                INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
            WHERE ApplyType = 'New' AND ImportLicence.Status = 'Approved'
                AND (@CompanyRegistrationNo <> '' OR (ImportLicence.CreatedDate >= @FromDate AND ImportLicence.CreatedDate <= @ToDate))
                AND ImportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                AND (@auto = '' OR ImportLicence.auto = @auto)
                AND (@quota = '' OR ImportLicence.quota = @quota)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
END

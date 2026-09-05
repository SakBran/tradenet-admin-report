CREATE OR ALTER PROCEDURE [dbo].[sp_ImportPermitListingCurrencyTotals]
    @ApplyType nvarchar(20) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @AmendRemarkId int = 0,
    @FormType nvarchar(50) = N'',
    @SakhanId int = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- Currency-grouped summary footer for the Import Permit / Border Import Permit New / Amendment /
    -- Actual Amendment listing reports (per-currency permit count + item amount; the C# wrapper adds
    -- the grand TOTAL count). The per-permit projection and WHERE clauses are kept in step with the
    -- grid queries so the footer always matches the rows shown:
    --   * New         -> sp_NewReport.ImportPermitQuery (ApplyType='New'; New permits carry a NULL
    --                   AmendRemarkId, so NO AmendRemarkId predicate is applied).
    --   * Amend       -> sp_AmendReport_pagination (ApplyType='Amend' + the AmendRemarkId CASE).
    --   * ActualAmend -> sp_ActualAmendReport_pagination Import Permit branch (ApplyType='Actual Amend'
    --                   -- note the SPACE -- + the AmendRemarkId CASE). That grid shows the FIRST
    --                   item's amount, so this branch uses TOP 1, NOT the SUM the Amend branch uses.
    --   * Cancel      -> sp_CancelReport.ImportPermitQuery (ApplyType='Cancel'). Also a FIRST-item
    --                   grid, so TOP 1 again; no AmendRemarkId predicate.
    --   * @FormType = 'Border Import Permit' -> the BorderImportPermit table (Pa Tha Ka only, plus the
    --                   Sakhan join/filter). Border New footers are not implemented and return an
    --                   empty set rather than falling through to the non-border branches.
    --
    -- Callers name the Actual Amendment branch 'ActualAmend' while the database stores
    -- 'Actual Amend'; @DbApplyType normalises the two spellings so either works.
    -- Date window: CreatedDate >= @FromDate AND CreatedDate <= @ToDate, mirroring the grids; callers
    -- pass @ToDate as '<day> 23:59:59'. OPTION (RECOMPILE) avoids the parameter-sniffing timeout the
    -- catch-all CASE predicates cause.
    --
    -- KNOWN (pre-existing, unchanged): the Amend branch sums ALL of a permit's items while its grid
    -- shows the first item only; tracked as a separate parity follow-up.

    DECLARE @DbApplyType nvarchar(20) = CASE WHEN @ApplyType = N'ActualAmend' THEN N'Actual Amend' ELSE @ApplyType END;

    IF @FormType = N'Border Import Permit'
    BEGIN
        IF @DbApplyType = N'Amend' OR @DbApplyType = N'Actual Amend'
        BEGIN
            -- Mirrors the 'Border Import Permit' branch of sp_AmendReport_pagination /
            -- sp_ActualAmendReport_pagination (Pa Tha Ka card type only, Sakhan join + filter).
            SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
            FROM (
                SELECT
                    (SELECT TOP 1 currency.Code FROM BorderImportPermitItem
                        INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
                        WHERE BorderImportPermitItem.BorderImportPermitId = BorderImportPermit.Id) AS Currency,
                    (SELECT TOP 1 ISNULL(BorderImportPermitItem.Amount, 0) FROM BorderImportPermitItem
                        WHERE BorderImportPermitItem.BorderImportPermitId = BorderImportPermit.Id) AS Amount
                FROM BorderImportPermit
                    INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
                    INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
                WHERE BorderImportPermit.ApplyType = @DbApplyType AND BorderImportPermit.Status = 'Approved'
                    AND (BorderImportPermit.CreatedDate >= @FromDate AND BorderImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
                    AND BorderImportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND BorderImportPermit.AmendRemarkId = (CASE WHEN @AmendRemarkId = 0 THEN BorderImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
                    AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                    AND BorderImportPermit.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderImportPermit.SakhanId ELSE @SakhanId END)
            ) d
            GROUP BY ISNULL(d.Currency, N'')
            OPTION (RECOMPILE);
        END
        ELSE
        BEGIN
            -- No Border New footer: return an EMPTY correctly-shaped result set rather than falling
            -- through to a non-border branch (which would count the wrong table).
            SELECT CAST(N'' AS nvarchar(50)) AS Currency, CAST(0 AS int) AS NoOfLicences, CAST(0 AS decimal(18, 4)) AS TotalValue
            WHERE 1 = 0;
        END
    END
    ELSE IF @DbApplyType = N'Actual Amend'
    BEGIN
        -- Mirrors the 'Import Permit' branch of sp_ActualAmendReport_pagination: TOP 1 item amount
        -- (NOT the SUM used by the Amend branch below) so the footer equals the displayed column.
        SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                (SELECT TOP 1 currency.Code FROM ImportPermitItem
                    INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
                    WHERE ImportPermitItem.ImportPermitId = ImportPermit.Id) AS Currency,
                (SELECT TOP 1 ISNULL(ImportPermitItem.Amount, 0) FROM ImportPermitItem
                    WHERE ImportPermitItem.ImportPermitId = ImportPermit.Id) AS Amount
            FROM ImportPermit
                INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
            WHERE ImportPermit.ApplyType = 'Actual Amend' AND ImportPermit.Status = 'Approved'
                AND (ImportPermit.CreatedDate >= @FromDate AND ImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
                AND ImportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND ImportPermit.AmendRemarkId = (CASE WHEN @AmendRemarkId = 0 THEN ImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE IF @DbApplyType = N'Cancel'
    BEGIN
        -- Cancellation footer (CancelReport.rdlc Tablix2): per currency,
        -- "<CUR>:CountDistinct(LicenceNo) licence(s)" (:1557) and
        -- "<CUR>:FORMAT(Sum(Amount),'N4')" (:1611). The grid shows the FIRST item's amount
        -- (sp_CancelReport.ImportPermitQuery takes MIN(ImportPermitItem.Id)), so this branch
        -- uses TOP 1 like the ActualAmend branch, not the Amend branch's SUM. The count is
        -- COUNT(DISTINCT ImportPermitNo) because the rdlc aggregate is CountDistinct, not Count.
        SELECT ISNULL(d.Currency, N'') AS Currency,
               COUNT(DISTINCT d.LicenceNo) AS NoOfLicences,
               ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                ImportPermit.ImportPermitNo AS LicenceNo,
                (SELECT TOP 1 currency.Code FROM ImportPermitItem
                    INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
                    WHERE ImportPermitItem.ImportPermitId = ImportPermit.Id
                    ORDER BY ImportPermitItem.Id) AS Currency,
                (SELECT TOP 1 ISNULL(ImportPermitItem.Amount, 0) FROM ImportPermitItem
                    WHERE ImportPermitItem.ImportPermitId = ImportPermit.Id
                    ORDER BY ImportPermitItem.Id) AS Amount
            FROM ImportPermit
                INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
            WHERE ImportPermit.ApplyType = 'Cancel' AND ImportPermit.Status = 'Approved'
                AND (ImportPermit.CreatedDate >= @FromDate AND ImportPermit.CreatedDate <= @ToDate)
                AND ImportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE IF @DbApplyType = N'Amend'
    BEGIN
        SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                (SELECT TOP 1 currency.Code FROM ImportPermitItem
                    INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
                    WHERE ImportPermitItem.ImportPermitId = ImportPermit.Id) AS Currency,
                (SELECT ISNULL(SUM(ImportPermitItem.Amount), 0) FROM ImportPermitItem
                    WHERE ImportPermitItem.ImportPermitId = ImportPermit.Id) AS Amount
            FROM ImportPermit
                INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
            WHERE ApplyType = 'Amend' AND ImportPermit.Status = 'Approved'
                AND (ImportPermit.CreatedDate >= @FromDate AND ImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
                AND ImportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                AND ImportPermit.AmendRemarkId = (CASE WHEN @AmendRemarkId = 0 THEN ImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE
    BEGIN
        SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                (SELECT TOP 1 currency.Code FROM ImportPermitItem
                    INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
                    WHERE ImportPermitItem.ImportPermitId = ImportPermit.Id) AS Currency,
                (SELECT ISNULL(SUM(ImportPermitItem.Amount), 0) FROM ImportPermitItem
                    WHERE ImportPermitItem.ImportPermitId = ImportPermit.Id) AS Amount
            FROM ImportPermit
                INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
            WHERE ApplyType = 'New' AND ImportPermit.Status = 'Approved'
                AND (ImportPermit.CreatedDate >= @FromDate AND ImportPermit.CreatedDate <= @ToDate)
                AND ImportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
END

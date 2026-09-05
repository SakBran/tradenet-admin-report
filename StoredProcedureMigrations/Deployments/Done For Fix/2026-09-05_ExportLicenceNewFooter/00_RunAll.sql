/* =====================================================================================
   Export Licence New Report currency-footer deployment - 2026-09-05
   One procedure. Run this file, or 01_sp_ExportLicenceListingCurrencyTotals.sql - they
   are the same definition. PROCEDURE FIRST, APPLICATION SECOND.

   Target database: TradeNetDB  (NOT ReportTemplateDB - that one only holds the Excel
   export job queue; deploying report procedures into it is a known trap.)

   Why: the Export Licence New Report showed no Total footer in the UI or in Excel
   (customer complaint, 2026-09). The footer branch already existed here but was never
   called, and it did not mirror its grid. Two changes:

     * new trailing parameter @auto (the New reports' Auto / None-Auto dropdown), applied
       in the non-border New branch as "(@auto='' OR ExportLicence.auto=@auto)" and in the
       border New branch as the CASE form - each copied from that branch's own grid in
       sp_NewReport_pagination, because the two forms differ on NULL auto;
     * the non-border New branch's date window moved from "CreatedDate <= @ToDate" to
       "< DATEADD(day, 1, CONVERT(date, @ToDate))", which is what its grid uses.

   The parameter is appended LAST and defaults to N'', so the currently deployed 8-argument
   callers (Amendment / Actual Amendment / Cancellation) keep working against this version
   unchanged. Deploy this BEFORE the application: the new backend passes 9 arguments for the
   New report, and against the old 8-parameter procedure that raises Error 8144, which the
   C# wrapper swallows into an empty footer.

   Generated from the repository file of the same name; see README.md in this folder.
   ===================================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE [TradeNetDB];
GO

-- Wrong-database guard: dbo.sp_CancelReport is the legacy Tradenet 2.0 procedure and
-- exists only in the report database. Stop before creating anything in the wrong place.
IF OBJECT_ID(N'dbo.sp_CancelReport', N'P') IS NULL
BEGIN
    RAISERROR(N'Wrong database: dbo.sp_CancelReport was not found in [%s]. Connect to TradeNetDB and run again.', 16, 1, DB_NAME());
    SET NOEXEC ON;
END
GO

-- ============================================================================
-- sp_ExportLicenceListingCurrencyTotals
--   (file 01_sp_ExportLicenceListingCurrencyTotals.sql)
-- ============================================================================
PRINT N'Applying sp_ExportLicenceListingCurrencyTotals ...';
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_ExportLicenceListingCurrencyTotals]
    @FormType nvarchar(50) = N'',
    @ApplyType nvarchar(20) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @AmendRemarkId int = 0,
    @SakhanId int = 0,
    @auto nvarchar(50) = N''
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
    --                            AmendRemarkId so NO AmendRemarkId predicate is applied. Both New
    --                            grids also expose the Auto / None-Auto dropdown, so @auto is
    --                            applied here in each grid's OWN form: the non-border grid uses
    --                            "(@auto='' OR auto=@auto)" (keeps NULL auto when unfiltered) while
    --                            the border grid uses the CASE form (drops NULL auto). Mirroring
    --                            them exactly is what keeps the footer count equal to the grid's.
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
                    AND (BorderExportLicence.CreatedDate >= @FromDate AND BorderExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
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
                    AND (BorderExportLicence.CreatedDate >= @FromDate AND BorderExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
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
                    AND BorderExportLicence.auto = (CASE WHEN @auto = N'' THEN BorderExportLicence.auto ELSE @auto END)
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
                    AND BorderExportLicence.auto = (CASE WHEN @auto = N'' THEN BorderExportLicence.auto ELSE @auto END)
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
                    AND (ExportLicence.CreatedDate >= @FromDate AND ExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
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
                    AND (ExportLicence.CreatedDate >= @FromDate AND ExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
                    AND ExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                    AND (@auto = N'' OR ExportLicence.auto = @auto)
            ) d
            GROUP BY ISNULL(d.Currency, N'')
            OPTION (RECOMPILE);
        END
    END
END

GO

SET NOEXEC OFF;
GO
PRINT N'Done.';
GO

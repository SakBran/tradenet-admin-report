CREATE OR ALTER PROCEDURE [dbo].[sp_ExtensionReportCurrencyTotals]
    @FormType nvarchar(50) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @SakhanId int = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- Currency-grouped summary footer for the Extension reports (legacy RDLC
    -- ExtensionReport.rdlc / BorderExtensionReport.rdlc "Currency" group footer:
    -- per-currency CountDistinct(LicenceNo) + Sum(Amount), then a grand TOTAL).
    --
    -- The per-licence projection (TOP 1 item currency, SUM of item amounts) and the
    -- WHERE clauses are kept byte-for-byte in step with sp_ExtensionReport_pagination
    -- so these totals always match the rows shown in the grid. OPTION (RECOMPILE)
    -- avoids the parameter-sniffing timeout the catch-all CASE predicates cause.

    IF @FormType = N'Import Permit'
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
            WHERE ApplyType = 'Extension' AND ImportPermit.Status = 'Approved'
                AND ((@FromDate IS NULL OR ImportPermit.CreatedDate >= @FromDate) AND (@ToDate IS NULL OR ImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))))
                AND ImportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE IF @FormType = N'Export Permit'
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
            WHERE ApplyType = 'Extension' AND ExportPermit.Status = 'Approved'
                AND ((@FromDate IS NULL OR ExportPermit.CreatedDate >= @FromDate) AND (@ToDate IS NULL OR ExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))))
                AND ExportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE IF @FormType = N'Export Licence'
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
            WHERE ApplyType = 'Extension' AND ExportLicence.Status = 'Approved'
                AND ((@FromDate IS NULL OR ExportLicence.CreatedDate >= @FromDate) AND (@ToDate IS NULL OR ExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))))
                AND ExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE IF @FormType = N'Border Export Licence'
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
            WHERE ApplyType = 'Extension' AND BorderExportLicence.Status = 'Approved' AND CardType = 'Pa Tha Ka'
                AND ((@FromDate IS NULL OR BorderExportLicence.CreatedDate >= @FromDate) AND (@ToDate IS NULL OR BorderExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))))
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
            WHERE ApplyType = 'Extension' AND BorderExportLicence.Status = 'Approved' AND CardType = 'Individual Trading'
                AND ((@FromDate IS NULL OR BorderExportLicence.CreatedDate >= @FromDate) AND (@ToDate IS NULL OR BorderExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))))
                AND BorderExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND IndividualTrading.TINNo = (CASE WHEN @CompanyRegistrationNo = '' THEN IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
                AND BorderExportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE IF @FormType = N'Border Import Licence'
    BEGIN
        SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                (SELECT TOP 1 currency.Code FROM BorderImportLicenceItem
                    INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
                    WHERE BorderImportLicenceItem.BorderImportLicenceId = BorderImportLicence.Id) AS Currency,
                (SELECT ISNULL(SUM(BorderImportLicenceItem.Amount), 0) FROM BorderImportLicenceItem
                    WHERE BorderImportLicenceItem.BorderImportLicenceId = BorderImportLicence.Id) AS Amount
            FROM BorderImportLicence
                INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
                INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
            WHERE ApplyType = 'Extension' AND BorderImportLicence.Status = 'Approved' AND CardType = 'Pa Tha Ka'
                AND ((@FromDate IS NULL OR BorderImportLicence.CreatedDate >= @FromDate) AND (@ToDate IS NULL OR BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))))
                AND BorderImportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                AND BorderImportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
            UNION ALL
            SELECT
                (SELECT TOP 1 currency.Code FROM BorderImportLicenceItem
                    INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
                    WHERE BorderImportLicenceItem.BorderImportLicenceId = BorderImportLicence.Id) AS Currency,
                (SELECT ISNULL(SUM(BorderImportLicenceItem.Amount), 0) FROM BorderImportLicenceItem
                    WHERE BorderImportLicenceItem.BorderImportLicenceId = BorderImportLicence.Id) AS Amount
            FROM BorderImportLicence
                INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
                INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
                INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
            WHERE ApplyType = 'Extension' AND BorderImportLicence.Status = 'Approved' AND CardType = 'Individual Trading'
                AND ((@FromDate IS NULL OR BorderImportLicence.CreatedDate >= @FromDate) AND (@ToDate IS NULL OR BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))))
                AND BorderImportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND IndividualTrading.TINNo = (CASE WHEN @CompanyRegistrationNo = '' THEN IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
                AND BorderImportLicence.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE IF @FormType = N'Border Export Permit'
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
            WHERE ApplyType = 'Extension' AND BorderExportPermit.Status = 'Approved'
                AND ((@FromDate IS NULL OR BorderExportPermit.CreatedDate >= @FromDate) AND (@ToDate IS NULL OR BorderExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))))
                AND BorderExportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                AND BorderExportPermit.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderExportPermit.SakhanId ELSE @SakhanId END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
    ELSE IF @FormType = N'Border Import Permit'
    BEGIN
        SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                (SELECT TOP 1 currency.Code FROM BorderImportPermitItem
                    INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
                    WHERE BorderImportPermitItem.BorderImportPermitId = BorderImportPermit.Id) AS Currency,
                (SELECT ISNULL(SUM(BorderImportPermitItem.Amount), 0) FROM BorderImportPermitItem
                    WHERE BorderImportPermitItem.BorderImportPermitId = BorderImportPermit.Id) AS Amount
            FROM BorderImportPermit
                INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
                INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
            WHERE ApplyType = 'Extension' AND BorderImportPermit.Status = 'Approved'
                AND ((@FromDate IS NULL OR BorderImportPermit.CreatedDate >= @FromDate) AND (@ToDate IS NULL OR BorderImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))))
                AND BorderImportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                AND BorderImportPermit.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderImportPermit.SakhanId ELSE @SakhanId END)
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
                (SELECT ISNULL(SUM(ImportLicenceItem.Amount), 0) FROM ImportLicenceItem
                    WHERE ImportLicenceItem.ImportLicenceId = ImportLicence.Id) AS Amount
            FROM ImportLicence
                INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
            WHERE ApplyType = 'Extension' AND ImportLicence.Status = 'Approved'
                AND ((@FromDate IS NULL OR ImportLicence.CreatedDate >= @FromDate) AND (@ToDate IS NULL OR ImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))))
                AND ImportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ) d
        GROUP BY ISNULL(d.Currency, N'')
        OPTION (RECOMPILE);
    END
END

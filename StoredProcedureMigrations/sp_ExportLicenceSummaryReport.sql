USE [TradeNetDB];
GO

/*
    sp_ExportLicenceSummaryReport
    -----------------------------
    Export-only grouped source for:
      Export Licence By Method
      Export Licence By Section
      Export Licence By Seller Country
      Export Licence Company List
      Export Licence Daily New Licence

    This does not depend on an indexed view. It groups ExportLicenceItem by
    licence/currency first, then groups by the requested report dimension.
*/
CREATE OR ALTER PROCEDURE dbo.sp_ExportLicenceSummaryReport
    @FromDate datetime,
    @ToDate datetime,
    @PaThaKaTypeId int = 0,
    @ExportImportSectionId int = 0,
    @ExportImportMethodId int = 0,
    @ExportImportIncotermId int = 0,
    @BuyerCountryId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @Dimension varchar(20) = 'Method'
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    IF @Dimension = 'Method'
    BEGIN
        ;WITH ItemTotals AS
        (
            SELECT item.ExportLicenceId, item.CurrencyId, SUM(item.Amount) AS TotalAmount
            FROM dbo.ExportLicenceItem AS item
            GROUP BY item.ExportLicenceId, item.CurrencyId
        )
        SELECT
            CAST(NULL AS nvarchar(200)) AS SectionName,
            CAST(NULL AS int) AS SectionId,
            m.Name AS MethodName,
            el.ExportImportMethodId AS MethodId,
            CAST(NULL AS nvarchar(200)) AS Country,
            CAST(NULL AS int) AS CountryId,
            CAST(NULL AS nvarchar(400)) AS CompanyName,
            CAST(NULL AS nvarchar(50)) AS CompanyRegistrationNo,
            CAST(NULL AS varchar(10)) AS GroupDate,
            c.Code AS Currency,
            COUNT(DISTINCT NULLIF(el.ExportLicenceNo, N'')) AS NoOfLicences,
            COALESCE(SUM(item.TotalAmount), 0) AS TotalValue
        FROM dbo.ExportLicence AS el
        INNER JOIN dbo.PaThaKa AS p ON p.Id = el.PaThaKaId
        INNER JOIN ItemTotals AS item ON item.ExportLicenceId = el.Id
        INNER JOIN dbo.Currency AS c ON c.Id = item.CurrencyId
        INNER JOIN dbo.ExportImportMethod AS m ON m.Id = el.ExportImportMethodId
        WHERE el.ApplyType = N'New'
          AND el.Status = N'Approved'
          AND el.CreatedDate >= @FromDate
          AND el.CreatedDate <= @ToDate
          AND (@CompanyRegistrationNo = N'' OR p.CompanyRegistrationNo = @CompanyRegistrationNo)
          AND (@PaThaKaTypeId = 0 OR p.PaThaKaTypeId = @PaThaKaTypeId)
          AND (@ExportImportSectionId = 0 OR el.ExportImportSectionId = @ExportImportSectionId)
          AND (@ExportImportMethodId = 0 OR el.ExportImportMethodId = @ExportImportMethodId)
          AND (@ExportImportIncotermId = 0 OR el.ExportImportIncotermId = @ExportImportIncotermId)
          AND (@BuyerCountryId = 0 OR el.BuyerCountryId = @BuyerCountryId)
        GROUP BY m.Name, el.ExportImportMethodId, c.Code
        OPTION (RECOMPILE);
        RETURN;
    END

    IF @Dimension = 'Section'
    BEGIN
        ;WITH ItemTotals AS
        (
            SELECT item.ExportLicenceId, item.CurrencyId, SUM(item.Amount) AS TotalAmount
            FROM dbo.ExportLicenceItem AS item
            GROUP BY item.ExportLicenceId, item.CurrencyId
        )
        SELECT
            s.Name AS SectionName,
            el.ExportImportSectionId AS SectionId,
            CAST(NULL AS nvarchar(200)) AS MethodName,
            CAST(NULL AS int) AS MethodId,
            CAST(NULL AS nvarchar(200)) AS Country,
            CAST(NULL AS int) AS CountryId,
            CAST(NULL AS nvarchar(400)) AS CompanyName,
            CAST(NULL AS nvarchar(50)) AS CompanyRegistrationNo,
            CAST(NULL AS varchar(10)) AS GroupDate,
            c.Code AS Currency,
            COUNT(DISTINCT NULLIF(el.ExportLicenceNo, N'')) AS NoOfLicences,
            COALESCE(SUM(item.TotalAmount), 0) AS TotalValue
        FROM dbo.ExportLicence AS el
        INNER JOIN dbo.PaThaKa AS p ON p.Id = el.PaThaKaId
        INNER JOIN ItemTotals AS item ON item.ExportLicenceId = el.Id
        INNER JOIN dbo.Currency AS c ON c.Id = item.CurrencyId
        INNER JOIN dbo.ExportImportSection AS s ON s.Id = el.ExportImportSectionId
        WHERE el.ApplyType = N'New'
          AND el.Status = N'Approved'
          AND el.CreatedDate >= @FromDate
          AND el.CreatedDate <= @ToDate
          AND (@CompanyRegistrationNo = N'' OR p.CompanyRegistrationNo = @CompanyRegistrationNo)
          AND (@PaThaKaTypeId = 0 OR p.PaThaKaTypeId = @PaThaKaTypeId)
          AND (@ExportImportSectionId = 0 OR el.ExportImportSectionId = @ExportImportSectionId)
          AND (@ExportImportMethodId = 0 OR el.ExportImportMethodId = @ExportImportMethodId)
          AND (@ExportImportIncotermId = 0 OR el.ExportImportIncotermId = @ExportImportIncotermId)
          AND (@BuyerCountryId = 0 OR el.BuyerCountryId = @BuyerCountryId)
        GROUP BY s.Name, el.ExportImportSectionId, c.Code
        OPTION (RECOMPILE);
        RETURN;
    END

    IF @Dimension = 'Country'
    BEGIN
        ;WITH ItemTotals AS
        (
            SELECT item.ExportLicenceId, item.CurrencyId, SUM(item.Amount) AS TotalAmount
            FROM dbo.ExportLicenceItem AS item
            GROUP BY item.ExportLicenceId, item.CurrencyId
        )
        SELECT
            CAST(NULL AS nvarchar(200)) AS SectionName,
            CAST(NULL AS int) AS SectionId,
            CAST(NULL AS nvarchar(200)) AS MethodName,
            CAST(NULL AS int) AS MethodId,
            co.Name AS Country,
            el.BuyerCountryId AS CountryId,
            CAST(NULL AS nvarchar(400)) AS CompanyName,
            CAST(NULL AS nvarchar(50)) AS CompanyRegistrationNo,
            CAST(NULL AS varchar(10)) AS GroupDate,
            c.Code AS Currency,
            COUNT(DISTINCT NULLIF(el.ExportLicenceNo, N'')) AS NoOfLicences,
            COALESCE(SUM(item.TotalAmount), 0) AS TotalValue
        FROM dbo.ExportLicence AS el
        INNER JOIN dbo.PaThaKa AS p ON p.Id = el.PaThaKaId
        INNER JOIN ItemTotals AS item ON item.ExportLicenceId = el.Id
        INNER JOIN dbo.Currency AS c ON c.Id = item.CurrencyId
        INNER JOIN dbo.Countries AS co ON co.Id = el.BuyerCountryId
        WHERE el.ApplyType = N'New'
          AND el.Status = N'Approved'
          AND el.CreatedDate >= @FromDate
          AND el.CreatedDate <= @ToDate
          AND (@CompanyRegistrationNo = N'' OR p.CompanyRegistrationNo = @CompanyRegistrationNo)
          AND (@PaThaKaTypeId = 0 OR p.PaThaKaTypeId = @PaThaKaTypeId)
          AND (@ExportImportSectionId = 0 OR el.ExportImportSectionId = @ExportImportSectionId)
          AND (@ExportImportMethodId = 0 OR el.ExportImportMethodId = @ExportImportMethodId)
          AND (@ExportImportIncotermId = 0 OR el.ExportImportIncotermId = @ExportImportIncotermId)
          AND (@BuyerCountryId = 0 OR el.BuyerCountryId = @BuyerCountryId)
        GROUP BY co.Name, el.BuyerCountryId, c.Code
        OPTION (RECOMPILE);
        RETURN;
    END

    IF @Dimension = 'Company'
    BEGIN
        ;WITH ItemTotals AS
        (
            SELECT item.ExportLicenceId, item.CurrencyId, SUM(item.Amount) AS TotalAmount
            FROM dbo.ExportLicenceItem AS item
            GROUP BY item.ExportLicenceId, item.CurrencyId
        )
        SELECT
            CAST(NULL AS nvarchar(200)) AS SectionName,
            CAST(NULL AS int) AS SectionId,
            CAST(NULL AS nvarchar(200)) AS MethodName,
            CAST(NULL AS int) AS MethodId,
            CAST(NULL AS nvarchar(200)) AS Country,
            CAST(NULL AS int) AS CountryId,
            p.CompanyName AS CompanyName,
            p.CompanyRegistrationNo AS CompanyRegistrationNo,
            CAST(NULL AS varchar(10)) AS GroupDate,
            c.Code AS Currency,
            COUNT(DISTINCT NULLIF(el.ExportLicenceNo, N'')) AS NoOfLicences,
            COALESCE(SUM(item.TotalAmount), 0) AS TotalValue
        FROM dbo.ExportLicence AS el
        INNER JOIN dbo.PaThaKa AS p ON p.Id = el.PaThaKaId
        INNER JOIN ItemTotals AS item ON item.ExportLicenceId = el.Id
        INNER JOIN dbo.Currency AS c ON c.Id = item.CurrencyId
        WHERE el.ApplyType = N'New'
          AND el.Status = N'Approved'
          AND el.CreatedDate >= @FromDate
          AND el.CreatedDate <= @ToDate
          AND (@CompanyRegistrationNo = N'' OR p.CompanyRegistrationNo = @CompanyRegistrationNo)
          AND (@PaThaKaTypeId = 0 OR p.PaThaKaTypeId = @PaThaKaTypeId)
          AND (@ExportImportSectionId = 0 OR el.ExportImportSectionId = @ExportImportSectionId)
          AND (@ExportImportMethodId = 0 OR el.ExportImportMethodId = @ExportImportMethodId)
          AND (@ExportImportIncotermId = 0 OR el.ExportImportIncotermId = @ExportImportIncotermId)
          AND (@BuyerCountryId = 0 OR el.BuyerCountryId = @BuyerCountryId)
        GROUP BY p.CompanyName, p.CompanyRegistrationNo, c.Code
        OPTION (RECOMPILE);
        RETURN;
    END

    IF @Dimension = 'Daily'
    BEGIN
        ;WITH ItemTotals AS
        (
            SELECT item.ExportLicenceId, item.CurrencyId, SUM(item.Amount) AS TotalAmount
            FROM dbo.ExportLicenceItem AS item
            GROUP BY item.ExportLicenceId, item.CurrencyId
        )
        SELECT
            CAST(NULL AS nvarchar(200)) AS SectionName,
            CAST(NULL AS int) AS SectionId,
            CAST(NULL AS nvarchar(200)) AS MethodName,
            CAST(NULL AS int) AS MethodId,
            CAST(NULL AS nvarchar(200)) AS Country,
            CAST(NULL AS int) AS CountryId,
            CAST(NULL AS nvarchar(400)) AS CompanyName,
            CAST(NULL AS nvarchar(50)) AS CompanyRegistrationNo,
            CONVERT(varchar(10), el.IssuedDate, 23) AS GroupDate,
            c.Code AS Currency,
            COUNT(DISTINCT NULLIF(el.ExportLicenceNo, N'')) AS NoOfLicences,
            COALESCE(SUM(item.TotalAmount), 0) AS TotalValue
        FROM dbo.ExportLicence AS el
        INNER JOIN dbo.PaThaKa AS p ON p.Id = el.PaThaKaId
        INNER JOIN ItemTotals AS item ON item.ExportLicenceId = el.Id
        INNER JOIN dbo.Currency AS c ON c.Id = item.CurrencyId
        WHERE el.ApplyType = N'New'
          AND el.Status = N'Approved'
          AND el.CreatedDate >= @FromDate
          AND el.CreatedDate <= @ToDate
          AND (@CompanyRegistrationNo = N'' OR p.CompanyRegistrationNo = @CompanyRegistrationNo)
          AND (@PaThaKaTypeId = 0 OR p.PaThaKaTypeId = @PaThaKaTypeId)
          AND (@ExportImportSectionId = 0 OR el.ExportImportSectionId = @ExportImportSectionId)
          AND (@ExportImportMethodId = 0 OR el.ExportImportMethodId = @ExportImportMethodId)
          AND (@ExportImportIncotermId = 0 OR el.ExportImportIncotermId = @ExportImportIncotermId)
          AND (@BuyerCountryId = 0 OR el.BuyerCountryId = @BuyerCountryId)
        GROUP BY CONVERT(varchar(10), el.IssuedDate, 23), c.Code
        OPTION (RECOMPILE);
        RETURN;
    END
END
GO

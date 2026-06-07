USE [TradeNetDB];
GO

/*
    sp_ImportLicenceSummaryReport_Indexed
    -------------------------------------
    Fast replacement source for the Oversea Import Licence SUMMARY reports:
      By Section / By Method / By Seller Country / Company List / Daily /
      Total Value (per currency) / Total Licences (per Pa Tha Ka type).

    Why this exists:
      The EF/LINQ aggregates joined dbo.ImportLicenceItem (~6.4M rows) and the
      GROUP BY + COUNT(DISTINCT) timed out (>180s). Referencing the indexed view
      dbo.vw_ImportLicenceItemTotalByCurrency from EF does NOT help, because EF
      cannot emit WITH (NOEXPAND) and SQL Server expands the view back into the
      base-table join. This proc reads the materialized indexed view DIRECTLY via
      WITH (NOEXPAND) (one pre-summed row per licence+currency), joins only the
      lookups each dimension displays, and groups in SQL.

    Filters mirror the legacy detail report exactly (ApplyType='New',
    Status='Approved', ImportLicenceNo<>''), so counts and sums match.

    @Dimension: Section | Method | Country | Company | Daily | TotalValue | PaThaKaType
    Result columns are uniform across dimensions (NULL where not applicable):
      SectionName, SectionId, MethodName, MethodId, Country, CountryId,
      CompanyName, CompanyRegistrationNo, PaThaKaType, GroupDate, Currency,
      NoOfLicences, TotalValue
*/
CREATE OR ALTER PROCEDURE dbo.sp_ImportLicenceSummaryReport_Indexed
    @FromDate datetime,
    @ToDate datetime,
    @PaThaKaTypeId int = 0,
    @ExportImportSectionId int = 0,
    @ExportImportMethodId int = 0,
    @ExportImportIncotermId int = 0,
    @SellerCountryId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @Currency nvarchar(20) = N'',
    @Dimension varchar(20) = 'TotalValue'
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @selDim nvarchar(max);
    DECLARE @joinDim nvarchar(max) = N'';
    DECLARE @groupDim nvarchar(max) = NULL;

    -- All non-applicable label columns are emitted as typed NULLs so every branch
    -- returns the identical, strongly-typed column set.
    DECLARE @nullSection nvarchar(max) = N'CAST(NULL AS nvarchar(200)) AS SectionName, CAST(NULL AS int) AS SectionId';
    DECLARE @nullMethod  nvarchar(max) = N'CAST(NULL AS nvarchar(200)) AS MethodName, CAST(NULL AS int) AS MethodId';
    DECLARE @nullCountry nvarchar(max) = N'CAST(NULL AS nvarchar(200)) AS Country, CAST(NULL AS int) AS CountryId';
    DECLARE @nullCompany nvarchar(max) = N'CAST(NULL AS nvarchar(400)) AS CompanyName, CAST(NULL AS nvarchar(50)) AS CompanyRegistrationNo';
    DECLARE @nullPtk     nvarchar(max) = N'CAST(NULL AS nvarchar(200)) AS PaThaKaType';
    DECLARE @nullDate    nvarchar(max) = N'CAST(NULL AS varchar(10)) AS GroupDate';

    IF @Dimension = 'Section'
    BEGIN
        SET @selDim = N's.Name AS SectionName, il.ExportImportSectionId AS SectionId, '
            + @nullMethod + N', ' + @nullCountry + N', ' + @nullCompany + N', ' + @nullPtk + N', ' + @nullDate;
        SET @joinDim = N' INNER JOIN dbo.ExportImportSection AS s ON s.Id = il.ExportImportSectionId';
        SET @groupDim = N's.Name, il.ExportImportSectionId';
    END
    ELSE IF @Dimension = 'Method'
    BEGIN
        SET @selDim = @nullSection + N', m.Name AS MethodName, il.ExportImportMethodId AS MethodId, '
            + @nullCountry + N', ' + @nullCompany + N', ' + @nullPtk + N', ' + @nullDate;
        SET @joinDim = N' INNER JOIN dbo.ExportImportMethod AS m ON m.Id = il.ExportImportMethodId';
        SET @groupDim = N'm.Name, il.ExportImportMethodId';
    END
    ELSE IF @Dimension = 'Country'
    BEGIN
        SET @selDim = @nullSection + N', ' + @nullMethod + N', co.Name AS Country, il.SellerCountryId AS CountryId, '
            + @nullCompany + N', ' + @nullPtk + N', ' + @nullDate;
        SET @joinDim = N' INNER JOIN dbo.Countries AS co ON co.Id = il.SellerCountryId';
        SET @groupDim = N'co.Name, il.SellerCountryId';
    END
    ELSE IF @Dimension = 'Company'
    BEGIN
        SET @selDim = @nullSection + N', ' + @nullMethod + N', ' + @nullCountry
            + N', p.CompanyName AS CompanyName, p.CompanyRegistrationNo AS CompanyRegistrationNo, ' + @nullPtk + N', ' + @nullDate;
        SET @groupDim = N'p.CompanyName, p.CompanyRegistrationNo';
    END
    ELSE IF @Dimension = 'PaThaKaType'
    BEGIN
        SET @selDim = @nullSection + N', ' + @nullMethod + N', ' + @nullCountry + N', ' + @nullCompany
            + N', pt.Description AS PaThaKaType, ' + @nullDate;
        SET @joinDim = N' INNER JOIN dbo.PaThaKaType AS pt ON pt.Id = p.PaThaKaTypeId';
        SET @groupDim = N'pt.Description';
    END
    ELSE IF @Dimension = 'Daily'
    BEGIN
        SET @selDim = @nullSection + N', ' + @nullMethod + N', ' + @nullCountry + N', ' + @nullCompany + N', ' + @nullPtk
            + N', CONVERT(varchar(10), il.IssuedDate, 23) AS GroupDate';
        SET @groupDim = N'CONVERT(varchar(10), il.IssuedDate, 23)';
    END
    ELSE -- TotalValue (per currency only)
    BEGIN
        SET @selDim = @nullSection + N', ' + @nullMethod + N', ' + @nullCountry + N', ' + @nullCompany + N', ' + @nullPtk + N', ' + @nullDate;
        SET @groupDim = NULL;
    END

    DECLARE @sql nvarchar(max) =
        N'SELECT ' + @selDim + N', c.Code AS Currency,
       COUNT(DISTINCT il.ImportLicenceNo) AS NoOfLicences,
       COALESCE(SUM(v.TotalAmount), 0) AS TotalValue
  FROM dbo.ImportLicence AS il
  INNER JOIN dbo.PaThaKa AS p ON p.Id = il.PaThaKaId
  INNER JOIN dbo.vw_ImportLicenceItemTotalByCurrency AS v WITH (NOEXPAND) ON v.ImportLicenceId = il.Id
  INNER JOIN dbo.Currency AS c ON c.Id = v.CurrencyId'
        + @joinDim
        + N' WHERE il.ApplyType = N''New'' AND il.Status = N''Approved'' AND il.ImportLicenceNo <> N''''
    AND il.CreatedDate >= @FromDate AND il.CreatedDate <= @ToDate
    AND (@CompanyRegistrationNo = N'''' OR p.CompanyRegistrationNo = @CompanyRegistrationNo)
    AND (@PaThaKaTypeId = 0 OR p.PaThaKaTypeId = @PaThaKaTypeId)
    AND (@ExportImportSectionId = 0 OR il.ExportImportSectionId = @ExportImportSectionId)
    AND (@ExportImportMethodId = 0 OR il.ExportImportMethodId = @ExportImportMethodId)
    AND (@ExportImportIncotermId = 0 OR il.ExportImportIncotermId = @ExportImportIncotermId)
    AND (@SellerCountryId = 0 OR il.SellerCountryId = @SellerCountryId)
    AND (@Currency = N'''' OR c.Code = @Currency)
  GROUP BY ' + ISNULL(@groupDim + N', ', N'') + N'c.Code
  OPTION (RECOMPILE);';

    EXEC sys.sp_executesql @sql,
        N'@FromDate datetime, @ToDate datetime, @PaThaKaTypeId int, @ExportImportSectionId int, @ExportImportMethodId int, @ExportImportIncotermId int, @SellerCountryId int, @CompanyRegistrationNo nvarchar(50), @Currency nvarchar(20)',
        @FromDate, @ToDate, @PaThaKaTypeId, @ExportImportSectionId, @ExportImportMethodId, @ExportImportIncotermId, @SellerCountryId, @CompanyRegistrationNo, @Currency;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_ExportPermitDetailReport_Aggregate]
    @Type nvarchar(20) = N'Oversea',          -- 'Oversea' (ExportPermit) | 'Border' (BorderExportPermit)
    @Dimension nvarchar(20) = N'Section',      -- 'Section' | 'Country' | 'Company' | 'Daily'
    @IncludeSakhan bit = 0,                     -- only meaningful for Border (Sakhan grouping/display)
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @PaThaKaTypeId int = 0,
    @ExportImportSectionId int = 0,
    @BuyerCountryId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @SakhanId int = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- SQL-side replacement for the in-memory aggregate path of sp_ExportPermitDetailReport_Fast
    -- (the C# OverseaRows/BorderRows LINQ + ReportAggregationService.Aggregate). The old path pulled
    -- the ENTIRE detail join (~12k rows x ~40 columns) to the app and grouped in memory; over the
    -- remote DB link that transfer took ~50s and hung the By-Section / By-SellerCountry / Company-List
    -- / Daily reports. This proc does the GROUP BY in SQL so only the handful of grouped rows cross the
    -- wire (~0.3s). The FROM/WHERE mirror OverseaRows/BorderRows EXACTLY (same INNER JOINs, so row
    -- membership -- and therefore COUNT(DISTINCT licence) + SUM(amount) -- is identical; NRC prefixes
    -- are LEFT joins in the C# and are not needed here). Output columns map 1:1 onto
    -- ReportAggregateResult so the rows feed ReportAggregationService.CreatePagedResultFromGroups,
    -- which still does the ordering / paging / column totals (and, for Daily, the USD fill).
    --   * Section  -> group by Section name (+ currency [+ sakhan]); SectionId = drill-down id
    --   * Country   -> group by buyer Country name (+ currency [+ sakhan]); CountryId = drill-down id
    --   * Company  -> group by CompanyName + CompanyRegistrationNo (+ currency [+ sakhan])
    --   * Daily     -> group by IssuedDate (yyyy-MM-dd) (+ currency [+ sakhan]); USD value filled in C#
    -- NoOfLicences = COUNT(DISTINCT ExportPermitNo) within the group (the join fans permit -> items).

    DECLARE @tbl sysname        = CASE WHEN @Type = N'Border' THEN N'BorderExportPermit' ELSE N'ExportPermit' END;
    DECLARE @itemTbl sysname    = CASE WHEN @Type = N'Border' THEN N'BorderExportPermitItem' ELSE N'ExportPermitItem' END;
    DECLARE @fk sysname         = CASE WHEN @Type = N'Border' THEN N'BorderExportPermitId' ELSE N'ExportPermitId' END;
    DECLARE @useSakhan bit      = CASE WHEN @Type = N'Border' AND @IncludeSakhan = 1 THEN 1 ELSE 0 END;

    -- Dimension-specific display columns (the non-matching ones are emitted as NULL so the result set
    -- shape -- and the ReportAggregateResult mapping -- is identical for every dimension).
    DECLARE @selSection nvarchar(max) = N'CAST(NULL AS nvarchar(200))';
    DECLARE @selSectionId nvarchar(max) = N'CAST(NULL AS int)';
    DECLARE @selCountry nvarchar(max) = N'CAST(NULL AS nvarchar(200))';
    DECLARE @selCountryId nvarchar(max) = N'CAST(NULL AS int)';
    DECLARE @selCompany nvarchar(max) = N'CAST(NULL AS nvarchar(400))';
    DECLARE @selCompanyReg nvarchar(max) = N'CAST(NULL AS nvarchar(50))';
    DECLARE @selDate nvarchar(max) = N'CAST(NULL AS nvarchar(10))';
    DECLARE @grpExtra nvarchar(max) = N'';

    IF @Dimension = N'Section'
    BEGIN
        SET @selSection = N'section.Name';
        SET @selSectionId = N'MAX(permit.ExportImportSectionId)';
        SET @grpExtra = N'section.Name';
    END
    ELSE IF @Dimension = N'Country'
    BEGIN
        SET @selCountry = N'buyerCountry.Name';
        SET @selCountryId = N'MAX(permit.BuyerCountryId)';
        SET @grpExtra = N'buyerCountry.Name';
    END
    ELSE IF @Dimension = N'Company'
    BEGIN
        SET @selCompany = N'paThaKa.CompanyName';
        SET @selCompanyReg = N'paThaKa.CompanyRegistrationNo';
        SET @grpExtra = N'paThaKa.CompanyName, paThaKa.CompanyRegistrationNo';
    END
    ELSE IF @Dimension = N'Daily'
    BEGIN
        SET @selDate = N'ISNULL(CONVERT(varchar(10), permit.IssuedDate, 23), '''')';
        SET @grpExtra = N'CONVERT(varchar(10), permit.IssuedDate, 23)';
    END

    DECLARE @selSakhanCode nvarchar(max) = CASE WHEN @useSakhan = 1 THEN N'sakhan.Code' ELSE N'CAST(NULL AS nvarchar(50))' END;
    DECLARE @selSakhanName nvarchar(max) = CASE WHEN @useSakhan = 1 THEN N'sakhan.Name' ELSE N'CAST(NULL AS nvarchar(200))' END;
    DECLARE @grpSakhan nvarchar(max) = CASE WHEN @useSakhan = 1 THEN N', sakhan.Code, sakhan.Name' ELSE N'' END;
    DECLARE @joinSakhan nvarchar(max) = CASE WHEN @Type = N'Border' THEN N'INNER JOIN Sakhan sakhan ON permit.SakhanId = sakhan.Id' ELSE N'' END;
    DECLARE @predSakhan nvarchar(max) = CASE WHEN @Type = N'Border' THEN N'AND (@SakhanId = 0 OR permit.SakhanId = @SakhanId)' ELSE N'' END;

    DECLARE @sql nvarchar(max) = N'
    SELECT
        ' + @selSakhanCode + N' AS SakhanCode,
        ' + @selSakhanName + N' AS SakhanName,
        ' + @selSection + N' AS SectionName,
        CAST(NULL AS nvarchar(200)) AS MethodName,
        ' + @selCountry + N' AS Country,
        ' + @selCompany + N' AS CompanyName,
        ' + @selCompanyReg + N' AS CompanyRegistrationNo,
        CAST(NULL AS nvarchar(50)) AS HSCode,
        CAST(NULL AS nvarchar(1000)) AS HSDescription,
        ' + @selDate + N' AS Date,
        COUNT(DISTINCT permit.ExportPermitNo) AS NoOfLicences,
        SUM(item.Amount) AS TotalValue,
        currency.Code AS Currency,
        ' + @selSectionId + N' AS SectionId,
        CAST(NULL AS int) AS MethodId,
        ' + @selCountryId + N' AS CountryId,
        CAST(NULL AS decimal(38,6)) AS TotalUSDValue
    FROM ' + QUOTENAME(@tbl) + N' permit
        INNER JOIN PaThaKa paThaKa ON permit.PaThaKaId = paThaKa.Id
        INNER JOIN PaThaKaType paThaKaType ON paThaKa.PaThaKaTypeId = paThaKaType.Id
        INNER JOIN ' + QUOTENAME(@itemTbl) + N' item ON permit.Id = item.' + QUOTENAME(@fk) + N'
        INNER JOIN Unit unit ON item.UnitId = unit.Id
        INNER JOIN Currency currency ON item.CurrencyId = currency.Id
        INNER JOIN HSCode hsCode ON item.HSCodeId = hsCode.Id
        INNER JOIN ExportImportSection section ON permit.ExportImportSectionId = section.Id
        INNER JOIN Countries buyerCountry ON permit.BuyerCountryId = buyerCountry.Id
        INNER JOIN Countries consignedCountry ON permit.ConsignedCountryId = consignedCountry.Id
        ' + @joinSakhan + N'
    WHERE permit.ApplyType = ''New'' AND permit.Status = ''Approved''
        AND permit.CreatedDate >= @FromDate AND permit.CreatedDate <= @ToDate
        AND (@CompanyRegistrationNo = '''' OR paThaKa.CompanyRegistrationNo = @CompanyRegistrationNo)
        AND (@PaThaKaTypeId = 0 OR paThaKa.PaThaKaTypeId = @PaThaKaTypeId)
        AND (@ExportImportSectionId = 0 OR permit.ExportImportSectionId = @ExportImportSectionId)
        AND (@BuyerCountryId = 0 OR permit.BuyerCountryId = @BuyerCountryId)
        ' + @predSakhan + N'
    GROUP BY ' + @grpExtra + @grpSakhan + N', currency.Code
    OPTION (RECOMPILE);';

    EXEC sp_executesql @sql,
        N'@FromDate datetime, @ToDate datetime, @PaThaKaTypeId int, @ExportImportSectionId int, @BuyerCountryId int, @CompanyRegistrationNo nvarchar(50), @SakhanId int',
        @FromDate = @FromDate, @ToDate = @ToDate, @PaThaKaTypeId = @PaThaKaTypeId,
        @ExportImportSectionId = @ExportImportSectionId, @BuyerCountryId = @BuyerCountryId,
        @CompanyRegistrationNo = @CompanyRegistrationNo, @SakhanId = @SakhanId;
END

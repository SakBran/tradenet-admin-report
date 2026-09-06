/* =====================================================================================
   Border Import Permit customer-complaint deployment - 2026-09-05
   Run this ONE file to apply all four procedures, or run the numbered files 01..04
   individually. Either way: PROCEDURES FIRST, APPLICATION SECOND.

   Target database: TradeNetDB  (NOT ReportTemplateDB - that one only holds the Excel
   export job queue; deploying report procedures into it is a known trap.)

   What changes:
     01 sp_NewReport_pagination - the Border Import Permit and Border Export Permit New
        branches windowed on '< DATEADD(day, 1, @ToDate)'. Callers pass @ToDate as
        '<day> 23:59:59', so that reached a whole extra day and the report listed permits
        the old sp_NewReport ('CreatedDate <= @ToDate') never showed. Measured on the live
        report API: To = 2025-01-12 23:59:59 returned 2 permits actually dated 2025-01-13.
        Both branches now use the calendar-date form the other six branches already use.

     02 sp_HSCodeReport_pagination - two fixes.
        (a) @FetchSize: the caller used to ask for one row MORE than a page so it could tell
            whether a next page exists without paying for COUNT(*), but it did that by
            inflating @PageSize -- which also inflated this procedure's OFFSET. Page 2 of a
            10-row page started at row 12, so one row was lost at every page boundary: a
            31-row report displayed 10 + 10 + 9 = 29. This affects ALL EIGHT FormType
            branches, so re-check paging on every HS Code report after deploying.
            The application is no longer order-sensitive here: seven of the eight branches
            return COUNT(*) OVER() regardless, and the C# now pages off that count. Only the
            Export Licence fast page still needs @FetchSize for its next-page marker.
        (b) the Border Import Permit @HSCode='' branch groups on (HSCodeId, Currency) only.
            BorderHSCodeReport.rdlc's row group is exactly that (rdlc:1157-1169) and renders
            no company column, so the extra company key split one HS code into a row per
            buyer, each carrying a partial Total Value (31 rows where the old shape gives
            16). The Start/End sub-branches KEEP the company: they also serve
            BorderImportPermitHSCodeDetailReport, whose rdlc does render Company Name.

     03 sp_ImportPermitListingCurrencyTotals - new Border Import Permit ApplyType='New'
        branch. The Border Import Permit New Report had no TOTAL at all, in the grid or the
        .xlsx, because this procedure deliberately returned an empty set for it. Restores
        BorderNewReport.rdlc's second tablix: '<CUR>: n licence(s)' + summed Total Value per
        currency, then the grand 'Total: n licence(s)'. Amount is SUM(items), matching the
        New grid -- not the TOP 1 the Amend branches use.

     04 sp_ExportPermitListingCurrencyTotals - the Border Export Permit New footer moves onto
        the same calendar-date window as its grid, which 01 just corrected. The two must
        always be flipped together or the footer count stops matching the rows.

   Generated from the repository files of the same name; see README.md in this folder.
   ===================================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE [TradeNetDB];
GO

-- Check `SELECT DB_NAME();` reads TradeNetDB before executing -- NOT ReportTemplateDB,
-- which only holds the Excel export job queue.

-- ============================================================================
-- sp_NewReport_pagination   (file 01_sp_NewReport_pagination.sql)
-- ============================================================================
PRINT N'Applying sp_NewReport_pagination ...';
GO

﻿CREATE OR ALTER PROCEDURE [dbo].[sp_NewReport_pagination]
    @FormType nvarchar(50) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @SakhanId int = 0,
    @auto nvarchar(50) = N'',
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL,
    @IncludeTotalCount bit = 1,
    @quota nvarchar(50) = N''
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ps bigint = CASE
        WHEN ISNULL(@PageSize,0) <= 0 THEN 9223372036854775807
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
        ELSE @PageSize END;
    DECLARE @off bigint = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 0 ELSE ISNULL(@PageIndex,0) * CAST(@PageSize AS bigint) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder,'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'Date', N'SectionCode', N'SectionName', N'OldLicenceNo', N'LicenceNo', N'sDate', N'CompanyRegistrationNo', N'CompanyName', N'UnitLevel', N'StreetNumberStreetName', N'QuarterCityTownship', N'State', N'Country', N'PostalCode', N'auto', N'quota', N'CommodityType')
    BEGIN
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir;
        IF @SortColumn <> N'Date' SET @ob += N', [Date] ASC';
        IF @SortColumn <> N'LicenceNo' SET @ob += N', [LicenceNo] ASC';
    END
    ELSE
        SET @ob = N'[Date] ASC, [LicenceNo] ASC';

    DECLARE @cntpart nvarchar(max);
    DECLARE @sql nvarchar(max);

    -- TotalCount only when requested, computed over the UN-paged base (no subqueries) as a separate scalar.
    IF @FormType = N'Import Permit'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ImportPermit
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType=''New'' AND ImportPermit.Status=''Approved''
		AND (ImportPermit.CreatedDate>=@FromDate AND ImportPermit.CreatedDate<=@ToDate)
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        -- ImportPermit has no auto/quota columns, so emit those as NULL to keep the result
        -- set matching sp_NewReportRow. CommodityType IS a real ImportPermit column, so
        -- source it (the grid's "Commodity Type" column was showing N/A otherwise).
        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ImportPermitItem
		INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
		WHERE ImportPermitItem.ImportPermitId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ImportPermitItem
		INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id
		WHERE ImportPermitItem.ImportPermitId=pg.__k_Id) HSCode,
        (SELECT ISNULL(SUM(ImportPermitItem.Amount),0) FROM ImportPermitItem
		WHERE ImportPermitItem.ImportPermitId=pg.__k_Id) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ImportPermit.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldImportPermitNo OldLicenceNo,
ImportPermitNo LicenceNo,
CONVERT(varchar,ImportPermit.LastDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
CAST(NULL AS nvarchar(50)) auto,
CAST(NULL AS nvarchar(50)) quota,
ImportPermit.CommodityType,
ImportPermit.Id AS __k_Id
        FROM ImportPermit
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType=''New'' AND ImportPermit.Status=''Approved''
		AND (ImportPermit.CreatedDate>=@FromDate AND ImportPermit.CreatedDate<=@ToDate)
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @FormType = N'Export Permit'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ExportPermit
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType=''New'' AND ExportPermit.Status=''Approved''
		AND (ExportPermit.CreatedDate>=@FromDate AND ExportPermit.CreatedDate<=@ToDate)
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        -- ExportPermit has no auto/quota columns; emit those as NULL. CommodityType is a
        -- real ExportPermit column (surfaced on the Export Permit New report) so select it.
        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ExportPermitItem
		INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
		WHERE ExportPermitItem.ExportPermitId=pg.__k_Id ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo) Currency,
        (SELECT top 1 HSCode.Code FROM ExportPermitItem
		INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id
		WHERE ExportPermitItem.ExportPermitId=pg.__k_Id ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo) HSCode,
        (SELECT ISNULL(SUM(ExportPermitItem.Amount),0) FROM ExportPermitItem
		WHERE ExportPermitItem.ExportPermitId=pg.__k_Id) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ExportPermit.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldExportPermitNo OldLicenceNo,
ExportPermitNo LicenceNo,
CONVERT(varchar,ExportPermit.LastDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
CAST(NULL AS nvarchar(50)) auto,
CAST(NULL AS nvarchar(50)) quota,
ExportPermit.CommodityType,
ExportPermit.Id AS __k_Id
        FROM ExportPermit
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType=''New'' AND ExportPermit.Status=''Approved''
		AND (ExportPermit.CreatedDate>=@FromDate AND ExportPermit.CreatedDate<=@ToDate)
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @FormType = N'Export Licence'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''New'' AND ExportLicence.Status=''Approved''
		AND ((@FromDate IS NULL) OR ExportLicence.CreatedDate>=@FromDate)
		AND ((@ToDate IS NULL) OR ExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND (@auto='''' OR ExportLicence.auto=@auto) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ExportLicenceItem
		INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id) HSCode,
        (SELECT ISNULL(SUM(ExportLicenceItem.Amount),0) FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ExportLicence.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldExportLicenceNo OldLicenceNo,
ExportLicenceNo LicenceNo,
CONVERT(varchar,ExportLicence.LastDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
ExportLicence.auto,
CAST(N'''' AS nvarchar(50)) quota,
ExportLicence.CommodityType,
ExportLicence.Id AS __k_Id
        FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''New'' AND ExportLicence.Status=''Approved''
		AND ((@FromDate IS NULL) OR ExportLicence.CreatedDate>=@FromDate)
		AND ((@ToDate IS NULL) OR ExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND (@auto='''' OR ExportLicence.auto=@auto)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @FormType = N'Border Export Licence'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM (
		SELECT BorderExportLicence.Id FROM BorderExportLicence
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''New'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		AND BorderExportLicence.auto=(CASE WHEN @auto='''' then BorderExportLicence.auto ELSE @auto END)
		UNION ALL
		SELECT BorderExportLicence.Id FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''New'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		AND BorderExportLicence.auto=(CASE WHEN @auto='''' then BorderExportLicence.auto ELSE @auto END)
	) tmp OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM BorderExportLicenceItem
		INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=pg.__k_Id) HSCode,
        (SELECT ISNULL(SUM(BorderExportLicenceItem.Amount),0) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=pg.__k_Id) Amount, @__total AS TotalCount
    FROM (
        SELECT * FROM (
        SELECT BorderExportLicence.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldExportLicenceNo OldLicenceNo,
ExportLicenceNo LicenceNo,
CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
BorderExportLicence.auto,
CAST(NULL AS nvarchar(50)) quota,
CAST(NULL AS nvarchar(max)) CommodityType,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportLicence.Id AS __k_Id
        FROM BorderExportLicence
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''New'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		AND BorderExportLicence.auto=(CASE WHEN @auto='''' then BorderExportLicence.auto ELSE @auto END)
		UNION ALL
        SELECT BorderExportLicence.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldExportLicenceNo OldLicenceNo,
ExportLicenceNo LicenceNo,
CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
IndividualTrading.TINNo CompanyRegistrationNo,
IndividualTrading.Name CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
BorderExportLicence.auto,
CAST(NULL AS nvarchar(50)) quota,
CAST(NULL AS nvarchar(max)) CommodityType,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportLicence.Id AS __k_Id
        FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''New'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		AND BorderExportLicence.auto=(CASE WHEN @auto='''' then BorderExportLicence.auto ELSE @auto END)
        ) u
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @FormType = N'Border Import Licence'
    BEGIN
        SET @cntpart = N'DECLARE @__total int = NULL; ';

        SET @sql = @cntpart + N'SELECT
        pg.Date,
        pg.SectionCode,
        pg.SectionName,
        pg.OldLicenceNo,
        pg.LicenceNo,
        pg.sDate,
        pg.CompanyRegistrationNo,
        pg.CompanyName,
        pg.UnitLevel,
        pg.StreetNumberStreetName,
        pg.QuarterCityTownship,
        pg.State,
        pg.Country,
        pg.PostalCode,
        pg.auto,
        pg.quota,
        pg.CommodityType,
        pg.SakhanId,
        pg.SakhanCode,
        pg.SakhanName,
        pg.__k_Id,
        (SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM BorderImportLicenceItem
		INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=pg.__k_Id) HSCode,
        (SELECT ISNULL(SUM(BorderImportLicenceItem.Amount),0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=pg.__k_Id) Amount, pg.__TotalCount AS TotalCount
    FROM (
        SELECT counted.* FROM (
        SELECT u.*, COUNT(*) OVER() AS __TotalCount FROM (
        SELECT BorderImportLicence.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldImportLicenceNo OldLicenceNo,
ImportLicenceNo LicenceNo,
CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
BorderImportLicence.auto,
BorderImportLicence.quota,
CAST(NULL AS nvarchar(max)) CommodityType,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportLicence.Id AS __k_Id
        FROM BorderImportLicence
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''New'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND ((@FromDate IS NULL) OR BorderImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		AND (@auto='''' OR BorderImportLicence.auto=@auto)
		UNION ALL
        SELECT BorderImportLicence.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldImportLicenceNo OldLicenceNo,
ImportLicenceNo LicenceNo,
CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
IndividualTrading.TINNo CompanyRegistrationNo,
IndividualTrading.Name CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
BorderImportLicence.auto,
BorderImportLicence.quota,
CAST(NULL AS nvarchar(max)) CommodityType,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportLicence.Id AS __k_Id
        FROM BorderImportLicence
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''New'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND ((@FromDate IS NULL) OR BorderImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		AND (@auto='''' OR BorderImportLicence.auto=@auto)
        ) u
        ) counted
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @FormType = N'Border Export Permit'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM BorderExportPermit
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		WHERE ApplyType=''New'' AND BorderExportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR BorderExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM BorderExportPermitItem
		INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM BorderExportPermitItem
		INNER JOIN HSCode ON BorderExportPermitItem.HSCodeId = HSCode.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=pg.__k_Id) HSCode,
        (SELECT ISNULL(SUM(BorderExportPermitItem.Amount),0) FROM BorderExportPermitItem
		WHERE BorderExportPermitItem.BorderExportPermitId=pg.__k_Id) Amount, @__total AS TotalCount
    FROM (
        SELECT BorderExportPermit.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldExportPermitNo OldLicenceNo,
ExportPermitNo LicenceNo,
CONVERT(varchar,BorderExportPermit.CreatedDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
CAST(NULL AS nvarchar(50)) auto,
CAST(NULL AS nvarchar(50)) quota,
BorderExportPermit.CommodityType,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportPermit.Id AS __k_Id
        FROM BorderExportPermit
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		WHERE ApplyType=''New'' AND BorderExportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR BorderExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @FormType = N'Border Import Permit'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM (
		SELECT BorderImportPermit.Id FROM BorderImportPermit
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		WHERE ApplyType=''New'' AND BorderImportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR BorderImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
	) tmp OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM BorderImportPermitItem
		INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM BorderImportPermitItem
		INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=pg.__k_Id) HSCode,
        (SELECT ISNULL(SUM(BorderImportPermitItem.Amount),0) FROM BorderImportPermitItem
		WHERE BorderImportPermitItem.BorderImportPermitId=pg.__k_Id) Amount, @__total AS TotalCount
    FROM (
        SELECT * FROM (
        SELECT BorderImportPermit.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldImportPermitNo OldLicenceNo,
ImportPermitNo LicenceNo,
CONVERT(varchar,BorderImportPermit.CreatedDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
CAST(NULL AS nvarchar(50)) auto,
CAST(NULL AS nvarchar(50)) quota,
CAST(NULL AS nvarchar(max)) CommodityType,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportPermit.Id AS __k_Id
        FROM BorderImportPermit
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		WHERE ApplyType=''New'' AND BorderImportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR BorderImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
        ) u
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE
    BEGIN
        -- Import Licence New listing. A "by Company Registration No" search must ignore the
        -- date window: the page defaults the range to the current month, so a reg-no lookup
        -- was returning only that month's licences (looked like "no data" to the customer).
        -- When @CompanyRegistrationNo is supplied we skip the date predicate and match the
        -- (indexed) reg-no exactly; when it is empty the date browse is unchanged. Both the
        -- COUNT and the paged SELECT use the same predicate so TotalCount == rows shown.
        -- (See key-search-trapped-behind-date-window. Comment kept OUT of the N'...' literal
        --  to avoid the nvarchar(4000) dynamic-SQL truncation trap.)
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''New'' AND ImportLicence.Status=''Approved''
		AND (@CompanyRegistrationNo<>'''' OR (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate))
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND (@auto='''' OR ImportLicence.auto=@auto)
		AND (@quota='''' OR ImportLicence.quota=@quota) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ImportLicenceItem
		INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
		WHERE ImportLicenceItem.ImportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ImportLicenceItem
		INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id
		WHERE ImportLicenceItem.ImportLicenceId=pg.__k_Id) HSCode,
        (SELECT ISNULL(SUM(ImportLicenceItem.Amount),0) FROM ImportLicenceItem
		WHERE ImportLicenceItem.ImportLicenceId=pg.__k_Id) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ImportLicence.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldImportLicenceNo OldLicenceNo,
ImportLicenceNo LicenceNo,
CONVERT(varchar,ImportLicence.LastDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
ImportLicence.auto,
ImportLicence.quota,
ImportLicence.CommodityType,
ImportLicence.Id AS __k_Id
        FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''New'' AND ImportLicence.Status=''Approved''
		AND (@CompanyRegistrationNo<>'''' OR (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate))
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND (@auto='''' OR ImportLicence.auto=@auto)
		AND (@quota='''' OR ImportLicence.quota=@quota)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END

    EXEC sp_executesql @sql, N'@FormType nvarchar(50), @FromDate datetime, @ToDate datetime, @ExportImportSectionId int, @CompanyRegistrationNo nvarchar(50), @SakhanId int, @auto nvarchar(50), @quota nvarchar(50), @off bigint, @ps bigint', @FormType=@FormType, @FromDate=@FromDate, @ToDate=@ToDate, @ExportImportSectionId=@ExportImportSectionId, @CompanyRegistrationNo=@CompanyRegistrationNo, @SakhanId=@SakhanId, @auto=@auto, @quota=@quota, @off=@off, @ps=@ps;
END

-- ============================================================================
-- sp_HSCodeReport_pagination   (file 02_sp_HSCodeReport_pagination.sql)
-- ============================================================================
PRINT N'Applying sp_HSCodeReport_pagination ...';
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_HSCodeReport_pagination]
	@FromDate datetime,
	@ToDate datetime,
	@FormType nvarchar(50),
	@FilterType nvarchar(20),
	@HSCode nvarchar(50),
	@SakhanId int,
	@PageIndex int = 0,
	@PageSize int = 10,
	@IncludeTotalCount bit = 1
AS
BEGIN
	SET NOCOUNT ON;

	SET @PageIndex = CASE WHEN @PageIndex < 0 THEN 0 ELSE @PageIndex END;
	SET @PageSize = CASE WHEN @PageSize <= 0 THEN 10 ELSE @PageSize END;
	SET @PageSize = CASE WHEN @PageSize > 1000 THEN 1000 ELSE @PageSize END;
	SET @HSCode = LTRIM(RTRIM(ISNULL(@HSCode, '')));
	SET @FilterType = ISNULL(@FilterType, '');

	-- The grid asks for one row MORE than a page so the caller can tell whether a next page
	-- exists without paying for COUNT(*). That sentinel must widen the FETCH only -- it must
	-- NOT widen the OFFSET. It used to be added by the caller to @PageSize itself, so page 2
	-- started at row (pageSize+1)*1 and every page boundary silently swallowed one row: a
	-- 31-row report showed 10+10+9 = 29. @FetchSize keeps the two apart.
	DECLARE @FetchSize int = @PageSize + CASE WHEN @IncludeTotalCount = 0 THEN 1 ELSE 0 END;

	IF(@FormType='Export Licence')
	BEGIN
		IF(@IncludeTotalCount=0)
		BEGIN
			SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
			result.NoOfLicences,result.TotalValue,CAST(NULL AS int) TotalCount
			FROM
			(
			SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
			COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
			FROM
			(SELECT HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,ExportLicenceItem.Amount,currency.Code Currency,
			ExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM ExportLicence
			INNER JOIN ExportLicenceItem ON ExportLicence.Id = ExportLicenceItem.ExportLicenceId
			INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
			WHERE ExportLicence.ApplyType='New' AND ExportLicence.Status='Approved'
			AND (ExportLicence.LicenceDate>=@FromDate AND ExportLicence.LicenceDate<=@ToDate)
			AND (@HSCode='' OR (@FilterType='Start' AND HSCode.Code LIKE @HSCode+'%') OR (@FilterType<>'Start' AND HSCode.Code LIKE '%'+@HSCode)))tmp
			GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
			)result
			ORDER BY result.HSCode,result.CompanyName,result.Currency
			OFFSET @PageIndex * @PageSize ROWS
			FETCH NEXT @FetchSize ROWS ONLY
			OPTION (RECOMPILE, MAXDOP 1);

			RETURN;
		END

		IF(@HSCode='')
		BEGIN
			SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
			result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
			FROM
			(
			SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
			COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
			FROM
			(SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			ExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM ExportLicence
			INNER JOIN ExportLicenceItem ON ExportLicence.Id = ExportLicenceItem.ExportLicenceId
			INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND ExportLicence.Status='Approved'
			AND (ExportLicence.LicenceDate>=@FromDate AND ExportLicence.LicenceDate<=@ToDate))tmp
			GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
			)result
			ORDER BY result.HSCode,result.CompanyName,result.Currency
			OFFSET @PageIndex * @PageSize ROWS
			FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ExportLicence
				INNER JOIN ExportLicenceItem ON ExportLicence.Id = ExportLicenceItem.ExportLicenceId
				INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ExportLicence.Status='Approved'
				AND (ExportLicence.LicenceDate>=@FromDate AND ExportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%')tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
			ELSE
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ExportLicence
				INNER JOIN ExportLicenceItem ON ExportLicence.Id = ExportLicenceItem.ExportLicenceId
				INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ExportLicence.Status='Approved'
				AND (ExportLicence.LicenceDate>=@FromDate AND ExportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode)tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
		END
	END
	ELSE IF(@FormType='Import Licence')
	BEGIN
		IF(@HSCode='')
		BEGIN
			SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
			result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
			FROM
			(
			SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
			COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
			FROM
			(SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			ImportLicence.ImportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM ImportLicence
			INNER JOIN ImportLicenceItem ON ImportLicence.Id = ImportLicenceItem.ImportLicenceId
			INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND ImportLicence.Status='Approved'
			AND (ImportLicence.LicenceDate>=@FromDate AND ImportLicence.LicenceDate<=@ToDate))tmp
			GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
			)result
			ORDER BY result.HSCode,result.CompanyName,result.Currency
			OFFSET @PageIndex * @PageSize ROWS
			FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ImportLicence.ImportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ImportLicence
				INNER JOIN ImportLicenceItem ON ImportLicence.Id = ImportLicenceItem.ImportLicenceId
				INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ImportLicence.Status='Approved'
				AND (ImportLicence.LicenceDate>=@FromDate AND ImportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%')tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
			ELSE
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ImportLicence.ImportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ImportLicence
				INNER JOIN ImportLicenceItem ON ImportLicence.Id = ImportLicenceItem.ImportLicenceId
				INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ImportLicence.Status='Approved'
				AND (ImportLicence.LicenceDate>=@FromDate AND ImportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode)tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
		END
	END
	ELSE IF(@FormType='Export Permit')
	BEGIN
		IF(@HSCode='')
		BEGIN
			SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
			result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
			FROM
			(
			SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
			COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
			FROM
			(SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			ExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM ExportPermit
			INNER JOIN ExportPermitItem ON ExportPermit.Id = ExportPermitItem.ExportPermitId
			INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND ExportPermit.Status='Approved'
			AND (ExportPermit.LicenceDate>=@FromDate AND ExportPermit.LicenceDate<=@ToDate))tmp
			GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
			)result
			ORDER BY result.HSCode,result.CompanyName,result.Currency
			OFFSET @PageIndex * @PageSize ROWS
			FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ExportPermit
				INNER JOIN ExportPermitItem ON ExportPermit.Id = ExportPermitItem.ExportPermitId
				INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ExportPermit.Status='Approved'
				AND (ExportPermit.LicenceDate>=@FromDate AND ExportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%')tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
			ELSE
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ExportPermit
				INNER JOIN ExportPermitItem ON ExportPermit.Id = ExportPermitItem.ExportPermitId
				INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ExportPermit.Status='Approved'
				AND (ExportPermit.LicenceDate>=@FromDate AND ExportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode)tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
		END
	END
	ELSE IF(@FormType='Import Permit')
	BEGIN
		-- Grouped on (HSCodeId, Currency) only -- NOT on the company. The legacy
		-- HSCodeReport.rdlc row group is exactly <GroupExpression>=Fields!HSCodeId.Value and
		-- =Fields!Currency.Value (rdlc:1152-1153), and the grid renders no company column
		-- (reportConfigs.ts ImportPermitByHSCodeReport), so keeping the company in the key
		-- split one HS code into one invisible row per buyer, each with a partial Total Value.
		-- The other @FormType branches still group by company because their *HSCodeDetailReport
		-- configs render Company Name off this same procedure.
		IF(@HSCode='')
		BEGIN
			SELECT result.HSCode,result.HSDescription,
			CAST(NULL AS nvarchar(200)) CompanyRegistrationNo,CAST(NULL AS nvarchar(500)) CompanyName,result.Currency,
			result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
			FROM
			(
			SELECT tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency,
			COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
			FROM
			(SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			ImportPermit.ImportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM ImportPermit
			INNER JOIN ImportPermitItem ON ImportPermit.Id = ImportPermitItem.ImportPermitId
			INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND ImportPermit.Status='Approved'
			AND (ImportPermit.LicenceDate>=@FromDate AND ImportPermit.LicenceDate<=@ToDate))tmp
			GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency
			)result
			ORDER BY result.HSCode,result.Currency
			OFFSET @PageIndex * @PageSize ROWS
			FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT result.HSCode,result.HSDescription,
				CAST(NULL AS nvarchar(200)) CompanyRegistrationNo,CAST(NULL AS nvarchar(500)) CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ImportPermit.ImportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ImportPermit
				INNER JOIN ImportPermitItem ON ImportPermit.Id = ImportPermitItem.ImportPermitId
				INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ImportPermit.Status='Approved'
				AND (ImportPermit.LicenceDate>=@FromDate AND ImportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%')tmp
				GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency
				)result
				ORDER BY result.HSCode,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
			ELSE
			BEGIN
				SELECT result.HSCode,result.HSDescription,
				CAST(NULL AS nvarchar(200)) CompanyRegistrationNo,CAST(NULL AS nvarchar(500)) CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ImportPermit.ImportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ImportPermit
				INNER JOIN ImportPermitItem ON ImportPermit.Id = ImportPermitItem.ImportPermitId
				INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ImportPermit.Status='Approved'
				AND (ImportPermit.LicenceDate>=@FromDate AND ImportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode)tmp
				GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency
				)result
				ORDER BY result.HSCode,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
		END
	END
	ELSE IF(@FormType='Border Export Licence')
	BEGIN
		IF(@HSCode='')
		BEGIN
			SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
			result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
			FROM
			(
			SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
			COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
			FROM
			(SELECT BorderExportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			BorderExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM BorderExportLicence
			INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
			INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Pa Tha Ka'
			AND (BorderExportLicence.LicenceDate>=@FromDate AND BorderExportLicence.LicenceDate<=@ToDate)
			AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
			UNION ALL
			SELECT BorderExportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			BorderExportLicence.ExportLicenceNo LicenceNo,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName
			FROM BorderExportLicence
			INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
			INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
			INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Individual Trading'
			AND (BorderExportLicence.LicenceDate>=@FromDate AND BorderExportLicence.LicenceDate<=@ToDate)
			AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END))tmp
			GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
			)result
			ORDER BY result.HSCode,result.CompanyName,result.Currency
			OFFSET @PageIndex * @PageSize ROWS
			FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT BorderExportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderExportLicence
				INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
				INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Pa Tha Ka'
				AND (BorderExportLicence.LicenceDate>=@FromDate AND BorderExportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
				UNION ALL
				SELECT BorderExportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderExportLicence.ExportLicenceNo LicenceNo,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName
				FROM BorderExportLicence
				INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
				INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
				INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Individual Trading'
				AND (BorderExportLicence.LicenceDate>=@FromDate AND BorderExportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END))tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
			ELSE
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT BorderExportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderExportLicence
				INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
				INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Pa Tha Ka'
				AND (BorderExportLicence.LicenceDate>=@FromDate AND BorderExportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
				UNION ALL
				SELECT BorderExportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderExportLicence.ExportLicenceNo LicenceNo,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName
				FROM BorderExportLicence
				INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
				INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
				INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Individual Trading'
				AND (BorderExportLicence.LicenceDate>=@FromDate AND BorderExportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END))tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
		END
	END
	ELSE IF(@FormType='Border Import Licence')
	BEGIN
		IF(@HSCode='')
		BEGIN
			SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
			result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
			FROM
			(
			SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
			COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
			FROM
			(SELECT BorderImportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			BorderImportLicence.ImportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM BorderImportLicence
			INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId
			INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Pa Tha Ka'
			AND (BorderImportLicence.LicenceDate>=@FromDate AND BorderImportLicence.LicenceDate<=@ToDate)
			AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
			UNION ALL
			SELECT BorderImportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			BorderImportLicence.ImportLicenceNo LicenceNo,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName
			FROM BorderImportLicence
			INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId
			INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
			INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Individual Trading'
			AND (BorderImportLicence.LicenceDate>=@FromDate AND BorderImportLicence.LicenceDate<=@ToDate)
			AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END))tmp
			GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
			)result
			ORDER BY result.HSCode,result.CompanyName,result.Currency
			OFFSET @PageIndex * @PageSize ROWS
			FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT BorderImportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderImportLicence.ImportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderImportLicence
				INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId
				INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Pa Tha Ka'
				AND (BorderImportLicence.LicenceDate>=@FromDate AND BorderImportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
				UNION ALL
				SELECT BorderImportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderImportLicence.ImportLicenceNo LicenceNo,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName
				FROM BorderImportLicence
				INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId
				INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
				INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Individual Trading'
				AND (BorderImportLicence.LicenceDate>=@FromDate AND BorderImportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
				)tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
			ELSE
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT BorderImportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderImportLicence.ImportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderImportLicence
				INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId
				INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Pa Tha Ka'
				AND (BorderImportLicence.LicenceDate>=@FromDate AND BorderImportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
				UNION ALL
				SELECT BorderImportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderImportLicence.ImportLicenceNo LicenceNo,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName
				FROM BorderImportLicence
				INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId
				INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
				INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Individual Trading'
				AND (BorderImportLicence.LicenceDate>=@FromDate AND BorderImportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
				)tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
		END
	END
	ELSE IF(@FormType='Border Export Permit')
	BEGIN
		IF(@HSCode='')
		BEGIN
			SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
			result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
			FROM
			(
			SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
			COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
			FROM
			(SELECT BorderExportPermit.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			BorderExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM BorderExportPermit
			INNER JOIN BorderExportPermitItem ON BorderExportPermit.Id = BorderExportPermitItem.BorderExportPermitId
			INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON BorderExportPermitItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND BorderExportPermit.Status='Approved'
			AND (BorderExportPermit.LicenceDate>=@FromDate AND BorderExportPermit.LicenceDate<=@ToDate)
			AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportPermit.SakhanId ELSE @SakhanId END))tmp
			GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
			)result
			ORDER BY result.HSCode,result.CompanyName,result.Currency
			OFFSET @PageIndex * @PageSize ROWS
			FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT BorderExportPermit.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderExportPermit
				INNER JOIN BorderExportPermitItem ON BorderExportPermit.Id = BorderExportPermitItem.BorderExportPermitId
				INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderExportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderExportPermit.Status='Approved'
				AND (BorderExportPermit.LicenceDate>=@FromDate AND BorderExportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportPermit.SakhanId ELSE @SakhanId END))tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
			ELSE
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT BorderExportPermit.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderExportPermit
				INNER JOIN BorderExportPermitItem ON BorderExportPermit.Id = BorderExportPermitItem.BorderExportPermitId
				INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderExportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderExportPermit.Status='Approved'
				AND (BorderExportPermit.LicenceDate>=@FromDate AND BorderExportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportPermit.SakhanId ELSE @SakhanId END))tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
		END
	END
	ELSE IF(@FormType='Border Import Permit')
	BEGIN
		-- @HSCode='' is the By HS Code SUMMARY, whose legacy BorderHSCodeReport.rdlc row group is
		-- (HSCodeId, Currency) with no company column (rdlc:1157-1169) -- so that sub-branch drops
		-- the company from the key. Keeping it split one HS code into one row per buyer, each with
		-- a partial Total Value (measured: 31 rows where the old grouping gives 16).
		-- The Start/End sub-branches below KEEP the company: they also serve the HS Code detail
		-- drill (BorderImportPermitHSCodeDetailReport), whose HSCodeDetailReport.rdlc does render
		-- Company Name. sp_HSCodeReport.GroupsByCompany makes the identical split for the LINQ twin.
		IF(@HSCode='')
		BEGIN
			SELECT result.HSCode,result.HSDescription,
			CAST(NULL AS nvarchar(200)) CompanyRegistrationNo,CAST(NULL AS nvarchar(500)) CompanyName,result.Currency,
			result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
			FROM
			(
			SELECT tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency,
			COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
			FROM
			(SELECT BorderImportPermit.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			BorderImportPermit.ImportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM BorderImportPermit
			INNER JOIN BorderImportPermitItem ON BorderImportPermit.Id = BorderImportPermitItem.BorderImportPermitId
			INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND BorderImportPermit.Status='Approved'
			AND (BorderImportPermit.LicenceDate>=@FromDate AND BorderImportPermit.LicenceDate<=@ToDate)
			AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportPermit.SakhanId ELSE @SakhanId END))tmp
			GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency
			)result
			ORDER BY result.HSCode,result.Currency
			OFFSET @PageIndex * @PageSize ROWS
			FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT BorderImportPermit.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderImportPermit.ImportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderImportPermit
				INNER JOIN BorderImportPermitItem ON BorderImportPermit.Id = BorderImportPermitItem.BorderImportPermitId
				INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderImportPermit.Status='Approved'
				AND (BorderImportPermit.LicenceDate>=@FromDate AND BorderImportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportPermit.SakhanId ELSE @SakhanId END))tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
			ELSE
			BEGIN
				SELECT result.HSCode,result.HSDescription,result.CompanyRegistrationNo,result.CompanyName,result.Currency,
				result.NoOfLicences,result.TotalValue,COUNT(*) OVER() TotalCount
				FROM
				(
				SELECT tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency,
				COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
				FROM
				(SELECT BorderImportPermit.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderImportPermit.ImportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderImportPermit
				INNER JOIN BorderImportPermitItem ON BorderImportPermit.Id = BorderImportPermitItem.BorderImportPermitId
				INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderImportPermit.Status='Approved'
				AND (BorderImportPermit.LicenceDate>=@FromDate AND BorderImportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportPermit.SakhanId ELSE @SakhanId END))tmp
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @FetchSize ROWS ONLY
		OPTION (RECOMPILE);
			END
		END
	END
END
GO

-- ============================================================================
-- sp_ImportPermitListingCurrencyTotals   (file 03_sp_ImportPermitListingCurrencyTotals.sql)
-- ============================================================================
PRINT N'Applying sp_ImportPermitListingCurrencyTotals ...';
GO

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
    --                   Sakhan join/filter), branching on New and Amend/Actual Amend. Any other
    --                   Border ApplyType returns an empty set rather than falling through to the
    --                   non-border branches.
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
        ELSE IF @DbApplyType = N'New'
        BEGIN
            -- Mirrors the 'Border Import Permit' branch of sp_NewReport_pagination. Two things
            -- differ from the Amend branch above and must not be "tidied" into line with it:
            --   * Amount is SUM(items), not TOP 1 -- the New grid's Total Value column sums every
            --     item on the permit, so the footer has to sum the same value.
            --   * the date window is the grid's calendar-date form; converting @ToDate to a
            --     date first is what keeps a '23:59:59' argument out of the following day.
            -- Reproduces BorderNewReport.rdlc's second tablix: one row per currency
            -- ("<CUR>: n licence(s)" + summed amount), with the grand TOTAL added by the wrapper.
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
                WHERE BorderImportPermit.ApplyType = N'New' AND BorderImportPermit.Status = 'Approved'
                    AND ((@FromDate IS NULL) OR BorderImportPermit.CreatedDate >= @FromDate)
                    AND ((@ToDate IS NULL) OR BorderImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
                    AND BorderImportPermit.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                    AND BorderImportPermit.SakhanId = (CASE WHEN @SakhanId = 0 THEN BorderImportPermit.SakhanId ELSE @SakhanId END)
            ) d
            GROUP BY ISNULL(d.Currency, N'')
            OPTION (RECOMPILE);
        END
        ELSE
        BEGIN
            -- Border Cancel / Extension footers are not implemented: return an EMPTY correctly-shaped
            -- result set rather than falling through to a non-border branch (which would count the
            -- wrong table).
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

GO

-- ============================================================================
-- sp_ExportPermitListingCurrencyTotals   (file 04_sp_ExportPermitListingCurrencyTotals.sql)
-- ============================================================================
PRINT N'Applying sp_ExportPermitListingCurrencyTotals ...';
GO

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

    -- Callers name the Actual Amendment branch 'ActualAmend' while the database stores
    -- 'Actual Amend' (WITH a space); @DbApplyType normalises the two spellings. Without it the
    -- Export Permit / Border Export Permit Actual Amendment footers matched no rows at all.
    DECLARE @DbApplyType nvarchar(20) = CASE WHEN @ApplyType = N'ActualAmend' THEN N'Actual Amend' ELSE @ApplyType END;

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
        IF @DbApplyType = N'Amend' OR @DbApplyType = N'Actual Amend'
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
                WHERE BorderExportPermit.ApplyType = @DbApplyType AND BorderExportPermit.Status = 'Approved'
                    AND (BorderExportPermit.CreatedDate >= @FromDate AND BorderExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
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
                    AND (BorderExportPermit.CreatedDate >= @FromDate AND BorderExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
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
                    -- Flipped to the calendar-date form together with its grid
                    -- (sp_NewReport_pagination, Border Export Permit branch), which carried the
                    -- same extra-day bug: DATEADD(day, 1, ...) over a '23:59:59' @ToDate reached
                    -- a whole day further than the old sp_NewReport's 'CreatedDate <= @ToDate'.
                    AND ((@FromDate IS NULL) OR BorderExportPermit.CreatedDate >= @FromDate)
                    AND ((@ToDate IS NULL) OR BorderExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
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
        IF @DbApplyType = N'Amend' OR @DbApplyType = N'Actual Amend'
        BEGIN
            SELECT ISNULL(d.Currency, N'') AS Currency, COUNT(*) AS NoOfLicences, ISNULL(SUM(d.Amount), 0) AS TotalValue
            FROM (
                SELECT
                    (SELECT TOP 1 currency.Code FROM ExportPermitItem
                        INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
                        WHERE ExportPermitItem.ExportPermitId = ExportPermit.Id
                        ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo) AS Currency,
                    (SELECT TOP 1 ExportPermitItem.Amount FROM ExportPermitItem
                        WHERE ExportPermitItem.ExportPermitId = ExportPermit.Id
                        ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo) AS Amount
                FROM ExportPermit
                    INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
                    INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
                WHERE ExportPermit.ApplyType = @DbApplyType AND ExportPermit.Status = 'Approved'
                    AND (ExportPermit.CreatedDate >= @FromDate AND ExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
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
                        WHERE ExportPermitItem.ExportPermitId = ExportPermit.Id
                        ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo) AS Currency,
                    (SELECT TOP 1 ExportPermitItem.Amount FROM ExportPermitItem
                        WHERE ExportPermitItem.ExportPermitId = ExportPermit.Id
                        ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo) AS Amount
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
                        WHERE ExportPermitItem.ExportPermitId = ExportPermit.Id
                        ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo) AS Currency,
                    -- New / Extension sum every item (matching the legacy sp_NewReport /
                    -- sp_ExtensionReport and the grid procedures), so no ORDER BY here: a
                    -- scalar aggregate cannot be ordered by a column outside its select list.
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

GO

PRINT N'All four procedures applied. Now run VerifyDeployment.sql before deploying the application.';
GO

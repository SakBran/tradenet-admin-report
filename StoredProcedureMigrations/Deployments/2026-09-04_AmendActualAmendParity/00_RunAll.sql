/* =====================================================================================
   Amend / Actual Amendment parity deployment - 2026-09-04
   Run this ONE file to apply all six procedures in the correct order, or run the
   numbered files 01..06 individually. Either way: PROCEDURES FIRST, APPLICATION SECOND.

   Target database: TradeNetDB  (NOT ReportTemplateDB - that one only holds the Excel
   export job queue; deploying report procedures into it is a known trap.)

   Generated from the repository files of the same name; see README.md in this folder.
   ===================================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE [TradeNetDB];
GO

-- Wrong-database guard: dbo.sp_ActualAmendReport is the legacy Tradenet 2.0 procedure and
-- exists only in the report database. Stop before creating anything in the wrong place.
IF OBJECT_ID(N'dbo.sp_ActualAmendReport', N'P') IS NULL
BEGIN
    RAISERROR(N'Wrong database: dbo.sp_ActualAmendReport was not found in [%s]. Connect to TradeNetDB and run again.', 16, 1, DB_NAME());
    SET NOEXEC ON;
END
GO

-- ============================================================================
-- sp_ActualAmendReport_pagination   (file 01_sp_ActualAmendReport_pagination.sql)
-- ============================================================================
PRINT N'Applying sp_ActualAmendReport_pagination ...';
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_ActualAmendReport_pagination]
    @FormType nvarchar(50) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @AmendRemarkId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @SakhanId int = 0,
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL,
    @IncludeTotalCount bit = 1
AS
BEGIN
    SET NOCOUNT ON;

    -- Date window mirrors the original dbo.sp_ActualAmendReport exactly: CreatedDate >= @FromDate AND CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)),
    -- i.e. the whole selected calendar day. Controllers pass ToDate as request.ToDate.Date.
    -- CONVERT(date, ...) is what makes this safe: the plain '< DATEADD(day, 1, @ToDate)' form admits
    -- the whole NEXT day whenever @ToDate carries a time (commit e88c13e; Actual Amend reports showed one extra day).

    DECLARE @ps bigint = CASE
        WHEN ISNULL(@PageSize,0) <= 0 THEN 9223372036854775807
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
        ELSE @PageSize END;
    DECLARE @off bigint = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 0 ELSE ISNULL(@PageIndex,0) * CAST(@PageSize AS bigint) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder,'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'Date', N'SectionCode', N'SectionName', N'OldLicenceNo', N'LicenceNo', N'sDate', N'CompanyRegistrationNo', N'CompanyName', N'UnitLevel', N'StreetNumberStreetName', N'QuarterCityTownship', N'State', N'Country', N'PostalCode')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir
            + CASE WHEN @SortColumn = N'Date' THEN N'' ELSE N', [Date] ASC' END
            + CASE WHEN @SortColumn = N'LicenceNo' THEN N'' ELSE N', [LicenceNo] ASC' END;
    ELSE
        SET @ob = N'[Date] ASC, [LicenceNo] ASC';

    DECLARE @cntpart nvarchar(max);
    DECLARE @sql nvarchar(max);

    -- TotalCount only when requested, computed over the UN-paged base (no subqueries) as a separate scalar.
    IF @FormType = N'Export Licence'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''Actual Amend'' AND ExportLicence.Status=''Approved''
		AND ((@FromDate IS NULL) OR ExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ExportLicenceItem
		INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(ExportLicenceItem.Amount,0) FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ExportLicence.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldExportLicenceNo OldLicenceNo,
ExportLicenceNo LicenceNo,
CONVERT(varchar,ExportLicence.CreatedDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
ExportLicence.Id AS __k_Id
        FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''Actual Amend'' AND ExportLicence.Status=''Approved''
		AND ((@FromDate IS NULL) OR ExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @FormType = N'Import Permit'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ImportPermit
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType=''Actual Amend'' AND ImportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR ImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ImportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ImportPermitItem
		INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
		WHERE ImportPermitItem.ImportPermitId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ImportPermitItem
		INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id
		WHERE ImportPermitItem.ImportPermitId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(ImportPermitItem.Amount,0) FROM ImportPermitItem
		WHERE ImportPermitItem.ImportPermitId=pg.__k_Id) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ImportPermit.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldImportPermitNo OldLicenceNo,
ImportPermitNo LicenceNo,
CONVERT(varchar,ImportPermit.CreatedDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
ImportPermit.Id AS __k_Id
        FROM ImportPermit
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType=''Actual Amend'' AND ImportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR ImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ImportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
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
		WHERE ApplyType=''Actual Amend'' AND ExportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR ExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ExportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ExportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ExportPermitItem
		INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
		WHERE ExportPermitItem.ExportPermitId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ExportPermitItem
		INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id
		WHERE ExportPermitItem.ExportPermitId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(ExportPermitItem.Amount,0) FROM ExportPermitItem
		WHERE ExportPermitItem.ExportPermitId=pg.__k_Id) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ExportPermit.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldExportPermitNo OldLicenceNo,
ExportPermitNo LicenceNo,
CONVERT(varchar,ExportPermit.CreatedDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
ExportPermit.Id AS __k_Id
        FROM ExportPermit
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType=''Actual Amend'' AND ExportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR ExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ExportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ExportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
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
		WHERE ApplyType=''Actual Amend'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderExportLicence.Id FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Actual Amend'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
	) tmp OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM BorderExportLicenceItem
		INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(BorderExportLicenceItem.Amount,0) FROM BorderExportLicenceItem
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
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportLicence.Id AS __k_Id
        FROM BorderExportLicence
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Actual Amend'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
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
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportLicence.Id AS __k_Id
        FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Actual Amend'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
        ) u
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @FormType = N'Border Import Licence'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM (
		SELECT BorderImportLicence.Id FROM BorderImportLicence
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Actual Amend'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND ((@FromDate IS NULL) OR BorderImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderImportLicence.Id FROM BorderImportLicence
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Actual Amend'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND ((@FromDate IS NULL) OR BorderImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
	) tmp OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM BorderImportLicenceItem
		INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(BorderImportLicenceItem.Amount,0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=pg.__k_Id) Amount, @__total AS TotalCount
    FROM (
        SELECT * FROM (
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
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportLicence.Id AS __k_Id
        FROM BorderImportLicence
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Actual Amend'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND ((@FromDate IS NULL) OR BorderImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
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
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportLicence.Id AS __k_Id
        FROM BorderImportLicence
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Actual Amend'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND ((@FromDate IS NULL) OR BorderImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
        ) u
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
		WHERE ApplyType=''Actual Amend'' AND BorderExportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR BorderExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM BorderExportPermitItem
		INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM BorderExportPermitItem
		INNER JOIN HSCode ON BorderExportPermitItem.HSCodeId = HSCode.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(BorderExportPermitItem.Amount,0) FROM BorderExportPermitItem
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
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportPermit.Id AS __k_Id
        FROM BorderExportPermit
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		WHERE ApplyType=''Actual Amend'' AND BorderExportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR BorderExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportPermit.AmendRemarkId ELSE @AmendRemarkId END)
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
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM BorderImportPermit
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		WHERE ApplyType=''Actual Amend'' AND BorderImportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR BorderImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM BorderImportPermitItem
		INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM BorderImportPermitItem
		INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(BorderImportPermitItem.Amount,0) FROM BorderImportPermitItem
		WHERE BorderImportPermitItem.BorderImportPermitId=pg.__k_Id) Amount, @__total AS TotalCount
    FROM (
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
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportPermit.Id AS __k_Id
        FROM BorderImportPermit
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		WHERE ApplyType=''Actual Amend'' AND BorderImportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR BorderImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''Actual Amend'' AND ImportLicence.Status=''Approved''
		AND ((@FromDate IS NULL) OR ImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ImportLicenceItem
		INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
		WHERE ImportLicenceItem.ImportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ImportLicenceItem
		INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id
		WHERE ImportLicenceItem.ImportLicenceId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(ImportLicenceItem.Amount,0) FROM ImportLicenceItem
		WHERE ImportLicenceItem.ImportLicenceId=pg.__k_Id) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ImportLicence.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldImportLicenceNo OldLicenceNo,
ImportLicenceNo LicenceNo,
CONVERT(varchar,ImportLicence.CreatedDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
ImportLicence.Id AS __k_Id
        FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''Actual Amend'' AND ImportLicence.Status=''Approved''
		AND ((@FromDate IS NULL) OR ImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END

    EXEC sp_executesql @sql, N'@FormType nvarchar(50), @FromDate datetime, @ToDate datetime, @ExportImportSectionId int, @AmendRemarkId int, @CompanyRegistrationNo nvarchar(50), @SakhanId int, @off bigint, @ps bigint', @FormType=@FormType, @FromDate=@FromDate, @ToDate=@ToDate, @ExportImportSectionId=@ExportImportSectionId, @AmendRemarkId=@AmendRemarkId, @CompanyRegistrationNo=@CompanyRegistrationNo, @SakhanId=@SakhanId, @off=@off, @ps=@ps;
END

GO

-- ============================================================================
-- sp_AmendReport_pagination   (file 02_sp_AmendReport_pagination.sql)
-- ============================================================================
PRINT N'Applying sp_AmendReport_pagination ...';
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_AmendReport_pagination]
    @FormType nvarchar(50) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @AmendRemarkId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @SakhanId int = 0,
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL,
    @IncludeTotalCount bit = 1
AS
BEGIN
    SET NOCOUNT ON;

    -- Date window mirrors the original dbo.sp_AmendReport exactly: CreatedDate >= @FromDate AND CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)),
    -- i.e. the whole selected calendar day. Controllers pass ToDate as request.ToDate.Date.
    -- CONVERT(date, ...) is what makes this safe: the plain '< DATEADD(day, 1, @ToDate)' form admits
    -- the whole NEXT day whenever @ToDate carries a time (commit e88c13e; Amend reports showed one extra day).

    DECLARE @ps bigint = CASE
        WHEN ISNULL(@PageSize,0) <= 0 THEN 9223372036854775807
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
        ELSE @PageSize END;
    DECLARE @off bigint = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 0 ELSE ISNULL(@PageIndex,0) * CAST(@PageSize AS bigint) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder,'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'Date', N'SectionCode', N'SectionName', N'OldLicenceNo', N'LicenceNo', N'sDate', N'CompanyRegistrationNo', N'CompanyName', N'UnitLevel', N'StreetNumberStreetName', N'QuarterCityTownship', N'State', N'Country', N'PostalCode')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir
            + CASE WHEN @SortColumn = N'Date' THEN N'' ELSE N', [Date] ASC' END
            + CASE WHEN @SortColumn = N'LicenceNo' THEN N'' ELSE N', [LicenceNo] ASC' END;
    ELSE
        SET @ob = N'[Date] ASC, [LicenceNo] ASC';

    DECLARE @cntpart nvarchar(max);
    DECLARE @sql nvarchar(max);

    -- TotalCount only when requested, computed over the UN-paged base (no subqueries) as a separate scalar.
    IF @FormType = N'Export Licence'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''Amend'' AND ExportLicence.Status=''Approved''
		AND ((@FromDate IS NULL) OR ExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ExportLicenceItem
		INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(ExportLicenceItem.Amount,0) FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ExportLicence.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldExportLicenceNo OldLicenceNo,
ExportLicenceNo LicenceNo,
CONVERT(varchar,ExportLicence.CreatedDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
ExportLicence.Id AS __k_Id
        FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''Amend'' AND ExportLicence.Status=''Approved''
		AND ((@FromDate IS NULL) OR ExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @FormType = N'Import Permit'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ImportPermit
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType=''Amend'' AND ImportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR ImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ImportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ImportPermitItem
		INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
		WHERE ImportPermitItem.ImportPermitId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ImportPermitItem
		INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id
		WHERE ImportPermitItem.ImportPermitId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(ImportPermitItem.Amount,0) FROM ImportPermitItem
		WHERE ImportPermitItem.ImportPermitId=pg.__k_Id) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ImportPermit.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldImportPermitNo OldLicenceNo,
ImportPermitNo LicenceNo,
CONVERT(varchar,ImportPermit.CreatedDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
ImportPermit.Id AS __k_Id
        FROM ImportPermit
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType=''Amend'' AND ImportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR ImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ImportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
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
		WHERE ApplyType=''Amend'' AND ExportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR ExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ExportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ExportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ExportPermitItem
		INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
		WHERE ExportPermitItem.ExportPermitId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ExportPermitItem
		INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id
		WHERE ExportPermitItem.ExportPermitId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(ExportPermitItem.Amount,0) FROM ExportPermitItem
		WHERE ExportPermitItem.ExportPermitId=pg.__k_Id) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ExportPermit.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldExportPermitNo OldLicenceNo,
ExportPermitNo LicenceNo,
CONVERT(varchar,ExportPermit.CreatedDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
ExportPermit.Id AS __k_Id
        FROM ExportPermit
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType=''Amend'' AND ExportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR ExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ExportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ExportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
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
		WHERE ApplyType=''Amend'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderExportLicence.Id FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Amend'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
	) tmp OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM BorderExportLicenceItem
		INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(BorderExportLicenceItem.Amount,0) FROM BorderExportLicenceItem
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
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportLicence.Id AS __k_Id
        FROM BorderExportLicence
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Amend'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
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
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportLicence.Id AS __k_Id
        FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Amend'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND ((@FromDate IS NULL) OR BorderExportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
        ) u
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @FormType = N'Border Import Licence'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM (
		SELECT BorderImportLicence.Id FROM BorderImportLicence
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Amend'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND ((@FromDate IS NULL) OR BorderImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderImportLicence.Id FROM BorderImportLicence
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Amend'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND ((@FromDate IS NULL) OR BorderImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
	) tmp OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM BorderImportLicenceItem
		INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(BorderImportLicenceItem.Amount,0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=pg.__k_Id) Amount, @__total AS TotalCount
    FROM (
        SELECT * FROM (
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
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportLicence.Id AS __k_Id
        FROM BorderImportLicence
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Amend'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND ((@FromDate IS NULL) OR BorderImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
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
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportLicence.Id AS __k_Id
        FROM BorderImportLicence
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Amend'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND ((@FromDate IS NULL) OR BorderImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
        ) u
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
		WHERE ApplyType=''Amend'' AND BorderExportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR BorderExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM BorderExportPermitItem
		INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM BorderExportPermitItem
		INNER JOIN HSCode ON BorderExportPermitItem.HSCodeId = HSCode.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(BorderExportPermitItem.Amount,0) FROM BorderExportPermitItem
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
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportPermit.Id AS __k_Id
        FROM BorderExportPermit
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		WHERE ApplyType=''Amend'' AND BorderExportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR BorderExportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderExportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportPermit.AmendRemarkId ELSE @AmendRemarkId END)
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
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM BorderImportPermit
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		WHERE ApplyType=''Amend'' AND BorderImportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR BorderImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM BorderImportPermitItem
		INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM BorderImportPermitItem
		INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(BorderImportPermitItem.Amount,0) FROM BorderImportPermitItem
		WHERE BorderImportPermitItem.BorderImportPermitId=pg.__k_Id) Amount, @__total AS TotalCount
    FROM (
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
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportPermit.Id AS __k_Id
        FROM BorderImportPermit
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		WHERE ApplyType=''Amend'' AND BorderImportPermit.Status=''Approved''
		AND ((@FromDate IS NULL) OR BorderImportPermit.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR BorderImportPermit.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''Amend'' AND ImportLicence.Status=''Approved''
		AND ((@FromDate IS NULL) OR ImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ImportLicenceItem
		INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
		WHERE ImportLicenceItem.ImportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ImportLicenceItem
		INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id
		WHERE ImportLicenceItem.ImportLicenceId=pg.__k_Id) HSCode,
        (SELECT top 1 ISNULL(ImportLicenceItem.Amount,0) FROM ImportLicenceItem
		WHERE ImportLicenceItem.ImportLicenceId=pg.__k_Id) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ImportLicence.CreatedDate Date,
section.Code SectionCode,
section.Name SectionName,
OldImportLicenceNo OldLicenceNo,
ImportLicenceNo LicenceNo,
CONVERT(varchar,ImportLicence.CreatedDate,103) sDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
UnitLevel,
StreetNumberStreetName,
QuarterCityTownship,
State,
Country,
PostalCode,
ImportLicence.Id AS __k_Id
        FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''Amend'' AND ImportLicence.Status=''Approved''
		AND ((@FromDate IS NULL) OR ImportLicence.CreatedDate >= @FromDate)
		AND ((@ToDate IS NULL) OR ImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END

    EXEC sp_executesql @sql, N'@FormType nvarchar(50), @FromDate datetime, @ToDate datetime, @ExportImportSectionId int, @AmendRemarkId int, @CompanyRegistrationNo nvarchar(50), @SakhanId int, @off bigint, @ps bigint', @FormType=@FormType, @FromDate=@FromDate, @ToDate=@ToDate, @ExportImportSectionId=@ExportImportSectionId, @AmendRemarkId=@AmendRemarkId, @CompanyRegistrationNo=@CompanyRegistrationNo, @SakhanId=@SakhanId, @off=@off, @ps=@ps;
END

GO

-- ============================================================================
-- sp_ExportLicenceListingCurrencyTotals   (file 03_sp_ExportLicenceListingCurrencyTotals.sql)
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
                    AND (ExportLicence.CreatedDate >= @FromDate AND ExportLicence.CreatedDate <= @ToDate)
                    AND ExportLicence.ExportImportSectionId = (CASE WHEN @ExportImportSectionId = 0 THEN ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                    AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
            ) d
            GROUP BY ISNULL(d.Currency, N'')
            OPTION (RECOMPILE);
        END
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
                    -- TODO(follow-up): still DATEADD because the Border Export Permit New GRID
                    -- (sp_NewReport_pagination, Border Export Permit branch) has the same extra-day
                    -- form; flip both together or this footer stops matching its grid.
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
        IF @DbApplyType = N'Amend' OR @DbApplyType = N'Actual Amend'
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

GO

-- ============================================================================
-- sp_ImportLicenceListingCurrencyTotals   (file 05_sp_ImportLicenceListingCurrencyTotals.sql)
-- ============================================================================
PRINT N'Applying sp_ImportLicenceListingCurrencyTotals ...';
GO

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

GO

-- ============================================================================
-- sp_ImportPermitListingCurrencyTotals   (file 06_sp_ImportPermitListingCurrencyTotals.sql)
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

SET NOEXEC OFF;
GO
PRINT N'All six procedures applied. Now run VerifyDeployment.sql before deploying the application.';
GO

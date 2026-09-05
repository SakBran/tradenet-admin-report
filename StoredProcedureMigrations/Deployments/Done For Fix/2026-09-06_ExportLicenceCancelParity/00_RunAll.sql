/* =====================================================================================
   Export Licence Cancellation parity deployment - 2026-09-06
   Run this ONE file to apply both procedures, or run 01 and 02 individually.
   Either way: PROCEDURES FIRST, APPLICATION SECOND.

   Target database: TradeNetDB  (NOT ReportTemplateDB - that one only holds the Excel
   export job queue; deploying report procedures into it is a known trap.)

   What changes: the three correlated TOP 1 sub-selects over ExportLicenceItem in the
   @FormType='Export Licence' branch of sp_CancelReport_pagination, and the two in the
   non-Border @ApplyType='Cancel' branch of sp_ExportLicenceListingCurrencyTotals, now
   carry
       ORDER BY ExportLicenceItem.Id, ExportLicenceItem.UniqueId
   the table's clustered primary key. Currency / HS Code / Total Value therefore come
   from the SAME item, and the new per-currency footer is the sum of the rows on screen.
   The legacy sp_CancelReport uses a bare TOP 1 and returns whatever the plan's index
   order yields; (Id, UniqueId) is the key measured to reproduce it (466/466 over
   Aug-2025). Do NOT copy the Export Permit key (HSCodeId, ItemNo) - the effective key
   differs per item table.

   Deliberately untouched: every Border Export Licence branch, and the Cancel branch's
   CreatedDate <= @ToDate window (the grid's count and base predicate use the same form,
   so grid and footer already agree).

   Generated from the repository files of the same name; see README.md in this folder.
   ===================================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE [TradeNetDB];
GO

-- Wrong-database guard: dbo.sp_CancelReport is the legacy Tradenet 2.0 procedure and
-- exists only in the report database. Stop before creating anything in the wrong place.
-- IF OBJECT_ID(N'dbo.sp_CancelReport', N'P') IS NULL
-- BEGIN
--     RAISERROR(N'Wrong database: dbo.sp_CancelReport was not found in [%s]. Connect to TradeNetDB and run again.', 16, 1, DB_NAME());
--     SET NOEXEC ON;
-- END
-- GO

-- ============================================================================
-- sp_CancelReport_pagination   (file 01_sp_CancelReport_pagination.sql)
-- ============================================================================
PRINT N'Applying sp_CancelReport_pagination ...';
GO

﻿﻿CREATE OR ALTER PROCEDURE [dbo].[sp_CancelReport_pagination]
    @FormType nvarchar(50) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
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

    DECLARE @ps bigint = CASE
        WHEN ISNULL(@PageSize,0) <= 0 THEN 9223372036854775807
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
        ELSE @PageSize END;
    DECLARE @off bigint = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 0 ELSE ISNULL(@PageIndex,0) * CAST(@PageSize AS bigint) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder,'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'Date', N'SectionCode', N'SectionName', N'OldLicenceNo', N'LicenceNo', N'sDate', N'CompanyRegistrationNo', N'CompanyName', N'UnitLevel', N'StreetNumberStreetName', N'QuarterCityTownship', N'State', N'Country', N'PostalCode', N'Remark')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir
            + CASE WHEN @SortColumn = N'Date' THEN N'' ELSE N', [Date] ASC' END
            + CASE WHEN @SortColumn = N'LicenceNo' THEN N'' ELSE N', [LicenceNo] ASC' END;
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
		WHERE ApplyType=''Cancel'' AND ImportPermit.Status=''Approved''
		AND (ImportPermit.CreatedDate>=@FromDate AND ImportPermit.CreatedDate<=@ToDate)
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ImportPermitItem
		INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
		WHERE ImportPermitItem.ImportPermitId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ImportPermitItem
		INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id
		WHERE ImportPermitItem.ImportPermitId=pg.__k_Id) HSCode,
        (SELECT top 1  ISNULL(ImportPermitItem.Amount,0) FROM ImportPermitItem
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
ImportPermit.Remark,
ImportPermit.Id AS __k_Id
        FROM ImportPermit
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType=''Cancel'' AND ImportPermit.Status=''Approved''
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
		WHERE ApplyType=''Cancel'' AND ExportPermit.Status=''Approved''
		AND (ExportPermit.CreatedDate>=@FromDate AND ExportPermit.CreatedDate<=@ToDate)
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        -- ORDER BY HSCodeId, ItemNo on all three sub-selects. The legacy sp_CancelReport's
        -- bare TOP 1 is non-deterministic: it returns whatever the plan's index order yields,
        -- which is the IX_ExportPermitItem_ReportCover seek order (ExportPermitId, HSCodeId,
        -- ItemNo). Measured against the legacy procedure over 2025, this key reproduces it on
        -- 17/17 Export Permit cancellations; ORDER BY ItemNo scores 16/17 and ORDER BY Id
        -- 14/17 -- Id is a char(36) GUID string with no relation to item order. (An earlier
        -- pass ordered by Id and produced the customer-reported 5,769.2300 / USD:10,038.1050
        -- where the old report shows 27,230.7600 / USD:33,835.1200.)
        -- sp_ExportPermitListingCurrencyTotals must use the IDENTICAL expression or the
        -- footer stops being the sum of the rows on screen.
        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ExportPermitItem
		INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
		WHERE ExportPermitItem.ExportPermitId=pg.__k_Id ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo) Currency,
        (SELECT top 1 HSCode.Code FROM ExportPermitItem
		INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id
		WHERE ExportPermitItem.ExportPermitId=pg.__k_Id ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo) HSCode,
        (SELECT top 1  ISNULL(ExportPermitItem.Amount,0) FROM ExportPermitItem
		WHERE ExportPermitItem.ExportPermitId=pg.__k_Id ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
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
ExportPermit.Remark,
ExportPermit.Id AS __k_Id
        FROM ExportPermit
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType=''Cancel'' AND ExportPermit.Status=''Approved''
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
		WHERE ApplyType=''Cancel'' AND ExportLicence.Status=''Approved''
		AND (ExportLicence.CreatedDate>=@FromDate AND ExportLicence.CreatedDate<=@ToDate)
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        -- Currency / HS Code / Total Value all come from ONE item: an explicit ORDER BY
        -- (Id, UniqueId) on all three sub-selects. That is ExportLicenceItem's clustered PK,
        -- and the key measured to reproduce the legacy sp_CancelReport (466/466 over
        -- Aug-2025). The legacy bare TOP 1 returns whatever the plan's index order yields, so
        -- the three sub-selects could each land on a different item. Do NOT copy the Export
        -- Permit key (HSCodeId, ItemNo) here: the effective key differs per item table. The
        -- Cancel branch of sp_ExportLicenceListingCurrencyTotals MUST use the identical
        -- expression, or the footer stops being the sum of the rows on screen.
        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id ORDER BY ExportLicenceItem.Id, ExportLicenceItem.UniqueId) Currency,
        (SELECT top 1 HSCode.Code FROM ExportLicenceItem
		INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id ORDER BY ExportLicenceItem.Id, ExportLicenceItem.UniqueId) HSCode,
        (SELECT top 1  ISNULL(ExportLicenceItem.Amount,0) FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id ORDER BY ExportLicenceItem.Id, ExportLicenceItem.UniqueId) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
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
ExportLicence.Remark,
ExportLicence.Id AS __k_Id
        FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''Cancel'' AND ExportLicence.Status=''Approved''
		AND (ExportLicence.CreatedDate>=@FromDate AND ExportLicence.CreatedDate<=@ToDate)
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
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
		WHERE ApplyType=''Cancel'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderExportLicence.Id FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Cancel'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
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
        (SELECT top 1  ISNULL(BorderExportLicenceItem.Amount,0) FROM BorderExportLicenceItem
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
BorderExportLicence.Remark,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportLicence.Id AS __k_Id
        FROM BorderExportLicence
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Cancel'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
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
BorderExportLicence.Remark,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportLicence.Id AS __k_Id
        FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Cancel'' AND BorderExportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
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
		WHERE ApplyType=''Cancel'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderImportLicence.Id FROM BorderImportLicence
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Cancel'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
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
        (SELECT top 1  ISNULL(BorderImportLicenceItem.Amount,0) FROM BorderImportLicenceItem
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
BorderImportLicence.Remark,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportLicence.Id AS __k_Id
        FROM BorderImportLicence
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Cancel'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Pa Tha Ka''
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
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
BorderImportLicence.Remark,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportLicence.Id AS __k_Id
        FROM BorderImportLicence
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType=''Cancel'' AND BorderImportLicence.Status=''Approved'' AND CardType=''Individual Trading''
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
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
		WHERE ApplyType=''Cancel'' AND BorderExportPermit.Status=''Approved''
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
        (SELECT top 1  ISNULL(BorderExportPermitItem.Amount,0) FROM BorderExportPermitItem
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
BorderExportPermit.Remark,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportPermit.Id AS __k_Id
        FROM BorderExportPermit
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		WHERE ApplyType=''Cancel'' AND BorderExportPermit.Status=''Approved''
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
		WHERE ApplyType=''Cancel'' AND BorderImportPermit.Status=''Approved''
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
        (SELECT top 1  ISNULL(BorderImportPermitItem.Amount,0) FROM BorderImportPermitItem
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
BorderImportPermit.Remark,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportPermit.Id AS __k_Id
        FROM BorderImportPermit
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		WHERE ApplyType=''Cancel'' AND BorderImportPermit.Status=''Approved''
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
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''Cancel'' AND ImportLicence.Status=''Approved''
		AND (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate)
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ImportLicenceItem
		INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
		WHERE ImportLicenceItem.ImportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ImportLicenceItem
		INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id
		WHERE ImportLicenceItem.ImportLicenceId=pg.__k_Id) HSCode,
        (SELECT top 1  ISNULL(ImportLicenceItem.Amount,0) FROM ImportLicenceItem
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
ImportLicence.Remark,
ImportLicence.Id AS __k_Id
        FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType=''Cancel'' AND ImportLicence.Status=''Approved''
		AND (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate)
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END

    EXEC sp_executesql @sql, N'@FormType nvarchar(50), @FromDate datetime, @ToDate datetime, @ExportImportSectionId int, @CompanyRegistrationNo nvarchar(50), @SakhanId int, @off bigint, @ps bigint', @FormType=@FormType, @FromDate=@FromDate, @ToDate=@ToDate, @ExportImportSectionId=@ExportImportSectionId, @CompanyRegistrationNo=@CompanyRegistrationNo, @SakhanId=@SakhanId, @off=@off, @ps=@ps;
END
GO

-- ============================================================================
-- sp_ExportLicenceListingCurrencyTotals   (file 02_sp_ExportLicenceListingCurrencyTotals.sql)
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
                    -- Same item pick as the Cancel grid in sp_CancelReport_pagination: TOP 1
                    -- ordered by (Id, UniqueId), the clustered PK. Both expressions must stay
                    -- byte-identical or this footer stops being the sum of the visible rows.
                    (SELECT TOP 1 currency.Code FROM ExportLicenceItem
                        INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
                        WHERE ExportLicenceItem.ExportLicenceId = ExportLicence.Id
                        ORDER BY ExportLicenceItem.Id, ExportLicenceItem.UniqueId) AS Currency,
                    (SELECT TOP 1 ExportLicenceItem.Amount FROM ExportLicenceItem
                        WHERE ExportLicenceItem.ExportLicenceId = ExportLicence.Id
                        ORDER BY ExportLicenceItem.Id, ExportLicenceItem.UniqueId) AS Amount
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

PRINT N'Both procedures applied. Now run VerifyDeployment.sql.';
GO

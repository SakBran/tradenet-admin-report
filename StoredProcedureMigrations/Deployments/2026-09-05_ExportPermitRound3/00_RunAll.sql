/* =====================================================================================
   Export Permit round-3 parity deployment - 2026-09-05
   Run this ONE file to apply both procedures, or run the numbered files 01..02
   individually. Either way: PROCEDURES FIRST, APPLICATION SECOND.

   Target database: TradeNetDB  (NOT ReportTemplateDB - that one only holds the Excel
   export job queue; deploying report procedures into it is a known trap.)

   What changes:
     01 sp_CancelReport_pagination  - Export Permit branch: the three TOP 1 sub-selects
        (Currency / HSCode / Amount) gain ORDER BY ExportPermitItem.Id, so a multi-item
        permit can no longer show three values taken from three different items, and the
        grid agrees with sp_ExportPermitListingCurrencyTotals (which already orders by Id).
     02 sp_VoucherReport_pagination - Export Permit branch: ExchangeRate / TotalCIF are
        emitted as 0 instead of NULL. The ExportPermit table has no such columns; the old
        app's non-nullable decimals defaulted to 0 and production's VoucherReport.rdlc
        prints 0, so the new Total CIF / Exchange Rate columns must print 0, not N/A.

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

CREATE OR ALTER PROCEDURE [dbo].[sp_CancelReport_pagination]
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

        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id) Currency,
        (SELECT top 1 HSCode.Code FROM ExportLicenceItem
		INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id) HSCode,
        (SELECT top 1  ISNULL(ExportLicenceItem.Amount,0) FROM ExportLicenceItem
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
-- sp_VoucherReport_pagination   (file 02_sp_VoucherReport_pagination.sql)
-- ============================================================================
PRINT N'Applying sp_VoucherReport_pagination ...';
GO

-- The OUTER APPLYs below read the materialized (indexed) per-currency views WITH (NOEXPAND).
-- Indexed-view access requires the procedure to be CREATED with QUOTED_IDENTIFIER ON and
-- ANSI_NULLS ON (both are captured at create time and reapplied on every execution, overriding
-- the connection). Deploying without this header bakes in QUOTED_IDENTIFIER OFF and every call
-- then fails with: Msg 1934 "SELECT failed because the following SET options have incorrect
-- settings: 'QUOTED_IDENTIFIER' ... for use with indexed views". Keep these batches.
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_VoucherReport_pagination]
    @FormType nvarchar(50) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @PaymentType nvarchar(50) = N'',
    @ApplyType nvarchar(20) = N'',
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
    -- Indexed-view access via WITH (NOEXPAND) also requires ARITHABORT ON at execution time.
    -- Unlike QUOTED_IDENTIFIER/ANSI_NULLS this is NOT captured at create time, and .NET SqlClient
    -- connects with ARITHABORT OFF, so set it here for the dynamic SQL EXEC below.
    SET ARITHABORT ON;

    DECLARE @ps bigint = CASE
        WHEN ISNULL(@PageSize,0) <= 0 THEN 9223372036854775807
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
        ELSE @PageSize END;
    DECLARE @off bigint = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 0 ELSE ISNULL(@PageIndex,0) * CAST(@PageSize AS bigint) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder,'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'ApplicationNo', N'ApplicationDate', N'ApprovedUser', N'Date', N'sDate', N'SectionCode', N'ApplyType', N'OldLicenceNo', N'LicenceNo', N'LicenceDate', N'sLicenceDate', N'CompanyRegistrationNo', N'CompanyName', N'VoucherNo', N'VoucherDate', N'sVoucherDate', N'Amount', N'PaymentType', N'CommodityType', N'ExchangeRate', N'TotalCIF')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir
            + CASE WHEN @SortColumn = N'ApplicationNo' THEN N'' ELSE N', [ApplicationNo] ASC' END
            + CASE WHEN @SortColumn = N'LicenceNo' THEN N'' ELSE N', [LicenceNo] ASC' END;
    ELSE
        SET @ob = N'[ApplicationNo] ASC, [LicenceNo] ASC';

    DECLARE @cntpart nvarchar(max);
    DECLARE @sql nvarchar(max);
    DECLARE @ComputedTotal int = NULL;

    -- Page-first: page the base query, then resolve Currency/TotalAmount on the ~PageSize rows only
    -- via a lateral join to the materialized per-currency totals view (index seek, no re-aggregation).
    -- WITH (NOEXPAND) forces the materialized index to be used (required outside Enterprise edition).
    IF @FormType = N'Import Permit'
    BEGIN
        -- TotalCount only when requested, computed over the UN-paged base (no subqueries) as a separate scalar.
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ImportPermit
		INNER JOIN AccountTransaction ON ImportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ImportPermit.ApproveUserId
		WHERE IsPayment=1
		AND AccountTransaction.TransactionFormType=''Import Permit''
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ImportPermit.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE, LOOP JOIN); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        -- The original sp_VoucherReport selects only CommodityType for Import Permit;
        -- emit ExchangeRate/TotalCIF as NULL so the result set still matches sp_VoucherReportRow.
        SET @sql = @cntpart + N'SELECT pg.*, cur.Code AS Currency, amt.TotalAmount AS TotalAmount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ImportPermit.ApplicationNo,
ImportPermit.ApplicationDate,
Users.FullName as ApprovedUser,
AccountTransaction.PaymentDate Date,
CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,
section.Code SectionCode,
ApplyType,
OldImportPermitNo OldLicenceNo,
ImportPermitNo LicenceNo,
ImportPermit.CreatedDate LicenceDate,
CONVERT(varchar,ImportPermit.CreatedDate,103) sLicenceDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
VoucherNo,
VoucherDate,
CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,
CAST(AccountTransaction.TotalAmount AS decimal(38,6)) Amount,
PaymentType,
ImportPermit.CommodityType,
CAST(ImportPermit.ExchangeRate AS decimal(38,6)) ExchangeRate,
-- Must be decimal(38,6) to match the C# sp_VoucherReportRow.TotalCIF (decimal?) and all
-- other branch casts. AS float made EF GetDecimal throw InvalidCastException (Double to
-- Decimal), returning HTTP 500 for the whole Import Permit Voucher report (no data).
CAST(ImportPermit.TotalCIF AS decimal(38,6)) TotalCIF,
ImportPermit.Id AS __k_Id
        FROM ImportPermit
		INNER JOIN AccountTransaction ON ImportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ImportPermit.ApproveUserId
		WHERE IsPayment=1
		AND AccountTransaction.TransactionFormType=''Import Permit''
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ImportPermit.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    OUTER APPLY (
        SELECT SUM(v.TotalAmount) AS TotalAmount
        FROM dbo.vw_ImportPermitItemTotalByCurrency AS v WITH (NOEXPAND)
        WHERE v.ImportPermitId = pg.__k_Id
    ) amt
    OUTER APPLY (
        SELECT TOP 1 currency.Code
        FROM dbo.vw_ImportPermitItemTotalByCurrency AS v WITH (NOEXPAND)
        INNER JOIN Currency currency ON v.CurrencyId = currency.Id
        WHERE v.ImportPermitId = pg.__k_Id
    ) cur
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @FormType = N'Export Permit'
    BEGIN
        -- TotalCount only when requested, computed over the UN-paged base (no subqueries) as a separate scalar.
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ExportPermit
		INNER JOIN AccountTransaction ON ExportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ExportPermit.ApproveUserId
		WHERE IsPayment=1
		AND AccountTransaction.TransactionFormType=''Export Permit''
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ExportPermit.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE, LOOP JOIN); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        -- The original sp_VoucherReport selects only CommodityType for Export Permit: the
        -- ExportPermit table has no ExchangeRate / TotalCIF column at all. The old app's
        -- Reports.cs Export-Permit branch never assigns those two non-nullable decimals, so
        -- production's VoucherReport.rdlc prints a literal 0 in both -- emit 0, not NULL, so
        -- the grid and the .xlsx read the same way.
        SET @sql = @cntpart + N'SELECT pg.*, cur.Code AS Currency, amt.TotalAmount AS TotalAmount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ExportPermit.ApplicationNo,
ExportPermit.ApplicationDate,
Users.FullName as ApprovedUser,
AccountTransaction.PaymentDate Date,
CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,
section.Code SectionCode,
ApplyType,
OldExportPermitNo OldLicenceNo,
ExportPermitNo LicenceNo,
ExportPermit.CreatedDate LicenceDate,
CONVERT(varchar,ExportPermit.CreatedDate,103) sLicenceDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
VoucherNo,
VoucherDate,
CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,
CAST(AccountTransaction.TotalAmount AS decimal(38,6)) Amount,
PaymentType,
ExportPermit.CommodityType,
CAST(0 AS decimal(38,6)) ExchangeRate,
CAST(0 AS decimal(38,6)) TotalCIF,
ExportPermit.Id AS __k_Id
        FROM ExportPermit
		INNER JOIN AccountTransaction ON ExportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ExportPermit.ApproveUserId
		WHERE IsPayment=1
		AND AccountTransaction.TransactionFormType=''Export Permit''
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ExportPermit.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    OUTER APPLY (
        SELECT SUM(v.TotalAmount) AS TotalAmount
        FROM dbo.vw_ExportPermitItemTotalByCurrency AS v WITH (NOEXPAND)
        WHERE v.ExportPermitId = pg.__k_Id
    ) amt
    OUTER APPLY (
        SELECT TOP 1 currency.Code
        FROM dbo.vw_ExportPermitItemTotalByCurrency AS v WITH (NOEXPAND)
        INNER JOIN Currency currency ON v.CurrencyId = currency.Id
        WHERE v.ExportPermitId = pg.__k_Id
    ) cur
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @FormType = N'Export Licence'
    BEGIN
        -- TotalCount only when requested, computed over the UN-paged base (no subqueries) as a separate scalar.
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ExportLicence
		INNER JOIN AccountTransaction WITH (INDEX([IX_AccountTransaction_ExportLicenceVoucher])) ON ExportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ExportLicence.ApproveUserId
		WHERE IsPayment=1
		AND AccountTransaction.TransactionFormType=''Export Licence''
		AND ((@FromDate IS NULL) OR AccountTransaction.PaymentDate >= @FromDate)
		AND ((@ToDate IS NULL) OR AccountTransaction.PaymentDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND (@ApplyType='''' OR ApplyType=@ApplyType) AND ExportLicence.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE, MAXDOP 1); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        -- Resolve item values after paging so the report shows data without scanning item totals for every matching licence.
        SET @sql = @cntpart + N'SELECT pg.*, itemCurrency.Currency, itemTotal.TotalAmount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ExportLicence.ApplicationNo,
ExportLicence.ApplicationDate,
Users.FullName as ApprovedUser,
AccountTransaction.PaymentDate Date,
CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,
section.Code SectionCode,
ApplyType,
OldExportLicenceNo OldLicenceNo,
ExportLicenceNo LicenceNo,
ExportLicence.CreatedDate LicenceDate,
CONVERT(varchar,ExportLicence.CreatedDate,103) sLicenceDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
VoucherNo,
VoucherDate,
CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,
CAST(AccountTransaction.TotalAmount AS decimal(38,6)) Amount,
PaymentType,
ExportLicence.CommodityType,
CAST(NULL AS decimal(38,6)) ExchangeRate,
CAST(NULL AS decimal(38,6)) TotalCIF,
ExportLicence.Id AS __k_Id
        FROM ExportLicence
		INNER JOIN AccountTransaction WITH (INDEX([IX_AccountTransaction_ExportLicenceVoucher])) ON ExportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ExportLicence.ApproveUserId
		WHERE IsPayment=1
		AND AccountTransaction.TransactionFormType=''Export Licence''
		AND ((@FromDate IS NULL) OR AccountTransaction.PaymentDate >= @FromDate)
		AND ((@ToDate IS NULL) OR AccountTransaction.PaymentDate < DATEADD(day, 1, CONVERT(date, @ToDate)))
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND (@ApplyType='''' OR ApplyType=@ApplyType) AND ExportLicence.Status=''Approved''
        AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    OUTER APPLY (
        SELECT TOP 1 currency.Code AS Currency
        FROM ExportLicenceItem
        INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
        WHERE ExportLicenceItem.ExportLicenceId = pg.__k_Id
    ) itemCurrency
    OUTER APPLY (
        SELECT SUM(CAST(ExportLicenceItem.Amount AS decimal(38,6))) AS TotalAmount
        FROM ExportLicenceItem
        WHERE ExportLicenceItem.ExportLicenceId = pg.__k_Id
    ) itemTotal
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE, MAXDOP 1);';
    END
    ELSE IF @FormType = N'Border Export Licence'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM (
		SELECT BorderExportLicence.Id FROM BorderExportLicence
		INNER JOIN AccountTransaction ON BorderExportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderExportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderExportLicence.Status=''Approved'' AND BorderExportLicence.CardType=''Pa Tha Ka''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderExportLicence.Id FROM BorderExportLicence
		INNER JOIN AccountTransaction ON BorderExportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId=IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderExportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderExportLicence.Status=''Approved'' AND BorderExportLicence.CardType=''Individual Trading''
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
	) tmp OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,
        (SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=pg.__k_Id) Currency,
        (SELECT SUM(BorderExportLicenceItem.Amount) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=pg.__k_Id) TotalAmount, @__total AS TotalCount
    FROM (
        SELECT * FROM (
        SELECT BorderExportLicence.ApplicationNo,
BorderExportLicence.ApplicationDate,
Users.FullName as ApprovedUser,
AccountTransaction.PaymentDate Date,
CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,
section.Code SectionCode,
ApplyType,
OldExportLicenceNo OldLicenceNo,
ExportLicenceNo LicenceNo,
BorderExportLicence.CreatedDate LicenceDate,
CONVERT(varchar,BorderExportLicence.CreatedDate,103) sLicenceDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
VoucherNo,
VoucherDate,
CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,
CAST(AccountTransaction.TotalAmount AS decimal(38,6)) Amount,
PaymentType,
BorderExportLicence.CommodityType,
CAST(NULL AS decimal(38,6)) ExchangeRate,
CAST(NULL AS decimal(38,6)) TotalCIF,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportLicence.Id AS __k_Id
        FROM BorderExportLicence
		INNER JOIN AccountTransaction ON BorderExportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderExportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderExportLicence.Status=''Approved'' AND BorderExportLicence.CardType=''Pa Tha Ka''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
        SELECT BorderExportLicence.ApplicationNo,
BorderExportLicence.ApplicationDate,
Users.FullName as ApprovedUser,
AccountTransaction.PaymentDate Date,
CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,
section.Code SectionCode,
ApplyType,
OldExportLicenceNo OldLicenceNo,
ExportLicenceNo LicenceNo,
BorderExportLicence.CreatedDate LicenceDate,
CONVERT(varchar,BorderExportLicence.CreatedDate,103) sLicenceDate,
IndividualTrading.TINNo CompanyRegistrationNo,
IndividualTrading.Name CompanyName,
VoucherNo,
VoucherDate,
CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,
CAST(AccountTransaction.TotalAmount AS decimal(38,6)) Amount,
PaymentType,
BorderExportLicence.CommodityType,
CAST(NULL AS decimal(38,6)) ExchangeRate,
CAST(NULL AS decimal(38,6)) TotalCIF,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportLicence.Id AS __k_Id
        FROM BorderExportLicence
		INNER JOIN AccountTransaction ON BorderExportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId=IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderExportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderExportLicence.Status=''Approved'' AND BorderExportLicence.CardType=''Individual Trading''
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
        IF @IncludeTotalCount = 1
        BEGIN
            SELECT @ComputedTotal = COUNT(*) FROM (
                SELECT BorderImportLicence.Id FROM AccountTransaction WITH (INDEX([IX_AccountTransaction_BorderImportLicenceVoucher]))
                INNER JOIN BorderImportLicence ON BorderImportLicence.Id=AccountTransaction.TransactionId
                INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId=PaThaKa.Id
                WHERE AccountTransaction.IsPayment=1
                AND AccountTransaction.TransactionFormType='Border Import Licence'
                AND ((@FromDate IS NULL) OR AccountTransaction.PaymentDate >= @FromDate)
                AND ((@ToDate IS NULL) OR AccountTransaction.PaymentDate < DATEADD(day, 1, @ToDate))
                AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND (@PaymentType='' OR AccountTransaction.PaymentType=@PaymentType)
                AND (@ApplyType='' OR BorderImportLicence.ApplyType=@ApplyType) AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Pa Tha Ka'
                AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
                AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
                UNION ALL
                SELECT BorderImportLicence.Id FROM AccountTransaction WITH (INDEX([IX_AccountTransaction_BorderImportLicenceVoucher]))
                INNER JOIN BorderImportLicence ON BorderImportLicence.Id=AccountTransaction.TransactionId
                INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId=IndividualTrading.Id
                WHERE AccountTransaction.IsPayment=1
                AND AccountTransaction.TransactionFormType='Border Import Licence'
                AND ((@FromDate IS NULL) OR AccountTransaction.PaymentDate >= @FromDate)
                AND ((@ToDate IS NULL) OR AccountTransaction.PaymentDate < DATEADD(day, 1, @ToDate))
                AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
                AND (@PaymentType='' OR AccountTransaction.PaymentType=@PaymentType)
                AND (@ApplyType='' OR BorderImportLicence.ApplyType=@ApplyType) AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Individual Trading'
                AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
                AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
            ) tmp OPTION (RECOMPILE, MAXDOP 1);
        END

        SET @cntpart = N'DECLARE @__total int = @ComputedTotal; ';

        SET @sql = @cntpart + N'SELECT pg.*, itemAgg.Currency, itemAgg.TotalAmount, @__total AS TotalCount
    FROM (
        SELECT * FROM (
        SELECT BorderImportLicence.ApplicationNo,
BorderImportLicence.ApplicationDate,
Users.FullName as ApprovedUser,
AccountTransaction.PaymentDate Date,
CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,
section.Code SectionCode,
ApplyType,
OldImportLicenceNo OldLicenceNo,
ImportLicenceNo LicenceNo,
BorderImportLicence.CreatedDate LicenceDate,
CONVERT(varchar,BorderImportLicence.CreatedDate,103) sLicenceDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
VoucherNo,
VoucherDate,
CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,
CAST(AccountTransaction.TotalAmount AS decimal(38,6)) Amount,
PaymentType,
BorderImportLicence.CommodityType,
BorderImportLicence.ExchangeRate,
CAST(BorderImportLicence.TotalCIF AS decimal(38,6)) TotalCIF,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportLicence.Id AS __k_Id
        FROM BorderImportLicence
		INNER JOIN AccountTransaction WITH (INDEX([IX_AccountTransaction_BorderImportLicenceVoucher])) ON BorderImportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderImportLicence.ApproveUserId
		WHERE IsPayment=1
		AND AccountTransaction.TransactionFormType=''Border Import Licence''
		AND ((@FromDate IS NULL) OR AccountTransaction.PaymentDate >= @FromDate)
		AND ((@ToDate IS NULL) OR AccountTransaction.PaymentDate < DATEADD(day, 1, @ToDate))
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND (@ApplyType='''' OR BorderImportLicence.ApplyType=@ApplyType) AND BorderImportLicence.Status=''Approved'' AND BorderImportLicence.CardType=''Pa Tha Ka''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
        SELECT BorderImportLicence.ApplicationNo,
BorderImportLicence.ApplicationDate,
Users.FullName as ApprovedUser,
AccountTransaction.PaymentDate Date,
CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,
section.Code SectionCode,
ApplyType,
OldImportLicenceNo OldLicenceNo,
ImportLicenceNo LicenceNo,
BorderImportLicence.CreatedDate LicenceDate,
CONVERT(varchar,BorderImportLicence.CreatedDate,103) sLicenceDate,
IndividualTrading.TINNo CompanyRegistrationNo,
IndividualTrading.Name CompanyName,
VoucherNo,
VoucherDate,
CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,
CAST(AccountTransaction.TotalAmount AS decimal(38,6)) Amount,
PaymentType,
BorderImportLicence.CommodityType,
BorderImportLicence.ExchangeRate,
CAST(BorderImportLicence.TotalCIF AS decimal(38,6)) TotalCIF,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportLicence.Id AS __k_Id
        FROM BorderImportLicence
		INNER JOIN AccountTransaction WITH (INDEX([IX_AccountTransaction_BorderImportLicenceVoucher])) ON BorderImportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId=IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderImportLicence.ApproveUserId
		WHERE IsPayment=1
		AND AccountTransaction.TransactionFormType=''Border Import Licence''
		AND ((@FromDate IS NULL) OR AccountTransaction.PaymentDate >= @FromDate)
		AND ((@ToDate IS NULL) OR AccountTransaction.PaymentDate < DATEADD(day, 1, @ToDate))
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND (@ApplyType='''' OR BorderImportLicence.ApplyType=@ApplyType) AND BorderImportLicence.Status=''Approved'' AND BorderImportLicence.CardType=''Individual Trading''
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='''' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
        ) u
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    OUTER APPLY (
        SELECT
            MIN(currency.Code) AS Currency,
            SUM(item.Amount) AS TotalAmount
        FROM BorderImportLicenceItem item WITH (INDEX([IX_BorderImportLicenceItem_VoucherReport]))
        INNER JOIN Currency currency ON item.CurrencyId = currency.Id
        WHERE item.BorderImportLicenceId = pg.__k_Id
    ) itemAgg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE, MAXDOP 1);';
    END
    ELSE IF @FormType = N'Border Export Permit'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM BorderExportPermit
		INNER JOIN AccountTransaction ON BorderExportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderExportPermit.ApproveUserId
		WHERE IsPayment=1
		AND AccountTransaction.TransactionFormType=''Border Export Permit''
		AND ((@FromDate IS NULL) OR AccountTransaction.PaymentDate>=@FromDate)
		AND ((@ToDate IS NULL) OR AccountTransaction.PaymentDate<DATEADD(day, 1, @ToDate))
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderExportPermit.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END) OPTION (RECOMPILE, LOOP JOIN); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,
        (SELECT top 1 currency.Code FROM BorderExportPermitItem
		INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=pg.__k_Id) Currency,
        (SELECT SUM(BorderExportPermitItem.Amount) FROM BorderExportPermitItem
		WHERE BorderExportPermitItem.BorderExportPermitId=pg.__k_Id) TotalAmount, @__total AS TotalCount
    FROM (
        SELECT BorderExportPermit.ApplicationNo,
BorderExportPermit.ApplicationDate,
Users.FullName as ApprovedUser,
AccountTransaction.PaymentDate Date,
CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,
section.Code SectionCode,
ApplyType,
OldExportPermitNo OldLicenceNo,
ExportPermitNo LicenceNo,
BorderExportPermit.CreatedDate LicenceDate,
CONVERT(varchar,BorderExportPermit.CreatedDate,103) sLicenceDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
VoucherNo,
VoucherDate,
CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,
CAST(AccountTransaction.TotalAmount AS decimal(38,6)) Amount,
PaymentType,
BorderExportPermit.CommodityType,
CAST(NULL AS decimal(38,6)) ExchangeRate,
CAST(NULL AS decimal(38,6)) TotalCIF,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderExportPermit.Id AS __k_Id
        FROM BorderExportPermit
		INNER JOIN AccountTransaction ON BorderExportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderExportPermit.ApproveUserId
		WHERE IsPayment=1
		AND AccountTransaction.TransactionFormType=''Border Export Permit''
		AND ((@FromDate IS NULL) OR AccountTransaction.PaymentDate>=@FromDate)
		AND ((@ToDate IS NULL) OR AccountTransaction.PaymentDate<DATEADD(day, 1, @ToDate))
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderExportPermit.Status=''Approved''
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
		INNER JOIN AccountTransaction ON BorderImportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderImportPermit.ApproveUserId
		WHERE IsPayment=1
		AND AccountTransaction.TransactionFormType=''Border Import Permit''
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderImportPermit.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
	) tmp OPTION (RECOMPILE, LOOP JOIN); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*,
        (SELECT top 1 currency.Code FROM BorderImportPermitItem
		INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=pg.__k_Id) Currency,
        (SELECT SUM(BorderImportPermitItem.Amount) FROM BorderImportPermitItem
		WHERE BorderImportPermitItem.BorderImportPermitId=pg.__k_Id) TotalAmount, @__total AS TotalCount
    FROM (
        SELECT * FROM (
        SELECT BorderImportPermit.ApplicationNo,
BorderImportPermit.ApplicationDate,
Users.FullName as ApprovedUser,
AccountTransaction.PaymentDate Date,
CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,
section.Code SectionCode,
ApplyType,
OldImportPermitNo OldLicenceNo,
ImportPermitNo LicenceNo,
BorderImportPermit.CreatedDate LicenceDate,
CONVERT(varchar,BorderImportPermit.CreatedDate,103) sLicenceDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
VoucherNo,
VoucherDate,
CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,
CAST(AccountTransaction.TotalAmount AS decimal(38,6)) Amount,
PaymentType,
BorderImportPermit.CommodityType,
BorderImportPermit.ExchangeRate,
CAST(BorderImportPermit.TotalCIF AS decimal(38,6)) TotalCIF,
sakhan.Id SakhanId,
sakhan.Code SakhanCode,
sakhan.Name SakhanName,
BorderImportPermit.Id AS __k_Id
        FROM BorderImportPermit
		INNER JOIN AccountTransaction ON BorderImportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderImportPermit.ApproveUserId
		WHERE IsPayment=1
		AND AccountTransaction.TransactionFormType=''Border Import Permit''
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderImportPermit.Status=''Approved''
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
        -- TotalCount only when requested, computed over the UN-paged base (no subqueries) as a separate scalar.
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM ImportLicence
		INNER JOIN AccountTransaction ON ImportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ImportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ImportLicence.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END) OPTION (RECOMPILE); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        SET @sql = @cntpart + N'SELECT pg.*, cur.Code AS Currency, amt.TotalAmount AS TotalAmount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
    FROM (
        SELECT ImportLicence.ApplicationNo,
ImportLicence.ApplicationDate,
Users.FullName as ApprovedUser,
AccountTransaction.PaymentDate Date,
CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,
section.Code SectionCode,
ApplyType,
OldImportLicenceNo OldLicenceNo,
ImportLicenceNo LicenceNo,
ImportLicence.CreatedDate LicenceDate,
CONVERT(varchar,ImportLicence.CreatedDate,103) sLicenceDate,
PaThaKa.CompanyRegistrationNo,
PaThaKa.CompanyName,
VoucherNo,
VoucherDate,
CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,
CAST(AccountTransaction.TotalAmount AS decimal(38,6)) Amount,
PaymentType,
ImportLicence.CommodityType,
ImportLicence.ExchangeRate,
CAST(ImportLicence.TotalCIF AS decimal(38,6)) TotalCIF,
ImportLicence.Id AS __k_Id
        FROM ImportLicence
		INNER JOIN AccountTransaction ON ImportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ImportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ImportLicence.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    OUTER APPLY (
        SELECT SUM(v.TotalAmount) AS TotalAmount
        FROM dbo.vw_ImportLicenceItemTotalByCurrency AS v WITH (NOEXPAND)
        WHERE v.ImportLicenceId = pg.__k_Id
    ) amt
    OUTER APPLY (
        SELECT TOP 1 currency.Code
        FROM dbo.vw_ImportLicenceItemTotalByCurrency AS v WITH (NOEXPAND)
        INNER JOIN Currency currency ON v.CurrencyId = currency.Id
        WHERE v.ImportLicenceId = pg.__k_Id
    ) cur
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END

    EXEC sp_executesql @sql, N'@FormType nvarchar(50), @FromDate datetime, @ToDate datetime, @ExportImportSectionId int, @PaymentType nvarchar(50), @ApplyType nvarchar(20), @CompanyRegistrationNo nvarchar(50), @SakhanId int, @off bigint, @ps bigint, @ComputedTotal int', @FormType=@FormType, @FromDate=@FromDate, @ToDate=@ToDate, @ExportImportSectionId=@ExportImportSectionId, @PaymentType=@PaymentType, @ApplyType=@ApplyType, @CompanyRegistrationNo=@CompanyRegistrationNo, @SakhanId=@SakhanId, @off=@off, @ps=@ps, @ComputedTotal=@ComputedTotal;
END


GO

SET NOEXEC OFF;
GO

PRINT N'Export Permit round-3 procedures applied. Now run VerifyDeployment.sql.';
GO

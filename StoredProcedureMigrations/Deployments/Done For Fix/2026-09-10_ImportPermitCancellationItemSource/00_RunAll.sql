/* =====================================================================================
   Import Permit Cancellation item-source deployment - 2026-09-10
   Run this ONE file to apply both procedures, or run 01 and 02 individually.
   Either way: PROCEDURES FIRST, APPLICATION SECOND.

   Target database: TradeNetDB  (NOT ReportTemplateDB - that one only holds the Excel
   export job queue; deploying report procedures into it is a known trap.)

   Complaint: "Import Permit Cancellation Report - currency & total value တွေမှာ N/A
   တွေထွက်နေပါတယ်".

   What changes: an Import Permit cancellation record normally carries no ImportPermitItem
   rows of its own, so the correlated TOP 1 lookups keyed on the cancellation's own Id
   returned NULL and the grid printed "N/A" - 10 of the 20 production rows for 2023-2026.
   Both procedures now resolve the item through a fallback:

       COALESCE(<the cancellation's own items>, <the items of the permit it cancels>)

   matched on ImportPermit.OldImportPermitNo -> ImportPermit.ImportPermitNo, and both take
   the item with ORDER BY ImportPermitItem.Id so the grid row and the per-currency footer
   can never pick different items.

   01 sp_CancelReport_pagination            - @FormType='Import Permit' branch ONLY. The
                                              Export Permit / Export Licence / Border
                                              branches are deliberately untouched: their
                                              cancellation records DO carry items (verified
                                              on production - 0 NULL rows for each).
   02 sp_ImportPermitListingCurrencyTotals  - @DbApplyType='Cancel' branch ONLY. Without it
                                              the footer keeps collapsing those permits into
                                              a blank-currency line with a 0 total.

   Also ships in the application build: ImportPermitCancellationReportController
   [ExcelFormatVersion(3)], so cached closed-period .xlsx files are not reused.

   Generated from the repository files of the same name; see README.md in this folder.
   ===================================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE [TradeNetDB];
GO

/* ---------- 01_sp_CancelReport_pagination.sql ---------- */
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

        -- Currency / HSCode / Amount come from __k_ItemId, not __k_Id: an Import Permit
        -- cancellation record usually carries no items of its own, so keying the lookup on
        -- it returned NULL and the grid printed "N/A" (10 of 20 production rows, 2023-2026).
        -- __k_ItemId prefers the cancellation's own items and falls back to the permit it
        -- cancels, which does hold them. ORDER BY ImportPermitItem.Id makes the TOP 1 pick
        -- deterministic and identical to sp_ImportPermitListingCurrencyTotals' Cancel branch,
        -- so the per-currency footer and the rows can no longer disagree.
        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ImportPermitItem
		INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
		WHERE ImportPermitItem.ImportPermitId=pg.__k_ItemId
		ORDER BY ImportPermitItem.Id) Currency,
        (SELECT top 1 HSCode.Code FROM ImportPermitItem
		INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id
		WHERE ImportPermitItem.ImportPermitId=pg.__k_ItemId
		ORDER BY ImportPermitItem.Id) HSCode,
        (SELECT top 1  ISNULL(ImportPermitItem.Amount,0) FROM ImportPermitItem
		WHERE ImportPermitItem.ImportPermitId=pg.__k_ItemId
		ORDER BY ImportPermitItem.Id) Amount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
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
ImportPermit.Id AS __k_Id,
COALESCE(
	(SELECT TOP 1 own.ImportPermitId FROM ImportPermitItem own
		WHERE own.ImportPermitId = ImportPermit.Id),
	(SELECT TOP 1 parent.Id FROM ImportPermit parent
		WHERE parent.ImportPermitNo = ImportPermit.OldImportPermitNo
		AND EXISTS (SELECT 1 FROM ImportPermitItem pi WHERE pi.ImportPermitId = parent.Id)
		ORDER BY parent.Id)
) AS __k_ItemId
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

/* ---------- 02_sp_ImportPermitListingCurrencyTotals.sql ---------- */
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
        --
        -- ItemPermitId, not ImportPermit.Id: a cancellation record usually holds no items of
        -- its own, which used to collapse those permits into a blank-currency footer line
        -- with a 0 total. It mirrors __k_ItemId in sp_CancelReport_pagination's Import Permit
        -- branch exactly -- prefer the cancellation's own items, else the permit it cancels --
        -- so the footer and the grid rows always pick the same item.
        SELECT ISNULL(d.Currency, N'') AS Currency,
               COUNT(DISTINCT d.LicenceNo) AS NoOfLicences,
               ISNULL(SUM(d.Amount), 0) AS TotalValue
        FROM (
            SELECT
                ImportPermit.ImportPermitNo AS LicenceNo,
                (SELECT TOP 1 currency.Code FROM ImportPermitItem
                    INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
                    WHERE ImportPermitItem.ImportPermitId = src.ItemPermitId
                    ORDER BY ImportPermitItem.Id) AS Currency,
                (SELECT TOP 1 ISNULL(ImportPermitItem.Amount, 0) FROM ImportPermitItem
                    WHERE ImportPermitItem.ImportPermitId = src.ItemPermitId
                    ORDER BY ImportPermitItem.Id) AS Amount
            FROM ImportPermit
                INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
                INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
                CROSS APPLY (
                    SELECT COALESCE(
                        (SELECT TOP 1 own.ImportPermitId FROM ImportPermitItem own
                            WHERE own.ImportPermitId = ImportPermit.Id),
                        (SELECT TOP 1 parent.Id FROM ImportPermit parent
                            WHERE parent.ImportPermitNo = ImportPermit.OldImportPermitNo
                            AND EXISTS (SELECT 1 FROM ImportPermitItem pi WHERE pi.ImportPermitId = parent.Id)
                            ORDER BY parent.Id)
                    ) AS ItemPermitId
                ) src
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


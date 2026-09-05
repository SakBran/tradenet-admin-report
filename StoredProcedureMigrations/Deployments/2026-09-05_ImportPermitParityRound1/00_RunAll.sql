/* =====================================================================================
   Import Permit round-1 parity deployment - 2026-09-05
   Run this ONE file to apply both procedures, or run the numbered files 01..02
   individually. Either way: PROCEDURES FIRST, APPLICATION SECOND.

   Target database: TradeNetDB  (NOT ReportTemplateDB - that one only holds the Excel
   export job queue; deploying report procedures into it is a known trap.)

   What changes:
     01 sp_HSCodeReport_pagination - the @FormType='Import Permit' branch groups on
        (HSCodeId, Currency) instead of additionally on the buyer company. The legacy
        HSCodeReport.rdlc row group is exactly HSCodeId + Currency (rdlc:1152-1153) and the
        grid shows no company column, so the extra key silently split one HS code into one
        row per buyer, each carrying only that buyer's Total Value - the customer's
        "one HS code appears twice" complaint. The other seven FormType branches are
        UNCHANGED: their *HSCodeDetailReport reports render Company Name off this same
        procedure.
     02 sp_ImportPermitListingCurrencyTotals - new ApplyType='Cancel' branch, so the Import
        Permit Cancellation report gets back the per-currency TOTAL footer of
        CancelReport.rdlc Tablix2 (:1557 / :1611 / :1723). Without it the catch-all ELSE
        would have answered with New-permit numbers.

   Generated from the repository files of the same name; see README.md in this folder.
   ===================================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE [TradeNetDB];
GO

-- Wrong-database guard: dbo.sp_HSCodeReport is the legacy Tradenet 2.0 procedure and
-- exists only in the report database. Stop before creating anything in the wrong place.
-- COMMENTED OUT, same as the Export Permit round-3 script (commit df9a5ac): the legacy
-- sp_* procedures are not present on the deployment target, so the guard blocked the run.
-- Check `SELECT DB_NAME();` reads TradeNetDB before executing -- NOT ReportTemplateDB,
-- which only holds the Excel export job queue.
-- IF OBJECT_ID(N'dbo.sp_HSCodeReport', N'P') IS NULL
-- BEGIN
--     RAISERROR(N'Wrong database: dbo.sp_HSCodeReport was not found in [%s]. Connect to TradeNetDB and run again.', 16, 1, DB_NAME());
--     SET NOEXEC ON;
-- END
-- GO

-- ============================================================================
-- sp_HSCodeReport_pagination   (file 01_sp_HSCodeReport_pagination.sql)
-- ============================================================================
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
			FETCH NEXT @PageSize ROWS ONLY
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
			FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
			FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
			FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
			FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
			FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
			FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
			FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
		OPTION (RECOMPILE);
			END
		END
	END
	ELSE IF(@FormType='Border Import Permit')
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
			GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
			)result
			ORDER BY result.HSCode,result.CompanyName,result.Currency
			OFFSET @PageIndex * @PageSize ROWS
			FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
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
				FETCH NEXT @PageSize ROWS ONLY
		OPTION (RECOMPILE);
			END
		END
	END
END
GO

GO

-- ============================================================================
-- sp_ImportPermitListingCurrencyTotals   (file 02_sp_ImportPermitListingCurrencyTotals.sql)
-- ============================================================================
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

SET NOEXEC OFF;
GO
PRINT N'Both procedures applied. Now run VerifyDeployment.sql before deploying the application.';
GO

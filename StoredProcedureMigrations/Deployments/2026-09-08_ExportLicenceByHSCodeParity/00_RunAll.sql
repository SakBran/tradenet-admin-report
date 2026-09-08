/* =====================================================================================
   Export Licence / Border Export Licence By HS Code parity deployment - 2026-09-08
   Run this ONE file to apply the procedure, or run 01_sp_HSCodeReport_pagination.sql
   directly. Either way: PROCEDURE FIRST, APPLICATION SECOND.

   Target database: TradeNetDB  (NOT ReportTemplateDB - that one only holds the Excel
   export job queue; deploying report procedures into it is a known trap.)

   What changes - two @FormType branches of sp_HSCodeReport_pagination, nothing else:

     'Export Licence'        (4 sub-branches)
     'Border Export Licence' (3 sub-branches)

   1. They now GROUP BY (HSCodeId, HSCode, HSDescription, Currency) instead of
      additionally on the buyer company. HSCodeReport.rdlc's row group is exactly
      =Fields!HSCodeId.Value + =Fields!Currency.Value (rdlc:1150-1160), and
      BorderHSCodeReport.rdlc's is the same (rdlc:1158-1168); neither grid renders a
      company column, so the extra key silently split one HS code into one row per buyer,
      each carrying only that buyer's slice of Total Value. Customer complaint 2026-09-08,
      measured on the live API for 31/08-01/09/2026:

                                        old report   before   after
        Export Licence By HS Code            304      1058      304
        Border Export Licence By HS Code      33        76       33

      "Total No of License" (961 / 41) already matched and is unaffected: it is a separate
      whole-set COUNT(DISTINCT LicenceNo), the RDLC's =CountDistinct(Fields!LicenceNo.Value).

   2. ORDER BY is now (HSCode, Currency, HSCodeId) - a UNIQUE key. It used to be
      (HSCode, CompanyName, Currency) over a group key that also contained
      CompanyRegistrationNo and HSDescription, so tied rows were ordered arbitrarily and
      OFFSET/FETCH could return one row on two pages and another on none. That is the
      "pagination shows 106 pages but data stops at page 97" half of the complaint.

   3. The 'Export Licence' @IncludeTotalCount=0 fast page now joins ExportImportSection,
      like every counted sub-branch and like legacy dbo.sp_HSCodeReport. Without it the
      fast page could show a licence whose ExportImportSectionId has no section row - one
      the old report never printed and the exact-count branch does not count.

   The other six @FormType branches are UNCHANGED. Import Licence, Export Permit and
   Border Import Licence By HS Code still carry the company in their key (the same latent
   defect, deliberately left for a later round - owner decision 2026-09-08); their
   *HSCodeDetailReport reports render Company Name off this same procedure.

   The HS Code DETAIL drills (ExportLicenceHSCodeDetailReport,
   BorderExportLicenceHSCodeDetailReport) do NOT use this procedure: they post
   GroupBy='Company' and run the LINQ twin (sp_HSCodeReport.AggregateQuery), which keys on
   (HSCodeId, HSCode, CompanyRegistrationNo) - HSCodeDetailReport.rdlc:1261-1265.

   ⚠ Do NOT re-run the sp_HSCodeReport_pagination copies under
   Deployments/Done For Fix/2026-09-05_ImportPermitParityRound1/ or
   .../2026-09-05_BorderImportPermitComplaints/. Those are frozen snapshots of what was
   deployed then and would revert this change.

   Generated from the repository files of the same name; see README.md in this folder.
   ===================================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE [TradeNetDB];
GO

-- ============================================================================
-- sp_HSCodeReport_pagination   (file 01_sp_HSCodeReport_pagination.sql)
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
		-- Grouped on (HSCodeId, Currency) only -- NOT on the company. HSCodeReport.rdlc's row
		-- group is exactly <GroupExpression>=Fields!HSCodeId.Value and =Fields!Currency.Value
		-- (rdlc:1150-1160) and the grid renders no company column (reportConfigs.ts
		-- ExportLicenceByHSCodeReport), so keeping the company in the key split one HS code into
		-- one invisible row per buyer, each with a partial Total Value: 31/08-01/09/2026 returned
		-- 1058 rows where the old report shows 304. The ExportLicenceHSCodeDetailReport drill,
		-- which DOES render Company Name, asks for that shape explicitly with GroupBy='Company'
		-- and therefore runs the LINQ twin, not this procedure.
		IF(@IncludeTotalCount=0)
		BEGIN
			SELECT result.HSCode,result.HSDescription,
			CAST(NULL AS nvarchar(200)) CompanyRegistrationNo,CAST(NULL AS nvarchar(500)) CompanyName,result.Currency,
			result.NoOfLicences,result.TotalValue,CAST(NULL AS int) TotalCount
			FROM
			(
			SELECT tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency,
			COUNT(DISTINCT tmp.LicenceNo) NoOfLicences,SUM(tmp.Amount) TotalValue
			FROM
			(SELECT HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,ExportLicenceItem.Amount,currency.Code Currency,
			ExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM ExportLicence
			INNER JOIN ExportLicenceItem ON ExportLicence.Id = ExportLicenceItem.ExportLicenceId
			INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
			-- Legacy dbo.sp_HSCodeReport and every counted sub-branch below join the section;
			-- only this fast page did not, so it could show a licence whose ExportImportSectionId
			-- has no ExportImportSection row -- one the old report never printed, and one the
			-- exact-count branch does not count.
			INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
			WHERE ExportLicence.ApplyType='New' AND ExportLicence.Status='Approved'
			AND (ExportLicence.LicenceDate>=@FromDate AND ExportLicence.LicenceDate<=@ToDate)
			AND (@HSCode='' OR (@FilterType='Start' AND HSCode.Code LIKE @HSCode+'%') OR (@FilterType<>'Start' AND HSCode.Code LIKE '%'+@HSCode)))tmp
			GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency
			)result
			ORDER BY result.HSCode,result.Currency,result.HSCodeId
			OFFSET @PageIndex * @PageSize ROWS
			FETCH NEXT @FetchSize ROWS ONLY
			OPTION (RECOMPILE, MAXDOP 1);

			RETURN;
		END

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
			ExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM ExportLicence
			INNER JOIN ExportLicenceItem ON ExportLicence.Id = ExportLicenceItem.ExportLicenceId
			INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND ExportLicence.Status='Approved'
			AND (ExportLicence.LicenceDate>=@FromDate AND ExportLicence.LicenceDate<=@ToDate))tmp
			GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency
			)result
			ORDER BY result.HSCode,result.Currency,result.HSCodeId
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
				GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency
				)result
				ORDER BY result.HSCode,result.Currency,result.HSCodeId
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
				GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency
				)result
				ORDER BY result.HSCode,result.Currency,result.HSCodeId
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
		-- Grouped on (HSCodeId, Currency) only -- NOT on the company, for the same reason as the
		-- Export Licence branch above: BorderHSCodeReport.rdlc's row group is
		-- =Fields!HSCodeId.Value + =Fields!Currency.Value (rdlc:1158-1168) and the grid has no
		-- company column, so the company in the key produced 76 rows where the old report shows
		-- 33 (31/08-01/09/2026). BorderExportLicenceHSCodeDetailReport asks for the company shape
		-- explicitly with GroupBy='Company' and runs the LINQ twin instead.
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
			GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency
			)result
			ORDER BY result.HSCode,result.Currency,result.HSCodeId
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
				GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency
				)result
				ORDER BY result.HSCode,result.Currency,result.HSCodeId
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
				GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency
				)result
				ORDER BY result.HSCode,result.Currency,result.HSCodeId
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



GO

PRINT N'sp_HSCodeReport_pagination applied. Now run VerifyDeployment.sql before deploying the application.';
GO

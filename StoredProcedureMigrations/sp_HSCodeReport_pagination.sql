CREATE OR ALTER PROCEDURE [dbo].[sp_HSCodeReport_pagination]
	@FromDate datetime,
	@ToDate datetime,
	@FormType nvarchar(50),
	@FilterType nvarchar(20),
	@HSCode nvarchar(50),
	@SakhanId int,
	@PageIndex int = 0,
	@PageSize int = 10
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
			FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
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
			FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
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
			FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
			END
		END
	END
	ELSE IF(@FormType='Import Permit')
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
			ImportPermit.ImportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM ImportPermit
			INNER JOIN ImportPermitItem ON ImportPermit.Id = ImportPermitItem.ImportPermitId
			INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND ImportPermit.Status='Approved'
			AND (ImportPermit.LicenceDate>=@FromDate AND ImportPermit.LicenceDate<=@ToDate))tmp
			GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
			)result
			ORDER BY result.HSCode,result.CompanyName,result.Currency
			OFFSET @PageIndex * @PageSize ROWS
			FETCH NEXT @PageSize ROWS ONLY;
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
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @PageSize ROWS ONLY;
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
				GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
				)result
				ORDER BY result.HSCode,result.CompanyName,result.Currency
				OFFSET @PageIndex * @PageSize ROWS
				FETCH NEXT @PageSize ROWS ONLY;
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
			FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
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
			FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
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
			FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
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
			FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
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
				FETCH NEXT @PageSize ROWS ONLY;
			END
		END
	END
END
GO



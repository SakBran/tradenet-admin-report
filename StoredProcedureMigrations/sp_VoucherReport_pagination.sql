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

    DECLARE @ps bigint = CASE
        WHEN ISNULL(@PageSize,0) <= 0 THEN 9223372036854775807
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
        ELSE @PageSize END;
    DECLARE @off bigint = CASE WHEN ISNULL(@PageSize,0) <= 0 THEN 0 ELSE ISNULL(@PageIndex,0) * CAST(@PageSize AS bigint) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder,'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'ApplicationNo', N'ApplicationDate', N'ApprovedUser', N'Date', N'sDate', N'SectionCode', N'ApplyType', N'OldLicenceNo', N'LicenceNo', N'LicenceDate', N'sLicenceDate', N'CompanyRegistrationNo', N'CompanyName', N'VoucherNo', N'VoucherDate', N'sVoucherDate', N'Amount', N'PaymentType', N'CommodityType', N'ExchangeRate', N'TotalCIF')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir + N', [ApplicationNo] ASC, [LicenceNo] ASC';
    ELSE
        SET @ob = N'[ApplicationNo] ASC, [LicenceNo] ASC';

    DECLARE @cntpart nvarchar(max);
    DECLARE @sql nvarchar(max);

    -- Page-first: page the base query, then resolve Currency/TotalAmount on the ~PageSize rows only
    -- via a lateral join to the materialized per-currency totals view (index seek, no re-aggregation).
    -- WITH (NOEXPAND) forces the materialized index to be used (required outside Enterprise edition).
    IF @FormType = N'Import Permit'
    BEGIN
        -- TotalCount only when requested, computed over the UN-paged base (no subqueries) as a separate scalar.
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int = (SELECT COUNT(*) FROM ImportPermit
		INNER JOIN AccountTransaction ON ImportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ImportPermit.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ImportPermit.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)); '
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
CAST(NULL AS decimal(38,6)) ExchangeRate,
CAST(NULL AS decimal(38,6)) TotalCIF,
ImportPermit.Id AS __k_Id
        FROM ImportPermit
		INNER JOIN AccountTransaction ON ImportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ImportPermit.ApproveUserId
		WHERE IsPayment=1
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
            THEN N'DECLARE @__total int = (SELECT COUNT(*) FROM ExportPermit
		INNER JOIN AccountTransaction ON ExportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ExportPermit.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ExportPermit.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        -- The original sp_VoucherReport selects only CommodityType for Export Permit;
        -- emit ExchangeRate/TotalCIF as NULL so the result set still matches sp_VoucherReportRow.
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
CAST(NULL AS decimal(38,6)) ExchangeRate,
CAST(NULL AS decimal(38,6)) TotalCIF,
ExportPermit.Id AS __k_Id
        FROM ExportPermit
		INNER JOIN AccountTransaction ON ExportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ExportPermit.ApproveUserId
		WHERE IsPayment=1
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
            THEN N'DECLARE @__total int = (SELECT COUNT(*) FROM ExportLicence
		INNER JOIN AccountTransaction ON ExportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ExportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ExportLicence.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)); '
            ELSE N'DECLARE @__total int = NULL; ' END;

        -- The original sp_VoucherReport selects only CommodityType for Export Licence;
        -- emit ExchangeRate/TotalCIF as NULL so the result set still matches sp_VoucherReportRow.
        SET @sql = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id) Currency,
        (SELECT SUM(ExportLicenceItem.Amount) FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=pg.__k_Id) TotalAmount, CAST(NULL AS int) SakhanId, CAST(NULL AS nvarchar(50)) SakhanCode, CAST(NULL AS nvarchar(200)) SakhanName, @__total AS TotalCount
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
		INNER JOIN AccountTransaction ON ExportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ExportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ExportLicence.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END
    ELSE IF @FormType = N'Border Export Licence'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int = (SELECT COUNT(*) FROM (
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
	) tmp); '
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
    ELSE IF @FormType = N'Border Export Permit'
    BEGIN
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int = (SELECT COUNT(*) FROM BorderExportPermit
		INNER JOIN AccountTransaction ON BorderExportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderExportPermit.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderExportPermit.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END)); '
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
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderExportPermit.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END)
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';
    END    ELSE
    BEGIN
        -- TotalCount only when requested, computed over the UN-paged base (no subqueries) as a separate scalar.
        SET @cntpart = CASE WHEN @IncludeTotalCount = 1
            THEN N'DECLARE @__total int = (SELECT COUNT(*) FROM ImportLicence
		INNER JOIN AccountTransaction ON ImportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ImportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='''' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ImportLicence.Status=''Approved''
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='''' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)); '
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

    EXEC sp_executesql @sql, N'@FormType nvarchar(50), @FromDate datetime, @ToDate datetime, @ExportImportSectionId int, @PaymentType nvarchar(50), @ApplyType nvarchar(20), @CompanyRegistrationNo nvarchar(50), @SakhanId int, @off bigint, @ps bigint', @FormType=@FormType, @FromDate=@FromDate, @ToDate=@ToDate, @ExportImportSectionId=@ExportImportSectionId, @PaymentType=@PaymentType, @ApplyType=@ApplyType, @CompanyRegistrationNo=@CompanyRegistrationNo, @SakhanId=@SakhanId, @off=@off, @ps=@ps;
END


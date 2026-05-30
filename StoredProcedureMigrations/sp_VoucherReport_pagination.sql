CREATE OR ALTER PROCEDURE [dbo].[sp_VoucherReport_pagination]
    @FormType nvarchar(50) = N'',
    @FromDate datetime = NULL,
    @ToDate datetime = NULL,
    @ExportImportSectionId int = 0,
    @PaymentType nvarchar(50) = N'',
    @ApplyType nvarchar(20) = N'',
    @CompanyRegistrationNo nvarchar(10) = N'',
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

    -- TotalCount only when requested, computed over the UN-paged base (no subqueries) as a separate scalar.
    DECLARE @cntpart nvarchar(max) = CASE WHEN @IncludeTotalCount = 1
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

    DECLARE @sql nvarchar(max) = @cntpart + N'SELECT pg.*,(SELECT top 1 currency.Code FROM ImportLicenceItem
		INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
		WHERE ImportLicenceItem.ImportLicenceId=pg.__k_Id) Currency,
        (Select SUM(Amount) as TotalAmount  from ImportLicenceItem
       where ImportLicenceItem.ImportLicenceId=pg.__k_Id) as TotalAmount, @__total AS TotalCount
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
TotalAmount Amount,
PaymentType,
ImportLicence.CommodityType,
ImportLicence.ExchangeRate,
ImportLicence.TotalCIF,
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
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';

    EXEC sp_executesql @sql, N'@FormType nvarchar(50), @FromDate datetime, @ToDate datetime, @ExportImportSectionId int, @PaymentType nvarchar(50), @ApplyType nvarchar(20), @CompanyRegistrationNo nvarchar(10), @SakhanId int, @off bigint, @ps bigint', @FormType=@FormType, @FromDate=@FromDate, @ToDate=@ToDate, @ExportImportSectionId=@ExportImportSectionId, @PaymentType=@PaymentType, @ApplyType=@ApplyType, @CompanyRegistrationNo=@CompanyRegistrationNo, @SakhanId=@SakhanId, @off=@off, @ps=@ps;
END

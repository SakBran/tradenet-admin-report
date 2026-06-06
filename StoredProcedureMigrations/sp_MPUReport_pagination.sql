CREATE OR ALTER PROCEDURE [dbo].[sp_MPUReport_pagination]
    @FromDate datetime,
    @ToDate datetime,
    @FormType nvarchar(200),
    @PaymentType nvarchar(200),
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL,
    @IncludeTotalCount bit = 1
AS
BEGIN
    SET NOCOUNT ON;

    SET @PaymentType = LTRIM(RTRIM(ISNULL(@PaymentType, N'')));
    IF REPLACE(REPLACE(REPLACE(LOWER(@PaymentType), N' ', N''), N'-', N''), N'_', N'') IN (N'citizenpay', N'citizen', N'cp')
    BEGIN
        SET @PaymentType = N'CitizenPay';
    END;

    DECLARE @OnlineFeeAmount nvarchar(50) = CASE WHEN @FromDate < '2025-11-15' THEN N'3000' ELSE N'10000' END;
    DECLARE @OnlineFeeTotalAmount decimal(18, 2) = CASE WHEN @FromDate < '2025-11-15' THEN 3000 ELSE 10000 END;
    DECLARE @ps bigint = CASE
        WHEN ISNULL(@PageSize, 0) <= 0 THEN 9223372036854775807
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
        ELSE @PageSize END;
    DECLARE @off bigint = CASE WHEN ISNULL(@PageSize, 0) <= 0 THEN 0 ELSE ISNULL(@PageIndex, 0) * CAST(@PageSize AS bigint) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder, 'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL AND @SortColumn IN (
        N'Id', N'Sakhan', N'TransactionDateTime', N'CompanyName', N'CompanyRegistrationNo',
        N'ApplicationNo', N'MerchantId', N'AccountNo', N'InvoiceNo', N'ApprovalCode',
        N'TransactionRefNo', N'TransactionAmount', N'MOCAmount', N'IMAmount', N'MPUAmount', N'AmountDiff',
        N'FormType', N'ApplyType', N'VoucherNo')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir + N', [TransactionDateTime] ASC, [Id] ASC';
    ELSE
        SET @ob = N'[TransactionDateTime] ASC, [Id] ASC';

    DECLARE @cntpart nvarchar(max) = CASE WHEN @IncludeTotalCount = 1
        THEN N'DECLARE @__total int; SELECT @__total = COUNT(*) FROM (
            SELECT MPUPaymentTransaction.Id
            FROM dbo.MPUPaymentTransaction
            WHERE dbo.MPUPaymentTransaction.ResponseCode = ''00''
            AND dbo.MPUPaymentTransaction.TransactionDateTime IS NOT NULL
            AND (dbo.MPUPaymentTransaction.TransactionDateTime >= @FromDate AND dbo.MPUPaymentTransaction.TransactionDateTime <= @ToDate)
            AND dbo.MPUPaymentTransaction.FormType IS NOT NULL
            AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType = '''' THEN dbo.MPUPaymentTransaction.FormType + ''%'' ELSE @FormType + ''%'' END)
            AND (@PaymentType = ''''
                OR dbo.MPUPaymentTransaction.PaymentType = @PaymentType
                OR (@PaymentType = ''CitizenPay''
                    AND REPLACE(REPLACE(REPLACE(LOWER(ISNULL(dbo.MPUPaymentTransaction.PaymentType, '''')), '' '', ''''), ''-'', ''''), ''_'', '''')
                        IN (''citizenpay'', ''citizen'', ''cp'')))
            AND dbo.MPUPaymentTransaction.MOCAmount <> @OnlineFeeAmount
            UNION
            SELECT MPUPaymentTransaction.Id
            FROM dbo.MPUPaymentTransaction
            WHERE dbo.MPUPaymentTransaction.ResponseCode = ''00''
            AND dbo.MPUPaymentTransaction.TransactionDateTime IS NOT NULL
            AND (dbo.MPUPaymentTransaction.TransactionDateTime >= @FromDate AND dbo.MPUPaymentTransaction.TransactionDateTime <= @ToDate)
            AND dbo.MPUPaymentTransaction.FormType IS NOT NULL
            AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType = '''' THEN dbo.MPUPaymentTransaction.FormType + ''%'' ELSE @FormType + ''%'' END)
            AND (@PaymentType = ''''
                OR dbo.MPUPaymentTransaction.PaymentType = @PaymentType
                OR (@PaymentType = ''CitizenPay''
                    AND REPLACE(REPLACE(REPLACE(LOWER(ISNULL(dbo.MPUPaymentTransaction.PaymentType, '''')), '' '', ''''), ''-'', ''''), ''_'', '''')
                        IN (''citizenpay'', ''citizen'', ''cp'')))
            AND dbo.MPUPaymentTransaction.MOCAmount = @OnlineFeeAmount
        ) c OPTION (RECOMPILE); '
        ELSE N'DECLARE @__total int = NULL; ' END;

    DECLARE @sql nvarchar(max) = @cntpart + N'
    SELECT pg.*, @__total AS TotalCount
    FROM (
        SELECT *
        FROM (
            SELECT MPUPaymentTransaction.Id,
                Sakhan,
                TransactionDateTime,
                ISNULL((SELECT TOP 1 CompanyName FROM PaThaka
                    WHERE PaThaka.PaThaKaNo = MPUPaymentTransaction.PaThaKaNo
                    OR PaThaka.CompanyRegistrationNo = MPUPaymentTransaction.PaThaKaNo), '''') CompanyName,
                MPUPaymentTransaction.PaThaKaNo CompanyRegistrationNo,
                dbo.MPUPaymentTransaction.ApplicationNo,
                MPUPaymentTransaction.MerchantId,
                AccountNo,
                InvoiceNo,
                ApprovalCode,
                TransactionRefNo,
                CONVERT(nvarchar(50), CONVERT(decimal(18, 2), ISNULL(TRY_CONVERT(decimal(18, 2),
                    CASE
                        WHEN LEN(ISNULL(TransactionAmount, '''')) > 2 THEN LEFT(TransactionAmount, LEN(TransactionAmount) - 2) + ''.'' + RIGHT(TransactionAmount, 2)
                        WHEN LEN(ISNULL(TransactionAmount, '''')) > 0 THEN ''0.'' + RIGHT(''00'' + TransactionAmount, 2)
                        ELSE ''0''
                    END), 0))) TransactionAmount,
                MOCAmount,
                IMAmount,
                CONVERT(nvarchar(50), ISNULL(TRY_CONVERT(decimal(18, 2),
                    CASE
                        WHEN LEN(ISNULL(TransactionAmount, '''')) > 2 THEN LEFT(TransactionAmount, LEN(TransactionAmount) - 2) + ''.'' + RIGHT(TransactionAmount, 2)
                        WHEN LEN(ISNULL(TransactionAmount, '''')) > 0 THEN ''0.'' + RIGHT(''00'' + TransactionAmount, 2)
                        ELSE ''0''
                    END), 0)
                    - ISNULL(TRY_CONVERT(decimal(18, 2), MOCAmount), 0)
                    - ISNULL(TRY_CONVERT(decimal(18, 2), IMAmount), 0)) MPUAmount,
                CONVERT(nvarchar(50), ISNULL(TRY_CONVERT(decimal(18, 2),
                    CASE
                        WHEN LEN(ISNULL(TransactionAmount, '''')) > 2 THEN LEFT(TransactionAmount, LEN(TransactionAmount) - 2) + ''.'' + RIGHT(TransactionAmount, 2)
                        WHEN LEN(ISNULL(TransactionAmount, '''')) > 0 THEN ''0.'' + RIGHT(''00'' + TransactionAmount, 2)
                        ELSE ''0''
                    END), 0)
                    - ISNULL(TRY_CONVERT(decimal(18, 2), MOCAmount), 0)) AmountDiff,
                dbo.MPUPaymentTransaction.FormType,
                dbo.MPUPaymentTransaction.ApplyType,
                (SELECT TOP 1 VoucherNo FROM AccountTransaction
                    WHERE MPUPaymentTransaction.TransactionId = AccountTransaction.TransactionId
                    AND AccountTransaction.TotalAmount <> @OnlineFeeTotalAmount
                    ORDER BY CreatedDate DESC) VoucherNo
            FROM dbo.MPUPaymentTransaction
            WHERE dbo.MPUPaymentTransaction.ResponseCode = ''00''
            AND dbo.MPUPaymentTransaction.TransactionDateTime IS NOT NULL
            AND (TransactionDateTime >= @FromDate AND TransactionDateTime <= @ToDate)
            AND dbo.MPUPaymentTransaction.FormType IS NOT NULL
            AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType = '''' THEN dbo.MPUPaymentTransaction.FormType + ''%'' ELSE @FormType + ''%'' END)
            AND (@PaymentType = ''''
                OR dbo.MPUPaymentTransaction.PaymentType = @PaymentType
                OR (@PaymentType = ''CitizenPay''
                    AND REPLACE(REPLACE(REPLACE(LOWER(ISNULL(dbo.MPUPaymentTransaction.PaymentType, '''')), '' '', ''''), ''-'', ''''), ''_'', '''')
                        IN (''citizenpay'', ''citizen'', ''cp'')))
            AND MPUPaymentTransaction.MOCAmount <> @OnlineFeeAmount
            UNION
            SELECT MPUPaymentTransaction.Id,
                Sakhan,
                TransactionDateTime,
                ISNULL((SELECT TOP 1 CompanyName FROM PaThaka
                    WHERE PaThaka.PaThaKaNo = MPUPaymentTransaction.PaThaKaNo
                    OR PaThaka.CompanyRegistrationNo = MPUPaymentTransaction.PaThaKaNo), '''') CompanyName,
                MPUPaymentTransaction.PaThaKaNo CompanyRegistrationNo,
                dbo.MPUPaymentTransaction.ApplicationNo,
                MPUPaymentTransaction.MerchantId,
                AccountNo,
                InvoiceNo,
                ApprovalCode,
                TransactionRefNo,
                CONVERT(nvarchar(50), CONVERT(decimal(18, 2), ISNULL(TRY_CONVERT(decimal(18, 2),
                    CASE
                        WHEN LEN(ISNULL(TransactionAmount, '''')) > 2 THEN LEFT(TransactionAmount, LEN(TransactionAmount) - 2) + ''.'' + RIGHT(TransactionAmount, 2)
                        WHEN LEN(ISNULL(TransactionAmount, '''')) > 0 THEN ''0.'' + RIGHT(''00'' + TransactionAmount, 2)
                        ELSE ''0''
                    END), 0))) TransactionAmount,
                MOCAmount,
                IMAmount,
                CONVERT(nvarchar(50), ISNULL(TRY_CONVERT(decimal(18, 2),
                    CASE
                        WHEN LEN(ISNULL(TransactionAmount, '''')) > 2 THEN LEFT(TransactionAmount, LEN(TransactionAmount) - 2) + ''.'' + RIGHT(TransactionAmount, 2)
                        WHEN LEN(ISNULL(TransactionAmount, '''')) > 0 THEN ''0.'' + RIGHT(''00'' + TransactionAmount, 2)
                        ELSE ''0''
                    END), 0)
                    - ISNULL(TRY_CONVERT(decimal(18, 2), MOCAmount), 0)
                    - ISNULL(TRY_CONVERT(decimal(18, 2), IMAmount), 0)) MPUAmount,
                CONVERT(nvarchar(50), ISNULL(TRY_CONVERT(decimal(18, 2),
                    CASE
                        WHEN LEN(ISNULL(TransactionAmount, '''')) > 2 THEN LEFT(TransactionAmount, LEN(TransactionAmount) - 2) + ''.'' + RIGHT(TransactionAmount, 2)
                        WHEN LEN(ISNULL(TransactionAmount, '''')) > 0 THEN ''0.'' + RIGHT(''00'' + TransactionAmount, 2)
                        ELSE ''0''
                    END), 0)
                    - ISNULL(TRY_CONVERT(decimal(18, 2), MOCAmount), 0)) AmountDiff,
                dbo.MPUPaymentTransaction.FormType,
                dbo.MPUPaymentTransaction.ApplyType,
                (SELECT TOP 1 VoucherNo FROM AccountTransaction
                    WHERE MPUPaymentTransaction.TransactionId = AccountTransaction.TransactionId
                    AND AccountTransaction.TotalAmount = @OnlineFeeTotalAmount
                    ORDER BY CreatedDate DESC) VoucherNo
            FROM dbo.MPUPaymentTransaction
            WHERE dbo.MPUPaymentTransaction.ResponseCode = ''00''
            AND dbo.MPUPaymentTransaction.TransactionDateTime IS NOT NULL
            AND (TransactionDateTime >= @FromDate AND TransactionDateTime <= @ToDate)
            AND dbo.MPUPaymentTransaction.FormType IS NOT NULL
            AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType = '''' THEN dbo.MPUPaymentTransaction.FormType + ''%'' ELSE @FormType + ''%'' END)
            AND (@PaymentType = ''''
                OR dbo.MPUPaymentTransaction.PaymentType = @PaymentType
                OR (@PaymentType = ''CitizenPay''
                    AND REPLACE(REPLACE(REPLACE(LOWER(ISNULL(dbo.MPUPaymentTransaction.PaymentType, '''')), '' '', ''''), ''-'', ''''), ''_'', '''')
                        IN (''citizenpay'', ''citizen'', ''cp'')))
            AND MPUPaymentTransaction.MOCAmount = @OnlineFeeAmount
        ) baseRows
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';

    EXEC sp_executesql @sql,
        N'@FromDate datetime, @ToDate datetime, @FormType nvarchar(200), @PaymentType nvarchar(200), @OnlineFeeAmount nvarchar(50), @OnlineFeeTotalAmount decimal(18, 2), @off bigint, @ps bigint',
        @FromDate = @FromDate,
        @ToDate = @ToDate,
        @FormType = @FormType,
        @PaymentType = @PaymentType,
        @OnlineFeeAmount = @OnlineFeeAmount,
        @OnlineFeeTotalAmount = @OnlineFeeTotalAmount,
        @off = @off,
        @ps = @ps;
END

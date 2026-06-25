CREATE OR ALTER PROCEDURE [dbo].[sp_MPUReport_V3_pagination]
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

    DECLARE @ps int = CASE
        WHEN ISNULL(@PageSize, 0) <= 0 THEN 2147483647
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
        ELSE @PageSize END;
    DECLARE @off int = CASE WHEN ISNULL(@PageSize, 0) <= 0 THEN 0 ELSE ISNULL(@PageIndex, 0) * @PageSize END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder, 'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL AND @SortColumn IN (
        N'Id', N'Sakhan', N'TransactionDateTime', N'CompanyName', N'CompanyRegistrationNo',
        N'ApplicationNo', N'MerchantId', N'AccountNo', N'InvoiceNo', N'ApprovalCode',
        N'TransactionRefNo', N'TransactionAmount', N'MOCAmount', N'IMAmount', N'MPUAmount',
        N'AmountDiff', N'FormType', N'ApplyType', N'VoucherNo', N'TotalAmount', N'PaymentDate')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir
            + CASE WHEN @SortColumn = N'TransactionId' THEN N'' ELSE N', [TransactionId] ASC' END
            + CASE WHEN @SortColumn = N'TransactionDateTime' THEN N'' ELSE N', [TransactionDateTime] ASC' END
            + CASE WHEN @SortColumn = N'Id' THEN N'' ELSE N', [Id] ASC' END;
    ELSE
        SET @ob = N'[TransactionId] ASC, [TransactionDateTime] ASC, [Id] ASC';

    SELECT
        m.*,
        ROW_NUMBER() OVER (
            PARTITION BY m.TransactionId
            ORDER BY m.TransactionDateTime, m.Id
        ) AS rn
    INTO #mpu
    FROM dbo.MPUPaymentTransaction m
    WHERE m.TransactionDateTime >= @FromDate
        AND m.TransactionDateTime <= @ToDate
        AND m.ResponseCode = '00'
        AND m.FormType IS NOT NULL
        AND m.FormType LIKE (CASE WHEN @FormType = '' THEN m.FormType + '%' ELSE @FormType + '%' END)
        AND (@PaymentType = ''
            OR m.PaymentType = @PaymentType
            OR (@PaymentType = 'CitizenPay'
                AND REPLACE(REPLACE(REPLACE(LOWER(ISNULL(m.PaymentType, '')), ' ', ''), '-', ''), '_', '')
                    IN ('citizenpay', 'citizen', 'cp')));

    CREATE INDEX IX_mpu_TransactionId_rn ON #mpu(TransactionId, rn);
    CREATE INDEX IX_mpu_Order ON #mpu(TransactionId, TransactionDateTime, Id);

    SELECT
        a.TransactionId,
        a.VoucherNo,
        a.TotalAmount,
        a.PaymentDate,
        ROW_NUMBER() OVER (
            PARTITION BY a.TransactionId
            ORDER BY a.CreatedDate, a.Id
        ) AS rn
    INTO #acc
    FROM dbo.AccountTransaction a
    WHERE EXISTS (
        SELECT 1
        FROM #mpu m
        WHERE m.TransactionId = a.TransactionId
    );

    CREATE INDEX IX_acc_TransactionId_rn ON #acc(TransactionId, rn);

    SELECT
        m.Id,
        m.Sakhan,
        m.TransactionDateTime,
        ISNULL((
            SELECT TOP 1 p.CompanyName
            FROM dbo.PaThaKa p
            WHERE p.PaThaKaNo = m.PaThaKaNo
               OR p.CompanyRegistrationNo = m.PaThaKaNo
        ), '') AS CompanyName,
        m.PaThaKaNo AS CompanyRegistrationNo,
        m.ApplicationNo,
        m.MerchantId,
        m.AccountNo,
        m.InvoiceNo,
        m.ApprovalCode,
        m.TransactionRefNo,
        CONVERT(nvarchar(50), CONVERT(decimal(18, 2), ISNULL(TRY_CONVERT(decimal(18, 2),
            CASE
                WHEN LEN(ISNULL(m.TransactionAmount, '')) > 2 THEN LEFT(m.TransactionAmount, LEN(m.TransactionAmount) - 2) + '.' + RIGHT(m.TransactionAmount, 2)
                WHEN LEN(ISNULL(m.TransactionAmount, '')) > 0 THEN '0.' + RIGHT('00' + m.TransactionAmount, 2)
                ELSE '0'
            END), 0))) AS TransactionAmount,
        m.MOCAmount,
        m.IMAmount,
        CONVERT(nvarchar(50), ISNULL(TRY_CONVERT(decimal(18, 2),
            CASE
                WHEN LEN(ISNULL(m.TransactionAmount, '')) > 2 THEN LEFT(m.TransactionAmount, LEN(m.TransactionAmount) - 2) + '.' + RIGHT(m.TransactionAmount, 2)
                WHEN LEN(ISNULL(m.TransactionAmount, '')) > 0 THEN '0.' + RIGHT('00' + m.TransactionAmount, 2)
                ELSE '0'
            END), 0)
            - ISNULL(TRY_CONVERT(decimal(18, 2), m.MOCAmount), 0)
            - ISNULL(TRY_CONVERT(decimal(18, 2), m.IMAmount), 0)) AS MPUAmount,
        CONVERT(nvarchar(50), ISNULL(TRY_CONVERT(decimal(18, 2),
            CASE
                WHEN LEN(ISNULL(m.TransactionAmount, '')) > 2 THEN LEFT(m.TransactionAmount, LEN(m.TransactionAmount) - 2) + '.' + RIGHT(m.TransactionAmount, 2)
                WHEN LEN(ISNULL(m.TransactionAmount, '')) > 0 THEN '0.' + RIGHT('00' + m.TransactionAmount, 2)
                ELSE '0'
            END), 0)
            - ISNULL(TRY_CONVERT(decimal(18, 2), m.MOCAmount), 0)) AS AmountDiff,
        m.FormType,
        m.ApplyType,
        a.VoucherNo,
        a.TotalAmount,
        a.PaymentDate,
        m.TransactionId
    INTO #rows
    FROM #mpu m
    INNER JOIN #acc a
        ON m.TransactionId = a.TransactionId
       AND m.rn = a.rn
    WHERE a.VoucherNo IS NOT NULL;

    CREATE INDEX IX_rows_Order ON #rows(TransactionId, TransactionDateTime, Id);

    DECLARE @total int = CASE WHEN @IncludeTotalCount = 1 THEN (SELECT COUNT(*) FROM #rows) ELSE NULL END;
    DECLARE @sql nvarchar(max) = N'
        SELECT
            Id,
            Sakhan,
            TransactionDateTime,
            CompanyName,
            CompanyRegistrationNo,
            ApplicationNo,
            MerchantId,
            AccountNo,
            InvoiceNo,
            ApprovalCode,
            TransactionRefNo,
            TransactionAmount,
            MOCAmount,
            IMAmount,
            MPUAmount,
            AmountDiff,
            FormType,
            ApplyType,
            VoucherNo,
            TotalAmount,
            PaymentDate,
            @total AS TotalCount
        FROM #rows
        ORDER BY ' + @ob + N'
        OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY;';

    EXEC sp_executesql @sql,
        N'@off int, @ps int, @total int',
        @off = @off,
        @ps = @ps,
        @total = @total;
END

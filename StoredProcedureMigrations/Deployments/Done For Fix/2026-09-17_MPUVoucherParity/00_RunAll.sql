/* =====================================================================================
   MPU Voucher No parity deployment - 2026-09-17

   Run this ONE file to apply both procedures, or run 01_... then 02_... directly.
   Either way: PROCEDURES FIRST, APPLICATION SECOND. Against the stale procedures the
   reports keep printing the wrong voucher numbers.

   Target database: TradeNetDB  (NOT ReportTemplateDB - that one only holds the Excel
   export job queue; deploying report procedures into it is a known trap.)

   What changes:

     sp_MPUReport_V3_pagination  MPU Report V3. The charge-to-voucher pairing is rebuilt
                                 so that (a) neither sequence is numbered over the report's
                                 own filtered rows - which made a row's Voucher No depend
                                 on the date range the user picked - (b) only rows Account
                                 Summary can display (IsPayment = 1 with a voucher) are
                                 candidates, and (c) a payment more than two days from any
                                 of the application's card charges is not a candidate at
                                 all, because it was not paid by card.

     sp_MPUReport_pagination     MPU Report V1. Keeps its own TOP 1 / @OnlineFeeTotalAmount
                                 rule; all four subqueries gain the same IsPayment = 1 and
                                 VoucherNo IS NOT NULL restriction.

   Measured on the live PROD API before this release: 8 of 112 rows present in two nested
   date windows carried a DIFFERENT Voucher No in each, and 5-7% of rows showed a voucher
   from a different month than the card charge (a 2025-02-10 charge printing U04202443562,
   April 2024). On those same 1,000 rows, every correctly-paired row was paid within one
   day of the charge and every mispaired one was a median 42 days away - which is what
   rule (c) keys on. See README.md for the figures and VerifyDeployment.sql for the
   acceptance tests.

   Run CaptureRollback.sql FIRST and save its grid - that is the rollback artifact.
   ===================================================================================== */

USE [TradeNetDB];
GO
SET QUOTED_IDENTIFIER ON;
GO
SET ANSI_NULLS ON;
GO

IF DB_NAME() <> N'TradeNetDB'
BEGIN
    RAISERROR(N'Wrong database: these report procedures belong in TradeNetDB.', 16, 1);
    SET NOEXEC ON;
END;
GO

/* ===================================================================================
   01 - sp_MPUReport_V3_pagination
   =================================================================================== */
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

    -- How far a payment may sit from the card charge it belongs to. 2 days is double the
    -- widest gap seen on any correctly-paired PROD row, and a thirtieth of the median gap
    -- on the mispaired ones, so the two populations do not overlap anywhere near it.
    DECLARE @PairingToleranceDays int = 2;

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

    -- MPUPaymentTransaction has no foreign key to AccountTransaction: the only link is
    -- the shared application id, one-to-many on both sides, so the voucher is found by
    -- pairing the Nth successful card charge with the Nth payment record. Two rules keep
    -- that pairing honest, and breaking either is what the 2026-09-17 ငွေစာရင်း complaint
    -- ("MPU Report V3 မှာ ပြနေတဲ့ voucher no တွေက မှားနေပါတယ်") was reporting:
    --
    --   1. Both sequences must be numbered over the SAME universe the pairing assumes —
    --      successful charges against payment vouchers — or the two ordinals mean
    --      different things.
    --   2. Neither may be numbered over the REPORT'S filtered rows. Numbering after the
    --      date window made a row's voucher depend on the range the user picked: the same
    --      charge read U01... over one week and U02... over two months (measured on PROD,
    --      2025-02, 8 of 112 rows), and 5-7% of rows showed a voucher from a different
    --      month entirely -- a February charge printing an April 2024 voucher.
    --
    -- So: pick the applications the filters select, number their WHOLE history, and only
    -- then narrow to the rows the report shows.
    SELECT DISTINCT m.TransactionId
    INTO #ids
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

    CREATE INDEX IX_ids_TransactionId ON #ids(TransactionId);

    -- Every successful charge of those applications, whenever it happened. A failed
    -- attempt creates no AccountTransaction, so it must not consume an ordinal.
    SELECT
        m.*,
        ROW_NUMBER() OVER (
            PARTITION BY m.TransactionId
            ORDER BY m.TransactionDateTime, m.Id
        ) AS rn
    INTO #mpuAll
    FROM dbo.MPUPaymentTransaction m
    WHERE m.ResponseCode = '00'
        AND EXISTS (SELECT 1 FROM #ids i WHERE i.TransactionId = m.TransactionId);

    CREATE INDEX IX_mpuAll_TransactionId_rn ON #mpuAll(TransactionId, rn);

    -- Now, and only now, the report's own filters.
    SELECT *
    INTO #mpu
    FROM #mpuAll m
    WHERE m.TransactionDateTime >= @FromDate
        AND m.TransactionDateTime <= @ToDate
        AND m.FormType IS NOT NULL
        AND m.FormType LIKE (CASE WHEN @FormType = '' THEN m.FormType + '%' ELSE @FormType + '%' END)
        AND (@PaymentType = ''
            OR m.PaymentType = @PaymentType
            OR (@PaymentType = 'CitizenPay'
                AND REPLACE(REPLACE(REPLACE(LOWER(ISNULL(m.PaymentType, '')), ' ', ''), '-', ''), '_', '')
                    IN ('citizenpay', 'citizen', 'cp')));

    CREATE INDEX IX_mpu_TransactionId_rn ON #mpu(TransactionId, rn);
    CREATE INDEX IX_mpu_Order ON #mpu(TransactionId, TransactionDateTime, Id);

    -- The candidates are exactly the rows Account Summary itself can display: a payment
    -- carrying a voucher (sp_AccountSummaryReport_pagination.sql:58). Anything else --
    -- a provisional or non-payment record -- holds a voucher number this report must
    -- never print, because Account Summary never will.
    --
    -- They are narrowed once more, to the payments that actually belong to a card charge.
    -- An application's fees are not all paid by card, and a payment made at the counter
    -- still creates an AccountTransaction with a voucher; left in, it takes an ordinal
    -- and pushes every later charge onto the wrong voucher. Time separates the two
    -- cleanly -- measured over 1,000 PROD rows for 2025-02:
    --
    --     voucher month == charge month (950 rows):  100.0% paid within 1 day, median 0.00
    --     voucher month != charge month ( 50 rows):    0.0% paid within 1 day, median 42.3
    --
    -- so a payment more than @PairingToleranceDays from every one of the application's
    -- charges is not a card payment, and is not a candidate.
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
    WHERE a.IsPayment = 1
        AND a.VoucherNo IS NOT NULL
        AND EXISTS (SELECT 1 FROM #ids i WHERE i.TransactionId = a.TransactionId)
        AND EXISTS (
            SELECT 1
            FROM #mpuAll m
            WHERE m.TransactionId = a.TransactionId
                AND m.TransactionDateTime IS NOT NULL
                AND ABS(DATEDIFF(day, ISNULL(a.PaymentDate, a.CreatedDate), m.TransactionDateTime))
                    <= @PairingToleranceDays);

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
       AND m.rn = a.rn;

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

GO

/* ===================================================================================
   02 - sp_MPUReport_pagination
   =================================================================================== */
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
        N'FormType', N'ApplyType', N'VoucherNo', N'TotalAmount')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir
            + CASE WHEN @SortColumn = N'TransactionDateTime' THEN N'' ELSE N', [TransactionDateTime] ASC' END
            + CASE WHEN @SortColumn = N'Id' THEN N'' ELSE N', [Id] ASC' END;
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
                -- IsPayment = 1 and a non-null voucher are the rows Account Summary can
                -- display; without them this picked up provisional records holding a
                -- voucher that report never prints (2026-09-17 ငွေစာရင်း complaint, the
                -- same defect fixed in sp_MPUReport_V3_pagination.sql). Both subqueries
                -- must carry the identical predicate or the voucher and the amount come
                -- from two different rows.
                (SELECT TOP 1 VoucherNo FROM AccountTransaction
                    WHERE MPUPaymentTransaction.TransactionId = AccountTransaction.TransactionId
                    AND AccountTransaction.IsPayment = 1
                    AND AccountTransaction.VoucherNo IS NOT NULL
                    AND AccountTransaction.TotalAmount <> @OnlineFeeTotalAmount
                    ORDER BY CreatedDate DESC) VoucherNo,
                (SELECT TOP 1 TotalAmount FROM AccountTransaction
                    WHERE MPUPaymentTransaction.TransactionId = AccountTransaction.TransactionId
                    AND AccountTransaction.IsPayment = 1
                    AND AccountTransaction.VoucherNo IS NOT NULL
                    AND AccountTransaction.TotalAmount <> @OnlineFeeTotalAmount
                    ORDER BY CreatedDate DESC) TotalAmount
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
                -- The online-fee half of the same rule; see the branch above.
                (SELECT TOP 1 VoucherNo FROM AccountTransaction
                    WHERE MPUPaymentTransaction.TransactionId = AccountTransaction.TransactionId
                    AND AccountTransaction.IsPayment = 1
                    AND AccountTransaction.VoucherNo IS NOT NULL
                    AND AccountTransaction.TotalAmount = @OnlineFeeTotalAmount
                    ORDER BY CreatedDate DESC) VoucherNo,
                (SELECT TOP 1 TotalAmount FROM AccountTransaction
                    WHERE MPUPaymentTransaction.TransactionId = AccountTransaction.TransactionId
                    AND AccountTransaction.IsPayment = 1
                    AND AccountTransaction.VoucherNo IS NOT NULL
                    AND AccountTransaction.TotalAmount = @OnlineFeeTotalAmount
                    ORDER BY CreatedDate DESC) TotalAmount
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

GO

SET NOEXEC OFF;
GO

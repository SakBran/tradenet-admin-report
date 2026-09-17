/* =====================================================================================
   MPU Voucher No parity deployment - 2026-09-17
   Run AFTER 00_RunAll.sql. Read-only: every section only SELECTs.
   ===================================================================================== */

USE [TradeNetDB];
GO
SET QUOTED_IDENTIFIER ON;
GO

/* -------------------------------------------------------------------------------------
   1. Did both procedures actually change?
   ------------------------------------------------------------------------------------- */
SELECT
    p.name,
    p.modify_date,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%IsPayment = 1%'
         THEN 'OK - carries the IsPayment restriction'
         ELSE 'STALE - this release was not applied'
    END AS state
FROM sys.procedures p
WHERE p.name IN ('sp_MPUReport_V3_pagination', 'sp_MPUReport_pagination')
ORDER BY p.name;
GO

/* -------------------------------------------------------------------------------------
   2. THE ACCEPTANCE TEST - the voucher must not move with the date range.

   The same week is read twice: once on its own, once inside the two months around it. A
   row that appears in both MUST carry the same Voucher No. Before this release 8 of 112
   such rows differed; afterwards the count must be 0.
   ------------------------------------------------------------------------------------- */
DROP TABLE IF EXISTS #narrow;
DROP TABLE IF EXISTS #wide;

CREATE TABLE #narrow (
    Id int, Sakhan nvarchar(200), TransactionDateTime datetime, CompanyName nvarchar(500),
    CompanyRegistrationNo nvarchar(50), ApplicationNo nvarchar(50), MerchantId nvarchar(50),
    AccountNo nvarchar(50), InvoiceNo nvarchar(50), ApprovalCode nvarchar(50),
    TransactionRefNo nvarchar(50), TransactionAmount nvarchar(50), MOCAmount nvarchar(50),
    IMAmount nvarchar(50), MPUAmount nvarchar(50), AmountDiff nvarchar(50),
    FormType nvarchar(50), ApplyType nvarchar(50), VoucherNo nvarchar(50),
    TotalAmount float, PaymentDate datetime, TotalCount int);

CREATE TABLE #wide (
    Id int, Sakhan nvarchar(200), TransactionDateTime datetime, CompanyName nvarchar(500),
    CompanyRegistrationNo nvarchar(50), ApplicationNo nvarchar(50), MerchantId nvarchar(50),
    AccountNo nvarchar(50), InvoiceNo nvarchar(50), ApprovalCode nvarchar(50),
    TransactionRefNo nvarchar(50), TransactionAmount nvarchar(50), MOCAmount nvarchar(50),
    IMAmount nvarchar(50), MPUAmount nvarchar(50), AmountDiff nvarchar(50),
    FormType nvarchar(50), ApplyType nvarchar(50), VoucherNo nvarchar(50),
    TotalAmount float, PaymentDate datetime, TotalCount int);

INSERT INTO #narrow
EXEC dbo.sp_MPUReport_V3_pagination
    @FromDate = '2025-02-10 00:00:00', @ToDate = '2025-02-16 23:59:59',
    @FormType = N'', @PaymentType = N'', @PageSize = 0, @IncludeTotalCount = 0;

INSERT INTO #wide
EXEC dbo.sp_MPUReport_V3_pagination
    @FromDate = '2025-01-01 00:00:00', @ToDate = '2025-02-28 23:59:59',
    @FormType = N'', @PaymentType = N'', @PageSize = 0, @IncludeTotalCount = 0;

SELECT
    (SELECT COUNT(*) FROM #narrow) AS narrow_rows,
    (SELECT COUNT(*) FROM #wide)   AS wide_rows,
    COUNT(*)                        AS rows_in_both,
    SUM(CASE WHEN ISNULL(n.VoucherNo, '') <> ISNULL(w.VoucherNo, '') THEN 1 ELSE 0 END)
                                    AS voucher_changed_MUST_BE_ZERO
FROM #narrow n
INNER JOIN #wide w ON w.Id = n.Id;
GO

-- The offenders, if any survived.
SELECT TOP 50 n.Id, n.CompanyRegistrationNo, n.TransactionDateTime,
       n.VoucherNo AS narrow_voucher, w.VoucherNo AS wide_voucher
FROM #narrow n
INNER JOIN #wide w ON w.Id = n.Id
WHERE ISNULL(n.VoucherNo, '') <> ISNULL(w.VoucherNo, '')
ORDER BY n.Id;
GO

/* -------------------------------------------------------------------------------------
   3. THE SECOND ACCEPTANCE TEST - the voucher must belong to the month of the charge.

   A voucher number carries its own month and year: 'U02202515580' is February 2025. A
   February charge printing a November 2024 voucher is the pairing landing on an unrelated
   account transaction. Before this release 5-7% of rows failed this; afterwards the share
   should be ~0. A handful of genuine cases can survive (a fee paid in a later month than
   the charge), so read the share, not a hard zero.
   ------------------------------------------------------------------------------------- */
SELECT
    COUNT(*) AS rows_checked,
    SUM(CASE WHEN SUBSTRING(w.VoucherNo, 2, 2) = FORMAT(w.TransactionDateTime, 'MM')
              AND SUBSTRING(w.VoucherNo, 4, 4) = FORMAT(w.TransactionDateTime, 'yyyy')
             THEN 1 ELSE 0 END) AS voucher_month_matches,
    SUM(CASE WHEN SUBSTRING(w.VoucherNo, 2, 2) = FORMAT(w.TransactionDateTime, 'MM')
              AND SUBSTRING(w.VoucherNo, 4, 4) = FORMAT(w.TransactionDateTime, 'yyyy')
             THEN 0 ELSE 1 END) AS voucher_month_differs
FROM #wide w
WHERE w.VoucherNo LIKE '[A-Z][0-9][0-9][0-9][0-9][0-9][0-9]%'
    AND w.TransactionDateTime IS NOT NULL;
GO

SELECT TOP 30 w.Id, w.CompanyRegistrationNo, w.TransactionDateTime, w.VoucherNo
FROM #wide w
WHERE w.VoucherNo LIKE '[A-Z][0-9][0-9][0-9][0-9][0-9][0-9]%'
    AND w.TransactionDateTime IS NOT NULL
    AND NOT (SUBSTRING(w.VoucherNo, 2, 2) = FORMAT(w.TransactionDateTime, 'MM')
         AND SUBSTRING(w.VoucherNo, 4, 4) = FORMAT(w.TransactionDateTime, 'yyyy'))
ORDER BY w.TransactionDateTime;
GO

/* -------------------------------------------------------------------------------------
   4. Every voucher the report prints must be one Account Summary can also print, i.e. it
      must sit on an IsPayment = 1 row. This must return 0.
   ------------------------------------------------------------------------------------- */
SELECT COUNT(*) AS vouchers_account_summary_cannot_show_MUST_BE_ZERO
FROM #wide w
WHERE w.VoucherNo IS NOT NULL
    AND NOT EXISTS (
        SELECT 1 FROM dbo.AccountTransaction a
        WHERE a.VoucherNo = w.VoucherNo AND a.IsPayment = 1);
GO

/* -------------------------------------------------------------------------------------
   5. Row count, for the record. The old shape dropped a row whenever the wrongly-picked
      account transaction had no voucher at all, so a small INCREASE here is expected and
      correct. Note it against the count CaptureRollback.sql was run alongside.
   ------------------------------------------------------------------------------------- */
SELECT
    COUNT(*)                                        AS rows_2025_01_to_02,
    COUNT(DISTINCT VoucherNo)                       AS distinct_vouchers,
    SUM(CASE WHEN VoucherNo IS NULL THEN 1 ELSE 0 END) AS null_vouchers_MUST_BE_ZERO
FROM #wide;
GO

DROP TABLE IF EXISTS #narrow;
DROP TABLE IF EXISTS #wide;
GO

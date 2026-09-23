USE [TradeNetDB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET QUOTED_IDENTIFIER ON;   -- required for filtered indexes
SET ANSI_NULLS ON;
GO

/*
    Payment report indexes -- MPU Report and MPU Report V3 (2026-09-23)
    -------------------------------------------------------------------
    Run manually in SSMS / Azure Data Studio against TradeNetDB, in quiet hours.
    Safe to rerun: each index is created only if no index of that name exists.
    Nothing is dropped or rebuilt. ONLINE = ON is used where the edition supports it.

    Why (read from the code, not yet measured on production):
      * Backend/StoredProcedureToLinq/sp_MPUReport.cs -- per row, two subqueries into
        AccountTransaction by TransactionId (IsPayment = 1, VoucherNo NOT NULL,
        ORDER BY CreatedDate DESC) and one into PaThaKa by PaThaKaNo OR
        CompanyRegistrationNo.
      * StoredProcedureMigrations/sp_MPUReport_V3_pagination.sql -- filters
        MPUPaymentTransaction by ResponseCode = '00' + TransactionDateTime range, then
        numbers each TransactionId's whole history in MPUPaymentTransaction and
        AccountTransaction.
      No existing report index can seek by TransactionId: the AccountTransaction ones
      lead with IsPayment + PaymentDate/VoucherDate, and the MPUPaymentTransaction one
      with TransactionRefNo (TradeNetReportIndexes_Production.sql, candidates 7, 8, 10).

    BEFORE running on production:
      EXEC sp_helpindex 'dbo.AccountTransaction';
      EXEC sp_helpindex 'dbo.MPUPaymentTransaction';
      EXEC sp_helpindex 'dbo.PaThaKa';
    Skip any index below whose key columns an existing index already leads with.

    MEASURE: run MPU Report V3 for one month with SET STATISTICS IO, TIME ON before
    and after; logical reads on AccountTransaction should drop sharply.

    ROLLBACK: the DROP statements are at the bottom, commented out.
*/

-- Same batch as the CREATEs on purpose: a THROW only stops its own batch.
IF DB_NAME() <> N'TradeNetDB'
    THROW 50001, N'Abort: run this script only against TradeNetDB.', 1;

DECLARE @Online bit =
    CASE WHEN CONVERT(int, SERVERPROPERTY(N'EngineEdition')) IN (3, 5, 8) THEN 1 ELSE 0 END;
PRINT N'ONLINE index builds: ' + CASE WHEN @Online = 1 THEN N'yes' ELSE N'no (table is locked while each index builds)' END;

DECLARE @With nvarchar(200) = N' WITH (SORT_IN_TEMPDB = ON'
    + CASE WHEN @Online = 1 THEN N', ONLINE = ON' ELSE N'' END + N');';

DECLARE @Indexes TABLE (Ord int, TableName sysname, IndexName sysname, Body nvarchar(max));
INSERT INTO @Indexes (Ord, TableName, IndexName, Body)
VALUES
    -- 1. Voucher No / Total Amount lookups (both reports) and V3's #acc numbering.
    (1, N'AccountTransaction', N'IX_AccountTransaction_MPU_TransactionId',
        N'ON dbo.AccountTransaction (TransactionId, CreatedDate) '
      + N'INCLUDE (IsPayment, VoucherNo, TotalAmount) '
      + N'WHERE IsPayment = 1 AND VoucherNo IS NOT NULL'),
    -- 2. The date-range filter both reports start from.
    (2, N'MPUPaymentTransaction', N'IX_MPUPaymentTransaction_Report',
        N'ON dbo.MPUPaymentTransaction (TransactionDateTime) '
      + N'INCLUDE (TransactionId, FormType, PaymentType, MOCAmount, PaThaKaNo) '
      + N'WHERE ResponseCode = ''00'''),
    -- 3. V3's per-transaction history (PARTITION BY TransactionId ORDER BY TransactionDateTime, Id).
    (3, N'MPUPaymentTransaction', N'IX_MPUPaymentTransaction_TransactionHistory',
        N'ON dbo.MPUPaymentTransaction (TransactionId, TransactionDateTime, Id) '
      + N'WHERE ResponseCode = ''00'''),
    -- 4. Company Name lookup: the OR needs BOTH columns indexed.
    (4, N'PaThaKa', N'IX_PaThaKa_PaThaKaNo',
        N'ON dbo.PaThaKa (PaThaKaNo) INCLUDE (CompanyName)'),
    (5, N'PaThaKa', N'IX_PaThaKa_CompanyRegistrationNo',
        N'ON dbo.PaThaKa (CompanyRegistrationNo) INCLUDE (CompanyName)');

DECLARE @Ord int = 1, @Table sysname, @Name sysname, @Body nvarchar(max), @Sql nvarchar(max);
WHILE @Ord <= 5
BEGIN
    SELECT @Table = TableName, @Name = IndexName, @Body = Body FROM @Indexes WHERE Ord = @Ord;

    IF EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.' + @Table) AND name = @Name)
    BEGIN
        PRINT N'SKIP   ' + @Name + N' (already exists)';
    END
    ELSE
    BEGIN
        SET @Sql = N'CREATE NONCLUSTERED INDEX ' + QUOTENAME(@Name) + N' ' + @Body + @With;
        PRINT N'CREATE ' + @Name + N' ...';
        EXEC sys.sp_executesql @Sql;
        PRINT N'       done';
    END;

    SET @Ord += 1;
END;
GO

/*
-- ROLLBACK
DROP INDEX IF EXISTS [IX_AccountTransaction_MPU_TransactionId]     ON [dbo].[AccountTransaction];
DROP INDEX IF EXISTS [IX_MPUPaymentTransaction_Report]             ON [dbo].[MPUPaymentTransaction];
DROP INDEX IF EXISTS [IX_MPUPaymentTransaction_TransactionHistory] ON [dbo].[MPUPaymentTransaction];
DROP INDEX IF EXISTS [IX_PaThaKa_PaThaKaNo]                        ON [dbo].[PaThaKa];
DROP INDEX IF EXISTS [IX_PaThaKa_CompanyRegistrationNo]            ON [dbo].[PaThaKa];
*/

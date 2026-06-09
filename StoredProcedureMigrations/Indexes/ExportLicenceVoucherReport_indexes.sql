USE [TradeNetDB]
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_AccountTransaction_ExportLicenceVoucher'
      AND object_id = OBJECT_ID(N'[dbo].[AccountTransaction]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccountTransaction_ExportLicenceVoucher]
    ON [dbo].[AccountTransaction]
    (
        [TransactionFormType],
        [IsPayment],
        [PaymentDate],
        [TransactionId]
    )
    INCLUDE
    (
        [PaymentType],
        [VoucherNo],
        [VoucherDate],
        [TotalAmount]
    )
    WHERE [TransactionFormType] = N'Export Licence'
      AND [IsPayment] = 1;
END
GO

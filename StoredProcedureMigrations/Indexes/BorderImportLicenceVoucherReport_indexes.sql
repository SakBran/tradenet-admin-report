USE [TradeNetDB]
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_AccountTransaction_BorderImportLicenceVoucher'
      AND object_id = OBJECT_ID(N'[dbo].[AccountTransaction]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccountTransaction_BorderImportLicenceVoucher]
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
    WHERE [TransactionFormType] = N'Border Import Licence'
      AND [IsPayment] = 1;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_BorderImportLicenceItem_VoucherReport'
      AND object_id = OBJECT_ID(N'[dbo].[BorderImportLicenceItem]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BorderImportLicenceItem_VoucherReport]
    ON [dbo].[BorderImportLicenceItem]
    (
        [BorderImportLicenceId],
        [CurrencyId]
    )
    INCLUDE
    (
        [Amount]
    );
END
GO

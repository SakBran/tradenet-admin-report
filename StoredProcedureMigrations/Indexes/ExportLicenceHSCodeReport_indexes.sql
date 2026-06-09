USE [TradeNetDB]
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExportLicence_HSCodeReport_LicenceDate'
      AND object_id = OBJECT_ID(N'[dbo].[ExportLicence]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExportLicence_HSCodeReport_LicenceDate]
    ON [dbo].[ExportLicence]
    (
        [ApplyType],
        [Status],
        [LicenceDate],
        [Id]
    )
    INCLUDE
    (
        [PaThaKaId],
        [ExportLicenceNo]
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExportLicenceItem_HSCodeReport_Licence'
      AND object_id = OBJECT_ID(N'[dbo].[ExportLicenceItem]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExportLicenceItem_HSCodeReport_Licence]
    ON [dbo].[ExportLicenceItem]
    (
        [ExportLicenceId]
    )
    INCLUDE
    (
        [HSCodeId],
        [CurrencyId],
        [Amount]
    );
END
GO

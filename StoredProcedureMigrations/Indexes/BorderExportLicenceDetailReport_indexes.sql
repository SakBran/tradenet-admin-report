USE [TradeNetDB]
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_BorderExportLicence_Report_NewDetail'
      AND object_id = OBJECT_ID(N'[dbo].[BorderExportLicence]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BorderExportLicence_Report_NewDetail]
    ON [dbo].[BorderExportLicence]
    (
        [ApplyType],
        [Status],
        [CreatedDate],
        [CardType],
        [ExportImportSectionId],
        [ExportImportMethodId],
        [ExportImportIncotermId],
        [BuyerCountryId],
        [SakhanId]
    )
    INCLUDE
    (
        [Id],
        [PaThaKaId],
        [IndividualTradingId],
        [ExportLicenceNo],
        [IssuedDate]
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_BorderExportLicenceItem_Report_Licence'
      AND object_id = OBJECT_ID(N'[dbo].[BorderExportLicenceItem]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BorderExportLicenceItem_Report_Licence]
    ON [dbo].[BorderExportLicenceItem]
    (
        [BorderExportLicenceId]
    )
    INCLUDE
    (
        [Id],
        [HSCodeId],
        [UnitId],
        [CurrencyId],
        [Description],
        [Price],
        [Quantity],
        [Amount]
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_IndividualTrading_Report_TINNo'
      AND object_id = OBJECT_ID(N'[dbo].[IndividualTrading]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_IndividualTrading_Report_TINNo]
    ON [dbo].[IndividualTrading]
    (
        [TINNo],
        [Id],
        [PaThaKaTypeId]
    )
    INCLUDE
    (
        [Name],
        [UnitLevel],
        [StreetNumberStreetName],
        [QuarterCityTownship],
        [State],
        [Country],
        [PostalCode]
    );
END
GO

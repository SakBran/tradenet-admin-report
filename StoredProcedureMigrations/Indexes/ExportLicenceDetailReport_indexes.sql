USE [TradeNetDB]
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExportLicence_Report_NewDetail'
      AND object_id = OBJECT_ID(N'[dbo].[ExportLicence]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExportLicence_Report_NewDetail]
    ON [dbo].[ExportLicence]
    (
        [ApplyType],
        [Status],
        [CreatedDate],
        [ExportImportSectionId],
        [ExportImportMethodId],
        [ExportImportIncotermId],
        [BuyerCountryId],
        [PaThaKaId]
    )
    INCLUDE
    (
        [Id],
        [ExportLicenceNo],
        [IssuedDate],
        [PortofExportId],
        [PortofDischarge],
        [DestinationCountryId],
        [ConsignedCountryId],
        [CountryofOriginId],
        [LastDate],
        [BuyerName],
        [BuyerAddress],
        [Remark],
        [ApplicationNo],
        [ApplicationDate],
        [CommodityType],
        [ApproveDate],
        [LicenceDate],
        [auto]
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExportLicenceItem_Report_Licence'
      AND object_id = OBJECT_ID(N'[dbo].[ExportLicenceItem]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExportLicenceItem_Report_Licence]
    ON [dbo].[ExportLicenceItem]
    (
        [ExportLicenceId],
        [HSCodeId],
        [UnitId],
        [CurrencyId]
    )
    INCLUDE
    (
        [Description],
        [Price],
        [Quantity],
        [Amount]
    );
END
GO

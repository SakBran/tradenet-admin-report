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
    WHERE name = N'IX_ExportLicence_Report_NewDetail_Page'
      AND object_id = OBJECT_ID(N'[dbo].[ExportLicence]')
)
BEGIN
    SET NUMERIC_ROUNDABORT OFF;
    SET ANSI_PADDING ON;
    SET ANSI_WARNINGS ON;
    SET CONCAT_NULL_YIELDS_NULL ON;
    SET ARITHABORT ON;
    SET QUOTED_IDENTIFIER ON;
    SET ANSI_NULLS ON;

    CREATE NONCLUSTERED INDEX [IX_ExportLicence_Report_NewDetail_Page]
    ON [dbo].[ExportLicence]
    (
        [CreatedDate],
        [Id]
    )
    INCLUDE
    (
        [IssuedDate],
        [ExportLicenceNo],
        [PaThaKaId],
        [ExportImportSectionId],
        [ExportImportMethodId],
        [ExportImportIncotermId],
        [BuyerCountryId]
    )
    WHERE [ApplyType] = N'New'
      AND [Status] = N'Approved'
    WITH (MAXDOP = 1);
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

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExportLicenceItem_Report_Licence_Page'
      AND object_id = OBJECT_ID(N'[dbo].[ExportLicenceItem]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExportLicenceItem_Report_Licence_Page]
    ON [dbo].[ExportLicenceItem]
    (
        [ExportLicenceId],
        [HSCode],
        [ItemNo],
        [Id]
    )
    INCLUDE
    (
        [UniqueId],
        [HSCodeId],
        [UnitId],
        [CurrencyId],
        [Description],
        [Price],
        [Quantity],
        [Amount]
    )
    WITH (MAXDOP = 1);
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExportLicenceItem_Report_Licence_Page'
      AND object_id = OBJECT_ID(N'[dbo].[ExportLicenceItem]')
)
AND NOT EXISTS (
    SELECT 1
    FROM sys.indexes AS i
    INNER JOIN sys.index_columns AS ic
        ON ic.object_id = i.object_id
        AND ic.index_id = i.index_id
    INNER JOIN sys.columns AS c
        ON c.object_id = ic.object_id
        AND c.column_id = ic.column_id
    WHERE i.name = N'IX_ExportLicenceItem_Report_Licence_Page'
      AND i.object_id = OBJECT_ID(N'[dbo].[ExportLicenceItem]')
      AND c.name = N'Amount'
      AND ic.is_included_column = 1
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExportLicenceItem_Report_Licence_Page]
    ON [dbo].[ExportLicenceItem]
    (
        [ExportLicenceId],
        [HSCode],
        [ItemNo],
        [Id]
    )
    INCLUDE
    (
        [UniqueId],
        [HSCodeId],
        [UnitId],
        [CurrencyId],
        [Description],
        [Price],
        [Quantity],
        [Amount]
    )
    WITH (DROP_EXISTING = ON, MAXDOP = 1);
END
GO

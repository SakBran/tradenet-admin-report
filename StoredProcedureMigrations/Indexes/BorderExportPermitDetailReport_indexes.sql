USE [TradeNetDB]
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_BorderExportPermit_Report_NewDetail'
      AND object_id = OBJECT_ID(N'[dbo].[BorderExportPermit]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BorderExportPermit_Report_NewDetail]
    ON [dbo].[BorderExportPermit]
    (
        [ApplyType],
        [Status],
        [CreatedDate],
        [ExportImportSectionId],
        [BuyerCountryId],
        [SakhanId],
        [PaThaKaId]
    )
    INCLUDE
    (
        [Id],
        [ExportPermitNo],
        [IssuedDate],
        [PortofExportId],
        [PortofDischarge],
        [DestinationCountryId],
        [LastDate],
        [ConsignedCountryId],
        [CountryofOriginId],
        [NRCType],
        [NRCPrefixId],
        [NRCPrefixCodeId],
        [NRCNo],
        [PermitType],
        [Remark],
        [ApproveDate]
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_BorderExportPermitItem_Report_Permit'
      AND object_id = OBJECT_ID(N'[dbo].[BorderExportPermitItem]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BorderExportPermitItem_Report_Permit]
    ON [dbo].[BorderExportPermitItem]
    (
        [BorderExportPermitId],
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
    WHERE name = N'IX_PaThaKa_Report_CompanyRegistrationNo'
      AND object_id = OBJECT_ID(N'[dbo].[PaThaKa]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaThaKa_Report_CompanyRegistrationNo]
    ON [dbo].[PaThaKa]
    (
        [CompanyRegistrationNo],
        [Id],
        [PaThaKaTypeId]
    )
    INCLUDE
    (
        [CompanyName],
        [UnitLevel],
        [StreetNumberStreetName],
        [QuarterCityTownship],
        [State],
        [Country],
        [PostalCode]
    );
END
GO

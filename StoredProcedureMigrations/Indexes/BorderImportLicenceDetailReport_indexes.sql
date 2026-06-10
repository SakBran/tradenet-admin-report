IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_BorderImportLicence_DetailReport_Approved'
      AND object_id = OBJECT_ID(N'dbo.BorderImportLicence')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_BorderImportLicence_DetailReport_Approved
    ON dbo.BorderImportLicence (Status, ApplyType, CardType, CreatedDate)
    INCLUDE (
        Id,
        PaThaKaId,
        IndividualTradingId,
        ExportImportSectionId,
        ExportImportMethodId,
        ExportImportIncotermId,
        SellerCountryId,
        SakhanId
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_BorderImportLicence_DetailReport_Pending'
      AND object_id = OBJECT_ID(N'dbo.BorderImportLicence')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_BorderImportLicence_DetailReport_Pending
    ON dbo.BorderImportLicence (Status, ApplyType, CardType, ApplicationDate)
    INCLUDE (
        Id,
        PaThaKaId,
        IndividualTradingId,
        ExportImportSectionId,
        ExportImportMethodId,
        ExportImportIncotermId,
        SellerCountryId,
        SakhanId
    );
END;
GO

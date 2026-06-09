/*
Recommended indexes for dbo.sp_HSCodeReport Border Import Licence branch.
Review existing indexes first; do not add duplicates with the same leading keys.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_BorderImportLicence_HSCodeReport'
      AND object_id = OBJECT_ID('dbo.BorderImportLicence')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_BorderImportLicence_HSCodeReport
    ON dbo.BorderImportLicence (ApplyType, Status, CardType, LicenceDate, SakhanId)
    INCLUDE (PaThaKaId, IndividualTradingId, ExportImportSectionId, ImportLicenceNo);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_BorderImportLicenceItem_HSCodeReport_ByLicence'
      AND object_id = OBJECT_ID('dbo.BorderImportLicenceItem')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_BorderImportLicenceItem_HSCodeReport_ByLicence
    ON dbo.BorderImportLicenceItem (BorderImportLicenceId)
    INCLUDE (HSCodeId, CurrencyId, Amount);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_HSCode_Code_HSCodeReport'
      AND object_id = OBJECT_ID('dbo.HSCode')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_HSCode_Code_HSCodeReport
    ON dbo.HSCode (Code)
    INCLUDE (Description);
END
GO

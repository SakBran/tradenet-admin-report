USE [TradeNetDB];
GO

DECLARE @Definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_NewReport_pagination'));

SELECT
    IIF(@Definition LIKE N'%' + NCHAR(10) + N'ImportLicence.IssuedDate LicenceDate,%', 1, 0) AS ImportLicence,
    IIF(@Definition LIKE N'%' + NCHAR(10) + N'ImportPermit.IssuedDate LicenceDate,%', 1, 0) AS ImportPermit,
    IIF(@Definition LIKE N'%' + NCHAR(10) + N'ExportLicence.IssuedDate LicenceDate,%', 1, 0) AS ExportLicence,
    IIF(@Definition LIKE N'%' + NCHAR(10) + N'ExportPermit.IssuedDate LicenceDate,%', 1, 0) AS ExportPermit,
    IIF(@Definition LIKE N'%BorderImportLicence.IssuedDate LicenceDate,%', 1, 0) AS BorderImportLicence,
    IIF(@Definition LIKE N'%BorderImportPermit.IssuedDate LicenceDate,%', 1, 0) AS BorderImportPermit,
    IIF(@Definition LIKE N'%BorderExportLicence.IssuedDate LicenceDate,%', 1, 0) AS BorderExportLicence,
    IIF(@Definition LIKE N'%BorderExportPermit.IssuedDate LicenceDate,%', 1, 0) AS BorderExportPermit,
    IIF(@Definition NOT LIKE N'%CAST(NULL AS datetime) LicenceDate%', 1, 0) AS NoNullLicenceDateProjection;
GO

SELECT N'Import Licence' AS FormType, COUNT(*) AS ApprovedNewRows, COUNT(IssuedDate) AS RowsWithLicenceDate
FROM dbo.ImportLicence WHERE ApplyType = N'New' AND Status = N'Approved'
UNION ALL
SELECT N'Import Permit', COUNT(*), COUNT(IssuedDate)
FROM dbo.ImportPermit WHERE ApplyType = N'New' AND Status = N'Approved'
UNION ALL
SELECT N'Export Licence', COUNT(*), COUNT(IssuedDate)
FROM dbo.ExportLicence WHERE ApplyType = N'New' AND Status = N'Approved'
UNION ALL
SELECT N'Export Permit', COUNT(*), COUNT(IssuedDate)
FROM dbo.ExportPermit WHERE ApplyType = N'New' AND Status = N'Approved'
UNION ALL
SELECT N'Border Import Licence', COUNT(*), COUNT(IssuedDate)
FROM dbo.BorderImportLicence WHERE ApplyType = N'New' AND Status = N'Approved'
UNION ALL
SELECT N'Border Import Permit', COUNT(*), COUNT(IssuedDate)
FROM dbo.BorderImportPermit WHERE ApplyType = N'New' AND Status = N'Approved'
UNION ALL
SELECT N'Border Export Licence', COUNT(*), COUNT(IssuedDate)
FROM dbo.BorderExportLicence WHERE ApplyType = N'New' AND Status = N'Approved'
UNION ALL
SELECT N'Border Export Permit', COUNT(*), COUNT(IssuedDate)
FROM dbo.BorderExportPermit WHERE ApplyType = N'New' AND Status = N'Approved';
GO

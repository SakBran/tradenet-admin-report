USE [TradeNetDB];
GO

DECLARE @Definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_PendingReport_pagination'));

SELECT
    IIF(@Definition LIKE N'%ImportLicence.IssuedDate LicenceDate,%', 1, 0) AS ImportLicence,
    IIF(@Definition LIKE N'%BorderImportLicence.IssuedDate LicenceDate,%', 1, 0) AS BorderImportLicence;
GO

SELECT N'Import Licence' AS FormType, COUNT(*) AS PendingRows, COUNT(IssuedDate) AS RowsWithLicenceDate
FROM dbo.ImportLicence
WHERE Status IN (N'Pending', N'Reject')
UNION ALL
SELECT N'Border Import Licence', COUNT(*), COUNT(IssuedDate)
FROM dbo.BorderImportLicence
WHERE Status IN (N'Pending', N'Reject');
GO

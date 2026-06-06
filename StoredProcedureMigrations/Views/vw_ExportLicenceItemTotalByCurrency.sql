USE [TradeNetDB];
GO

SET NUMERIC_ROUNDABORT OFF;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE VIEW dbo.vw_ExportLicenceItemTotalByCurrency
WITH SCHEMABINDING
AS
SELECT
    eli.ExportLicenceId,
    eli.CurrencyId,
    SUM(eli.Amount) AS TotalAmount,
    COUNT_BIG(*) AS ItemCount
FROM dbo.ExportLicenceItem AS eli
GROUP BY
    eli.ExportLicenceId,
    eli.CurrencyId;
GO

CREATE UNIQUE CLUSTERED INDEX IX_vw_ExportLicenceItemTotalByCurrency
    ON dbo.vw_ExportLicenceItemTotalByCurrency (ExportLicenceId, CurrencyId);
GO

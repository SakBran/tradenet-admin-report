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

CREATE VIEW dbo.vw_ImportLicenceItemTotalByCurrency
WITH SCHEMABINDING
AS
SELECT
    ili.ImportLicenceId,
    ili.CurrencyId,
    SUM(ili.Amount) AS TotalAmount,
    COUNT_BIG(*) AS ItemCount
FROM dbo.ImportLicenceItem AS ili
GROUP BY
    ili.ImportLicenceId,
    ili.CurrencyId;
GO
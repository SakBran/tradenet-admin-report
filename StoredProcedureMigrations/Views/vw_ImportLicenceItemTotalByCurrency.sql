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

-- Materialize the view so the report join is an index seek instead of a
-- full re-aggregation of ImportLicenceItem on every request.
-- The unique clustered index must be on the GROUP BY columns.
CREATE UNIQUE CLUSTERED INDEX IX_vw_ImportLicenceItemTotalByCurrency
    ON dbo.vw_ImportLicenceItemTotalByCurrency (ImportLicenceId, CurrencyId);
GO
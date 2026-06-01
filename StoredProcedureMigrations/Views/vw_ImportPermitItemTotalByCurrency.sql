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

CREATE VIEW dbo.vw_ImportPermitItemTotalByCurrency
WITH SCHEMABINDING
AS
SELECT
    ipi.ImportPermitId,
    ipi.CurrencyId,
    SUM(ipi.Amount) AS TotalAmount,
    COUNT_BIG(*) AS ItemCount
FROM dbo.ImportPermitItem AS ipi
GROUP BY
    ipi.ImportPermitId,
    ipi.CurrencyId;
GO

-- Materialize the view so the report join is an index seek instead of a
-- full re-aggregation of ImportPermitItem on every request.
-- The unique clustered index must be on the GROUP BY columns.
CREATE UNIQUE CLUSTERED INDEX IX_vw_ImportPermitItemTotalByCurrency
    ON dbo.vw_ImportPermitItemTotalByCurrency (ImportPermitId, CurrencyId);
GO

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

CREATE VIEW dbo.vw_ExportPermitItemTotalByCurrency
WITH SCHEMABINDING
AS
SELECT
    epi.ExportPermitId,
    epi.CurrencyId,
    SUM(epi.Amount) AS TotalAmount,
    COUNT_BIG(*) AS ItemCount
FROM dbo.ExportPermitItem AS epi
GROUP BY
    epi.ExportPermitId,
    epi.CurrencyId;
GO

-- Materialize the view so the report join is an index seek instead of a
-- full re-aggregation of ExportPermitItem on every request.
-- The unique clustered index must be on the GROUP BY columns.
CREATE UNIQUE CLUSTERED INDEX IX_vw_ExportPermitItemTotalByCurrency
    ON dbo.vw_ExportPermitItemTotalByCurrency (ExportPermitId, CurrencyId);
GO

USE [TradeNetDB];
GO

SELECT
    p.name AS ProcedureName,
    p.modify_date AS LastModifiedAt,
    OBJECT_DEFINITION(p.object_id) AS CurrentDefinition
FROM sys.procedures AS p
WHERE p.name = N'sp_PendingReport_pagination';
GO

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

-- =============================================================================
-- Indexes supporting dbo.sp_PendingReport_pagination (Import Licence Pending /
-- Reject report).
--
-- WHY: the pending report had no index covering its filter + sort, so every
-- page load forced a full scan of dbo.ImportLicence plus a full sort to honour
-- ORDER BY ApplicationDate ASC, ApplicationNo ASC + OFFSET/FETCH. That cost
-- scales with the width of the date range (not the page size), so wide ranges
-- exceeded the 30s command timeout -- the de-facto "only 3 months" limit.
-- =============================================================================

-- 1) Base query: composite index keyed (Status, ApplicationDate, ApplicationNo).
--    Status leads because Pending/Reject are a small subset of all licences, so
--    the two equality seeks are highly selective; within each, rows are already
--    ordered by (ApplicationDate, ApplicationNo), matching the report's ORDER BY
--    so the engine seeks + merges instead of scanning + sorting the whole table.
--    INCLUDEd columns cover the derived-table projection + join/filter keys so
--    the seek is covering. (Id is the clustered key and is included implicitly.)
--
--    NOTE: deliberately NOT a filtered index (WHERE Status IN ...). A filtered
--    index would require ARITHABORT/QUOTED_IDENTIFIER/etc. ON for every writer
--    of ImportLicence; .NET SqlClient defaults ARITHABORT OFF, which would make
--    INSERT/UPDATE/DELETE on this shared table fail. A plain index has no such
--    requirement.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ImportLicence_PendingReport'
               AND object_id = OBJECT_ID('dbo.ImportLicence'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ImportLicence_PendingReport
        ON dbo.ImportLicence (Status, ApplicationDate, ApplicationNo)
        INCLUDE (ApplyType, CommodityType, PaThaKaId, ExportImportSectionId);
END
GO

-- 2) Per-page detail subqueries (Currency / Description / Amount / HSCode) read
--    ImportLicenceItem by ImportLicenceId. This covering index turns each
--    correlated subquery into a seek instead of a scan of ImportLicenceItem.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ImportLicenceItem_LicenceId_Cover'
               AND object_id = OBJECT_ID('dbo.ImportLicenceItem'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ImportLicenceItem_LicenceId_Cover
        ON dbo.ImportLicenceItem (ImportLicenceId)
        INCLUDE (CurrencyId, Description, Amount, HSCode);
END
GO

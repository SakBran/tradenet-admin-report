/* =====================================================================================
   Amend / Actual Amendment parity deployment - 2026-09-04
   Run this BEFORE 00_RunAll.sql and save the result grid. It is the rollback artifact:
   each row is the full CREATE text currently on the server. To roll back, replace the
   leading CREATE with CREATE OR ALTER and execute the saved text.

   In SSMS: Results to Text, and set Tools > Options > Query Results > SQL Server >
   Results to Text > Maximum characters per column to 8192 first, or the definitions
   are truncated and the rollback artifact is useless.
   ===================================================================================== */

USE [TradeNetDB];
GO

SELECT p.name, p.modify_date, OBJECT_DEFINITION(p.object_id) AS definition
FROM sys.procedures p
WHERE p.name IN (
    'sp_ActualAmendReport_pagination', 'sp_AmendReport_pagination',
    'sp_ExportLicenceListingCurrencyTotals', 'sp_ExportPermitListingCurrencyTotals',
    'sp_ImportLicenceListingCurrencyTotals', 'sp_ImportPermitListingCurrencyTotals')
ORDER BY p.name;
GO

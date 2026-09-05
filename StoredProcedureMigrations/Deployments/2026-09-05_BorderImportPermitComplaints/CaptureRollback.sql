/* =====================================================================================
   Border Import Permit customer-complaint deployment - 2026-09-05
   Run this BEFORE 00_RunAll.sql and save the result grid. It is the rollback artifact:
   each row is the full CREATE text currently on the server. To roll back, replace the
   leading CREATE with CREATE OR ALTER and execute the saved text.

   In SSMS: Results to Text, and set Tools > Options > Query Results > SQL Server >
   Results to Text > Maximum characters per column to 8192 first, or the definitions are
   truncated and the rollback artifact is useless. sp_HSCodeReport_pagination and
   sp_NewReport_pagination are both long - check the captured text really ends with the
   procedure's last END.
   ===================================================================================== */

USE [TradeNetDB];
GO

SELECT p.name, p.modify_date, OBJECT_DEFINITION(p.object_id) AS definition
FROM sys.procedures p
WHERE p.name IN (
    'sp_NewReport_pagination',
    'sp_HSCodeReport_pagination',
    'sp_ImportPermitListingCurrencyTotals',
    'sp_ExportPermitListingCurrencyTotals')
ORDER BY p.name;
GO

/* -------------------------------------------------------------------------------------
   Blast radius, so a rollback decision can be made with the facts:
     * sp_NewReport_pagination and sp_HSCodeReport_pagination each serve all EIGHT
       FormType families (Import/Export Permit, Import/Export Licence and the four Border
       variants). The @FetchSize change in particular alters paging for every one of them.
     * sp_ImportPermitListingCurrencyTotals is also called by the Import Permit and Border
       Import Permit Amendment / Actual Amendment / Cancellation footers.
   ------------------------------------------------------------------------------------- */

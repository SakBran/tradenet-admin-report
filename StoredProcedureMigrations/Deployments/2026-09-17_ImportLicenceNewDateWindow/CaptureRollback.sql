/* =====================================================================================
   Import Licence New date-window deployment - 2026-09-17

   Run this BEFORE 00_RunAll.sql and save the result grid. It is the rollback artifact:
   each row is the full CREATE text currently on the server. To roll back, replace the
   leading CREATE with CREATE OR ALTER and execute the saved text.

   In SSMS: Results to Text, and set Tools > Options > Query Results > SQL Server >
   Results to Text > Maximum characters per column to 8192 first, or the definition is
   truncated and the rollback artifact is useless. Check that each captured text really
   ends with its last END.
   ===================================================================================== */

USE [TradeNetDB];
GO

SELECT p.name, p.modify_date, OBJECT_DEFINITION(p.object_id) AS definition
FROM sys.procedures p
WHERE p.name IN ('sp_NewReport_pagination', 'sp_ImportLicenceListingCurrencyTotals')
ORDER BY p.name;
GO

/* -------------------------------------------------------------------------------------
   ALSO CAPTURE THIS. Procedures in this repository are applied by hand, so the definition
   on the server can be older than the .sql file. Before trusting any before/after
   comparison, confirm the deployed procedures really are the version this release
   targets - i.e. that they still carry the date-skip being removed. If the "carries the
   date-skip" column below already reads NO on both rows, the server never received the
   2026-06-19 patch, this release is a no-op there, and the 1386-row symptom will not
   reproduce - stop and find out which server the customer is actually on.

   Note sp_NewReport_pagination is shared by 8 report families (Import/Export Permit,
   Import/Export Licence, and the four Border variants). Only its final ELSE branch -
   the one reached by @FormType = 'Import Licence' - changes in this release. The
   capture above is of the whole procedure, which is what a rollback needs.
   ------------------------------------------------------------------------------------- */
SELECT
    p.name,
    p.modify_date,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%@CompanyRegistrationNo<>%CreatedDate>=@FromDate%'
           OR OBJECT_DEFINITION(p.object_id) LIKE '%@CompanyRegistrationNo <> %CreatedDate >= @FromDate%'
         THEN 'YES - pre-release version, this deployment will change it'
         ELSE 'NO  - already without the date-skip (or never had it)'
    END AS carries_the_date_skip
FROM sys.procedures p
WHERE p.name IN ('sp_NewReport_pagination', 'sp_ImportLicenceListingCurrencyTotals')
ORDER BY p.name;
GO

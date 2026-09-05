/* =====================================================================================
   Export Permit round-3 parity deployment - 2026-09-05
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
WHERE p.name IN ('sp_CancelReport_pagination', 'sp_VoucherReport_pagination')
ORDER BY p.name;
GO

/* -------------------------------------------------------------------------------------
   ALSO CAPTURE THIS, and send it to whoever is working the Export Permit Cancellation
   complaint. dbo.sp_CancelReport is the LEGACY Tradenet 2.0 procedure that the old admin
   app still calls. Production's copy has moved on from the snapshot this repository was
   built from (production's CancelReport.rdlc and Business/Reports.cs both read an HSCode
   column that our snapshot's Export Permit branch does not return), so the new report's
   "Total Value" cannot be proved equal to the old one's until this text is compared.
   ------------------------------------------------------------------------------------- */
SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_CancelReport')) AS legacy_sp_CancelReport;
GO

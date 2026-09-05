/* =====================================================================================
   Export Licence Detail parity deployment - 2026-09-05
   Run this BEFORE 00_RunAll.sql and save the result grid.

   This release only CREATES dbo.sp_ExportLicenceDetailReportV3_pagination, so on a first
   deployment the first query returns no row and the rollback is simply
       DROP PROCEDURE dbo.sp_ExportLicenceDetailReportV3_pagination;
   (the application then falls back to its slower LINQ page on its own). If the procedure
   already exists, the row is its current CREATE text: to roll back, replace the leading
   CREATE with CREATE OR ALTER and execute the saved text.

   In SSMS: Results to Text, and set Tools > Options > Query Results > SQL Server >
   Results to Text > Maximum characters per column to 8192 first, or the definitions
   are truncated and the rollback artifact is useless.
   ===================================================================================== */

USE [TradeNetDB];
GO

SELECT p.name, p.modify_date, OBJECT_DEFINITION(p.object_id) AS definition
FROM sys.procedures p
WHERE p.name = N'sp_ExportLicenceDetailReportV3_pagination';
GO

/* The legacy procedure the old Tradenet 2.0 admin still calls is the oracle for section 3
   of VerifyDeployment.sql. Capture its text too: the repository's copy
   (docs/StoredProcedureDefinitions.sql) is a snapshot, and the parity diff has to be read
   against THIS text if production's copy has moved on. */
SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_ExportLicenceDetailReport')) AS legacy_sp_ExportLicenceDetailReport;
GO

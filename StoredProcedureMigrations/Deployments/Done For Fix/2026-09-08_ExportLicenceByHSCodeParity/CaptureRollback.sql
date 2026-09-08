/* =====================================================================================
   Export Licence / Border Export Licence By HS Code parity deployment - 2026-09-08
   Run this BEFORE 00_RunAll.sql and save the result grid. It is the rollback artifact:
   the row is the full CREATE text currently on the server. To roll back, replace the
   leading CREATE with CREATE OR ALTER and execute the saved text.

   In SSMS: Results to Text, and set Tools > Options > Query Results > SQL Server >
   Results to Text > Maximum characters per column to 8192 first, or the definition is
   truncated and the rollback artifact is useless. sp_HSCodeReport_pagination is a long
   one (~950 lines) - check the tail of the captured text really ends with its last END.
   ===================================================================================== */

USE [TradeNetDB];
GO

SELECT p.name, p.modify_date, OBJECT_DEFINITION(p.object_id) AS definition
FROM sys.procedures p
WHERE p.name = 'sp_HSCodeReport_pagination';
GO

/* -------------------------------------------------------------------------------------
   ALSO CAPTURE THIS. dbo.sp_HSCodeReport is the LEGACY Tradenet 2.0 procedure the old
   admin app still calls, and it is the oracle for the row counts this deployment targets
   (304 / 33 for 31/08-01/09/2026). The old app does no grouping in SQL at all
   (Business/Reports.cs:1180 GetHSCodeReport just projects the rows) - the grouping happens
   in HSCodeReport.rdlc / BorderHSCodeReport.rdlc row groups.

   WARNING: docs/sp_HSCodeReport_AggregatePagination.sql in this repository is an
   ALTER PROCEDURE [dbo].[sp_HSCodeReport] that adds a company GROUP BY of its own. If
   that script was ever run against this database, the OLD report is showing split rows
   too and the old-vs-new comparison is meaningless until the original definition is
   restored. The capture below settles it: if the text contains a GROUP BY, it has been
   overwritten.
   ------------------------------------------------------------------------------------- */
SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_HSCodeReport')) AS legacy_sp_HSCodeReport;
GO

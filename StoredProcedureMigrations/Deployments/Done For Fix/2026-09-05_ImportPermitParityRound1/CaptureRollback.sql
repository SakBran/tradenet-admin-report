/* =====================================================================================
   Import Permit round-1 parity deployment - 2026-09-05
   Run this BEFORE 00_RunAll.sql and save the result grid. It is the rollback artifact:
   each row is the full CREATE text currently on the server. To roll back, replace the
   leading CREATE with CREATE OR ALTER and execute the saved text.

   In SSMS: Results to Text, and set Tools > Options > Query Results > SQL Server >
   Results to Text > Maximum characters per column to 8192 first, or the definitions
   are truncated and the rollback artifact is useless. sp_HSCodeReport_pagination is a
   long one - check the tail of the captured text really ends with its last END.
   ===================================================================================== */

USE [TradeNetDB];
GO

SELECT p.name, p.modify_date, OBJECT_DEFINITION(p.object_id) AS definition
FROM sys.procedures p
WHERE p.name IN ('sp_HSCodeReport_pagination', 'sp_ImportPermitListingCurrencyTotals')
ORDER BY p.name;
GO

/* -------------------------------------------------------------------------------------
   ALSO CAPTURE THIS. dbo.sp_HSCodeReport is the LEGACY Tradenet 2.0 procedure the old
   admin app still calls, and it is the oracle for the "one HS code, one row" claim: the
   old app does no grouping in SQL at all (Business/Reports.cs:930 just projects the rows),
   the grouping happens in HSCodeReport.rdlc's row group. Capture the text so the old
   report's row set can be reproduced and compared.

   WARNING: docs/sp_HSCodeReport_AggregatePagination.sql in this repository is an
   ALTER PROCEDURE [dbo].[sp_HSCodeReport] that applies the SAME company GROUP BY this
   deployment removes. If that script was ever run against this database, the old system's
   HS Code report is already showing the split rows too, and the old-vs-new comparison is
   meaningless until the original definition is restored. The capture below settles it: if
   the text contains a GROUP BY, it has been overwritten.
   ------------------------------------------------------------------------------------- */
SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_HSCodeReport')) AS legacy_sp_HSCodeReport;
GO

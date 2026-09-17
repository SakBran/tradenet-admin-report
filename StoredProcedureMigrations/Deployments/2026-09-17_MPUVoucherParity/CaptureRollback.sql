/* =====================================================================================
   MPU Voucher No parity deployment - 2026-09-17

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
WHERE p.name IN ('sp_MPUReport_V3_pagination', 'sp_MPUReport_pagination')
ORDER BY p.name;
GO

/* -------------------------------------------------------------------------------------
   ALSO CAPTURE THIS. Procedures in this repository are applied by hand, so the definition
   on the server can be older than the .sql file. Before trusting any before/after
   comparison, confirm the deployed sp_MPUReport_V3_pagination really is the version this
   release targets: its MPU-side ROW_NUMBER() should sit in a SELECT ... INTO #mpu whose
   WHERE clause carries the date window. If it does NOT - if the filters are in an outer
   WHERE instead - the server is still running something closer to the legacy Tradenet 2.0
   shape and the "voucher moves with the date range" symptom will not reproduce.
   ------------------------------------------------------------------------------------- */
SELECT
    CASE WHEN OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_MPUReport_V3_pagination')) LIKE '%INTO #mpuAll%'
         THEN 'already at the 2026-09-17 shape'
         ELSE 'pre-2026-09-17 (expected before this deployment)'
    END AS deployed_shape;
GO

/* -------------------------------------------------------------------------------------
   The legacy Tradenet 2.0 procedure the old admin app still calls, for reference: it is
   the oracle for what the old screen showed.
   ------------------------------------------------------------------------------------- */
SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_MPUReport_V3')) AS legacy_sp_MPUReport_V3;
GO

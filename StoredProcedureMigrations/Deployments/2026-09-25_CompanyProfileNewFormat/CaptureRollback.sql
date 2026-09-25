/* =====================================================================================
   Company Profile "11 ministries" layout deployment - 2026-09-25
   Run this BEFORE 00_RunAll.sql and save the result grid. It is the rollback artifact:
   the row is the full CREATE text currently on the server. To roll back, replace the
   leading CREATE with CREATE OR ALTER and execute the saved text - and roll the
   application back with it, since the new application needs the new columns.

   In SSMS: Results to Text, and set Tools > Options > Query Results > SQL Server >
   Results to Text > Maximum characters per column to 8192 first, or the definition
   is truncated and the rollback artifact is useless.
   ===================================================================================== */

USE [TradeNetDB];
GO

SELECT p.name, p.modify_date, OBJECT_DEFINITION(p.object_id) AS definition
FROM sys.procedures p
WHERE p.name = 'sp_CompanyProfileReport_pagination';
GO

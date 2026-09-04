/* =====================================================================================
   Amend / Actual Amendment parity deployment - 2026-09-04
   Verification. Run section 1 BEFORE deploying (it records what is on the server today)
   and again AFTER; run sections 2 and 3 after.
   ===================================================================================== */

USE [TradeNetDB];
GO

-- -------------------------------------------------------------------------------------
-- 1. What is deployed right now
--    Expected params AFTER deployment, in the order listed: 12, 12, 8, 8, 10, 8.
--    date_pred must read 'ok' and border_il must read 'has-BorderIL' for the two grid
--    procedures; 'DATEADD-raw' or 'NO-BorderIL (stale)' means the server still carries an
--    older definition, which is exactly what the customer complaints describe.
-- -------------------------------------------------------------------------------------
SELECT
    p.name,
    p.modify_date,
    m.uses_quoted_identifier,
    (SELECT COUNT(*) FROM sys.parameters pa WHERE pa.object_id = p.object_id) AS params,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%DATEADD(day, 1, @ToDate)%'
         THEN 'DATEADD-raw (extra day)' ELSE 'ok' END AS date_pred,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%Border Import Licence%'
         THEN 'has-BorderIL' ELSE 'NO-BorderIL (stale)' END AS border_il
FROM sys.procedures p
    JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name IN (
    'sp_ActualAmendReport_pagination', 'sp_AmendReport_pagination',
    'sp_ExportLicenceListingCurrencyTotals', 'sp_ExportPermitListingCurrencyTotals',
    'sp_ImportLicenceListingCurrencyTotals', 'sp_ImportPermitListingCurrencyTotals')
ORDER BY p.name;
GO

-- -------------------------------------------------------------------------------------
-- 2. UAT parity, November 2025. The three numbers must agree.
--    The ORIGINAL Tradenet 2.0 procedure is the oracle; the new one must equal it.
-- -------------------------------------------------------------------------------------
DECLARE @f datetime = '2025-11-01 00:00:00', @t datetime = '2025-11-30 23:59:59';

EXEC dbo.sp_ActualAmendReport            N'Border Export Licence', @f, @t, 0, 0, N'', 0;                       -- old: 20 rows
EXEC dbo.sp_ActualAmendReport_pagination N'Border Export Licence', @f, @t, 0, 0, N'', 0, NULL, NULL, 0, 0, 1;  -- new: 20 rows, TotalCount 20
EXEC dbo.sp_ExportLicenceListingCurrencyTotals N'Border Export Licence', N'Actual Amend', @f, @t, 0, N'', 0, 0; -- THB 6, USD 6, CNY 8 = 20

EXEC dbo.sp_ActualAmendReport            N'Border Import Licence', @f, @t, 0, 0, N'', 0;                       -- old: 53 rows
EXEC dbo.sp_ActualAmendReport_pagination N'Border Import Licence', @f, @t, 0, 0, N'', 0, NULL, NULL, 0, 0, 1;  -- new: 53 rows
-- Border Import Licence footer, via the new 10-argument shape (@FormType, @SakhanId trailing):
EXEC dbo.sp_ImportLicenceListingCurrencyTotals N'ActualAmend', @f, @t, 0, N'', 0, N'', N'', N'Border Import Licence', 0;
GO

-- -------------------------------------------------------------------------------------
-- 3. PRODUCTION - the customer's own windows. These are the numbers in the complaint.
-- -------------------------------------------------------------------------------------
DECLARE @f datetime = '2026-08-01 00:00:00', @t datetime = '2026-09-01 23:59:59';

-- Border Export Licence Actual Amend, Sakhan MWD (SakhanId 5): old 10, new must be 10 (was 11).
EXEC dbo.sp_ActualAmendReport            N'Border Export Licence', @f, @t, 0, 0, N'', 5;
EXEC dbo.sp_ActualAmendReport_pagination N'Border Export Licence', @f, @t, 0, 0, N'', 5, NULL, NULL, 0, 0, 1;
EXEC dbo.sp_ExportLicenceListingCurrencyTotals N'Border Export Licence', N'Actual Amend', @f, @t, 0, N'', 0, 5; -- THB 7 + USD 3 = 10

-- Border Import Licence Actual Amend: old 17, new must equal it (was 6 - stale procedure).
EXEC dbo.sp_ActualAmendReport            N'Border Import Licence', @f, @t, 0, 0, N'', 0;
EXEC dbo.sp_ActualAmendReport_pagination N'Border Import Licence', @f, @t, 0, 0, N'', 0, NULL, NULL, 0, 0, 1;
EXEC dbo.sp_ImportLicenceListingCurrencyTotals N'ActualAmend', @f, @t, 0, N'', 0, N'', N'', N'Border Import Licence', 0;
GO

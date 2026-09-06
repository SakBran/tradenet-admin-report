/* =====================================================================================
   Extension reports - "Licence No" column parity - 2026-09-05
   VERIFICATION ONLY. Read-only SELECTs; nothing here changes data or procedures.

   NOTHING TO DEPLOY: the fix was frontend-only (Frontend/src/Report/config/reportConfigs.ts).
   Every branch of sp_ExtensionReport_pagination already projects OldLicenceNo, so no
   procedure changed and no procedure needs to be re-applied.

   WHY RUN THIS
   The Extension reports' "Licence No" column used to be bound to the extension's own
   E-number, so it printed the same value as "Extension No" (customer complaint). It is now
   bound to the PARENT number (OldLicenceNo), matching ExtensionReport.rdlc:303/868.
   If the parent column is empty in the database, the cell will now render BLANK instead of
   wrong - which is what the old Tradenet 2.0 report did too, but it is worth knowing before
   the customer sees it. Section 1 answers that in one row per report.

   HOW TO READ THE RESULT
   BlankParentPct near 0   -> ship it, the column will be populated.
   BlankParentPct high     -> stop and raise it with the customer before shipping.

   Run whole-file (F5). Safe to run on PROD.
   ===================================================================================== */

USE [TradeNetDB];
GO

-- -------------------------------------------------------------------------------------
-- 1. Is the parent licence/permit number actually populated on Extension rows?
--    One row per fixed report. Predicates mirror sp_ExtensionReport_pagination
--    (ApplyType='Extension' AND Status='Approved').
-- -------------------------------------------------------------------------------------
SELECT
    r.ReportName,
    r.TotalExtensionRows,
    r.BlankParentRows,
    CAST(CASE WHEN r.TotalExtensionRows = 0 THEN 0
              ELSE 100.0 * r.BlankParentRows / r.TotalExtensionRows END AS decimal(5, 1))
        AS BlankParentPct,
    CASE WHEN r.TotalExtensionRows = 0 THEN 'NO DATA - widen the check'
         WHEN r.BlankParentRows = 0    THEN 'OK'
         ELSE 'REVIEW - some rows will show a blank Licence No' END AS Verdict
FROM (
    SELECT 'ExportLicenceExtensionReport' AS ReportName,
           COUNT(*) AS TotalExtensionRows,
           SUM(CASE WHEN NULLIF(LTRIM(RTRIM(OldExportLicenceNo)), '') IS NULL THEN 1 ELSE 0 END)
               AS BlankParentRows
    FROM ExportLicence
    WHERE ApplyType = 'Extension' AND Status = 'Approved'

    UNION ALL
    SELECT 'ExportPermitExtensionReport',
           COUNT(*),
           SUM(CASE WHEN NULLIF(LTRIM(RTRIM(OldExportPermitNo)), '') IS NULL THEN 1 ELSE 0 END)
    FROM ExportPermit
    WHERE ApplyType = 'Extension' AND Status = 'Approved'

    UNION ALL
    SELECT 'BorderImportLicenceExtensionReport',
           COUNT(*),
           SUM(CASE WHEN NULLIF(LTRIM(RTRIM(OldImportLicenceNo)), '') IS NULL THEN 1 ELSE 0 END)
    FROM BorderImportLicence
    WHERE ApplyType = 'Extension' AND Status = 'Approved'

    UNION ALL
    SELECT 'BorderImportPermitExtensionReport',
           COUNT(*),
           SUM(CASE WHEN NULLIF(LTRIM(RTRIM(OldImportPermitNo)), '') IS NULL THEN 1 ELSE 0 END)
    FROM BorderImportPermit
    WHERE ApplyType = 'Extension' AND Status = 'Approved'
) r
ORDER BY r.ReportName;
GO

-- -------------------------------------------------------------------------------------
-- 2. Eyeball the two columns side by side.
--    ParentLicenceNo is what the "Licence No" column now shows; ExtensionNo is unchanged.
--    They must DIFFER on every row - identical values are the bug this fix removed.
-- -------------------------------------------------------------------------------------
SELECT TOP 10 'ExportLicence' AS Source,
       OldExportLicenceNo AS ParentLicenceNo, ExportLicenceNo AS ExtensionNo, CreatedDate
FROM ExportLicence
WHERE ApplyType = 'Extension' AND Status = 'Approved'
ORDER BY CreatedDate DESC;

SELECT TOP 10 'ExportPermit' AS Source,
       OldExportPermitNo AS ParentLicenceNo, ExportPermitNo AS ExtensionNo, CreatedDate
FROM ExportPermit
WHERE ApplyType = 'Extension' AND Status = 'Approved'
ORDER BY CreatedDate DESC;

SELECT TOP 10 'BorderImportLicence' AS Source,
       OldImportLicenceNo AS ParentLicenceNo, ImportLicenceNo AS ExtensionNo, CreatedDate
FROM BorderImportLicence
WHERE ApplyType = 'Extension' AND Status = 'Approved'
ORDER BY CreatedDate DESC;

SELECT TOP 10 'BorderImportPermit' AS Source,
       OldImportPermitNo AS ParentLicenceNo, ImportPermitNo AS ExtensionNo, CreatedDate
FROM BorderImportPermit
WHERE ApplyType = 'Extension' AND Status = 'Approved'
ORDER BY CreatedDate DESC;
GO

-- -------------------------------------------------------------------------------------
-- 3. Same check inside the date window the customer will actually search.
--    Report data in this database lives in 2025 - a 2026 window comes back empty and
--    looks exactly like a broken report. Edit the two dates and re-run.
-- -------------------------------------------------------------------------------------
DECLARE @FromDate datetime = '2025-01-01';
DECLARE @ToDate   datetime = '2025-12-31';

SELECT
    COUNT(*) AS RowsInWindow,
    SUM(CASE WHEN NULLIF(LTRIM(RTRIM(OldExportLicenceNo)), '') IS NULL THEN 1 ELSE 0 END)
        AS BlankParentRows
FROM ExportLicence
WHERE ApplyType = 'Extension' AND Status = 'Approved'
  AND CreatedDate >= @FromDate
  AND CreatedDate <  DATEADD(day, 1, CONVERT(date, @ToDate));
GO

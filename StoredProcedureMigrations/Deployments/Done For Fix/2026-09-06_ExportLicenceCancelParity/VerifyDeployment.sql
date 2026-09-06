/* =====================================================================================
   Export Licence Cancellation parity deployment - 2026-09-06
   Run section 1 BEFORE deploying (it records what is on the server today) and again
   AFTER; run sections 2 and 3 after. Section 3 is the real test: it compares the new
   procedure against the LEGACY one the old Tradenet 2.0 app still calls.
   ===================================================================================== */

USE [TradeNetDB];
GO

-- -------------------------------------------------------------------------------------
-- 1. What is deployed right now.
--    AFTER deployment both rows must read item_key = 'Id,UniqueId'. 'Id (stale)' or
--    'unordered (stale)' means the server still carries an older definition -- and an
--    unordered TOP 1 is what makes the grid's Total Value disagree with its footer.
-- -------------------------------------------------------------------------------------
SELECT
    p.name,
    p.modify_date,
    m.uses_quoted_identifier,
    CASE
        WHEN OBJECT_DEFINITION(p.object_id) LIKE '%ORDER BY ExportLicenceItem.Id, ExportLicenceItem.UniqueId%'
            THEN 'Id,UniqueId'
        WHEN OBJECT_DEFINITION(p.object_id) LIKE '%ORDER BY ExportLicenceItem.Id%'
            THEN 'Id (stale)'
        ELSE 'unordered (stale)'
    END AS item_key
FROM sys.procedures p
    JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name IN ('sp_CancelReport_pagination', 'sp_ExportLicenceListingCurrencyTotals')
ORDER BY p.name;
GO

-- -------------------------------------------------------------------------------------
-- 2. The report as the customer runs it: Export Licence Cancellation over a month with
--    data. The grid's TotalCount and the footer's summed licence count MUST be equal --
--    if they are not, the footer is not the sum of the rows on screen and something in
--    the two WHERE clauses has drifted apart.
--    Also eyeball that HSCode is populated: it is the column added in this pass.
-- -------------------------------------------------------------------------------------
EXEC dbo.sp_CancelReport_pagination
    @FormType = N'Export Licence',
    @FromDate = '2025-08-01 00:00:00', @ToDate = '2025-08-31 23:59:59',
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0,
    @SortColumn = NULL, @SortOrder = NULL, @PageIndex = 0, @PageSize = 1000, @IncludeTotalCount = 1;
GO

EXEC dbo.sp_ExportLicenceListingCurrencyTotals
    @FormType = N'Export Licence', @ApplyType = N'Cancel',
    @FromDate = '2025-08-01 00:00:00', @ToDate = '2025-08-31 23:59:59',
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @AmendRemarkId = 0, @SakhanId = 0;
GO

-- -------------------------------------------------------------------------------------
-- 3. Parity against the legacy procedure, over all of 2025. Both must return the same
--    LicenceNo -> Amount / Currency / HSCode. Run them side by side and diff.
--    The prior measurement for ExportLicenceItem was Cancel 466/466 over Aug-2025;
--    record the score you get here in README.md before signing this release off.
--
--    If the legacy side disagrees, capture OBJECT_DEFINITION(OBJECT_ID('dbo.sp_CancelReport'))
--    first (CaptureRollback.sql does this): production's copy has drifted from the 2022
--    snapshot this repository was built from before, and the diff has to be read against
--    the server's actual text.
-- -------------------------------------------------------------------------------------
DECLARE @From datetime = '2025-01-01 00:00:00', @To datetime = '2025-12-31 23:59:59';

EXEC dbo.sp_CancelReport @FormType = N'Export Licence', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0;
EXEC dbo.sp_CancelReport_pagination @FormType = N'Export Licence', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0,
    @SortColumn = NULL, @SortOrder = NULL, @PageIndex = 0, @PageSize = 5000, @IncludeTotalCount = 1;
GO

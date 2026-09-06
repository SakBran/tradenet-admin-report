/* =====================================================================================
   Export Permit item-order parity deployment - 2026-09-06
   Run section 1 BEFORE deploying (it records what is on the server today) and again AFTER;
   run sections 2 and 3 after. Section 3 is the real test: it compares the new procedures
   against the LEGACY ones the old Tradenet 2.0 app still calls.
   ===================================================================================== */

USE [TradeNetDB];
GO

-- -------------------------------------------------------------------------------------
-- 1. What is deployed right now.
--    AFTER deployment every row's item_key must read 'HSCodeId,ItemNo'. 'Id (stale)' or
--    'unordered (stale)' means the server still carries an older definition, which is
--    exactly what the customer complaints describe.
-- -------------------------------------------------------------------------------------
SELECT
    p.name,
    p.modify_date,
    m.uses_quoted_identifier,
    CASE
        WHEN OBJECT_DEFINITION(p.object_id) LIKE '%ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo%'
            THEN 'HSCodeId,ItemNo'
        WHEN OBJECT_DEFINITION(p.object_id) LIKE '%ORDER BY ExportPermitItem.Id%'
            THEN 'Id (stale)'
        ELSE 'unordered (stale)'
    END AS item_key
FROM sys.procedures p
    JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name IN (
    'sp_CancelReport_pagination', 'sp_AmendReport_pagination', 'sp_ActualAmendReport_pagination',
    'sp_NewReport_pagination', 'sp_ExtensionReport_pagination',
    'sp_ExportPermitListingCurrencyTotals', 'sp_ExtensionReportCurrencyTotals')
ORDER BY p.name;
GO

-- -------------------------------------------------------------------------------------
-- 2. The customer's exact case: Export Permit Cancellation, 01/09/2025 - 30/09/2025.
--    Expect 4 rows with OVSEP12526C000012 = 27230.7600 (NOT 5769.2300), and the footer
--    USD / 4 / 33835.1200 (NOT 10038.1050).
-- -------------------------------------------------------------------------------------
EXEC dbo.sp_CancelReport_pagination
    @FormType = N'Export Permit',
    @FromDate = '2025-09-01 00:00:00', @ToDate = '2025-09-30 23:59:59',
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0,
    @SortColumn = NULL, @SortOrder = NULL, @PageIndex = 0, @PageSize = 100, @IncludeTotalCount = 1;
GO

EXEC dbo.sp_ExportPermitListingCurrencyTotals
    @FormType = N'Export Permit', @ApplyType = N'Cancel',
    @FromDate = '2025-09-01 00:00:00', @ToDate = '2025-09-30 23:59:59',
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @AmendRemarkId = 0, @SakhanId = 0;
GO

-- -------------------------------------------------------------------------------------
-- 3. Parity against the legacy procedures, over all of 2025. Each pair must return the
--    same LicenceNo -> Amount / Currency. Run them side by side and diff.
--    Measured on 2026-09-06: Cancel 17/17, Amend 3/3, Actual Amend 1/1, Extension 29/29,
--    New 1147/1147 -- identical on both columns.
-- -------------------------------------------------------------------------------------
DECLARE @From datetime = '2025-01-01 00:00:00', @To datetime = '2025-12-31 23:59:59';

EXEC dbo.sp_CancelReport @FormType = N'Export Permit', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0;
EXEC dbo.sp_CancelReport_pagination @FormType = N'Export Permit', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0,
    @SortColumn = NULL, @SortOrder = NULL, @PageIndex = 0, @PageSize = 1000, @IncludeTotalCount = 1;

EXEC dbo.sp_AmendReport @FormType = N'Export Permit', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @AmendRemarkId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0;
EXEC dbo.sp_AmendReport_pagination @FormType = N'Export Permit', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @AmendRemarkId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0,
    @SortColumn = NULL, @SortOrder = NULL, @PageIndex = 0, @PageSize = 1000, @IncludeTotalCount = 1;

EXEC dbo.sp_ActualAmendReport @FormType = N'Export Permit', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @AmendRemarkId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0;
EXEC dbo.sp_ActualAmendReport_pagination @FormType = N'Export Permit', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @AmendRemarkId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0,
    @SortColumn = NULL, @SortOrder = NULL, @PageIndex = 0, @PageSize = 1000, @IncludeTotalCount = 1;

EXEC dbo.sp_ExtensionReport @FormType = N'Export Permit', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0;
EXEC dbo.sp_ExtensionReport_pagination @FormType = N'Export Permit', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0,
    @SortColumn = NULL, @SortOrder = NULL, @PageIndex = 0, @PageSize = 1000, @IncludeTotalCount = 1;

-- NB: the legacy sp_NewReport takes an extra @auto parameter; its _pagination copy calls it @Auto.
EXEC dbo.sp_NewReport @FormType = N'Export Permit', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0, @auto = 0;
EXEC dbo.sp_NewReport_pagination @FormType = N'Export Permit', @FromDate = @From, @ToDate = @To,
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0, @Auto = 0,
    @SortColumn = NULL, @SortOrder = NULL, @PageIndex = 0, @PageSize = 2000, @IncludeTotalCount = 1;
GO

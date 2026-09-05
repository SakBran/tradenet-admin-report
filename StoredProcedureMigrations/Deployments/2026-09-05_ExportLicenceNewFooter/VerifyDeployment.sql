/* =====================================================================================
   Export Licence New Report currency-footer deployment - 2026-09-05
   Run section 1 before AND after deploying. Run sections 2-4 after.
   Section 3 is the real gate: the footer's grand total must EQUAL the grid's TotalCount
   for the same filters, including when the Auto / None-Auto dropdown is used.

   Use 2025 date windows - the report database's data lives in 2025, not 2026.
   ===================================================================================== */

USE [TradeNetDB];
GO

-- -------------------------------------------------------------------------------------
-- 1. What is deployed right now.
--    AFTER deployment: has_auto_param = 1 and new_branch_date_window = 'calendar date'.
--    Anything else means the server still carries the older definition.
-- -------------------------------------------------------------------------------------
SELECT
    p.name,
    p.modify_date,
    m.uses_quoted_identifier,
    (SELECT COUNT(*) FROM sys.parameters sp
        WHERE sp.object_id = p.object_id AND sp.name = '@auto') AS has_auto_param,
    CASE
        WHEN OBJECT_DEFINITION(p.object_id)
             LIKE '%ExportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))%'
            THEN 'calendar date'
        ELSE '<= @ToDate (stale)'
    END AS new_branch_date_window
FROM sys.procedures p
    JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name = 'sp_ExportLicenceListingCurrencyTotals';
GO

-- -------------------------------------------------------------------------------------
-- 2. The footer the Export Licence New Report will now show.
--    Expect one row per currency; TotalValue is the SUM of each licence's summed items,
--    matching the grid's "Total Value" column.
-- -------------------------------------------------------------------------------------
EXEC dbo.sp_ExportLicenceListingCurrencyTotals
    @FormType = N'Export Licence', @ApplyType = N'New',
    @FromDate = '2025-01-01 00:00:00', @ToDate = '2025-12-31 23:59:59',
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'',
    @AmendRemarkId = 0, @SakhanId = 0, @auto = N'';
GO

-- -------------------------------------------------------------------------------------
-- 3. THE GATE: footer count must equal grid count, unfiltered and per Auto value.
--    Run each pair and compare. Any mismatch means the footer and the grid disagree,
--    which is the class of bug this deployment exists to prevent.
-- -------------------------------------------------------------------------------------
-- 3a. unfiltered
EXEC dbo.sp_NewReport_pagination
    @FormType = N'Export Licence',
    @FromDate = '2025-01-01 00:00:00', @ToDate = '2025-12-31 23:59:59',
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0,
    @auto = N'', @SortColumn = NULL, @SortOrder = NULL,
    @PageIndex = 0, @PageSize = 1, @IncludeTotalCount = 1, @quota = N'';   -- read TotalCount
GO
-- 3b. @auto = 'auto'         (repeat 2 and 3a with the same value; the counts must track)
EXEC dbo.sp_ExportLicenceListingCurrencyTotals
    @FormType = N'Export Licence', @ApplyType = N'New',
    @FromDate = '2025-01-01 00:00:00', @ToDate = '2025-12-31 23:59:59',
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'',
    @AmendRemarkId = 0, @SakhanId = 0, @auto = N'auto';
GO
EXEC dbo.sp_NewReport_pagination
    @FormType = N'Export Licence',
    @FromDate = '2025-01-01 00:00:00', @ToDate = '2025-12-31 23:59:59',
    @ExportImportSectionId = 0, @CompanyRegistrationNo = N'', @SakhanId = 0,
    @auto = N'auto', @SortColumn = NULL, @SortOrder = NULL,
    @PageIndex = 0, @PageSize = 1, @IncludeTotalCount = 1, @quota = N'';
GO
-- 3c. repeat both with @auto = N'none-auto'.

-- -------------------------------------------------------------------------------------
-- 4. Backward compatibility: the Amendment / Actual Amendment / Cancellation callers
--    still pass only 8 arguments. These must keep returning their old numbers.
-- -------------------------------------------------------------------------------------
EXEC dbo.sp_ExportLicenceListingCurrencyTotals
    N'Export Licence', N'Amend', '2025-01-01 00:00:00', '2025-12-31 23:59:59', 0, N'', 0, 0;
GO
EXEC dbo.sp_ExportLicenceListingCurrencyTotals
    N'Border Export Licence', N'New', '2025-01-01 00:00:00', '2025-12-31 23:59:59', 0, N'', 0, 0;
GO

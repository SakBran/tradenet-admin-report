/* =====================================================================================
   Export Permit round-3 parity deployment - 2026-09-05
   Verification. Run section 1 BEFORE deploying (it records what is on the server today)
   and again AFTER; run sections 2 and 3 after.
   ===================================================================================== */

USE [TradeNetDB];
GO

-- -------------------------------------------------------------------------------------
-- 1. What is deployed right now
--    AFTER deployment: cancel_top1 must read 'ordered' and voucher_cif must read 'zero'.
--    'unordered' / 'null' means the server still carries the older definition, which is
--    exactly what the customer complaints describe.
-- -------------------------------------------------------------------------------------
SELECT
    p.name,
    p.modify_date,
    m.uses_quoted_identifier,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%ExportPermitId=pg.__k_Id ORDER BY ExportPermitItem.Id%'
         THEN 'ordered' ELSE 'unordered (stale)' END AS cancel_top1,
    CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%CAST(0 AS decimal(38,6)) ExchangeRate%'
         THEN 'zero' ELSE 'null (stale)' END AS voucher_cif
FROM sys.procedures p
    JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name IN ('sp_CancelReport_pagination', 'sp_VoucherReport_pagination')
ORDER BY p.name;
GO

-- -------------------------------------------------------------------------------------
-- 2. Export Permit Voucher: Total CIF / Exchange Rate must come back as 0, never NULL,
--    and Commodity Type / Application Date must be populated. Adjust the dates to a range
--    that has data (2025 is where the data lives).
-- -------------------------------------------------------------------------------------
EXEC dbo.sp_VoucherReport_pagination
    @FormType = N'Export Permit',
    @FromDate = '2025-01-01 00:00:00',
    @ToDate   = '2025-01-31 23:59:59',
    @ApplyType = N'New',
    @PageIndex = 0, @PageSize = 10, @IncludeTotalCount = 1;
GO

-- -------------------------------------------------------------------------------------
-- 3. Export Permit Cancellation: Currency / HSCode / Amount must now all come from the
--    SAME (lowest-Id) ExportPermitItem. Re-run the customer's date range and compare the
--    Total Value column against the old Tradenet 2.0 report row by row.
-- -------------------------------------------------------------------------------------
EXEC dbo.sp_CancelReport_pagination
    @FormType = N'Export Permit',
    @FromDate = '2025-01-01 00:00:00',
    @ToDate   = '2025-01-31 23:59:59',
    @PageIndex = 0, @PageSize = 25, @IncludeTotalCount = 1;
GO

-- Same window through the LEGACY procedure the old admin app calls. The Amount column of
-- the two result sets must agree licence-for-licence; any difference is the open item
-- described in CaptureRollback.sql.
EXEC dbo.sp_CancelReport
    @FormType = N'Export Permit',
    @FromDate = '2025-01-01 00:00:00',
    @ToDate   = '2025-01-31 23:59:59',
    @ExportImportSectionId = 0,
    @CompanyRegistrationNo = N'',
    @SakhanId = 0;
GO

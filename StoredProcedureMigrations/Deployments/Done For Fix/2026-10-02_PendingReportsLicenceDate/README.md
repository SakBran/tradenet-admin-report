# Pending reports - Licence Date - 2026-10-02

This package updates `dbo.sp_PendingReport_pagination` so these Pending report
menus return `Licence Date` from the application table's `IssuedDate` field:

- Import Licence Pending Report
- Border Import Licence Pending Report

The two Detail Report (Pending) menus already expose `Licence Date`; their query
paths do not require this procedure change. The site change keeps all four Pending
menus on the same visible field contract and updates both table and Excel output.

## Deployment order

Connect to `tn2db\\PRODUCTION / TradeNetDB` using **Windows Authentication**. Do
not use the application SQL login from `appsettings.json`.

1. Run `CaptureRollback.sql` and save the returned definition as
   `RollbackCaptured.sql`.
2. Run `01_sp_PendingReport_pagination.sql` against `TradeNetDB`.
3. Run `VerifyDeployment.sql`; both definition checks must return `1`.
4. Deploy the matching backend and frontend site build.
5. Verify both plain Pending reports and both Detail Report (Pending) reports in
   the browser and in Excel.

The procedure must be deployed before the site. The new backend maps a nullable
`LicenceDate` result column, so rows without an issued date remain blank. No table
data is inserted, updated, or deleted by this package.

## Deployment status

`dbo.sp_PendingReport_pagination` was deployed on 2026-10-02 to
`tn2db\\PRODUCTION / TradeNetDB` using Windows Authentication. The previous
definition is stored in `RollbackCaptured.sql`. Definition checks and execution
checks for both Import Licence and Border Import Licence passed. The matching site
has not been deployed; local runtime testing remains in progress.

## Rollback

Run the definition captured in step 1, then roll back the matching site deployment.
`01_sp_PendingReport_pagination.sql` is an exact copy of the canonical procedure
source in `StoredProcedureMigrations`.

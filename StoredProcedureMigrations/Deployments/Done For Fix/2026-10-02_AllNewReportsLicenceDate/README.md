# All application New Reports - Licence Date - 2026-10-02

This package updates `dbo.sp_NewReport_pagination` so the eight New Report
(New Report) pages return `Licence Date` from each application table's
`IssuedDate` field:

- Import Licence and Import Permit
- Export Licence and Export Permit
- Border Import Licence and Border Import Permit
- Border Export Licence and Border Export Permit

The matching site change displays only this added field. In particular, Import
Licence no longer displays `Online No`, `Online Date`, or `Remark` in the table
or Excel output. The stored procedure retains those result columns for backward
compatibility with the currently deployed result model.

`Licence Date` deliberately maps to `IssuedDate`, which is the issued/date-of-issue
value shown by Tradenet 2.0. It does not map to the similarly named `LicenceDate`
database field.

## Deployment

Connect to `tn2db.myanmartradenet.com,14133` / `TradeNetDB` using **Windows
Authentication**. Do not use the application SQL login from `appsettings.json`.

1. Run `CaptureRollback.sql` and save its result.
2. Run `00_RunAll.sql` against `TradeNetDB`.
3. Run `VerifyDeployment.sql`; all nine definition checks must return `1`.
4. Deploy the matching backend and frontend site build.
5. Open each of the eight New Report pages and confirm `Licence Date` appears
   immediately after `Licence No`; also verify Excel output.

The database procedure must be deployed before the site. Deploying the site alone
will make seven reports request a column that the old procedure returns as null.
No table data is inserted, updated, or deleted by this package.

## Rollback

Re-run the procedure definition captured in step 1, then roll back the matching
site deployment. `01_sp_NewReport_pagination.sql` is an exact copy of the canonical
procedure source in `StoredProcedureMigrations`.

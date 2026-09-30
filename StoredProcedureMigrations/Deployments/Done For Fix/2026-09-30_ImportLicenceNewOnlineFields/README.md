# Import Licence New Report online and licence columns — 2026-09-30

This package updates `dbo.sp_NewReport_pagination` so **Import Licence New Report (New Report)** returns four requested fields.

| Report column | Database source |
| --- | --- |
| Online No | `ImportLicence.ApplicationNo` |
| Online Date | `ImportLicence.ApplicationDate` |
| Licence Date | `ImportLicence.IssuedDate` |
| Remark | `ImportLicence.Remark` |

`Licence Date` deliberately uses `IssuedDate`. In Tradenet 2.0 this value is displayed as **Issued Date** in the licence detail and **Date of Issue** on the generated licence. It is not mapped from the similarly named `ImportLicence.LicenceDate` timestamp.

## Deployment

Connect to `tn2db.myanmartradenet.com,14133` / `TradeNetDB` using **Windows Authentication**. Confirm that the target instance is `tn2db\PRODUCTION`. Do not use the `tn2db` application SQL login from `Backend/appsettings.json`; it cannot view or alter stored-procedure definitions.

1. Run `CaptureRollback.sql` against the target database and save its result before changing the procedure.
2. Run `00_RunAll.sql` against `TradeNetDB`.
3. Run `VerifyDeployment.sql` and confirm that every definition check returns `1`, the raw sample values are sensible, and the stored-procedure sample returns the four columns.
4. Deploy the matching backend and frontend application changes. Application deployment does not install this stored procedure automatically.

## Rollback

Re-run the procedure definition captured in step 1. The change is additive to the result shape; it does not modify table data.

`01_sp_NewReport_pagination.sql` is an exact copy of the repository's deployable procedure source. Its SHA-256 is recorded in `checksums.txt`.

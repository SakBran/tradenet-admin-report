# Border Import Licence Detail and Pending Detail Parity/Procedure Audit

Date: 2026-06-10

## Scope

- `BorderImportLicenceDetailReport`
- `BorderImportLicenceDetailReportPending`

## Old Admin Sources Checked

- Old filter views:
  - `C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderImportLicenceDetailReport.cshtml`
  - `C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderImportLicencePendingDetailReport.cshtml`
- Old RDLC:
  - `C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\BorderImportLicenceDetailReport.rdlc`
- New UI config:
  - `Frontend/src/Report/config/reportConfigs.ts`
- New controllers:
  - `Backend/Controllers/Report/BorderImportLicenceDetailReportController.cs`
  - `Backend/Controllers/Report/BorderImportLicenceDetailReportPendingController.cs`
- Stored procedure migrations:
  - `StoredProcedureMigrations/sp_ImportLicenceDetailReport_pagination.sql`
  - `StoredProcedureMigrations/sp_ImportLicencePendingDetailReport_pagination.sql`

## Filter Parity

Old Border Import Licence Detail filter box:

- From Date
- To Date
- Sakhan
- PaThaKa Type
- Import Section
- Import Method
- Import Incoterms
- hidden `Type`

Old Border Import Licence Pending Detail filter box:

- From Date
- To Date
- Sakhan
- PaThaKa Type
- Import Section
- Import Method
- Import Incoterms
- hidden `Type`

New `BorderImportLicenceDetailReport` filter result:

- PASS: same visible filter set as old admin.
- PASS: section/method/incoterm use the Border Import Licence scoped lookups already added in config.
- Note: new UI label is `EIR Card Type` through the shared PaThaKa type config, while old admin resource is `PaThaKa Type`.

New `BorderImportLicenceDetailReportPending` filter result:

- FAIL: includes visible extra `Type` text filter. Old admin has hidden `Type`, not visible.
- FAIL: includes extra `Seller Country` filter. Old admin pending detail view does not show it.
- FAIL: includes extra `Company Registration No` filter. Old admin pending detail view does not show it.
- FAIL: filter controls are raw number/text entries, not lookup-backed controls, so options do not match the old dropdown behavior.
- PASS: includes From Date / To Date, Sakhan, PaThaKa Type, Import Section, Import Method, Import Incoterms.

## Column Parity

Old RDLC table headers:

`Sr.No.`, `Section`, `Application Date`, `Application No`, `Licence No`, `Create Date`, `Approve Date`, `Company Registration No`, `Company Name`, `Company Address`, `Seller Name`, `Seller Address`, `Seller Country`, `Place/Port of Discharge`, `Last Date`, `Method`, `Consigned Country`, `Country of Orign`, `HSCode`, `Decription`, `A/U`, `Price`, `Qty`, `Value`, `Currency`, `Commodity Type`, `Conditions`

New `BorderImportLicenceDetailReport` columns:

- PASS: same set of columns.
- PASS: same English language.
- PASS: `No` row-number column corresponds to old `Sr.No.`.

New `BorderImportLicenceDetailReportPending` columns:

- FAIL: missing `Create Date`.
- FAIL: missing `Approve Date`.
- FAIL: extra/renamed `Licence Date`.
- PASS: remaining columns match the old RDLC set and English language.

## Stored Procedure Check

Live DB procedure metadata checked in `TradeNetDB`:

- Existing:
  - `dbo.sp_ImportLicenceDetailReport`
  - `dbo.sp_ImportLicenceDetailReport_pagination`
  - `dbo.sp_ImportLicencePendingDetailReport`
  - `dbo.sp_ImportLicencePendingDetailReport_pagination`
- Missing:
  - `dbo.sp_BorderImportLicenceDetailReport_pagination`
  - `dbo.sp_BorderImportLicencePendingDetailReport_pagination`

The shared Import Licence pagination procedures are not valid Border Import Licence procedures:

- `sp_ImportLicenceDetailReport_pagination` selects from `ImportLicence` and `ImportLicenceItem`.
- `sp_ImportLicencePendingDetailReport_pagination` selects from `ImportLicence` and `ImportLicenceItem`.
- Both accept `@Type`, but the checked definitions do not branch to `BorderImportLicence` / `BorderImportLicenceItem`.

Current Border controllers avoid those shared procedures by using LINQ fast paths:

- `sp_ImportLicenceDetailReport_Fast`
- `sp_ImportLicencePendingDetailReport_Fast`

Those LINQ paths do branch to `BorderImportLicence` / `BorderImportLicenceItem` when `Type = "Border"`, but there is no real Border-specific SQL stored procedure to deploy.

## Live Data Evidence

Approved Border Import Licence sample:

- Date: `2026-05-21`
- Licence: `MWDBIL32627000002`
- Status: `Approved`
- CardType: `Pa Tha Ka`
- Item rows: `2`

Pending Border Import Licence sample:

- Date: `2026-05-19`
- Licences include `test 3` and `test 4`
- Status: `Pending`
- CardType: `Pa Tha Ka`
- Item rows: `1` each

## Required Changes

- Fix `BorderImportLicenceDetailReportPending` filters to match the old admin filter box and use scoped lookup-backed controls.
- Fix `BorderImportLicenceDetailReportPending` columns to match the old RDLC: replace `Licence Date` with `Create Date` and add `Approve Date`.
- Create Border-specific stored procedures:
  - `dbo.sp_BorderImportLicenceDetailReport_pagination`
  - `dbo.sp_BorderImportLicencePendingDetailReport_pagination`
- Wire the two Border detail controllers to the Border-specific SQL procedures.

## Implementation Update

- Fixed `BorderImportLicenceDetailReportPending` filter parity in `Frontend/src/Report/config/reportConfigs.ts`.
- Fixed `BorderImportLicenceDetailReportPending` date columns to `Create Date` and `Approve Date`.
- Added `ApproveDate` to the pending result/row model and the existing pending fast LINQ projection.
- Added procedure migration files:
  - `StoredProcedureMigrations/sp_BorderImportLicenceDetailReport_pagination.sql`
  - `StoredProcedureMigrations/sp_BorderImportLicencePendingDetailReport_pagination.sql`
- Added targeted index migration:
  - `StoredProcedureMigrations/Indexes/BorderImportLicenceDetailReport_indexes.sql`
- Deployed both new procedure names to live `TradeNetDB`.
- Deployed both targeted live indexes:
  - `IX_BorderImportLicence_DetailReport_Approved`
  - `IX_BorderImportLicence_DetailReport_Pending`

## Procedure Validation Result

The new procedure names were tuned, deployed to live `TradeNetDB`, and validated against known live data dates:

- Approved/detail sample: `2026-05-21`
  - Procedure: `dbo.sp_BorderImportLicenceDetailReport_pagination`
  - Result: `2` rows, `TotalCount = 2`
  - Sample application: `MWDBIL32627000002`
  - Sample HS codes: `3901101200`, `5602900000`
- Pending detail sample: `2026-05-19`
  - Procedure: `dbo.sp_BorderImportLicencePendingDetailReport_pagination`
  - Result: `2` rows, `TotalCount = 2`
  - Sample applications: `test 3`, `test 4`

Both Border detail controllers now use the Border-specific procedure wrappers:

- `sp_BorderImportLicenceDetailReport`
- `sp_BorderImportLicencePendingDetailReport`

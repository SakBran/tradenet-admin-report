# ChequeNoReport to ChequeNoDetailReport Check

Date: 2026-06-29

## Old Admin Sources Checked

- Summary controller/view: `TradenetAdmin/Controllers/ReportsController.cs`, `Views/Reports/ChequeNoReport.cshtml`
- Detail controller/view: `TradenetAdmin/Controllers/ReportsController.cs`, `Views/Reports/ChequeNoDetailReport.cshtml`
- RDLC source of truth: `ReportControl/ChequeNoReport.rdlc`, `ReportControl/ChequeNoDetailReport.rdlc`
- Old stored procedures: `dbo.sp_ChequeNoReport`, `dbo.sp_ChequeNoDetailReport`

## Parity Findings Before Changes

### ChequeNoReport

- Filters matched old admin:
  - `FromDate`
  - `ToDate`
  - `ChequeNoId` / Cheque No dropdown with `--- All ---`
- Columns matched old RDLC:
  - `Cheque Id`
  - `Cheque No`
  - `Date`
  - `Amount`
- Missing behavior in new project:
  - Old RDLC makes `Cheque No` a hyperlink to `Reports/ChequeNoDetailReport?fdate=...&tdate=...&chequenoId=...`.
  - New `ChequeNoReport` did not drill into a detail report.

### ChequeNoDetailReport

- Old detail page exists in old admin and is opened from the summary hyperlink.
- Old detail page receives `FromDate`, `ToDate`, and `ChequeNoId` from the query string.
- Old detail RDLC columns:
  - `No.`
  - `Cheque No`
  - `Trxn Ref No.`
  - `Trxn Date`
  - `Form Type`
  - `Licence/Permit/Card No`
  - `Amount`
  - `Company Registration No`
  - `Company Name`
  - `Company Address`
- Old detail RDLC has an `Amount` total footer.
- New project had `Backend/StoredProcedureToLinq/sp_ChequeNoDetailReport.cs`, but was missing:
  - API controller
  - frontend report config
  - frontend page
  - route
  - summary drilldown link

## Changes Made

- Added `Backend/Controllers/Report/ChequeNoDetailReportController.cs`.
- Reused existing LINQ conversion `sp_ChequeNoDetailReport.Query`.
- Added `ColumnTotals` support for detail `amount`.
- Added `CompanyAddress` output assembled from the address fields.
- Added `ChequeNoDetailReport` frontend config with RDLC header order.
- Added summary `ChequeNoReport` drilldown on `Cheque No`:
  - carries `FromDate` and `ToDate`
  - maps clicked row `chequeId` to target `ChequeNoId`
  - opens detail report in a new tab, matching the old RDLC hyperlink behavior
- Added `Frontend/src/Report/Page/ChequeNoDetailReport.tsx`.
- Added route and Payment menu grouping.
- Extended `docs/ReportColumnComparison.md`.

## Follow-up Fixes

- Removed `ChequeNoDetailReport` from the sitemap/menu. It remains a route-only report reached from the `Cheque No` drilldown link in `ChequeNoReport`.
- Disabled the frontend lazy total-count request for `ChequeNoDetailReport`; the first page now avoids the expensive background count/footer request.
- Added `StoredProcedureMigrations/sp_ChequeNoDetailReport_pagination.sql`.
- Updated the detail API to use `dbo.sp_ChequeNoDetailReport_pagination` for paged data when the migration is deployed, with the LINQ query as a fallback if the procedure is not installed yet.

## SQL Decision

Initially no SQL was required to create the report because the LINQ conversion already existed. After UI testing showed the detail page could stay slow, I added a pagination wrapper procedure. Deploy `StoredProcedureMigrations/sp_ChequeNoDetailReport_pagination.sql` so the API can fetch only the requested page instead of relying on the large LINQ UNION query.

## Tests Run

- Passed: `dotnet test Backend.Tests\Backend.Tests.csproj --filter "FullyQualifiedName=Backend.Tests.ReportQueryTranslationTests.Cheque_no_detail_report_translates_to_sql"`
- Passed: `dotnet test Backend.Tests\Backend.Tests.csproj --filter "FullyQualifiedName~Cheque_no_detail_report"`
- Passed: `npm test -- src/Report/config/reportConfigs.chequeNo.test.ts`
- Passed: `npm test -- src/Report/config/reportConfigs.chequeNo.test.ts src/Report/reportNavItems.test.ts`
- Passed: `dotnet build Backend\API.csproj --no-restore`
- Passed: `npm run build`

Note: an earlier broad backend filter accidentally ran the shared endpoint fixture suite across many existing reports and failed broadly in this local environment. The focused Cheque No backend and frontend checks passed.

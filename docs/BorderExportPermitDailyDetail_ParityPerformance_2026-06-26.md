# Border Export Permit Daily and Detail Report Check

Date: 2026-06-26

## Scope

Checked and changed only:

- Border Export Permit Daily Report (New Permit Report)
- Border Export Permit Detail Report

Old admin source checked:

- `C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`
- `C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin\Business\Reports.cs`
- `C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\BorderExportPermitByDailyReport.rdlc`
- `C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\BorderExportPermitDetailReport.rdlc`

## Old Admin Parity

### Filter Box

Old Daily and Detail filter boxes both show:

- From Date / To Date
- EIR Card Type (`PaThaKaTypeId`)
- Export Section, scoped to Export Permit sections where `IsBorder == true`
- Sakhan

New UI before this change had extra visible fields on these reports:

- `Type`
- `BuyerCountryId`
- `CompanyRegistrationNo`

New UI after this change now shows only the old-admin filters. `BuyerCountryId` and `CompanyRegistrationNo` remain accepted by the backend for drilldown requests from other Border Export Permit summary reports, but they are no longer visible on the direct Detail/Daily filter box.

### Columns

Daily RDLC columns:

- Date
- No of Licences
- Total Value
- Currency
- Total USD Value

Detail RDLC columns:

- Section
- Permit No
- Permit Date
- Company Registration No
- Company Name
- Company Address
- Union Citizenship No
- Consignee Name
- Consignee Address
- Buyer Country
- Place/Port of Export
- Place/Port of Discharge
- Last Date
- Country of Orign
- Consigned Country
- Country of Destination
- Type of Permit
- HSCode
- Decription
- A/U
- Price
- Qty
- Value
- Currency
- Conditions

The new columns already matched the old RDLC headers and language. Focused tests now lock this.

## Backend Performance

### Daily Report

The Daily report was already using SQL-side aggregation:

- `sp_ExportPermitDetailReport_Fast.CreateAggregateResultAsync`
- `ReportAggregateDimension.Daily`
- `includeColumnTotals: true`

This avoids loading the full detail result into memory before grouping. No new stored procedure was required for Daily.

### Detail Report

The Detail report POST endpoint was still using the LINQ wide-join path:

- old path: `sp_ExportPermitDetailReport_Fast.CreatePagedResultAsync`

It now uses stored-procedure pagination:

- new path: `sp_ExportPermitDetailReport.CreatePagedResultAsync`
- SQL procedure: `dbo.sp_ExportPermitDetailReport_Fast_pagination`

This keeps paging, sorting, lazy total count, and `PageSize + 1` fast-page behavior server-side. This is the main performance change for the 10-second target.

## Database Deployment

Applied to the configured backend report database:

- Server: `203.81.66.111,14330`
- Database: `TradeNetDB`
- Applied procedure for Daily: `dbo.sp_ExportPermitDetailReport_Aggregate`
- Applied procedure for Detail: `dbo.sp_ExportPermitDetailReport_Fast_pagination`

After applying, SQL Server showed both procedures updated on `2026-06-26 13:59`.

Smoke tests against the latest approved Border Export Permit issued date in the database (`2026-05-25`) completed successfully:

- Daily aggregate procedure: `0.73s`
- Detail pagination procedure: `0.75s`

### Detail Load Failure Fix

After the API was switched to the Detail pagination stored procedure, the Border Export Permit Detail table failed to load because the generated dynamic SQL inside `dbo.sp_ExportPermitDetailReport_Fast_pagination` was truncated before execution. SQL Server returned:

- `Msg 4145 ... An expression of non-boolean type specified in a context where a condition is expected, near 'p'.`

The procedure script now starts the dynamic SQL string as `nvarchar(max)` for both Oversea and Border branches. The updated procedure was applied to `TradeNetDB`.

Verification after the fix:

- Detail procedure returned real Border Export Permit rows.
- First page query with total count returned `TotalCount = 269`.
- SQL execution time for the broad smoke range was `1.72s`.
- Detail frontend column bindings were checked against the backend API field names.

### UI Fast-Page Failure Fix

The generic table requests the first page with `includeTotalCount = false` so data can paint quickly. In that mode the Detail stored procedure returned `NULL` for `TotalCount`; the C# row model expects `int`, so EF could fail while materializing rows even though SQL Server returned data.

The procedure now initializes `@__total` to `0`, so fast-page responses always return a non-null `TotalCount`.

The requested Daily and Detail UI filters now keep the old-admin filter keywords and use the existing project pattern `defaultDateRangeMonths: 3`. This makes the default search range `2026-04-01` to `2026-06-26` on today's date, which includes the latest Border Export Permit data in `TradeNetDB` (`2026-05-21`).

Verification after this fix:

- SQL fast-page call with UI-style parameters returned rows in `0.87s`.
- Backend EF wrapper materialized the same Detail report rows successfully.
- EF wrapper smoke result: `Rows=3`, first permit `MWDBEP12627000001`, first company `SD04Co., Ltd.`

## Drilldown Navigation

The following Border Export Permit summary links now open the Detail report in a new browser tab:

- Border Export Permit Section Report: `Section` link
- Border Export Permit Buyer Country Report: `Country` link
- Border Export Permit Company List Report: `Company Name` link

Each link still carries the selected filter/date values and the clicked row parameter to `BorderExportPermitDetailReport`.

## Files Changed

- `Frontend/src/Report/config/reportConfigs.ts`
- `Frontend/src/Report/config/reportConfigs.borderExportPermit.test.ts`
- `Backend/Controllers/Report/BorderExportPermitDetailReportController.cs`
- `Backend/StoredProcedureToLinq/sp_ExportPermitDetailReport.cs`
- `Backend.Tests/BorderExportPermitDailyDetailContractTests.cs`

## Verification

Passed:

- `npm test -- --run src/Report/config/reportConfigs.borderExportPermit.test.ts -t "Daily and Detail"`
- `npm test -- --run src/Report/config/reportConfigs.borderExportPermit.test.ts -t "Daily and Detail|Detail report column bindings"`
- `npm test -- --run src/Report/config/reportConfigs.borderExportPermit.test.ts -t "drilldowns open Detail|Daily and Detail|Detail report column bindings"`
- `dotnet test Backend.Tests\Backend.Tests.csproj -c Release --filter FullyQualifiedName~BorderExportPermitDailyDetailContractTests`
- `dotnet build Backend/API.csproj -c Release`
- `npm run build`

Notes:

- The full `reportConfigs.borderExportPermit.test.ts` file still has unrelated pre-existing failures for other Border Export Permit reports. The new Daily/Detail tests pass.
- Frontend build passes with the existing Vite large chunk warning.
- The stored procedure deployment has now been applied to the configured `TradeNetDB` database for these two reports.

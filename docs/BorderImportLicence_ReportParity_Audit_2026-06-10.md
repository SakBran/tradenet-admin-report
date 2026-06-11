# Border Import Licence Report Parity Audit

Date: 2026-06-10  
Scope:

- Border Import Licence Daily Report
- Border Import Licence Detail Report
- Border Import Licence by Section Report
- Border Import Licence by Method Report
- Border Import Licence by Seller Country Report
- Border Import Licence Company List Report

Old source checked:

- `C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin\ReportControl\*.rdlc`
- `C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin\Views\Reports\BorderImportLicence*.cshtml`
- `C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`

New source checked:

- `Frontend/src/Report/config/reportConfigs.ts`
- `Frontend/src/Report/Page/GenericReportPage.tsx`
- `Backend/Controllers/Report/BorderImportLicence*.cs`
- `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs`
- `Backend/Service/Reports/ReportAggregationService.cs`

Reference family checked:

- Border Export Licence report configs/controllers and prior notes in `ReportTesting_PerfAndUiParity_2026-06-06.md`

## Executive Summary

The six requested Border Import Licence reports are not fully UI/data-parity ready with the old Tradenet 2.0 Admin.

Column parity is OK for all six reports. The old RDLC headers and new UI table columns match, with only the expected row-number label normalization (`Sr.No.`/`No.` in old, `No` in new BasicTable).

The main defects are:

1. New UI uses extra filters on multiple reports that the old filter boxes did not have.
2. `ExportImportSectionId` is not pinned to a Border Import Licence scoped lookup; it falls back to generic `exportImportSections`, unlike the old admin list: `Import Licence` + `IsBorder == true`.
3. Summary reports are missing legacy RDLC total footer rows because their controllers do not pass `includeColumnTotals: true`.
4. New Border Import Licence summary controllers do not follow the Border Export Licence reference behavior, where matching summary controllers already pass `includeColumnTotals: true`.
5. Daily report data design differs from old UI: the new UI exposes method/incoterm/seller-country filters, but old Daily only exposed From/To Date, Sakhan, Import Section, EIR Card Type, Company Registration No, and readonly Company Name.

## Column Parity

| Report | Old RDLC | Result |
| --- | --- | --- |
| BorderImportLicenceByMethodReport | `BorderImportLicenceByMethodReport.rdlc`: `Sr.No.`, `Method`, `No of Licences`, `Total Value`, `Currency` | PASS. New has `No`, `Method`, `No of Licences`, `Total Value`, `Currency`. |
| BorderImportLicenceBySectionReport | `BorderImportLicenceBySectionReport.rdlc`: `Sr.No.`, `Section`, `No of Licences`, `Total Value`, `Currency` | PASS. New has `No`, `Section`, `No of Licences`, `Total Value`, `Currency`. |
| BorderImportLicenceBySellerCountryReport | `BorderImportLicenceBySellerCountryReport.rdlc`: `Sr.No.`, `Country`, `No of Licences`, `Total Value`, `Currency` | PASS. New has `No`, `Country`, `No of Licences`, `Total Value`, `Currency`. |
| BorderImportLicenceCompanyListReport | `BorderImportLicenceByCompanyReport.rdlc`: `Sr.No.`, `Company Name`, `No of Licences`, `Total Value`, `Currency` | PASS. New has `No`, `Company Name`, `No of Licences`, `Total Value`, `Currency`. |
| BorderImportLicenceDailyReportNewLicenceReport | `BorderImportLicenceByDailyReport.rdlc`: `Date`, `No of Licences`, `Total Value`, `Currency`, `Total USD Value` | PASS. New has the same five headers. |
| BorderImportLicenceDetailReport | `BorderImportLicenceDetailReport.rdlc`: 27 columns from `Sr.No.` through `Conditions`, including `Create Date` and `Approve Date` | FIXED 2026-06-10. New now uses `Create Date` and `Approve Date` with `No` row number. Earlier audit text incorrectly recorded `Licence Date` and 26 columns. |

## Filter/UI Parity

| Report | Old filter box | New filter box | Parity result |
| --- | --- | --- | --- |
| By Method | From Date, To Date, Sakhan, EIR Card Type, Import Section, Import Method | Old filters plus `Type`, Import Incoterms, Seller Country, Company Registration No | FAIL. Extra visible filters in new. |
| By Section | From Date, To Date, Sakhan, EIR Card Type, Import Section, Import Method | Old filters plus `Type`, Import Incoterms, Seller Country, Company Registration No | FAIL. Extra visible filters in new. |
| By Seller Country | From Date, To Date, Sakhan, EIR Card Type, Import Section, Import Method, Seller Country | Old filters plus `Type`, Import Incoterms, Company Registration No | FAIL. Extra visible filters in new. |
| Company List | From Date, To Date, EIR Card Type, Sakhan, Import Section, Import Method, Company Registration No, readonly Company Name | Old filters plus `Type`, Import Incoterms, Seller Country; readonly Company Name helper omitted | FAIL. Extra filters and missing readonly helper. |
| Daily | From Date, To Date, Sakhan, Import Section, EIR Card Type, Company Registration No, readonly Company Name | Old filters plus `Type`, Import Method, Import Incoterms, Seller Country; readonly Company Name helper omitted | FAIL. Extra filters and missing readonly helper. |
| Detail | From Date, To Date, Sakhan, EIR Card Type, Import Section, Import Method, Import Incoterms | Old filters plus `Type`, Seller Country, Company Registration No | FAIL. Extra filters in new. |

Lookup/value parity notes:

- Old `ExportImportSectionList`: `exportImportSectionRepository.GetAll(AppConfig.ImportLicence).Where(IsActive && IsBorder)`, with user-section restriction for Check/Approve users.
- New `ExportImportSectionId`: no explicit `lookupName`, so `GenericReportPage` resolves it through `idFilterLookups` to generic `exportImportSections`.
- Old `ExportImportMethodList`: `GetAll(AppConfig.Import).Where(IsActive && IsBorder)` for the posted/report path.
- New `ExportImportMethodId`: no explicit `lookupName`, so it resolves to generic `exportImportMethods`.
- Old `ExportImportIncotermList` is used only by Detail among these six reports.
- New `ExportImportIncotermId` appears on all six, but old only had it on Detail.
- Old `SellerCountryList` is used only by Seller Country among these six reports.
- New `SellerCountryId` appears on all six.
- Old Company Name was a readonly helper paired with Company Registration No on Company List and Daily. New UI can fetch company-name helper for `CompanyRegistrationNo`, but the configs do not declare a separate readonly `CompanyName` filter like the old page.

## Total Row Parity

Old RDLC footer checks:

- By Method, By Section, By Seller Country, Company List: old RDLC contains `TOTAL`, `CountDistinct(Fields!LicenceNo.Value)`, and `Sum(Fields!Amount.Value)`.
- Daily: old RDLC contains `TOTAL`, `CountDistinct(Fields!LicenceNo.Value)`, `Sum(Fields!Amount.Value)`, and `Sum(Fields!totalUSDAmount.Value)`.
- Detail: old RDLC has no `Sum(...)`/grand-total footer.

New controller checks:

- `BorderImportLicenceByMethodReportController`
- `BorderImportLicenceBySectionReportController`
- `BorderImportLicenceBySellerCountryReportController`
- `BorderImportLicenceCompanyListReportController`
- `BorderImportLicenceDailyReportNewLicenceReportController`

All call `sp_ImportLicenceDetailReport_Fast.CreateAggregateResultAsync(...)` without `includeColumnTotals: true`, so their API result will not include the legacy total footer data.

Border Export Licence reference:

- Matching Border Export Licence summary controllers already pass `includeColumnTotals: true` for Method, Section, Seller Country, Company List, and Daily.
- Border Import Licence should follow the same design.

Result:

- Summary reports: FAIL, missing total footer parity.
- Detail report: PASS, no total footer expected.

## Data Accuracy Design Check

Old design:

- The six old reports call `LicencePermitReports.GetImportLicenceDetailReport(model)` and then use RDLC grouping/table definitions for the summary views.
- The data model filters include `Type = Border`, `ApplyType = New`, approved licence rows, border import section/method option sets, and optional dimension filters.

New design:

- The six new reports share `sp_ImportLicenceDetailReport_Fast`.
- Detail uses paged rows from the same source.
- Summary reports group in SQL through `AggregateInSqlAsync`, avoiding full detail materialization.
- Daily fills `TotalUSDValue` during aggregation.
- This is generally the correct performance design, and it follows the same family pattern as Border Export Licence.

Accuracy risks:

1. Extra UI filters can change the query result in ways impossible in old Admin for those reports.
2. Generic lookup endpoints can let users choose non-border or non-import section/method/incoterm values. Those values may return no data or wrong scoped data compared with old Admin.
3. Missing `includeColumnTotals: true` means visible totals do not match old RDLC even when row-level data is correct.
4. User section restriction from the old MVC session (`GetSections(AppConfig.ImportLicence, AppConfig.Border)`) was part of old UI/list binding. I did not confirm an equivalent current-user section restriction in the new lookup/API path during this audit.

## Performance Check

Current performance design is better than the old RDLC path in principle:

- New detail paging uses `Rows(db, request).Skip(...).Take(...)`.
- Summary reports use SQL-side aggregation through `AggregateInSqlAsync`.
- Excel export uses queued/background streaming or grouped rows.

Known related tracker:

- `docs/LinqToStoredProcedurePaginationTask.md` marks `sp_ImportLicenceDetailReport` / `sp_ImportLicenceDetailReport_pagination` as in progress for Border Import Licence and Import Licence detail/summary reports.

Performance risk:

- Because the new summary reports expose extra filters, especially generic section/method/incoterm/country filters, users can submit combinations the old report did not allow. That can create empty results or less predictable query plans.

## Test Result

Command run:

```powershell
dotnet test Backend.Tests/Backend.Tests.csproj --filter "FullyQualifiedName~ReportControllerBranchDefaultsTests|FullyQualifiedName~ReportEndpointPayloadFixtureTests" --no-restore
```

Result:

- Build completed with existing nullable warnings.
- Tests run: 269.
- Passed: 267.
- Failed: 2.
- Failures are unrelated to these six reports: `CardListsByCompanyRegistrationNumberController` is missing `TryCreateReportRequest`.
- The requested Border Import Licence controller request-factory cases were included in the passing set.

## Recommended Task Checklist

### UI parity fixes

- [ ] Add scoped lookup endpoints for Border Import Licence sections, methods, and incoterms, mirroring old Admin:
  - Sections: `Type == "Import Licence"` and `IsBorder == true`.
  - Methods: `Type == "Import"` and `IsBorder == true`.
  - Incoterms: `Type == "Import"` and `IsBorder == true`.
- [ ] Pin `ExportImportSectionId` in all six configs to the new Border Import Licence section lookup.
- [ ] Pin `ExportImportMethodId` where present to the new Border Import Licence method lookup.
- [ ] Pin `ExportImportIncotermId` where present to the new Border Import Licence incoterm lookup.
- [ ] Remove visible `Type` filter from all six reports; the controller already forces `Type = "Border"`.
- [ ] By Method: remove `ExportImportIncotermId`, `SellerCountryId`, and `CompanyRegistrationNo`.
- [ ] By Section: remove `ExportImportIncotermId`, `SellerCountryId`, and `CompanyRegistrationNo`.
- [ ] By Seller Country: remove `ExportImportIncotermId` and `CompanyRegistrationNo`.
- [ ] Company List: remove `ExportImportIncotermId` and `SellerCountryId`; add/confirm readonly Company Name helper behavior for Company Registration No.
- [ ] Daily: remove `ExportImportMethodId`, `ExportImportIncotermId`, and `SellerCountryId`; add/confirm readonly Company Name helper behavior for Company Registration No.
- [ ] Detail: remove `SellerCountryId` and `CompanyRegistrationNo`.

### Total row fixes

- [ ] Add `includeColumnTotals: true` to:
  - `BorderImportLicenceByMethodReportController`
  - `BorderImportLicenceBySectionReportController`
  - `BorderImportLicenceBySellerCountryReportController`
  - `BorderImportLicenceCompanyListReportController`
  - `BorderImportLicenceDailyReportNewLicenceReportController`
- [ ] Confirm BasicTable renders `noOfLicences`, `totalValue`, and for Daily `totalUSDValue` totals from `ColumnTotals`.
- [ ] Leave `BorderImportLicenceDetailReportController` unchanged for totals; old RDLC has no grand-total footer.

### Data accuracy tests

- [ ] Add tests that the six controller request factories force `Type = "Border"` regardless of client `Type`.
- [ ] Add tests for scoped lookup endpoints so Border Import Licence sections/methods/incoterms do not leak oversea/export/permit values.
- [ ] Add aggregate tests comparing grouped rows for Method, Section, Seller Country, Company, and Daily against a seeded detail dataset.
- [ ] Add a Daily total test that verifies `totalUSDValue` is included in `ColumnTotals` after `includeColumnTotals: true`.
- [ ] Add a UI config snapshot or unit test to verify each report exposes only the old Admin filter set.

### Manual QA checklist

- [ ] Open each of the six report pages.
- [ ] Confirm filter labels and controls match old Admin.
- [ ] Confirm section/method/incoterm dropdown values are scoped to Border Import Licence.
- [ ] Search a known date range with data.
- [ ] Verify displayed columns and language match old RDLC.
- [ ] Verify summary report total rows match old RDLC.
- [ ] Drill from summary rows into Detail and confirm carried filters match old URL behavior.
- [ ] Export Excel for each report and verify row count, headers, and totals.

## Final Verdict

Do not mark these six Border Import Licence reports as old-admin parity complete yet.

Columns pass, and the shared fast data path is the right performance direction. The remaining work is mostly UI/filter parity, scoped lookup parity, and missing summary total rows. Border Export Licence provides a clear implementation reference for the total-row behavior.

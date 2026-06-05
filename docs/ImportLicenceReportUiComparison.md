# Import Licence Report UI Comparison

Status: implementation pass completed for non-border `ImportLicence*` reports only.
Validation method: code-only comparison against the old source at `..\tradenet-2.0-admin\TradenetAdmin`, per the latest user instruction.

## Sources Compared

- Current frontend:
  - `Frontend/src/Report/config/reportConfigs.ts`
  - `Frontend/src/Report/Page/GenericReportPage.tsx`
  - `Frontend/src/Report/config/reportTypes.ts`
- Current backend:
  - `Backend/Controllers/ReportLookupsController.cs`
  - `Backend/Controllers/Report/ImportLicenceByHSCodeReportController.cs`
  - `Backend/Controllers/Report/ImportLicenceNewReportNewReportController.cs`
  - `Backend/StoredProcedureToLinq/sp_HSCodeReport.cs`
  - `Backend/StoredProcedureToLinq/sp_NewReport.cs`
- Old Tradenet Admin:
  - `..\tradenet-2.0-admin\TradenetAdmin\Views\Reports\ImportLicence*.cshtml`
  - `..\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`
  - `..\tradenet-2.0-admin\TradenetAdmin\Business\Reports.cs`
  - `..\tradenet-2.0-admin\TradenetAdmin\ReportControl\*.rdlc`
- Existing table-header extraction:
  - `ReportColumnComparison.md`
  - `ReportColumnUiFixStatus.md`

## Implemented Filter Parity

All current routes stay the same: `/Report/<ReportName>`. API route stays `<ReportName>`.

| Current report | Old view/action | Old visible filters | New visible filters after fix | Status |
| --- | --- | --- | --- | --- |
| `ImportLicenceDetailReport` | `ImportLicenceDetailReport.cshtml` / `ImportLicenceDetailReport` | From Date, To Date, PaThaKa Type, Import Section, Import Method, Import Incoterms | From Date / To Date, PaThaKa Type, Import Section, Import Method, Import Incoterms | Fixed |
| `ImportLicenceDetailReportPending` | same old detail view/report path for pending | From Date, To Date, PaThaKa Type, Import Section, Import Method, Import Incoterms | From Date / To Date, PaThaKa Type, Import Section, Import Method, Import Incoterms | Fixed |
| `ImportLicenceBySectionReport` | `ImportLicenceBySectionReport.cshtml` | From Date, To Date, PaThaKa Type, Import Section, Import Method | Same | Fixed |
| `ImportLicenceByMethodReport` | `ImportLicenceByMethodReport.cshtml` | From Date, To Date, PaThaKa Type, Import Section, Import Method | Same | Fixed |
| `ImportLicenceBySellerCountryReport` | `ImportLicenceBySellerCountryReport.cshtml` | From Date, To Date, PaThaKa Type, Import Section, Import Method, Seller Country | Same | Fixed |
| `ImportLicenceCompanyListReport` | old `ImportLicenceByCompanyReport` | From Date, To Date, PaThaKa Type, Import Section, Export Method, Company Registration No, readonly Company Name | Same | Fixed |
| `ImportLicenceDailyReportNewLicenceReport` | old `ImportLicenceByDailyReport` | From Date, To Date, Import Section, PaThaKa Type, Company Registration No, readonly Company Name | Same | Fixed |
| `ImportLicenceTotalValueLicencesReport` | old total value/licences report | From Date, To Date, PaThaKa Type, Import Section | Same | Fixed |
| `ImportLicenceByHSCodeReport` | `ImportLicenceByHSCodeReport.cshtml` | From Date, To Date, Import Section, Filter By, HS Code | Same | Fixed |
| `ImportLicenceAmendmentReport` | `ImportLicenceAmendReport.cshtml` | From Date, To Date, Import Section, Company Registration No, readonly Company Name, Remark | Same | Fixed |
| `ImportLicenceActualAmendmentReport` | old actual amendment action/view family | From Date, To Date, Import Section, Company Registration No, readonly Company Name, Remark | Same | Fixed |
| `ImportLicenceExtensionReport` | `ImportLicenceExtensionReport.cshtml` | From Date, To Date, Import Section, Company Registration No, readonly Company Name | Same | Fixed |
| `ImportLicenceCancellationReport` | `ImportLicenceCancelReport.cshtml` | From Date, To Date, Import Section, Company Registration No, readonly Company Name | Same | Fixed |
| `ImportLicenceNewReportNewReport` | `ImportLicenceNewReport.cshtml` | From Date, To Date, Import Section, Company Registration No, readonly Company Name, Auto / None Auto, Quota | Same | Fixed |
| `ImportLicencePendingReport` | `ImportLicencePendingReport.cshtml` | From Date, To Date, Import Section | Same | Fixed |
| `ImportLicenceVoucherReport` | `ImportLicenceVoucherReport.cshtml` | From Date, To Date, Import Section, Apply Type, Payment Type, Company Registration No, readonly Company Name | Same | Fixed |

## Implemented Lookup/Option Data Parity

| Filter | Old source behavior | New behavior |
| --- | --- | --- |
| Import Section | `ExportImportSectionRepository.GetAll(AppConfig.ImportLicence)` filtered by `IsActive == true` and `IsOversea == true` | New `ReportLookups/importLicenceSections` filters `Type == "Import Licence"`, active, not deleted, and oversea |
| Import Method | `ExportImportMethodRepository.GetAll(AppConfig.Import)` filtered by active oversea | New `ReportLookups/importLicenceMethods` filters `Type == "Import"`, active, not deleted, and oversea |
| Import Incoterms | `ExportImportIncotermRepository.GetAll(AppConfig.Import)` filtered by active oversea | New `ReportLookups/importLicenceIncoterms` filters `Type == "Import"`, active, not deleted, and oversea |
| Payment Type | Old voucher screen uses active `PaymentType` rows, label `Name`, value `Id` | New `ReportLookups/paymentTypes` returns active, not-deleted rows with label `Name`, string value `Id` |
| Apply Type | Old `CommonRepository.GetApplyTypeList()` excluding `Fine` | New Import Licence voucher options: New, Amend, Extension, Cancel, Actual Amend |
| Auto / None Auto | Old option values `auto`, `none-auto` | New select uses All, auto, none-auto |
| Quota | Old visible option values `quota`, `no-quota`; old stored-procedure parameter was commented out | New select uses All, quota, no-quota; when selected, new API filters by `ImportLicence.Quota` through the LINQ path |

Current-user section restriction note: old screens further restricted sections for Check/Approve users through `GetSections(AppConfig.ImportLicence, AppConfig.Oversea)`. Old code reads `UserDetail.Section` as comma-separated section codes for the current MOC session user. The new backend has `UserDetails`, but this pass did not change section dropdowns by current user because the current JWT/user-role contract and code-to-id mapping need confirmation before filtering lookup results.

Readonly `CompanyName` note: old screens showed a readonly field populated by old `reports.js` typeahead/company lookup. The new React filter panel now reproduces that display-only field for Import Licence reports that had it. It is populated from `ReportLookups/company-name?companyRegistrationNo=...` and is excluded from report request payloads, so stored-procedure behavior remains keyed by `CompanyRegistrationNo`.

## Table Header / Result UI Comparison

The old result UI renders a `Microsoft.Reporting.WebForms.ReportViewer` through partials such as `_ImportLicenceReportView.cshtml`, `_AmendReportView.cshtml`, `_ExtensionReportView.cshtml`, `_CancelReportView.cshtml`, `_VoucherReportView.cshtml`, and `_HSCodeReportView.cshtml`.

The old `SafeReportViewerHtmlHelper` emits an iframe with `width:100%; height:800px; border:none;`. The old controller configures the viewer with `SizeToReportContent = true` and `ZoomMode = FullPage`.

The current frontend still uses `BasicTable`, but Import Licence reports now render it in a scoped legacy ReportViewer-like container:

- 800px minimum report area.
- Flat gray toolbar strip around the grid controls.
- Black table grid borders.
- Compact RDLC-like cell padding.
- White centered wrapped table headers.
- Row number header label `No.` for Import Licence reports.
- Old `header1` report title/subtitle text generated from the active filter date range.

Implemented table-header fix:

- `ReportColumnComparison.md` now shows `Need in new (0)` and `Extra in new (0)` for every non-border `ImportLicence*` report.
- `ImportLicenceVoucherReport` now matches the old visible `VoucherReport.rdlc` column list and order. It includes the old `Licence No`, `Application Date`, dynamic number header, `Application No`, dynamic date header, company, licence value, currency, voucher, approved user, commodity, CIF, exchange rate, and amount columns.
- `ImportLicenceVoucherReport` changes the two dynamic RDLC headers by `ApplyType`:
  - New: `Licence No`, `Licence Date`
  - Amend: `Licence Amendment No`, `Amendment Date`
  - Extension: `Licence Extension No`, `Extension Date`
  - Cancel: `Licence Cancel No`, `Cancellation Date`
  - Actual Amend: `Licence Actual Amendment No`, `Actual Amendment Date`

Intentional result UI differences for this pass:

- New frontend uses `BasicTable`, not a full Microsoft RDLC `ReportViewer` toolbar.
- Browser screenshot/runtime validation was skipped because the latest user instruction asked to compare via code only.
- The old Import Licence seller country controller title says `List of Export Licences By Seller Country...`; current code uses `List of Import Licences By Seller Country...` because this is an Import Licence report. Ask before preserving that old typo exactly.

## Backend Result Parity Changes

- `ImportLicenceByHSCodeReport` now accepts `ExportImportSectionId`, matching the old HS Code screen. If Section is `All`, the existing aggregate stored procedure path is preserved. If a specific Section is selected, the existing LINQ aggregate path is used so the filter actually applies.
- `ImportLicenceNewReportNewReport` now accepts `Quota`. If Quota is not selected, the existing paged stored procedure path is preserved. If Quota is selected, the existing LINQ query path is used and filters `ImportLicence.Quota`.
- `sp_NewReport.ImportLicenceQuery` now also honors `Auto` and `Quota` when the LINQ path is used.
- `ReportLookups/company-name` returns the current PaThaKa company name for the entered `CompanyRegistrationNo`, allowing the frontend to match the old readonly Company Name filter field without changing report request models.

## Generated Column Comparison

`node tools\compare-report-columns.mjs` was rerun after the voucher/table updates and regenerated `ReportColumnComparison.md`.

Import Licence result:

- `ImportLicenceActualAmendmentReport`: 0 missing, 0 extra.
- `ImportLicenceAmendmentReport`: 0 missing, 0 extra.
- `ImportLicenceByHSCodeReport`: 0 missing, 0 extra.
- `ImportLicenceByMethodReport`: 0 missing, 0 extra.
- `ImportLicenceBySectionReport`: 0 missing, 0 extra.
- `ImportLicenceBySellerCountryReport`: 0 missing, 0 extra.
- `ImportLicenceCancellationReport`: 0 missing, 0 extra.
- `ImportLicenceCompanyListReport`: 0 missing, 0 extra.
- `ImportLicenceDailyReportNewLicenceReport`: 0 missing, 0 extra.
- `ImportLicenceDetailReport`: 0 missing, 0 extra.
- `ImportLicenceDetailReportPending`: 0 missing, 0 extra.
- `ImportLicenceExtensionReport`: 0 missing, 0 extra.
- `ImportLicenceNewReportNewReport`: 0 missing, 0 extra.
- `ImportLicencePendingReport`: 0 missing, 0 extra.
- `ImportLicenceTotalValueLicencesReport`: 0 missing, 0 extra.
- `ImportLicenceVoucherReport`: 0 missing, 0 extra.

Remaining `ReportColumnComparison.md` mismatches are outside the current Import Licence scope.

## Verification

- `node tools\compare-report-columns.mjs`: passed for scoped Import Licence reports. The generated file still reports unrelated non-Import-Licence mismatches.
- `dotnet build Backend\API.csproj --no-restore`: passed with 0 errors. Current full output reports existing nullable/migration warnings.
- `npm run build` in `Frontend`: passed. Vite reported the existing large chunk warning.
- `dotnet test Backend.Tests\Backend.Tests.csproj --no-restore`: failed because the local test environment/suite is not ready for a full run. Key failures were missing `TRADENET_REPORT_TEST_CONNECTION_STRING`, missing stored procedures such as `dbo.sp_VoucherReport_pagination`, and an unrelated fixture failure for `CardListsByCompanyRegistrationNumberController` missing `TryCreateReportRequest`.

## Final Gaps / User Confirmation Needed

- Whether current-user section restriction must be implemented for MOC Check/Approve users in the new app. If yes, confirm that `ClaimTypes.Name` maps to `TradeNet.User.Id`, `ClaimTypes.Role` can identify old Check/Approve users, and `UserDetail.Section` stores ExportImportSection codes that should filter the Import Licence section dropdown.
- Whether the old `Export Method` label on the Import Licence Company List screen is a typo. It is currently preserved for visual parity.
- Whether to preserve the old seller country report title typo (`Export Licences`) exactly.

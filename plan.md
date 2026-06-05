# Import Licence Report UI Parity Plan

## Confirmed Requirement

- Work only on non-border `ImportLicence*` reports for now.
- Compare the current React report UI with the old Tradenet Admin source at `..\tradenet-2.0-admin\TradenetAdmin`.
- Use code-only validation for this pass. Do not use browser screenshots unless the user asks again.
- Match old Import Licence behavior for:
  - Report-specific visible filters.
  - Filter labels.
  - Filter option values and lookup scoping.
  - Result table header labels and order.
  - Result table look close to the old RDLC ReportViewer output.
- Do not change unrelated report families unless the user expands the scope.
- Do not assume missing behavior. If current source and old source do not provide enough evidence, document the gap and ask.

## Old Source Authority

- Old views:
  - `..\tradenet-2.0-admin\TradenetAdmin\Views\Reports\ImportLicence*.cshtml`
  - `..\tradenet-2.0-admin\TradenetAdmin\Views\Reports\_ImportLicenceReportView.cshtml`
  - Other partials used by Import Licence reports: `_AmendReportView`, `_ExtensionReportView`, `_CancelReportView`, `_VoucherReportView`, `_HSCodeReportView`.
- Old controller:
  - `..\tradenet-2.0-admin\TradenetAdmin\Controllers\ReportsController.cs`
- Old RDLC reports:
  - `ImportLicenceDetailReport.rdlc`
  - `ImportLicenceBySectionReport.rdlc`
  - `ImportLicenceByMethodReport.rdlc`
  - `ImportLicenceBySellerCountryReport.rdlc`
  - `ImportLicenceByCompanyReport.rdlc`
  - `ImportLicenceByDailyReport.rdlc`
  - `ImportLicenceByTotalValueLicenceReport.rdlc`
  - `HSCodeReport.rdlc`
  - `HSCodeDetailReport.rdlc`
  - `AmendReport.rdlc`
  - `CancelReport.rdlc`
  - `ExtensionReport.rdlc`
  - `NewLicenceReport.rdlc`
  - `PendingLicenceReport.rdlc`
  - `VoucherReport.rdlc`
- Old result viewer source:
  - `Helpers\SafeReportViewerHtmlHelper.cs`
  - `ReportViewerWebForm.aspx`

## Completed Fixes

- Report-specific Import Licence filter sets are now configured in `Frontend/src/Report/config/reportConfigs.ts`.
- Import Licence lookup select boxes now use Import Licence-specific lookup names instead of broad global lookup data.
- `ReportLookupsController` now exposes:
  - `importLicenceSections`: active, not deleted, oversea, `Type == "Import Licence"`.
  - `importLicenceMethods`: active, not deleted, oversea, `Type == "Import"`.
  - `importLicenceIncoterms`: active, not deleted, oversea, `Type == "Import"`.
  - `paymentTypes`: active, not deleted, label `Name`, value `Id`.
- Voucher `ApplyType` values now match the old voucher list: `New`, `Amend`, `Extension`, `Cancel`, `Actual Amend`.
- `ImportLicenceByHSCodeReport` now sends and applies `ExportImportSectionId`.
- `ImportLicenceNewReportNewReport` now sends and applies `Quota` when selected, and continues to support `Auto`.
- Import Licence report grids now render through a scoped RDLC-like table shell:
  - 800px minimum report area.
  - Gray toolbar strip.
  - Black grid borders.
  - Compact cell padding.
  - White centered wrapped headers.
  - Row number header `No.`.
- Old `header1` subtitle strings are generated from filters for Import Licence reports.
- `ImportLicenceVoucherReport` columns now match the old visible `VoucherReport.rdlc` columns and order, including dynamic `header2` and `header3` behavior.
- `ReportColumnComparison.md` was regenerated and shows 0 missing and 0 extra columns for every non-border `ImportLicence*` report.
- Detailed comparison status is documented in `docs/ImportLicenceReportUiComparison.md`.

## Current Import Licence Filter Parity Status

| Report | Status |
| --- | --- |
| `ImportLicenceDetailReport` | Fixed |
| `ImportLicenceDetailReportPending` | Fixed |
| `ImportLicenceBySectionReport` | Fixed |
| `ImportLicenceByMethodReport` | Fixed |
| `ImportLicenceBySellerCountryReport` | Fixed |
| `ImportLicenceCompanyListReport` | Mostly fixed, old readonly Company Name not reproduced |
| `ImportLicenceDailyReportNewLicenceReport` | Mostly fixed, old readonly Company Name not reproduced |
| `ImportLicenceTotalValueLicencesReport` | Fixed |
| `ImportLicenceByHSCodeReport` | Fixed |
| `ImportLicenceAmendmentReport` | Mostly fixed, old readonly Company Name not reproduced |
| `ImportLicenceActualAmendmentReport` | Mostly fixed, old readonly Company Name not reproduced |
| `ImportLicenceExtensionReport` | Mostly fixed, old readonly Company Name not reproduced |
| `ImportLicenceCancellationReport` | Mostly fixed, old readonly Company Name not reproduced |
| `ImportLicenceNewReportNewReport` | Mostly fixed, old readonly Company Name not reproduced |
| `ImportLicencePendingReport` | Fixed |
| `ImportLicenceVoucherReport` | Mostly fixed, old readonly Company Name not reproduced |

## Remaining Gaps To Confirm Before Further Changes

- Old `Company Name` filter fields are readonly display fields populated by old JavaScript/typeahead behavior. Current report APIs filter by `CompanyRegistrationNo`; a safe equivalent company lookup flow was not found in the current report UI.
- Old section lists may be further restricted for MOC Check/Approve users via `GetSections(AppConfig.ImportLicence, AppConfig.Oversea)`. A current backend equivalent for that user-section mapping was not found yet.
- Old `ImportLicenceByCompanyReport` labels the method filter as `Export Method` even though this is an Import Licence screen. Current code preserves that label for visual parity.
- Old Import Licence seller country report header text says `List of Export Licences By Seller Country...`. Current code uses `List of Import Licences By Seller Country...`; ask before preserving that old typo exactly.
- Current result UI is RDLC-like but not a full Microsoft ReportViewer toolbar. Recreating a full RDLC/WebForms toolbar in React is outside this pass.

## Verification Results

- `node tools\compare-report-columns.mjs`: completed. All non-border Import Licence reports have 0 missing and 0 extra columns; remaining generated mismatches are outside scope.
- `dotnet build Backend\API.csproj --no-restore`: passed with 0 warnings and 0 errors.
- `npm run build` in `Frontend`: passed. Vite reported the existing large chunk warning.
- `dotnet test Backend.Tests\Backend.Tests.csproj --no-restore`: failed due existing test environment/suite prerequisites:
  - `TRADENET_REPORT_TEST_CONNECTION_STRING` is not set for shared-database validation.
  - Some stored procedures required by broad report smoke tests are missing locally, including `dbo.sp_VoucherReport_pagination`.
  - `CardListsByCompanyRegistrationNumberController` is missing `TryCreateReportRequest`, which is unrelated to this Import Licence pass.

## Definition Of Done For This Pass

- Import Licence visible filters match old report-specific screens, except documented readonly display fields.
- Import Licence lookup option values are scoped like old Tradenet Admin.
- Import Licence table headers and order match old RDLC visible columns.
- Import Licence result grid is visually closer to the old RDLC ReportViewer output.
- Code-only comparison docs and generated column comparison are updated.
- Build/test status is documented.

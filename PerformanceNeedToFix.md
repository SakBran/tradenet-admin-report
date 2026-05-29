# Performance Need To Fix

Updated: 2026-05-29 13:21:47 +06:30

## Scope

- Report API controllers measured: 125
- Report lookup endpoints measured: 12
- Endpoints measured: 262 (`POST /api/{Report}`, `POST /api/{Report}/Excel`, and `GET /api/ReportLookups/{lookupName}`).
- Data source: `TradeNetDBTest` via `ReportTestHelper.CreateTradeNetDbTestDbContext()`.
- Payload window: `2000-01-01 00:00:00` to `2100-12-31 23:59:59`.
- Paging payload: `PageIndex=0`, `PageSize=10`, no API sort/filter, `IncludeTotalCount=false`.
- Budgets: page endpoint <= 1000 ms; lookup endpoint <= 500 ms; Excel endpoint <= 5000 ms.
- `Need Fix` includes endpoints that throw errors or exceed the current budget.
- Interpretation: this is a local `TradeNetDBTest` baseline; rerun against a production-sized restore before treating the empty/fast rows as final capacity proof.
- Non-report auth/upload/chat/user endpoints are not included here because they require separate credential, multipart upload, or write-safe mutation fixtures.

## How To Re-run

```powershell
$env:REPORT_PERF_MARKDOWN_PATH='C:\Code\Ministry of Commerce\Tradenet\tradenet-admin-report\PerformanceNeedToFix.md'
dotnet test Backend.Tests\Backend.Tests.csproj --filter "FullyQualifiedName~ReportEndpointPerformanceTests" -p:UseAppHost=false -p:BaseOutputPath=C:\Code\Ministry_of_Commerce_Tradenet_test_build\
```

Set `REPORT_PERF_FAIL_ON_SLOW=1` before running when the test should fail on over-budget endpoints.

## Need Fix

No measured endpoints exceeded the current performance budgets.

## Slowest Page Endpoints

| Endpoint | Action | Elapsed ms | Result | Status |
| --- | ---: | ---: | --- | --- |
| `/api/AccountSummaryReport` | `Post` | 615 | TotalCount=0 | OK |
| `/api/BorderExportLicenceByMethodReport` | `Post` | 248 | TotalCount=0 | OK |
| `/api/OnlineFeesReport` | `Post` | 120 | TotalCount=0 | OK |
| `/api/BorderExportLicenceActualAmendmentReport` | `Post` | 89 | TotalCount=0 | OK |
| `/api/BorderImportLicenceByMethodReport` | `Post` | 71 | TotalCount=0 | OK |
| `/api/BorderExportLicenceVoucherReport` | `Post` | 69 | TotalCount=0 | OK |
| `/api/BorderImportLicenceDetailReportPending` | `Post` | 68 | TotalCount=0 | OK |
| `/api/BorderImportLicencePendingReport` | `Post` | 65 | TotalCount=0 | OK |
| `/api/BorderImportLicenceActualAmendmentReport` | `Post` | 59 | TotalCount=0 | OK |
| `/api/BorderImportLicenceVoucherReport` | `Post` | 59 | TotalCount=0 | OK |
| `/api/BorderImportLicenceExtensionReport` | `Post` | 55 | TotalCount=0 | OK |
| `/api/BorderImportLicenceAmendmentReport` | `Post` | 54 | TotalCount=0 | OK |
| `/api/BorderExportLicenceExtensionReport` | `Post` | 53 | TotalCount=0 | OK |
| `/api/BorderExportLicenceAmendmentReport` | `Post` | 51 | TotalCount=0 | OK |
| `/api/BorderExportLicenceCancellationReport` | `Post` | 51 | TotalCount=0 | OK |
| `/api/BorderExportLicenceNewReportNewReport` | `Post` | 49 | TotalCount=0 | OK |
| `/api/BorderImportLicenceCancellationReport` | `Post` | 49 | TotalCount=0 | OK |
| `/api/BorderImportLicenceNewReportNewReport` | `Post` | 49 | TotalCount=0 | OK |
| `/api/CompanyProfile` | `Post` | 46 | TotalCount=3 | OK |
| `/api/BorderExportPermitBySectionReport` | `Post` | 45 | TotalCount=0 | OK |
| `/api/BorderImportPermitBySectionReport` | `Post` | 44 | TotalCount=0 | OK |
| `/api/ExportLicenceByMethodReport` | `Post` | 44 | TotalCount=0 | OK |
| `/api/ImportPermitVoucherReport` | `Post` | 44 | TotalCount=0 | OK |
| `/api/ExportPermitBySectionReport` | `Post` | 43 | TotalCount=0 | OK |
| `/api/ExportLicenceVoucherReport` | `Post` | 40 | TotalCount=0 | OK |

## Slowest Excel Endpoints

| Endpoint | Action | Elapsed ms | Result | Status |
| --- | ---: | ---: | --- | --- |
| `/api/AccountSummaryReport/Excel` | `Excel` | 121 | Bytes=2199 | OK |
| `/api/OnlineFeesReport/Excel` | `Excel` | 106 | Bytes=2138 | OK |
| `/api/BorderExportLicenceByMethodReport/Excel` | `Excel` | 51 | Bytes=2467 | OK |
| `/api/BorderImportLicenceByMethodReport/Excel` | `Excel` | 44 | Bytes=2483 | OK |
| `/api/BorderImportLicenceDetailReportPending/Excel` | `Excel` | 42 | Bytes=2474 | OK |
| `/api/ImportLicencePendingReport/Excel` | `Excel` | 40 | Bytes=2192 | OK |
| `/api/BorderImportLicenceExtensionReport/Excel` | `Excel` | 36 | Bytes=2247 | OK |
| `/api/BorderImportLicencePendingReport/Excel` | `Excel` | 36 | Bytes=2197 | OK |
| `/api/BorderImportLicenceVoucherReport/Excel` | `Excel` | 36 | Bytes=2290 | OK |
| `/api/BorderExportLicenceActualAmendmentReport/Excel` | `Excel` | 33 | Bytes=2249 | OK |
| `/api/BorderExportLicenceVoucherReport/Excel` | `Excel` | 32 | Bytes=2290 | OK |
| `/api/BorderImportLicenceActualAmendmentReport/Excel` | `Excel` | 32 | Bytes=2249 | OK |
| `/api/BorderImportLicenceCancellationReport/Excel` | `Excel` | 32 | Bytes=2256 | OK |
| `/api/BorderExportLicenceAmendmentReport/Excel` | `Excel` | 31 | Bytes=2247 | OK |
| `/api/BorderImportLicenceAmendmentReport/Excel` | `Excel` | 31 | Bytes=2246 | OK |
| `/api/BorderExportLicenceCancellationReport/Excel` | `Excel` | 30 | Bytes=2257 | OK |
| `/api/BorderExportLicenceNewReportNewReport/Excel` | `Excel` | 30 | Bytes=2274 | OK |
| `/api/BorderExportPermitBySectionReport/Excel` | `Excel` | 30 | Bytes=2456 | OK |
| `/api/BorderImportLicenceNewReportNewReport/Excel` | `Excel` | 29 | Bytes=2273 | OK |
| `/api/BorderImportPermitBySectionReport/Excel` | `Excel` | 27 | Bytes=2443 | OK |
| `/api/ExportPermitBySectionReport/Excel` | `Excel` | 27 | Bytes=2455 | OK |
| `/api/ImportPermitBySectionReport/Excel` | `Excel` | 27 | Bytes=2443 | OK |
| `/api/ImportLicenceDetailReportPending/Excel` | `Excel` | 26 | Bytes=2474 | OK |
| `/api/BorderExportLicenceExtensionReport/Excel` | `Excel` | 25 | Bytes=2246 | OK |
| `/api/BorderExportPermitVoucherReport/Excel` | `Excel` | 25 | Bytes=2290 | OK |

## Slowest Lookup Endpoints

| Endpoint | Action | Elapsed ms | Result | Status |
| --- | ---: | ---: | --- | --- |
| `/api/ReportLookups/amendremarks` | `Get` | 7 | Options=3 | OK |
| `/api/ReportLookups/nrcprefixes` | `Get` | 7 | Options=418 | OK |
| `/api/ReportLookups/countries` | `Get` | 6 | Options=251 | OK |
| `/api/ReportLookups/exportimportmethods` | `Get` | 6 | Options=35 | OK |
| `/api/ReportLookups/exportimportsections` | `Get` | 6 | Options=7 | OK |
| `/api/ReportLookups/lineofbusinesses` | `Get` | 6 | Options=11 | OK |
| `/api/ReportLookups/businesstypes` | `Get` | 5 | Options=16 | OK |
| `/api/ReportLookups/chequenos` | `Get` | 5 | Options=4 | OK |
| `/api/ReportLookups/exportimportincoterms` | `Get` | 5 | Options=22 | OK |
| `/api/ReportLookups/sakhans` | `Get` | 5 | Options=21 | OK |
| `/api/ReportLookups/nrcprefixcodes` | `Get` | 4 | Options=6 | OK |
| `/api/ReportLookups/pathakatypes` | `Get` | 4 | Options=7 | OK |

## Full Results

| Endpoint | Action | Elapsed ms | Result | Status |
| --- | ---: | ---: | --- | --- |
| `/api/AccountSummaryReport` | `Post` | 615 | TotalCount=0 | OK |
| `/api/AccountSummaryReport/Excel` | `Excel` | 121 | Bytes=2199 | OK |
| `/api/BorderExportLicenceActualAmendmentReport` | `Post` | 89 | TotalCount=0 | OK |
| `/api/BorderExportLicenceActualAmendmentReport/Excel` | `Excel` | 33 | Bytes=2249 | OK |
| `/api/BorderExportLicenceAmendmentReport` | `Post` | 51 | TotalCount=0 | OK |
| `/api/BorderExportLicenceAmendmentReport/Excel` | `Excel` | 31 | Bytes=2247 | OK |
| `/api/BorderExportLicenceByHSCodeReport` | `Post` | 26 | TotalCount=0 | OK |
| `/api/BorderExportLicenceByHSCodeReport/Excel` | `Excel` | 17 | Bytes=2171 | OK |
| `/api/BorderExportLicenceByMethodReport` | `Post` | 248 | TotalCount=0 | OK |
| `/api/BorderExportLicenceByMethodReport/Excel` | `Excel` | 51 | Bytes=2467 | OK |
| `/api/BorderExportLicenceBySectionReport` | `Post` | 7 | TotalCount=0 | OK |
| `/api/BorderExportLicenceBySectionReport/Excel` | `Excel` | 15 | Bytes=2467 | OK |
| `/api/BorderExportLicenceBySellerCountryReport` | `Post` | 17 | TotalCount=0 | OK |
| `/api/BorderExportLicenceBySellerCountryReport/Excel` | `Excel` | 8 | Bytes=2467 | OK |
| `/api/BorderExportLicenceCancellationReport` | `Post` | 51 | TotalCount=0 | OK |
| `/api/BorderExportLicenceCancellationReport/Excel` | `Excel` | 30 | Bytes=2257 | OK |
| `/api/BorderExportLicenceCompanyListReport` | `Post` | 7 | TotalCount=0 | OK |
| `/api/BorderExportLicenceCompanyListReport/Excel` | `Excel` | 7 | Bytes=2467 | OK |
| `/api/BorderExportLicenceDailyReportNewLicenceReport` | `Post` | 6 | TotalCount=0 | OK |
| `/api/BorderExportLicenceDailyReportNewLicenceReport/Excel` | `Excel` | 11 | Bytes=2468 | OK |
| `/api/BorderExportLicenceDetailReport` | `Post` | 8 | TotalCount=0 | OK |
| `/api/BorderExportLicenceDetailReport/Excel` | `Excel` | 8 | Bytes=2467 | OK |
| `/api/BorderExportLicenceExtensionReport` | `Post` | 53 | TotalCount=0 | OK |
| `/api/BorderExportLicenceExtensionReport/Excel` | `Excel` | 25 | Bytes=2246 | OK |
| `/api/BorderExportLicenceNewReportNewReport` | `Post` | 49 | TotalCount=0 | OK |
| `/api/BorderExportLicenceNewReportNewReport/Excel` | `Excel` | 30 | Bytes=2274 | OK |
| `/api/BorderExportLicenceTotalValueLicencesReport` | `Post` | 8 | TotalCount=0 | OK |
| `/api/BorderExportLicenceTotalValueLicencesReport/Excel` | `Excel` | 8 | Bytes=2466 | OK |
| `/api/BorderExportLicenceVoucherReport` | `Post` | 69 | TotalCount=0 | OK |
| `/api/BorderExportLicenceVoucherReport/Excel` | `Excel` | 32 | Bytes=2290 | OK |
| `/api/BorderExportPermitActualAmendmentReport` | `Post` | 32 | TotalCount=0 | OK |
| `/api/BorderExportPermitActualAmendmentReport/Excel` | `Excel` | 17 | Bytes=2250 | OK |
| `/api/BorderExportPermitAmendmentReport` | `Post` | 27 | TotalCount=0 | OK |
| `/api/BorderExportPermitAmendmentReport/Excel` | `Excel` | 19 | Bytes=2247 | OK |
| `/api/BorderExportPermitByHSCodeReport` | `Post` | 16 | TotalCount=0 | OK |
| `/api/BorderExportPermitByHSCodeReport/Excel` | `Excel` | 12 | Bytes=2171 | OK |
| `/api/BorderExportPermitBySectionReport` | `Post` | 45 | TotalCount=0 | OK |
| `/api/BorderExportPermitBySectionReport/Excel` | `Excel` | 30 | Bytes=2456 | OK |
| `/api/BorderExportPermitBySellerCountryReport` | `Post` | 6 | TotalCount=0 | OK |
| `/api/BorderExportPermitBySellerCountryReport/Excel` | `Excel` | 6 | Bytes=2455 | OK |
| `/api/BorderExportPermitCancellationReport` | `Post` | 27 | TotalCount=0 | OK |
| `/api/BorderExportPermitCancellationReport/Excel` | `Excel` | 16 | Bytes=2257 | OK |
| `/api/BorderExportPermitCompanyListReport` | `Post` | 6 | TotalCount=0 | OK |
| `/api/BorderExportPermitCompanyListReport/Excel` | `Excel` | 6 | Bytes=2457 | OK |
| `/api/BorderExportPermitDailyReportNewPermitReport` | `Post` | 5 | TotalCount=0 | OK |
| `/api/BorderExportPermitDailyReportNewPermitReport/Excel` | `Excel` | 5 | Bytes=2457 | OK |
| `/api/BorderExportPermitDetailReport` | `Post` | 6 | TotalCount=0 | OK |
| `/api/BorderExportPermitDetailReport/Excel` | `Excel` | 6 | Bytes=2456 | OK |
| `/api/BorderExportPermitExtensionReport` | `Post` | 28 | TotalCount=0 | OK |
| `/api/BorderExportPermitExtensionReport/Excel` | `Excel` | 23 | Bytes=2247 | OK |
| `/api/BorderExportPermitNewReportNewReport` | `Post` | 24 | TotalCount=0 | OK |
| `/api/BorderExportPermitNewReportNewReport/Excel` | `Excel` | 22 | Bytes=2273 | OK |
| `/api/BorderExportPermitVoucherReport` | `Post` | 39 | TotalCount=0 | OK |
| `/api/BorderExportPermitVoucherReport/Excel` | `Excel` | 25 | Bytes=2290 | OK |
| `/api/BorderImportLicenceActualAmendmentReport` | `Post` | 59 | TotalCount=0 | OK |
| `/api/BorderImportLicenceActualAmendmentReport/Excel` | `Excel` | 32 | Bytes=2249 | OK |
| `/api/BorderImportLicenceAmendmentReport` | `Post` | 54 | TotalCount=0 | OK |
| `/api/BorderImportLicenceAmendmentReport/Excel` | `Excel` | 31 | Bytes=2246 | OK |
| `/api/BorderImportLicenceByHSCodeReport` | `Post` | 28 | TotalCount=0 | OK |
| `/api/BorderImportLicenceByHSCodeReport/Excel` | `Excel` | 19 | Bytes=2170 | OK |
| `/api/BorderImportLicenceByMethodReport` | `Post` | 71 | TotalCount=0 | OK |
| `/api/BorderImportLicenceByMethodReport/Excel` | `Excel` | 44 | Bytes=2483 | OK |
| `/api/BorderImportLicenceBySectionReport` | `Post` | 6 | TotalCount=0 | OK |
| `/api/BorderImportLicenceBySectionReport/Excel` | `Excel` | 11 | Bytes=2483 | OK |
| `/api/BorderImportLicenceBySellerCountryReport` | `Post` | 5 | TotalCount=0 | OK |
| `/api/BorderImportLicenceBySellerCountryReport/Excel` | `Excel` | 8 | Bytes=2483 | OK |
| `/api/BorderImportLicenceCancellationReport` | `Post` | 49 | TotalCount=0 | OK |
| `/api/BorderImportLicenceCancellationReport/Excel` | `Excel` | 32 | Bytes=2256 | OK |
| `/api/BorderImportLicenceCompanyListReport` | `Post` | 6 | TotalCount=0 | OK |
| `/api/BorderImportLicenceCompanyListReport/Excel` | `Excel` | 5 | Bytes=2483 | OK |
| `/api/BorderImportLicenceDailyReportNewLicenceReport` | `Post` | 5 | TotalCount=0 | OK |
| `/api/BorderImportLicenceDailyReportNewLicenceReport/Excel` | `Excel` | 6 | Bytes=2484 | OK |
| `/api/BorderImportLicenceDetailReport` | `Post` | 5 | TotalCount=0 | OK |
| `/api/BorderImportLicenceDetailReport/Excel` | `Excel` | 9 | Bytes=2482 | OK |
| `/api/BorderImportLicenceDetailReportPending` | `Post` | 68 | TotalCount=0 | OK |
| `/api/BorderImportLicenceDetailReportPending/Excel` | `Excel` | 42 | Bytes=2474 | OK |
| `/api/BorderImportLicenceExtensionReport` | `Post` | 55 | TotalCount=0 | OK |
| `/api/BorderImportLicenceExtensionReport/Excel` | `Excel` | 36 | Bytes=2247 | OK |
| `/api/BorderImportLicenceNewReportNewReport` | `Post` | 49 | TotalCount=0 | OK |
| `/api/BorderImportLicenceNewReportNewReport/Excel` | `Excel` | 29 | Bytes=2273 | OK |
| `/api/BorderImportLicencePendingReport` | `Post` | 65 | TotalCount=0 | OK |
| `/api/BorderImportLicencePendingReport/Excel` | `Excel` | 36 | Bytes=2197 | OK |
| `/api/BorderImportLicenceTotalValueLicencesReport` | `Post` | 7 | TotalCount=0 | OK |
| `/api/BorderImportLicenceTotalValueLicencesReport/Excel` | `Excel` | 6 | Bytes=2482 | OK |
| `/api/BorderImportLicenceVoucherReport` | `Post` | 59 | TotalCount=0 | OK |
| `/api/BorderImportLicenceVoucherReport/Excel` | `Excel` | 36 | Bytes=2290 | OK |
| `/api/BorderImportPermitActualAmendmentReport` | `Post` | 32 | TotalCount=0 | OK |
| `/api/BorderImportPermitActualAmendmentReport/Excel` | `Excel` | 19 | Bytes=2249 | OK |
| `/api/BorderImportPermitAmendmentReport` | `Post` | 26 | TotalCount=0 | OK |
| `/api/BorderImportPermitAmendmentReport/Excel` | `Excel` | 19 | Bytes=2247 | OK |
| `/api/BorderImportPermitByHSCodeReport` | `Post` | 16 | TotalCount=0 | OK |
| `/api/BorderImportPermitByHSCodeReport/Excel` | `Excel` | 12 | Bytes=2170 | OK |
| `/api/BorderImportPermitBySectionReport` | `Post` | 44 | TotalCount=0 | OK |
| `/api/BorderImportPermitBySectionReport/Excel` | `Excel` | 27 | Bytes=2443 | OK |
| `/api/BorderImportPermitBySellerCountryReport` | `Post` | 4 | TotalCount=0 | OK |
| `/api/BorderImportPermitBySellerCountryReport/Excel` | `Excel` | 4 | Bytes=2443 | OK |
| `/api/BorderImportPermitCancellationReport` | `Post` | 26 | TotalCount=0 | OK |
| `/api/BorderImportPermitCancellationReport/Excel` | `Excel` | 19 | Bytes=2256 | OK |
| `/api/BorderImportPermitCompanyListReport` | `Post` | 4 | TotalCount=0 | OK |
| `/api/BorderImportPermitCompanyListReport/Excel` | `Excel` | 4 | Bytes=2445 | OK |
| `/api/BorderImportPermitDailyReportNewPermitReport` | `Post` | 3 | TotalCount=0 | OK |
| `/api/BorderImportPermitDailyReportNewPermitReport/Excel` | `Excel` | 4 | Bytes=2444 | OK |
| `/api/BorderImportPermitDetailReport` | `Post` | 3 | TotalCount=0 | OK |
| `/api/BorderImportPermitDetailReport/Excel` | `Excel` | 4 | Bytes=2443 | OK |
| `/api/BorderImportPermitExtensionReport` | `Post` | 30 | TotalCount=0 | OK |
| `/api/BorderImportPermitExtensionReport/Excel` | `Excel` | 20 | Bytes=2248 | OK |
| `/api/BorderImportPermitNewReportNewReport` | `Post` | 24 | TotalCount=0 | OK |
| `/api/BorderImportPermitNewReportNewReport/Excel` | `Excel` | 23 | Bytes=2273 | OK |
| `/api/BorderImportPermitVoucherReport` | `Post` | 37 | TotalCount=0 | OK |
| `/api/BorderImportPermitVoucherReport/Excel` | `Excel` | 21 | Bytes=2290 | OK |
| `/api/CardListsByCompanyRegistrationNumber` | `Post` | 12 | TotalCount=0 | OK |
| `/api/CardListsByCompanyRegistrationNumber/Excel` | `Excel` | 7 | Bytes=2225 | OK |
| `/api/ChequeNoReport` | `Post` | 17 | TotalCount=0 | OK |
| `/api/ChequeNoReport/Excel` | `Excel` | 15 | Bytes=2098 | OK |
| `/api/CompanyProfile` | `Post` | 46 | TotalCount=3 | OK |
| `/api/CompanyProfile/Excel` | `Excel` | 14 | Bytes=2634 | OK |
| `/api/EIRCardBindReport` | `Post` | 15 | TotalCount=0 | OK |
| `/api/EIRCardBindReport/Excel` | `Excel` | 10 | Bytes=2151 | OK |
| `/api/ExportLicenceActualAmendmentReport` | `Post` | 26 | TotalCount=0 | OK |
| `/api/ExportLicenceActualAmendmentReport/Excel` | `Excel` | 20 | Bytes=2248 | OK |
| `/api/ExportLicenceAmendmentReport` | `Post` | 25 | TotalCount=0 | OK |
| `/api/ExportLicenceAmendmentReport/Excel` | `Excel` | 19 | Bytes=2248 | OK |
| `/api/ExportLicenceByHSCodeReport` | `Post` | 18 | TotalCount=0 | OK |
| `/api/ExportLicenceByHSCodeReport/Excel` | `Excel` | 16 | Bytes=2171 | OK |
| `/api/ExportLicenceByMethodReport` | `Post` | 44 | TotalCount=0 | OK |
| `/api/ExportLicenceByMethodReport/Excel` | `Excel` | 14 | Bytes=2467 | OK |
| `/api/ExportLicenceBySectionReport` | `Post` | 4 | TotalCount=0 | OK |
| `/api/ExportLicenceBySectionReport/Excel` | `Excel` | 5 | Bytes=2467 | OK |
| `/api/ExportLicenceBySellerCountryReport` | `Post` | 4 | TotalCount=0 | OK |
| `/api/ExportLicenceBySellerCountryReport/Excel` | `Excel` | 5 | Bytes=2468 | OK |
| `/api/ExportLicenceCancellationReport` | `Post` | 26 | TotalCount=0 | OK |
| `/api/ExportLicenceCancellationReport/Excel` | `Excel` | 17 | Bytes=2256 | OK |
| `/api/ExportLicenceCompanyListReport` | `Post` | 5 | TotalCount=0 | OK |
| `/api/ExportLicenceCompanyListReport/Excel` | `Excel` | 15 | Bytes=2466 | OK |
| `/api/ExportLicenceDailyReportNewLicenceReport` | `Post` | 6 | TotalCount=0 | OK |
| `/api/ExportLicenceDailyReportNewLicenceReport/Excel` | `Excel` | 16 | Bytes=2467 | OK |
| `/api/ExportLicenceDetailReport` | `Post` | 6 | TotalCount=0 | OK |
| `/api/ExportLicenceDetailReport/Excel` | `Excel` | 5 | Bytes=2462 | OK |
| `/api/ExportLicenceExtensionReport` | `Post` | 28 | TotalCount=0 | OK |
| `/api/ExportLicenceExtensionReport/Excel` | `Excel` | 18 | Bytes=2246 | OK |
| `/api/ExportLicenceNewReportNewReport` | `Post` | 30 | TotalCount=0 | OK |
| `/api/ExportLicenceNewReportNewReport/Excel` | `Excel` | 19 | Bytes=2274 | OK |
| `/api/ExportLicenceTotalValueLicencesReport` | `Post` | 6 | TotalCount=0 | OK |
| `/api/ExportLicenceTotalValueLicencesReport/Excel` | `Excel` | 6 | Bytes=2470 | OK |
| `/api/ExportLicenceVoucherReport` | `Post` | 40 | TotalCount=0 | OK |
| `/api/ExportLicenceVoucherReport/Excel` | `Excel` | 20 | Bytes=2284 | OK |
| `/api/ExportPermitActualAmendmentReport` | `Post` | 28 | TotalCount=0 | OK |
| `/api/ExportPermitActualAmendmentReport/Excel` | `Excel` | 17 | Bytes=2249 | OK |
| `/api/ExportPermitAmendmentReport` | `Post` | 26 | TotalCount=0 | OK |
| `/api/ExportPermitAmendmentReport/Excel` | `Excel` | 20 | Bytes=2246 | OK |
| `/api/ExportPermitByHSCodeReport` | `Post` | 17 | TotalCount=0 | OK |
| `/api/ExportPermitByHSCodeReport/Excel` | `Excel` | 12 | Bytes=2171 | OK |
| `/api/ExportPermitBySectionReport` | `Post` | 43 | TotalCount=0 | OK |
| `/api/ExportPermitBySectionReport/Excel` | `Excel` | 27 | Bytes=2455 | OK |
| `/api/ExportPermitBySellerCountryReport` | `Post` | 6 | TotalCount=0 | OK |
| `/api/ExportPermitBySellerCountryReport/Excel` | `Excel` | 7 | Bytes=2457 | OK |
| `/api/ExportPermitCancellationReport` | `Post` | 24 | TotalCount=0 | OK |
| `/api/ExportPermitCancellationReport/Excel` | `Excel` | 17 | Bytes=2256 | OK |
| `/api/ExportPermitCompanyListReport` | `Post` | 6 | TotalCount=0 | OK |
| `/api/ExportPermitCompanyListReport/Excel` | `Excel` | 7 | Bytes=2457 | OK |
| `/api/ExportPermitDailyReportNewPermitReport` | `Post` | 6 | TotalCount=0 | OK |
| `/api/ExportPermitDailyReportNewPermitReport/Excel` | `Excel` | 6 | Bytes=2456 | OK |
| `/api/ExportPermitDetailReport` | `Post` | 5 | TotalCount=0 | OK |
| `/api/ExportPermitDetailReport/Excel` | `Excel` | 8 | Bytes=2450 | OK |
| `/api/ExportPermitExtensionReport` | `Post` | 29 | TotalCount=0 | OK |
| `/api/ExportPermitExtensionReport/Excel` | `Excel` | 20 | Bytes=2244 | OK |
| `/api/ExportPermitNewReportNewReport` | `Post` | 26 | TotalCount=0 | OK |
| `/api/ExportPermitNewReportNewReport/Excel` | `Excel` | 20 | Bytes=2273 | OK |
| `/api/ExportPermitVoucherReport` | `Post` | 40 | TotalCount=0 | OK |
| `/api/ExportPermitVoucherReport/Excel` | `Excel` | 22 | Bytes=2286 | OK |
| `/api/ImportLicenceActualAmendmentReport` | `Post` | 30 | TotalCount=0 | OK |
| `/api/ImportLicenceActualAmendmentReport/Excel` | `Excel` | 21 | Bytes=2248 | OK |
| `/api/ImportLicenceAmendmentReport` | `Post` | 26 | TotalCount=0 | OK |
| `/api/ImportLicenceAmendmentReport/Excel` | `Excel` | 17 | Bytes=2247 | OK |
| `/api/ImportLicenceByHSCodeReport` | `Post` | 15 | TotalCount=0 | OK |
| `/api/ImportLicenceByHSCodeReport/Excel` | `Excel` | 12 | Bytes=2171 | OK |
| `/api/ImportLicenceByMethodReport` | `Post` | 36 | TotalCount=0 | OK |
| `/api/ImportLicenceByMethodReport/Excel` | `Excel` | 25 | Bytes=2483 | OK |
| `/api/ImportLicenceBySectionReport` | `Post` | 5 | TotalCount=0 | OK |
| `/api/ImportLicenceBySectionReport/Excel` | `Excel` | 9 | Bytes=2483 | OK |
| `/api/ImportLicenceBySellerCountryReport` | `Post` | 7 | TotalCount=0 | OK |
| `/api/ImportLicenceBySellerCountryReport/Excel` | `Excel` | 4 | Bytes=2483 | OK |
| `/api/ImportLicenceCancellationReport` | `Post` | 24 | TotalCount=0 | OK |
| `/api/ImportLicenceCancellationReport/Excel` | `Excel` | 17 | Bytes=2255 | OK |
| `/api/ImportLicenceCompanyListReport` | `Post` | 4 | TotalCount=0 | OK |
| `/api/ImportLicenceCompanyListReport/Excel` | `Excel` | 5 | Bytes=2482 | OK |
| `/api/ImportLicenceDailyReportNewLicenceReport` | `Post` | 4 | TotalCount=0 | OK |
| `/api/ImportLicenceDailyReportNewLicenceReport/Excel` | `Excel` | 4 | Bytes=2483 | OK |
| `/api/ImportLicenceDetailReport` | `Post` | 3 | TotalCount=0 | OK |
| `/api/ImportLicenceDetailReport/Excel` | `Excel` | 4 | Bytes=2478 | OK |
| `/api/ImportLicenceDetailReportPending` | `Post` | 29 | TotalCount=0 | OK |
| `/api/ImportLicenceDetailReportPending/Excel` | `Excel` | 26 | Bytes=2474 | OK |
| `/api/ImportLicenceExtensionReport` | `Post` | 28 | TotalCount=0 | OK |
| `/api/ImportLicenceExtensionReport/Excel` | `Excel` | 21 | Bytes=2246 | OK |
| `/api/ImportLicenceNewReportNewReport` | `Post` | 28 | TotalCount=0 | OK |
| `/api/ImportLicenceNewReportNewReport/Excel` | `Excel` | 19 | Bytes=2273 | OK |
| `/api/ImportLicencePendingReport` | `Post` | 34 | TotalCount=0 | OK |
| `/api/ImportLicencePendingReport/Excel` | `Excel` | 40 | Bytes=2192 | OK |
| `/api/ImportLicenceTotalValueLicencesReport` | `Post` | 5 | TotalCount=0 | OK |
| `/api/ImportLicenceTotalValueLicencesReport/Excel` | `Excel` | 4 | Bytes=2486 | OK |
| `/api/ImportLicenceVoucherReport` | `Post` | 39 | TotalCount=0 | OK |
| `/api/ImportLicenceVoucherReport/Excel` | `Excel` | 21 | Bytes=2284 | OK |
| `/api/ImportPermitActualAmendmentReport` | `Post` | 29 | TotalCount=0 | OK |
| `/api/ImportPermitActualAmendmentReport/Excel` | `Excel` | 19 | Bytes=2249 | OK |
| `/api/ImportPermitAmendmentReport` | `Post` | 28 | TotalCount=0 | OK |
| `/api/ImportPermitAmendmentReport/Excel` | `Excel` | 18 | Bytes=2245 | OK |
| `/api/ImportPermitByHSCodeReport` | `Post` | 17 | TotalCount=0 | OK |
| `/api/ImportPermitByHSCodeReport/Excel` | `Excel` | 14 | Bytes=2170 | OK |
| `/api/ImportPermitBySectionReport` | `Post` | 35 | TotalCount=0 | OK |
| `/api/ImportPermitBySectionReport/Excel` | `Excel` | 27 | Bytes=2443 | OK |
| `/api/ImportPermitBySellerCountryReport` | `Post` | 6 | TotalCount=0 | OK |
| `/api/ImportPermitBySellerCountryReport/Excel` | `Excel` | 6 | Bytes=2444 | OK |
| `/api/ImportPermitCancellationReport` | `Post` | 25 | TotalCount=0 | OK |
| `/api/ImportPermitCancellationReport/Excel` | `Excel` | 20 | Bytes=2256 | OK |
| `/api/ImportPermitCompanyListReport` | `Post` | 7 | TotalCount=0 | OK |
| `/api/ImportPermitCompanyListReport/Excel` | `Excel` | 6 | Bytes=2445 | OK |
| `/api/ImportPermitDailyReportNewPermitReport` | `Post` | 5 | TotalCount=0 | OK |
| `/api/ImportPermitDailyReportNewPermitReport/Excel` | `Excel` | 6 | Bytes=2444 | OK |
| `/api/ImportPermitDetailReport` | `Post` | 4 | TotalCount=0 | OK |
| `/api/ImportPermitDetailReport/Excel` | `Excel` | 5 | Bytes=2438 | OK |
| `/api/ImportPermitExtensionReport` | `Post` | 29 | TotalCount=0 | OK |
| `/api/ImportPermitExtensionReport/Excel` | `Excel` | 21 | Bytes=2244 | OK |
| `/api/ImportPermitNewReportNewReport` | `Post` | 30 | TotalCount=0 | OK |
| `/api/ImportPermitNewReportNewReport/Excel` | `Excel` | 24 | Bytes=2272 | OK |
| `/api/ImportPermitVoucherReport` | `Post` | 44 | TotalCount=0 | OK |
| `/api/ImportPermitVoucherReport/Excel` | `Excel` | 24 | Bytes=2285 | OK |
| `/api/ListOfCompany` | `Post` | 30 | TotalCount=1 | OK |
| `/api/ListOfCompany/Excel` | `Excel` | 16 | Bytes=2588 | OK |
| `/api/ListOfDirectors` | `Post` | 5 | TotalCount=3 | OK |
| `/api/ListOfDirectors/Excel` | `Excel` | 7 | Bytes=2696 | OK |
| `/api/ListOfDirectorsByCompanyRegistrationNo` | `Post` | 30 | TotalCount=3 | OK |
| `/api/ListOfDirectorsByCompanyRegistrationNo/Excel` | `Excel` | 20 | Bytes=2707 | OK |
| `/api/ListOfTopCapitalCompany` | `Post` | 14 | TotalCount=1 | OK |
| `/api/ListOfTopCapitalCompany/Excel` | `Excel` | 13 | Bytes=2378 | OK |
| `/api/ListOfValidAndInvalidCompany` | `Post` | 12 | TotalCount=1 | OK |
| `/api/ListOfValidAndInvalidCompany/Excel` | `Excel` | 11 | Bytes=2356 | OK |
| `/api/MPUReport` | `Post` | 24 | TotalCount=0 | OK |
| `/api/MPUReport/Excel` | `Excel` | 20 | Bytes=2223 | OK |
| `/api/MPUReportV3` | `Post` | 23 | TotalCount=0 | OK |
| `/api/MPUReportV3/Excel` | `Excel` | 17 | Bytes=2247 | OK |
| `/api/MemberRegistrationReport` | `Post` | 32 | TotalCount=11 | OK |
| `/api/MemberRegistrationReport/Excel` | `Excel` | 14 | Bytes=9701 | OK |
| `/api/OnlineFeesReport` | `Post` | 120 | TotalCount=0 | OK |
| `/api/OnlineFeesReport/Excel` | `Excel` | 106 | Bytes=2138 | OK |
| `/api/PaThaKaRegisteredBusinessOrganizationReport` | `Post` | 3 | TotalCount=1 | OK |
| `/api/PaThaKaRegisteredBusinessOrganizationReport/Excel` | `Excel` | 5 | Bytes=2382 | OK |
| `/api/RegistrationByBusinessType` | `Post` | 10 | TotalCount=1 | OK |
| `/api/RegistrationByBusinessType/Excel` | `Excel` | 10 | Bytes=2109 | OK |
| `/api/RegistrationByVoucher` | `Post` | 16 | TotalCount=0 | OK |
| `/api/RegistrationByVoucher/Excel` | `Excel` | 11 | Bytes=2224 | OK |
| `/api/ReportLookups/amendremarks` | `Get` | 7 | Options=3 | OK |
| `/api/ReportLookups/businesstypes` | `Get` | 5 | Options=16 | OK |
| `/api/ReportLookups/chequenos` | `Get` | 5 | Options=4 | OK |
| `/api/ReportLookups/countries` | `Get` | 6 | Options=251 | OK |
| `/api/ReportLookups/exportimportincoterms` | `Get` | 5 | Options=22 | OK |
| `/api/ReportLookups/exportimportmethods` | `Get` | 6 | Options=35 | OK |
| `/api/ReportLookups/exportimportsections` | `Get` | 6 | Options=7 | OK |
| `/api/ReportLookups/lineofbusinesses` | `Get` | 6 | Options=11 | OK |
| `/api/ReportLookups/nrcprefixcodes` | `Get` | 4 | Options=6 | OK |
| `/api/ReportLookups/nrcprefixes` | `Get` | 7 | Options=418 | OK |
| `/api/ReportLookups/pathakatypes` | `Get` | 4 | Options=7 | OK |
| `/api/ReportLookups/sakhans` | `Get` | 5 | Options=21 | OK |

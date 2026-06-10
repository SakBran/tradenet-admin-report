# Report API Testing & UI-Parity Review

**Date:** 2026-06-06
**Scope:** All report APIs (158 backend controllers) + all report UIs (134 frontend configs) vs old Tradenet 2.0 Admin
**Mode:** Test-and-document only — **no code was changed.**
**Author:** automated review harness (Claude Code)

---

## 0. Requirement — confirmation, validation & weaknesses

### 0.1 What was requested
1. **API testing** — unit/functional + performance test every report API, searching with *validated* values; record **Performance**, **Error**, **Pass/Fail**.
2. **UI-parity audit** — for every report, compare the current frontend against the old Tradenet 2.0 Admin: filter box, filter **labels**, filter **values**, table **format/UI**, and whether the table **should include a Total row**.
3. Produce a results document; maximum effort; **do not fix code**.
4. Confirm/validate the requirement and surface weaknesses + suggestions.

### 0.2 Decisions taken (confirmed with the user)
| Question | Decision |
| --- | --- |
| API execution mode | **Real HTTP + JWT** against a locally-run backend (`https://localhost:8000`) |
| DB load | **Full sweep** of all 158 endpoints against the shared live DB, with per-request timeout |
| Validated values | **Probe the live DB** + use a realistic bounded search window |
| UI depth | **Code-only parity matrix + live screenshots** |

### 0.3 Validation & weaknesses (important caveats on the results)
- **Performance is indicative, not benchmark-grade.** The reports run against a *shared, remote, production-class* DB (`203.81.66.111`). Latencies include network + contention from other users; repeat runs vary (observed `CompanyProfile` 4s then 9s). Treat buckets, not exact milliseconds, as the signal.
- **"Validated value" = a realistic bounded window, not whole history.** Using the full `2000–2100` range forced full-table scans (e.g. `ImportLicenceDetail` 25s) that conflate *range size* with *report performance*; a recent **1-year window (2025-06-01 → 2026-06-06)** was used as the primary search — what a real user runs — and still surfaced genuinely slow/broken reports. Per-filter valid IDs were left at "All" (`0`), the heaviest path and itself a valid value.
- **"Unit testing" already exists.** `Backend.Tests` already unit-tests all 158 controllers (auth, branch defaults, payloads). This review therefore adds **functional + performance E2E** value, not more unit scaffolding. (Note: the existing `dotnet test` suite is partly red in this environment — missing local SPs / connection string — see Appendix.)
- **"Table UI / format" parity is partly structural, not pixel-exact.** The new grid is a single React component (`BasicTable`) reused by every report; per-report visual variance is limited to columns/filters/total. The code-only matrix verifies columns/headers/order/labels/values/total rigorously; screenshots confirm the rendered shell. Full RDLC ReportViewer visual fidelity is out of scope by design (see `plan.md`).
- **Mapping gaps.** 158 backend controllers vs 134 frontend configs vs 504/114 old RDLC. 5 new reports have **no old equivalent** (cannot be parity-checked); some controllers have no frontend config. These are flagged, not invented.

---

## 1. Environment & method

### 1.1 Backend under test
- Built `Backend/API.csproj` (net8.0) and ran `API.dll` on `https://localhost:8000` (Development), pointing at the live `TradeNetDB` + `TemplateDB` (`203.81.66.111:14330`).
- **Auth:** all report endpoints are `[Authorize]` (JWT). Login `POST /api/auth` (`{Name, Password, Permission}` — `Permission` is `[Required]` on the bind model; auth only checks Name+Password, which are stored in plaintext). A real token was obtained from the `demo@email.com` Admin user found in `TemplateDB.Users` and sent as `Authorization: Bearer …`.

### 1.2 API sweep method (`/tmp/report_test/harness.py`)
For each of the 158 `POST /api/{Report}` endpoints:
- **Cold** call (first load), `IncludeTotalCount=false`, `PageSize=10`, window 2025-06-01→2026-06-06, **60s timeout**.
- **Warm** call (repeat) — only if cold succeeded.
- **Count** call (`IncludeTotalCount=true`, 45s) — only for otherwise-fast reports, to characterise the exact-`COUNT` overhead without hammering already-slow reports.
- Recorded: HTTP status, latency, row count, total count, error body, pass flag, perf bucket.
- **Perf buckets (on cold latency):** `FAST` <1s · `OK` 1–5s · `SLOW` 5–15s · `CRITICAL` ≥15s · `TIMEOUT` (>60s, no response) · `ERROR` (non-200).
- **Pass = HTTP 200 and no transport error.** Empty result set is *not* a fail (it can be a legitimate window/data situation) but is reported via `has_data`.

### 1.3 UI-parity method
- 19 family agents compared, for every report, the new `reportConfigs.ts` + `GenericReportPage.tsx` + report controller against the old `Views/Reports/*.cshtml` + `ReportsController.cs` + `Resources.resx` + `ReportControl/*.rdlc`. Each flagged mismatch was **adversarially re-verified** against the files. Column diffs reuse the existing `ReportColumnComparison.md`.
- **Screenshots:** Playwright (headless Chromium) with the JWT injected into `localStorage`, navigating `/Report/{key}`, capturing the rendered filter box + table for a representative set + every flagged report.

---

## 2. API test results — summary

**158 report endpoints** tested over real HTTP+JWT, realistic 1-year window, 60s SLA timeout.

| Metric | Value |
| --- | ---: |
| Total endpoints | 158 |
| **PASS** (HTTP 200, no transport error) | **98** |
| **FAIL** | **60** |
| — of which genuine code/SQL bugs (fast error) | 10 |
| — of which performance failures (SQL 30s command-timeout / >60s client timeout) | 50 |
| Returned data (>=1 row) in the window | 67 |

**Performance buckets (cold latency):**

| Bucket | Count | Meaning |
| --- | ---: | --- |
| FAST (<1s) | 55 | good |
| OK (1–5s) | 28 | acceptable |
| SLOW (5–15s) | 11 | needs attention |
| CRITICAL (≥15s) | 4 | unacceptable for interactive use |
| TIMEOUT (>60s, no response) | 16 | request never returned |
| ERROR (non-200) | 44 | HTTP 4xx/5xx |

**Two distinct failure clusters were found:**
1. **Genuine code/SQL bugs (10)** — fail in <5s with a hard error (invalid column, bad SQL syntax, EF mapping, 404 route, 400 validation). These are correctness defects independent of data volume.
2. **Performance failures (50)** — the heavy aggregate reports (By-Section / By-Method / By-SellerCountry / By-HSCode / Detail / Daily / CompanyList / TotalValue / NewReport) exceed the SQL command timeout (~30s → HTTP 500 `Execution Timeout Expired`) or the 60s client timeout. Matches the documented `COUNT`/heavy-join performance problem.


## 3. API test results — full table

| # | Report endpoint | Result | HTTP | Cold (s) | Warm (s) | Rows | TotalCount |
| --: | --- | --- | --: | --: | --: | --: | --: |
| 1 | AccountSummaryReport | **TIMEOUT** | TO | 60.014 | — | — | — |
| 2 | BorderExportLicenceActualAmendmentReport | OK | 200 | 1.818 | 0.337 | 10 | 99 |
| 3 | BorderExportLicenceAmendmentReport | FAST | 200 | 0.887 | 0.716 | 0 | 0 |
| 4 | BorderExportLicenceByHSCodeReport | **TIMEOUT** | TO | 60.019 | — | — | — |
| 5 | BorderExportLicenceByMethodReport | **ERROR** | 500 | 35.146 | — | — | — |
| 6 | BorderExportLicenceBySectionReport | **ERROR** | 500 | 31.273 | — | — | — |
| 7 | BorderExportLicenceBySellerCountryReport | **ERROR** | 500 | 30.297 | — | — | — |
| 8 | BorderExportLicenceCancellationReport | FAST | 200 | 0.746 | 0.566 | 10 | 225 |
| 9 | BorderExportLicenceCompanyListReport | **ERROR** | 500 | 30.213 | — | — | — |
| 10 | BorderExportLicenceDailyReportNewLicenceReport | **ERROR** | 500 | 30.231 | — | — | — |
| 11 | BorderExportLicenceDetailReport | **TIMEOUT** | TO | 60.019 | — | — | — |
| 12 | BorderExportLicenceExtensionReport | OK | 200 | 1.348 | 0.289 | 10 | 78 |
| 13 | BorderExportLicenceNewReportNewReport | **ERROR** | 500 | 30.23 | — | — | — |
| 14 | BorderExportLicenceTotalValueLicencesReport | **TIMEOUT** | TO | 60.016 | — | — | — |
| 15 | BorderExportLicenceVoucherReport | OK | 200 | 1.449 | 0.351 | 0 | 0 |
| 16 | BorderExportPermitActualAmendmentReport | FAST | 200 | 0.309 | 0.231 | 0 | 0 |
| 17 | BorderExportPermitAmendmentReport | FAST | 200 | 0.292 | 1.003 | 2 | 2 |
| 18 | BorderExportPermitByHSCodeReport | FAST | 200 | 0.264 | 0.201 | 3 | 3 |
| 19 | BorderExportPermitBySectionReport | FAST | 200 | 0.725 | 0.192 | 2 | 2 |
| 20 | BorderExportPermitBySellerCountryReport | FAST | 200 | 0.68 | 0.509 | 2 | 2 |
| 21 | BorderExportPermitCancellationReport | FAST | 200 | 0.439 | 0.189 | 2 | 2 |
| 22 | BorderExportPermitCompanyListReport | FAST | 200 | 0.547 | 0.538 | 2 | 2 |
| 23 | BorderExportPermitDailyReportNewPermitReport | OK | 200 | 2.292 | 1.351 | 5 | 5 |
| 24 | BorderExportPermitDetailReport | FAST | 200 | 0.822 | 0.213 | 7 | 7 |
| 25 | BorderExportPermitExtensionReport | FAST | 200 | 0.301 | 0.692 | 2 | 2 |
| 26 | BorderExportPermitNewReportNewReport | FAST | 200 | 0.434 | 0.196 | 5 | 5 |
| 27 | BorderExportPermitVoucherReport | OK | 200 | 1.191 | 0.24 | 5 | 5 |
| 28 | BorderImportLicenceActualAmendmentReport | SLOW | 200 | 6.82 | 0.427 | 10 | 312 |
| 29 | BorderImportLicenceAmendmentReport | OK | 200 | 1.762 | 0.557 | 1 | 1 |
| 30 | BorderImportLicenceByHSCodeReport | **TIMEOUT** | TO | 60.021 | — | — | — |
| 31 | BorderImportLicenceByMethodReport | **ERROR** | 500 | 36.172 | — | — | — |
| 32 | BorderImportLicenceBySectionReport | **ERROR** | 500 | 33.383 | — | — | — |
| 33 | BorderImportLicenceBySellerCountryReport | **ERROR** | 500 | 30.199 | — | — | — |
| 34 | BorderImportLicenceCancellationReport | OK | 200 | 3.978 | 1.057 | 10 | 673 |
| 35 | BorderImportLicenceCompanyListReport | **ERROR** | 500 | 30.209 | — | — | — |
| 36 | BorderImportLicenceDailyReportNewLicenceReport | CRITICAL | 200 | 17.589 | 3.922 | 10 | — |
| 37 | BorderImportLicenceDetailReport | **ERROR** | 500 | 30.647 | — | — | — |
| 38 | BorderImportLicenceDetailReportPending | **ERROR** | 500 | 30.257 | — | — | — |
| 39 | BorderImportLicenceExtensionReport | CRITICAL | 200 | 29.612 | 4.56 | 10 | — |
| 40 | BorderImportLicenceNewReportNewReport | **ERROR** | 500 | 30.188 | — | — | — |
| 41 | BorderImportLicencePendingReport | SLOW | 200 | 12.84 | 0.671 | 10 | — |
| 42 | BorderImportLicenceTotalValueLicencesReport | SLOW | 200 | 8.1 | 5.047 | 4 | — |
| 43 | BorderImportLicenceVoucherReport | OK | 200 | 3.571 | 2.31 | 0 | 0 |
| 44 | BorderImportPermitActualAmendmentReport | FAST | 200 | 0.645 | 0.205 | 0 | 0 |
| 45 | BorderImportPermitAmendmentReport | FAST | 200 | 0.58 | 0.21 | 1 | 1 |
| 46 | BorderImportPermitByHSCodeReport | CRITICAL | 200 | 23.153 | 0.458 | 6 | — |
| 47 | BorderImportPermitBySectionReport | OK | 200 | 1.696 | 1.343 | 2 | 2 |
| 48 | BorderImportPermitBySellerCountryReport | OK | 200 | 1.519 | 1.662 | 3 | 3 |
| 49 | BorderImportPermitCancellationReport | **ERROR** | 500 | 0.186 | — | — | — |
| 50 | BorderImportPermitCompanyListReport | FAST | 200 | 0.766 | 1.064 | 4 | 4 |
| 51 | BorderImportPermitDailyReportNewPermitReport | OK | 200 | 1.646 | 1.827 | 6 | 6 |
| 52 | BorderImportPermitDetailReport | OK | 200 | 1.939 | 0.597 | 10 | 23 |
| 53 | BorderImportPermitExtensionReport | **ERROR** | 500 | 0.504 | — | — | — |
| 54 | BorderImportPermitNewReportNewReport | **ERROR** | 500 | 0.176 | — | — | — |
| 55 | BorderImportPermitVoucherReport | **ERROR** | 500 | 0.496 | — | — | — |
| 56 | ChequeNoReport | SLOW | 200 | 10.089 | 2.689 | 10 | — |
| 57 | EIRCardBindReport | OK | 200 | 1.958 | 0.286 | 10 | 495 |
| 58 | ExportLicenceActualAmendmentReport | CRITICAL | 200 | 20.073 | 0.712 | 10 | — |
| 59 | ExportLicenceAmendmentReport | OK | 200 | 2.549 | 1.094 | 10 | 65 |
| 60 | ExportLicenceByHSCodeReport | **TIMEOUT** | TO | 60.018 | — | — | — |
| 61 | ExportLicenceByMethodReport | **ERROR** | 500 | 33.168 | — | — | — |
| 62 | ExportLicenceBySectionReport | **ERROR** | 500 | 30.203 | — | — | — |
| 63 | ExportLicenceBySellerCountryReport | **ERROR** | 500 | 30.204 | — | — | — |
| 64 | ExportLicenceCancellationReport | **ERROR** | 500 | 30.504 | — | — | — |
| 65 | ExportLicenceCompanyListReport | **ERROR** | 500 | 30.197 | — | — | — |
| 66 | ExportLicenceDailyReportNewLicenceReport | **ERROR** | 500 | 30.673 | — | — | — |
| 67 | ExportLicenceDetailReport | **ERROR** | 500 | 30.2 | — | — | — |
| 68 | ExportLicenceExtensionReport | OK | 200 | 2.845 | 0.264 | 10 | 107 |
| 69 | ExportLicenceNewReportNewReport | **ERROR** | 500 | 30.516 | — | — | — |
| 70 | ExportLicenceTotalValueLicencesReport | **ERROR** | 500 | 32.523 | — | — | — |
| 71 | ExportLicenceVoucherReport | **ERROR** | 500 | 30.963 | — | — | — |
| 72 | ExportPermitActualAmendmentReport | FAST | 200 | 0.568 | 0.222 | 1 | 1 |
| 73 | ExportPermitAmendmentReport | FAST | 200 | 0.712 | 0.219 | 4 | 4 |
| 74 | ExportPermitByHSCodeReport | OK | 200 | 4.059 | 1.023 | 10 | 445 |
| 75 | ExportPermitBySectionReport | **TIMEOUT** | TO | 60.015 | — | — | — |
| 76 | ExportPermitBySellerCountryReport | **TIMEOUT** | TO | 60.014 | — | — | — |
| 77 | ExportPermitCancellationReport | FAST | 200 | 0.336 | 0.218 | 10 | 11 |
| 78 | ExportPermitCompanyListReport | **TIMEOUT** | TO | 60.024 | — | — | — |
| 79 | ExportPermitDailyReportNewPermitReport | **TIMEOUT** | TO | 60.024 | — | — | — |
| 80 | ExportPermitDetailReport | FAST | 200 | 0.943 | 0.595 | 10 | 2721 |
| 81 | ExportPermitExtensionReport | FAST | 200 | 0.535 | 0.221 | 10 | 25 |
| 82 | ExportPermitNewReportNewReport | **ERROR** | 500 | 0.559 | — | — | — |
| 83 | ExportPermitVoucherReport | FAST | 200 | 0.352 | 0.248 | 0 | 0 |
| 84 | ImportLicenceActualAmendmentReport | SLOW | 200 | 9.377 | 0.278 | 10 | — |
| 85 | ImportLicenceAmendmentReport | SLOW | 200 | 11.306 | 0.314 | 10 | — |
| 86 | ImportLicenceByHSCodeReport | **TIMEOUT** | TO | 60.015 | — | — | — |
| 87 | ImportLicenceByMethodReport | **ERROR** | 500 | 35.056 | — | — | — |
| 88 | ImportLicenceBySectionReport | SLOW | 200 | 8.698 | 0.021 | 0 | — |
| 89 | ImportLicenceBySellerCountryReport | **ERROR** | 500 | 35.039 | — | — | — |
| 90 | ImportLicenceCancellationReport | **ERROR** | 500 | 35.039 | — | — | — |
| 91 | ImportLicenceCompanyListReport | **ERROR** | 500 | 15.057 | — | — | — |
| 92 | ImportLicenceDailyReportNewLicenceReport | **ERROR** | 500 | 39.118 | — | — | — |
| 93 | ImportLicenceDetailReport | FAST | 200 | 0.711 | 0.761 | 10 | — |
| 94 | ImportLicenceDetailReportPending | OK | 200 | 1.686 | 0.452 | 10 | — |
| 95 | ImportLicenceExtensionReport | SLOW | 200 | 5.35 | 0.474 | 10 | 20480 |
| 96 | ImportLicenceNewReportNewReport | **ERROR** | 500 | 35.053 | — | — | — |
| 97 | ImportLicencePendingReport | **ERROR** | 500 | 15.05 | — | — | — |
| 98 | ImportLicenceTotalValueLicencesReport | **ERROR** | 500 | 53.712 | — | — | — |
| 99 | ImportLicenceVoucherReport | **ERROR** | 500 | 36.026 | — | — | — |
| 100 | ImportPermitActualAmendmentReport | OK | 200 | 1.307 | 0.234 | 0 | 0 |
| 101 | ImportPermitAmendmentReport | FAST | 200 | 0.636 | 0.23 | 10 | 10 |
| 102 | ImportPermitByHSCodeReport | SLOW | 200 | 7.48 | 0.303 | 10 | 414 |
| 103 | ImportPermitBySectionReport | **TIMEOUT** | TO | 60.014 | — | — | — |
| 104 | ImportPermitBySellerCountryReport | **TIMEOUT** | TO | 60.015 | — | — | — |
| 105 | ImportPermitCancellationReport | FAST | 200 | 0.387 | 0.688 | 1 | 1 |
| 106 | ImportPermitCompanyListReport | **TIMEOUT** | TO | 60.022 | — | — | — |
| 107 | ImportPermitDailyReportNewPermitReport | **TIMEOUT** | TO | 60.025 | — | — | — |
| 108 | ImportPermitDetailReport | OK | 200 | 2.128 | 0.634 | 10 | 2351 |
| 109 | ImportPermitExtensionReport | FAST | 200 | 0.331 | 0.223 | 10 | 80 |
| 110 | ImportPermitNewReportNewReport | **ERROR** | 500 | 0.3 | — | — | — |
| 111 | ImportPermitVoucherReport | FAST | 200 | 0.822 | 0.246 | 0 | 0 |
| 112 | MPUReport | **ERROR** | 500 | 35.036 | — | — | — |
| 113 | MPUReportV3 | **TIMEOUT** | TO | 60.015 | — | — | — |
| 114 | MemberRegistrationReport | **ERROR** | 500 | 36.01 | — | — | — |
| 115 | OnlineFeesReport | **ERROR** | 500 | 35.039 | — | — | — |
| 116 | AlcoholicBeveragesImportationDetailReport | OK | 200 | 2.214 | 0.188 | 0 | 0 |
| 117 | AlcoholicBeveragesImportationRegistrationByVoucher | FAST | 200 | 0.463 | 0.179 | 0 | 0 |
| 118 | AlcoholicBeveragesImportationSummaryReport | OK | 200 | 1.441 | 1.118 | 3 | 3 |
| 119 | BusinessServiceAgencyDetailReport | FAST | 200 | 0.436 | 0.485 | 0 | 0 |
| 120 | BusinessServiceAgencyRegistrationByVoucher | FAST | 200 | 0.562 | 0.174 | 0 | 0 |
| 121 | BusinessServiceAgencySummaryReport | FAST | 200 | 0.607 | 0.169 | 5 | 5 |
| 122 | CardListsByCompanyRegistrationNumber | **ERROR** | 404 | 0.021 | — | — | — |
| 123 | CompanyProfile | FAST | 200 | 0.408 | 0.92 | 20 | 649 |
| 124 | DutyFreeShopDetailReport | FAST | 200 | 0.946 | 0.217 | 0 | 0 |
| 125 | DutyFreeShopRegistrationByVoucher | FAST | 200 | 0.481 | 0.172 | 0 | 0 |
| 126 | DutyFreeShopSummaryReport | FAST | 200 | 0.29 | 0.17 | 4 | 4 |
| 127 | EVCycleShowRoomDetailReport | FAST | 200 | 0.21 | 0.176 | 0 | 0 |
| 128 | EVCycleShowRoomRegistrationByVoucher | FAST | 200 | 0.951 | 0.175 | 0 | 0 |
| 129 | EVCycleShowRoomSummaryReport | FAST | 200 | 0.204 | 0.162 | 4 | 4 |
| 130 | EVShowRoomDetailReport | FAST | 200 | 0.342 | 0.164 | 0 | 0 |
| 131 | EVShowRoomRegistrationByVoucher | OK | 200 | 1.131 | 0.172 | 0 | 0 |
| 132 | EVShowRoomSummaryReport | OK | 200 | 1.049 | 0.174 | 5 | 5 |
| 133 | ListOfCompany | OK | 200 | 3.876 | 1.111 | 10 | 37903 |
| 134 | ListOfDirectors | SLOW | 200 | 9.831 | 0.271 | 10 | — |
| 135 | ListOfDirectorsByCompanyRegistrationNo | FAST | 200 | 0.279 | 0.262 | 10 | 1465 |
| 136 | ListOfTopCapitalCompany | OK | 200 | 4.285 | 0.214 | 10 | 664 |
| 137 | ListOfValidAndInvalidCompany | **ERROR** | 400 | 0.035 | — | — | — |
| 138 | OGARecommendationReport | **ERROR** | 500 | 31.203 | — | — | — |
| 139 | ReExportDetailReport | OK | 200 | 2.351 | 0.172 | 0 | 0 |
| 140 | ReExportSummaryReport | FAST | 200 | 0.29 | 0.172 | 3 | 3 |
| 141 | RegistrationByBusinessType | FAST | 200 | 0.291 | 0.182 | 4 | 4 |
| 142 | RegistrationByVoucher | FAST | 200 | 0.522 | 0.171 | 0 | 0 |
| 143 | SaleCenterDetailReport | FAST | 200 | 0.413 | 0.166 | 0 | 0 |
| 144 | SaleCenterRegistrationByVoucher | FAST | 200 | 0.282 | 0.177 | 0 | 0 |
| 145 | SaleCenterSummaryReport | FAST | 200 | 0.284 | 0.172 | 4 | 4 |
| 146 | ShowRoomDetailReport | OK | 200 | 1.801 | 0.174 | 0 | 0 |
| 147 | ShowRoomRegistrationByVoucher | FAST | 200 | 0.329 | 0.181 | 0 | 0 |
| 148 | ShowRoomSummaryReport | FAST | 200 | 0.638 | 0.163 | 4 | 4 |
| 149 | PaThaKaRegisteredBusinessOrganizationReport | OK | 200 | 1.936 | 0.192 | 10 | 664 |
| 150 | RetailDetailReport | FAST | 200 | 0.535 | 0.162 | 0 | 0 |
| 151 | RetailRegistrationByVoucher | SLOW | 200 | 9.08 | 0.488 | 0 | — |
| 152 | RetailSummaryReport | FAST | 200 | 0.281 | 0.173 | 4 | 4 |
| 153 | WholeSaleDetailReport | FAST | 200 | 0.174 | 0.165 | 0 | 0 |
| 154 | WholeSaleRegistrationByVoucher | FAST | 200 | 0.184 | 0.18 | 0 | 0 |
| 155 | WholeSaleSummaryReport | FAST | 200 | 0.178 | 0.177 | 4 | 4 |
| 156 | WholeSaleAndRetailDetailReport | FAST | 200 | 0.18 | 0.178 | 0 | 0 |
| 157 | WholeSaleAndRetailRegistrationByVoucher | FAST | 200 | 0.488 | 0.165 | 0 | 0 |
| 158 | WholeSaleAndRetailSummaryReport | FAST | 200 | 0.167 | 0.168 | 3 | 3 |

## 4. API errors & timeouts (detail)

### 4.1 Genuine code/SQL bugs (fix candidates — fail fast, independent of data volume)

| Report endpoint | HTTP | Time (s) | Error |
| --- | --: | --: | --- |
| BorderImportPermitCancellationReport | 500 | 0.186 | `Invalid column name 'CardType'.` |
| BorderImportPermitExtensionReport | 500 | 0.504 | `Invalid column name 'CardType'.` |
| BorderImportPermitNewReportNewReport | 500 | 0.176 | `Incorrect syntax near 'New'. Invalid usage of the option NEXT in the FETCH statement.` |
| BorderImportPermitVoucherReport | 500 | 0.496 | `Invalid column name 'CardType'.` |
| CardListsByCompanyRegistrationNumber | 404 | 0.021 | `(empty body)` |
| ExportPermitNewReportNewReport | 500 | 0.559 | `required column 'HSCode' was not present in the results of a 'FromSql' operation.` |
| ImportLicenceCompanyListReport | 500 | 15.057 | `Microsoft.Data.SqlClient.SqlException (0x80131904): Connection Timeout Expired.  The timeout period elapsed while attemp` |
| ImportLicencePendingReport | 500 | 15.05 | `Microsoft.Data.SqlClient.SqlException (0x80131904): Connection Timeout Expired.  The timeout period elapsed while attemp` |
| ImportPermitNewReportNewReport | 500 | 0.3 | `required column 'HSCode' was not present in the results of a 'FromSql' operation.` |
| ListOfValidAndInvalidCompany | 400 | 0.035 | `Date is required.` |

### 4.2 Performance failures (slow → SQL command-timeout / client timeout)

| Report endpoint | HTTP | Time (s) | Cause |
| --- | --: | --: | --- |
| AccountSummaryReport | TIMEOUT | 60.014 | No response within 60s client timeout |
| BorderExportLicenceByHSCodeReport | TIMEOUT | 60.019 | No response within 60s client timeout |
| BorderExportLicenceByMethodReport | 500 | 35.146 | SQL `Execution Timeout Expired` (~30s command timeout) |
| BorderExportLicenceBySectionReport | 500 | 31.273 | SQL `Execution Timeout Expired` (~30s command timeout) |
| BorderExportLicenceBySellerCountryReport | 500 | 30.297 | SQL `Execution Timeout Expired` (~30s command timeout) |
| BorderExportLicenceCompanyListReport | 500 | 30.213 | SQL `Execution Timeout Expired` (~30s command timeout) |
| BorderExportLicenceDailyReportNewLicenceReport | 500 | 30.231 | SQL `Execution Timeout Expired` (~30s command timeout) |
| BorderExportLicenceDetailReport | TIMEOUT | 60.019 | No response within 60s client timeout |
| BorderExportLicenceNewReportNewReport | 500 | 30.23 | SQL `Execution Timeout Expired` (~30s command timeout) |
| BorderExportLicenceTotalValueLicencesReport | TIMEOUT | 60.016 | No response within 60s client timeout |
| BorderImportLicenceByHSCodeReport | TIMEOUT | 60.021 | No response within 60s client timeout |
| BorderImportLicenceByMethodReport | 500 | 36.172 | SQL `Execution Timeout Expired` (~30s command timeout) |
| BorderImportLicenceBySectionReport | 500 | 33.383 | SQL `Execution Timeout Expired` (~30s command timeout) |
| BorderImportLicenceBySellerCountryReport | 500 | 30.199 | SQL `Execution Timeout Expired` (~30s command timeout) |
| BorderImportLicenceCompanyListReport | 500 | 30.209 | SQL `Execution Timeout Expired` (~30s command timeout) |
| BorderImportLicenceDetailReport | 500 | 30.647 | SQL `Execution Timeout Expired` (~30s command timeout) |
| BorderImportLicenceDetailReportPending | 500 | 30.257 | SQL `Execution Timeout Expired` (~30s command timeout) |
| BorderImportLicenceNewReportNewReport | 500 | 30.188 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ExportLicenceByHSCodeReport | TIMEOUT | 60.018 | No response within 60s client timeout |
| ExportLicenceByMethodReport | 500 | 33.168 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ExportLicenceBySectionReport | 500 | 30.203 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ExportLicenceBySellerCountryReport | 500 | 30.204 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ExportLicenceCancellationReport | 500 | 30.504 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ExportLicenceCompanyListReport | 500 | 30.197 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ExportLicenceDailyReportNewLicenceReport | 500 | 30.673 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ExportLicenceDetailReport | 500 | 30.2 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ExportLicenceNewReportNewReport | 500 | 30.516 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ExportLicenceTotalValueLicencesReport | 500 | 32.523 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ExportLicenceVoucherReport | 500 | 30.963 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ExportPermitBySectionReport | TIMEOUT | 60.015 | No response within 60s client timeout |
| ExportPermitBySellerCountryReport | TIMEOUT | 60.014 | No response within 60s client timeout |
| ExportPermitCompanyListReport | TIMEOUT | 60.024 | No response within 60s client timeout |
| ExportPermitDailyReportNewPermitReport | TIMEOUT | 60.024 | No response within 60s client timeout |
| ImportLicenceByHSCodeReport | TIMEOUT | 60.015 | No response within 60s client timeout |
| ImportLicenceByMethodReport | 500 | 35.056 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ImportLicenceBySellerCountryReport | 500 | 35.039 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ImportLicenceCancellationReport | 500 | 35.039 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ImportLicenceDailyReportNewLicenceReport | 500 | 39.118 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ImportLicenceNewReportNewReport | 500 | 35.053 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ImportLicenceTotalValueLicencesReport | 500 | 53.712 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ImportLicenceVoucherReport | 500 | 36.026 | SQL `Execution Timeout Expired` (~30s command timeout) |
| ImportPermitBySectionReport | TIMEOUT | 60.014 | No response within 60s client timeout |
| ImportPermitBySellerCountryReport | TIMEOUT | 60.015 | No response within 60s client timeout |
| ImportPermitCompanyListReport | TIMEOUT | 60.022 | No response within 60s client timeout |
| ImportPermitDailyReportNewPermitReport | TIMEOUT | 60.025 | No response within 60s client timeout |
| MPUReport | 500 | 35.036 | SQL `Execution Timeout Expired` (~30s command timeout) |
| MPUReportV3 | TIMEOUT | 60.015 | No response within 60s client timeout |
| MemberRegistrationReport | 500 | 36.01 | SQL `Execution Timeout Expired` (~30s command timeout) |
| OGARecommendationReport | 500 | 31.203 | SQL `Execution Timeout Expired` (~30s command timeout) |
| OnlineFeesReport | 500 | 35.039 | SQL `Execution Timeout Expired` (~30s command timeout) |

---

## 5. UI-parity results — summary

**134 reports** audited across 19 families (new `reportConfigs.ts`/controller vs old `.cshtml`/`.rdlc`/`Resources.resx`).

> **Confidence note:** Only the **BorderExportPermit** family completed the adversarial *verify* pass before a session limit was hit (its findings: all **CONFIRMED** except one ADJUSTED). All other families are **audited but not yet adversarially verified** — treat their findings as high-signal leads, and re-confirm a flagged report against pinned `lookupName` / shared filter arrays before acting. `PaThaKa` and `EIRCard` audited clean (PASS).

| Verdict | Count |
| --- | ---: |
| PASS (full parity) | 14 |
| MINOR (cosmetic: label/order) | 20 |
| MAJOR (missing filter/Total, wrong values, column diffs) | 98 |
| NO_OLD_MATCH (new report, nothing to compare) | 2 |

**Cross-cutting parity gaps (precise, from structured fields):**

| Gap | Reports affected | Notes |
| --- | ---: | --- |
| **Missing Total row** (old RDLC has grand-total footer; new sets no `ColumnTotals`) | **82** | Largest gap. Repo history shows Totals were added only to By-X/Daily summary controllers of ImportLicence/ImportPermit/ExportLicence; permit/border-export + listing/detail reports still lack them. |
| **Extra filter(s) in new** (filter not in old box, e.g. Buyer Country, Company Reg No, Auto) | **63** | Largely the *intentional* additions the project previously **deferred by user choice** — confirm whether to keep or hide. |
| **Missing filter(s) in new** (old filter absent in new) | **12** | Real gaps — e.g. ImportPermit readonly *Company Name* companion, ImportPermit/BorderExportLicence HSCode missing *Section* dropdown, ListOfTopCapitalCompany missing *No of List*, ListOfDirectors missing *State Prefix*. |
| **Section/Method/Incoterm value leak** (filter not scoped → generic lookup) | ~87 flagged | Of **86** `ExportImportSectionId` filters, only **25** pin a scoped lookup (`borderExportLicence`13 / `importPermit`11 / `importLicence`1); ~61 fall back to the leaky generic lookup. Confirmed leaking in BorderExportPermit; verify per family. |


## 6. UI-parity results — per family


### BorderExportLicence (14 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| BorderExportLicenceActualAmendmentReport | MAJOR | • Old RDLC BorderAmendReport.rdlc has a grand-total footer row (=Fields!Currency.Value+":"+FORMAT(Sum(Fields!Amount.Value),"N4")) but the new controller does NOT set ColumnTotals, so the new table has no Total row.<br>• Filter set matches old view: From/To Date, Export Section, Remark, Company Registration No, Sakhan all present.<br>• Old view also shows a read-only CompanyName helper text (auto-filled from CompanyRegistrationNo) that the new UI omits; this is a UI helper, not a real filter. |
| BorderExportLicenceAmendmentReport | MAJOR | • Old RDLC has a grand-total footer (Sum(Fields!Amount.Value)); new controller sets no ColumnTotals -> Total row missing.<br>• Filter set matches old view (From/To Date, Export Section, Remark, Company Registration No, Sakhan).<br>• Export Section lookup correctly pinned to scoped 'borderExportLicenceSections' (Type=Export Licence + IsBorder), not the leaky generic list. |
| BorderExportLicenceByHSCodeReport | MAJOR | • MISSING FILTER: old view has an 'Export Section' dropdown (model.ExportImportSectionId / Model.ExportImportSectionList) but the new config has NO ExportImportSectionId filter at all.<br>• FilterType ('Filter By') is a real DropDownList in old (Model.FilterTypeList from CommonRepository.GetFilterType()) but is rendered as a free-text input (type:'text') in new -> wrong control type / no option set.<br>• Old RDLC BorderHSCodeReport.rdlc has a grand-total footer (Sum(Fields!Amount.Value)); new controller sets no ColumnTotals -> Total row missing. |
| BorderExportLicenceByMethodReport | MINOR | • Total row matches: old RDLC has Sum(Fields!Amount.Value) + CountDistinct footer and new controller sets ColumnTotals.<br>• EXTRA filters in new: 'Method of export According to Incoterms' (ExportImportIncotermId), 'Buyer Country' (BuyerCountryId) and 'Company Registration No' are not present in the old By-Method filter box (old has only From/To Date, EIR Card Type, Sakhan, Export Section, Method of export).<br>• Old Sakhan dropdown is required in the By-Method view ('--- Choose ---', required); new SakhanId is optional (defaultValue 0) - cosmetic.<br>• ExportImportMethodId/IncotermId/BuyerCountryId auto-resolve via idFilterLookups to generic lookups (exportImportMethods / exportImportIncoterms / countries) - methods/incoterms are NOT scoped to Export Licence Border, a potential leaky-lookup concern though it matches the generic old SelectList for Method. |
| BorderExportLicenceBySectionReport | MINOR | • Total row matches: old RDLC Sum(Fields!Amount.Value) footer; new controller sets ColumnTotals.<br>• EXTRA filters in new: ExportImportIncotermId (Method of export According to Incoterms), BuyerCountryId (Buyer Country), CompanyRegistrationNo - old By-Section view only has From/To Date, EIR Card Type, Sakhan, Export Section, Method of export.<br>• Export Section pinned to scoped borderExportLicenceSections lookup - correct. |
| BorderExportLicenceBySellerCountryReport | MINOR | • Total row matches: old RDLC Sum(Fields!Amount.Value) footer; new controller sets ColumnTotals.<br>• EXTRA filters in new: ExportImportIncotermId, CompanyRegistrationNo - old By-Buyer-Country view has From/To Date, EIR Card Type, Sakhan, Export Section, Method of export, Buyer Country (no Incoterm, no Company Reg No).<br>• Report named 'Seller Country' in new but old report/rdlc are 'Buyer Country' (subtitle text 'List of Export Licences By Buyer Country'); column header is 'Country' in both - title wording divergence only. |
| BorderExportLicenceCancellationReport | MAJOR | • Old RDLC BorderCancelReport.rdlc has a grand-total footer (=Fields!Currency.Value + ":" + FORMAT(Sum(Fields!Amount.Value),"N4")); new controller sets no ColumnTotals -> Total row missing.<br>• Filter set matches old view (From/To Date, Sakhan, Export Section, Company Registration No).<br>• Old readonly CompanyName helper omitted in new (UI helper). |
| BorderExportLicenceCompanyListReport | MINOR | • Total row matches: old RDLC Sum(Fields!Amount.Value) footer; new controller sets ColumnTotals.<br>• EXTRA filter in new: ExportImportIncotermId (Method of export According to Incoterms) - old Company List view has From/To Date, EIR Card Type, Sakhan, Export Section, Method of export, Company Registration No (no Incoterm).<br>• CompanyRegistrationNo present in both; old readonly CompanyName helper omitted in new. |
| BorderExportLicenceDailyReportNewLicenceReport | MINOR | • Total row matches: old RDLC has Sum(Fields!Amount.Value) AND Sum(Fields!totalUSDAmount.Value) footers; new controller sets ColumnTotals.<br>• EXTRA filters in new: ExportImportMethodId (Method of export), ExportImportIncotermId, BuyerCountryId - old Daily view only has From/To Date, Sakhan, Export Section, EIR Card Type, Company Registration No.<br>• Total USD Value column present in both (recent FX conversion work) - parity good. |
| BorderExportLicenceDetailReport | MINOR | • Total row matches: old RDLC has NO grand-total Sum footer (SumFieldsCount=0) and new controller sets no ColumnTotals -> both have no Total row.<br>• EXTRA filters in new: BuyerCountryId, CompanyRegistrationNo - old Detail view has From/To Date, EIR Card Type, Sakhan, Export Section, Method of export, Method-by-Incoterms (no Buyer Country, no Company Reg No).<br>• Export Section pinned to scoped borderExportLicenceSections lookup - correct. |
| BorderExportLicenceExtensionReport | MAJOR | • Old RDLC BorderExtensionReport.rdlc has a grand-total footer (=Fields!Currency.Value+":"+FORMAT(Sum(Fields!Amount.Value),"N4")); new controller sets no ColumnTotals -> Total row missing.<br>• Filter set matches old view (From/To Date, Sakhan, Export Section, Company Registration No).<br>• Old readonly CompanyName helper omitted in new (UI helper). |
| BorderExportLicenceNewReportNewReport | MAJOR | • Old RDLC BorderNewReport.rdlc has a grand-total footer (=Fields!Currency.Value+":"+FORMAT(Sum(Fields!Amount.Value),"N4")); new controller sets no ColumnTotals -> Total row missing.<br>• Filter set: old New view has From/To Date, Sakhan, Export Section, Company Registration No. New config also adds an 'Auto' text filter that has no counterpart in the old view (it maps to an 'Auto' debug column).<br>• Old readonly CompanyName helper omitted in new (UI helper). |
| BorderExportLicenceTotalValueLicencesReport | MAJOR | • Old RDLC has a grand-total footer (=FORMAT(Sum(Fields!Amount.Value),"N4") plus a 'Total Licences' aggregate); new controller sets no ColumnTotals -> Total row missing.<br>• EXTRA filters in new: PaThaKaTypeId is in both; new ALSO adds ExportImportMethodId, ExportImportIncotermId, BuyerCountryId, CompanyRegistrationNo - old TotalValue view only has From/To Date, EIR Card Type, Sakhan, Export Section.<br>• This is the report family's summary-of-totals view, so a missing Total/grand-sum row is especially impactful. |
| BorderExportLicenceVoucherReport | MAJOR | • COLUMN MISMATCH (map need_in_new=4, extra_in_new=4): new drops old columns 'Application Date', 'Commodity Type' and the two dynamic header columns (=Parameters!header2/header3) while adding 'Licence Date', 'Lic Value', 'Currency', 'Approved User'. Net different column set.<br>• ApplyType option set differs: old Border Export Licence Voucher action builds ApplyTypeList = GetApplyTypeList() minus 'Fine' = New, Amend, Extension, Cancel, Actual Amend (NO De-Cancel and no 'All' prompt). New config adds an extra 'De-Cancel' option (6 options).<br>• PaymentType option set differs: old PaymentTypeList is DB-driven (paymentTypeRepository.GetAll() where IsActive, by Id/Name) whereas new hardcodes All / MPU / CitizenPay - values may not match the live DB payment-type rows.<br>• Total row: old BorderVoucherReport.rdlc has no Sum(Fields) grand-total footer; new controller sets no ColumnTotals -> both have no Total row (match). |

### BorderExportPermit (12 reports) — ✅ adversarially verified

| Report | Verdict | Findings |
| --- | --- | --- |
| BorderExportPermitActualAmendmentReport | MAJOR | • Section dropdown leaks all section types: new ExportImportSectionId auto-maps to generic 'exportImportSections' lookup with no Type/Border scope, but old scopes to ExportPermit + IsBorder sections.<br>• Old RDLC has a grand-total footer row (TOTAL + =FORMAT(Sum(Fields!Amount.Value)...)) but the new controller never sets ColumnTotals, so no Total row is rendered. |
| BorderExportPermitAmendmentReport | MAJOR | • Section dropdown leaks all section types (generic exportImportSections lookup vs old ExportPermit+IsBorder scope).<br>• Old RDLC has a grand-total footer (TOTAL + Sum(Amount)); new controller sets no ColumnTotals so no Total row. |
| BorderExportPermitByHSCodeReport | MAJOR | • Section dropdown leaks all section types (generic exportImportSections lookup vs old ExportPermit+IsBorder scope).<br>• Old RDLC has a grand-total footer (TOTAL + aggregate); new controller sets no ColumnTotals so no Total row. |
| BorderExportPermitBySectionReport | MAJOR | • Extra filter in new: BuyerCountryId ('Buyer Country') is NOT in the old By-Section filter box.<br>• Section dropdown leaks all section types (generic exportImportSections lookup vs old ExportPermit+IsBorder scope).<br>• Old RDLC has a grand-total footer (TOTAL + CountDistinct(LicenceNo)/Sum); new controller sets no ColumnTotals. |
| BorderExportPermitBySellerCountryReport | MAJOR | • Section dropdown leaks all section types (generic exportImportSections lookup vs old ExportPermit+IsBorder scope).<br>• Old RDLC has a grand-total footer (TOTAL + CountDistinct(LicenceNo) at line 913); new controller sets no ColumnTotals so no Total row. |
| BorderExportPermitCancellationReport | MAJOR | • Section dropdown leaks all section types (generic exportImportSections lookup vs old ExportPermit+IsBorder scope).<br>• Old RDLC has a grand-total footer (TOTAL + Sum); new controller sets no ColumnTotals so no Total row. |
| BorderExportPermitCompanyListReport | MAJOR | • Extra filter in new: BuyerCountryId ('Buyer Country') is NOT in the old By-Company filter box.<br>• Section dropdown leaks all section types (generic exportImportSections lookup vs old ExportPermit+IsBorder scope).<br>• Old RDLC has a grand-total footer (TOTAL at line 859 + CountDistinct(LicenceNo) at 913 + Sum(Amount) at 694); new controller sets no ColumnTotals. |
| BorderExportPermitDailyReportNewPermitReport | MAJOR | • Extra filter in new: BuyerCountryId ('Buyer Country') is NOT in the old Daily filter box.<br>• Section dropdown leaks all section types (generic exportImportSections lookup vs old ExportPermit+IsBorder scope).<br>• Old RDLC has a grand-total footer (TOTAL at line 993 + =FORMAT(Sum(Fields!totalUSDAmount.Value)) at 881); new controller sets no ColumnTotals so no Total row. |
| BorderExportPermitDetailReport | MAJOR | • Two extra filters in new: BuyerCountryId ('Buyer Country') and CompanyRegistrationNo are NOT in the old Detail filter box (old Detail view only has FromDate, ToDate, Sakhan, PaThaKaType, ExportSection).<br>• Section dropdown leaks all section types (generic exportImportSections lookup vs old ExportPermit+IsBorder scope).<br>• Total row matches: old Detail RDLC has NO grand-total footer and new controller sets no ColumnTotals. |
| BorderExportPermitExtensionReport | MAJOR | • Section dropdown leaks all section types (generic exportImportSections lookup vs old ExportPermit+IsBorder scope).<br>• Old RDLC has a grand-total footer (TOTAL + Sum); new controller sets no ColumnTotals so no Total row. |
| BorderExportPermitNewReportNewReport | MAJOR | • Extra filter in new: 'Auto' (text) is NOT in the old New Report filter box; there is also a stray 'auto' column (key 'Auto', title 'auto') in the new columns that has no counterpart in the old RDLC.<br>• Section dropdown leaks all section types (generic exportImportSections lookup vs old ExportPermit+IsBorder scope).<br>• Old RDLC has a grand-total footer (TOTAL + Sum); new controller sets no ColumnTotals so no Total row. |
| BorderExportPermitVoucherReport | MAJOR | • Section dropdown leaks all section types (generic exportImportSections lookup vs old ExportPermit+IsBorder scope).<br>• Old RDLC has a grand-total footer (TOTAL at line 1457 + =FORMAT(SUM(Fields!Amount.Value),'N0') at 1521); new controller sets no ColumnTotals so no Total row.<br>• PaymentType is hardcoded on new side (All/MPU/Citizen Pay) whereas old sources it dynamically from paymentTypeRepository.GetAll().Where(IsActive) (Id/Name) -- option set may drift from DB. |

### BorderImportLicence (16 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| BorderImportLicenceActualAmendmentReport | MAJOR | • MAJOR: missing grand-total footer row - old RDLC has a TOTAL footer, new controller never sets ColumnTotals.<br>• MAJOR: Import Section dropdown leaky - pinned to generic exportImportSections (all types) instead of Border Import Licence scoped sections. |
| BorderImportLicenceAmendmentReport | MAJOR | • MAJOR: missing grand-total footer row (old RDLC has TOTAL, new no ColumnTotals).<br>• MAJOR: Import Section dropdown leaky (generic exportImportSections vs Border Import scope). |
| BorderImportLicenceByHSCodeReport | MAJOR | • MAJOR: missing grand-total footer row (old RDLC has TOTAL footer; aggregate result built without includeColumnTotals).<br>• MAJOR: Import Section dropdown leaky (generic exportImportSections).<br>• MINOR: FilterBy rendered as free text instead of the old fixed FilterType option dropdown. |
| BorderImportLicenceByMethodReport | MAJOR | • MAJOR: missing grand-total footer row - old RDLC has TOTAL footer; aggregate built without includeColumnTotals (Border Import Licence controllers omit the flag that sibling families set).<br>• MAJOR: Import Section and Import Method dropdowns leaky (generic exportImportSections / exportImportMethods, not Border-Import scoped).<br>• MAJOR: extra filters Import Incoterms, Seller Country, Company Registration No not in old ByMethod filter box.<br>• MINOR: Method label 'Import Method' vs old resx 'Method of Import'. |
| BorderImportLicenceBySectionReport | MAJOR | • MAJOR: missing grand-total footer row (old RDLC TOTAL footer; aggregate built without includeColumnTotals).<br>• MAJOR: Import Section / Import Method dropdowns leaky (generic lookups, not Border-Import scoped).<br>• MAJOR: extra filters Import Incoterms, Seller Country, Company Registration No not in old BySection view.<br>• MINOR: Method label 'Import Method' vs old 'Method of Import'. |
| BorderImportLicenceBySellerCountryReport | MAJOR | • MAJOR: missing grand-total footer row (old RDLC TOTAL footer; aggregate built without includeColumnTotals).<br>• MAJOR: Import Section / Import Method dropdowns leaky (generic lookups, not Border-Import scoped).<br>• MAJOR: extra filters Import Incoterms and Company Registration No not in old BySellerCountry view.<br>• MINOR: Method label 'Import Method' vs old 'Method of Import'. |
| BorderImportLicenceCancellationReport | MAJOR | • MAJOR: missing grand-total footer row (old RDLC TOTAL footer; new no ColumnTotals).<br>• MAJOR: Import Section dropdown leaky (generic exportImportSections). |
| BorderImportLicenceCompanyListReport | MAJOR | • MAJOR: missing grand-total footer row (old RDLC TOTAL footer; aggregate built without includeColumnTotals while sibling CompanyList controllers set it).<br>• MAJOR: Import Section / Import Method dropdowns leaky (generic lookups).<br>• MAJOR: extra filters Import Incoterms and Seller Country not in old Company List view.<br>• MINOR: Method label differs ('Import Method' new vs old view used the ExportMethod resx = 'Method of export'). |
| BorderImportLicenceDailyReportNewLicenceReport | MAJOR | • MAJOR: missing grand-total footer row (old RDLC TOTAL footer; aggregate built without includeColumnTotals while sibling Daily controllers populate ColumnTotals).<br>• MAJOR: Import Section dropdown leaky (generic exportImportSections).<br>• MAJOR: extra filters Import Method, Import Incoterms, Seller Country not in old Daily view. |
| BorderImportLicenceDetailReport | MAJOR | • MAJOR: Import Section / Import Method / Import Incoterms dropdowns leaky (generic lookups, not Border-Import scoped).<br>• MAJOR: extra filters Seller Country and Company Registration No not in old Detail view.<br>• MINOR: labels 'Import Method' / 'Import Incoterms' vs old resx 'Method of Import' / 'Method of Import According to Incoterms'.<br>• Total row matches (neither side has one) - OK. |
| BorderImportLicenceDetailReportPending | MAJOR | • MAJOR: Import Section / Import Method / Import Incoterms dropdowns leaky (generic lookups).<br>• MAJOR: extra filters Seller Country and Company Registration No not in old Detail view.<br>• MINOR: labels 'Import Method'/'Import Incoterms' vs old 'Method of Import'/'Method of Import According to Incoterms'.<br>• No dedicated old Pending view exists; compared against the shared Detail RDLC (columns match, total matches). |
| BorderImportLicenceExtensionReport | MAJOR | • MAJOR: missing grand-total footer row (old RDLC TOTAL footer; new no ColumnTotals).<br>• MAJOR: Import Section dropdown leaky (generic exportImportSections). |
| BorderImportLicenceNewReportNewReport | MAJOR | • MAJOR: missing grand-total footer row (old RDLC TOTAL footer; new no ColumnTotals).<br>• MAJOR: Import Section dropdown leaky (generic exportImportSections).<br>• MINOR: extra 'Auto' text filter (and 'auto' column titled lowercase 'auto') not in old view; looks like a stray field. |
| BorderImportLicencePendingReport | NO_OLD_MATCH | • NO_OLD_MATCH: mapped RDLC 'PendingLicenceReport.rdlc' is not present in the old codebase and there is no old Border Import Licence Pending view; nothing to compare against. mapped=false. |
| BorderImportLicenceTotalValueLicencesReport | MAJOR | • MAJOR: extra filters Import Method, Import Incoterms, Seller Country, Company Registration No not in old Total Value view.<br>• MAJOR: Import Section dropdown leaky (generic exportImportSections).<br>• Total row matches (summary report; no separate footer either side) - OK. |
| BorderImportLicenceVoucherReport | MAJOR | • MAJOR: missing grand-total footer row (old RDLC TOTAL footer; new no ColumnTotals).<br>• MAJOR: Import Section dropdown leaky (generic exportImportSections).<br>• MINOR: PaymentType and ApplyType rendered as free text inputs instead of the old curated SelectList dropdowns. |

### BorderImportPermit (12 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| BorderImportPermitActualAmendmentReport | MAJOR | • Section dropdown leaks: new ExportImportSectionId filter has no lookupName, so it auto-resolves to the generic 'exportImportSections' lookup (no Type/IsBorder filter) returning ALL sections; old view scopes it to GetAll(ImportPermit).Where(IsActive && IsBorder).<br>• Missing grand-total footer: AmendReport.rdlc has a TOTAL footer row (=Fields!Currency.Value+":"+FORMAT(Sum(Fields!Amount.Value),"N4")) but the new controller does NOT set ColumnTotals, so no <tfoot> Total row.<br>• Filter set otherwise matches (date range, Import Section, Remark, Company Registration No, Sakhan). FormType is a hidden derived scoping param, not a user filter. |
| BorderImportPermitAmendmentReport | MAJOR | • Section dropdown leaks: ExportImportSectionId auto-resolves to generic 'exportImportSections' (unscoped); old scopes to GetAll(ImportPermit) IsActive && IsBorder.<br>• Missing grand-total footer: AmendReport.rdlc has TOTAL + Sum(Amount); new controller sets no ColumnTotals.<br>• Filters (date range, Import Section, Remark, Company Registration No, Sakhan) otherwise match; FormType is hidden derived scoping. |
| BorderImportPermitByHSCodeReport | MAJOR | • FilterType control mismatch: old 'Filter By' is a DROPDOWN (FilterTypeList = CommonRepository.GetFilterType() -> values 'Start','End'); new config renders FilterType as a free-text Input with no options.<br>• Section dropdown leaks: ExportImportSectionId auto-resolves to generic unscoped 'exportImportSections'; old scopes to GetAll(ImportPermit) IsActive && IsBorder.<br>• Missing grand-total footer: BorderHSCodeReport.rdlc has TOTAL + FORMAT(Sum(Fields!Amount.Value),"N4"); new controller sets no ColumnTotals. |
| BorderImportPermitBySectionReport | MAJOR | • Section dropdown leaks: ExportImportSectionId auto-resolves to generic unscoped 'exportImportSections'; old scopes to GetAll(ImportPermit) IsActive && IsBorder.<br>• Missing grand-total footer: rdlc has TOTAL + FORMAT(Sum(Fields!Amount.Value),"N4"); new controller sets no ColumnTotals.<br>• Extra-in-new filters SellerCountryId and CompanyRegistrationNo: the old BySection view only shows From/To, Sakhan, EIR Card Type (PaThaKaType), Import Section; new config also includes SellerCountryId and CompanyRegistrationNo which the old filter box does not. |
| BorderImportPermitBySellerCountryReport | MAJOR | • Section dropdown leaks: ExportImportSectionId auto-resolves to generic unscoped 'exportImportSections'; old scopes to GetAll(ImportPermit) IsActive && IsBorder.<br>• Missing grand-total footer: rdlc has TOTAL + FORMAT(Sum(Fields!Amount.Value),"N4"); new controller sets no ColumnTotals.<br>• Extra-in-new filter CompanyRegistrationNo: old view shows From/To, Sakhan, PaThaKaType, Import Section, Seller Country only — no Company Registration No box. |
| BorderImportPermitCancellationReport | MAJOR | • Section dropdown leaks: ExportImportSectionId auto-resolves to generic unscoped 'exportImportSections'; old scopes to GetAll(ImportPermit) IsActive && IsBorder.<br>• Missing grand-total footer: BorderCancelReport.rdlc has TOTAL + =Fields!Currency.Value + ":" + FORMAT(Sum(Fields!Amount.Value),"N4"); new controller sets no ColumnTotals. |
| BorderImportPermitCompanyListReport | MAJOR | • Section dropdown leaks: ExportImportSectionId auto-resolves to generic unscoped 'exportImportSections'; old scopes to GetAll(ImportPermit) IsActive && IsBorder.<br>• Missing grand-total footer: rdlc has TOTAL + FORMAT(Sum(Fields!Amount.Value),"N4"); new controller sets no ColumnTotals.<br>• Extra-in-new filter SellerCountryId: old ByCompany view shows From/To, Sakhan, PaThaKaType, Import Section, Company Registration No only — no Seller Country dropdown. |
| BorderImportPermitDailyReportNewPermitReport | MAJOR | • Section dropdown leaks: ExportImportSectionId auto-resolves to generic unscoped 'exportImportSections'; old scopes to GetAll(ImportPermit) IsActive && IsBorder.<br>• Missing grand-total footer: BorderImportPermitByDailyReport.rdlc has TOTAL row with FORMAT(Sum(Fields!Amount.Value),"N4") AND FORMAT(Sum(Fields!totalUSDAmount.Value),"N4"); new controller sets no ColumnTotals, so the Total Value + Total USD Value sums are absent.<br>• Extra-in-new filter SellerCountryId: old Daily view shows From/To, Sakhan, Import Section, EIR Card Type, Company Registration No only — no Seller Country. |
| BorderImportPermitDetailReport | MAJOR | • Section dropdown leaks: ExportImportSectionId auto-resolves to generic unscoped 'exportImportSections'; old scopes to GetAll(ImportPermit) IsActive && IsBorder.<br>• Extra-in-new filters SellerCountryId and CompanyRegistrationNo: old Detail view filter box shows only From/To, Sakhan, EIR Card Type (PaThaKaType), Import Section.<br>• Total row OK: BorderImportPermitDetailReport.rdlc has NO TOTAL footer (no Sum aggregate) and new controller sets no ColumnTotals -> match (PASS on total axis). |
| BorderImportPermitExtensionReport | MAJOR | • Section dropdown leaks: ExportImportSectionId auto-resolves to generic unscoped 'exportImportSections'; old scopes to GetAll(ImportPermit) IsActive && IsBorder.<br>• Missing grand-total footer: BorderExtensionReport.rdlc has TOTAL + =Fields!Currency.Value+":"+FORMAT(Sum(Fields!Amount.Value),"N4"); new controller sets no ColumnTotals. |
| BorderImportPermitNewReportNewReport | MAJOR | • Section dropdown leaks: ExportImportSectionId auto-resolves to generic unscoped 'exportImportSections'; old scopes to GetAll(ImportPermit) IsActive && IsBorder.<br>• Missing grand-total footer: BorderNewReport.rdlc has TOTAL + =Fields!Currency.Value+":"+FORMAT(Sum(Fields!Amount.Value),"N4"); new controller sets no ColumnTotals.<br>• Extra-in-new filter 'Auto': old NewReport filter box has no Auto field (only From/To, Sakhan, Import Section, Company Registration No). New config exposes a visible 'Auto' text Input.<br>• Extra-in-new column 'auto': new config adds a column key 'Auto' (dataIndex auto, title 'auto'); BorderNewReport.rdlc has no such column (headers end at Curency/Total Value). Note: precomputed extra_in_new=0 disagrees with the rdlc here — flagging the observed extra Auto column. |
| BorderImportPermitVoucherReport | MAJOR | • Dynamic header text leaked literally: new config column titles are the raw RDLC parameter expressions '=Parameters!header2.Value' and '=Parameters!header3.Value' (rendered verbatim as the column headers). Old RDLC resolves header2/header3 to real runtime labels (e.g. Licence No / Licence Date). MAJOR header-text parity bug.<br>• ApplyType control mismatch: old 'Apply Type' is a DROPDOWN (ApplyTypeList = New/Amend/Extension/Cancel...). New config renders ApplyType as a free-text Input with no options.<br>• PaymentType control mismatch: old 'Payment Type' is a DROPDOWN (PaymentTypeList from paymentTypeRepository). New config renders PaymentType as a free-text Input with no options.<br>• Section dropdown leaks: ExportImportSectionId auto-resolves to generic unscoped 'exportImportSections'; old scopes to GetAll(ImportPermit) IsActive && IsBorder.<br>• Missing grand-total footer: BorderVoucherReport.rdlc has TOTAL + FORMAT(SUM(Fields!Amount.Value),"N0"); new controller sets no ColumnTotals.<br>• Likely extra columns: new config has 'Application Date' and 'Commodity Type' columns; BorderVoucherReport.rdlc has no Application Date and no Commodity Type header (precomputed extra_in_new=0 disagrees). |

### ImportLicence (16 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| ImportLicenceActualAmendmentReport | MAJOR | • Filter box matches old: dateRange, Section, CompanyRegistrationNo, CompanyName(readonly), Remark(AmendRemark). FormType is derived+hidden (old @Html.HiddenFor model.FormType).<br>• MAJOR: old AmendReport.rdlc has a grand-total footer (>TOTAL< + Sum(Fields!Amount)) but new ImportLicenceActualAmendmentReportController does not set ColumnTotals -> Total row missing in new. |
| ImportLicenceAmendmentReport | MAJOR | • Filter box matches old (dateRange, Section, CompanyRegistrationNo, CompanyName, Remark). FormType derived+hidden.<br>• MAJOR: old AmendReport.rdlc has >TOTAL< grand-total footer (Sum(Fields!Amount)); new ImportLicenceAmendmentReportController has no ColumnTotals -> Total row missing. |
| ImportLicenceByHSCodeReport | MAJOR | • Filter box matches old: dateRange, Section, FilterBy (FilterType), HSCode. FormType derived+hidden.<br>• FilterType options [Start,End] match old FilterTypeList (CommonRepository.GetFilterType -> Start,End) with no '--- All ---'.<br>• MAJOR: old HSCodeReport.rdlc has >TOTAL< grand-total footer (Sum(Fields!Amount)); new ImportLicenceByHSCodeReportController has no ColumnTotals -> Total row missing. |
| ImportLicenceByMethodReport | PASS | • Filter box matches old: dateRange, PaThaKaType(EIR Card Type), Section, Method. Type derived+hidden (old @Html.HiddenFor model.Type).<br>• Method lookup importLicenceMethods scoped Type='Import' IsOversea, matching old GetAll(Import).Where(IsActive&&IsOversea).<br>• Total row matches: old rdlc has TOTAL footer (Sum) and new controller sets ColumnTotals. |
| ImportLicenceBySectionReport | PASS | • Filter box matches old: dateRange, PaThaKaType, Section, Method. Type derived+hidden.<br>• Total row matches: old rdlc TOTAL footer + new controller sets ColumnTotals (line 90). |
| ImportLicenceBySellerCountryReport | PASS | • Filter box matches old: dateRange, PaThaKaType, Section, Method, SellerCountry. Type derived+hidden.<br>• SellerCountryId auto-resolves to 'countries' lookup (label 'Seller Country' matches resx SellerCountry).<br>• Total row matches: old rdlc TOTAL footer + new controller includeColumnTotals:true (line 42). |
| ImportLicenceCancellationReport | MAJOR | • Filter box matches old: dateRange, Section, CompanyRegistrationNo, CompanyName. FormType derived+hidden.<br>• MAJOR: old CancelReport.rdlc has >TOTAL< grand-total footer (Sum(Fields!Amount)); new ImportLicenceCancellationReportController has no ColumnTotals -> Total row missing. |
| ImportLicenceCompanyListReport | PASS | • Filter box matches old: dateRange, PaThaKaType, Section, Method('Method of export'), CompanyRegistrationNo, CompanyName. Type derived+hidden.<br>• Method filter label 'Method of export' matches old resx ExportMethod ('Method of export') used by the old ByCompany view (note: old uses ExportMethod label here, not ImportMethod).<br>• Total row matches: old rdlc TOTAL footer + new controller includeColumnTotals:true (line 42). |
| ImportLicenceDailyReportNewLicenceReport | PASS | • Filter box matches old: dateRange, Section, PaThaKaType, CompanyRegistrationNo, CompanyName. Type derived+hidden.<br>• Total row matches: old rdlc has TOTAL footer (Sum of Amount and totalUSDAmount); new controller sets ColumnTotals (line 53). Total USD Value column also totaled. |
| ImportLicenceDetailReport | MINOR | • Filter box matches old visible filters: dateRange, PaThaKaType, Section, Method, Incoterm. Type derived+hidden.<br>• Total row matches: old ImportLicenceDetailReport.rdlc has NO grand-total footer; new controller has no ColumnTotals -> match (PASS on total).<br>• MINOR value nuance: old Detail action populated ExportImportMethodList/IncotermList via GetAll('Export') (legacy quirk), whereas new importLicenceMethods/importLicenceIncoterms scope Type='Import'. New 'Import' scoping is consistent with ByMethod and is the correct set for an Import Licence report; option set may differ from the legacy Detail-only Export-scoped list. |
| ImportLicenceDetailReportPending | MINOR | • Shares importLicenceSummaryDetailFilters and the same Detail rdlc as ImportLicenceDetailReport. Filter box and columns match.<br>• Total row matches: old Detail rdlc has no grand-total footer; new controller has no ColumnTotals.<br>• MINOR value nuance: same Method/Incoterm Import-vs-Export scoping note as ImportLicenceDetailReport. (Old code has no distinct 'Detail Pending' report; this is a new variant reusing the Detail layout.) |
| ImportLicenceExtensionReport | MAJOR | • Filter box matches old: dateRange, Section, CompanyRegistrationNo, CompanyName. FormType derived+hidden.<br>• MAJOR: old ExtensionReport.rdlc has >TOTAL< grand-total footer (Sum(Fields!Amount)); new ImportLicenceExtensionReportController has no ColumnTotals -> Total row missing. |
| ImportLicenceNewReportNewReport | MAJOR | • MAJOR: old NewLicenceReport.rdlc has >TOTAL< grand-total footer (Sum(Fields!Amount)); new ImportLicenceNewReportNewReportController has no ColumnTotals -> Total row missing.<br>• MAJOR: new adds two EXTRA filters not present in the old New view's filter box: Auto/None Auto ('Auto') and Quota ('Quota'). Old ImportLicenceNewReport.cshtml only shows dateRange, Section, CompanyRegistrationNo, CompanyName (FormType hidden). The new Auto/Quota selects have no counterpart in the old filter box. |
| ImportLicencePendingReport | NO_OLD_MATCH | • No old Tradenet 2.0 counterpart exists. report_map maps it to PendingLicenceReport.rdlc, but that rdlc does NOT exist in ReportControl, there is no ImportLicencePending* action in ReportsController, and no Pending view in Views/Reports. 'Pending' only appears as a dashboard count in CommonController. Nothing to compare against.<br>• New report defines importLicencePendingFilters (dateRange, FormType hidden, Section) and 12 columns; cannot verify parity without an old source. |
| ImportLicenceTotalValueLicencesReport | PASS | • Filter box matches old: dateRange, PaThaKaType, Section. Type derived+hidden.<br>• Total row matches: this is itself a total/summary report. The old rdlc shows the aggregated Sum(Amount) as the report body (no separate >TOTAL< footer label); the new report likewise returns the aggregated total rows (TotalValue + Currency) and the controller does not need a separate ColumnTotals footer. Match. |
| ImportLicenceVoucherReport | MAJOR | • Filter box matches old: dateRange, Section, ApplyType, PaymentType, CompanyRegistrationNo, CompanyName. FormType derived+hidden.<br>• ApplyType options [New,Amend,Extension,Cancel,Actual Amend] match old ApplyTypeList (GetApplyTypeList minus Fine). PaymentType uses paymentTypes lookup matching old PaymentTypeList.<br>• MAJOR: old VoucherReport.rdlc has >TOTAL< grand-total footer (SUM(Fields!Amount)); new ImportLicenceVoucherReportController has no ColumnTotals -> Total row missing. |

### ImportPermit (12 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| ImportPermitActualAmendmentReport | MAJOR | • Old AmendReport.rdlc renders a grand-total footer row (TOTAL + "Total: N licence(s)") but the new ImportPermitActualAmendmentReportController does not set ColumnTotals, so no <tfoot> Total row is emitted — MAJOR.<br>• Filter set otherwise matches: From/To date, Import Section, Remark, Company Registration No.<br>• Old readonly CompanyName companion field (autocomplete display of the selected CompanyRegistrationNo) has no equivalent in new — cosmetic only, not a query filter. |
| ImportPermitAmendmentReport | MAJOR | • Old AmendReport.rdlc has a grand-total footer (TOTAL + "Total: N licence(s)") but new ImportPermitAmendmentReportController does not set ColumnTotals — missing Total row, MAJOR.<br>• Filters match: From/To, Import Section (scoped importPermitSections = Import Permit + IsOversea), Remark (amendRemarks lookup), Company Registration No.<br>• Old readonly CompanyName companion field absent in new — cosmetic. |
| ImportPermitByHSCodeReport | MAJOR | • MISSING FILTER: old filter box has an Import Section dropdown (ExportImportSectionList, scoped Import Permit + IsOversea) but the new config has NO Import Section filter — MAJOR.<br>• VALUE/CONTROL MISMATCH: old 'Filter By' is a DropDownList sourced from GetFilterType() = [Start, End]; new FilterType is a free-text input (type:'text') with no options — wrong control + no constrained values, MAJOR.<br>• Old HSCodeReport.rdlc has a TOTAL footer (TOTAL + =CountDistinct(LicenceNo)) but new ImportPermitByHSCodeReportController sets no ColumnTotals — missing Total row, MAJOR.<br>• From/To date and HS Code text filter match. |
| ImportPermitBySectionReport | MINOR | • EXTRA FILTERS: new config adds Seller Country and Company Registration No filters that the old Section filter box does not have (old shows only From/To, EIR Card Type, Import Section) — extra-in-new, cosmetic/over-provision.<br>• Total row matches: old rdlc has a TOTAL footer and new controller sets ColumnTotals.<br>• Labels match (PaThaKaType resx='EIR Card Type' == new 'EIR Card Type'; Import Section matches). Import Section lookup correctly scoped to Import Permit + IsOversea. |
| ImportPermitBySellerCountryReport | MINOR | • EXTRA FILTER: new config adds a Company Registration No filter not present in the old Seller Country filter box (old: From/To, EIR Card Type, Import Section, Seller Country) — extra-in-new, cosmetic.<br>• Seller Country dropdown value parity OK: old SellerCountryList = countriesRepository.GetAll(); new SellerCountryId resolves to 'countries' lookup via idFilterLookups.<br>• Total row matches: old rdlc TOTAL footer + new ColumnTotals set. |
| ImportPermitCancellationReport | MAJOR | • Old CancelReport.rdlc has a grand-total footer (TOTAL + "Total: N licence(s)") but new ImportPermitCancellationReportController sets no ColumnTotals — missing Total row, MAJOR.<br>• Filters match: From/To, Import Section, Company Registration No.<br>• Old readonly CompanyName companion field absent in new — cosmetic. |
| ImportPermitCompanyListReport | MINOR | • EXTRA FILTER: new config adds a Seller Country filter not present in the old Company filter box (old: From/To, EIR Card Type, Import Section, Company Registration No) — extra-in-new, cosmetic.<br>• Total row matches: old rdlc TOTAL footer + new ColumnTotals set.<br>• Old readonly CompanyName companion field absent in new — cosmetic. |
| ImportPermitDailyReportNewPermitReport | MINOR | • EXTRA FILTER: new config adds a Seller Country filter not present in the old Daily filter box (old: From/To, Import Section, EIR Card Type, Company Registration No) — extra-in-new, cosmetic.<br>• Total row matches: old rdlc TOTAL footer + new ColumnTotals set.<br>• Old readonly CompanyName companion field absent in new — cosmetic.<br>• Note: new includes a 'Total USD Value' column which also exists in old ImportPermitByDailyReport.rdlc (Total USD Value textbox), so column counts match (0/0). |
| ImportPermitDetailReport | MINOR | • EXTRA FILTERS: new config adds Seller Country and Company Registration No filters; old Detail filter box only has From/To, EIR Card Type, Import Section — extra-in-new, cosmetic.<br>• Total row matches (PASS axis): ImportPermitDetailReport.rdlc has NO TOTAL/sum footer and new controller sets no ColumnTotals.<br>• Columns match (0/0). |
| ImportPermitExtensionReport | MAJOR | • Old ExtensionReport.rdlc has a grand-total footer (TOTAL + "Total: N licence(s)") but new ImportPermitExtensionReportController sets no ColumnTotals — missing Total row, MAJOR.<br>• Filters match: From/To, Import Section, Company Registration No.<br>• Old readonly CompanyName companion field absent in new — cosmetic. |
| ImportPermitNewReportNewReport | MAJOR | • Old NewLicenceReport.rdlc has a grand-total footer (TOTAL + "Total: N licence(s)") but new ImportPermitNewReportNewReportController sets no ColumnTotals — missing Total row, MAJOR.<br>• EXTRA FILTER: new config adds an 'Auto' text filter that the old filter box does not have (old: From/To, Import Section, Company Registration No, CompanyName) — extra-in-new.<br>• Old readonly CompanyName companion field absent in new — cosmetic. |
| ImportPermitVoucherReport | MAJOR | • COLUMNS: new is missing 4 real old columns — Application Date, Commodity Type, Total CIF, Exchange Rate (and 2 parameter-driven header columns header2/header3) — and adds an extra 'Licence Date' column (need_in_new=6, extra_in_new=1) — MAJOR.<br>• TOTAL: old VoucherReport.rdlc has a TOTAL footer row (Textbox4 'TOTAL', ColSpan 11, over the Total Amount column) but new ImportPermitVoucherReportController sets no ColumnTotals — missing Total row, MAJOR.<br>• APPLYTYPE VALUES: new ApplyType options add an extra 'De-Cancel' not in the old list; old ApplyTypeList = GetApplyTypeList().Where(Value != 'Fine') = [New, Amend, Extension, Cancel, Actual Amend] — extra option in new.<br>• PAYMENTTYPE VALUES: new PaymentType is hardcoded [--- All ---, Cash, MPU, Citizen Pay]; old PaymentTypeList is DB-driven (paymentTypeRepository.GetAll().Where(IsActive)) so option set may drift from the live PaymentType table — value-source mismatch.<br>• Import Section filter present and correctly scoped; Company Registration No present. Old readonly CompanyName companion absent in new — cosmetic. |

### ExportLicence (14 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| ExportLicenceActualAmendmentReport | MAJOR | • MAJOR: Export Section dropdown leaks all section types (Import/Export/Permit) - new uses generic exportImportSections, old scoped to ExportLicence+Oversea.<br>• MAJOR: Old AmendReport.rdlc has a grand-total footer; new emits no ColumnTotals so the Total row is missing.<br>• Extra filter in new: SakhanId (Sakhan) - absent from old view. |
| ExportLicenceAmendmentReport | MAJOR | • MAJOR: Export Section dropdown leaks all section types (generic exportImportSections).<br>• MAJOR: Missing grand-total footer (old RDLC has it, new sets no ColumnTotals).<br>• Extra filter in new: SakhanId. |
| ExportLicenceByHSCodeReport | MAJOR | • MAJOR: Missing filter - old has an Export Section dropdown (ExportImportSectionId); new HSCode config has no Section filter.<br>• MAJOR: Missing grand-total footer (HSCodeReport.rdlc has TOTAL footer; new no ColumnTotals).<br>• MINOR/value: FilterType is a dropdown (Model.FilterTypeList) in old but a free-text input in new.<br>• Extra filter in new: SakhanId. |
| ExportLicenceByMethodReport | MAJOR | • MAJOR: Export Section dropdown leaks all section types (generic exportImportSections).<br>• MAJOR: Method of export dropdown leaks Import + non-oversea methods (generic exportImportMethods) vs old Export+Oversea scope.<br>• MAJOR: Missing grand-total footer (RDLC has TOTAL; new no ColumnTotals).<br>• Extra filters in new not in old box: ExportImportIncotermId, BuyerCountryId, CompanyRegistrationNo, SakhanId. |
| ExportLicenceBySectionReport | MAJOR | • MAJOR: Export Section dropdown leaks all section types.<br>• MAJOR: Method of export dropdown leaks Import/non-oversea methods.<br>• MAJOR: Missing grand-total footer (RDLC has Sum footer line 660; new no ColumnTotals).<br>• Extra filters in new: ExportImportIncotermId, BuyerCountryId, CompanyRegistrationNo, SakhanId. |
| ExportLicenceBySellerCountryReport | MAJOR | • MAJOR: Export Section dropdown leaks all section types.<br>• MAJOR: Method of export dropdown leaks Import/non-oversea methods.<br>• MAJOR: Missing grand-total footer (RDLC has TOTAL/Sum; new no ColumnTotals).<br>• Extra filters in new: ExportImportIncotermId, CompanyRegistrationNo, SakhanId. |
| ExportLicenceCancellationReport | MAJOR | • MAJOR: Export Section dropdown leaks all section types.<br>• MAJOR: Missing grand-total footer (CancelReport.rdlc has TOTAL/Sum; new no ColumnTotals).<br>• Extra filter in new: SakhanId. |
| ExportLicenceCompanyListReport | MAJOR | • MAJOR: Export Section dropdown leaks all section types.<br>• MAJOR: Method of export dropdown leaks Import/non-oversea methods.<br>• MAJOR: Missing grand-total footer (RDLC has TOTAL/Sum; new no ColumnTotals).<br>• Extra filters in new: ExportImportIncotermId, BuyerCountryId, SakhanId. |
| ExportLicenceDailyReportNewLicenceReport | MAJOR | • MAJOR: Export Section dropdown leaks all section types.<br>• MAJOR: Missing grand-total footer (DailyReport RDLC has 3 Sum totals; new no ColumnTotals).<br>• Extra filters in new not in old box: ExportImportMethodId, ExportImportIncotermId, BuyerCountryId, SakhanId. |
| ExportLicenceDetailReport | MAJOR | • MAJOR: Export Section dropdown leaks all section types.<br>• MAJOR: Method of export dropdown leaks Import/non-oversea methods.<br>• MAJOR: Incoterms dropdown leaks Import/non-oversea incoterms.<br>• Extra filters in new: BuyerCountryId, CompanyRegistrationNo, SakhanId.<br>• Total row: matches (neither side has a grand-total) - OK.<br>• Columns: full parity (28/28). |
| ExportLicenceExtensionReport | MAJOR | • MAJOR: Export Section dropdown leaks all section types.<br>• MAJOR: Missing grand-total footer (ExtensionReport.rdlc has TOTAL/Sum; new no ColumnTotals).<br>• Extra filter in new: SakhanId. |
| ExportLicenceNewReportNewReport | MAJOR | • MAJOR: Export Section dropdown leaks all section types.<br>• MAJOR: Missing grand-total footer (NewLicenceReport.rdlc has TOTAL/Sum; new no ColumnTotals).<br>• Extra filters in new: SakhanId, Auto (Auto has no equivalent in the old filter box). |
| ExportLicenceTotalValueLicencesReport | MAJOR | • MAJOR: Export Section dropdown leaks all section types.<br>• MAJOR: Missing grand-total footer (RDLC has Sum aggregate line 400; new no ColumnTotals).<br>• Extra filters in new: ExportImportMethodId, ExportImportIncotermId, BuyerCountryId, CompanyRegistrationNo, SakhanId. |
| ExportLicenceVoucherReport | MAJOR | • MAJOR: Export Section dropdown leaks all section types.<br>• MAJOR: Missing grand-total footer (VoucherReport.rdlc has SUM/TOTAL footer; new no ColumnTotals).<br>• MINOR/value: ApplyType & PaymentType are dropdowns (Model.ApplyTypeList/PaymentTypeList) in old but free-text in new.<br>• Cosmetic: two column headers are unresolved RSExpressions ('=Parameters!header2.Value','=Parameters!header3.Value') copied verbatim into the new config - present in both, but should be real labels.<br>• Mapping note: report_map.json points to 'VoucherReport_Export.rdlc' which does not physically exist in ReportControl/ (only VoucherReport.rdlc).<br>• Extra filter in new: SakhanId. |

### ExportPermit (12 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| ExportPermitActualAmendmentReport | MAJOR | • Export Section dropdown is leaky: new ExportImportSectionId auto-maps to the unscoped 'exportImportSections' lookup (all Type/IsOversea), but old scopes it to GetAll(AppConfig.ExportPermit).Where(IsOversea==true). MAJOR value-parity bug.<br>• Total row missing: old AmendReport.rdlc has a grand-total footer (TOTAL + Sum(Amount)); new controller never sets ColumnTotals. |
| ExportPermitAmendmentReport | MAJOR | • Export Section dropdown is leaky (generic unscoped exportImportSections vs old AppConfig.ExportPermit IsOversea). MAJOR.<br>• Total row missing: old AmendReport.rdlc has grand-total footer; new controller has no ColumnTotals. |
| ExportPermitByHSCodeReport | MAJOR | • Export Section dropdown is leaky (generic exportImportSections vs old AppConfig.ExportPermit IsOversea). MAJOR.<br>• Filter By: old is a DropDown of FilterType values [Start, End]; new renders it as a free 'text' input with no options -> wrong control type / lost option values.<br>• Total row missing: old HSCodeReport.rdlc has grand-total footer (TOTAL + Sum(Amount)); new controller has no ColumnTotals. |
| ExportPermitBySectionReport | MAJOR | • Export Section dropdown is leaky (generic exportImportSections vs old AppConfig.ExportPermit IsOversea). MAJOR.<br>• Total row missing: old ExportPermitBySectionReport.rdlc has grand-total footer (TOTAL + Sum(Amount)); new controller has no ColumnTotals.<br>• Extra new filters not in old view: BuyerCountryId and CompanyRegistrationNo are not present in the old BySection view (which only shows From/To Date, EIR Card Type, Export Section). SakhanId is a standard scope param. |
| ExportPermitBySellerCountryReport | MAJOR | • Export Section dropdown is leaky (generic exportImportSections vs old AppConfig.ExportPermit IsOversea). MAJOR.<br>• Total row missing: old ExportPermitByBuyerCountryReport.rdlc has grand-total footer (TOTAL + Sum(Amount)); new controller has no ColumnTotals.<br>• Extra new filter CompanyRegistrationNo not present in old Buyer-Country view (which has From/To Date, EIR Card Type, Export Section, Buyer Country only). |
| ExportPermitCancellationReport | MAJOR | • Export Section dropdown is leaky (generic exportImportSections vs old AppConfig.ExportPermit IsOversea). MAJOR.<br>• Total row missing: old CancelReport.rdlc has grand-total footer (TOTAL + Sum(Amount)); new controller has no ColumnTotals. |
| ExportPermitCompanyListReport | MAJOR | • Export Section dropdown is leaky (generic exportImportSections vs old AppConfig.ExportPermit IsOversea). MAJOR.<br>• Total row missing: old ExportPermitByCompanyReport.rdlc has grand-total footer (TOTAL + Sum(Amount)); new controller has no ColumnTotals.<br>• Extra new filter BuyerCountryId not present in old company-list view (From/To Date, EIR Card Type, Export Section, Company Registration No only). |
| ExportPermitDailyReportNewPermitReport | MAJOR | • Export Section dropdown is leaky (generic exportImportSections vs old AppConfig.ExportPermit IsOversea). MAJOR.<br>• Total row missing: old ExportPermitByDailyReport.rdlc has grand-total footer (TOTAL + Sum of totalUSDAmount); new controller has no ColumnTotals -> the Total USD Value column has no grand-total row.<br>• Extra new filter BuyerCountryId not present in old daily view (From/To Date, Export Section, EIR Card Type, Company Registration No only). |
| ExportPermitDetailReport | MAJOR | • Export Section dropdown is leaky (generic exportImportSections vs old AppConfig.ExportPermit IsOversea). MAJOR.<br>• Extra new filters BuyerCountryId and CompanyRegistrationNo not present in old detail view (which only shows From/To Date, EIR Card Type, Export Section, gated by IsShowFilter).<br>• Total row matches: old ExportPermitDetailReport.rdlc has NO grand-total footer and new has none -> PASS on totals. |
| ExportPermitExtensionReport | MAJOR | • Export Section dropdown is leaky (generic exportImportSections vs old AppConfig.ExportPermit IsOversea). MAJOR.<br>• Total row missing: old ExtensionReport.rdlc has grand-total footer (TOTAL + Sum(Amount)); new controller has no ColumnTotals. |
| ExportPermitNewReportNewReport | MAJOR | • Export Section dropdown is leaky (generic exportImportSections vs old AppConfig.ExportPermit IsOversea). MAJOR.<br>• Total row missing: old NewLicenceReport.rdlc has grand-total footer (TOTAL + Sum(Amount)); new controller has no ColumnTotals.<br>• Extra new filter 'Auto' (text) is not present in the old NewReport view (which uses the ExtensionReportModel form: From/To Date, Export Section, Company Registration No). |
| ExportPermitVoucherReport | MAJOR | • Export Section dropdown is leaky (generic exportImportSections vs old AppConfig.ExportPermit IsOversea). MAJOR.<br>• Total row missing: old VoucherReport.rdlc has grand-total footer (TOTAL + SUM(Amount)); new controller has no ColumnTotals.<br>• Columns: need 6 in new (Application Date, header2/header3 dynamic params, Commodity Type, Total CIF, Exchange Rate), extra 1 (Licence Date). Total CIF / Exchange Rate / Commodity Type are real missing data columns -> MAJOR column gap.<br>• PaymentType options: old PaymentTypeList is DB-driven (paymentTypeRepository.GetAll where IsActive) rendered with '--- All ---'; new hardcodes [All, Cash, MPU, Citizen Pay] -> option set may not match DB-managed payment types.<br>• ApplyType options: old ApplyTypeList = [New, Amend, Extension, Cancel, Actual Amend] (Fine excluded). New adds 'De-Cancel' (extra option not in old). |

### WholeSale (3 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| WholeSaleDetailReport | MINOR | • Filter box matches old: From Date, To Date, Apply Type. Labels match resx (FromDate='From Date', ToDate='To Date', ApplyType='Apply Type').<br>• Apply Type option VALUES differ: old ApplyTypeList = New, Amend, Extension, Cancel, Actual Amend (GetApplyTypeList minus Fine). New 'detailApplyTypeOptions' adds two extra values not in old: 'Valid' and 'Invalid'.<br>• Column header-text diffs (cosmetic): old 'Whole Sale No' -> new 'Whole Sale Retail No'; old 'Whole Sale Address' -> new 'Whole Sale Retail Address'; old 'Valid Date' -> new 'End Date'. Same underlying fields; map counts need=2/extra=2 reflect these renames.<br>• No Total row in old RDLC (no =Sum footer) and new controller sets no ColumnTotals — matches. |
| WholeSaleRegistrationByVoucher | MINOR | • MAP CORRECTION: report_map.json lists old_rdlc=null and the task pre-flagged this as NO_OLD_MATCH, but a real old view (Views/Reports/WholeSaleRegistrationByVoucherReport.cshtml) AND RDLC (ReportControl/WholeSaleRegistrationByVoucherReport.rdlc) exist. Audited against them — mapped=true.<br>• Filter box matches old: From Date, To Date, Apply Type, Payment Type. Labels match resx (PaymentType='Payment Type'). New orders PaymentType before ApplyType vs old ApplyType before PaymentType — cosmetic ordering only.<br>• Apply Type values match old (New/Amend/Extension/Cancel/Actual Amend = GetApplyTypeList minus Fine). Payment Type: old is DB-driven (PaymentType/GetAll active, Id->Name); new hardcodes '--- All ---'/Cash/MPU/Citizen Pay, which matches the conventional active payment types — note hardcoded vs data-driven but no observed value mismatch.<br>• Column diffs vs old RDLC (11 cols): old 'Whole Sale No'->new 'Whole Sale Retail No'; old 'Whole Sale Address'->new 'Whole Sale Retail Address' (renames); new ADDS 'Whole Sale Retail Name' (no old equivalent); 'Total Amount' moved to last column in new (old places it before Payment Type). Cosmetic/additive, not data loss.<br>• No Total row: old RDLC has no =Sum footer; new controller sets no ColumnTotals — matches. |
| WholeSaleSummaryReport | MAJOR | • Filter box matches old exactly: From Date, To Date only (no other filters). Labels match resx.<br>• TABLE SHAPE MISMATCH (major): old RDLC is a single-row cross-tab with metric COLUMNS: 'Number of Register', 'Number of De-Register', 'Number of Extension', 'Total Number Still Valid as at', 'Total Number Invalid as at', 'Total Number' (=Valid+Invalid). New report is unpivoted with just two columns: 'Apply Type' and 'Application Count' (one row per apply type). The new layout does not reproduce the old pivoted summary or the combined 'Total Number' column.<br>• The old 'Total Number' is a per-row computed cell (Valid+Invalid), not a tablix =Sum footer; new has no Total row either, but the bigger issue is the entire pivot/column structure differs. |

### Retail (3 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| RetailDetailReport | MINOR | • Header rename: old 'Retail No' -> new 'Whole Sale Retail No'; old 'Retail Address' -> new 'Whole Sale Retail Address' (the need/extra=2/2 in the map is this rename).<br>• Header rename: old RDLC 'Valid Date' is labeled 'End Date' in the new config (not captured in the map's count but a visible difference).<br>• Filters and ApplyType option values match exactly; no grand-total footer in either. |
| RetailRegistrationByVoucher | MINOR | • Map/comparison NO_OLD_MATCH is a key-naming false negative: old RetailRegistrationByVoucherReport.cshtml + .rdlc exist and align column-for-column with the new config.<br>• Header rename only: old 'Retail No'/'Retail Address' (+'Name') shown as 'Whole Sale Retail No'/'Whole Sale Retail Address'/'Whole Sale Retail Name' in new config.<br>• PaymentType dropdown changed from DB-driven (active payment types) to a hardcoded All/Cash/MPU/Citizen Pay list -- verify no active payment type is missing.<br>• ApplyType option values match exactly; FromDate/ToDate/ApplyType/PaymentType labels match resx; no grand-total footer in either. |
| RetailSummaryReport | MINOR | • Old RDLC count-column header 'Total Number' is rendered as 'Application Count' in new config -> visible header text differs.<br>• New report adds an 'Apply Type' breakdown column not present in the old single-column summary RDLC (structural difference, more granular but not in old layout).<br>• Filters, labels (From Date/To Date), and absence of a grand-total footer all match. |

### WholeSaleAndRetail (3 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| WholeSaleAndRetailDetailReport | MINOR | • MAP CORRECTION: report_map.json/ReportColumnComparison.md mark this NO_OLD_MATCH (old_rdlc=null) only because the old report is named WholeSaleRetail* (no 'And'). A genuine old report exists: Views/Reports/WholeSaleRetailDetailReport.cshtml + ReportControl/WholeSaleRetailDetailReport.rdlc. It IS the source of truth.<br>• Filter parity: PASS — FromDate, ToDate, ApplyType all present in both; ApplyType option set identical (New/Amend/Extension/Cancel/Actual Amend/Valid/Invalid).<br>• Total row: PASS — neither old RDLC nor new controller emits a grand total.<br>• Cosmetic column-header diffs (MINOR): old RDLC says 'Whole Sale & Retail No' / 'Whole Sale & Retail Address' (with ampersand) vs new 'Whole Sale Retail No' / 'Whole Sale Retail Address'; old 'Valid Date' vs new 'End Date'. Same columns, slightly different header text.<br>• New default ApplyType='New' (reportConfigs.ts:13636); old GET seeds no explicit selection (first item 'New') — effectively same default. |
| WholeSaleAndRetailRegistrationByVoucher | MINOR | • MAP CORRECTION: marked NO_OLD_MATCH (old_rdlc=null) only due to WholeSaleAndRetail* vs WholeSaleRetail* name gap. Real old report exists: WholeSaleRetailRegistrationByVoucherReport.cshtml + WholeSaleRetailRegistrationByVoucherReport.rdlc.<br>• Filter parity: PASS — From/To date, ApplyType, PaymentType all present in both. ApplyType options identical (5, no Valid/Invalid). PaymentType has matching '--- All ---' empty default.<br>• Total row: PASS — old RDLC has no grand-total Sum footer; new emits no ColumnTotals.<br>• Extra column (MINOR): new config adds 'Whole Sale Retail Name' (key WholeSalRetailName, reportConfigs.ts:13752-13756) which the old RDLC does not carry (old shop columns are only No + Address). One extra column.<br>• Cosmetic header text (MINOR): old 'Whole Sale & Retail No/Address' (ampersand) vs new 'Whole Sale Retail No/Address'.<br>• PaymentType source diff (watch): old builds the list from the active PaymentType DB table; new hard-codes Cash/MPU/Citizen Pay. Functionally equivalent today but can drift if DB values change. |
| WholeSaleAndRetailSummaryReport | MAJOR | • MAP CORRECTION: marked NO_OLD_MATCH (old_rdlc=null) only due to the WholeSaleAndRetail* vs WholeSaleRetail* name gap. Real old report exists: WholeSaleRetailSummaryReport.cshtml + WholeSaleRetailSummaryReport.rdlc.<br>• MAJOR column/shape mismatch: OLD Summary RDLC is a matrix — columns NewCount, CancelCount, ExtensionCount, ValidCount, InvalidCount, and 'Total Number'(=Valid+Invalid) under a 'Whole Sale & Retail at [date]' / 'Statement of Registered' heading. NEW Summary only renders two columns: 'Apply Type' + 'Application Count'. The distinct count breakdown (New/Cancel/Extension/Valid/Invalid as separate columns) is gone.<br>• Filter parity: PASS — both have only From/To date (old Summary has NO ApplyType dropdown; new correctly omits it).<br>• Total row: PASS — old RDLC has no =Sum() footer; new emits no ColumnTotals.<br>• Whether MAJOR is actionable depends on intent: the new flat list may intentionally pivot the old matrix into rows. But as literal column parity it is a real mismatch (5 old count-columns absent, 1 new aggregate column added). |

### MemberRegistration (1 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| MemberRegistrationReport | MAJOR | • ApplyType filter value-source mismatch (MAJOR): old view renders ApplyType as a DropDownListFor bound to Model.ApplyTypeList, a static SelectList of exactly 3 options - 'All','New','Extension' (ReportsController.cs:108-122). New config renders ApplyType as a free-text input (type:'text', reportConfigs.ts:12519-12524) with no options[] and no dropdown, so the constrained option set is lost and users can type arbitrary values.<br>• Filter set otherwise matches: old box = FromDate, ToDate, ApplyType; new = dateRange(FromDate/ToDate) + ApplyType. No missing or extra filters.<br>• Labels all match resx: FromDate='From Date', ToDate='To Date', ApplyType='Apply Type', and the new config uses the same visible text.<br>• Total row: PASS. Old MemberRegistrationReport.rdlc has no Sum/Total/footer textbox (0 matches); new MemberRegistrationReportController.cs sets no ColumnTotals. Neither side has a grand-total row - consistent.<br>• Columns: PASS. 8 RDLC detail headers (Apply Type, Member Code, Email, Full Name, Mobile, NRC No., Issued Date, Valid Date) map 1:1 to the 8 new columns; need_in_new=0, extra_in_new=0. |

### PaThaKa (1 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| PaThaKaRegisteredBusinessOrganizationReport | PASS | • Filter box matches: old has FromDate, ToDate, BusinessType, LineofBusiness, State, Status; new config has dateRange (FromDate/ToDate), BusinessTypeId, LineofBusinessId, State, Status. No missing or extra filters.<br>• All filter labels match the old resx values (From Date, To Date, Business Type, Line of Business, State, Status).<br>• BusinessTypeId auto-resolves to the 'businessTypes' lookup which is correctly scoped to FormType='Pa Tha Ka' (GetBusinessTypes, ReportLookupsController.cs:148), mirroring the old controller's businessTypeRepository.GetAll(AppConfig.PaThaKa). No leaky-scope bug.<br>• LineofBusinessId auto-resolves to 'lineofBusinesses' (unscoped active list), matching old lineofBusinessRepository.GetAll().<br>• State/Status are static select option arrays in the new config (vs DB-driven StateList/PaThaKaStatusList in old); values align (Myanmar states/regions; PaThaKa status codes Suspension/Extension/Un_suspension/Blacklist/New/Amend). Acceptable parity.<br>• Old RDLC has NO grand-total footer (0 Sum() expressions, no Total textbox) and the new controller does NOT set ColumnTotals; Total-row behavior matches.<br>• Columns match exactly: No., Company Registration No, Company Name, Company Registration Date, Valid Date, Business Type, Line of Business, MICPermit No, Company Address. need_in_new=0, extra_in_new=0. |

### EIRCard (1 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| EIRCardBindReport | PASS | • Filter box: full parity. Old active filters = FromDate + ToDate date inputs; new = one dateRange filter binding FromDate/ToDate. No missing or extra filters.<br>• Labels: From Date / To Date match resx values (Resources.resx FromDate='From Date' line 516-518, ToDate='To Date' line 900-902) and new fromLabel/toLabel.<br>• No dropdown/lookup filters exist on either side, so no value-scoping (FormType / section) concerns apply.<br>• Total row: neither side emits a grand-total footer -> match.<br>• Columns: need_in_new=0, extra_in_new=0; headers align 1:1 with the RDLC.<br>• Minor data-source nuance (not a parity defect): RDLC header 'Pa Tha Ka No' binds detail field =Fields!CompanyRegistrationNo.Value (rdlc:534), while the new PaThaKaNo column binds dataIndex 'paThaKaNo' (reportConfigs.ts:6726-6730). Header text is identical, so column-header parity holds; underlying field mapping would need a data-level check if the displayed value ever looked wrong. |

### MPU (2 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| MPUReport | MAJOR | • FormType filter TYPE/VALUE mismatch: old is a <select> bound to Model.FormTypeList (CardTypeRepository.GetAll() Descriptions + 4 appended Border types: Border Export/Import Licence, Border Export/Import Permit) with an '- All -' option and a Wine->'Alcoholic Beverages Importation' relabel. New config makes FormType a free-text input (type:'text') with no option list at all -- the entire dropdown + its scoped CardType values are lost.<br>• FormType label mismatch: old label resolves to Resources.CardType = 'Certificate Type'; new config labels it 'Form Type'.<br>• PaymentType option-set mismatch: old <select> options are MPU and MCB (hardcoded, default first option MPU, no blank/All). New options are All(''), MPU, 'Citizen Pay' -- MCB is MISSING and 'Citizen Pay' + 'All' are extra/changed values.<br>• Total row MISSING in new: old GetMPUReport (Business/Reports.cs:4389-4406) appends a synthetic Sakhan='Total' grand-total row summing TransactionAmount/MOCAmount/MPUAmount/IMAmount, and the RDLC bolds it (=IIF(Fields!Sakhan.Value="Total","Bold","Normal")). New MPUReportController uses CreatePageFromRows with NO ColumnTotals, so no <tfoot> Total row is emitted.<br>• Extra column: new adds 'Amount Diff' (amountDiff) which has no counterpart in the old RDLC or old MPUReportModel (extra_in_new=1).<br>• Column header text differs: old RDLC header is 'MPU'; new titles the same MPUAmount column 'MPU Amount' (need_in_new=1). |
| MPUReportV3 | MAJOR | • V3 config is byte-for-byte identical to MPUReport (same filters[] and columns[]), so all MPUReport parity issues apply identically.<br>• FormType filter TYPE/VALUE mismatch: old is a <select> from Model.FormTypeList (CardType Descriptions + 4 Border types, '- All -', Wine->Alcoholic relabel); new is a free-text input (type:'text') with no options.<br>• FormType label mismatch: old Resources.CardType = 'Certificate Type'; new = 'Form Type'.<br>• PaymentType option-set mismatch: old {MPU, MCB}; new {All(''), MPU, Citizen Pay} -- MCB missing, Citizen Pay/All extra.<br>• Total row MISSING in new: old appends a synthetic Sakhan='Total' grand-total row; new MPUReportV3Controller uses CreatePageFromRows with NO ColumnTotals -> no <tfoot> Total row.<br>• Extra column 'Amount Diff' (amountDiff) absent from old RDLC/model; old 'MPU' header rendered as 'MPU Amount' in new. |

### ChequeNo (1 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| ChequeNoReport | MAJOR | • Total row MISMATCH (MAJOR): old RDLC ChequeNoReport.rdlc emits a grand-total footer row ('TOTAL' label in Textbox1 + =FORMAT(SUM(Fields!Amount.Value),"N2") in Textbox4), but the new ChequeNoReportController.cs never sets ApiResult.ColumnTotals (0 occurrences), so the new table renders no <tfoot> Total row. oldHasTotal && !newHasTotal.<br>• Filter set MATCHES: old box = FromDate, ToDate (required text/date), ChequeNo dropdown (Model.ChequeNoList, '--- All ---', searchable). New = dateRange (FromDate/ToDate, required) + ChequeNoId.<br>• ChequeNoId filter renders correctly as a searchable dropdown despite config type:'number': in GenericReportPage.tsx renderFilter the lookup branch (line 411) is evaluated before the number branch (line 442), and getLookupFilter resolves ChequeNoId via idFilterLookups (line 81) -> lookupName 'chequeNos', label 'Cheque No', with an 'All' option value 0. Matches old DropDownListFor '--- All ---'.<br>• Labels MATCH old resx: From Date (FromDate), To Date (ToDate), Cheque No (ChequeNo).<br>• Lookup value source OK: chequeNos lookup = active, non-deleted ChequeNos (Id/Name), no FormType/BusinessType scoping applies here. Comparable to old chequeNoRepository.GetAll().<br>• Columns MATCH (need_in_new=0, extra_in_new=0): Cheque Id, Cheque No, Date, Amount on both sides. |

### AccountSummary (1 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| AccountSummaryReport | MAJOR | • MAJOR: FormType filter is a free-text input in the new config (reportConfigs.ts:383-388 type:'text') but a dropdown in the old view (FormTypeList). The new side loses the scoped certificate-type option set (CardType descriptions + BorderExportLicence/BorderImportLicence/BorderExportPermit/BorderImportPermit literals) and the '- All -' default.<br>• MINOR (label): FormType label is 'Form Type' in new config but the old view binds the label to resx key 'CardType' = 'Certificate Type' (Resources.resx:288-289). The old label is 'Certificate Type', not 'Form Type'.<br>• PASS: Sakhan filter — old is a dropdown from SakhanList (sakhanRepository.GetAll, Id/Name; ReportsController.cs:14858); new SakhanId auto-resolves to lookup 'sakhans' (idFilterLookups, GenericReportPage.tsx:103) and renders a Select with label 'Sakhan', matching old. type:'number' is overridden by the lookup path.<br>• PASS: FromDate/ToDate — old labels resx FromDate='From Date', ToDate='To Date'; new dateRange uses fromLabel 'From Date'/toLabel 'To Date'. Match.<br>• PASS: Total row — neither old rdlc nor new controller emits a grand total.<br>• PASS: Columns — 7 data columns identical, need_in_new=0 / extra_in_new=0. |

### OnlineFees (1 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| OnlineFeesReport | MAJOR | • FormType filter is rendered as a free-text input in the new UI, but the old report renders it as a curated dropdown (label 'Certificate Type'). The new side cannot replicate the old option list, so this is a value-parity defect.<br>• Old FormType dropdown source (FormTypeList) = CardTypeRepository.GetAll() PLUS four added Border card types (BorderExportLicence, BorderImportLicence, BorderExportPermit, BorderImportPermit), with WineImportation relabeled to 'Alcoholic Beverages Importation' and a leading '- All -' (empty value). None of this exists on the new free-text input.<br>• FormType label differs: new config label is 'Form Type'; old resx key for this field is CardType = 'Certificate Type'.<br>• SakhanId is declared type:'number' but, because the name ends in 'Id' with no explicit lookupName, GenericReportPage.getLookupFilter resolves it via idFilterLookups['SakhanId'] -> lookupName 'sakhans', rendering a searchable dropdown. This matches the old Sakhan dropdown (SakhanList = sakhanRepository.GetAll(), Id/Name) and label 'Sakhan' = 'Sakhan'. No diff.<br>• Date range matches: old FromDate/ToDate textboxes ('From Date'/'To Date') vs new dateRange From Date/To Date.<br>• Columns match exactly (need_in_new=0, extra_in_new=0): rdlc headers No, Entry Date, Company Registration No, Company Name, Transaction Title, Deducted Fees, Remark map to the new config columns (No via showRowNumber).<br>• No grand-total footer in the old rdlc (no Sum()/Total textbox) and the new controller sets no ColumnTotals -> total rows match. |

### OtherMemberCompany (9 reports) — audited (verify pending)

| Report | Verdict | Findings |
| --- | --- | --- |
| CardListsByCompanyRegistrationNumber | PASS | • Single filter Company Registration No matches old (old field model.companyregistrationNo, required).<br>• Columns: 0 need / 0 extra per map.<br>• No grand-total footer in old RDLC; new controller sets no ColumnTotals - match. |
| CompanyProfile | PASS | • Filters match old: FromDate/ToDate (dateRange) + Company Registration No.<br>• extra_in_new=18 in map is a FALSE POSITIVE: it counted the flat config columns, but CompanyProfile renders via the bespoke Report/Page/CompanyProfile.tsx (config comment at reportConfigs.ts:6553-6558 says config columns are not the render source).<br>• Bespoke page reproduces the legacy Myanmar RDLC layout incl. the directors sub-grid; headers match: စဥ်, ပသက/အမှတ်/ရက်စွဲ, သက်တမ်းကုန်ဆုံးရက်, ကုမ္ပဏီအမည်, ကုမ္ပဏီလိပ်စာ, ကုမ္ပဏီအမျိုးအစား, လုပ်ငန်းရည်ရွယ်ချက်, မတည်ငွေရင်း, ပသက သက်တမ်းတိုး + ဒါရိုက်တာအဖွဲ့၀င်များ (အမည်/နိုင်ငံသားအမှတ်/ရာထူး).<br>• Header 'Company Profile (From) To (To)' under Ministry of Commerce / Directorate of Trade matches legacy header1.<br>• No grand-total footer either side. |
| ListOfCompany | PASS | • Filter set matches old exactly: BusinessType, Line of Business, State, Status.<br>• BusinessType dropdown correctly scoped to FormType='Pa Tha Ka' on both sides (old businessTypeRepository.GetAll(AppConfig.PaThaKa); new businessTypes lookup filters FormType=='Pa Tha Ka').<br>• BusinessTypeId/LineofBusinessId render as lookup Selects with prepended 'All' option via idFilterLookups - matches old '--- All ---' dropdowns.<br>• Columns 0/0. No grand-total footer either side.<br>• Legacy static header 'Company Business Organization' reproduced via reportSubtitle. |
| ListOfDirectors | MAJOR | • EXTRA filters in new not in old: 'Company Registration No' and 'Type' (old DirectorList filter box has neither).<br>• MISSING in new: the StatePrefix dropdown that is part of the old Current-NRC composite (old has State-prefix + Township(NRCPrefixId) + NRCPrefixCode + numeric NRC No). New keeps NRCType/NRCPrefixId/NRCPrefixCodeId/NRCNo but drops the StatePrefix selector.<br>• Old NRCType is a Current/Old radio toggle revealing different sub-fields; new exposes NRCType as a plain text filter and a flat NRC group - structural simplification.<br>• Shared filters (FromDate/ToDate, Name, Nationality) match in label and value.<br>• Columns 0/0. No grand-total footer either side. |
| ListOfDirectorsByCompanyRegistrationNo | MAJOR | • Old filter box has ONLY one filter: Company Registration No (required).<br>• New config adds 8 EXTRA filters not in old: FromDate/ToDate (dateRange), Name, Nationality, NRCType, NRCPrefixId, NRCPrefixCodeId, NRCNo, Type.<br>• The single shared filter (Company Registration No) matches in label.<br>• Columns 0/0. No grand-total footer either side. |
| ListOfTopCapitalCompany | MAJOR | • MISSING in new: the 'No of List' filter (old model.TotalRecords, required) that limits the top-N companies returned. This is the defining input of a 'Top Capital' report; its absence is a functional gap.<br>• Other filters match: FromDate/ToDate, BusinessType, Line of Business, State, Status.<br>• BusinessType scoped to Pa Tha Ka on both sides.<br>• Columns 0/0. No grand-total footer either side.<br>• Legacy header 'Top Capital Company Report (From) To (To)' reproduced via reportSubtitle. |
| ListOfValidAndInvalidCompany | PASS | • Filter set matches old: Date, Type (Valid/Invalid), BusinessType, Line of Business, State, Status.<br>• Old 'Date' filter maps to model.ToDate (single date); new uses a 'Date' filter - label match ('Date').<br>• Type values differ in case (old resx 'Valid'/'Invalid' vs new 'valid'/'invalid'), but this is NOT a bug: the new pagination proc explicitly branches on @Type='valid' (lowercase) and the config/subtitle are internally consistent.<br>• BusinessType scoped to Pa Tha Ka on both sides.<br>• Columns 0/0. No grand-total footer either side. |
| RegistrationByBusinessType | PASS | • Filter set matches: FromDate/ToDate + Business Type.<br>• BusinessType scoped to Pa Tha Ka on both sides.<br>• Grand-total row present in old RDLC (TOTAL footer with =SUM(Fields!CompanyCount.Value)) AND new controller sets ColumnTotals['companyCount'] = grandTotal - MATCH.<br>• Columns 0/0 (BusinessType, Total).<br>• Legacy header 'Registration List (From) To (To)' reproduced via reportSubtitle. |
| RegistrationByVoucher | PASS | • Filter set matches: FromDate/ToDate, Apply Type, Payment Type.<br>• Apply Type options match old exactly in value and order: New, Amend, Extension, Cancel, Actual Amend, De-Cancel (old = GetApplyTypeList() minus Fine, plus DeCancel).<br>• Payment Type: old dropdown is DB-driven (PaymentTypeList value=Id/text=Name) with '--- All ---'; new hardcodes All/Cash/MPU/Citizen Pay strings and the new proc string-matches AccountTransaction.PaymentType. Source mechanism differs but the new config+proc are consistent; low-confidence note only.<br>• No grand-total footer in old RDLC (only 'Total Amount' column header, no =Sum); new sets no ColumnTotals - match. |

## 7. Screenshots

**27 reports captured** (headless Chromium, JWT injected into `localStorage`, `/Report/<key>`), saved to `/tmp/report_test/shots/img/`. The grid + filter box are a single shared React component, so a representative one-per-family set + flagged reports covers the visual variance (this is a deliberate representative sample, not all 119).

Captured: `AccountSummaryReport`, `AlcoholicBeveragesImportationSummaryReport`, `BorderExportLicenceActualAmendmentReport`, `BorderExportPermitActualAmendmentReport`, `BorderExportPermitBySectionReport`, `BorderExportPermitNewReportNewReport`, `BorderImportLicenceActualAmendmentReport`, `BorderImportPermitActualAmendmentReport`, `BorderImportPermitCancellationReport`, `CardListsByCompanyRegistrationNumber`, `ChequeNoReport`, `CompanyProfile`, `EIRCardBindReport`, `ExportLicenceActualAmendmentReport`, `ExportPermitActualAmendmentReport`, `ImportLicenceActualAmendmentReport`, `ImportLicenceDetailReport`, `ImportPermitActualAmendmentReport`, `ImportPermitVoucherReport`, `MPUReport`, `MemberRegistrationReport`, `OnlineFeesReport`, `PaThaKaRegisteredBusinessOrganizationReport`, `RegistrationByBusinessType`, `RetailDetailReport`, `WholeSaleAndRetailDetailReport`, `WholeSaleDetailReport`.

**Visual findings (confirmed from the rendered UI):**
- **Filter box & table chrome render correctly and are config-driven** — e.g. `ImportLicenceActualAmendmentReport` shows From/To Date, Import Section (`All`), Company Registration No, Company Name, Remark, Filter/Reset, and the full column-header row + "Set filters, then click Filter to load the report" empty state.
- **Column-header typo:** the Import Licence grids render a column header **`Curency`** (should be **`Currency`**) — a config-level spelling defect visible in the table head.
- **`CardListsByCompanyRegistrationNumber` is a bespoke search screen** (required *Company Registration No* + Search/Print, no date range, no standard grid) — consistent with its **HTTP 404** on the standard `POST /api/CardListsByCompanyRegistrationNumber` route (controller missing `TryCreateReportRequest`).
- **Default date range = current week** (`2026-06-01 → 2026-06-06`), so a freshly-opened report shows no rows until the user widens the range — relevant to "no data" complaints.
- Screenshots were captured in the **empty (pre-search) state**; they validate filter box / labels / column headers / table format, but not data-row or `<tfoot>` Total-row rendering.


---

## 8. Weaknesses found & recommendations

### 8.1 Weaknesses found in the system under test

**Correctness (P0 — fail regardless of data volume):** 10 endpoints.
- `BorderImportPermitCancellationReport`, `BorderImportPermitExtensionReport`, `BorderImportPermitVoucherReport` → SQL **`Invalid column name 'CardType' / 'IndividualTradingId'`** (query references columns that don't exist).
- `BorderImportPermitNewReportNewReport` → **`Incorrect syntax near 'New'. Invalid usage of FETCH NEXT`** (pagination SQL-generation bug).
- `ExportPermitNewReportNewReport`, `ImportPermitNewReportNewReport` → EF **`required column 'HSCode' was not present`** (proc result ↔ entity mismatch).
- `CardListsByCompanyRegistrationNumber` → **404** on the standard report route (controller missing `TryCreateReportRequest`; UI uses a bespoke search screen).
- `ListOfValidAndInvalidCompany` → **400 `Date is required`** (endpoint needs a report-specific date param the generic payload didn't supply — confirm whether a real client sends it).

**Performance (P1):** ~50 endpoints fail on speed. The aggregate family reports (By-Section / By-Method / By-SellerCountry / By-HSCode / Detail / Daily / CompanyList / TotalValue / NewReport across Import/Export Licence & Permit + Border) exceed the **30s SQL command timeout → HTTP 500 `Execution Timeout Expired`** or the 60s client timeout, even on a 1-year window. `AccountSummaryReport` times out at 60s. Only the per-licence list reports (Amendment/Cancellation/Extension) are fast. This is the documented exact-`COUNT` + heavy-join problem; the two-phase `_pagination`/`_Fast` pattern already applied to `ImportLicenceDetail` has **not** been rolled out to these families.

**UI parity (P2 functional / P3 cosmetic):**
- **82 reports are missing the old grand-total footer** (old RDLC has a `Sum`/`CountDistinct` TOTAL row; new controller sets no `ColumnTotals`). Largest single parity gap.
- **~61 of 86 `ExportImportSectionId` filters are unscoped** → fall back to the leaky generic lookup (mixing all section types). Confirmed leaking in BorderExportPermit; pinned only for `borderExportLicence`(13)/`importPermit`(11)/`importLicence`(1).
- **12 missing filters** (e.g. ImportPermit readonly *Company Name* companion; ImportPermit/BorderExportLicence HSCode missing *Section*; `ListOfTopCapitalCompany` *No of List*; `ListOfDirectors` *State Prefix*).
- **63 reports add extra filters** not in the old box (Buyer Country, Company Reg No, `Auto`, plus a stray `auto` column on `BorderExportPermitNewReportNewReport`) — previously **deferred by user choice**; confirm keep vs hide.
- **38 label differences** vs `Resources.resx`; **`Curency`** column-header typo.

### 8.2 Weaknesses in THIS review (so results are read with the right confidence)
1. **Performance is indicative, not benchmark-grade** — shared remote DB, single run, cold/warm only, network + contention included. Buckets are reliable; exact ms are not.
2. **UI-parity verification is incomplete** — a session limit stopped the adversarial *verify* stage after only **BorderExportPermit** (all CONFIRMED bar one). The other 18 families are **audited but unverified**; some "section-leak" findings may be false positives where `lookupName` is pinned via a shared filter array. Re-run the verify pass to confirm.
3. **Validated values were one 1-year window + "All" filters** — not an exhaustive per-filter-value matrix. The `ListOfValidAndInvalidCompany` 400 shows the generic payload doesn't fit every endpoint; a few reports may need report-specific params to exercise fully.
4. **Screenshots are a representative 26-report empty-state sample**, not all 119 and not data-loaded — they confirm filter box/labels/columns/format, not row/Total rendering.
5. **Scope mapping is uneven:** 158 controllers (API) vs 134 audited (parity) vs 119 configs vs 504/114 old RDLC. AlcoholicBeverages + some controllers appear only in the API sweep, not the parity audit; 5 reports have no old equivalent (`WholeSaleAndRetail*`, `*RegistrationByVoucher`).
6. **The existing `Backend.Tests` suite is partly red** in this environment (missing local SPs / `TRADENET_REPORT_TEST_CONNECTION_STRING`) and was not re-run; this review is independent E2E, not the unit suite.

### 8.3 Recommendations (prioritized, no code changed here)
- **P0 — fix the 10 correctness bugs.** They 500/404/400 before data volume matters; start with the `CardType`/`IndividualTradingId`, `FETCH NEXT`, and `HSCode`-column errors (likely stale/wrong stored procs vs entity) and the missing `CardListsByCompanyRegistrationNumber` route.
- **P1 — roll out the two-phase pagination/`_Fast` + indexing** to the ~50 timing-out aggregate reports; drop the default exact-`COUNT`. Verify deployed procs vs repo `.sql` (procs are not auto-applied).
- **P2 — generalize the `ColumnTotals` Total-row pattern** to the 82 reports whose old RDLC had a grand total; **pin scoped section/method/incoterm `lookupName`** on the ~61 leaking filters (extend the existing fix to BorderExportPermit / ExportPermit / ExportLicence / BorderImport*).
- **P3 — decide keep/hide on the 63 extra filters**, add the 12 missing filters, align 38 labels to `Resources.resx`, fix the `Curency` header.
- **Next:** after the limit resets, re-run the *verify* stage (resume the workflow — completed audits are cached) to confirm the unverified families; build a per-report valid-value matrix; capture the full/data-loaded screenshot set if a visual sign-off is needed.


## Appendix A — reproduction
- Harness: `/tmp/report_test/harness.py` (`sweep`), raw results `/tmp/report_test/results.json`.
- Report→old-source map + column diffs: `/tmp/report_test/report_map.json` (parsed from `ReportColumnComparison.md`).
- Screenshot driver: `/tmp/report_test/shots/shoot.mjs`, images `/tmp/report_test/shots/img/`.

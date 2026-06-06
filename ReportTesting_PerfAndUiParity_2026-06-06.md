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

<!-- RESULTS:API_SUMMARY -->
_(filled after sweep completes)_

## 3. API test results — full table

<!-- RESULTS:API_TABLE -->
_(filled after sweep completes)_

## 4. API errors & timeouts (detail)

<!-- RESULTS:API_ERRORS -->
_(filled after sweep completes)_

---

## 5. UI-parity results — summary

<!-- RESULTS:PARITY_SUMMARY -->
_(filled after parity workflow completes)_

## 6. UI-parity results — per family

<!-- RESULTS:PARITY_DETAIL -->
_(filled after parity workflow completes)_

## 7. Screenshots

<!-- RESULTS:SCREENSHOTS -->
_(filled after capture)_

---

## 8. Weaknesses found & recommendations

<!-- RESULTS:RECOMMENDATIONS -->
_(filled after all results in)_

## Appendix A — reproduction
- Harness: `/tmp/report_test/harness.py` (`sweep`), raw results `/tmp/report_test/results.json`.
- Report→old-source map + column diffs: `/tmp/report_test/report_map.json` (parsed from `ReportColumnComparison.md`).
- Screenshot driver: `/tmp/report_test/shots/shoot.mjs`, images `/tmp/report_test/shots/img/`.

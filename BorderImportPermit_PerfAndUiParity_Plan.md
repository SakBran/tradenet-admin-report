# Border Import Permit — Performance & UI-Parity Fix Plan

> **Status:** Plan / instructions for an executing LLM. **No code changed by this document.**
> **Scope (confirmed with user, 2026-06-06):** all **12** Border Import Permit reports; **both** content parity *and* visual parity vs old Tradenet 2.0; performance on **both** initial load and page-to-page paging; the executing LLM **has live-DB access** (pymssql / SSMS).
> **Source of truth for parity:** old Tradenet 2.0 Admin at `/Users/saobranaung/Code/Ministry of Commerce/tradenet-2.0-admin/TradenetAdmin` (`.rdlc` = table columns, `Views/Reports/*.cshtml` + `Controllers/ReportsController.cs` = filter box + dropdown sources).
> **Prior measured evidence:** `ReportTesting_PerfAndUiParity_2026-06-06.md` (family already audited).

---

## ⚡ LIVE VERIFICATION RESULTS — 2026-06-06 (overrides the stale test sweep)

Ran Phase A read-only against the live DB (`TradeNetDB` @ `203.81.66.111,14330`). Findings that **change the plan**:

- **The 4 "500 errors" are ALREADY FIXED on the live DB.** Executed all four procs with `FormType='Border Import Permit'`: `sp_CancelReport_pagination` OK (0 rows, 0.2s), `sp_ExtensionReport_pagination` OK (2 rows, 0.2s), `sp_VoucherReport_pagination` OK (10 rows, 5.1s), `sp_NewReport_pagination` OK (10 rows, 0.4s). No `CardType`, no `FETCH NEXT` error. The deployed Border Import Permit branches are clean and already carry `OPTION(RECOMPILE)`; `BorderImportPermit` has no `CardType`/`IndividualTradingId` column. → **WS-1 collapses to "re-test the 4 API endpoints"; no SQL fix needed.** The test sweep predated the proc deploy.
- **HSCode slowness confirmed real.** Deployed `sp_HSCodeReport_pagination` has **no `OPTION(RECOMPILE)`** and uses **`COUNT(*) OVER()`** → WS-2 stands as the genuine perf work.
- **Voucher ~5s** with `ApplyType='New'` — secondary perf watch item.

**Code changes already applied this session (compile-clean, `dotnet build` 0 errors; not yet deployed/endpoint-tested):**

- ✅ **T3a — Section dropdown scoping.** Added `GetBorderImportPermitSections()` (`Type='Import Permit' AND IsBorder`, mirroring old `GetAll(ImportPermit).Where(IsActive && IsBorder)`) + `"borderimportpermitsections"` dispatch key in `Backend/Controllers/ReportLookupsController.cs`; added `lookupName: 'borderImportPermitSections'` to all **11** Section filters in `reportConfigs.ts` (HSCode has no Section filter — see T3-gap below). Live check: scoped list returns 1 section vs 7 for the leaky generic — leak fixed.
- ✅ **T3c — Missing Total footer (aggregate reports).** Added `includeColumnTotals: true` to `BorderImportPermit{BySection,BySellerCountry,CompanyList,DailyReportNewPermit}ReportController.cs` (matches sibling `ImportPermit*` pattern).

**New gap found:** `BorderImportPermitByHSCodeReport` has **no Section filter at all** in the new config (old HSCode view has a Section dropdown) — a *missing-filter* parity gap, separate from the leak. Add `ExportImportSectionId` (with the scoped lookup) to that report.

---

## ✅ TASK CHECKLIST (execute top-to-bottom; this is the to-do list)

> Each task links to its detailed spec below. **Do WS-0 first (gate), then WS-1.** Before editing anything, confirm the §7 decisions with the user. Tick boxes as you go.

### Phase A — Verify & baseline (GATE — §WS-0)

- [x] **T0.1** Connect to live DB (`ConnectionStrings:TradeNetDBTest`, `203.81.66.111:14330`, `TradeNetDB`). ✅ done (note: pymssql lands in `master` — run `USE TradeNetDB` or 3-part names). → §WS-0
- [x] **T0.2** Dumped `OBJECT_DEFINITION` for the 5 key procs; **none stale** — deployed = repo, already clean. → §WS-0
- [ ] **T0.3** Capture baseline latency for all 12 *endpoints* at page 1 + a deep page (CSV). *(Proc-level timings captured; full 12-endpoint API CSV still TODO.)* → §WS-0
- [ ] **T0.4** Capture HSCode row counts + `sp_helpindex` for `BorderImportPermit`/`BorderImportPermitItem`/`HSCode`. → §WS-0

### Phase B — Fix the 4 hard 500 errors (§WS-1) — ⚠️ ALREADY RESOLVED AT SQL LAYER

- [x] **T1.1** Confirmed: deployed procs are **not** stale — already clean (no `CardType` in Border Import Permit branch, `RECOMPILE` present). → §WS-1
- [x] **T1.2** Not needed — repo `.sql` already matches deployed; all 4 procs execute successfully live. → §WS-1
- [x] **T1.3** N/A — `BorderImportPermit` confirmed to have no `CardType`/`IndividualTradingId`; procs PaThaKa-only and correct. → §WS-1
- [ ] **T1.4** **Re-test the 4 API endpoints** (200 + rows) to confirm the C# layer is also healthy; the proc layer is verified green. → §WS-1

### Phase C — Performance (§WS-2)

- [ ] **T2.1a** `sp_HSCodeReport_pagination.sql:43-44`: add `OPTION (RECOMPILE)`. → §WS-2
- [ ] **T2.1b** Remove `COUNT(*) OVER()` from hot path; honor `@IncludeTotalCount` (sentinel when false, separate scalar when true). → §WS-2
- [ ] **T2.1c** If upstream aggregate still slow: apply page-first pattern. → §WS-2
- [ ] **T2.1d** Add only plan-justified indexes (from T0.4). → §WS-2
- [ ] **T2.1e** Apply the same HSCode fix to the sibling HSCode reports (Import/Export Licence, Export Permit). → §WS-2
- [ ] **T2.2** (Optional) Split catch-all `CASE` predicates only if measurement still shows them as the bottleneck. → §WS-2
- [ ] **T2.3** Re-measure deep paging; pursue keyset pagination only if still painful (decision §7.5). → §WS-2

### Phase D — Content parity, all 12 reports (§WS-3)

- [x] **T3a** Scoped `borderImportPermitSections` lookup added (backend method + dispatch key + `lookupName` on 11 Section filters). ✅ **Still TODO (backend-coordinated):** `BorderImportPermitByHSCodeReport` is *missing* the Section filter, but `sp_HSCodeReport` + its controller request model have **no `@ExportImportSectionId`** — a frontend-only filter would be a dead control. Fixing it requires adding the section param to the proc + `sp_HSCodeReportRequest` + controller, then the scoped filter. Deferred (verified: dead-control reverted). → §WS-3
- [ ] **T3b** Convert free-text → dropdowns: HSCode `FilterType`, Voucher `ApplyType` + `PaymentType`. → §WS-3
- [x] **T3c** `includeColumnTotals: true` added to the 4 aggregate controllers (BySection, BySellerCountry, CompanyList, Daily). ✅ **Still verify:** HSCode path supports it; Daily sums USD; Detail path (decision §7.3). → §WS-3
- [ ] **T3d** Remove stray `Auto` filter + column from NewReport (pending decision §7.1). → §WS-3
- [ ] **T3e** Voucher: resolve `=Parameters!header2/3.Value` to real titles by `ApplyType`. → §WS-3
- [ ] **T3f** Reconcile extra/missing filters per report (present as decisions §7.1). → §WS-3

### Phase E — Visual parity, all 12 reports (§WS-4)

- [ ] **T4.1** Enable `legacyReportViewer` for all 12 reports. → §WS-4
- [ ] **T4.2** Refine legacy CSS/BasicTable: right-align numerics, style `TOTAL` row, title band, `pyidaungsu` font. → §WS-4
- [ ] **T4.3** Match filter-form layout/density to old. → §WS-4
- [ ] **T4.4** Confirm `reportHeading`/`reportSubtitle` text matches old header strings. → §WS-4

### Phase F — Acceptance (§WS-5)

- [ ] **T5.1** Run the per-report checklist (§WS-5) for all 12; record before/after perf numbers; 0 × 500s; HSCode < ~2 s.

**Pre-work decisions (block Phases C–E):** confirm §7 items 1–5 with the user before editing.

---

## 0. The 12 reports in scope

| # | Report (controller) | Proc / data path | Server perf (measured) | UI verdict (audited) |
|---|---|---|---|---|
| 1 | BorderImportPermitActualAmendmentReport | `sp_ActualAmendReport_pagination` | FAST 0.65s | MAJOR |
| 2 | BorderImportPermitAmendmentReport | `sp_AmendReport_pagination` | FAST 0.58s | MAJOR |
| 3 | BorderImportPermitByHSCodeReport | `sp_HSCodeReport` + `sp_HSCodeReport_pagination` | **CRITICAL 23.1s** | MAJOR |
| 4 | BorderImportPermitBySectionReport | `sp_ImportPermitDetailReport_Fast` (aggregate) | OK 1.7s | MAJOR |
| 5 | BorderImportPermitBySellerCountryReport | `sp_ImportPermitDetailReport_Fast` (aggregate) | OK 1.5s | MAJOR |
| 6 | BorderImportPermitCancellationReport | `sp_CancelReport_pagination` | **500 ERROR** | MAJOR |
| 7 | BorderImportPermitCompanyListReport | `sp_ImportPermitDetailReport_Fast` (aggregate) | FAST 0.77s | MAJOR |
| 8 | BorderImportPermitDailyReportNewPermitReport | `sp_ImportPermitDetailReport_Fast` (aggregate) | OK 1.6s | MAJOR |
| 9 | BorderImportPermitDetailReport | `sp_ImportPermitDetailReport_Fast` (paged) | OK 1.9s | MAJOR |
| 10 | BorderImportPermitExtensionReport | `sp_ExtensionReport_pagination` | **500 ERROR** | MAJOR |
| 11 | BorderImportPermitNewReportNewReport | `sp_NewReport_pagination` | **500 ERROR** | MAJOR |
| 12 | BorderImportPermitVoucherReport | `sp_VoucherReport_pagination` | **500 ERROR** | MAJOR |

Nav grouping: `Frontend/src/Report/reportNavItems.tsx:82-88` (`controllerName.startsWith('BorderImportPermit')`).
Config blocks: `Frontend/src/Report/config/reportConfigs.ts` lines **5160–6431** (one object per report).

---

## 1. Problem statement (validated, with corrections)

The user's three complaints are confirmed, with two important corrections discovered during verification.

### 1a. "Data fetching is so slow" — confirmed, concentrated, not uniform
- Most of the family is actually fast (0.5–2 s) in the test sweep. The genuine outlier is **`BorderImportPermitByHSCodeReport` = 23.1 s cold / 0.46 s warm** (`ReportTesting_PerfAndUiParity_2026-06-06.md:134`). The ~50× cold/warm gap = **parameter-sniffing** + a materialize-everything pagination shape.
- **Verified root cause** in `StoredProcedureMigrations/sp_HSCodeReport_pagination.sql:43-44`:
  - Uses `COUNT(*) OVER() AS TotalCount FROM #r ... OFFSET (@pi*@ps) ROWS FETCH NEXT @ps ROWS ONLY` — `COUNT(*) OVER()` forces SQL Server to materialize **every** matching row even for a single 10-row page (the same antipattern the repo's own pagination guide warns cost 146 s elsewhere — `docs/LinqToStoredProcedurePaginationTask.md:56-92`).
  - **No `OPTION(RECOMPILE)`.** Commit `cab341c` (today) added RECOMPILE to 10 procs (ActualAmend / Amend / Cancel / Extension / New / Voucher / ImportLicenceDetail / ImportLicencePendingDetail / MPU / Pending) but **skipped `sp_HSCodeReport_pagination`**.
  - The wrapper first `EXEC sp_HSCodeReport` into temp table `#r` (the heavy `GROUP BY`/`SUM` aggregate), then paginates — so the cost is the upstream aggregate, run in full, every call.

### 1b. "UI doesn't fit old Tradenet looks" — confirmed on BOTH axes
All 12 reports are MAJOR. Two distinct axes (user wants both fixed):
- **Content parity** (filters / dropdown values / columns / headers / totals) — see §4.
- **Visual parity** (the RDLC viewer look: bordered grid, grey header band, bold `TOTAL` footer, right-aligned numerics, Burmese `pyidaungsu` font, Bootstrap-style filter form) — see §5. A `legacyReportViewer` mode already exists in the new UI and is the lever for this.

### 1c. "Pagination still not good" — confirmed, two sub-causes
- **Hard error:** `BorderImportPermitNewReportNewReport` returns `Incorrect syntax near 'New'. Invalid usage of the option NEXT in the FETCH statement` — paging is fully broken for that report (see correction below).
- **Cost:** `COUNT(*) OVER()` on HSCode and deep `OFFSET` re-scans. The fast list procs already got RECOMPILE today; HSCode and the aggregate path did not.

### ⚠️ CORRECTION 1 — the 4 "500 errors" are **deployed-proc drift, NOT source bugs**
The sub-agents hypothesized the proc `.sql` files reference a non-existent `CardType` column in the Border Import Permit branch and recommended "rewrite the branch." **I verified the actual SQL — this is wrong and that rewrite would corrupt correct code:**
- In `sp_CancelReport_pagination.sql` the Border Import Permit branch is **lines 421–477** and is clean: it joins `BorderImportPermit → PaThaKa → ExportImportSection → Sakhan`, filters `ApplyType='Cancel' AND Status='Approved'`, has proper `OFFSET/FETCH` and `OPTION(RECOMPILE)`. **No `CardType`, no `IndividualTradingId`.**
- Same for `sp_NewReport_pagination.sql` (Border branch **455–515**), `sp_ExtensionReport_pagination.sql` (branch **413–468**), `sp_VoucherReport_pagination.sql` (branch **559–630**).
- `CardType` appears **only** in the *Border Export Licence* and *Border Import Licence* branches — whose tables genuinely have that column.
- **Conclusion:** the repo `.sql` already fixes these. The 500s come from an **older proc version still deployed on the live DB** (classic drift trap — see memory `deployed-proc-drift-and-db-access`). **The fix is to re-apply the current `.sql` to the live DB and re-test — not to edit the source.** (Mandatory live-DB confirmation in WS-1.)

### ⚠️ CORRECTION 2 — `ColumnTotals` flag is a one-line controller fix, but the Detail/HSCode paths differ
- The aggregate controllers omit `includeColumnTotals: true`. **Verified:** `BorderImportPermitBySectionReportController.cs:43-44` calls `CreateAggregateResultAsync(...)` **without** the flag, while the sibling `ImportPermitBySectionReportController.cs:40-42` passes `includeColumnTotals: true`. Same gap on BySellerCountry, CompanyList, Daily.
- But `BorderImportPermitDetailReportController.cs:46` uses `CreatePagedResultAsync` (not the aggregate path) and `BorderImportPermitByHSCodeReportController.cs:40` uses `sp_HSCodeReport.CreateAggregateResultAsync(...)` (a different class) — **do not blindly add the flag**; confirm each overload supports it (§4c).

---

## 2. Ground rules the executing LLM MUST follow

1. **Parity check before patching (project rule).** For each report, compare the *new* config/controller against the *old* `.cshtml` + `.rdlc` + `ReportsController.cs` on two axes — filter box (filters **and** option values) and table columns (text + language) — and report diffs before changing. Ref `CLAUDE.md`, `AGENTS.md`, memory `customer-complaint-report-parity-check`.
2. **Never modify an original stored procedure.** Paginated reports use a `sp_*_pagination` copy in `StoredProcedureMigrations/`; the original `sp_*` is untouched. `docs/StoredProcedureDefinitions.sql` is a read-only snapshot. Ref memory `sp-pagination-conversion-convention`, `docs/LinqToStoredProcedurePaginationTask.md:28-40`.
3. **Treat deployed procs as possibly stale.** A `.sql` file in the repo is the canonical version but may NOT be what's running. **Always** `SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.<proc>'))` on the live DB and diff against the repo `.sql` *before* concluding a bug exists or a fix is deployed. Ref memory `deployed-proc-drift-and-db-access`.
4. **Shared-proc blast radius.** `sp_NewReport_/AmendReport_/CancelReport_/ExtensionReport_/VoucherReport_/ActualAmendReport_pagination` and `sp_HSCodeReport*` and `sp_ImportPermitDetailReport_Fast` each serve **8 FormType families** (Import/Export Permit & Licence, plus all 4 Border variants). Any edit must be re-tested for **every** FormType, not just Border Import Permit.
5. **BusinessType / PaThaKaType dropdowns must be scoped** (`FormType='Pa Tha Ka'`); Section dropdowns must be scoped to `ImportPermit + IsActive + IsBorder`. Ref memory `report-header-and-businesstype-conventions`, `import-licence-dropdown-and-total-rootcauses`.
6. **Measure before and after** every perf change against the live DB (recipe in §6). Record numbers; don't claim a speedup you didn't time.
7. **Build after backend edits:** `dotnet build Backend/API.csproj`. **Type-check after frontend edits.** Don't trust a stale build (known trap).
8. **Do not auto-remove "extra" filters** (Seller Country, Company Reg No, etc.) — several were *intentionally* added and deferred by user choice. Flag them as decisions (§7), don't silently delete (except the clearly-stray `Auto`, which still needs confirmation).

---

## 3. Execution order (workstreams)

Do them in this order. WS-0 is a hard gate; WS-1 unblocks 4 broken reports cheaply; WS-2 fixes the one truly slow report; WS-3/WS-4 are the bulk parity sweep.

```
WS-0  Connect to live DB, snapshot deployed procs, capture perf baselines   (GATE — do first)
WS-1  Fix the 4 hard 500s by re-deploying current procs (drift)             (cheap, high impact)
WS-2  Performance: HSCode + pagination/index work                          (the real "slow")
WS-3  Content parity (filters, values, columns, totals) vs old             (12 reports)
WS-4  Visual parity (RDLC look) via legacyReportViewer + CSS               (12 reports)
WS-5  Per-report verification & acceptance                                 (sign-off)
```

---

## WS-0 — Live-DB verification & baseline (GATE)

**Goal:** know the real deployed state and capture before-numbers so every later claim is measurable.

- **T0.1** Connect to the live DB. Connection string `ConnectionStrings:TradeNetDBTest` in `Backend/appsettings.json` (server `203.81.66.111:14330`, db `TradeNetDB`, `TrustServerCertificate=True`). Confirm network access. *(Open Q: confirm this is the environment the user actually hits — §7.)*
- **T0.2** For each proc the family uses (`sp_CancelReport_pagination`, `sp_ExtensionReport_pagination`, `sp_VoucherReport_pagination`, `sp_NewReport_pagination`, `sp_ActualAmendReport_pagination`, `sp_AmendReport_pagination`, `sp_HSCodeReport`, `sp_HSCodeReport_pagination`, `sp_ImportPermitDetailReport_Fast`): dump `OBJECT_DEFINITION(OBJECT_ID('dbo.<proc>'))` and **diff against the repo `.sql`**. Record which are stale.
- **T0.3** Capture baseline latency for all 12 endpoints at **page 1** (`PageIndex=0, PageSize=10`, `includeTotalCount:false`) and a **deep page** (`PageIndex=100`), with a realistic date window, using the harness approach in `ReportTesting_PerfAndUiParity_2026-06-06.md` §1.2 (`/tmp/report_test/harness.py`). Save a CSV.
- **T0.4** For HSCode, also capture row counts: `SELECT COUNT(*) FROM BorderImportPermit WHERE ApplyType='New' AND Status='Approved' AND CreatedDate BETWEEN @From AND @To` and `sp_helpindex 'BorderImportPermit'` (and `BorderImportPermitItem`, `HSCode`) — to know if the bottleneck is cardinality or missing indexes.

**Exit:** a table of {proc → stale? }, a baseline-latency CSV, current index inventory.

---

## WS-1 — Fix the 4 hard 500 errors (deployed-proc drift)

**Hypothesis (verified in source, confirm on live DB):** the repo procs are correct; the live DB runs older buggy copies.

- **T1.1** From WS-0, confirm the deployed `sp_CancelReport_pagination` / `sp_ExtensionReport_pagination` / `sp_VoucherReport_pagination` Border Import Permit branches reference `CardType`/`IndividualTradingId` (the bug), while the repo `.sql` branches do **not** (verified clean: Cancel 421-477, Extension 413-468, Voucher 559-630, New 455-515).
- **T1.2** If stale: **re-apply the current repo `.sql`** for those 4 procs to the live DB (`CREATE OR ALTER`, idempotent). Do **not** edit the `.sql`.
- **T1.3** If — and only if — the deployed proc already matches the repo `.sql` and the error persists, then the bug is genuinely in source; re-open root-cause:
  - `CardType` case: confirm whether `BorderImportPermit` has `CardType`/`IndividualTradingId` columns (`SELECT * FROM sys.columns WHERE object_id=OBJECT_ID('BorderImportPermit')`). The repo branch is PaThaKa-only, which is correct per the old source (§4a) — so a source bug here is unlikely.
  - `'New' / FETCH NEXT` case: the repo New branch is clean. If deployed differs, re-deploy. If not, check what `@SortColumn` value the controller sends (`sp_NewReport.cs`); `@ob` is whitelisted (`sp_NewReport_pagination.sql:26-27`) and defaults safely, so a malformed `ORDER BY ... OFFSET ... FETCH NEXT` would come from a deployed copy missing that guard.
- **T1.4** Re-test the 4 endpoints (200, rows render). **Then re-test the same proc for every other FormType** (Import/Export Permit & Licence, Border Export Permit, Border Import/Export Licence) to prove no regression (blast radius rule).

**Exit:** all 4 reports return 200; sibling FormTypes unaffected.

---

## WS-2 — Performance

### T2.1 `sp_HSCodeReport_pagination` — the 23 s report (highest perf priority)
File `StoredProcedureMigrations/sp_HSCodeReport_pagination.sql:43-44`. Apply, measuring after each step:
1. **Add `OPTION (RECOMPILE)`** to the paginated SELECT (matches the cab341c pattern in the sibling procs). Kills parameter-sniffing → expected to crush the cold/warm gap.
2. **Drop `COUNT(*) OVER()` from the hot path.** Honor `@IncludeTotalCount`: when false (the BasicTable default), fetch `@PageSize+1` as a next-page sentinel and return `NULL` TotalCount; when true, compute count as a **separate scalar** over the base set (pattern: `docs/LinqToStoredProcedurePaginationTask.md:56-92`). This is the single biggest win — it stops materializing the full result for a 10-row page.
3. If the upstream `EXEC sp_HSCodeReport` (the heavy `GROUP BY` into `#r`) is itself slow, evaluate **page-first**: filter+page the base rows before the per-row currency/amount correlated subqueries (`docs/LinqToStoredProcedurePaginationTask.md:71-75`, which took Import Licence Detail 25s→2s).
4. **Indexes** (from WS-0 plan analysis): candidate composite on `BorderImportPermit(ApplyType, Status, CreatedDate)` (+ `SakhanId`, `ExportImportSectionId`), `BorderImportPermitItem(BorderImportPermitId)`, join keys on `HSCode`/`Sakhan`. Add only what the actual execution plan shows missing; weigh write cost.
5. **Apply the same RECOMPILE + count fix to the sibling HSCode reports** that also time out (`BorderImportLicenceByHSCodeReport`, `BorderExportLicenceByHSCodeReport`, `ExportLicenceByHSCodeReport`, `ExportPermitByHSCodeReport` — same shared proc) once the Border Import Permit one is proven.

### T2.2 Catch-all predicate sargability (broader, optional)
The `col = (CASE WHEN @x=0 THEN col ELSE @x END)` pattern across these procs is non-sargable (defeats index seeks when a filter is "all"). With `OPTION(RECOMPILE)` the optimizer handles each parameter set better; only split into `IF @x=0 … ELSE …` branches if measurement shows it's still the bottleneck. Don't over-engineer.

### T2.3 Deep paging
`OFFSET (@pi*@ps)` re-scans skipped rows. After T2.1, re-measure deep pages from WS-0. Only pursue keyset pagination (a frontend-breaking change) if deep paging is still a real user pain — defer otherwise.

**Exit:** HSCode page-1 cold load < ~2 s; deep-page latency ≈ page-1; before/after numbers recorded; sibling FormTypes still correct.

---

## WS-3 — Content parity (filters, values, columns, totals)

Source of truth per report in §0 + old `ReportsController.cs` (~lines 13089–14421) and the `.rdlc` headers. Recurring fixes:

### T3a. Scope the Section dropdown (all 12 reports) — **filter-value fix #1**
**Problem (verified):** every report's `ExportImportSectionId` filter has **no `lookupName`**, so `GenericReportPage` auto-resolves it via `idFilterLookups` (`GenericReportPage.tsx:77-105`) to the generic **`exportImportSections`** (all sections). Old scopes to `ExportImportSectionRepository.GetAll(AppConfig.ImportPermit).Where(IsActive && IsBorder)` (old `ReportsController.cs:13089-13155`).
**Fix:**
1. Backend: add a scoped lookup endpoint `GET /ReportLookups/borderImportPermitSections` returning sections where `Type='ImportPermit' AND IsActive=1 AND IsBorder=1` (mirror however `importPermitSections` is implemented — used by ImportPermit reports at `reportConfigs.ts:10708,10816,11002`). For Check/Approve users, also restrict to the user's `GetSections(ImportPermit, Border)` codes (old `ReportsController.cs:22-53`).
2. Frontend: add `lookupName: 'borderImportPermitSections'` to the `ExportImportSectionId` filter in all 12 config blocks (start at `reportConfigs.ts:5160`).

### T3b. Convert free-text filters to scoped dropdowns — **filter-value fix #2**
| Report | Filter | New (wrong) | Old source (target) |
|---|---|---|---|
| ByHSCode | `FilterType` | `type:'text'` (`reportConfigs.ts:5420-5424`) | `CommonRepository.GetFilterType()` → **Start / End** dropdown |
| Voucher | `ApplyType` | `type:'text'` (`~6348-6351`) | `GetApplyTypeList()` minus `Fine` → New/Amend/Extension/Cancel/Actual Amend |
| Voucher | `PaymentType` | `type:'text'` (`~6342-6345`) | `PaymentTypeRepository.GetAll().Where(IsActive)` |
Convert each to a Select (add a `lookupName` + backend lookup, or a static `options` array for the fixed Start/End and ApplyType sets). Verify `PaThaKaTypeId`, `SakhanId`, `AmendRemarkId`, `SellerCountryId` already resolve to dropdowns whose option source matches old (PaThaKaType=`Description`, Sakhan/Section/Country/PaymentType/AmendRemark=`Name`).

### T3c. Grand-total footer (`ColumnTotals`) — **table-structure fix**
Old RDLCs have a `TOTAL` footer (Sum of Amount/Value, and `totalUSDAmount` on Daily). New omits it. The BasicTable renders a footer **only if the backend returns `columnTotals`** (`BasicTable.tsx:456-488`).
- **Aggregate reports — add `includeColumnTotals: true`** to the `CreateAggregateResultAsync(...)` call (the verified one-liner), matching `ImportPermitBySectionReportController.cs:40-42`:
  - `BorderImportPermitBySectionReportController.cs:43-44`
  - `BorderImportPermitBySellerCountryReportController.cs:43-44`
  - `BorderImportPermitCompanyListReportController.cs:43-44`
  - `BorderImportPermitDailyReportNewPermitReportController.cs:43-44` (must sum **TotalValue and Total USD Value** — see memory `daily-report-usd-value-fx-conversion`)
- **HSCode** (`BorderImportPermitByHSCodeReportController.cs:40`): uses `sp_HSCodeReport.CreateAggregateResultAsync` — **confirm that overload supports `includeColumnTotals`** before adding; if not, extend it like the `sp_ImportPermitDetailReport_Fast` path.
- **List procs** (New/Amend/Cancel/Extension/Voucher/ActualAmend): old RDLCs for these **do** have a currency-grouped `TOTAL`. These go through the pagination procs (not the aggregate path), which return only `TotalCount`. Decide per report whether to compute footer sums server-side; verify against each `.rdlc` whether a footer is expected (e.g. ActualAmend/Amend/Cancel/Extension/New/Voucher all have one per the audit). *(Detail report: the two audits disagree on whether `BorderImportPermitDetailReport.rdlc` has a Sum footer — **verify the RDLC directly** before adding/omitting.)*

### T3d. Remove the stray `Auto` (NewReport)
`reportConfigs.ts:6247-6250` (filter) and `~6304-6307` (column, `title:'auto'`). The proc returns `CAST(NULL AS nvarchar(50)) auto` (always null) and old NewReport has no such field. **Recommend removal** (confirm as a decision — §7).

### T3e. Voucher dynamic headers — **table-column / header-text fix**
`reportConfigs.ts:~6389-6396` ship literal SSRS titles `'=Parameters!header2.Value'` / `'=Parameters!header3.Value'`. Old resolves these at runtime by `ApplyType` (old `ReportsController.cs:14271-14295`):
| ApplyType | header2 | header3 |
|---|---|---|
| New | Licence No | Licence Date |
| Amend | Licence Amendment No | Amendment Date |
| Extension | Licence Extension No | Extension Date |
| Cancel | Licence Cancel No | Cancellation Date |
| Actual Amend | Licence Actual Amendment No | Actual Amendment Date |
Make the new column titles resolve from the selected `ApplyType` (dynamic title, or sensible default if "All").

### T3f. Reconcile extra/missing filters per report (decision-gated)
Using §0 + old views, list per report: extra-in-new (e.g. `SellerCountryId`, `CompanyRegistrationNo` on BySection/CompanyList/Daily; `Auto` on New) and missing-in-new. **Present as decisions** (§7) — hide for strict parity vs keep as intentional additions.

---

## WS-4 — Visual parity (make the React UI look like the RDLC viewer)

The new UI is Ant Design; the old is an RDLC report viewer. A **`legacyReportViewer`** mode already exists and is the lever (`BasicTable.tsx:307-315`, CSS `style.css:146-193`), currently enabled only for ImportLicence reports.

**Old visual spec (target):** bordered grid, every cell `1pt solid black`; header band + column headers `LightGrey` background, **bold, centered**, `pyidaungsu` 12 pt; numeric/currency columns **right-aligned**; bold **`TOTAL`** footer styled like the header; report title band spanning all columns; filter form = labels-above-inputs, primary-blue **Search** button, responsive columns. (Old refs: `BorderImportPermitDetailReport.rdlc`, `BorderImportPermitByDailyReport.rdlc`, `Views/Reports/BorderImportPermit*.cshtml`, `Content/css/style.css`.)

- **T4.1** Enable `legacyReportViewer` for all 12 Border Import Permit reports (set the flag in their `ReportPageConfig`; see how ImportLicence sets it).
- **T4.2** Refine the legacy CSS / `BasicTable` to close gaps vs the spec: right-align numeric columns (per-column alignment, currently all left), style the `TOTAL` row (`report-total-row`) bold with header-like shading, render `reportHeading`/`reportSubtitle` as the centered title band, set the report-table font to `pyidaungsu` (Burmese support). Files: `BasicTable.tsx:319-321,456-488`; `style.css` legacy block `146-193`; theme `App.tsx:32-94`.
- **T4.3** Filter-form layout: match old grouping/labels. Current grid is 4-per-row (`GenericReportPage.tsx:748`, `lg={6}`); adjust to match old density if needed. Keep Search/Reset + the Excel button.
- **T4.4** Confirm `reportHeading`/`reportSubtitle` text matches the old header strings (e.g. *"List of Border Import Permit By Section From (FromDate) To (ToDate)"*).

---

## WS-5 — Per-report verification & acceptance

For **each** of the 12 reports, confirm and tick:

- [ ] Loads 200; renders rows; paging works (page 1 + deep page).
- [ ] **Section dropdown** shows only ImportPermit + Border sections (not all).
- [ ] All filters present that the old has; option **values** match old source; no un-asked-for extras (per §7 decisions).
- [ ] Columns match the `.rdlc` (text + language); no stray columns; no literal `=Parameters!…`.
- [ ] `TOTAL` footer present where the old `.rdlc` has one (and sums the right columns incl. USD on Daily).
- [ ] Visual: bordered grid, grey bold centered headers, right-aligned numerics, bold TOTAL, pyidaungsu, title band.
- [ ] Excel export still works (async job queue — memory `excel-job-queue-convention`).
- [ ] Sibling FormTypes of any shared proc/controller touched still pass.

Acceptance: HSCode cold < ~2 s; 0 × 500s; all 12 PASS the parity checklist; before/after perf numbers recorded.

---

## 6. Live-DB verification & measurement recipe (reference)

1. Connect via `ConnectionStrings:TradeNetDBTest` (`Backend/appsettings.json`), `TrustServerCertificate=True`.
2. Drift check: `SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.<proc>'))` → diff vs `StoredProcedureMigrations/<proc>.sql` (ignore `GO`/whitespace).
3. Apply a proc: run the full `.sql` (`CREATE OR ALTER`, idempotent) via pymssql/sqlcmd/SSMS; strip trailing `GO` if executing as one batch.
4. Time an endpoint: POST with real filters, `PageIndex/PageSize`, `includeTotalCount` both ways; record latency, rows, TotalCount presence.
5. Plan analysis: `SET STATISTICS IO ON; SET STATISTICS TIME ON;` + actual execution plan for the slow proc with realistic params.
6. Index inventory: `sp_helpindex '<table>'`.
7. After backend code edits: `dotnet build Backend/API.csproj`. Re-time. Re-confirm deployed proc via OBJECT_DEFINITION.

(Full step-by-step in the `db-verify` findings; conventions in `docs/LinqToStoredProcedurePaginationTask.md`, `docs/ExcelJobQueueTask.md`.)

---

## 7. Decisions needed from the user (do not guess)

1. **Extra filters** — keep or hide for strict parity? `SellerCountryId` (BySection, CompanyList, Daily), `CompanyRegistrationNo` (BySection, BySellerCountry, Detail), and the `Auto` field (NewReport). Several were intentional additions previously deferred.
2. **List-report totals** — old `.rdlc`s for New/Amend/Cancel/Extension/Voucher/ActualAmend show a currency-grouped `TOTAL`; these run through pagination procs that return no aggregate. OK to add server-side footer sums to those procs, or leave totals to aggregate/summary reports only?
3. **Detail-report total** — the two audits conflict on whether `BorderImportPermitDetailReport.rdlc` has a Sum footer; confirm expected behavior (verify against the RDLC).
4. **Environment** — is `203.81.66.111:14330 / TradeNetDB` the same instance the user sees as "slow"? If prod differs, point the perf/drift work there.
5. **Keyset pagination** (WS-2 T2.3) — pursue now (frontend change) or defer unless deep paging stays painful?

---

## 8. Risks & guardrails

- **Shared procs (8-family blast radius):** every proc edit (HSCode, any list proc) must be regression-tested for all FormTypes. Highest risk in WS-2.
- **Deployed drift:** never assume `.sql` == deployed. Re-verify after deploying.
- **`OPTION(RECOMPILE)` CPU:** acceptable trade for correctness vs param-sniffing; watch warm-cache CPU under concurrency.
- **Index write cost:** add only plan-justified indexes; prefer selective/filtered indexes.
- **Don't "fix" correct code:** the 4 errors are drift — re-deploy, don't rewrite the source branches (verified clean).
- **Stale build trap:** rebuild FE/BE before retesting.

---

## 9. File / line index (verified anchors)

**New frontend**
- `Frontend/src/Report/config/reportConfigs.ts:5160-6431` — 12 BorderImportPermit blocks. Section filter missing `lookupName`; FilterType/ApplyType/PaymentType `type:'text'`; NewReport `Auto` filter `6247-6250` + column `6304-6307`; Voucher literal headers `~6389-6396`.
- `Frontend/src/Report/Page/GenericReportPage.tsx:77-105` (idFilterLookups), `:382-391` (getLookupFilter), `:404-471` (renderFilter), `:725-732` (heading/subtitle), `:748` (filter grid).
- `Frontend/src/components/My Components/Table/BasicTable.tsx:307-315` (legacy class), `:319-321` (title), `:456-488` (totals footer), `:491-511` (pagination).
- `Frontend/src/components/My Components/Table/style.css:110-209` (table + legacy CSS); `Frontend/src/App.tsx:32-94` (theme).

**New backend**
- Controllers: `Backend/Controllers/Report/BorderImportPermit*ReportController.cs` (12). Aggregate ones missing `includeColumnTotals:true` at `:43-44`; sibling pattern `ImportPermitBySectionReportController.cs:40-42`; Detail uses `CreatePagedResultAsync` `:46`; HSCode `:40`.
- Procs (Border Import Permit branches **verified clean**): `sp_CancelReport_pagination.sql:421-477`, `sp_NewReport_pagination.sql:455-515`, `sp_ExtensionReport_pagination.sql:413-468`, `sp_VoucherReport_pagination.sql:559-630`.
- **Perf target:** `sp_HSCodeReport_pagination.sql:43-44` (`COUNT(*) OVER()`, no `OPTION(RECOMPILE)`).
- RECOMPILE already added by `cab341c` to: ActualAmend/Amend/Cancel/Extension/New/Voucher/ImportLicenceDetail/ImportLicencePendingDetail/MPU/Pending pagination procs (**not** HSCode).

**Old Tradenet 2.0 (source of truth)** — `/Users/saobranaung/Code/Ministry of Commerce/tradenet-2.0-admin/TradenetAdmin/`
- `Controllers/ReportsController.cs` ~`13089-14421` (filter sources, section scoping `22-53`, Voucher headers `14271-14295`).
- `Views/Reports/BorderImportPermit*.cshtml` (filter boxes); `ReportControl/BorderImportPermit*.rdlc` + shared `Border{New,Amend,Cancel,Extension,Voucher,HSCode}Report.rdlc` (columns + TOTAL footers).

**Docs / memory**
- `ReportTesting_PerfAndUiParity_2026-06-06.md` (measured perf §2-4; family audit §6 lines 405-420).
- `docs/LinqToStoredProcedurePaginationTask.md`, `docs/ExcelJobQueueTask.md`.
- Memory: `deployed-proc-drift-and-db-access`, `pagination-count-recompile-timeout`, `sp-pagination-conversion-convention`, `import-licence-dropdown-and-total-rootcauses`, `report-header-and-businesstype-conventions`, `daily-report-usd-value-fx-conversion`, `excel-job-queue-convention`, `customer-complaint-report-parity-check`.

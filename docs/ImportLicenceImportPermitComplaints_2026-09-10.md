# Import Licence / Import Permit complaint round — 2026-09-10

Branch `fix/import-licence-round-2026-09-10`, off `main` @ `521715d`.

Eight customer complaints. Every one was reproduced against **production**
(`reportapi.myanmartradenet.com`, hand-minted JWT) *before* any code changed, so each fix
below has a measured before-number rather than an inference from an audit document.

| # | Complaint | Root cause | Status |
| --- | --- | --- | --- |
| 1 | Total Value & Licences — `Excel Report ထုတ်၍မရပါ` | Fixed on 2026-09-03 by `8130333`; two latent defects remained | Fixed |
| 2 | `Import Licence Detail Report` menu နှစ်ခုဖြစ်နေတယ် | Drill-only config listed in the sidebar | Fixed |
| 3 | By Method — `No of licences` Total | Footer removed wholesale on 2026-09-03; legacy prints a count | Fixed |
| 4 | 2.15 By Section — same | same | Fixed |
| 5 | 2.16 By Seller Country — same | same | Fixed |
| 6 | 2.17 Company List — same | same | Fixed |
| 7 | Border Import Licence By HS Code — extra `HS Code Detail Report` menu | Drill config aliases the summary's `controllerName` → duplicate menu key | Fixed (5 families) |
| 8 | Import Permit Cancellation — Currency/Total Value show `N/A` | Cancellation record holds no items; lookup keyed on it | Fixed |

---

## 1. Total Value & Licences — Excel export

**The complaint was already fixed.** Commit `8130333` (2026-09-03) added
`IExcelReportLayoutProvider` to the controller. Before that,
`RequireExcelPresentationSpecFilter.cs:43-55` rejected the page's spec-less POST with HTTP
400, and the page `await`ed that POST with no `try/catch` — so the click produced an
unhandled rejection and *nothing at all* happened. Production evidence that it now works:
two jobs completed on 2026-09-10 (`2947` and `3001` bytes) and the downloaded `.xlsx`
parses with the correct title, both sections and the right numbers.

Two real defects remained, and both are fixed here:

**(a) The button never handed over a file, and swallowed errors.** The four bespoke
`*TotalValueLicencesReport.tsx` pages were the only pages posting to an `/Excel` route by
hand, bypassing `Report/excel/excelEnqueue.ts` — the module whose own header says it is
"the one place that posts an Excel export job". Nothing anywhere polled, so an export of
an open period (and the first export of *any* period) ended at
`"Export queued. It will appear in Exports when ready."`

- All four pages now call `enqueueExcelExport`, whose `spec` parameter became optional —
  the filter only demands a spec from controllers that have no typed layout.
- `enqueueExcelExport` now follows a queued job via `GET ExcelExport/{id}` (1 s interval,
  60 s budget) and downloads it, so the button delivers the file. **Every** report gets
  this, not just these four.
- Polling never throws: the job is already queued, and `BasicTable.tsx:443-447` turns a
  throw into "Failed to generate Excel file", so a transient status-read blip must not
  report a successful export as failed. Anything unresolved falls through to the old
  Exports message.
- New tests: `Frontend/src/Report/excel/excelEnqueue.test.ts` (7).

**(b) The footer probe was a wasted heavy query and a hard failure mode.** The four
controllers did not implement `IExcelNoFooterReport`, so `ExcelFooterTotalsResolver.cs:82`
replayed `Post` — a second `GetTotalValueLicencesSummaryAsync`, three more DB round trips —
and since `FooterTotals` defaults to `Required` (`ExcelExportOptions.cs:6-11`;
`appsettings.json` sets no key) a hiccup in that replay failed the whole export. The
report's own layout test already asserts the probe can never yield a footer
(`ImportLicenceTotalValueLicencesReportControllerLayoutTests.cs:119-136`). All four now
carry the marker. Sheet bytes are unchanged, so **no** `ExcelFormatVersion` bump.

## 2 + 7. The two menu complaints — one mechanism

`reportNavItems.tsx:211-215` builds a menu row's key **and** its URL from
`config.controllerName`, not from the config key. That is the whole of both complaints.

- **#2:** `ImportLicenceDetailByLicenceReport` (`reportConfigs.ts:9855`) carries the title
  `Import Licence Detail Report`, identical to the real `ImportLicenceDetailReport`. Its
  own comment says it is the licence-level drill target of the four By-X summaries, and
  `docs/ReportAndLinqMappingList.md:66-85` lists that report exactly once.
- **#7:** five `*HSCodeDetailReport` configs deliberately set `controllerName` to their
  summary's (same endpoint), so each rendered a second row with the **same antd key and
  the same link**. That is why the customer saw it appear on clicking into the report
  (`SideNav.tsx:70-77` auto-opens the group) and why both rows highlighted at once
  (`SideNav.tsx:118`). The row was also broken: it navigated to the summary.

The existing `hiddenReportKeys` deny-list could not express #7 — it matches on
`controllerName`, which these share with the summary that must stay visible. So
`ReportPageConfig` gained `hideInMenu?: boolean`, set on all six configs, and
`navReportConfigList` filters on it. Every config and route is untouched, because the
drill-downs navigate by config key (`GenericReportPage.tsx:868`).

Fixed in all five families, not just the reported one: Border Import Licence, Border
Export Licence, Border Export Permit, Border Import Permit, Export Licence.

New test `Frontend/src/Report/reportNavMenu.test.ts` — including
`has no two leaves sharing a key`, the assertion that would have caught this class of bug.
All 10 of its tests fail on `521715d` and pass here.

## 3–6. The count-only Total footer

The old RDLCs print `TOTAL` + `=CountDistinct(Fields!LicenceNo.Value)` with the Total
Value **and** Currency cells blank: `ImportLicenceBySectionReport.rdlc:837/891/945/997`,
`ImportLicenceByMethodReport.rdlc:835/889/943/995`,
`ImportLicenceBySellerCountryReport.rdlc:836/890/944/996`,
`ImportLicenceByCompanyReport.rdlc:835/889/943/995` (read via `git show origin/master:` —
the local checkout is a stale 2022 branch). So the customer is asking for legacy parity.

The 2026-09-03 removal (`badeed9`) took the whole row when only the money cell was
meaningless — the same over-removal the Import Permit round already corrected. Restored
with the existing `ReportColumnTotalsMode.CountOnly`, now threaded through
`sp_ImportLicenceDetailReport_Fast.CreateAggregateResultAsync`. Each controller:
`includeColumnTotals: true, columnTotalsMode: CountOnly`, `IExcelNoFooterReport` **removed**
(it would keep the count out of the sheet), `[ExcelFormatVersion(2)]` → `(3)` so the queue
cannot serve a cached footer-less file. `ImportLicenceBySectionReportController` also moved
onto `CreateAggregateResultAsync` so all four are identical.

**No distinct-count query was needed, and adding one would have been a mistake.** The
Export/Import Permit families need a separate `COUNT(DISTINCT LicenceNo)` because a permit
can span two currencies and so appears in two rows. Import Licence does not. Measured on
production for 2025, `SUM(noOfLicences)` over **all** rows is `65,452` for every dimension
— By Section (28 rows), By Method (31), By Seller Country (173), Company List (5,080, paged
through in full) — and equals the distinct licence count from the Total Value report's Pa
Tha Ka split (`6 + 65,051 + 394 + 1`). The alternative, borrowing the Export Permit
`Rows(db, request)…Distinct().CountAsync()` line, would have hit the 6.4M-row
`ImportLicenceItem` join that `sp_ImportLicenceSummaryReport_Indexed.sql:11-18` records as
timing out past 180 s.

Frontend: no change. The footer is entirely payload-driven (`BasicTable.tsx:519-527`), the
column already uses `dataIndex: 'noOfLicences'`, and `BasicTable.tsx:790-796` puts the
`TOTAL` label in the first column with no total — the dimension column, as the RDLC does.

## 8. Import Permit Cancellation — the `N/A` cells

Production, 2023 → 2026-09-10: **10 of 20 rows** returned `currency: null` and
`amount: null`, which the grid renders as the literal `N/A` (`BasicTable.tsx:485` for the
text column, `GenericReportPage.tsx:161-163` for the money column). The footer was wrong
the same way, emitting `{"currency": "", "noOfLicences": 2, "totalValue": 0}`.

Not a DTO/casing bug: `sp_CancelReportResult.Currency`/`.Amount` (`sp_CancelReport.cs:37,39`)
camel-case to exactly the `currency`/`amount` the config reads. The values were genuinely
NULL, because **an Import Permit cancellation record carries no `ImportPermitItem` rows of
its own** and the lookups were keyed on it (`sp_CancelReport_pagination.sql:48-55`,
`WHERE ImportPermitItem.ImportPermitId = pg.__k_Id`). The `ISNULL(…,0)` there sits *inside*
the `TOP 1`, so it guards a null column, not an empty result set.

The data exists on the parent: for the N/A row `OVSIP42425C000005`, its `oldLicenceNo`
`OVSIP42324000449` returns USD / HS `2939119000` / 6,100 and USD / HS `2933599000` / 4,600
from the Import Permit Detail report on production.

There was no legacy behaviour to copy. The old app's `GetCancelReport`
(`Business/Reports.cs:2006`) does `decimal.Parse(row["Amount"].ToString())` inside a
catch-all returning an empty list, so a single NULL amount made the **whole** old report
render zero rows. The owner chose to populate the cells from the cancelled permit.

**Fix.** Both procedures resolve the item through
`COALESCE(<the cancellation's own items>, <the items of the permit it cancels>)`, matched
`OldImportPermitNo` → `ImportPermitNo` and guarded by an `EXISTS` so a parent with no items
cannot mask a cancellation that has its own, and both take the item with
`ORDER BY ImportPermitItem.Id` — the key the footer branch already used, so a row and its
footer can no longer disagree. Only the `@FormType = N'Import Permit'` and
`@DbApplyType = N'Cancel'` branches change. `[ExcelFormatVersion(3)]` on the controller.

Scope confirmed on production, not assumed: Export Permit, Export Licence and Import
Licence Cancellation each return **0** NULL rows (their cancellation records do carry
items), and Border Import Permit Cancellation has no rows at all.

**Verified locally end to end**, since production cannot be written to. The two procedures
were created on the Docker SQL Server and run against a seeded reproduction of the three
production shapes:

| row | before | after |
| --- | --- | --- |
| `…C000005` — cancellation empty, parent has 2 items | `NULL / NULL / NULL` | `USD / 2939119000 / 6100.00` |
| `…C000001` — cancellation has its own item | `USD / 6203430000 / 5841.00` | unchanged |
| `…C000099` — no items anywhere | `NULL / NULL / NULL` | unchanged (no error) |

Footer, same data: **before** `"" : 2 / 0.0000` + `USD : 1 / 5841.0000` — byte-identical to
what production returns today — and **after** `USD : 2 / 11941.0000` + `"" : 1 / 0.0000`,
i.e. exactly the sum of the two grid rows. The generated dynamic SQL was also extracted and
`SET PARSEONLY`-checked (2,952 chars, far below the 4,000 truncation threshold).

New guard: `Backend.Tests/ImportPermitCancellationItemSourceContractTests.cs` (5 tests,
all failing on `521715d`). There was no `ImportPermitItemOrderContractTests` before, so
nothing in CI would have noticed this drift.

---

## Deployment

**The two procedures must be applied by hand** — the auto-deploy watcher ships the
application only. Run
`StoredProcedureMigrations/Deployments/2026-09-10_ImportPermitCancellationItemSource/00_RunAll.sql`
against **TradeNetDB** *before* the application build. Procedures first, application second.

Note the deployed backend was at `f6fc344` when this was written (from the export jobs'
`processedBy` stamp), so `243b20c` is not live yet either.

## Test state

- Frontend `src/Report`: **1552 passed, 6 failed** vs a `521715d` baseline of
  **1535 passed, 6 failed** — the same 6 pre-existing failures in
  `reportConfigs.{borderExportPermit,borderImportLicence,exportLicence}.test.ts`; +17 new
  tests, no new failures.
- Backend (`Category!=LiveDatabase`): the only difference from the baseline is that the
  **three `DeploymentFolderContractTests` now pass**. They were red on `main` for a
  vacuous reason — the `Done For Fix` folder move left no dated folder for
  `DeploymentFolders()` to find, so the contract was asserting nothing. Adding this
  release's folder restores it.
- The baseline was taken with `git worktree add --detach`, per
  `concurrent-peer-sessions-edit-repo`.
- The DB-bound suites were not run, per the owner's standing decision.

## Production verification — all done, 2026-09-10 23:00–23:05

Merged as `a770009`; the auto-deploy shipped the API and then the frontend (bundle
`index-uhsnmlV_.js`). Export jobs now stamp `processedBy … @a770009`, confirming the build.

**#3–#6 footers, all four dimensions:**

| report | rows | `columnTotals` |
| --- | --- | --- |
| ImportLicenceByMethodReport | 31 | `{"noOfLicences": 65452}` |
| ImportLicenceBySectionReport | 28 | `{"noOfLicences": 65452}` |
| ImportLicenceBySellerCountryReport | 173 | `{"noOfLicences": 65452}` |
| ImportLicenceCompanyListReport | 5080 | `{"noOfLicences": 65452}` |

No `totalValue` key on any of them, so the money cell stays blank as the RDLC has it, and
the count matches the independently derived distinct-licence total.

**#8 Import Permit Cancellation** — the procedures were applied (note: `deploy.ps1` and
`tools/auto-deploy-watch.ps1` contain no SQL step, so this was a manual run, not the
pipeline; the hand-deploy note above still stands for future rounds):

- NULL-currency rows: **0 of 20**, from 10 of 20.
- `OVSIP42425C000005` → `USD` / `2933599000` / `4,600`. Note this is a *different* item
  from the one the local reproduction predicted (`2939119000` / `6,100`): both are that
  parent's real items, and `ORDER BY ImportPermitItem.Id` takes the lowest actual Id, which
  a seeded fixture cannot know. Deterministic, which is the property that matters.
- Footer `USD 16 / 680,812.773`, `EUR 3 / 9,994.16`, `CNY 1 / 2,857,800`; no `""` line;
  `grandTotalLicences` 20 = `totalCount`. Recomputing the per-currency sums from the 20 grid
  rows reproduces all three **to the cent** — grid and footer now agree by construction.

**#2 + #7 menu** — the live bundle carries 7 `hideInMenu` references: the 6 flagged configs
plus the `reportNavItems` filter. All five `HS Code Detail Report` configs are still present
(hidden, not deleted), so their drill-downs keep working.

**#1 Excel** — enqueued a real export through the API and polled it exactly as the shipped
`waitForJob` does: `Queued` → **`Completed` after 1 second**, so the 1 s first tick catches
it. The downloaded file (3,051 bytes, 22 rows) has the right title
(`Import Licences Total Value & Licences (01/01/2025) To (31/03/2025)`) and both sections.
The old UI stopped at that `Queued` response and never fetched the file.

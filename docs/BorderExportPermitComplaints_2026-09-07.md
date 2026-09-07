# Border Export Permit — customer complaints, 2026-09-07

Source: owner message, two reports, both searched **1-Jan-2025 → 6-Sep-2026**:

1. **Border Export Permit By HS Code Report** — "record မကိုက်ပါ" (the records do not match the old report).
2. **Border Export Permit Voucher Report** — "Excel တွင် Total ပေါင်း က တစ်မျိုးပြနေပါတယ်. UI ကအတိုင်းပြပေးပါရန်"
   (the Excel Total is a different figure from the UI; make it match the UI).

Both were **measured first** against production (`reportapi.myanmartradenet.com`, minted JWT — see
`docs/BorderImportPermitComplaints_2026-09-05.md` for the harness) and against the **customer's own
`.xlsx` files**, which the shared Exports drive still held (`GET /api/ExcelExport/jobs` → download).

| Report (customer window) | UI / API today | customer's Excel (07/09/2026 09:41 and 09:46) | old Tradenet 2.0 screen |
|---|---|---|---|
| By HS Code | 8 rows, footer 6 licences (border tables) | 8 rows, footer 6 | oversea `Export Permit` rows (850 company-split rows / 1,892 licences on the oversea endpoint) |
| Voucher (ApplyType blank → New) | 6 rows, footer `amount` **18,000** | 6 rows, footer **THB:4,762,475.0000 / USD:64,857.0180 / JPY:1,437,000.0000 + "Total:6 licence(s)"** | `TOTAL` + `FORMAT(SUM(Amount),"N0")` = 18,000 |

## 1. By HS Code — the old screen lists OVERSEA Export Permits

Legacy `ReportsController.cs:14120` (`origin/master`) sets `model.FormType = AppConfig.ExportPermit`
("Export Permit") on the **Border** Export Permit By HS Code screen and posts it back through
`@Html.HiddenFor(model => model.FormType)` (`Views/Reports/BorderExportPermitByHSCodeReport.cshtml:21`).
`dbo.sp_HSCodeReport`'s `Export Permit` branch reads `ExportPermit`/`ExportPermitItem`, windows on
`LicenceDate`, has **no section parameter** and ignores `@SakhanId` — the old Sakhan and Export Section
boxes were decorative. `BorderHSCodeReport.rdlc` then groups the rows on (HSCodeId, Currency)
(rdlc:1159-1162) with no sort over a result the procedure returns `ORDER BY HSCode.Id`, prints
`Sr.No.` / `CountDistinct(LicenceNo)` / `FORMAT(Sum(Amount),"N4")`, a `TOTAL` row of
`CountDistinct(LicenceNo)`, and the HS Code cell opens `BorderHSCodeDetailReport` in a new window
(rdlc:581). This is the **same defect the Border Import Permit twin had**, for which the owner chose
bug-for-bug parity on 2026-09-05 ("Same Result ပဲထွက်ရမယ်"); the doc above (Round 2) recorded the Export
twin as the follow-up. The new report read the `BorderExportPermit` tables (8 rows), hence "records
don't match".

**Changed (C#, config and tests only — no SQL):**

* `BorderExportPermitByHSCodeReportController` sends `FormType = "Export Permit"`, `LegacyOrder = true`,
  maps the drill's `GroupBy = 'Company'` to `GroupByCompany`, and no longer maps
  `ExportImportSectionId` (the old procedure never received it). `[ExcelFormatVersion(2)]` because the
  row set changed for an unchanged payload.
* `sp_HSCodeReport.cs`: `ExportPermitRows` now fills the legacy ordering keys (`PermitCreatedDate`,
  `PermitId`), and `AggregateQuery` decides `LegacyOrder` **before** `GroupsByCompany` — for the Export
  Permit source the default shape is the company split that the oversea `ExportPermitHSCodeDetailReport`
  renders, and the legacy summary must not inherit it. The oversea `ExportPermitByHSCodeReport` is
  untouched (pinned by a test).
* `reportConfigs.ts`: summary and drill get `defaultPageSize: 1000`, `rowNumberTitle: 'Sr.No.'`; Total
  Value is `money` + `#,##0.0000` (rdlc:713 N4); No of Licences is `number`; the HS Code drill opens in a
  new tab; the drill config posts the pinned `GroupBy: 'Company'` (same `constantValue` mechanism as the
  Import twin) and keeps the legacy drill header verbatim — `"List of Export Permits By HS Code From (…)
  To (…)"`, because the old `BorderHSCodeDetailReport` builds `header1` from the posted FormType.
* Tests: `BorderExportPermitByHSCodeLegacyParityTests` (routing, oversea-filter equality, drill flag, both
  grouping keys and the legacy ORDER BY via `ToQueryString()`, oversea report untouched);
  `ReportControllerBranchDefaultsTests.LegacyFormTypeOverrides`; frontend
  `reportConfigs.borderExportPermit.test.ts`; Excel spec fixtures regenerated.
* Also repaired: `BorderImportPermitByHSCodeLegacyParityTests.Summary_builds_the_same_procedure_request…`
  had been red since the Import twin gained `LegacyOrder` (2026-09-06) — it compared the presentation
  flags too.

**What this proves / does not.** By construction the Border report is now the oversea query regrouped the
way `BorderHSCodeReport.rdlc` does. The harness check after deploy is: Border By HS Code rows ≡ the
oversea `ExportPermitByHSCodeReport` rows re-grouped on (HS code, currency) — 1,892 distinct licences for
the customer window on both. The legacy row order inside an HS code (`ORDER BY HSCode.Id`, ties in
arrival order) is reproduced as (`HSCodeId`, first `CreatedDate`, first `Id`); the old order of ties is
not provable from code. Not changed: the oversea `ExportPermitByHSCodeReport` still company-splits its
summary, which `HSCodeReport.rdlc` (HSCodeId + Currency) does not — a separate, latent parity gap.

## 2. Voucher Excel — a second Excel worker on a stale build

The customer's file (job `24c017c8`, created 2026-09-07 03:16Z) carries the **pre-2026-09-04**
footer: per-currency sums of the permit **goods value** (`sp_ExportPermitVoucherCurrencyTotals`) plus
"Total:6 licence(s)", the exact shape commit `6d75eed` removed. Yet the API answering the grid is the
new build (`columnTotals.amount = 18000`, five consecutive POSTs). Two probe exports settled it:

| probe | created | footer |
|---|---|---|
| `01ea1895` | 04:05:07Z | `TOTAL … 18000` (new build) |
| `2da7bdc8` | 04:07:50Z | THB/USD/JPY goods-value rows + "Total:6 licence(s)" (**old build**) |

Same API, 2½ minutes apart, opposite results ⇒ **two `ExcelExportWorker` processes claim jobs from the
same TemplateDB queue, and one of them runs a build from between 2026-09-03 (`d434f2d`, the file's
`cellXfs count="11"` style table) and 2026-09-04 16:23 (`6d75eed`)**. Which host is it:

```sql
SELECT Id, ReportKey, CreatedAtUtc, LeaseOwner   -- LeaseOwner = "<MachineName>:<guid>"
FROM ExcelExportJobs
WHERE Id IN ('24c017c8-4d7f-4ae9-9006-4deb94d58cb7', '2da7bdc8-f9fb-4a03-b568-84c4b4b4010a', '01ea1895-bce4-4062-bb95-cef0fad92ad6');
```

(The two probe jobs were deleted through the API afterwards; their rows are gone, the customer's row
remains.) Candidates: the former UAT site (`P:\WEBSITES\tradenet-admin-backend`, still pointed at the
same TemplateDB) or a `dotnet run` left running on the Build Server. Until that process is stopped or
re-pointed, every export in the system is a coin toss between two builds — **this is an operations fix,
not a code fix.**

**Changed in code (defensive):**

* `GET /api/ExcelExport/jobs` and `/{id}` now expose `processedBy` (= `LeaseOwner`) so the Exports drive
  can name the host that produced a file.
* `BorderExportPermitVoucherReportController` → `[ExcelFormatVersion(3)]`: the wrong files were
  enqueued by the new build under the **v2** hash and the period is closed, so a re-export with the
  same filters would have been served the poisoned file for 24h. v3 invalidates it.
* The grid prints the fee with `numberFormat: '#,##0'` (rdlc:1631 / :1808 `FORMAT(…,"N0")`), and the
  Excel writer gained the matching `Integer` cell style (`#,##0`, numFmt 168; styles 11 = cell, 12 =
  bold TOTAL). Before this the sheet showed `18000` where the grid shows `18,000` — the same number,
  but not the same string. `rowNumberTitle: 'No.'` (rdlc:261).

**Filter box vs the old form (unchanged, for the record):** old ApplyType list is New / Amend /
Extension / Cancel / Actual Amend (no "--- All ---", no De-Cancel; `Fine` filtered out), defaulting to
New; ours offers "--- All ---" (mapped to New by the controller) and De-Cancel. Payment Type is a lookup
of active payment types in the old form, a static list here. Column headers per ApplyType match the old
`header2`/`header3` literals exactly ("Licence Amendment No", "Licence Cancel No", …); the frontend test
that expected "Amendment No"/"Cancellation No" was wrong and is corrected.

## Test baseline

`dotnet test` with the mandatory DB-skip filter (`docs/ExcelParity/Prompts/_gate-common.md` §3a):
1,690 run, **10 failures, all pre-existing** — the seven `TryCreateReportRequest` /
payload-fixture failures already in `known-failures.json`, and the three `DeploymentFolderContractTests`
("Collection was empty") that fail since the deployment folders moved under
`StoredProcedureMigrations/Deployments/Done For Fix`. Frontend `vitest src/Report`: the three failures
left in `reportConfigs.borderExportPermit.test.ts` are the unrelated pre-existing ones (Cancellation
filter list, action-report subtitle wording, `sakhans` lookup on the New report). `tsc --noEmit` clean.
An `Integer`-column workbook was generated locally and read back with openpyxl: `number_format ==
'#,##0'` on the cells and on the bold TOTAL cell.

## Verification against the OLD report (owner's concern 2026-09-07: "is it really the same result?")

Plan: `~/.claude/plans/make-sure-with-harness-elegant-minsky.md`. Three layers; A is the owner's part.

### Layer B — legacy SQL vs the new code on the SAME database (UAT `203.81.66.111`) — ALL PASS

The new backend (this branch) was run locally against UAT (`scratchpad/run_local_api.sh`: report
queries → UAT `TradeNetDB`, export queue → UAT `TemplateDB`, local file store) and driven by the
harness with the grid's own request shape (`sortColumn` = the config's `initialSortColumn`). The
oracle (`scratchpad/legacy_oracle.py`) is the legacy `dbo.sp_HSCodeReport` **query text verbatim**
from `docs/StoredProcedureDefinitions.sql:4727-4737` / `:4743-4754` — not the UAT procedure, which was
ALTERed into the paginated aggregate on 2026-06-01 (`sys.procedures.modify_date`) and no longer returns
`SectionCode`/`HSCodeId`/`Amount`/`LicenceNo`, so **the old By HS Code screen prints "no data" on any
database where that ALTER ran** — check production (step 0 in the plan). `dbo.sp_VoucherReport` is
intact (2024-10-03) and is EXECed as the voucher oracle. RDLC shaping replayed in Python; the same
comparison is now a permanent test, `Backend.Tests/BorderExportPermitLegacyParityLiveDbTests.cs`
(`TRADENET_REPORT_TEST_CONNECTION_STRING`; 5/5 green against UAT, runnable on the Build Server against
production).

| comparison (UAT) | legacy | new | verdict |
|---|---|---|---|
| By HS Code, 01/01/2025–06/09/2026 | 356 (HS code, currency) rows, TOTAL 1,153 | 356 / 1,153, every `No of Licences` and `Total Value` (4 dp) equal | PASS; order differs only inside HS code 9018190000 (EUR/USD tie) |
| By HS Code, 2025 | 353 / 1,147 | 353 / 1,147 | PASS, order identical |
| Sakhan = 5, Export Section = 1 | (old boxes were dead) | identical rows and TOTAL to Sakhan 0 / Section 0 | PASS |
| Drill 0713319050 / 3702529000 / 0201300000 | 10 rows/28, 16/49, 1/1 companies | same rows, names, counts | PASS; company order inside an HS code differs |
| Voucher, 01/01/2025–06/09/2026 | 10 rows, TOTAL 778,000, ORDER BY PaymentDate | 10 rows, 778,000, same order | PASS |
| Voucher, 2025 | 6 rows, 18,000 | 6 / 18,000, same order | PASS |
| Excel (local worker) voucher | — | 10 rows in PaymentDate order, `Total Amount` cells and bold TOTAL 778,000 with `#,##0` | PASS |
| Excel (local worker) By HS Code | 356 / 1,153 | 356 rows = legacy set, `Sr.No.`, Total Value `#,##0.0000`, footer 1,153 | PASS |

**Row order inside one HS code is not reproducible from the old code.** The legacy query is `ORDER BY
HSCode.Id` only; the order of a code's rows (which decides which currency / company the RDLC prints
first) is whatever the join plan emits. Measured on UAT over the 6 multi-currency codes: our tie-break
(first permit `CreatedDate`, then `Id`) reproduces 5; no candidate key (`CreatedDate`, permit `Id`,
`LicenceDate`, `ApplicationDate`, item `Id`, `ItemNo`, `LicenceNo`) explains all rows' arrival order.
Sets and figures are identical; the order of ties is documented as unprovable (same caveat as the
Border Import Permit twin).

**Two defects found by testing the grid's real request shape:**

* **Production Border Import Permit By HS Code has answered HTTP 500 since the 2026-09-06 legacy-order
  deploy.** The grid posts `sortColumn: 'SakhanId'` (its `initialSortColumn`); `ReportAggregateResult`
  has no such property and `ApiResult.ApplySort` throws `NotSupportedException` for an unknown column on
  every LINQ path (measured 2026-09-07: `BorderImportPermitByHSCodeReport` 500 for every request;
  `BorderImportLicenceByHSCodeReport` and `ExportLicenceByHSCodeReport` 500 as soon as a section is
  chosen; the procedure path ignores the column, which is why nobody noticed earlier). Fix in this
  branch: `sp_HSCodeReport.GridSortColumnOrNull` — an unknown sort column means "no explicit sort", a
  real column (a header the user clicked) is still honoured. Regression test
  `The_grids_initial_SakhanId_sort_does_not_break_the_legacy_path`.
* The voucher grid and sheet defaulted to `ApplicationNo` order; the old report is `ORDER BY
  AccountTransaction.PaymentDate`. `initialSortColumn: 'Date'` and the Excel path now sort by `Date`;
  measured on UAT: only `Date` reproduces the old order (10/10 rows).

### Layer A — the old report on production data (owner)

Owner decision 2026-09-07, after Layer B: **merge to `main` now, ahead of Layer A** (the production
Border Import Permit By HS Code 500 above weighed in favour of shipping). Still owed by the owner: the
SSMS check (`sys.procedures` for `sp_HSCodeReport`/`sp_VoucherReport` on production — if
`sp_HSCodeReport` is the altered aggregate, the old By HS Code screen has printed "no data" since June
and only an export taken before then can serve as Layer A for that report) and the ReportViewer Excel
exports of both old reports for 01/01/2025–06/09/2026 into `scratchpad/oldreport/`.
`scratchpad/compare_old_vs_new.py --api` then diffs them against the **deployed**
`BorderExportPermitByHSCodeReport` / drill / voucher directly (reference figure: 494 rows / 1,892
licences from the production oversea endpoint on 2026-09-07).

### Layer C — production self-consistency after merge (harness)

`BorderExportPermitByHSCodeReport` ≡ oversea `ExportPermitByHSCodeReport` re-grouped (rows, footer),
drills ≡ oversea company rows per HS code, Sakhan/Section dead, voucher 6 / 18,000 in PaymentDate order,
then Excel once the stale second worker is stopped.

## Deployment

No stored-procedure change. Merged to `main` on 2026-09-07 (`--no-ff` merge of
`fix/BorderExportPermit`) at the owner's instruction → the Build Server watcher deploys backend +
frontend. Then: Layer C with `scratchpad/layer_c.py` against production (`deploy_probe.py` tells when
the new build is live: the Import twin answers 200 to the grid's request, By HS Code lists the oversea
rows, `processedBy` appears on the jobs API), stop the stale worker (above), re-export the voucher for
the customer's window.

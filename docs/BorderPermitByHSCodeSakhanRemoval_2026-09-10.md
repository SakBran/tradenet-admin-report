# Border Import / Export Permit By HS Code — the Sakhan box that never filtered

Customer complaint, 2026-09-10, two reports:

> **Border Import Permit By HS Code Report** — 1-May-2024 to 6-Sep-2026 နဲ့ sakhan ကိုရွေးရှာလဲ
> all အတိုင်းပဲ ၁၀၁၄ ပဲ ကျပါတယ်။ Sakhan Filter တွ အလုပ်မလုပ်တာ
>
> **Border Export Permit By HS Code Report** — … 494 ပဲ ကျပါတယ်။ Sakhan Filter တွ အလုပ်မလုပ်တာ

Branch `fix/border-permit-hscode-sakhan-removal`. No SQL, no stored-procedure deploy, no row-set change.

## The complaint is accurate

Measured on live PROD (`reportapi.myanmartradenet.com`, hand-minted JWT — see the JWT harness note):

| Report | window | Sakhan = All | Myawaddy (5) | Tachileik (4) | Muse (1) |
|---|---|---|---|---|---|
| `BorderImportPermitByHSCodeReport` | 2024-05-01 → 2026-09-06 | 1,014 rows / 1,328 permits | identical | identical | identical |
| `BorderExportPermitByHSCodeReport` | 2024-05-01 → 2026-09-06 | 578 rows / 2,637 permits | identical | identical | — |
| `BorderExportPermitByHSCodeReport` | 2025-01-01 → 2026-09-06 | **494** rows / 1,892 permits | identical | identical | — |

The customer's `494` is the 1-Jan-2025 window, not the 1-May-2024 one they quoted.

**The Import / Export Section dropdown is equally inert.** `ExportImportSectionId` 0, 1, 2, 3 and 10
(10 being the only value `borderImportPermitSections` actually returns) all give the same 1,014 rows.
If it filtered at all, the non-existent ids would have returned zero.

## Why it cannot work on this data

- The **old** Tradenet 2.0 Border screens post the *oversea* form type — `model.FormType =
  AppConfig.ImportPermit` (legacy `ReportsController.cs:15464`) and `AppConfig.ExportPermit`
  (`:14119`) — through a hidden field.
- `dbo.sp_HSCodeReport`'s `'Import Permit'` / `'Export Permit'` branches read the oversea
  `ImportPermit` / `ExportPermit` tables and never reference `@SakhanId`
  (`docs/StoredProcedureDefinitions.sql:4723-4823`). The proc *does* have correct, Sakhan-aware
  `'Border Import Permit'` / `'Border Export Permit'` branches (`:5015`, `:5068`) — nothing in the old
  admin ever reaches them.
- We reproduce that deliberately (owner decisions 2026-09-05 and 2026-09-07, "same result as the old
  report"), so our Sakhan box was inert for the same reason the old one was.
- `Backend/Model/TradeNet/ImportPermit.cs` and `ExportPermit.cs` have **no `SakhanId` property at
  all**. Only the `Border*` entities do. There is nothing on these rows to filter by.

The old screen's own RDLC is the tell: `BorderHSCodeReport.rdlc:56-59` declares a `SakhanId` field
that no cell ever renders, because the non-border row mapper never populates it.

## Two escape hatches, both rejected

**Port of discharge.** `PortOfDischarge.SakhanId` exists, so "filter oversea permits by the Sakhan of
their discharge port" looked like a way to keep 1,014 *and* make the box live. It is dead on the data:

- That column is captured only for `Type='Border'` crossing points. The legacy Ports CRUD hides the
  Sakhan dropdown and strips its `required` attribute the moment Type is `Oversea`
  (`Content/frontend/portofdischarges.js:13-23`), and the API's own readers require `SakhanId` only on
  the Border path (`tradenet-2.0-api BusinessAssociationController.cs:1179-1185` vs `:1201-1204`).
- **100 % of the permits these reports count discharge/export at one port, "Yangon"** — 5,530/5,530
  import rows and 4,299/4,299 export rows sampled across the head, middle and tail of the oversea
  Detail reports on PROD, every one with `sakhanId: null`.
- So a port-derived filter yields one non-empty bucket and 24 empty ones — turning "the same 1,014
  every time" into "0 rows for Myawaddy/Muse/Tachileik", which is a worse complaint.

**Switching to the Border tables.** Makes Sakhan real, but collapses the row set (measured via the
Sakhan-aware `Border*PermitNewReportNewReport` and `Border*PermitBySectionReport`):

| | oversea (today) | border (if switched) |
|---|---|---|
| Border Import Permit | 1,328 permits | **112** — Kanpitetee 97, Yangon 6, Tachileik 5, Myawaddy 3, Kawthaung 1 |
| Border Export Permit | 2,637 permits | **8** — Tachileik 3, Myawaddy 3, Kanpitetee 2 |

For the 2025 window that is 18 and 6, and **18 is verbatim the figure
`BorderImportPermitByHSCodeLegacyParityTests.cs:15` records as already rejected by the customer**.
Not an engineering call — it needs a fresh owner decision, in writing, with both numbers side by side.

## What shipped (owner decision, 2026-09-10)

Keep the legacy oversea rows — **the numbers do not move** — and remove the two dropdowns that can
never be honoured, so the screen stops offering a control it ignores.

| file | change |
|---|---|
| `Frontend/src/Report/config/reportConfigs.ts` | Deleted the `SakhanId` and `ExportImportSectionId` filter objects from all four configs (`BorderImportPermitByHSCodeReport`, `BorderImportPermitHSCodeDetailReport`, `BorderExportPermitByHSCodeReport`, `BorderExportPermitHSCodeDetailReport`); both summaries' `carryFilters` → `['FromDate','ToDate','FilterType']`; deleted `initialSortColumn: 'SakhanId'` from both summaries; rewrote the two block comments that used to justify keeping the dead controls |
| same | **Import drill header** now the legacy string verbatim: `'List of Import Permits By HS Code'` (legacy `ReportsController.cs:10589` builds header1 from the posted FormType), matching what the Export twin already did |
| `BorderImportPermitByHSCodeReportController.cs` | `[ExcelFormatVersion(3)]` → `(4)` — the drill's title row is part of the sheet, so without the bump a replayed job keeps serving the old-header workbook for 24 h. Comment retag only, no logic change |
| `BorderExportPermitByHSCodeReportController.cs` | Comment + DTO doc retag only. **No** version bump — its sheet is unchanged |
| both controllers | `SakhanId` / `ExportImportSectionId` stay on the DTOs, bound and ignored, so bookmarked drill URLs and already-queued Excel jobs keep working |
| `reportConfigs.border{Import,Export}Permit.test.ts` | Re-pointed the four filter-list tests, plus **negative guards** (`expect(cfg.filters.some(f => f.name === 'SakhanId')).toBe(false)`) so nobody restores them, plus a drill-subtitle assertion |
| `Backend.Tests/Fixtures/ExcelSpecs/BorderImportPermitHSCodeDetailReport.json` | Regenerated (`cd Frontend && npm run fixtures:excel`) — one line, the new `headerLines[0]` |

Visible filters afterwards: **From Date / To Date, Filter By, HS Code**. `FormType` stays in the
config but is never rendered (`reportPresentation.ts:62-64` derives it, `GenericReportPage.tsx:533-538`
excludes any filter with a derived value) — exactly as the old form hid it, and keeping it avoids a
pointless Excel cache miss.

`initialSortColumn: 'SakhanId'` was inert already — `BasicTable.tsx:92` declares the prop but never
destructures it and hardcodes `sortColumn: ''` (`:146`, `:282`, `:369`). Deleting it removes the last
Sakhan trace; **the backend guard `GridSortColumnOrNull` (`sp_HSCodeReport.cs:214-231`) and its test
stay**, because `'SakhanId'` is not a property of `ReportAggregateResult` and used to be an HTTP 500.

## Deliberately NOT done

The Import twin is still behind its Export sibling on presentation — no `rowNumberTitle: 'Sr.No.'`
(`BorderHSCodeReport.rdlc:177`), no `dataType: 'money'` / `#,##0.0000` on Total Value (`rdlc:713`
`=FORMAT(Sum(Fields!Amount.Value),"N4")`), no `openInNewTab` on the HS Code drill (`rdlc:581`
`window.open(..., '_blank')`). All three are RDLC-confirmed gaps left over from 2026-09-07; the owner
scoped them out of this round. The `dataType: 'number'` thousands separator on No of Licences should
**not** be copied from the Export twin without a call — the RDLC prints a centred bare `1328` and the
By Section sibling deliberately omits it, so there the Export twin is probably the deviation.

## Verification

- `npx tsc --noEmit` — clean.
- `npx vitest run src/Report/config/reportConfigs.border{Import,Export}Permit.test.ts
  src/Report/config/reportConfigs.importPermit.test.ts` — 34 passed, 3 failed. Those 3
  (`action reports keep the old admin filter shape and footer totals`, `action report subtitles keep
  the legacy Border Export Permit wording`, `new report keeps old-admin filters plus Wai Phyo
  Sakhan/search parity`) fail **identically on a clean `main` worktree** and concern configs this
  change does not touch. Pre-existing baseline.
- `dotnet test Backend.Tests --filter "…BorderImportPermitByHSCode|…BorderExportPermitByHSCode|…ReportControllerBranchDefaults|…ExcelSpecContract"`
  — 1,232 passed, 2 failed, both `ImportLicenceDataImportController is missing TryCreateReportRequest`
  (the standing `TryCreateReportRequest`-family red baseline, unrelated).
- **Post-deploy, on PROD:** re-run the two summaries and both drills and confirm the numbers did not
  move — 1,014 / 1,328 and 578 / 2,637 for 2024-05-01 → 2026-09-06, 494 / 1,892 for the 2025 window.
  Then in the **browser** (not the harness) confirm only From/To, Filter By and HS Code render, and
  that the Import drill header reads *"List of Import Permits By HS Code From (…) To (…)"*.
  Finally export the Import drill and open the file — fingerprint it rather than trusting the queue,
  and check `processedBy` on `GET /api/ExcelExport/jobs`; a second worker on a stale build has faked
  an "Excel ≠ UI" report before.

## What to tell the customer

The Sakhan box on these two screens never filtered anything — not in Tradenet 2.0 either. Both screens
list oversea Import/Export Permits, which carry no Sakhan at all, so the old system's dropdown was
equally decorative. Rather than leave a control that does nothing, it has been removed; the report's
numbers are unchanged.

If per-Sakhan figures are what they actually need, the **Border Import/Export Licence By HS Code**
reports already filter correctly today (Border Import Licence, same window: 20,692 rows for All →
4,346 for Myawaddy → 3,507 for Tachileik). Switching these two *Permit* screens to border data can be
revisited — knowing it takes the totals from 1,328 to 112 and from 2,637 to 8.

Related: `docs/BorderImportPermitComplaints_2026-09-05.md`,
`docs/BorderExportPermitComplaints_2026-09-07.md`.

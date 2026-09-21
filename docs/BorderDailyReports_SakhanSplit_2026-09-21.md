# Border Daily reports — same Date + same Currency split across rows

Date: 2026-09-21
Complaint: "Border Import Licence Daily Report (New Licence Report) မှာ တစ်ရက်ချင်းစီကို
Currency စီတူရင် ပေါင်းဖော်ပြပေးပါရန် (အဟောင်းမှာက ရက်စွဲတူ/currency တူရင် ပေါင်းဖော်ပြပါတယ်)" —
the screenshot shows `2026-01-01 / THB` printed three times (5 + 3 + 45 licences) where the
old report printed one row of 53.

This is the same defect fixed on the By Method twin on 2026-09-18
(`BorderImportLicenceByMethod_SakhanSplit_2026-09-18.md`), which already named all three
Daily reports as carrying it. The owner deferred them then and released them on 2026-09-21,
so all three are fixed here.

## Root cause

The controllers passed `includeSakhan: true`, which adds `SakhanCode` to the grouping key
(`Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs:692` and its
Permit twins) and a `.ThenBy(SakhanCode)` tie-break
(`Backend/Service/Reports/ReportAggregationService.cs:404-407`). None of these grids has a
Sakhan column, so the split was invisible: one row per border office, interleaved by an
unseen code. The three THB rows on 2026-01-01 are three Sakhans.

Group keys read from `origin/master:./ReportControl/*.rdlc` (the working-tree copies are on a
stale 2022 branch and are ~1,100 lines off):

| Old RDLC | group keys | Sakhan column? | new flag | status |
|---|---|---|---|---|
| `BorderImportLicenceByDailyReport.rdlc:1269-1272` | `sLicenceDate`, `Currency` | no | was `true` | **fixed here** (the complaint) |
| `BorderImportPermitByDailyReport.rdlc:1269-1270` | `sLicenceDate`, `Currency` | no | was `true` | **fixed here** |
| `BorderExportPermitByDailyReport.rdlc:1277-1278` | `sLicenceDate`, `Currency` | no | was `true` | **fixed here** |
| `BorderExportLicenceByDailyReport.rdlc:1445-1447` | `sLicenceDate`, `Currency`, **`SakhanId`** | yes | `true` | correct — **leave alone** |

Border Export Licence Daily is the one report the flag legitimately belongs to, and the one
it was copied from. Its grid does render a Sakhan column
(`reportConfigs.ts` → `dataIndex: 'sakhanCode'`), so its grain is untouched.

## The TOTAL footer

All three fixed RDLCs print the identical footer row (cited from
`BorderImportLicenceByDailyReport.rdlc`):

| cell | content |
|---|---|
| Textbox5 (`:985`) | `TOTAL` |
| Textbox9 (`:1039`) | `=CountDistinct(Fields!LicenceNo.Value)` |
| Textbox7 (`~:1086`) | **empty** — no Total Value |
| Textbox20 (`:1145`) | the literal `Total USD Value`, in the Currency cell |
| Textbox6 (`~:1201`) | `=FORMAT(Sum(Fields!totalUSDAmount.Value), "N4")` |

So the right mode is `ReportColumnTotalsMode.CountOnly`:
`ReportAggregationService.BuildColumnTotals` drops `totalValue` under `CountOnly` but keeps
`totalUSDValue` for the Daily dimension (`ReportAggregationService.cs:317-320`), which is
exactly the pair of cells the legacy footer fills. The grid was previously showing a
`totalValue` that added THB + USD + CNY together.

`IExcelNoFooterReport` deliberately **not** added — it would hide the surviving count and USD
total from the sheet.

## Changes

`Backend/Controllers/Report/BorderImportLicenceDailyReportNewLicenceReportController.cs`
and `…/BorderExportPermitDailyReportNewPermitReportController.cs` — four edits each:

1. `includeSakhan: false` on all three surfaces — `Post`, `GetAggregateRowsAsync` and
   `OrderGroups` — so the Excel export keeps matching the grid.
2. `columnTotalsMode: ReportColumnTotalsMode.CountOnly`.
3. `[ExcelFormatVersion(2)]` added (neither class had one, so both defaulted to 1). Without it
   the export queue keeps serving the cached pre-fix `.xlsx`.
4. The Export Permit controller's stale ordering comment ("then Sakhan (includeSakhan: true,
   matching this report's Post)") rewritten.

`…/BorderImportPermitDailyReportNewPermitReportController.cs` was already half-fixed by the
2026-09-10 footer round — it had `CountOnly` and `[ExcelFormatVersion(2)]` — so only
`includeSakhan: false` ×3 plus the bump to `[ExcelFormatVersion(3)]`, since the row shape
changes and the cached v2 sheets must not be reused.

No frontend change: all three configs already carry exactly the 5 RDLC columns
(`Date`, `No of Licences`, `Total Value`, `Currency`, `Total USD Value`) and no Sakhan
column, and the footer is entirely backend-driven (`ApiResult.ColumnTotals` →
`BasicTable.tsx:393-394, 522-532`). The `SakhanId` *filter* stays — it is correct and matches
the legacy search form.

No SQL: `request.Type == "Border"` never takes the stored-proc branch
(`sp_ImportLicenceDetailReport_Fast.cs:838-839`), so these run as EF LINQ and the app deploy
alone ships the fix.

## Expected after the fix

The complaint's own window, from the customer's screenshot:

| Date | No of Licences | Total Value | Currency |
|---|---:|---:|---|
| 2026-01-01 | 5 + 3 + 45 = **53** | 7,875,576.5160 + 4,250,873.2000 + 45,274,830.7402 = **57,401,280.4562** | THB |
| 2026-01-01 | 1 | 47,293.5080 | USD |
| 2026-01-02 | 2 | 549,526.4600 | CNY |
| 2026-01-02 | 3 + 3 + 23 = **29** | 3,897,496.0000 + 4,808,461.1600 + 32,428,011.3525 = **41,133,968.5125** | THB |

The **footer count is unchanged** by the merge: a licence belongs to exactly one Sakhan, so
the per-group `CountDistinct` sets never overlapped.

Total USD Value is also additive: `ReportUsdConversionService.ConvertToUsd` applies a factor
that depends only on (date, currency), so the merged row's USD value equals the sum of the
per-Sakhan ones, up to a possible ±0.0002 from rounding each slice at 4 decimals separately.

## Tests

`Backend.Tests/BorderDailyReportSakhanSplitTests.cs` (new):

1. A `[Theory]` over all three controllers — three `includeSakhan: false` call sites, no
   `includeSakhan: true` left, `CountOnly` present, the expected `[ExcelFormatVersion]`
   present, `IExcelNoFooterReport` absent, and the config's 5 column titles with no
   `sakhanCode`.
2. A guard that **Border Export Licence Daily keeps `includeSakhan: true`** and keeps its
   Sakhan column, so nobody "fixes" the one report whose RDLC really groups by Sakhan.
3. A replay of the screenshot's 8 served rows collapsing to the 4 rows above, with every
   `SakhanCode` null, an unchanged footer count (85), no `totalValue` key and a
   `totalUSDValue` roll-up.

## Still to verify after deploy

The DB is not reachable from the dev Mac, so the end-to-end run happens on the deployed
build: re-run 01/01/2026–06/01/2026 on all three reports and confirm one row per
(date, currency), a footer with a count and a USD total but a blank Total Value, and a
**freshly generated** (not cached) Excel file matching the grid. If the sheet still disagrees,
fingerprint it by `cellXfs` count — a second stale worker on the shared queue has caused that
before.

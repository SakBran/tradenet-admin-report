# Border Import / Export Permit By HS Code — the Sakhan filter made real

Customer, 2026-09-10, after a first round on the same two reports:

> **Border Import Permit By HS Code** — 1-May-2024 to 6-Sep-2026 … all အတိုင်းပဲ ၁၀၁၄ ပဲ ကျပါတယ်။
> **Sakhanတွေ Filterလုပ်လို့ရအောင်ပြင်ပေးပါ။**
> **Border Export Permit By HS Code** — … 494 ပဲ ကျပါတယ်။ **Sakhanတွေ Filterလုပ်လို့ရအောင်ပြင်ပေးပါ။**
> **Old Reportမှာမှားနေလို့ပါ။**  ("because the Old Report is wrong")

Branch `fix/border-permit-hscode-sakhan-filter`. **No SQL and no stored-procedure deploy.**

## Why the filter was dead, and why that is now over

- The old Tradenet 2.0 Border screens post the *oversea* form type — `model.FormType =
  AppConfig.ImportPermit` (legacy `ReportsController.cs:15465`) and `AppConfig.ExportPermit`
  (`:14120`) — so they list oversea permits under a Border title. `dbo.sp_HSCodeReport`'s oversea
  branches never reference `@SakhanId`, and the oversea `ImportPermit` / `ExportPermit` tables have
  no `SakhanId` column at all. The old dropdown was decorative.
- We reproduced that bug-for-bug on 2026-09-05 (Import) and 2026-09-07 (Export) on the owner's "same
  result as the old report" instruction.
- Round one of this complaint (2026-09-10 morning) answered by *deleting* the dead dropdowns —
  commit `b5210a5`, now abandoned unmerged. The customer came back saying the old report is the thing
  that is wrong and the filter must work. That is this change.

The legacy procedure has always had correct, Sakhan-aware `Border Import Permit` / `Border Export
Permit` branches (`docs/StoredProcedureDefinitions.sql:5015-5121`) — nothing in the old admin ever
reached them. So this is less a divergence than finally running the query the legacy author wrote.

## What changed

| file | change |
|---|---|
| `Backend/StoredProcedureToLinq/sp_HSCodeReport.cs` | **The prerequisite.** `BorderExportPermitRows` / `BorderImportPermitRows` now project `PermitCreatedDate` / `PermitId`. They already filtered on `SakhanId` and `ExportImportSectionId`; those two ordering keys were the missing piece — `AggregateQuery`'s `LegacyOrder` branch orders groups by `Min(PermitCreatedDate)` then `Min(PermitId)`, and `LegacyCompanyGroupsAsync` sorts rows by the same pair before an order-sensitive in-memory `GroupBy`. Left unset they collapse to a constant null: arbitrary group order, unstable OFFSET/FETCH paging, and a non-deterministic company name on the drill. |
| both `Border*PermitByHSCodeReportController.cs` | `FormType` → `"Border Import Permit"` / `"Border Export Permit"`. `ExportImportSectionId` now mapped (and **added to the Import DTO**, which never had the property, so its box was posted and dropped). `LegacyOrder` stays `true`. `[ExcelFormatVersion]` Import 3→4, Export 2→3. |
| `Frontend/src/Report/config/reportConfigs.ts` | Both drill subtitles follow the legacy rule `"List of " + <posted FormType> + "s By HS Code"`, which now yields **"List of Border Import/Export Permits By HS Code"**. Dropped `initialSortColumn: 'SakhanId'` from both summaries — inert (`BasicTable` never posts it) and an HTTP 500 before the `GridSortColumnOrNull` guard, which stays. The Sakhan and Section filters themselves are untouched. |

`LegacyOrder` keeps its name but has two jobs now: it forces the LINQ path
(`UsesAggregateStoredProcedure` returns false for it, so the procedure is never reached) and it makes
`AggregateQuery` take the `(HSCodeId, Currency)` branch **before** `GroupsByCompany` — which returns
`true` unconditionally for `"Border Export Permit"`, so dropping the flag would split the summary per
buyer company, the defect fixed on 2026-09-08.

## Numbers

Measured on live PROD for the customer's window, 2024-05-01 → 2026-09-06. "After" is predicted from
the Sakhan-aware Border Detail reports collapsed to distinct `(hsCode, currency)` / distinct
`licenceNo`, and agrees with `Border*PermitNewReportNewReport` and `Border*PermitBySectionReport` on
the permit totals.

| | before (oversea) | after (border) |
|---|---|---|
| Border Import Permit By HS Code | 1,014 rows / 1,328 permits | **31 rows / 112 permits** |
| Border Export Permit By HS Code | 578 rows / 2,637 permits | **12 rows / 8 permits** |

| Sakhan | Import rows / permits | Export rows / permits |
|---|---|---|
| Kanpitetee (15) | 6 / 97 | 4 / 2 |
| Yangon (26) | 6 / 6 | 0 / 0 |
| Tachileik (4) | 14 / 5 | 1 / 3 |
| Myawaddy (5) | 4 / 3 | 7 / 3 |
| Kawthaung (6) | 1 / 1 | 0 / 0 |

Per-Sakhan permit counts sum exactly to the All figure (97+6+5+3+1 = 112; 2+3+3 = 8). **Only 5 of the
25 Sakhan options have any border permits in this window** — Muse included returns nothing. Real data,
but it will look like a bug, so say it up front.

## Investigated and cleared

- **`LicenceDate` might be unpopulated.** `sp_HSCodeReport` is the only consumer of
  `BorderImportPermit.LicenceDate` in the backend — every sibling Border report windows on
  `CreatedDate` — so an empty column would have made the switched report return zero rows everywhere,
  strictly worse than before. Disproved on PROD: `licenceDate` is non-null on 526/526 border import
  item rows and 34/34 border export rows, all inside the window (2024-05 → 2025-10).
- **Would `Min()` over the unprojected fields throw or emit `MIN(NULL)`?** Moot now, and settled in
  passing: `BorderExportPermitByHSCodeBorderSourceTests` renders the query with `ToQueryString()` and
  it both translates and carries `[CreatedDate]` in the ORDER BY.
- **The Section box.** Owner chose to map it rather than leave a second dead control. Be honest about
  what that buys: each lookup returns exactly one option (`borderImportPermitSections` → id 10,
  `borderExportPermitSections` → id 5) and 100% of the permits sit in that one section, so selecting
  it changes nothing today. It is also an addition of ours — the legacy Border branches carry no
  section predicate at all — hence the comment at both mapping sites.

## Tests

`BorderImportPermitByHSCodeLegacyParityTests` / `BorderExportPermitByHSCodeLegacyParityTests` pinned
the reversed decision and are replaced by `Border*PermitByHSCodeBorderSourceTests`, which keep the
history in their docstrings and assert the opposite: the Border FormType, that the request no longer
mirrors the oversea report's, that Sakhan and Section reach the query, that choosing a Sakhan changes
the generated SQL, and that the summary reads `[BorderImportPermit]` / `[BorderExportPermit]` while
grouping on four columns with no company. `ReportControllerBranchDefaultsTests.LegacyFormTypeOverrides`
is now empty — the naming convention derives the right FormType by itself.

`BorderExportPermitLegacyParityLiveDbTests` needed the most care because **it fails nowhere**: its DB
tests soft-skip without `TRADENET_REPORT_TEST_CONNECTION_STRING`, and its one DB-less test only
compares its constants against the file. Left alone it would have become a false oracle asserting the
old behaviour forever. Its `LegacySummarySql` / `LegacyDrillSql` now hold the Border Export Permit
branch verbatim (`docs/StoredProcedureDefinitions.sql:5019-5030` and `:5036-5048`), `ReadLegacyItemsAsync`
binds the `@SakhanId` that branch takes, and `Sakhan_and_export_section_boxes_are_dead_like_the_old_form`
became `Sakhan_filters_and_the_parts_add_up_to_the_whole` — every Sakhan's rows must be a subset of
All, at least one must narrow, and the per-Sakhan footers must sum to the All footer.

**Result:** backend 22 passed / 2 failed, both the pre-existing `ImportLicenceDataImportController is
missing TryCreateReportRequest` baseline; `ExcelSpecContractTests` 1,220 passed; frontend 1,530 passed
/ 6 failed, the identical 6 that fail on a clean `main` worktree (verified) in files this change does
not touch. `tsc --noEmit` clean.

## Still to verify on PROD after deploy

The Border LINQ branches have never executed in production. Check: both summaries return rows (not 0,
not a 500); the figures above; that per-Sakhan footers sum to All; that walking every page of both
grids and one HS Code drill shows no repeated or missing rows (the paging symptom if the
`PermitCreatedDate` projection were wrong); and that a fresh export carries Border rows — fingerprint
the file rather than trusting the queue, and check `processedBy` on `GET /api/ExcelExport/jobs`.

Related: `docs/BorderImportPermitComplaints_2026-09-05.md`,
`docs/BorderExportPermitComplaints_2026-09-07.md`, `Border Import Permit To Fix List.md`.

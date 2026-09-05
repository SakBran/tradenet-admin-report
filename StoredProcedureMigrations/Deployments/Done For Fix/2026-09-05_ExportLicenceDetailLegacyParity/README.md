# Export Licence Detail parity — 2026-09-05

Customer complaint on the (non-Border) **Export Licence Detail Report**:

> ExportLicenceDetailReport ကဒီကောင်ပြန်ပြင်မှရမယ်။ အသစ်က Data ကိုမထွက်ဘူး။
> အဟောင်း Old Code မှာတောင် Data ကရှာရင်အမှန်တိုင်းထွက်သေးတယ်။ အသစ်မှာပိုလဲကြာတယ် Data လဲမမှန်ဘူး။
> နောက်ဆုံးမရရင် Old Code ထဲက stored procedure ကိုတိုက်ရိုက်သုံးပြီး တစ်ပုံစံထဲအဖြေတူထွက်အောင်ပြင်ပေးပါ။

(no data in the new grid; the old admin shows the right rows for the same search; the new one
is slower and wrong; make it right — if need be use the old stored procedure directly and
produce the same answer.)

## What was wrong

The grid was an inline, hand-rolled seek that **paged licences** (`OFFSET n FETCH 10` over
licence keys, then all items of those licences) while its **TotalCount counted items**. With
636 licences / 2683 items the pager offered 269 pages, page 1 held ~40 rows, and every page
after 64 was empty. On top, the pager never reset on a new Search, so choosing Method of
export / Incoterms while on page ≥ 2 asked for licences that did not exist and showed
"no data". Each page also ran ~300 round trips plus a full COUNT and a per-currency GROUP BY.

## What this folder deploys

| File | Object |
|---|---|
| `01_sp_ExportLicenceDetailReportV3_pagination.sql` | **new** `dbo.sp_ExportLicenceDetailReportV3_pagination` |

It is the legacy `dbo.sp_ExportLicenceDetailReport` (`'Oversea'` branch) kept **verbatim** —
same 13 `INNER JOIN`s, same `CASE WHEN @X = 0` filters, same `CreatedDate <= @ToDate`
window, same select list — with item-grain key-first paging wrapped around it:
`#L` licence keys → `#K` item keys → `#P` one page of keys → the legacy select list for
those keys only. The legacy procedure itself is not an option for the grid: it took 15–17 s
for a two-day window and 335 s for one month on UAT, and the grid's default range is three
months. This shape answers a two-day page in 1–3 s and a three-month page in 3–7 s
(one outlier at 32 s on a busy UAT box), and returned **exactly** the legacy row set
(2683 = 2683 rows, 38 columns compared, empty diff) for 2025-08-31..2025-09-01.

`@Auto` (default `N''`) is additive so the By-Section / By-Method summaries that carry an
Auto / None-Auto choice into the drill-down keep count parity.

**Nothing existing is altered or dropped.** The stale, never-used
`sp_ExportLicenceDetailReportV2_Pagination` may remain on the server; the application does
not call it.

## Application side (deployed by the app, not by this folder)

- Controller → `sp_ExportLicenceDetailReportV3` wrapper. Until this procedure exists the
  wrapper catches SQL error 2812 and falls back to the item-grain LINQ page the Excel export
  uses — same rows, slower — so deploying the app first does not break the report.
- Footer removed (UI + Excel; `ExportLicenceDetailReport.rdlc` has no total row).
- Grid asks for the exact count with the page; the table returns to page 1 on every Search.

## Run order

1. `CaptureRollback.sql` — save the result grid (first deployment: rollback is a `DROP`).
2. `VerifyDeployment.sql` **section 1** — record what is deployed today (expect no row).
3. `00_RunAll.sql` (or `01`). `SET QUOTED_IDENTIFIER ON` is in both.
4. `VerifyDeployment.sql` sections 1, 2 and 3.

Section 1 must read `uses_quoted_identifier = 1`, `params = 12`, `temp-table key paging`.
Section 3 must show `legacy_rows = grid_rows = grid_TotalCount` and two empty `EXCEPT`
results. **Record the result below before calling this released.**

| Check | UAT (203.81.66.111) | Production |
|---|---|---|
| Section 3 parity, 2025-08-31..09-01 | 2683 = 2683, empty diff (2026-09-05) | _not yet run — the report database is not reachable from a developer machine_ |
| Method = CMP (3) / Incoterms = CIF (12) | TotalCount 2343 / 10 = legacy | _not yet run_ |

## Provenance

`checksums.txt` records the SHA-256 of the repository original this copy was taken from.
`Backend.Tests/DeploymentFolderContractTests.cs` fails the build if the copy drifts from
`StoredProcedureMigrations/sp_ExportLicenceDetailReportV3_pagination.sql`, so editing the
canonical procedure means re-copying it here.

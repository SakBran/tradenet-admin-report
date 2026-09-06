# Border Import Permit Detail — legacy parity, 2026-09-06

Owner's instruction (2026-09-06): the **Border Import Permit Detail Report** must produce a
**byte-identical result to the old Tradenet 2.0 report, even where the old code is wrong**.

## What the old report is

`Reports/BorderImportPermitDetailReport` (legacy `ReportsController.cs:14455-14620`) calls
`dbo.sp_ImportPermitDetailReport @Type='Border'` with `FromDate` `" 00:00:00"` / `ToDate`
`" 23:59:59"` and renders `BorderImportPermitDetailReport.rdlc`: header line
`List of Border Import Permit By Detail From (dd/MM/yyyy) To (dd/MM/yyyy)`, a `Sr.No.` row
number, 23 columns, no group, no sort, no footer. Dates are the model's pre-formatted
`dd/MM/yyyy` strings, `Price`/`Value` are `FORMAT(...,"N4")`, `Qty` is `"N2"`, and
`Company Address` is `CommonRepository.GetAddress` (with its `"State,"` no-space and trailing
`", "` quirks).

## What this folder deploys

| File | Object |
|---|---|
| `01_sp_BorderImportPermitDetailReport_pagination.sql` | **new** `dbo.sp_BorderImportPermitDetailReport_pagination` |

The legacy `dbo.sp_ImportPermitDetailReport` (`'Border'` branch) kept **verbatim** — same ten
`INNER JOIN`s, same `CASE WHEN @X = 0` filters, same `CreatedDate <= @ToDate` window, same
select list including `dbo.fn_GetNRCNo` and the two `FOR XML PATH` CSV expanders — with
item-grain key paging around it: `#K` keys → `#P` one page → the legacy select list for
those keys only. `@PageSize = 0` returns every row (the Excel export and section 3 below).
Order is `CreatedDate, permit Id, ItemNo, item UniqueId` — the legacy has no `ORDER BY`, so
this is the deterministic reading of its plan order (permits as created, items in line order).

**Nothing existing is altered or dropped.**

## Application side (deployed by the app, not by this folder)

- Controller → `sp_BorderImportPermitDetailReport` wrapper for the grid **and** the Excel
  stream. Until this procedure exists the wrapper catches SQL error 2812 and falls back to the
  LINQ twin, now paged in the same order and printing the same `Company Address`; the LINQ NRC
  composition is the only thing it cannot make identical to `fn_GetNRCNo`.
- Grid: `Sr.No.`, header line, `dd/MM/yyyy` dates, `N4`/`N2` numbers, 1000 rows per page, filter
  box = the old form (From, To, Sakhan, EIR Card Type, Import Section). `[ExcelFormatVersion(2)]`.

## Run order

1. `CaptureRollback.sql` — save the result grid (first deployment: rollback is a `DROP`).
2. `VerifyDeployment.sql` **section 1** — record what is deployed today (expect no row).
3. `00_RunAll.sql` (or `01`). `SET QUOTED_IDENTIFIER ON` is in both.
4. `VerifyDeployment.sql` sections 1, 2 and 3.

Section 1 must read `uses_quoted_identifier = 1`, `params = 10`, `temp-table key paging`,
`legacy NRC function`. Section 3: `legacy_rows = grid_rows = grid_TotalCount` and both
`EXCEPT` results empty. On the report database the 2025 window is 70 rows / 18 permits
(all Sakhan) and 20 rows for TCL — the figures the customer quoted from the old report.

## Result

_Not yet run on production — fill in after deployment: legacy_rows / grid_rows / EXCEPT counts._

# 2026-09-21 — Export Permit By HS Code parity

One procedure changes: `dbo.sp_HSCodeReport_pagination`, and only its `'Export Permit'`
branch (3 sub-branches).

| Run order | File | Procedure |
|---|---|---|
| 1 | `01_sp_HSCodeReport_pagination.sql` | `dbo.sp_HSCodeReport_pagination` |

## Why

Customer complaint, 2026-09-21, **Export Permit By HS Code Report**:

> HS Code တစ်ခုမှာ Description /USD တန်ဖိုးတူတယ်ဆိုရင် ပေါင်းဖော်ပြပေးပါရန်

Gloss: where one HS Code has the same Description and the same USD value, merge the rows
and show them together. The screenshot (01/01/2025 → 03/01/2025) shows `8807300000` three
times — same description, all USD — with 1, 4 and 1 licences and 500.0000, 3,450.0000 and
9,000.0000.

Measured on the live PROD API (FilterType `Start`, HSCode `''`, Sakhan 0, Section 0),
paging the whole result and collapsing on distinct (HS code, currency):

| Window | Old report rows | Before | After | Total No of License |
|---|---|---|---|---|
| 2025-01-01 → 2025-01-03 23:59:59 | 3 | 5 | **3** | 8 (already matched) |
| 2025-01-01 → 2025-12-31 23:59:59 | 353 | 605 | **353** | 1,147 (already matched) |

The complaint's own row becomes one row: `8807300000` / USD / **6** licences /
**12,950.0000**. For 2025, 87 pairs were split; the worst, `3702529000` / USD, printed 16
times. The sum of every Total Value (144,929,409.1551) is unchanged — the rows merge, the
money does not move.

### Root cause

The old screen (`Views/Reports/ExportPermitByHSCodeReport.cshtml` +
`ReportsController.cs:7648-7706` on `origin/master` of tradenet-2.0-admin) renders
`HSCodeReport.rdlc`, whose **only** row group is `=Fields!HSCodeId.Value` +
`=Fields!Currency.Value` (`rdlc:1150-1160`). Its grid is `Sr.No. | HS Code | Description |
No of Licences | Total Value | Currency` (`rdlc:169/224/279/334/389/444`) — no company
column — with `Total Value` = `FORMAT(Sum(Fields!Amount.Value),"N4")` (`rdlc:705`) and a
`TOTAL` footer of `=CountDistinct(Fields!LicenceNo.Value)` under `No of Licences`
(`rdlc:978`, blank `Total Value` cell). Legacy `dbo.sp_HSCodeReport` returns raw item rows
(`ORDER BY HSCode.Id`), no SQL grouping.

Our paged procedure's `'Export Permit'` branch `GROUP`ed `BY tmp.HSCode, tmp.HSDescription,
tmp.CompanyRegistrationNo, tmp.CompanyName, tmp.Currency` and `ORDER`ed `BY result.HSCode,
result.CompanyName, result.Currency` — one row per buyer company, visually identical rows
each carrying a partial Total Value. The LINQ twin
(`Backend/StoredProcedureToLinq/sp_HSCodeReport.cs` `GroupsByCompany()`), which the Excel
export streams, did the same.

`Total No of License` was never wrong: it is a separate whole-set
`COUNT(DISTINCT LicenceNo)` computed in C#, exactly the RDLC footer.

## What changes, per sub-branch

All 3 sub-branches of `'Export Permit'` (`@HSCode=''` / `@FilterType='Start'` / `End`).
This FormType has no `@IncludeTotalCount=0` fast page — every sub-branch returns
`COUNT(*) OVER() TotalCount`:

1. `GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency` — the RDLC key. The
   outer `SELECT` keeps its 8-column list and order, with
   `CAST(NULL AS nvarchar(200)) CompanyRegistrationNo` /
   `CAST(NULL AS nvarchar(500)) CompanyName`, so the caller's DTO is unchanged. Same shape
   the `'Import Permit'`, `'Export Licence'`, `'Border Export Licence'` and
   `'Border Import Licence'` branches already use.
2. `ORDER BY result.HSCode,result.Currency,result.HSCodeId` — a unique key, so the page
   window is deterministic. The old `ORDER BY result.HSCode,result.CompanyName,
   result.Currency` would have left the page boundary to the optimiser once `CompanyName`
   became a literal `NULL`.
3. The inner derived table (the `ExportPermit`/`ExportPermitItem`/`PaThaKa`/`HSCode`/
   `Currency`/`ExportImportSection` joins, the `LicenceDate` window, the `HSCode.Code LIKE`
   predicates) is byte-identical to before.

**Unchanged:** the other seven `@FormType` branches. `Import Licence` By HS Code still
groups by company — the last family carrying the defect.

## Application changes shipped with it

The procedure is only half the fix — deploy the application too, or the grid gets the new
grain while the `.xlsx` keeps the old one:

- `sp_HSCodeReport.GroupsByCompany` returns `false` for `'Export Permit'`, so the LINQ twin
  (`AggregateQuery`, which is what the **Excel export** streams) groups identically.
- `ExportPermitByHSCodeReportController` accepts `GroupBy` and sets `GroupByCompany`; the
  new `ExportPermitHSCodeDetailReport` drill config posts `GroupBy='Company'` — that is how
  the drill keeps `HSCodeDetailReport.rdlc`'s `(HSCodeId, CompanyRegistrationNo)` grain
  (`rdlc:1262-1265`) and its Company Name column. The HS Code cell used to open the
  per-item `ExportPermitDetailReport`; the old report opened `HSCodeDetailReport` in a new
  window (`rdlc:573`, `ReportsController.cs:7702`).
- `[ExcelFormatVersion(2)]` on the controller (it had no attribute, i.e. version 1) — the
  export queue caches finished workbooks by payload + version, so without the bump a
  closed-period request keeps serving the company-split sheet.
- Filter-box parity: the **Export Section** dropdown the old `.cshtml:40-48` has and the
  new filter box did not (`ExportImportSectionId`, lookup `exportPermitSections`; the
  controller now maps it, and a non-zero value takes the report onto the LINQ twin, which
  is the only path that can filter on it). The inert **Form Type** text box is removed —
  the old form carries FormType as an `@Html.HiddenFor` (`.cshtml:21`) and the controller
  hardcodes `"Export Permit"`.
- Presentation parity: `Sr.No.` row header, `Total Value` as `#,##0.0000` (the RDLC's
  `FORMAT(…,"N4")`), `defaultPageSize: 1000` (the RDLC scrolled every row on one page), the
  drill opening in a new tab.

## Deploy order

**Procedure first, then the application.** Until the procedure is applied the grid still
pages the company-split proc while the `.xlsx` already has the new grain — the two would
disagree with each other as well as with the old report.

## How to run

1. `CaptureRollback.sql` — save the result grid. That text IS the rollback (swap the leading
   `CREATE` for `CREATE OR ALTER`). It also captures legacy `dbo.sp_HSCodeReport`, the oracle
   for the 353 / 1,147 figures.
2. `00_RunAll.sql` — or `01_sp_HSCodeReport_pagination.sql` on its own; they are the same
   procedure text.
3. `VerifyDeployment.sql` — section 1 before and after, sections 2-4 after, section 5 in
   the browser once the application is deployed.
4. Deploy the application.

⚠ **Do not re-run** the `sp_HSCodeReport_pagination` copies under
`Deployments/Done For Fix/2026-09-05_ImportPermitParityRound1/`,
`.../2026-09-05_BorderImportPermitComplaints/`,
`.../2026-09-08_ExportLicenceByHSCodeParity/` or
`.../2026-09-14_BorderImportLicenceByHSCodeParity/`. Those are frozen snapshots of what was
deployed on those dates and would revert this change.

`checksums.txt` records the SHA-256 of the repository original this copy was taken from;
verify with `shasum -a 256 -c checksums.txt` from `StoredProcedureMigrations/`.

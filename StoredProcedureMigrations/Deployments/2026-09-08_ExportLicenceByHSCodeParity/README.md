# 2026-09-08 — Export Licence / Border Export Licence By HS Code parity

One procedure changes: `dbo.sp_HSCodeReport_pagination`, and only its `'Export Licence'`
and `'Border Export Licence'` branches.

## Why

Customer complaint, 2026-09-08, both reports searched **31-Aug → 01-Sep 2026**:

| Report | Old report rows | Before | After | Total No of License |
|---|---|---|---|---|
| Export Licence By HS Code | 304 | 1058 | **304** | 961 (already matched) |
| Border Export Licence By HS Code | 33 | 76 | **33** | 41 (already matched) |

The old reports print one row per **(HS code, currency)** — `HSCodeReport.rdlc:1150-1160`
and `BorderHSCodeReport.rdlc:1158-1168` group on `=Fields!HSCodeId.Value` +
`=Fields!Currency.Value` and nothing else, and neither RDLC has a company column
(`rdlc:169-444` / `:177-452`). Our procedure also grouped on `CompanyRegistrationNo,
CompanyName`, so one HS code printed once per buyer — invisibly, since the grid renders no
company column — and each of those rows carried only that buyer's slice of Total Value.

`Total No of License` was never wrong: it is a separate whole-set
`COUNT(DISTINCT LicenceNo)` computed in C# (`sp_HSCodeReport.cs`), which is exactly the
RDLC footer's `=CountDistinct(Fields!LicenceNo.Value)` (`HSCodeReport.rdlc:978`).

The customer's second symptom — "pagination offers 106 pages but data stops at page 97" —
came from the same place. `ORDER BY result.HSCode, result.CompanyName, result.Currency` was
not a unique key over a group that also contained `CompanyRegistrationNo` and
`HSDescription`, so tied rows were ordered arbitrarily and `OFFSET/FETCH` could return one
row on two pages and another on none.

Measured on the live PROD API before the change: `totalCount 1058` / `76`, with
`distinct (hsCode, currency)` = **304** / **33** — i.e. the old reports' counts exactly.

## What changes, per sub-branch

`'Export Licence'` (4 sub-branches: the `@IncludeTotalCount=0` fast page, then `@HSCode=''`
/ `Start` / `End`) and `'Border Export Licence'` (3 — it has no fast page, it always returns
`COUNT(*) OVER()`):

1. `GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency` — the RDLC key. The
   outer `SELECT` keeps its column list and order, with
   `CAST(NULL AS nvarchar(200)) CompanyRegistrationNo` /
   `CAST(NULL AS nvarchar(500)) CompanyName`, so the caller's DTO
   (`sp_HSCodeAggregateReportResult`) is unchanged. Same shape the `'Import Permit'` branch
   already uses.
2. `ORDER BY result.HSCode,result.Currency,result.HSCodeId` — a unique key, so the page
   window is deterministic even where two `HSCode` rows share a code string.
3. The `'Export Licence'` fast page now joins `ExportImportSection`, like every counted
   sub-branch and like legacy `dbo.sp_HSCodeReport`
   (`docs/StoredProcedureDefinitions.sql:4627-4637`). Without it the fast page could show a
   licence whose `ExportImportSectionId` has no section row — one the old report never
   printed and the exact-count branch does not count.

**Unchanged:** the other six `@FormType` branches. Import Licence, Export Permit and Border
Import Licence By HS Code carry the same latent defect and were deliberately left for a
later round (owner decision, 2026-09-08); their `*HSCodeDetailReport` reports render
Company Name off this same procedure.

## Application changes shipped with it

The procedure is only half the fix — deploy the application too, or the grid gets the new
grain while the `.xlsx` keeps the old one:

- `sp_HSCodeReport.GroupsByCompany` returns `false` for these two form types, so the LINQ
  twin (`AggregateQuery`, which is what the **Excel export** streams) groups identically.
- Both controllers accept `GroupBy` and set `GroupByCompany`, and both HS Code **detail**
  drill configs post `GroupBy='Company'` — that is how the drill keeps
  `HSCodeDetailReport.rdlc`'s `(HSCodeId, CompanyRegistrationNo)` grain and its Company Name
  column. A new `BorderExportLicenceHSCodeDetailReport` config/route was added: the old
  Border screen's HS Code cell opens `Reports/BorderHSCodeDetailReport`
  (`ReportsController.cs:10489`), not the Border Export Licence Detail report we pointed at.
- `[ExcelFormatVersion(2)]` on both controllers — the export queue caches finished workbooks
  by payload + version, so without the bump a closed-period request keeps serving the
  company-split sheet.
- Presentation parity: `Sr.No.` row header, `Total Value` as `#,##0.0000` (the RDLC's
  `FORMAT(…,"N4")`), `defaultPageSize: 1000` (the RDLC scrolled every row on one page), the
  plural legacy header "List of Export Licences By HS Code", the drill opening in a new tab
  (`rdlc:570-576` `window.open(…,'_blank')`), and the Border filter box back to Start/End
  with no `--- All ---` (`BorderExportLicenceByHSCodeReport.cshtml:57`).

Pinned by `Backend.Tests/ExportLicenceByHSCodeParityTests.cs`,
`Frontend/src/Report/config/reportConfigs.borderExportLicence.test.ts` and the By-HS-Code
cases in `reportConfigs.exportLicence.test.ts`.

## How to run

1. `CaptureRollback.sql` — save the result grid. That text IS the rollback (swap the leading
   `CREATE` for `CREATE OR ALTER`). It also captures legacy `dbo.sp_HSCodeReport`, the oracle
   for the 304 / 33 figures.
2. `00_RunAll.sql` — or `01_sp_HSCodeReport_pagination.sql` on its own; they are the same
   procedure text.
3. `VerifyDeployment.sql` — section 1 before and after, sections 2-5 after.
4. Deploy the application.

⚠ **Do not re-run** the `sp_HSCodeReport_pagination` copies under
`Deployments/Done For Fix/2026-09-05_ImportPermitParityRound1/` or
`.../2026-09-05_BorderImportPermitComplaints/`. Those are frozen snapshots of what was
deployed on those dates and would revert this change.

`checksums.txt` records the SHA-256 of the repository original this copy was taken from;
verify with `shasum -a 256 -c checksums.txt` from `StoredProcedureMigrations/`.

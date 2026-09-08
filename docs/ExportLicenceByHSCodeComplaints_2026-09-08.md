# Export Licence / Border Export Licence By HS Code — complaint 2026-09-08

## The complaint (verbatim, Burmese)

> **Export Licence By HS Code Report** — From 31-Aug to 1-Sep 2026 နဲ့ရှာတာ No of License (961) က
> ကိုက်တယ်။ သို့သော် Record Count ကိုကြည့်ရင် OldReport တွင် 304 ရှိပြီး Report တွင် 962 ဆိုပြီးဖြစ်နေပါတယ်။
> Report ၏ pagination တွင် page က 106 အထိရှိသော်လည်း page 97 အထိသာ data ပါပါတယ်။ တစ် page တွင် 10
> ထုတ်ပြီးကြည့်ပါသည်။
>
> **Border Export Licence By HS Code Report** — From 31-Aug to 1-Sep 2026 နဲ့ရှာရင် record count row သည်
> Report တွင် 70 ကြောင်း ရှိပြီး OldReport တွင် ၃၃ ကြောင်းပဲရှိပါတယ်။ သို့သော် Total No of License တွင် 41
> တော့ကိုက်ပါတယ်။
>
> ဒီနှစ်ခုကို Old Report အတိုင်း အတိအကျ Data ထွက်တာ Table ပြတာ Excel ထုတ်တာမှာပါ တူအောင်ပြင်ပေးပါ။

## Measured first, on the live PROD API

`reportapi.myanmartradenet.com` with a hand-minted JWT ([[report-api-jwt-harness]]),
`FromDate 2026-08-31`, `ToDate 2026-09-01T23:59:59`, `FilterType Start`:

| | pager `totalCount` | `distinct (hsCode, currency)` | `columnTotals.noOfLicences` |
|---|---|---|---|
| `ExportLicenceByHSCodeReport` | 1058 | **304** | 961 |
| `BorderExportLicenceByHSCodeReport` | 76 | **33** | 41 |

`distinct (hsCode, currency)` reproduces the old reports' row counts **exactly** — 304 and 33. So
the row set was never wrong; the **grain** was.

## Root cause

Both old reports print one row per **(HS code, currency)**. The RDLC row group is two expressions
and nothing else:

`HSCodeReport.rdlc:1150-1160` (and `BorderHSCodeReport.rdlc:1158-1168`, identical):

```xml
<Group Name="Details">
  <GroupExpressions>
    <GroupExpression>=Fields!HSCodeId.Value</GroupExpression>
    <GroupExpression>=Fields!Currency.Value</GroupExpression>
  </GroupExpressions>
```

The legacy procedure `dbo.sp_HSCodeReport` does **no aggregation at all** — it returns one row per
licence item and the RDLC does the grouping. Our `sp_HSCodeReport_pagination` moved that grouping
into SQL but with the buyer company in the key:

```sql
GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
```

Neither grid has a company column (`HSCodeReport.rdlc:169-444` is Sr.No. / HS Code / Description /
No of Licences / Total Value / Currency), so one HS code printed once per buyer — visually
identical rows — each carrying only that buyer's slice of Total Value. HS code `6110300000 USD`
appeared as **51 rows**.

The same defect had already been fixed for Import Permit (`sp_HSCodeReport_pagination.sql:317-323`)
and Border Import Permit; Export Licence and Border Export Licence were the outliers.

### "Total No of License" was never wrong

961 / 41 matched because that footer is a separate whole-set `COUNT(DISTINCT LicenceNo)`
(`sp_HSCodeReport.cs`) — precisely the RDLC footer's `=CountDistinct(Fields!LicenceNo.Value)`
(`HSCodeReport.rdlc:978`), which is deliberately **not** the sum of the per-row `No of Licences`
column (a licence spanning 5 HS codes contributes to 5 rows but counts once).

### The pagination half is the same bug

The pager offered 106 pages because `totalCount` was a truthful count of 1058 company-split rows.
Probing `pageIndex` 95/96/97/100/105 showed the API *does* return rows on every page, so nothing was
missing server-side — but the page **window** was unstable:

```sql
ORDER BY result.HSCode,result.CompanyName,result.Currency
```

is not a unique key over a group that also contains `CompanyRegistrationNo` and `HSDescription`.
With ties, `OFFSET/FETCH` can hand back the same row on two pages and another on none — 1058 rows
yielding only ~962 distinct ones as the user pages through, which is exactly the "962 / data stops
at page 97" reading. Collapsing the grain fixes both halves at once.

## What changed

### Stored procedure — `StoredProcedureMigrations/sp_HSCodeReport_pagination.sql`

`'Export Licence'` (4 sub-branches) and `'Border Export Licence'` (3 — it has no fast page):

1. `GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency`; the outer `SELECT` keeps its
   column list with `CAST(NULL AS nvarchar(200)) CompanyRegistrationNo` /
   `CAST(NULL AS nvarchar(500)) CompanyName`, so `sp_HSCodeAggregateReportResult` is unchanged.
2. `ORDER BY result.HSCode,result.Currency,result.HSCodeId` — unique, so paging is deterministic.
3. The `'Export Licence'` fast page now joins `ExportImportSection`, like every counted sub-branch
   and like legacy `dbo.sp_HSCodeReport` (`docs/StoredProcedureDefinitions.sql:4627-4637`). Without
   it the fast page could show a licence whose section id has no section row — one the old report
   never printed and the exact-count branch does not count.

Owner decisions (2026-09-08): **HS-code-string order**, keeping the paged procedure rather than the
`LegacyOrder` LINQ path (the legacy `ORDER BY HSCode.Id` sequence would abandon SQL paging, and
within one HS code the old order is genuinely nondeterministic — the legacy proc has no ORDER BY
there); and **only these two reports** — Import Licence, Export Permit and Border Import Licence By
HS Code still carry the identical defect and are left for a later round.

### Application

- `sp_HSCodeReport.GroupsByCompany` returns `false` for these two form types, so the LINQ twin
  (`AggregateQuery`) groups identically. That twin is what the **Excel export** streams
  (`WriteRowsAsync` → `GetAggregateRowsAsync`) and what the grid uses when an Export Section is
  chosen, so this is the change that makes the sheet match the table.
- Both controllers take `GroupBy` and set `GroupByCompany`; both HS Code **detail** drills post
  `GroupBy='Company'` (`ReportFilterConfig.constantValue`) so they keep
  `HSCodeDetailReport.rdlc:1261-1265`'s `(HSCodeId, CompanyRegistrationNo)` grain and their Company
  Name column. Same mechanism as `BorderImportPermitHSCodeDetailReport`.
- **New** `BorderExportLicenceHSCodeDetailReport` config + route + page: the old Border screen's HS
  Code cell opens `Reports/BorderHSCodeDetailReport` (`ReportsController.cs:10489`), not the Border
  Export Licence Detail report we were pointing at.
- `[ExcelFormatVersion(2)]` on both controllers — the export queue caches finished workbooks by
  payload + version, so without the bump a closed-period request keeps serving the company-split
  sheet.
- Presentation parity: `Sr.No.` (`rdlc:169`/`:177`), Total Value as `#,##0.0000`
  (`=FORMAT(Sum(Fields!Amount.Value),"N4")`, `rdlc:705`/`:713`), `defaultPageSize: 1000` (the RDLC
  scrolled every row on one page), the plural legacy header "List of Export **Licences** By HS Code"
  (`ReportsController.cs:4289`), drill in a new tab (`rdlc:570-576` `window.open(…,'_blank')`), and
  the Border filter box back to Start/End with no `--- All ---`
  (`BorderExportLicenceByHSCodeReport.cshtml:57`) — which also takes the procedure off its
  unanchored `LIKE '%'+@HSCode` arm.

## Expected result

Recomputed from the 1058 / 76 PROD rows by collapsing on (hsCode, currency) — valid because a
licence belongs to exactly one company, so per-company distinct-licence counts add up:

```
Export Licence By HS Code         1058 rows -> 304   Total Value 115,313,535.05 unchanged
Border Export Licence By HS Code    76 rows ->  33   Total Value  42,280,027.56 unchanged
```

`Total No of License` stays 961 / 41. At `defaultPageSize: 1000` both reports now print on one page,
as the RDLC did.

## Verification done

- `Backend.Tests/ExportLicenceByHSCodeParityTests.cs` — 10 tests: request flags, the LINQ GROUP BY
  for summary (4 key columns, no company) and drill (3 columns, `CompanyRegistrationNo`, no
  `CompanyName`), the `GroupBy` round-trip through both controllers, and text assertions that all 7
  procedure sub-branches carry the RDLC key, the unique ORDER BY, and the section join.
- Full backend suite with the mandated DB-skip filter (`docs/ExcelParity/Prompts/_gate-common.md`
  §3a): **7 failures, 1710 passed**, against a clean-`HEAD` baseline of **10 failures, 1691 passed**
  — no new failures, and the 3 `DeploymentFolderContractTests` failures are *fixed* by this
  release folder (there were no date-prefixed folders directly under `Deployments/` any more, so
  its `Assert.NotEmpty` was red on `main`).
- Frontend: `npx vitest run` **6 failures / 1536 passed**, identical to the clean-`HEAD` baseline of
  6 / 1523. `npm run build` (tsc + vite) clean. New cases in
  `reportConfigs.borderExportLicence.test.ts` and `reportConfigs.exportLicence.test.ts`.
  (`npm run lint` is broken repo-wide — ESLint 9 with no `eslint.config.js` — pre-existing.)
- Excel spec fixtures regenerated with `npm run fixtures:excel`; the diff touches only the four
  affected reports plus their `index.json` rows, so no peer session's entries were swept.

## Still owed

- **The procedure is hand-deployed.** Apply
  `StoredProcedureMigrations/Deployments/2026-09-08_ExportLicenceByHSCodeParity/` (CaptureRollback →
  00_RunAll → VerifyDeployment) and then re-run the live-API check above expecting `totalCount` 304
  / 33. Until it is applied, `main` auto-deploys an application whose Excel export uses the new
  grain while the grid still pages the old procedure — the two surfaces will disagree in the
  opposite direction.
- Not re-run against a database from here: the report DB is CGNAT-internal and unreachable from this
  Mac, so every number above comes from the live API or from arithmetic on its rows.
- **Left as-is, deliberately:** the oversea Export Section dropdown actually filters in the new
  report, while the old form never sent it to the procedure (`ReportsController.cs:4269` — the
  dropdown was decorative). Bug-for-bug parity there would mean *removing* working behaviour and
  breaking the drill's `carryFilters`; say the word if the customer wants it.
- The LINQ path (`AggregateQuery`, used for the Excel export and section-filtered grids) orders on
  `(HSCode, Currency)` with no `HSCodeId` tiebreaker — `ReportAggregateResult` has no such field to
  order by after projection. Same theoretical page-window ambiguity as above, but only where two
  `HSCode` rows share a code string, and it predates this change on six other reports.
- Import Licence, Export Permit and Border Import Licence By HS Code still group by company
  (`sp_HSCodeReport_pagination.sql` and `GroupsByCompany`). Same one-line change per branch plus a
  `GroupBy='Company'` pin on each drill config.

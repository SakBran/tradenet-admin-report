# Border Import Licence By HS Code — complaint 2026-09-14

## The complaint (verbatim, Burmese)

> **Border Import Licence By HS Code Report** — 1. Data ထွက်ရှိမှု မမှန်ပါ အဟောင်းနဲ့ အသစ် မတူပါ
> (ဘာလို့မတူလဲဆိုရင် အသစ်မှာ company name column တစ်ခု ပိုနေတယ် အဲ့တော့ hS တစ်ခုက USD နဲ့ ၃
> ကြောင်းလျှောက်ထားရင် ၃ ကြောင်းလုံး ပြနေတော့ အဟောင်းနဲ့တိုက်စစ်ရင်လွဲနေပါတယ်) အဟောင်းမှာက HS တစ်ခု က
> USD တန်ဖိုးနဲ့ ၄ ကြောင်းလျှောက်ထားရင် ၄ ကြောင်းတန်ဖိုး ပေါင်းပြလိုက်တာပါ

Gloss: the new report does not match the old one. The new one carries an extra company-name
grouping, so an HS code applied for three times in USD prints three rows; the old report printed
one row with the values of all the applications summed.

## Measured first, on the live PROD API

`reportapi.myanmartradenet.com`, `FilterType Start`, no HS code, Sakhan and Import Section "All",
the whole result paged at 1000 rows, then collapsed on distinct `(hsCode, currency)`:

| window | pager `totalCount` (rows shown) | distinct `(hsCode, currency)` = old report rows | `columnTotals.noOfLicences` | worst split |
|---|---|---|---|---|
| 2025-01-01 .. 2025-12-31 | **9,213** | **2,881** | 12,435 | `3506990000` THB = 95 rows |
| 2026-01-01 .. 2026-09-14 | **7,420** | **2,690** | 6,496 | `8714109000` THB = 53 rows |

Every returned row carried a `companyName`, so the deployed build still grouped by company. The
customer's own example reproduces exactly — for 2025:

| HS code / currency | rows shown | per-row `noOfLicences` / `totalValue` | old report (one row) |
|---|---|---|---|
| `3918109000` USD | 4 | 1 / 50,000.00 · 1 / 42,400.00 · 1 / 42,400.00 · 1 / 50,000.00 | 4 / **184,800.0000** |
| `3902109000` USD | 4 | 1 / 49,000 · 5 / 53,532 · 3 / 34,680 · 2 / 98,400 | 11 / **235,612.0000** |
| `2106909900` USD | 3 | 1 / 49,694.40 · 1 / 5,206.08 · 2 / 99,388.80 | 4 / **154,289.2800** |

Per currency, 2025: THB 6,735 → 1,823 rows, USD 1,660 → 700, CNY 816 → 356, JPY 1 → 1, MMK 1 → 1.
The sum of every Total Value (14,032,670,046.0979) and the TOTAL footer (12,435) do not move: a
licence belongs to exactly one company, so per-company distinct-licence counts and amounts add.

## Root cause

The same defect already fixed for Import Permit and Border Import Permit (2026-09-05) and for
Export Licence / Border Export Licence (2026-09-08, `docs/ExportLicenceByHSCodeComplaints_2026-09-08.md`).
Border Import Licence was one of the three families deliberately left for a later round.

The old screen (`ReportsController.cs:12366-12467` on `origin/master` of tradenet-2.0-admin)
renders `BorderHSCodeReport.rdlc`, whose only row group is

```xml
<GroupExpression>=Fields!HSCodeId.Value</GroupExpression>      rdlc:1160
<GroupExpression>=Fields!Currency.Value</GroupExpression>      rdlc:1161
```

Its grid is Sr.No. | HS Code | Description | No of Licences | Total Value | Currency
(`rdlc:177/232/287/342/397/452`), Total Value is `=FORMAT(Sum(Fields!Amount.Value),"N4")`
(`rdlc:713`), and the footer is `TOTAL` + `=CountDistinct(Fields!LicenceNo.Value)` under No of
Licences with a blank Total Value cell (`rdlc:878/986`). Legacy `dbo.sp_HSCodeReport` returns raw
item rows (`ORDER BY HSCodeId`, `docs/StoredProcedureDefinitions.sql:4919-4947`); the RDLC does
the grouping.

`sp_HSCodeReport_pagination`'s `'Border Import Licence'` branch had moved that grouping into SQL
with the buyer in the key:

```sql
GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo,tmp.CompanyName,tmp.Currency
ORDER BY result.HSCode,result.CompanyName,result.Currency
```

so one HS code printed once per buyer — visually identical rows, each with that buyer's slice of
Total Value. The LINQ twin `sp_HSCodeReport.GroupsByCompany` returned `true` for this form type,
so the Excel export (which streams the twin through `WriteRowsAsync → GetAggregateRowsAsync →
AggregateQuery`) split the same way. The `ORDER BY` was also not a unique key over that grouping,
so `OFFSET/FETCH` could repeat one row across pages and drop another.

### "Total No of License" was never wrong

12,435 / 6,496 is the separate whole-set `COUNT(DISTINCT LicenceNo)` computed in
`sp_HSCodeReport.CreateAggregateResultAsync` — the RDLC footer's `=CountDistinct(Fields!LicenceNo.Value)`.
It is deliberately not the sum of the per-row No of Licences (a licence spanning five HS codes
contributes to five rows but counts once).

## Parity check against the old screen (CLAUDE.md rule)

| axis | old (`origin/master`) | new | verdict |
|---|---|---|---|
| Filter box | From Date, To Date, Sakhan `--- All ---`, Import Section `--- All ---` (border Import Licence sections), Filter By Start/End (Start first, no blank), HS Code (`BorderImportLicenceByHSCodeReport.cshtml:27-69`) | dateRange, Import Section (`borderImportLicenceSections`), Filter By Start/End default Start, HS Code, Sakhan (`sakhans`) | same filters and values; Sakhan sits last instead of third — the same order the four sibling By-HS-Code reports use |
| Columns | Sr.No. \| HS Code \| Description \| No of Licences \| Total Value (N4) \| Currency | identical after this change (`rowNumberTitle: 'Sr.No.'`, `#,##0.0000`) | match |
| Header | "List of Border Import Licences By HS Code From (dd/MM/yyyy) To (dd/MM/yyyy)" (`ReportsController.cs:12432`) | was singular "Licence"; now the legacy text verbatim | fixed |
| Footer | TOTAL + CountDistinct(LicenceNo); Total Value cell blank | `columnTotals.noOfLicences` only; BasicTable renders no Total Value sum | match |
| Row grain | one row per (HS code, currency) | was one row per (HS code, currency, **company**) | **fixed — the complaint** |
| Drill | HS Code cell → `Reports/BorderHSCodeDetailReport` in a new tab (`rdlc:581`), rendering `HSCodeDetailReport.rdlc`: Sr.No. \| HS Code \| Description \| Company Name \| No of Licences, grouped on (HSCodeId, CompanyRegistrationNo) (`rdlc:1263-1264`), header "List of Border Import Licences By HS Code From (..) To (..)" (`:10589`) | `BorderImportLicenceHSCodeDetailReport`, now `openInNewTab`, posts `GroupBy='Company'` so it keeps the (HS code, company) grain; plural header; `Sr.No.`; 1000 rows | match (also carries `ExportImportSectionId`, which the old URL did not — kept, see below) |
| Page size | RDLC scrolled every row on one page | `defaultPageSize: 1000` | match |

## What changed

### Stored procedure — `StoredProcedureMigrations/sp_HSCodeReport_pagination.sql`

Only the `ELSE IF(@FormType='Border Import Licence')` branch, all three sub-branches
(`@HSCode=''`, `Start`, `End`; this form type has no `@IncludeTotalCount=0` fast page — it always
returns `COUNT(*) OVER()`):

1. `GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency` — the RDLC key. The outer
   `SELECT` keeps its 8-column shape with `CAST(NULL AS nvarchar(200)) CompanyRegistrationNo` /
   `CAST(NULL AS nvarchar(500)) CompanyName`, so `sp_HSCodeAggregateReportResult` is unchanged.
2. `ORDER BY result.HSCode,result.Currency,result.HSCodeId` — a unique key, so the page window is
   deterministic.

The inner derived tables (Pa Tha Ka `UNION ALL` Individual Trading) are byte-identical to before
and to the legacy procedure's branch. The other seven `@FormType` branches are untouched.

Owner decisions carried over from 2026-09-08: HS-code-**string** order on the paged procedure
(not `LegacyOrder`, which needs `PermitCreatedDate`/`PermitId` that licence sources never fill),
and the working Import Section filter stays even though the old form never sent it to the
procedure (`Business/Reports.cs:930+` passes no section to `dbo.sp_HSCodeReport`).

### Application

- `sp_HSCodeReport.GroupsByCompany` returns `false` for `"Border Import Licence"`, so the LINQ twin
  — the Excel export and any Section-filtered grid — groups identically. Only Import Licence and
  Export Permit still group by company by default.
- `BorderImportLicenceByHSCodeReportController` takes `GroupBy` on its request and maps
  `GroupBy='Company'` to `GroupByCompany`; `[ExcelFormatVersion(2)]` so the export queue cannot
  keep serving a cached company-split workbook for a closed period.
- `reportConfigs.ts`: summary gets the plural legacy header, `rowNumberTitle: 'Sr.No.'`,
  `defaultPageSize: 1000`, `dataType: 'number'` on No of Licences, `dataType: 'money'` +
  `#,##0.0000` on Total Value, `openInNewTab` on the HS Code drill. The drill config
  `BorderImportLicenceHSCodeDetailReport` gets the same header/Sr.No./page size and the constant
  `GroupBy: 'Company'` filter (`ReportFilterConfig.constantValue`, never rendered, always posted).
- Excel spec fixtures regenerated (`npm run fixtures:excel`): only the two Border Import Licence
  HS Code fixtures and their `index.json` rows changed (header line, `Sr.No.`, data types).

### Release folder — `StoredProcedureMigrations/Deployments/2026-09-14_BorderImportLicenceByHSCodeParity/`

`CaptureRollback.sql` → `00_RunAll.sql` (or `01_sp_HSCodeReport_pagination.sql`) →
`VerifyDeployment.sql`, then deploy the application. `VerifyDeployment.sql` inspects only the
Border Import Licence branch of the deployed definition, re-runs the 2025 window expecting
2,881 rows / `TotalCount` 2,881 / no duplicate `(HSCode, Currency)` / company columns NULL /
`SUM(TotalValue)` 14,032,670,046.0979 exactly, joins the legacy `dbo.sp_HSCodeReport` output
per `(HSCode, Currency)` expecting 0 mismatches and 12,435 distinct licences, and page-walks at
10 rows a page expecting 2,881 distinct rows over pages 0..288.

Do **not** re-run the `sp_HSCodeReport_pagination` copies under `Done For Fix/2026-09-05_ImportPermitParityRound1`,
`2026-09-05_BorderImportPermitComplaints` or `2026-09-08_ExportLicenceByHSCodeParity` — frozen
snapshots that would revert this change.

## Expected result

Recomputed from the PROD rows by collapsing on `(hsCode, currency)`:

```
2025-01-01..2025-12-31       9,213 rows -> 2,881   Total Value sum 14,032,670,046.0979 unchanged   footer 12,435
2026-01-01..2026-09-14       7,420 rows -> 2,690                                                  footer  6,496
3918109000 USD               4 rows -> 1 row: 4 licences, 184,800.0000
```

At `defaultPageSize: 1000`, 2025 prints on three pages (1000 / 1000 / 881) instead of ten.

## Verification done

- `Backend.Tests/BorderImportLicenceByHSCodeParityTests.cs` — 7 tests: request flags, the LINQ
  GROUP BY for summary (4 key columns, no company) and drill (3 columns, `CompanyRegistrationNo`,
  no `CompanyName`), the `GroupBy` round-trip through the controller, text assertions that all 3
  procedure sub-branches carry the RDLC key, the unique ORDER BY and the typed-NULL company
  columns, the `ExcelFormatVersion ≥ 2` attribute, and proc-vs-LINQ agreement.
- `reportConfigs.borderImportLicence.test.ts` — drilldown now `openInNewTab`, plus two new cases
  pinning the RDLC column titles, Sr.No., N4 money format, page size, plural headers, Start/End
  options and the drill's `GroupBy` constant.
- Backend suite with the mandated DB-skip filter, branch vs a clean-`main` worktree: **7 failures /
  1,784 passed** against **10 / 1,774** — 0 new failures; the 3 `DeploymentFolderContractTests`
  turn green because a dated release folder exists again. The 7 remaining are the pre-existing
  `ReportControllerBranchDefaultsTests` (2) and `ReportEndpointPayloadFixtureTests` (5)
  "missing TryCreateReportRequest" family.
- Frontend `npx vitest run`, branch vs clean-`main` worktree: **6 failures / 1,561 passed** against
  **6 / 1,559** — identical failures (pre-existing ActualAmendment "From" subtitles and lookup
  cases), 2 new passing tests. `npm run build` (tsc + vite) clean.
- Four independent adversarial review passes (SQL/deploy scripts, grid-vs-Excel agreement, legacy
  parity, contracts/tests): 0 blockers; the should-fix items (unbounded `HSDescription` width in
  the verify temp tables, stale wording in the 2026-09-08 write-up and `ReportColumnComparison.md`,
  mixed legacy line numbering) are applied in this change.

## Still owed

- **The procedure is hand-deployed.** Apply the release folder, then re-run the live check
  expecting `totalCount` 2,881 for 2025 and `companyName` NULL on every row. Until it is applied,
  an app deploy makes the .xlsx (LINQ twin) print the new grain while the grid still pages the
  company-split procedure — the two surfaces disagree in the opposite direction. Deploy the
  procedure **before** merging to `main`.
- Not executed against a database from here: the report DB is CGNAT-internal, so every number
  above comes from the live API or arithmetic on its rows; the procedure edit was checked
  structurally (balanced blocks, 3/3/3 substitutions, branch byte-diff against the Border Export
  Licence twin) and by the text assertions, not by running it.
- **Left as-is, deliberately:** the Import Section dropdown filters in the new report while the old
  form never sent it to the procedure; the drill also carries it. Same ruling as 2026-09-08.
- The LINQ path (`AggregateQuery`) still orders on `(HSCode, Currency)` with no `HSCodeId`
  tie-break — pre-existing on every form type that takes it; only matters where two HS codes share
  one code string.
- **Import Licence** and **Export Permit** By HS Code are the last two families still grouping by
  company. Same recipe: one proc branch, `GroupsByCompany`, a `GroupBy='Company'` pin on the
  drill config, `[ExcelFormatVersion(n+1)]`.

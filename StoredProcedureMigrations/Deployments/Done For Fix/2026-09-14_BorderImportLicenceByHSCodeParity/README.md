# 2026-09-14 — Border Import Licence By HS Code parity

One procedure changes: `dbo.sp_HSCodeReport_pagination`, and only its
`'Border Import Licence'` branch (3 sub-branches).

| Run order | File | Procedure |
|---|---|---|
| 1 | `01_sp_HSCodeReport_pagination.sql` | `dbo.sp_HSCodeReport_pagination` |

## Why

Customer complaint, 2026-09-14, **Border Import Licence By HS Code Report**:

> 1. Data ထွက်ရှိမှု မမှန်ပါ အဟောင်းနဲ့ အသစ် မတူပါ (ဘာလို့မတူလဲဆိုရင် အသစ်မှာ company name column
> တစ်ခု ပိုနေတယ် အဲ့တော့ hS တစ်ခုက USD နဲ့ ၃ ကြောင်းလျှောက်ထားရင် ၃ ကြောင်းလုံး ပြနေတော့
> အဟောင်းနဲ့တိုက်စစ်ရင်လွဲနေပါတယ်) အဟောင်းမှာက HS တစ်ခု က USD တန်ဖိုးနဲ့ ၄ ကြောင်းလျှောက်ထားရင်
> ၄ ကြောင်းတန်ဖိုး ပေါင်းပြလိုက်တာပါ

Gloss: the new report's data is wrong versus the old one — the new one carries an extra
company-name grouping, so one HS code in USD applied 3 times shows 3 rows; the old report
showed ONE row with the values summed.

Measured on the live PROD API (FilterType `Start`, HSCode `''`, Sakhan 0, Section 0),
paging the whole result and collapsing on distinct (HS code, currency):

| Window | Old report rows | Before | After | Total No of License |
|---|---|---|---|---|
| 2025-01-01 → 2025-12-31 23:59:59 | 2,881 | 9,213 | **2,881** | 12,435 (already matched) |
| 2026-01-01 → 2026-09-14 23:59:59 | 2,690 | 7,420 | **2,690** | 6,496 (already matched) |

For 2025, 1,413 pairs were split; the worst, `3506990000` / THB, printed 95 times. Per
currency, rows before → after: THB 6,735 → 1,823, USD 1,660 → 700, CNY 816 → 356, JPY 1 → 1,
MMK 1 → 1. The sum of every Total Value (14,032,670,046.0979) is unchanged — the rows
merge, the money does not move.

### Root cause

The old screen (`ReportsController.cs:12366-12467` on `origin/master` of tradenet-2.0-admin) renders
`BorderHSCodeReport.rdlc`, whose **only** row group is `=Fields!HSCodeId.Value` +
`=Fields!Currency.Value` (`rdlc:1159-1162`). Its grid is `Sr.No. | HS Code | Description |
No of Licences | Total Value | Currency` — no company column — with `Total Value` =
`FORMAT(Sum(Fields!Amount.Value),"N4")` and a `TOTAL` footer of
`=CountDistinct(Fields!LicenceNo.Value)` under `No of Licences` (blank `Total Value` cell).
Legacy `dbo.sp_HSCodeReport` returns raw item rows (`ORDER BY HSCodeId`), no SQL grouping.

Our paged procedure's `'Border Import Licence'` branch `GROUP`ed `BY tmp.HSCode,
tmp.HSDescription, tmp.CompanyRegistrationNo, tmp.CompanyName, tmp.Currency` and `ORDER`ed
`BY result.HSCode, result.CompanyName, result.Currency` — one row per buyer company,
visually identical rows each carrying a partial Total Value. The LINQ twin
(`Backend/StoredProcedureToLinq/sp_HSCodeReport.cs` `GroupsByCompany()`), which the Excel
export streams, did the same.

`Total No of License` was never wrong: it is a separate whole-set
`COUNT(DISTINCT LicenceNo)` computed in C#, exactly the RDLC footer.

## What changes, per sub-branch

All 3 sub-branches of `'Border Import Licence'` (`@HSCode=''` / `@FilterType='Start'` /
`End`). This FormType has no `@IncludeTotalCount=0` fast page — every sub-branch returns
`COUNT(*) OVER() TotalCount`:

1. `GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency` — the RDLC key. The
   outer `SELECT` keeps its 8-column list and order, with
   `CAST(NULL AS nvarchar(200)) CompanyRegistrationNo` /
   `CAST(NULL AS nvarchar(500)) CompanyName`, so the caller's DTO
   (`sp_HSCodeAggregateReportResult`) is unchanged. Same shape the `'Import Permit'`,
   `'Export Licence'` and `'Border Export Licence'` branches already use.
2. `ORDER BY result.HSCode,result.Currency,result.HSCodeId` — a unique key, so the page
   window is deterministic even where two `HSCode` rows share a code string. HS-code-STRING
   order is kept on purpose (owner decision 2026-09-08): licence sources do not fill
   `PermitCreatedDate` / `PermitId`, so `LegacyOrder` is not an option here.
3. The inner derived tables (the `UNION ALL` of the Pa Tha Ka and Individual Trading
   halves, the `LicenceDate` window, the `SakhanId` filter, the `HSCode.Code LIKE`
   predicates) are byte-identical to before.

The Import Section filter keeps working as before (owner decision 2026-09-08), even though
the old form never sent it to the proc.

**Unchanged:** the other seven `@FormType` branches. Import Licence and Export Permit By HS
Code still group by company — the last two families carrying the defect.

## Application changes shipped with it

The procedure is only half the fix — deploy the application too, or the grid gets the new
grain while the `.xlsx` keeps the old one:

- `sp_HSCodeReport.GroupsByCompany` returns `false` for `'Border Import Licence'`, so the
  LINQ twin (`AggregateQuery`, which is what the **Excel export** streams) groups
  identically.
- `BorderImportLicenceByHSCodeReportController` accepts `GroupBy` and sets
  `GroupByCompany`; the `BorderImportLicenceHSCodeDetailReport` drill config posts
  `GroupBy='Company'` — that is how the drill keeps `HSCodeDetailReport.rdlc`'s
  `(HSCodeId, CompanyRegistrationNo)` grain (`rdlc:1262-1265`) and its Company Name column.
- `[ExcelFormatVersion(2)]` on the controller — the export queue caches finished workbooks
  by payload + version, so without the bump a closed-period request keeps serving the
  company-split sheet.
- Presentation parity: `Sr.No.` row header, `Total Value` as `#,##0.0000` (the RDLC's
  `FORMAT(…,"N4")`), `defaultPageSize: 1000` (the RDLC scrolled every row on one page), the
  plural legacy header "List of Border Import Licences By HS Code"
  (`ReportsController.cs:12432` on `origin/master`), the drill opening in a new tab (`rdlc:581`
  `window.open(…,'_blank')`).

## Deploy order

**Procedure first, then the application.** Until the procedure is applied the grid still
pages the company-split proc while the `.xlsx` already has the new grain — the two would
disagree with each other as well as with the old report.

## How to run

1. `CaptureRollback.sql` — save the result grid. That text IS the rollback (swap the leading
   `CREATE` for `CREATE OR ALTER`). It also captures legacy `dbo.sp_HSCodeReport`, the oracle
   for the 2,881 / 12,435 figures.
2. `00_RunAll.sql` — or `01_sp_HSCodeReport_pagination.sql` on its own; they are the same
   procedure text.
3. `VerifyDeployment.sql` — section 1 before and after, sections 2-4 after, section 5 in
   the browser once the application is deployed.
4. Deploy the application.

⚠ **Do not re-run** the `sp_HSCodeReport_pagination` copies under
`Deployments/Done For Fix/2026-09-05_ImportPermitParityRound1/`,
`.../2026-09-05_BorderImportPermitComplaints/` or
`.../2026-09-08_ExportLicenceByHSCodeParity/`. Those are frozen snapshots of what was
deployed on those dates and would revert this change.

`checksums.txt` records the SHA-256 of the repository original this copy was taken from;
verify with `shasum -a 256 -c checksums.txt` from `StoredProcedureMigrations/`.

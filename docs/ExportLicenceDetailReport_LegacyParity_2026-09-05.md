# Export Licence Detail Report — legacy parity (2026-09-05)

Customer complaint (Burmese): the new `ExportLicenceDetailReport` shows no data, is slower than
Tradenet 2.0 and its data is wrong; the old admin shows the right rows for the same search. Fix
it so the data is right — if need be, use the old stored procedure directly and produce the same
answer in the same format.

## What the old report is

`ReportsController.ExportLicenceDetailReport` → `LicencePermitReports.GetExportLicenceDetailReport`
→ `EXEC dbo.sp_ExportLicenceDetailReport 'Oversea', From 00:00:00, To 23:59:59, PaThaKaTypeId,
SectionId, MethodId, IncotermId, 0, '', 0` → `ExportLicenceDetailReport.rdlc` (28 English columns,
**no total row**, title `List of Export Licences By Detail From (dd/MM/yyyy) To (dd/MM/yyyy)`).
Grain is one row per `ExportLicenceItem`; 13 `INNER JOIN`s; `ApplyType='New' AND Status='Approved'
AND CreatedDate BETWEEN From AND To`; `CASE WHEN @X=0 THEN col ELSE @X END` filters;
`ORDER BY ExportLicence.LicenceDate`. Filter form: From, To, EIR Card Type, Export Section,
Method of export, Incoterms. (`docs/StoredProcedureDefinitions.sql:3922-3992` = the UAT text.)

## What was wrong in the new grid

| # | Cause | Effect |
|---|---|---|
| 1 | `sp_ExportLicenceDetailReportV2.cs` paged **licences** (`OFFSET n FETCH 10` over licence keys, then every item of those licences) while `TotalCount` counted **items** | 636 licences / 2683 items → pager offers 269 pages, page 1 shows ~40 rows, pages 65-269 are empty: "data does not come out" |
| 2 | `BasicTable` never reset `pageIndex` on a new Search / drill | choosing Method of export or Incoterms while on page ≥ 2 asked for licences that do not exist → "no data" for CIF / Normal LC. The SQL itself was fine (Method=CMP → 2343 rows, Incoterms=CIF → 10, both = legacy) |
| 3 | ~300 round trips per page (licence keys + per-licence item keys + 7 lookups per item row) + a full item `COUNT` + a per-currency `GROUP BY` over the whole range on every load | slow; the default range is three months (~150k items) |
| 4 | Per-currency footer the RDLC never had | extra full aggregate per load; customer rejected such footers before |

## The fix

**`dbo.sp_ExportLicenceDetailReportV3_pagination`** (`StoredProcedureMigrations/sp_ExportLicenceDetailReportV3_pagination.sql`)
is the legacy `'Oversea'` query **verbatim** — same joins, same predicates, same select list — with
item-grain key-first paging wrapped around it: `#L` licence keys (hinted filtered index
`IX_ExportLicence_Report_NewDetail_Page` + the legacy licence-level joins/WHERE) → `#K` item keys
(legacy item-level joins) → `#P` one page of keys (`ROW_NUMBER` over `LicenceDate, IssuedDate,
licence Id, ItemNo, item Id, UniqueId`) → the legacy select list for those keys only. `@PageSize <= 0`
returns every row (parity checks). `@Auto` is additive (default `N''`) for the By-X drill-downs.

Why not `EXEC` the legacy procedure and page in C#: on UAT it took 15-17 s for a two-day window and
**335 s for one month**; the grid's default range is three months.

| UAT measurement (203.81.66.111) | legacy proc | V3 |
|---|---|---|
| 2025-08-31..09-01, page of 10 | 12-17 s (all 2683 rows) | 0.9-2.9 s, `TotalCount` 2683 |
| same window, every row, 38 columns compared | 2683 rows | 2683 rows, **multiset equal** |
| Method = CMP (3) / Incoterms = CIF (12) | 2343 / 10 rows | 2343 / 10 |
| 2025-06-01..08-31 (149,805 rows), page 0 | timeout (> 280 s) | 3.2-6.8 s (one 32 s outlier on a busy box); `#L` 1-9 s, `#K` 0.6 s, `#P` 0.2 s |
| same window, offset 50,000 / 1000-row page | — | 4-19 s / 5-9 s |

Materialising `#P` before the display join is load-bearing: without it, page 0 of the three-month
window once took 103 s (plan flip on `OFFSET 0 FETCH 10`).

Application side:

- `Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReportV3.cs` — `EXEC` wrapper; on SQL error
  2812 (procedure not deployed) it falls back to `sp_ExportLicenceDetailReport_Fast.CreatePagedResultAsync`
  (item-grain LINQ, the Excel export's query: same rows, slower). Needed because the app auto-deploys
  to production while procedures are hand-run.
- `ExportLicenceDetailReportController` — grid → V3; footer removed; `IExcelNoFooterReport` +
  `[ExcelFormatVersion(2)]` so the cached `.xlsx` is rebuilt without a footer. Excel rows stay on
  `_Fast.StreamResolvedChunksAsync` (already equal to the old report's count).
- `sp_ExportLicenceDetailReportV2.cs` — only the By-X / Daily summary helpers remain.
- `reportConfigs.ts` — `currencyTotalsColumns` removed, `eagerTotalCount: true`.
- `BasicTable.tsx` — the page-1 reset on a new Search (cause #2) landed from a concurrent session in the
  same working tree: a render-phase `lastRefreshKey` check that calls `setPageIndex(0)` (pinned by
  `Frontend/src/components/BasicTablePaging.test.ts`). Nothing in this change duplicates it.
- `StoredProcedureMigrations/sp_ExportLicenceDetailReportV2_pagination.sql` deleted (stale licence-grain
  artifact the runtime never used).

## Parity decisions

- Columns: 28/28 match the RDLC. `Application Date` / `Application No` / `Commodity Type` are blank in
  the old app (its model never mapped them); the new grid fills them from `ExportLicence`. Kept.
- Filter box: the old form has no Company Registration No box; the new one keeps it (additive, `''` =
  All, the Company List drill-down carries the value). Remove if strict form parity is wanted.
- Footer: none (RDLC has none).

## Tests

- `Backend.Tests/ExportLicenceDetailReportContractTests.cs` — pins the legacy predicates verbatim in
  the V3 file, the item-grain count, no dynamic SQL, the controller wiring, the 2812 fallback, no footer.
- `Backend.Tests/ExportLicenceDetailReportLiveDbTests.cs` — `Detail_grid_matches_legacy_sp_ExportLicenceDetailReport`:
  `EXEC dbo.sp_ExportLicenceDetailReport` (oracle) vs V3 `@PageSize = 0` → same count, same sorted
  multiset (14 columns), API `TotalCount` and Excel row count equal to it; windows 2025-08-31..09-01 and
  2025-05-01..05-02, filters none / Method=3 / Incoterms=12. Env-gated on
  `TRADENET_REPORT_TEST_CONNECTION_STRING`.
- `Frontend/src/Report/config/reportConfigs.exportLicence.test.ts` — no footer, `eagerTotalCount`.

## Deployment

`StoredProcedureMigrations/Deployments/2026-09-05_ExportLicenceDetailLegacyParity/` (README, `00_RunAll.sql`,
`CaptureRollback.sql`, `VerifyDeployment.sql`, `checksums.txt`). Deployed and verified on UAT 2026-09-05.
Production (`tn2db`, CGNAT-internal) must be run by hand; until then the grid uses the LINQ fallback.
`SET QUOTED_IDENTIFIER ON` is required (XML `.value()`).

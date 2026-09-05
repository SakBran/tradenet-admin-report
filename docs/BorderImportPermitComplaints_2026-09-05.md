# Border Import Permit — customer complaints, 2026-09-05

Source: `Border Import Permit To Fix List.md` (repo root), six reports.

The list asked to **re-test with 2025 data and report back** before fixing anything. The live
production API (`reportapi.myanmartradenet.com`) answers a minted JWT from this machine, so every
complaint was measured end-to-end against the real deployment rather than reasoned about. `pymssql`
cannot log in to `tn2db` from the Mac — TCP opens, the TDS/TLS handshake stalls — so the API, not
direct SQL, is the harness here.

**A 2025 window reproduces the customer's numbers exactly.** All figures below are
`2025-01-01 → 2025-12-31`, all-Sakhan vs Sakhan = TCL (`Sakhan.Id` 4).

| Report | all Sakhan | TCL | customer said | verdict |
|---|---|---|---|---|
| Detail | 70 rows | 20 rows | 70 all / old TCL 20 | matches the old report exactly |
| By Section | 3 rows, 18 licences | 3 rows, 4 licences | 18 all / old TCL 4 | matches exactly |
| Company List | 13 rows, 18 licences | 3 rows, 4 licences | old 13, UI 10, Excel 13 | data right, grid wrong |
| By HS Code | 31 rows, 18 licences | 4 rows, 4 licences | UI 29, Excel 31 | grid loses 2 rows |
| Voucher (ApplyType New) | 28 rows | 8 rows | 28 all / old TCL 8 | matches exactly |
| New Report | 18 permits | 4 permits | — | no TOTAL footer at all |

## "Sakhan ဖြင့်ရှာရင် data မထွက်" — not a filter bug

The Sakhan chain is correct at every hop (`Sakhan.Id` from the lookup → `SakhanId: int` on the
request → the same `CASE WHEN @SakhanId=0` predicate the legacy procedures use), and TCL returns
precisely the numbers the customer quotes from the old report: Detail 20, By Section 4, Voucher 8.

The bug was in the grid. `BasicTable` kept `pageIndex` in state and only the pager ever wrote it;
applying filters merely bumped `refreshKey`, and the table is never remounted. So browsing Detail
all-Sakhan (70 rows = 7 pages), clicking to page 3, then picking a Sakhan (20 rows = 2 pages)
re-requested **page 3 of a 2-page result** and rendered nothing. Voucher: 28 rows → 8. This affected
every report in the application, not this family.

Fixed in `Frontend/src/components/My Components/Table/BasicTable.tsx` by adjusting `pageIndex`
during render when `refreshKey` changes (an effect would fire one wasted request for the stale page
first). Guarded by `Frontend/src/components/BasicTablePaging.test.ts`.

## "Old report shows 997 licences / 856 rows" — a Tradenet 2.0 bug

`ReportsController.cs:15465` sets `model.FormType = AppConfig.ImportPermit` — `"Import Permit"`, not
`"Border Import Permit"` — so the old Border Import Permit By HS Code screen queries the **oversea**
`ImportPermit` / `ImportPermitItem` tables and ignores `@SakhanId` entirely. Its Sakhan dropdown is
decorative. 997/856 are oversea numbers; 18 licences is the correct border figure, and it agrees
with By Section and Company List. The Border Export Permit twin has the same defect
(`ReportsController.cs:12754`); the two Border *Licence* HS Code reports are correct.

Decision taken with the owner: keep the new report border-only, fix the real paging bug, and tell
the customer why the old number was larger.

## Fixes

| Fix | What | Where |
|---|---|---|
| A | grid returns to page 1 when filters change | `BasicTable.tsx` |
| B | New Report per-currency TOTAL block (`BorderNewReport.rdlc` Tablix2), in the grid and the .xlsx | `sp_ImportPermitListingCurrencyTotals.sql` new Border `New` branch + `BorderImportPermitNewReportNewReportController` + `[ExcelFormatVersion(2)]` |
| C | By HS Code lost one row per page boundary (10+10+9 = 29 of 31) | `@FetchSize` in `sp_HSCodeReport_pagination.sql`; `sp_HSCodeReport.cs` stops inflating `@PageSize` |
| D | summary reports print on one page like the RDLC | `defaultPageSize: 1000` on By HS Code, HS Code Detail, By Section, By Seller Country, Company List |
| E | New Report admitted one extra day | `sp_NewReport_pagination.sql` Border branches → `DATEADD(day, 1, CONVERT(date, @ToDate))` |
| F | By HS Code split rows by company; the RDLC groups on (HSCodeId, Currency) | proc `@HSCode=''` branch + `sp_HSCodeReport.GroupsByCompany` + config column |
| G | Sakhan dropdown hid inactive stations the old one showed; Voucher footer LINQ lacked the `TransactionFormType` discriminator | `ReportLookupsController.cs`, `sp_VoucherReport.cs` |

### C — the deploy-order trap

`sp_HSCodeReport.cs` used to send `pageSize + 1` as `@PageSize` so `CreateFastPageFromRows` could
tell whether a next page exists without a `COUNT(*)`. The procedure derives its `OFFSET` from
`@PageSize` too, so page 2 of a 10-row page started at row 12. Measured before the fix: pages of
10 / 10 / 9 over a 31-row set.

The sentinel now widens `FETCH NEXT` only (`@FetchSize`). The C# side was also made independent
of it: seven of the eight FormType branches compute `COUNT(*) OVER()` whether or not the caller
asks, so `sp_HSCodeReport.cs` now builds an exact page from that count — correct against an old or
new procedure, and the pager gains a reachable last page. Only Export Licence's count-less fast
page still needs `@FetchSize` deployed. The procedure serves all eight families, so re-check paging
on every HS Code report (`VerifyDeployment.sql` §5).

### E — measured, not inferred

`@ToDate = 2025-01-12 23:59:59` returned 2 permits, both dated 2025-01-13, while By Section (LINQ,
`<= ToDate`) returned 0 for the same window. Legacy `sp_NewReport` used `CreatedDate <= @ToDate`.
Border Export Permit carried the identical defect and was flipped in the same pass, together with
its footer in `sp_ExportPermitListingCurrencyTotals.sql` (which held a `TODO` to stay in step).

### F — 31 rows becomes 16

`BorderHSCodeReport.rdlc` groups on `HSCodeId` + `Currency` (rdlc:1157-1169) and renders no company
column. Grouping by company as well split one HS code into a row per buyer, each with a partial
Total Value. The `Start`/`End` sub-branches keep the company: they also serve
`BorderImportPermitHSCodeDetailReport`, whose `HSCodeDetailReport.rdlc` does render Company Name.
The same fix was applied to the plain `Import Permit` branch in round 1; Border was missed.

## Not done, deliberately

* **LEFT JOIN on `Sakhan`.** Considered as hardening, then rejected: every legacy procedure uses
  `INNER JOIN Sakhan` (`docs/StoredProcedureDefinitions.sql:5763`, `:6782`, `:8957`), so a LEFT JOIN
  would show rows the old report never showed. Count orphan/NULL `BorderImportPermit.SakhanId` rows
  before revisiting.
* **Voucher default `ApplyType`.** Already `'New'` in the config; the procedure's
  `ApplyType = @ApplyType` is a strict equality, so a blank value legitimately returns nothing.
* Reproducing the old 997/856 figures — see above.

## Deployment

`StoredProcedureMigrations/Deployments/2026-09-05_BorderImportPermitComplaints/` — four procedures,
`CaptureRollback.sql` first, then `00_RunAll.sql`, then `VerifyDeployment.sql`, then the application.
The copies in `2026-09-04_AmendActualAmendParity`, `2026-09-05_ImportPermitParityRound1` and
`2026-09-06_ExportPermitItemOrder` were re-synced because they ship the same procedures.

## Test baseline

Unchanged from `9fb4b55`: backend non-DB suites 11 failed / 1662 passed; `Frontend` 8 failed / 1508
passed. The Excel spec fixtures were regenerated (`npm run fixtures:excel`), which also picked up two
`ExportLicence*` fixtures left stale by the previous round.

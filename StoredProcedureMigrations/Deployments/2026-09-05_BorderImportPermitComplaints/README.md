# 2026-09-05 — Border Import Permit customer complaints

Four procedures. Run order **01 → 04**, or `00_RunAll.sql` in one go. **Procedures first,
application second** — see the deploy-order note below, it is load-bearing for 02.

Source complaints: `Border Import Permit To Fix List.md` (repo root). Every number quoted
here was measured against the live report API over **2025-01-01 … 2025-12-31**; a 2026
window is empty in this database and proves nothing.

| # | Procedure | Why |
|---|---|---|
| 01 | `sp_NewReport_pagination` | Border Import/Export Permit New branches admitted one extra day |
| 02 | `sp_HSCodeReport_pagination` | paging lost a row per page boundary; Border HS Code summary split rows by company |
| 03 | `sp_ImportPermitListingCurrencyTotals` | Border Import Permit New Report had no TOTAL footer at all |
| 04 | `sp_ExportPermitListingCurrencyTotals` | keeps the Border Export Permit New footer in step with 01 |

## 01 — the extra day

The Border branches windowed on `< DATEADD(day, 1, @ToDate)` while callers pass `@ToDate`
as `'<day> 23:59:59'`, so the window reached a full day past the one selected. Legacy
`dbo.sp_NewReport` used `CreatedDate <= @ToDate`. Measured: `@ToDate = 2025-01-12 23:59:59`
returned 2 permits actually dated 2025-01-13. Both Border branches now use the
calendar-date form the other six branches already used.

## 02 — two fixes, and the deploy-order trap

**(a) `@FetchSize`.** The grid asks for one row more than a page so it can tell whether a
next page exists without paying for `COUNT(*)`. That sentinel used to be added by inflating
`@PageSize` in C# — but the procedure derives its `OFFSET` from `@PageSize` too, so page 2
of a 10-row page started at row 12 and one row vanished at every boundary: a 31-row report
displayed 10 + 10 + 9 = **29**, which is exactly the "UI 29, Excel 31" complaint. The
sentinel now widens `FETCH NEXT` only.

The application no longer depends on this procedure for the reports in the complaint: seven of
the eight FormType branches compute `COUNT(*) OVER()` unconditionally, so `sp_HSCodeReport.cs`
now builds an exact page from the count the procedure already returns and never needs the
sentinel. Those reports are correct against an old **or** new procedure — and they gain a
working pager (a reachable last page) as a side effect.

`@FetchSize` still matters for the one branch that answers a fast page with no count,
**Export Licence**: until this procedure is deployed, its By HS Code grid loses the next-page
marker and stops at page 1. Deploy the procedure promptly; nothing else in this release is
order-sensitive.

The change touches paging for **all eight FormType families**. Section 5 of
`VerifyDeployment.sql` sweeps them.

**(b) Border Import Permit HS Code grouping.** The `@HSCode=''` sub-branch groups on
`(HSCodeId, Currency)` only. `BorderHSCodeReport.rdlc`'s row group is exactly that
(rdlc:1157-1169) and it renders no company column, so the extra company key split one HS
code into a row per buyer, each carrying a partial Total Value: **31 rows where the old
report's shape gives 16**. The `Start`/`End` sub-branches deliberately KEEP the company —
they also serve `BorderImportPermitHSCodeDetailReport`, whose `HSCodeDetailReport.rdlc`
does render Company Name.

## 03 — the missing TOTAL

`BorderNewReport.rdlc` prints a second tablix next to the grid: one
`<CUR>: n licence(s)` + summed Total Value row per currency, then a grand
`TOTAL / Total: n licence(s)`. This procedure previously returned a deliberately empty set
for Border New, so the report had no footer in the grid **or** the `.xlsx` (the Excel footer
is resolved by replaying the grid action). The new branch mirrors
`sp_NewReport_pagination`'s Border Import Permit branch: `SUM(items)` for the amount — not
the `TOP 1` the Amend branches use — and the same calendar-date window as 01.

Expected: `SUM(NoOfLicences)` = 18 all-Sakhan, 4 for Sakhan = TCL (`Sakhan.Id` 4).

## 04 — keeping the pair in step

The Border Export Permit New footer carried a `TODO` to stay on the bare `DATEADD` form for
as long as its grid did. 01 flips that grid, so the footer flips with it; otherwise the
footer count stops matching the rows it sits under.

## Relationship to `2026-09-05_ImportPermitParityRound1`

That release ships two of these same procedures. Its copies, `checksums.txt` and
`00_RunAll.sql` were regenerated from the current sources, so both folders now carry
identical text and either one is safe to run — this folder is the one to run if you are
applying only this release.

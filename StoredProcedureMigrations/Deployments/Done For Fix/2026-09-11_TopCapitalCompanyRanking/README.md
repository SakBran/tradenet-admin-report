# 2026-09-11 — List of Top Capital Company ranking

One procedure. Ships with the frontend/backend change that restores the old screen's
**No of List** filter to PaThaKa → List of Top Capital Company.

| Run order | File | Procedure |
|---|---|---|
| 1 | `01_sp_PaThaKaReport_pagination.sql` | `dbo.sp_PaThaKaReport_pagination` |

`00_RunAll.sql` applies the same procedure in one script, with the `USE [TradeNetDB]`
target and `SET QUOTED_IDENTIFIER ON` the repository's other releases use.

## Sequence

1. `CaptureRollback.sql` — save the current definition (the rollback artifact).
2. `00_RunAll.sql` (or `01_…`).
3. `VerifyDeployment.sql`.
4. **Then** deploy the application.

The order matters more than usual here. Against a stale procedure the new controller
still returns `No of List` rows, but they are the *most recently issued* companies rather
than the *largest by capital* — a wrong answer that looks perfectly healthy on screen.

## What changed

One entry in the `@SortColumn` whitelist:

```sql
WHEN 'Capital'                 THEN 'PaThaKa.Capital'
```

## Why

The old admin's report (`Business/Reports.cs:2533` in tradenet-2.0-admin) ranked the whole
result set in C# before showing it:

```csharp
lst = lst.OrderByDescending(x => x.Capital).Take(model.TotalRecords).ToList();
```

`model.TotalRecords` is the required **No of List** box
(`Views/Reports/PaThaKaTopCapitalCompanyReport.cshtml:61-68`, default `10`). The rewritten
report had neither: no filter, and no way to rank — `'Capital'` was not in the whitelist,
so every request fell through to `ELSE 'PaThaKa.IssuedDate'`. It was a *list of companies*,
not a *list of top capital companies*.

With this entry, `ListOfTopCapitalCompanyController` asks the procedure for
`ORDER BY Capital DESC` + `FETCH NEXT @TotalRecords ROWS ONLY` and pages the grid inside
that window. `NULL` capitals sort last under `DESC`, matching
`OrderByDescending` in LINQ-to-Objects.

## Blast radius

`sp_PaThaKaReport_pagination` is shared with
`PaThaKaRegisteredBusinessOrganizationReportController`. That report never sends
`SortColumn = 'Capital'`, and the `WHERE` clause, the projection, the `OFFSET/FETCH`
paging and every other whitelist entry are untouched — so it is unaffected. Section 3 of
`VerifyDeployment.sql` checks that.

The legacy `dbo.sp_PaThaKaReport` is not touched by this release.

# Export Licence complaints 2026-09-05 — checks that need a database

Two items from the complaint list cannot be settled from the developer machine: production
(`tn2db.myanmartradenet.com,14133`) is CGNAT-internal. Run these from the Build Server
(`dev_vm3`) or anywhere the report database is reachable.

Everything else in the list is fixed in code — see the branch diff.

---

## 1. Actual Amendment shows 38 rows where Tradenet 2.0 shows 17

Expected to be **deployment drift, not a code bug.** The repo's
`sp_ActualAmendReport_pagination` Export Licence branch matches the legacy
`dbo.sp_ActualAmendReport` (`docs/StoredProcedureDefinitions.sql:505-540`) predicate for
predicate — same `ApplyType='Actual Amend'`, `Status='Approved'`, section / amend-remark /
company guards — differing only in the date window, which was deliberately moved to
`CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))` (equivalent to the old
`<= @ToDate` at 23:59:59, and the fix for an earlier "one extra day" complaint).

Stored procedures are never deployed by the app or by `auto-deploy-watch.ps1`; production is
migrated by hand. `Deployments/2026-09-04_AmendActualAmendParity/` is in git but may never
have been run against production.

**Step 1 — what is actually deployed:**

```
StoredProcedureMigrations/Deployments/2026-09-04_AmendActualAmendParity/VerifyDeployment.sql   -- section 1
```

`date_pred` must read `ok`. `DATEADD-raw (extra day)` means production still carries the old
definition, which reproduces the customer's 38 exactly.

**Step 2 — if it is stale, re-run** `00_RunAll.sql` from that folder with
`SET QUOTED_IDENTIFIER ON`.

**Step 3 — confirm against the customer's own window** (section 3 of that script covers the
Border reports; this is the plain Export Licence one from this complaint):

```sql
DECLARE @f datetime = '2026-08-31 00:00:00', @t datetime = '2026-09-01 23:59:59';

-- oracle: what the old report prints
EXEC dbo.sp_ActualAmendReport            N'Export Licence', @f, @t, 0, 0, N'', 0;
-- new grid + its TotalCount
EXEC dbo.sp_ActualAmendReport_pagination N'Export Licence', @f, @t, 0, 0, N'', 0, NULL, NULL, 0, 0, 1;
-- footer: per-currency licence counts must add up to the same number
EXEC dbo.sp_ExportLicenceListingCurrencyTotals N'Export Licence', N'Actual Amend', @f, @t, 0, N'', 0, 0;
```

All three must agree. The same three-way comparison now runs automatically as
`Backend.Tests/ExportLicenceAmendParityLiveDbTests` — set
`TRADENET_REPORT_TEST_CONNECTION_STRING` and run the suite wherever the database is reachable,
and it asserts old-proc row count == API `TotalCount` == footer licence count for both the
Amendment and Actual Amendment reports across several windows.

If the procedures are current and the counts still disagree, that is a genuine new bug and the
oracle above will show which rows the new query adds.

---

## 2. Method of Export / Incoterms return no rows for a specific value

No code defect found at any layer: the dropdowns send the numeric lookup `Id`
(`ReportLookupsController.GetExportLicenceMethods` / `GetExportLicenceIncoterms`, scoped to
`Type='Export' AND IsOversea`), and both the grid (`sp_ExportLicenceDetailReportV2`) and the
export (`sp_ExportLicenceDetailReport_Fast`) apply the same `(@X = 0 OR licence.X = @X)`
predicate. Tradenet 2.0 scoped its dropdowns identically.

The remaining hypothesis is data: licences in the affected range carry Method / Incoterm ids
that the scoped dropdown never offers, so every concrete choice matches zero rows while
"--- All ---" (0) works.

```sql
-- (a) Ids the dropdowns offer today
SELECT Id, Name, IsActive, IsDeleted, [Type], IsOversea
FROM dbo.ExportImportMethod   WHERE [Type] = 'Export' AND IsOversea = 1;

SELECT Id, Name, IsActive, IsDeleted, [Type], IsOversea
FROM dbo.ExportImportIncoterm WHERE [Type] = 'Export' AND IsOversea = 1;

-- (b) Ids the licences in the complained-about window actually reference
DECLARE @f datetime = '2026-08-31 00:00:00', @t datetime = '2026-09-01 23:59:59';

SELECT ExportImportMethodId, COUNT(*) AS Licences
FROM dbo.ExportLicence
WHERE ApplyType = N'New' AND Status = N'Approved' AND CreatedDate >= @f AND CreatedDate <= @t
GROUP BY ExportImportMethodId
ORDER BY Licences DESC;

SELECT ExportImportIncotermId, COUNT(*) AS Licences
FROM dbo.ExportLicence
WHERE ApplyType = N'New' AND Status = N'Approved' AND CreatedDate >= @f AND CreatedDate <= @t
GROUP BY ExportImportIncotermId
ORDER BY Licences DESC;
```

Any id in (b) that is missing from (a) confirms the hypothesis. The fix then depends on which
side is wrong — widen the lookup scope, or correct the affected lookup rows' `Type`/`IsOversea`
flags — so bring the results back before changing code.

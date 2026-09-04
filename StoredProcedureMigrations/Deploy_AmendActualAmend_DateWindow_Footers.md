# Deploy: Amend / Actual Amendment date-window + footer parity (2026-09-04)

Fixes the customer complaint that the new Actual Amendment reports list **one day more** than the
old Tradenet 2.0 reports (Border Export Licence Actual Amend, 1-Aug..1-Sep-2026, Sakhan MWD: new 11
rows vs old 10, footer 10), and gives every Actual Amendment report the legacy per-currency footer.

**Order: run these procedures FIRST, then deploy the application.** Every change is backward
compatible with the currently deployed backend (new parameters are trailing and defaulted), so the
old application keeps working against the new procedures. The reverse is not true for the new Border
footers, which simply stay empty until the procedures land.

Files to apply, in this order:

1. `StoredProcedureMigrations/sp_ActualAmendReport_pagination.sql`
2. `StoredProcedureMigrations/sp_AmendReport_pagination.sql`
3. `StoredProcedureMigrations/sp_ExportPermitListingCurrencyTotals.sql`
4. `StoredProcedureMigrations/sp_ImportLicenceListingCurrencyTotals.sql`
5. `StoredProcedureMigrations/sp_ImportPermitListingCurrencyTotals.sql`

```powershell
$sqlServer = "<SERVER_NAME>,<PORT>"
$database  = "TradeNetDB"
$sqlFiles = @(
  "StoredProcedureMigrations\sp_ActualAmendReport_pagination.sql",
  "StoredProcedureMigrations\sp_AmendReport_pagination.sql",
  "StoredProcedureMigrations\sp_ExportPermitListingCurrencyTotals.sql",
  "StoredProcedureMigrations\sp_ImportLicenceListingCurrencyTotals.sql",
  "StoredProcedureMigrations\sp_ImportPermitListingCurrencyTotals.sql"
)
foreach ($f in $sqlFiles) { Write-Host "Applying: $f"; sqlcmd -S $sqlServer -d $database -i $f -b }
```

`sqlcmd` sets `QUOTED_IDENTIFIER ON` by default. If you apply these through any other client, run
`SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;` on the same connection first.

## What changed

* Both listing grids now filter `CreatedDate <= @ToDate`, exactly like the original
  `dbo.sp_ActualAmendReport` / `dbo.sp_AmendReport`. Callers pass `@ToDate` as `<day> 23:59:59`, so
  the previous `< DATEADD(day, 1, @ToDate)` admitted the whole following day.
* The Import Licence / Import Permit footer procedures gained trailing `@FormType` and `@SakhanId`
  parameters and Border branches, so Border Import Licence / Border Import Permit reports get the
  per-currency footer. Existing short-argument callers are unaffected.
* All three footer procedures normalise the caller's `'ActualAmend'` to the stored `'Actual Amend'`
  (`@DbApplyType`). Without this the Export Permit and Border Export Permit Actual Amendment footers
  matched no rows at all.

## Before/after check (read-only)

Deployed-version diagnostic — run this first and keep the output:

```sql
USE [TradeNetDB];
SELECT p.name, p.modify_date, m.uses_quoted_identifier,
  (SELECT COUNT(*) FROM sys.parameters pa WHERE pa.object_id = p.object_id) AS params,
  CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%DATEADD(day, 1, @ToDate)%' THEN 'DATEADD-raw' ELSE 'ok' END AS date_pred,
  CASE WHEN OBJECT_DEFINITION(p.object_id) LIKE '%Border Import Licence%' THEN 'has-BorderIL' ELSE 'NO-BorderIL (stale)' END AS border_il
FROM sys.procedures p JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name IN ('sp_ActualAmendReport_pagination','sp_AmendReport_pagination',
                 'sp_ExportLicenceListingCurrencyTotals','sp_ExportPermitListingCurrencyTotals',
                 'sp_ImportLicenceListingCurrencyTotals','sp_ImportPermitListingCurrencyTotals');
```

Expected parameter counts after deployment: 12 / 12 / 8 / 8 / 10 / 8 (in the order listed above).

UAT parity (2025 data), the three numbers must agree:

```sql
DECLARE @f datetime = '2025-11-01 00:00:00', @t datetime = '2025-11-30 23:59:59';
EXEC dbo.sp_ActualAmendReport            N'Border Export Licence', @f, @t, 0, 0, N'', 0;                                  -- old: 20 rows
EXEC dbo.sp_ActualAmendReport_pagination N'Border Export Licence', @f, @t, 0, 0, N'', 0, NULL, NULL, 0, 0, 1;             -- new: 20 rows, TotalCount 20
EXEC dbo.sp_ExportLicenceListingCurrencyTotals N'Border Export Licence', N'Actual Amend', @f, @t, 0, N'', 0, 0;           -- THB 6, USD 6, CNY 8 = 20
-- Border Import Licence over the same window: 53 / 53, footer via the new 10-argument shape:
EXEC dbo.sp_ImportLicenceListingCurrencyTotals N'ActualAmend', @f, @t, 0, N'', 0, N'', N'', N'Border Import Licence', 0;
```

PROD, the customer's own window (the ORIGINAL procedure is the oracle — the new one must equal it):

```sql
DECLARE @f datetime = '2026-08-01 00:00:00', @t datetime = '2026-09-01 23:59:59';
EXEC dbo.sp_ActualAmendReport            N'Border Export Licence', @f, @t, 0, 0, N'', 5;                       -- expect 10 rows
EXEC dbo.sp_ActualAmendReport_pagination N'Border Export Licence', @f, @t, 0, 0, N'', 5, NULL, NULL, 0, 0, 1;  -- expect 10 rows
EXEC dbo.sp_ExportLicenceListingCurrencyTotals N'Border Export Licence', N'Actual Amend', @f, @t, 0, N'', 0, 5; -- expect THB 7 + USD 3
EXEC dbo.sp_ActualAmendReport            N'Border Import Licence', @f, @t, 0, 0, N'', 0;                       -- expect 17 rows
EXEC dbo.sp_ActualAmendReport_pagination N'Border Import Licence', @f, @t, 0, 0, N'', 0, NULL, NULL, 0, 0, 1;  -- must equal the line above
```

If Border Import Licence returns 6 rows from the pagination procedure while the original returns 17,
the deployed procedure predates the branch that reads `BorderImportLicence` (commit 347f7fd) and is
falling through to the non-border `ImportLicence` table — applying file 1 above fixes it.

`<repo>/private/tmp/.../scratchpad/validate_amend_parity.py` automates the whole comparison
(`--env uat --mode after`, `--env prod --customer`).

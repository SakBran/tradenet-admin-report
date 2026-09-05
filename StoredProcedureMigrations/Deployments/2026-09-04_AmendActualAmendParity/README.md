# 2026-09-04 — Amend / Actual Amendment parity

**Six stored procedures to re-run on the server.** Everything needed is in this folder; the
files are copies of the repository originals listed in `checksums.txt`.

> **Re-synced 2026-09-05.** `sp_ImportPermitListingCurrencyTotals` has since gained a
> `Cancel` branch (Import Permit round-1 parity), so `06_…` and the matching section of
> `00_RunAll.sql` were re-copied from the repository original and `checksums.txt` updated —
> a deployment copy must never be stale. If this folder has already been applied, the
> `Cancel` branch ships again in
> [2026-09-05_ImportPermitParityRound1](../2026-09-05_ImportPermitParityRound1/); running
> either folder is enough, and running both is harmless (`CREATE OR ALTER`).

Fixes the customer complaint that the new Actual Amendment reports do not match the old
Tradenet 2.0 reports:

* Border Export Licence Actual Amend, 1-Aug..1-Sep-2026, Sakhan MWD — new grid 11 rows,
  its own footer 10, old report 10. The grid was counting one extra calendar day.
* Border Import Licence Actual Amend — new 6 rows, old 17. Expected to be a **stale
  procedure on the server**: a definition that predates the `Border Import Licence` branch
  falls through to the non-border `ImportLicence` table. Section 1 of
  `VerifyDeployment.sql` confirms this before you deploy.

## Run order

**Procedures first, application second.** Every change is backward compatible with the
currently deployed backend — the new parameters are trailing and defaulted — so the old
application keeps working against the new procedures. The reverse is not true: the new
Border footers stay empty until these land.

| # | Procedure | Why |
|---|---|---|
| 1 | `sp_ActualAmendReport_pagination` | date window, all 8 FormType branches (the 11-vs-10 bug) |
| 2 | `sp_AmendReport_pagination` | same date window fix, Amend siblings |
| 3 | `sp_ExportLicenceListingCurrencyTotals` | footer for the complaint's own report |
| 4 | `sp_ExportPermitListingCurrencyTotals` | `@DbApplyType`; its Actual Amend footer returned 0 rows |
| 5 | `sp_ImportLicenceListingCurrencyTotals` | new Border Import Licence footer branch, `@FormType`/`@SakhanId` |
| 6 | `sp_ImportPermitListingCurrencyTotals` | new Border Import Permit + Actual Amend footer branches |

Either open **`00_RunAll.sql`** and execute it once, or run `01`…`06` in order.

**Target database: `TradeNetDB`.** Not `ReportTemplateDB` — that one only holds the Excel
export job queue. `00_RunAll.sql` carries a `USE` plus a guard that aborts if the database
does not contain the legacy `dbo.sp_ActualAmendReport`.

```powershell
# Build Server, from the repository root
$sqlServer = "<SERVER>,<PORT>"          # UAT 203.81.66.111,14330 | PROD tn2db.myanmartradenet.com,14133
sqlcmd -S $sqlServer -d TradeNetDB -b -i "StoredProcedureMigrations\Deployments\2026-09-04_AmendActualAmendParity\00_RunAll.sql"
```

`sqlcmd` and SSMS both set `QUOTED_IDENTIFIER ON`. Any other client must run
`SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;` on the same connection first, or the
procedures are created with the wrong option and fail at run time (Msg 1934).

## Sequence

1. `CaptureRollback.sql` — save the current definitions. This is the rollback artifact.
2. `VerifyDeployment.sql` **section 1** — record what is deployed today. On production this
   is also the diagnostic for the Border Import Licence 6-vs-17 complaint.
3. `00_RunAll.sql`.
4. `VerifyDeployment.sql` sections 1–3 — parameter counts must read 12, 12, 8, 8, 10, 8;
   `date_pred` `ok`; `border_il` `has-BorderIL`. The old procedure is the oracle in every
   comparison: the new one must return exactly the same rows.
5. Only then deploy the application (merging to `main` auto-deploys it).

Rolling back the procedures restores the bug, so prefer a forward fix. If you must:
re-run the definitions saved in step 1 with `CREATE` changed to `CREATE OR ALTER`.

## What changed

* Both grids filter `CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))` in all eight
  FormType branches — exactly the selected calendar day. The previous
  `< DATEADD(day, 1, @ToDate)` admitted the whole **following** day because callers pass
  `@ToDate` with a time of 23:59:59. That is the extra row.
* Every footer branch an Amend / Actual Amendment report reaches uses the same
  calendar-date form. The New and Cancel branches deliberately keep `<= @ToDate`: their
  controllers still send 23:59:59 and those branches mirror their own untouched grids.
* The Import Licence and Import Permit footer procedures gained trailing `@FormType` and
  `@SakhanId` parameters and Border branches, so the Border reports get a per-currency
  footer. Existing short-argument callers are unaffected.
* All three Listing footer procedures normalise the caller's `'ActualAmend'` to the stored
  `'Actual Amend'` (`@DbApplyType`). Without it the Export Permit and Border Export Permit
  Actual Amendment footers matched no rows at all.

The application side of the same fix (16 controllers now send `request.ToDate.Date`, plus
the footer wiring and column changes) ships in the matching commit — see
`../../Deploy_AmendActualAmend_DateWindow_Footers.md`.

# Export Licence New Report — currency footer (2026-09-05)

Customer complaint on **Export Licence New Report (New Report)**: no Total in the UI or in the
Excel export, and the pager would not go to the last page. The pager half is application-only;
this folder is the database half of the Total fix.

## What ships

One procedure: `dbo.sp_ExportLicenceListingCurrencyTotals`.

| Change | Why |
|---|---|
| New trailing parameter `@auto nvarchar(50) = N''` | Both New grids expose the Auto / None-Auto dropdown. Without it an Auto-filtered grid got an unfiltered footer. |
| Non-border `New` branch: `AND (@auto = N'' OR ExportLicence.auto = @auto)` | Copied from `sp_NewReport_pagination`'s Export Licence branch. |
| Border `New` branch (both card-type halves): `AND BorderExportLicence.auto = (CASE WHEN @auto = N'' THEN BorderExportLicence.auto ELSE @auto END)` | Copied from that branch's own grid. The two forms differ on NULL `auto` — the CASE form drops those rows — so each branch mirrors its own grid rather than sharing one form. |
| Non-border `New` branch date window `<= @ToDate` → `< DATEADD(day, 1, CONVERT(date, @ToDate))` | Its grid uses the calendar-date form. With `@ToDate` arriving as `23:59:59` the two disagreed on licences created in the final second of the last day. |

Amendment / Actual Amendment / Cancellation branches are untouched.

## Order

**Procedure first, application second.** `@auto` is appended last and defaults to `N''`, so the
currently deployed 8-argument callers keep working against this version unchanged. The reverse is
not true: the new backend passes 9 arguments for the New report, and the old 8-parameter procedure
answers that with Error 8144, which `ExportLicenceListingCurrencyTotals.RunAsync` swallows into an
empty footer — the report would simply keep showing no Total.

## Run

```
sqlcmd -S <server> -d TradeNetDB -i 00_RunAll.sql
```

or open `00_RunAll.sql` in SSMS **against TradeNetDB** (not ReportTemplateDB — that database only
holds the Excel export job queue). `SET QUOTED_IDENTIFIER ON` is in the script and matters: a
connection that defaults it OFF creates a procedure that fails at runtime.

Then run `VerifyDeployment.sql`. Section 3 is the gate: the footer's grand total must equal the
grid's `TotalCount` for the same filters, unfiltered and for each Auto value. Use **2025** windows
— the data lives in 2025.

## Note on the earlier snapshot

`Deployments/2026-09-04_AmendActualAmendParity/03_sp_ExportLicenceListingCurrencyTotals.sql` was
re-copied from the repository original in the same change, because `DeploymentFolderContractTests`
requires every deployment copy to equal the file developers edit. That folder now ships this same
definition — which is safe, since the change is backward compatible with its callers.

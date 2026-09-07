# Import Licence feedback deployment bundle (items 1-6)

This folder groups the database scripts associated with the six Import Licence
feedback items reported on 3 September 2026. The files are copies of the
canonical scripts in `StoredProcedureMigrations`; keep both copies synchronized
when a procedure is changed.

## Feedback-to-script mapping

| No. | Report / feedback | Database script(s) |
| --- | --- | --- |
| 1 | Import Licence - Daily Report: old report has 15 rows and the new page initially showed 10 | No stored-procedure change. The fix is application-side pagination: retain the global page size of 10 and expose all matching rows across pages. |
| 2 | Import Licence - Actual Amend Report: old report has 17 rows and the new report has 6 | `sp_ActualAmendReport_pagination.sql` and the shared footer-total procedure `sp_ImportLicenceListingCurrencyTotals.sql` |
| 3 | Import Licence - Amendment Report: a 1-5 date range also included sixth-day data | `sp_AmendReport_pagination.sql` and the shared footer-total procedure `sp_ImportLicenceListingCurrencyTotals.sql` |
| 4 | Border Import Licence - New Report: data differs from the old report | `sp_NewReport_pagination.sql`. Its footer total is calculated by application code, so there is no additional total stored procedure. |
| 5 | Border Import Licence - By Voucher Report: incorrect output for 1-5 January 2026 | `sp_VoucherReport_pagination.sql` |
| 6 | Border Import Licence - Pending Report: incorrect output | `sp_PendingReport_pagination.sql` |

The exact historical 17-versus-6 result for item 2 cannot be reproduced without
the customer's original complete filter values and matching database snapshot.
The bundled procedure contains the corrected date-boundary behavior and must be
validated with the customer's filters before that exact comparison is closed.

## Target database and run order

Run these scripts against **TradeNetDB**, not the report template database.
Apply and validate them in UAT before any separately authorized production
deployment.

Recommended order:

1. `sp_ActualAmendReport_pagination.sql`
2. `sp_AmendReport_pagination.sql`
3. `sp_ImportLicenceListingCurrencyTotals.sql`
4. `sp_NewReport_pagination.sql`
5. `sp_VoucherReport_pagination.sql`
6. `sp_PendingReport_pagination.sql`

There are no cross-script creation dependencies. The order groups the scripts
by feedback number, with the shared totals procedure immediately after items 2
and 3.

## Deployment options

Either open and execute each file in the order above while connected to
`TradeNetDB`, or run `00_Run_All.sql` in SQLCMD mode from this directory.

Command-line example (use environment-specific values; do not save credentials
in this repository):

```powershell
sqlcmd -S '<server>' -d 'TradeNetDB' -U '<user>' -P '<password>' -C -b -i '.\00_Run_All.sql'
```

Before deployment, save the current definitions of all six procedures so the
change can be rolled back. After deployment, rerun the report parity tests for
items 1-6. Item 1 still requires application deployment even though it has no
database script.

## UAT status as of 5 September 2026

- Items 5 and 6 procedures were applied and controller-level UAT tests passed.
- The corrected item 2-4 procedure definitions were already present on UAT and
  their controller-level tests passed for the documented test cases.
- Item 1 passed the compiled controller pagination test (15 total rows exposed
  as pages of 10 and 5).
- Production was not modified during this UAT verification.

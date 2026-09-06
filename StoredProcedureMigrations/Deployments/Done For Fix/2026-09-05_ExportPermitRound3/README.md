# Export Permit round-3 parity — 2026-09-05

> **SUPERSEDED for the Cancellation half.** This folder originally ordered the `TOP 1`
> sub-selects by `ExportPermitItem.Id`, which is a `char(36)` GUID string and the wrong key —
> it produced the customer-reported `5,769.2300` / `USD:10,038.1050`. The copies here now carry
> the corrected `ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo`; run
> `../2026-09-06_ExportPermitItemOrder/` instead, which covers this and the sibling procedures.

Customer complaints on the non-Border Export Permit reports 5.1 / 5.5–5.8 / 5.10–5.11.
Most of the fix is application code and rides the normal deploy; **these two procedures do
not** — stored procedures here are applied by hand.

## What is in this folder

| File | Procedure | Change |
|---|---|---|
| `01_sp_CancelReport_pagination.sql` | `sp_CancelReport_pagination` | Export Permit branch: an explicit `ORDER BY` on the three `TOP 1` sub-selects (Currency / HSCode / Amount) |
| `02_sp_VoucherReport_pagination.sql` | `sp_VoucherReport_pagination` | Export Permit branch: `ExchangeRate` / `TotalCIF` emit `0` instead of `NULL` |

`00_RunAll.sql` applies both in order. `CaptureRollback.sql` first, `VerifyDeployment.sql`
after.

## Why

**Voucher (5.1).** Production's `VoucherReport.rdlc` has 17 columns; the new report had 11.
The four missing ones — `Application Date`, `Commodity Type`, `Total CIF`, `Exchange Rate` —
were added to `reportConfigs.ts`. The `ExportPermit` table has no `TotalCIF` or
`ExchangeRate` column at all: the old app's `Business/Reports.cs` Export-Permit branch never
assigns those two non-nullable `decimal`s, so the old report prints a literal `0`. The
procedure returned `NULL`, which the grid renders as `N/A`. Emitting `0` reproduces the old
report exactly, in the grid **and** in the `.xlsx`.

**Cancellation (5.6).** The customer reports the `Total Value` column does not match the old
report. The row value is `SELECT TOP 1 ISNULL(ExportPermitItem.Amount, 0)` with **no
`ORDER BY`** — as are the sibling `Currency` and `HSCode` sub-selects. Three independent
unordered `TOP 1`s over a multi-item permit can each resolve to a different item, and can
resolve differently under the paged plan than under the old unpaged one.
`sp_ExportPermitListingCurrencyTotals` already orders by `Id`, so the grid could also
disagree with its own footer. Ordering all three by `ExportPermitItem.Id` pins the value
that an unordered `TOP 1` returns in practice and makes the three columns consistent.

## ⚠️ Open item — please capture this while you are connected

This procedure change makes the report *deterministic*; it does **not yet prove** the value
equals production's. `dbo.sp_CancelReport` — the legacy procedure the old admin app calls —
has moved on from the snapshot this repository was built from: production's
`CancelReport.rdlc` and `Business/Reports.cs:2011` both read an `HSCode` column that our
snapshot's Export Permit branch never returns. The production DB (`100.64.91.190` /
`tn2db.myanmartradenet.com,14133`) is CGNAT-internal and unreachable from a developer Mac.

`CaptureRollback.sql` ends with the query that captures it:

```sql
SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_CancelReport'));
```

Save that text and hand it back — its `@FormType='Export Permit'` branch is the oracle for
the Total Value expression.

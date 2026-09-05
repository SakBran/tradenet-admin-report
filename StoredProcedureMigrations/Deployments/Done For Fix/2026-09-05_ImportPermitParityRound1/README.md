# Import Permit round-1 parity — 2026-09-05

Customer complaints on the non-Border Import Permit reports: Voucher, Company List,
Cancellation, By Seller Country, By Section, By HS Code. Most of the fix is application
code and rides the normal deploy; **these two procedures do not** — stored procedures here
are applied by hand.

## What is in this folder

| File | Procedure | Change |
|---|---|---|
| `01_sp_HSCodeReport_pagination.sql` | `sp_HSCodeReport_pagination` | `@FormType='Import Permit'` branch only: group on `(HSCodeId, HSCode, HSDescription, Currency)` instead of additionally on `CompanyRegistrationNo, CompanyName`; `ORDER BY` loses `CompanyName` |
| `02_sp_ImportPermitListingCurrencyTotals.sql` | `sp_ImportPermitListingCurrencyTotals` | new `ApplyType='Cancel'` branch |

`00_RunAll.sql` applies both in order. `CaptureRollback.sql` first, `VerifyDeployment.sql`
after.

## Why

**By HS Code — "one HS code appears twice, and the Total Values differ".** The legacy
`HSCodeReport.rdlc` does all its grouping in the report layer, and its row group is exactly
two expressions: `=Fields!HSCodeId.Value` and `=Fields!Currency.Value` (rdlc:1152-1153).
The new procedure grouped on the buyer company as well. Because the grid renders only
`HS Code / Description / No of Licences / Total Value / Currency`, that extra key was
invisible: one HS code came back once per buyer, each row carrying only that buyer's
Total Value. Exactly the reported symptom.

The other seven `@FormType` branches are deliberately **unchanged**. Their
`*HSCodeDetailReport` configs (`BorderImportPermitHSCodeDetailReport`,
`ExportLicenceHSCodeDetailReport`, …) render Company Name off this same procedure —
`HSCodeDetailReport.rdlc:1263-1264` groups on HSCodeId + CompanyRegistrationNo. Import
Permit has no detail report of its own, which is why it can drop the company outright.
`sp_HSCodeReport.AggregateQuery` (the LINQ twin used for the Excel export and whenever a
section filter takes the report off this procedure) carries the same condition, so grid and
`.xlsx` agree.

**Cancellation — "the Total is missing".** `ImportPermitCancellationReport` was the only
cancellation report in the codebase with no `currencyTotalsColumns`, and its controller set
no `CurrencyTotals`. `CancelReport.rdlc` has a full footer: a second tablix (`:1497-1857`)
grouped on Currency (`:1837`) printing `"<CUR>:N licence(s)"` (`:1557`) and
`"<CUR>:FORMAT(Sum(Amount),'N4')"` (`:1611`), then a grand `"Total:N licence(s)"` (`:1723`).
The procedure's catch-all `ELSE` is hard-wired to `ApplyType='New'`, so routing a Cancel
request through the existing wrapper would have answered with New-permit numbers rather
than failing — hence a dedicated branch.

The branch mirrors the grid rather than the sibling branches: `sp_CancelReport`'s Import
Permit query takes the **first** item's `Amount` (`MIN(ImportPermitItem.Id)`), so this uses
`TOP 1 … ORDER BY ImportPermitItem.Id`, not the Amend branch's `SUM`. The count is
`COUNT(DISTINCT ImportPermitNo)` because the rdlc aggregate is `CountDistinct`, not `Count`.

## ⚠️ Check this before trusting any old-vs-new comparison

`docs/sp_HSCodeReport_AggregatePagination.sql:10` is an
`ALTER PROCEDURE [dbo].[sp_HSCodeReport]` — the **legacy** procedure the old admin app
calls — and it applies the same company `GROUP BY` this deployment removes. If that script
was ever run against this database, the old system's HS Code report is already showing the
split rows too and the "old shows one row" baseline is contaminated.
`CaptureRollback.sql` ends with the query that settles it: if the captured definition
contains a `GROUP BY`, it has been overwritten and must be restored first.

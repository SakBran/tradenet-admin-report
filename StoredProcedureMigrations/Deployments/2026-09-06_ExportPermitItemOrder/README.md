# Export Permit item-order parity — 2026-09-06

Customer complaint on **Export Permit Cancellation**, `01/09/2025 → 30/09/2025`:

| | old (Tradenet 2.0) | new |
|---|---|---|
| `OVSEP12526C000012` Total Value | 27,230.7600 | 5,769.23 |
| Footer | USD:33,835.1200 | USD:10,038.1050 |

**This folder supersedes the Export Permit half of `2026-09-05_ExportPermitRound3`**, whose
`ORDER BY ExportPermitItem.Id` was the wrong key. If that folder has not been run yet, run
this one instead; if it has, run this one on top.

## What changes

Every correlated `TOP 1` over `ExportPermitItem` in the `@FormType = N'Export Permit'`
branches gains

```sql
ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo
```

| File | Sub-selects touched |
|---|---|
| `01_sp_CancelReport_pagination.sql` | Currency, HSCode, Amount |
| `02_sp_AmendReport_pagination.sql` | Currency, HSCode, Amount |
| `03_sp_ActualAmendReport_pagination.sql` | Currency, HSCode, Amount |
| `04_sp_NewReport_pagination.sql` | Currency, HSCode (Amount is `SUM` — correct already) |
| `05_sp_ExtensionReport_pagination.sql` | Currency (Amount is `SUM` — correct already) |
| `06_sp_ExportPermitListingCurrencyTotals.sql` | Currency + Amount in the Amend/ActualAmend and Cancel branches; Currency in the New/Extension branch |
| `07_sp_ExtensionReportCurrencyTotals.sql` | Currency |

Border Export Permit branches are deliberately untouched — out of scope this pass, and the
same latent defect exists in all eight document families.

## Why this key

`ExportPermitItem.Id` is a **`char(36)` GUID string**; item order lives in `ItemNo int`. The
clustered PK is `(Id, UniqueId)` and the only other index is
`IX_ExportPermitItem_ReportCover (ExportPermitId, HSCodeId, ItemNo)`.

The legacy procedures use a bare `TOP 1` with **no `ORDER BY`**, so they return whatever the
plan's index order yields — the `ReportCover` seek order. Measured against them over 2025:

| `ORDER BY` | Cancel rows matching legacy | Sep-2025 USD footer |
|---|---|---|
| `ExportPermitItem.Id` | 14/17 | 10,038.1050 ✗ |
| `ExportPermitItem.ItemNo` | 16/17 | 34,136.7600 ✗ |
| **`HSCodeId, ItemNo`** | **17/17** | **33,835.1200** ✓ |

`ItemNo` alone is wrong by 301.64 across four rows — this had to be measured, not reasoned
about. Making the order **explicit** also removes the dependence on which index the plan
picks, so the answer is now the same on every server.

Not every report takes one item: `sp_NewReport` and `sp_ExtensionReport` **sum** all of a
permit's items (proved by `OVSEP12526E000010`, whose items 507.30 + 4,081.20 give the legacy
4,588.50). Their `_pagination` copies already summed and were left alone; only their
`Currency` / `HSCode` picks needed the key. Do not assume one rule across reports.

## Order of operations

1. `CaptureRollback.sql` — save the grid; it is the rollback artifact.
2. `00_RunAll.sql` — or `01`…`07` individually.
3. `VerifyDeployment.sql` — section 1 must report `HSCodeId,ItemNo` for all seven; section 2
   must show `OVSEP12526C000012 = 27230.7600` and the footer `USD / 4 / 33835.1200`; section 3
   diffs every listing report against its legacy counterpart.
4. Deploy the application.

# Export Licence Cancellation parity — 2026-09-06

Customer complaint on the non-Border **Export Licence Cancellation Report**:

> UI and Excel တွင် Total ပေါင်းတွေမပါနေပါ။
> HSCode column တိုင်တစ်ခုထည့်ပေးရန်။
> Pagination နောက်ဆုံးထိသွားမရပါ။

Most of that fix is application-side (the controller now returns `CurrencyTotals`, the
grid gained the HS Code column, and the pager gets a real row count). This folder holds
the one database half: making the item pick **deterministic**, so the new footer is the
sum of the rows the user can see.

## What changes

| File | Sub-selects touched |
|---|---|
| `01_sp_CancelReport_pagination.sql` | `@FormType = N'Export Licence'` branch — Currency, HSCode, Amount |
| `02_sp_ExportLicenceListingCurrencyTotals.sql` | non-Border `@ApplyType = N'Cancel'` branch — Currency, Amount |

Each gains

```sql
ORDER BY ExportLicenceItem.Id, ExportLicenceItem.UniqueId
```

`ExportLicenceItem`'s clustered primary key is `(Id, UniqueId)`. The legacy
`dbo.sp_CancelReport` uses a bare `TOP 1` with no `ORDER BY`, so it returns whatever the
plan's index order yields — which means the three sub-selects could each land on a
*different* item of the same licence, and the answer could differ between servers.
`(Id, UniqueId)` is the key measured to reproduce the legacy procedure (Cancel 466/466
over Aug-2025).

**Do not copy the Export Permit key here.** `ExportPermitItem` reproduces its legacy
procedure on `(HSCodeId, ItemNo)` and scores only 14/17 on `Id`
(`2026-09-06_ExportPermitItemOrder`). The effective key is the key order of each table's
narrowest covering index, so it has to be measured per item table, never reasoned across.

The grid and the footer must carry the **identical** expression. If they drift apart, the
footer stops being the sum of the visible `Total Value` column — which is exactly the
defect the Export Permit pass had to be re-run to fix.

## Deliberately untouched

- Every **Border Export Licence** branch of both procedures.
- The Cancel branch's `CreatedDate <= @ToDate` window. The grid's count and base predicate
  use the same form, so grid and footer already agree; widening it here would change which
  rows the report returns, which is not what was complained about.
- The New / Amend / Actual Amend branches of `sp_ExportLicenceListingCurrencyTotals`.

## Run order

1. `CaptureRollback.sql` — save the result grid; it is the rollback artifact. It also
   captures the legacy `dbo.sp_CancelReport` text, which section 3 of the verification
   needs if production's copy has drifted from this repository's 2022 snapshot.
2. `VerifyDeployment.sql` **section 1** — record what is deployed today.
3. `00_RunAll.sql` (or `01`, then `02`).
4. `VerifyDeployment.sql` sections 1, 2 and 3.
5. Deploy the application.

Section 1 must read `item_key = 'Id,UniqueId'` for both procedures afterwards. Section 2's
grid `TotalCount` must equal the footer's summed licence count. **Section 3 is the sign-off:
record the legacy-parity score below before calling this released.**

| Check | Result |
|---|---|
| Legacy parity, Cancel, 2025 | _not yet run — the report database is not reachable from a developer machine_ |

## Provenance

`checksums.txt` records the SHA-256 of the repository originals these copies were taken
from. `Backend.Tests/DeploymentFolderContractTests.cs` fails the build if a copy drifts
from its original, so editing either canonical procedure means re-copying it into every
dated folder that snapshots it.

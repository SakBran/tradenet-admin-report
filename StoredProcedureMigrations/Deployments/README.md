# Deployments

One folder per hand-applied database release, named `YYYY-MM-DD_ShortName`. Stored
procedures in this repository are **not** applied by a migration runner — someone runs them
in SSMS or `sqlcmd` on the Build Server — so a release that is not written down here is a
release nobody can reproduce, verify, or roll back.

Each folder holds the procedures for that release, numbered in run order, plus:

| File | Purpose |
|---|---|
| `README.md` | why the release exists, run order, sequence, what changed |
| `00_RunAll.sql` | all procedures in one script, with a wrong-database guard |
| `NN_<proc>.sql` | the individual procedures, in run order |
| `CaptureRollback.sql` | dumps the definitions currently on the server — run it first |
| `VerifyDeployment.sql` | deployed-version diagnostic and old-vs-new parity checks |
| `checksums.txt` | SHA-256 of the repository originals the copies were taken from |

The files in a dated folder are a **snapshot**. The canonical, edited files stay in
`StoredProcedureMigrations/`; `checksums.txt` records which revision of them a folder
captured. `Backend.Tests/DeploymentFolderContractTests.cs` fails the build if a copy drifts
from its original, so a snapshot can never quietly go stale.

| Release | Contents |
|---|---|
| [2026-09-04_AmendActualAmendParity](2026-09-04_AmendActualAmendParity/) | 6 procedures — Amend / Actual Amendment date window and per-currency footers |
| [2026-09-05_ExportPermitRound3](2026-09-05_ExportPermitRound3/) | 2 procedures — Export Permit Cancellation `TOP 1` ordering and Voucher `TotalCIF`/`ExchangeRate` zeros |
| [2026-09-05_ImportPermitParityRound1](2026-09-05_ImportPermitParityRound1/) | 2 procedures — Import Permit By HS Code grouping and the Cancellation per-currency footer |
| [2026-09-06_ExportLicenceCancelParity](2026-09-06_ExportLicenceCancelParity/) | 2 procedures — Export Licence Cancellation item order `(Id, UniqueId)`, so grid and footer pick the same item |
| [2026-09-05_ExportLicenceDetailLegacyParity](2026-09-05_ExportLicenceDetailLegacyParity/) | 1 NEW procedure — `sp_ExportLicenceDetailReportV3_pagination`, the legacy Export Licence Detail query paginated at item grain (the grid's rows now equal the old report's) |
| [2026-09-06_ExportPermitItemOrder](2026-09-06_ExportPermitItemOrder/) | 7 procedures — Export Permit item order `(HSCodeId, ItemNo)`; supersedes the Export Permit half of `2026-09-05_ExportPermitRound3` |
| [2026-09-05_BorderImportPermitComplaints](2026-09-05_BorderImportPermitComplaints/) | 4 procedures — Border Import Permit HS Code paging and grouping, the New Report TOTAL footer, and the New-branch date window; re-syncs the two procedures also shipped by `2026-09-05_ImportPermitParityRound1` |

# Branch Work Log

This journal records which assignment was implemented on each branch and its actual
merge/deployment state. Add the newest assignment first.

## `feature/all-new-reports-licence-date`

- Assignment date: 2026-10-02
- Base commit: `0a6d8e6`
- Feature commit: `15b1632` (`feat: add licence date to all new reports`)
- Status: Completed and committed locally on the feature branch. The database
  procedure was deployed to `tn2db\\PRODUCTION / TradeNetDB` on 2026-10-02 using
  Windows Authentication. The site was not deployed. The branch is not merged
  into `main` and not pushed.

### Scope completed

- Defined **all application** as Import/Export/Border Import/Border Export, each
  covering both Licence and Permit (eight report families total).
- Added `Licence Date` immediately after `Licence No` in all eight New Report
  (New Report) tables and Excel exports.
- Mapped the visible value to each application table's `IssuedDate` field.
- Removed `Online No`, `Online Date`, and `Remark` from the visible Import Licence
  New Report table and Excel export, leaving only `Licence Date` from that field set.
- Kept the shared backend result fields for compatibility with the already deployed
  result shape.
- Added a database-first deployment package at
  `StoredProcedureMigrations/Deployments/Done For Fix/2026-10-02_AllNewReportsLicenceDate`.

### Verification completed

- Focused frontend and Excel fixture tests: 25 passed.
- Frontend production build passed (existing large-chunk warning only).
- SQL parse-only validation passed against `TradeNetDB` using Windows
  Authentication; no database object or data was changed.
- Post-deployment execution checks passed for all eight form types over the
  2018-2026 range: every result shape was complete and every sample row had a
  non-null `Licence Date`.
- Read-only schema validation confirmed `IssuedDate` exists on all eight target
  application tables.
- Static backend projection, deployment-bundle parity, and checksum checks passed.
- Installed the complete x64 .NET SDK `8.0.425`, pinned the repository to .NET 8
  with `global.json`, and successfully rebuilt the backend with that SDK.
- Focused backend tests passed: 18 passed, 0 failed.
- Frontend lint could not run because the repository uses ESLint 9 without an
  `eslint.config.js`/`.mjs`/`.cjs` configuration file.

### Deployment note

Use Windows Authentication for database work. `dbo.sp_NewReport_pagination` has
been deployed and verified. The pre-deployment definition is preserved as
`RollbackCaptured.sql` in the deployment package. The matching site deployment
still remains to be performed after merge.

## `feature/import-pending-status-filters`

- Assignment date: 2026-09-30
- Recorded on main: 2026-10-02
- Base commit: `a813696`
- Feature commit: `aa8069a` (`feat: add filters to import pending reports`)
- Status: Completed and committed locally on the feature branch. Not merged into
  `main`, not pushed, and not deployed.

### Scope completed

- Updated both `ImportLicencePendingReport` and
  `BorderImportLicencePendingReport`.
- Added Company Registration No, readonly auto-populated Company Name, and Status
  filters while keeping the existing date and Import Section filters.
- Added these Status choices: All Application, Auto Cancel, Auto Cancel Feedback,
  Pending, Reject, and Payment Ready.
- Defined All Application as the five requested workflow states only; Approved and
  Archive are excluded.
- Applied the same filters to grid and queued Excel data paths.
- Updated the shared LINQ query and `dbo.sp_PendingReport_pagination` for company and
  status filtering.
- Appended the new optional stored-procedure parameters and changed the new backend
  call to named parameters, allowing a database-first rollout without breaking the
  old deployed backend.
- Added the deployment package at
  `StoredProcedureMigrations/Deployments/Done For Fix/2026-09-30_ImportPendingStatusFilters`.

### Verification completed

- Focused frontend tests: 3 passed.
- Focused backend tests: 13 passed.
- Frontend production build passed.
- SQL Server parse-only validation passed against `TradeNetDB` using Windows
  Authentication; no database object or data was changed.
- The repository-wide frontend/backend suites still contain unrelated pre-existing
  failures, including old report-config expectations and test databases missing
  unrelated stored procedures.

### Deployment note

Use Windows Authentication for database work. Deploy the stored procedure first,
verify it, and then deploy the site. The feature branch has not performed either
deployment step.

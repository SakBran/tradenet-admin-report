# Branch Work Log

This journal records which assignment was implemented on each branch and its actual
merge/deployment state. Add the newest assignment first.

## `feature/role-scoped-report-menus`

- Assignment date: 2026-10-05
- Base branch: `feature/all-pending-reports-licence-date`
- Base commit: `9de75d7`
- Feature commit: `67401fe` (`feat: scope report menus and APIs by user assignment`)
- Status: Completed and committed locally. Not merged into `main`, not pushed,
  and not deployed. No database object or data was changed.

### Scope completed

- Resolve the current login user's active TradeNet account and `UserDetail`
  `Type`/`SubType` assignments. Super Administrator sees all reports; Report,
  Check User, and Approve User see only assigned report families. Account User
  retains Payment reports; other roles receive no general report access.
- Apply the same family policy to the sidebar, direct report routes, and all
  report controller APIs (including Excel requests). Advance Search follows
  the assigned application families. Data Import and Activity Log are admin-only.
- Keep Home and Logout available to signed-in users. Non-admin Home no longer
  renders the unrestricted eight-family report summary.
- Show non-admin users only their own saved exports for currently permitted
  reports; prevent cross-user reuse of queued/completed export jobs. Admins can
  see every export.
- Authorization uses current database assignments on each request. It does
  not filter report rows by `UserDetail.Section`; that was outside this
  menu-family assignment and needs a separate data-scope decision if required.

### Verification completed

- Focused backend policy/export tests: 7 passed. An ownership-check mutation
  caused the expected test failure, then was restored and the suite passed.
- Frontend report-menu tests: 14 passed.
- Frontend production build passed with the pre-existing large-chunk warning.
- Backend build passed through the focused test run. `git diff --check` passed.
- No live login-role or browser walkthrough was performed; verify with one
  Super Administrator and assigned/unassigned Report, Check, and Approve users
  before deployment.

### Deployment note

This branch includes its unmerged parent feature work; merge in dependency order
or merge this branch as the cumulative change. It needs a site deployment only
for this assignment, not a new stored procedure. Database connections in this
environment use Windows Authentication.

## `feature/all-pending-reports-licence-date`

- Assignment date: 2026-10-02
- Base branch: `feature/all-new-reports-licence-date`
- Base commit: `423adde`
- Feature commit: `08e681c` (`feat: add licence date to pending reports`)
- Status: Completed and committed locally on the feature branch. The stored
  procedure was deployed to `tn2db\\PRODUCTION / TradeNetDB` on 2026-10-02 using
  Windows Authentication. The site is not deployed. The branch is not merged into
  `main` and not pushed.

### Scope completed

- Confirmed there are four Pending report menus: Import Licence Pending Report,
  Border Import Licence Pending Report, and their two Detail Report (Pending)
  counterparts.
- Confirmed both Detail Report (Pending) menus already displayed `Licence Date`.
- Added `Licence Date` after `Application Date` to the two plain Pending report
  tables and Excel exports.
- Mapped the value to the nullable `IssuedDate` field for Import Licence, Export
  Licence, and Border Import Licence LINQ branches and for both stored-procedure
  branches.
- Incremented the two changed Excel export format versions to invalidate files
  cached with the old column layout.
- Added a database-first deployment package at
  `StoredProcedureMigrations/Deployments/Done For Fix/2026-10-02_PendingReportsLicenceDate`.

### Verification completed

- Focused Pending config tests: 6 passed.
- Focused backend behavior tests: 7 passed.
- Pending/Excel integration contract tests: 1,321 passed.
- Frontend production build passed (existing large-chunk warning only).
- Backend build passed with 0 errors.
- SQL Server parse-only validation passed against `TradeNetDB` using Windows
  Authentication; no database object or data was changed.
- Post-deployment definition and execution checks passed for both Import Licence
  and Border Import Licence procedure branches. All current Pending/Reject rows
  have a null `IssuedDate`, so their visible `Licence Date` cells are expected to
  be blank until an issued date exists.
- Repository-wide frontend tests: 1,849 passed and 7 unrelated parity tests failed.
- Repository-wide backend tests: 2,550 passed and 350 unrelated baseline tests
  failed, primarily because `TradeNetDBTest` or unrelated stored procedures are
  unavailable and because older config-source extractors do not parse the current
  configuration shape.

### Deployment note

Use Windows Authentication for database work. `dbo.sp_PendingReport_pagination`
has been deployed and verified, and its previous definition is preserved as
`RollbackCaptured.sql`. Deploy the site next, then verify all four Pending menus
in the grid and Excel. This branch is based on and therefore includes the unmerged
`feature/all-new-reports-licence-date` work.

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

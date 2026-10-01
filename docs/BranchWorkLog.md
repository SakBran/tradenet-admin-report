# Branch Work Log

This journal records which assignment was implemented on each branch and its actual
merge/deployment state. Add the newest assignment first.

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

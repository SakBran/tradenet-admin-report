# TradeNet Report Database Index Rollout

## Summary
- Scope: `TradeNetDB` report APIs only. Exclude `TemplateDB`.
- Inventory: 158 report controllers, 47 active query classes, 22 deployed pagination procedures, and 3 deployed indexed views.
- Database facts: several multi-million-row tables, zero declared foreign keys, and many historical indexes. Use add-only phased tuning first; do not drop existing indexes during this rollout.
- Target: page-size `10`, `IncludeTotalCount = false`, warm median response under `5s`.

## Audit Artifacts
- Add `tools/audit-tradenet-report-indexes.ps1`. Read the connection string from configuration without printing credentials. Export untracked CSV/JSON snapshots under `artifacts/report-index-audit/`.
- Record table sizes, index definitions, index usage, duplicate-key candidates, missing-index DMVs, deployed indexed views, pagination procedures, and procedure runtime stats.
- Add `docs/ReportIndexAudit.md` with one row per active query class and branch, mapping its controllers, tables, predicates, joins, ordering, current indexes, benchmark result, and action.
- Treat SQL Server DMV suggestions as evidence only. Require query or execution-plan confirmation before DDL.

## Index Batches
1. **Batch 1: Clear Join Gaps**
   - Add `IX_PaThaKaPermitBusiness_PaThaKaId` on `(PaThaKaId) INCLUDE (PermitBusinessId)`. This addresses the strongest live DMV signal and supports `fn_GetPermitBusiness`.
   - Add parent-key covering indexes for `ExportPermitItem`, `BorderImportPermitItem`, and `BorderExportPermitItem` using `(ParentPermitId, HSCodeId, ItemNo)` plus report projection columns as includes.
   - Use semantic preflight checks so production skips creation when an equivalent left-prefix covering index already exists.

2. **Batch 2: Pending Reports**
   - Revise [IX_ImportLicence_PendingReport.sql](C:/Code/Ministry%20of%20Commerce/Tradenet/tradenet-admin-report/StoredProcedureMigrations/Indexes/IX_ImportLicence_PendingReport.sql): retain the `(Status, ApplicationDate, ApplicationNo)` base-table candidate, but remove or skip the proposed item index because an existing wider `ImportLicenceItem` index already covers it.
   - Add the equivalent `BorderImportLicence` pending-report candidate only if its LINQ endpoint remains above `5s` and its plan still sorts or scans.
   - Do not use filtered indexes; shared-table writers may not use the required session settings.

3. **Batch 3: Evidence-Gated Candidates**
   - Benchmark payment reports before adding `AccountTransaction(IsPayment, PaymentDate, TransactionId)` or `MPUPaymentTransaction(TransactionRefNo)` indexes; nearby indexes already exist.
   - Evaluate `PaThaKaRegistration(CompanyRegistrationNo, ApplyType, Status)` only if Company Profile remains slow after Batch 1.
   - For licence and permit base tables, add composite date/filter indexes only when the failing branch’s plan shows a scan or avoidable sort. These tables already contain many overlapping indexes.

4. **Batch 4: Query Fix Escalation**
   - For endpoints still over `5s`, tune SQL shape instead of adding more indexes blindly.
   - Prioritize HS-code grouping, detail reports, voucher reports, pending reports, account summary, and new-report branches.
   - Preserve the established rules: page first, fetch `PageSize + 1`, skip exact counts on the hot path, resolve stable lookup text after materialization, and keep legacy stored procedures untouched by adding or updating `_pagination` wrappers only.

## Deployment
- Create one idempotent SQL file and one rollback file per batch under `StoredProcedureMigrations/Indexes/`.
- Use online index creation with low-priority waiting on the confirmed Enterprise SQL Server edition.
- Apply one batch to test, warm caches, run targeted benchmarks, compare result parity, and monitor blocking, transaction-log growth, and write latency.
- Promote one validated batch at a time. Roll back only newly added named indexes if reads regress or writer impact is unacceptable.
- Document historical duplicate and zero-read indexes for a later shared-workload cleanup project; do not drop them here.

## Verification
- Reuse the reflection approach in `Backend.Tests` to exercise all 158 POST endpoints with configured database access.
- Benchmark page `0` and page `1` over a representative one-month range, with three timed warm runs. Require median `<5s`; record cold load separately.
- Track exact-count and Excel flows separately because they intentionally scan more data.
- For each index: verify rerunnable DDL, no duplicate equivalent index, unchanged row results, stable paging, reduced reads or eliminated sort/scan, and passing backend tests.

## Interfaces And Assumptions
- No public API response or request contract changes.
- Existing indexed views for import licence, import permit, and export permit totals remain deployed and are verified, not recreated.
- The current pending-report index migration is not deployed and must not be applied unchanged.
- `TemplateDB` auth/chat indexing is intentionally out of scope.

# Import Licence Report Feedback Work Note (2026-09-03)

## Purpose

Track all six Import Licence and Border Import Licence report errors reported in
the shared feedback sheet, their investigation status, confirmed causes, fixes
currently on the working branch, and the work still required before release.

Feedback source:
`https://docs.google.com/spreadsheets/d/12KDUDjtxclr4rXLNpi6VwdGLi08uV8QNCprPIqDgClQ/edit?gid=0#gid=0`

Working branch: `fix/import-licence-report-feedback`

Current status: the first four complaints have fixes on the working branch.
Border Import Licence New Report has been corrected and verified locally plus
against production with SELECT-only checks. The remaining two Border Import
Licence complaints have not yet been investigated. Local changes have not been
pushed, merged, deployed, or applied to a database.

## Reported Errors and Confirmed Causes

| Report | Feedback | Confirmed cause | Proposed fix / verification | Current status |
| --- | --- | --- | --- | --- |
| 2.2 Import Licence Daily Report (New Licence Report) | Old report displayed 15 rows, while the new report displayed 10. | The comparison treated the new report's first page as its complete result. The required UI behavior is the global 10-row page size; all matching rows must remain available through pagination. | Remove the report-specific 1000-row override, retain the global 10-row page, and preserve the exact total count so remaining rows appear on later pages. Keep the standard `No.` column because the feedback does not request its removal. | Corrected locally and covered by pagination/config regression tests. Not deployed. |
| 2.4 Import Licence Actual Amendment Report | The feedback recorded 17 rows in the old report and 6 in the new report. | Repository history confirms that an earlier new-report controller used the separate `sp_ActualAmendReport.Query` LINQ path. The report now uses `sp_ActualAmendReport.ExecuteAsync` / `ExecuteQueryable`, which call the legacy-compatible pagination procedure for the grid and Excel. A read-only production comparison for January 6-8, 2025 returns 6 rows from both `sp_ActualAmendReport` and `sp_ActualAmendReport_pagination`, with the same licence keys. Therefore the historical 17 cannot be reproduced because the sheet did not capture the complete filters/snapshot. | Keep both grid and Excel on the stored-procedure path, apply the calendar-date `ToDate` boundary correction, preserve the old `Licence No` mapping, and match the old `Curency` and `HSCode` headers. Do not change additional query predicates without the exact old request or a preserved 17-row result set. | Compatibility fix and regression guards are complete locally. Focused backend and frontend tests pass. The historical 17-row result remains unverified. Not deployed. |
| 2.5 Import Licence Amendment Report | A January 1-5 search included January 6 records. | The frontend sent January 5 at end-of-day, while the pagination procedure added another day to that timestamp. The resulting upper bound became the end of January 6. | Normalize `ToDate` to a calendar date and use an exclusive next-day boundary; verify January 1-5 excludes January 6 in UAT. | Fixed locally; deployment and UAT remain. |
| Border Import Licence New Report | The Sheet says the data differs from the old report and separately says the total is missing. | The generic frontend sends the selected `ToDate` as end-of-day, while the pagination procedure adds another day. A read-only production comparison for August 1, 2023 found 824 correct selected-day rows but 1,390 rows with the current boundary: 566 August 2 rows leak into the result. The API/config also did not provide the old RDLC currency and grand-total footer. | Normalize `ToDate` to its calendar date in the controller and SQL boundary; add a Border Import Licence currency-total query matching both card-type branches and every grid predicate; wire `CurrencyTotals` and `currencyTotalsColumns`. Restore the Border Import Licence section lookup and readonly Company Name filter. Remove the extra visible Form Type/Auto filters only as part of confirmed old-filter parity. Correct the old `Curency` header text. | Fixed and verified locally. Production SELECT-only verification confirms 824 selected-day rows and 566 next-day rows excluded. Not deployed. |
| Border Import Licence By Voucher Report | Output data is incorrect for January 1-5, 2026. | Not yet investigated. | Compare the date boundary and all old/new voucher predicates against the same data using read-only queries; report the exact differences and proposed correction before changing code. | Not started. |
| Border Import Licence Pending Report | Output data is incorrect. | Not yet investigated. | Compare the old/new report definitions, filters, and result-producing queries using read-only checks; report the exact differences and proposed correction before changing code. | Not started. |

## Status Summary

| # | Report | Status | What remains |
| --- | --- | --- | --- |
| 1 | Import Licence Daily Report | Code fix complete locally | Uses the global 10-row page size, retains all matching data across pages, and keeps the standard `No.` column. Not deployed. |
| 2 | Import Licence Actual Amend Report | Compatibility fix complete locally | Grid and Excel are guarded to use the legacy-compatible stored-procedure path; old column mapping/header parity and the inclusive date boundary are covered by tests. Production legacy/new procedures both return 6 rows for the inferred January 6-8, 2025 range. Reproducing the historical 17 still requires the customer's exact old filters or preserved result set. Not deployed. |
| 3 | Import Licence Amendment Report | Fixed locally | Commit, push, deploy to UAT, and confirm January 1-5 excludes January 6. |
| 4 | Border Import Licence New Report | Fixed and verified locally | UAT comparison against the old report remains. No deployment or database write has been performed. |
| 5 | Border Import Licence By Voucher Report | Not started | Diagnose the January 1-5, 2026 result difference with identical filters and read-only data checks. |
| 6 | Border Import Licence Pending Report | Not started | Complete the required Tradenet 2.0 parity check and read-only data diagnosis. |

No complaint is considered fully released until its fix is verified in UAT.

## Production Read-Only Verification

The production reporting database was accessed without printing or persisting
credentials and with `ApplicationIntent=ReadOnly`. All diagnostic statements
were read-only. `TradeNetDB` accepted the connection. On the same server/login,
`TemplateDB` is visible, while the supplied `ReportTemplateDB` catalog is not
visible and a direct connection to it failed. This distinction does not affect
the Import Licence investigation, which reads `TradeNetDB`.

- Import Licence Daily, January 6-8, 2025: the deployed Daily summary procedure
  returns 15 `(IssuedDate, Currency)` groups. With the required global page size,
  these display as 10 rows on page 1 and 5 rows on page 2.
- Import Licence Daily, January 1-5, 2026: the current production snapshot has
  10 `(IssuedDate, Currency)` groups.
- Import Licence Actual Amend, January 6-8, 2025: the legacy procedure and the
  pagination procedure each return 6 rows and the same licence keys.
- Import Licence Actual Amend, January 1-5, 2026 (an assumption because the sheet
  omits this report's dates): the legacy predicates currently match 1 row.
- Border Import Licence New Report, August 1, 2023: a direct SELECT-only
  comparison found 824 rows for the selected calendar day. The current
  end-of-day-plus-one-day boundary matches 1,390 rows, including 566 rows from
  August 2. The new footer query returned 3 currency groups whose licence count
  totals 824, matching the corrected grid population.

Production therefore confirms that item 1 was a paging presentation issue, not
data loss. It does not reproduce the historical 17-versus-6 discrepancy for
item 2; reproducing that result requires the exact old filters or a preserved
old result set/database snapshot.

## Tradenet 2.0 Parity Check

The existing repository parity documents confirm that the filter sets and table
columns for the first three Import Licence reports match the old Tradenet 2.0
report definitions. No filter or column change is required for those three
complaints.

The two remaining Border Import Licence reports still require their own
complaint-scoped parity check before any code change. That check must cover:

- The same filters and option values as the old filter forms.
- The same columns, header text, order, and language as the old RDLC reports.
- The same date boundaries, status/apply-type predicates, joins, and paging rules.
- Business Type lookup values filtered with `FormType = 'Pa Tha Ka'` where that
  filter is present.

Border Import Licence New Report parity differences reported before editing:

- Its column set/order matches `BorderNewReport.rdlc`, but the current
  `Currency` header differs from the old `Curency` text.
- Its Import Section filter falls back to the generic section lookup instead of
  the Border Import Licence section list used by the old filter box.
- The old readonly Company Name helper is missing.
- The new filter box exposes Form Type and Auto inputs not present in the old
  filter box.
- The old RDLC includes currency-wise licence/value totals and a grand licence
  count; the current API/config do not supply that footer.

Relevant existing documents:

- `docs/ReportColumnComparison.md`
- `docs/ImportLicenceReportUiComparison.md`
- `docs/ImportLicenceDailyReport_ComplaintFindings.md`

## Fixes Currently Implemented

### Daily report row display

The fix branch restores the shared table behavior and preserves the complete
result set:

- The report-specific `defaultPageSize: 1000` override is removed, so the page
  uses the global 10-row default.
- The backend returns an exact total count and retains all grouped rows, so a
  15-row result displays as 10 rows on page 1 and 5 rows on page 2.
- The standard `No.` column remains enabled because the feedback sheet does not
  request its removal.
- The matching Excel presentation fixture retains the row-number column.

### Amendment and Actual Amendment date boundaries

The two controllers now pass `request.ToDate.Date` to the pagination procedures.
This makes a selected January 5 end date become the exclusive upper bound of
January 6 at 00:00, rather than January 7 at 00:00.

Changed controllers:

- `Backend/Controllers/Report/ImportLicenceAmendmentReportController.cs`
- `Backend/Controllers/Report/ImportLicenceActualAmendmentReportController.cs`

The stored-procedure scripts also normalize the parameter before adding one day:

```sql
ImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))
```

Changed scripts:

- `StoredProcedureMigrations/sp_AmendReport_pagination.sql`
- `StoredProcedureMigrations/sp_ActualAmendReport_pagination.sql`
- `StoredProcedureMigrations/sp_ImportLicenceListingCurrencyTotals.sql`

The currency-total procedure uses the same boundary as the grid so footer totals
cannot include records excluded from the displayed report.

### Border Import Licence New Report

- The controller normalizes `ToDate` to the selected calendar date, trims the
  Company Registration Number, and ignores stale `Auto` input because the old
  filter box did not expose that filter.
- Both Border Import Licence branches in `sp_NewReport_pagination.sql` now use
  `DATEADD(day, 1, CONVERT(date, @ToDate))`.
- A parameterized SELECT-only footer query returns per-currency licence counts,
  values, and the grand licence total for both Pa Tha Ka and Individual Trading
  card types using the same predicates as the grid.
- The frontend restores the old Sakhan, Border Import Licence Section, Company
  Registration Number, and readonly Company Name filter set. The extra visible
  Form Type and Auto filters were removed.
- The old RDLC `Curency` header spelling is restored, while the `auto` output
  column remains because it exists in the old report.
- Currency/footer metadata was added to the frontend and Excel contract fixture.

### Regression coverage

Added:

- `Backend.Tests/ImportLicenceAmendmentDateBoundaryContractTests.cs`
- `Backend.Tests/BorderImportLicenceNewReportFeedbackTests.cs`
- `Frontend/src/Report/config/reportConfigs.importLicence.test.ts`

The tests cover:

- Controller date normalization for both reports.
- Actual Amendment use of the original stored-procedure path.
- Daily report use of the global 10-row page size.
- A 15-row result remains complete across two pages (10 + 5) with total count 15.
- Daily report configuration retains the standard row-number column.
- Correct SQL boundaries in both pagination procedures and currency totals.
- Inclusion of the selected day and exclusion of the following day.
- Border Import Licence New Report filter/header/footer parity.
- Border Import Licence controller date normalization and old filter scope.
- Matching date boundaries for its grid and footer queries.

## Verification Completed

- Border Import Licence New focused backend tests: 4 passed, 0 failed.
- Border Import Licence New focused frontend config test: 1 passed, 0 failed.
- Excel presentation tests: 1,342 passed, 0 failed.
- Backend Excel contract tests: 1,148 passed, 0 failed.
- Frontend production build: passed. Vite reported only its existing large-chunk warning.
- `git diff --check`: passed; only line-ending notices were reported.
- Structured code review: approved with no critical or required findings.
- No database write, migration, or stored-procedure deployment was performed.

Repository-wide suites are not fully green for reasons outside item 1:

- Backend full suite: 1,960 passed and 330 failed. Failures include test-database
  login/setup problems, missing stored procedures in the smoke-test database,
  and pre-existing controller-contract failures.
- Frontend full suite: 1,391 passed and 9 failed. Eight failures concern unrelated
  Export/Border report expectations; the global fixture test also reports many
  pre-existing stale fixtures. The item 1 fixture was updated directly and its
  focused presentation tests pass.

## Work Still Required

1. Investigate complaints 5-6 and report every filter, column, and data-query
   difference before changing their code. Database checks must remain read-only.
2. Commit only the existing Import Licence fix files and this note. Do not include the
   user's unrelated local changes in `Backend/appsettings.json`,
   `Frontend/package-lock.json`, or `Frontend/src/config.ts`.
3. Push `fix/import-licence-report-feedback` and open a pull request when
   authorized.
4. Deploy the backend branch to UAT. The controller normalization fixes the date
   leak with the currently deployed stored procedure, so a database write is not
   required for the first UAT pass.
5. Run the UAT checks below and record the exact filters and results.
6. Decide separately whether to deploy the three updated SQL scripts. This is a
   database write and requires explicit authorization.
7. If the Actual Amendment counts still differ, obtain the customer's complete
   filter inputs and compare old and new results against the same stable data
   snapshot.

## UAT Checklist

- Daily Report: choose a range returning more than 10 groups and confirm all rows
  are available without the old 10-row truncation impression.
- Amendment Report: search January 1-5 and confirm no January 6 record appears.
- Actual Amendment Report: search the same range and confirm no January 6 record
  appears.
- Actual Amendment Report: compare the old and new reports using identical
  Section, Remark, Company Registration Number, and date filters.
- Confirm the grid row count and currency footer totals use the same date range.
- Border Import Licence New Report: compare the old and new output with every
  filter value recorded.
- Border Import Licence By Voucher Report: reproduce January 1-5, 2026 and verify
  that both reports use the same inclusive date range and voucher predicates.
- Border Import Licence Pending Report: compare old and new rows using identical
  filters, status conditions, and ordering.

## Safety and Working-Tree Notes

- Database access remains read-only unless the user explicitly authorizes a
  deployment or other database write.
- No production connection string is explicitly named in the repository.
- The user's three pre-existing local file changes remain uncommitted and outside
  the report fix.
- A safety stash created before updating the branch is still retained.

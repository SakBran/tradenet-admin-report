# AccountSummaryReport Performance Check And Test List

Created: 2026-06-09

Scope:

- Current slow report: `AccountSummaryReport`
- Old source of truth: `C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin`
- Old RDLC: `ReportControl\AccountSummaryReport.rdlc`
- Old MVC view: `Views\Reports\AccountSummaryReport.cshtml`
- Old stored procedure: `dbo.sp_AccountSummaryReport`
- New API: `POST /api/AccountSummaryReport`
- New controller: `Backend\Controllers\Report\AccountSummaryReportController.cs`
- New pagination procedure: `StoredProcedureMigrations\sp_AccountSummaryReport_pagination.sql`
- New frontend config: `Frontend\src\Report\config\reportConfigs.ts`

## Current Finding

`AccountSummaryReport` is still slow.

Live DB tests from `MPUAndAccountSummary_PerformanceDataAccuracyChecklist.md` showed:

| Test | Result |
| --- | --- |
| Old `sp_AccountSummaryReport`, one-day all form types, `2026-06-01` | Timeout after about 60s |
| New `sp_AccountSummaryReport_pagination`, one-day all form types, `IncludeTotalCount=1` | Timeout after about 60s |
| Old `sp_AccountSummaryReport`, one-day `Import Licence`, `2026-06-01` | Timeout after about 60s |
| New `sp_AccountSummaryReport_pagination`, one-day `Import Licence`, `IncludeTotalCount=0` | Timeout after about 60s |

Important note:

- The controller was already changed to respect `request.IncludeTotalCount`.
- That removes one performance problem, but Account Summary still times out even on the fast-count path.
- So the remaining problem is probably inside `sp_AccountSummaryReport_pagination` query shape, joins, temp-table materialization, missing indexes, or old procedure complexity.

Update after fix:

- `StoredProcedureMigrations\sp_AccountSummaryReport_pagination.sql` was updated to normalize parameters, prefilter `#payments` by `AccountTransaction.TransactionFormType` when a specific `@FormType` is requested, add `OPTION (RECOMPILE)`, and include `TransactionId` in the temp-table order index.
- The updated procedure was applied manually to `TradeNetDBTest` on 2026-06-09.
- This same SQL file must be manually executed in any other database environment where the fix is needed.

## 1. Static Code Check List

### Controller

File: `Backend\Controllers\Report\AccountSummaryReportController.cs`

- [ ] Confirm `request.IncludeTotalCount` is passed to `sp_AccountSummaryReport.ExecuteAsync(...)`.
- [ ] Confirm `IncludeTotalCount=false` uses `ApiResult<T>.CreateFastPageFromRows(...)`.
- [ ] Confirm `IncludeTotalCount=true` uses `CreatePageFromRows(...)`.
- [ ] Confirm page size is capped at `MaxPageSize = 1000`.
- [ ] Confirm no controller code materializes all rows before paging.
- [ ] Add `CancellationToken` to JSON endpoint later, so abandoned slow requests can stop SQL work.

### Stored Procedure Wrapper

File: `Backend\StoredProcedureToLinq\sp_AccountSummaryReport.cs`

- [ ] Confirm wrapper calls `dbo.sp_AccountSummaryReport_pagination`.
- [ ] Confirm `@IncludeTotalCount` is passed into SQL.
- [ ] Confirm `TotalCount` is nullable in row model.
- [ ] Confirm Excel path still uses all-row streaming intentionally.

### Pagination Procedure

File: `StoredProcedureMigrations\sp_AccountSummaryReport_pagination.sql`

- [ ] Check whether `#payments` loads too many rows before report-specific filtering.
- [ ] Check whether `#rows` inserts all report branches before paging.
- [ ] Check whether `@FormType` and `@SakhanId` filters are pushed into each branch early.
- [ ] Check whether `@IncludeTotalCount=0` still materializes all branch rows before returning page.
- [ ] Check whether final `ORDER BY` requires sorting the whole `#rows` table.
- [ ] Check temp-table indexes:
  - `#payments(TransactionId)`
  - `#payments(PaymentDate, SortOrder, Id)`
  - `#rows` final order/index candidates
- [ ] Check base table indexes on:
  - `AccountTransaction(IsPayment, VoucherDate)`
  - `AccountTransactionDetail(AccountTransactionId)`
  - `AccountTitle(Id)`
  - branch transaction id columns, e.g. `ImportLicence(Id)`, `ExportLicence(Id)`, etc.

## 2. Data Accuracy Check List

The old report is the truth for output data.

### Column Parity

Old RDLC columns:

`No`, `Entry Date`, `Company Registration No`, `Company Name`, `Voucher No`, `Transaction Title`, `Deducted Fees`, `Remark`

New columns:

`No`, `Entry Date`, `Company Registration No`, `Company Name`, `Voucher No`, `Transaction Title`, `Deducted Fees`, `Remark`

Result:

- [x] Column parity passes.

### Filter Parity

Old filters:

- `FromDate`
- `ToDate`
- `SakhanId` dropdown
- `FormType` dropdown

New filters:

- `FromDate`
- `ToDate`
- `SakhanId`
- `FormType`

Checks:

- [ ] Confirm `SakhanId` is rendered as dropdown using `ReportLookups/sakhans`.
- [ ] Convert `FormType` from text input to dropdown if strict old UI parity is required.
- [ ] Confirm `FormType` values match old `CardTypeRepository.GetAll()` values.
- [ ] Confirm old special display behavior for `WineImportation` / `Alcoholic Beverages Importation`.

### Row Comparison

For each test window, compare old `dbo.sp_AccountSummaryReport` against new `dbo.sp_AccountSummaryReport_pagination`.

Compare keys:

- [ ] `Id`
- [ ] `VoucherDate`
- [ ] `VoucherNo`
- [ ] `AccountTitleCode`
- [ ] `TransactionTitle`
- [ ] `Amount`

Compare displayed fields:

- [ ] `VoucherDate` -> `Entry Date`
- [ ] `CompanyRegistrationNo`
- [ ] `CompanyName`
- [ ] `VoucherNo`
- [ ] `TransactionTitle`
- [ ] `Amount` -> `Deducted Fees`
- [ ] `Remark` behavior, if present/blank in old RDLC output

Branch coverage:

- [ ] `Member`
- [ ] `Pa Tha Ka`
- [ ] `Import Licence`
- [ ] `Import Permit`
- [ ] `Export Licence`
- [ ] `Export Permit`
- [ ] `Border Import Licence`
- [ ] `Border Import Permit`
- [ ] `Border Export Licence`
- [ ] `Border Export Permit`
- [ ] `Business Service Agency`
- [ ] `Duty Free Shop`
- [ ] `Re-Export`
- [ ] `Sale Center`
- [ ] `Show Room`
- [ ] `EV Show Room`
- [ ] `EVCycle Show Room`
- [ ] `Whole Sale`
- [ ] `Retail`
- [ ] `Whole Sale and Retail`
- [ ] `Wine Imporation` old spelling

## 3. Performance Test List

Use the DB connection from:

- `Backend/appsettings.json`
- `ConnectionStrings:TradeNetDBTest`

Do not copy credentials into logs or docs.

### Procedure Existence

```sql
SELECT name
FROM sys.procedures
WHERE name IN (
    'sp_AccountSummaryReport',
    'sp_AccountSummaryReport_pagination'
)
ORDER BY name;
```

Expected:

- [x] `sp_AccountSummaryReport`
- [x] `sp_AccountSummaryReport_pagination`

### Fast First Page Tests

Run each with `@IncludeTotalCount = 0`.

```sql
EXEC dbo.sp_AccountSummaryReport_pagination
    @FromDate = '2026-06-01 00:00:00',
    @ToDate = '2026-06-01 23:59:59',
    @FormType = N'Import Licence',
    @SakhanId = 0,
    @SortColumn = NULL,
    @SortOrder = NULL,
    @PageIndex = 0,
    @PageSize = 10,
    @IncludeTotalCount = 0;
```

Acceptance:

- [x] Returns in under 3 seconds for narrow/specific form type.
- [x] Returns 10 or 11 rows, or fewer when fewer rows match.
- [x] `TotalCount` is NULL.
- [ ] API response has `isTotalCountExact=false`.

Repeat for:

- [x] `@FormType = N''`, `@SakhanId = 0`
- [x] `@FormType = N'Import Licence'`, `@SakhanId = 0`
- [ ] `@FormType = N'Export Licence'`, `@SakhanId = 0`
- [ ] `@FormType = N'Border Import Licence'`, real `@SakhanId`
- [ ] `@FormType = N'Border Export Permit'`, real `@SakhanId`

### Exact Count Tests

Run each with `@IncludeTotalCount = 1`.

Acceptance:

- [x] Returns in under 10 seconds for narrow/specific form type.
- [x] Returns exact `TotalCount`.
- [x] Does not time out for the tested 2026-06-01 windows.

If exact count is still too slow:

- [ ] Keep UI default as `IncludeTotalCount=false`.
- [ ] Only run exact count on explicit user action.
- [ ] Consider cached counts by date/form/sakhan.

### Old vs New Accuracy Tests

For a very narrow date window:

```sql
EXEC dbo.sp_AccountSummaryReport
    @FromDate = '2026-06-01 00:00:00',
    @ToDate = '2026-06-01 23:59:59',
    @FormType = N'Import Licence',
    @SakhanId = 0;

EXEC dbo.sp_AccountSummaryReport_pagination
    @FromDate = '2026-06-01 00:00:00',
    @ToDate = '2026-06-01 23:59:59',
    @FormType = N'Import Licence',
    @SakhanId = 0,
    @SortColumn = NULL,
    @SortOrder = NULL,
    @PageIndex = 0,
    @PageSize = 1000,
    @IncludeTotalCount = 0;
```

Compare:

- [ ] row count when old returns fewer than page size
- [ ] first 10 rows
- [ ] last 10 rows, if practical
- [ ] amount totals for returned page
- [ ] duplicate/missing `VoucherNo`

## 4. SQL Investigation List

Run these read-only checks.

### Check Row Volume By Date

```sql
SELECT COUNT(*) AS PaymentRows
FROM dbo.AccountTransaction
WHERE IsPayment = 1
  AND VoucherDate >= '2026-06-01 00:00:00'
  AND VoucherDate <= '2026-06-01 23:59:59';
```

### Check Detail Fanout

```sql
SELECT TOP 20
    atx.Id,
    atx.TransactionId,
    COUNT(*) AS DetailRows
FROM dbo.AccountTransaction atx
INNER JOIN dbo.AccountTransactionDetail d
    ON atx.Id = d.AccountTransactionId
WHERE atx.IsPayment = 1
  AND atx.VoucherDate >= '2026-06-01 00:00:00'
  AND atx.VoucherDate <= '2026-06-01 23:59:59'
GROUP BY atx.Id, atx.TransactionId
ORDER BY DetailRows DESC;
```

### Check Missing Index Candidates

```sql
SELECT
    migs.avg_total_user_cost,
    migs.avg_user_impact,
    mid.statement,
    mid.equality_columns,
    mid.inequality_columns,
    mid.included_columns
FROM sys.dm_db_missing_index_group_stats migs
INNER JOIN sys.dm_db_missing_index_groups mig
    ON migs.group_handle = mig.index_group_handle
INNER JOIN sys.dm_db_missing_index_details mid
    ON mig.index_handle = mid.index_handle
WHERE mid.database_id = DB_ID()
  AND (
      mid.statement LIKE '%AccountTransaction%'
      OR mid.statement LIKE '%AccountTransactionDetail%'
      OR mid.statement LIKE '%ImportLicence%'
      OR mid.statement LIKE '%ExportLicence%'
      OR mid.statement LIKE '%Border%'
  )
ORDER BY migs.avg_total_user_cost * migs.avg_user_impact DESC;
```

### Check Execution Statistics

```sql
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

EXEC dbo.sp_AccountSummaryReport_pagination
    @FromDate = '2026-06-01 00:00:00',
    @ToDate = '2026-06-01 23:59:59',
    @FormType = N'Import Licence',
    @SakhanId = 0,
    @SortColumn = NULL,
    @SortOrder = NULL,
    @PageIndex = 0,
    @PageSize = 10,
    @IncludeTotalCount = 0;

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
```

Capture:

- [ ] logical reads for `AccountTransaction`
- [ ] logical reads for `AccountTransactionDetail`
- [ ] logical reads for branch tables
- [ ] CPU time
- [ ] elapsed time
- [ ] spills / memory grant warnings from actual execution plan

## 5. Fix Ideas To Test

Do not apply all at once. Test one change at a time.

### Option A: Page Payments First

Idea:

- Filter `AccountTransaction` by date/payment first.
- Join details and branch tables only for candidate page rows.

Risk:

- Might change ordering if old report sorts after branch expansion.

Test:

- Compare old/new rows for each form type.

### Option B: Branch Only Requested FormType

Idea:

- If `@FormType != ''`, execute only the matching branch.
- Avoid materializing every report family into `#rows`.

Risk:

- Must preserve exact old special names like `Wine Imporation`.

Test:

- One test per `FormType`.

### Option C: Add Targeted Indexes

Candidate indexes to test in dev only:

```sql
CREATE INDEX IX_AccountTransaction_IsPayment_VoucherDate
ON dbo.AccountTransaction(IsPayment, VoucherDate)
INCLUDE (Id, TransactionId, VoucherNo, PaymentDate);

CREATE INDEX IX_AccountTransactionDetail_AccountTransactionId
ON dbo.AccountTransactionDetail(AccountTransactionId)
INCLUDE (AccountTitleId, Amount);
```

Risk:

- Production write overhead.
- Must verify existing indexes before adding duplicates.

Test:

- Compare execution plan and logical reads before/after.

### Option D: Split Count From Page

Idea:

- Keep `@IncludeTotalCount=0` page path completely count-free.
- Exact count path can run separately and only when requested.

Risk:

- UI total pages are estimated until exact count is requested.

Test:

- API response should show `isTotalCountExact=false`.
- Pagination next/previous should still work.

## 6. Acceptance Criteria

Performance:

- [ ] `POST /api/AccountSummaryReport` first page returns under 3 seconds for a specific form type.
- [ ] `POST /api/AccountSummaryReport` first page returns under 10 seconds for all form types over one day.
- [ ] `@IncludeTotalCount=0` never runs exact count.
- [ ] No 60-second timeout for one-day windows.

Data accuracy:

- [ ] Columns match old RDLC.
- [ ] Filter values match old UI.
- [ ] First page rows match old stored procedure for the same parameters.
- [ ] Amount values match old output.
- [ ] No missing/extra rows for windows smaller than page size.

Operational:

- [ ] `dotnet build Backend\API.csproj` passes.
- [ ] `npm run build` from `Frontend` passes if frontend filters change.
- [ ] Live DB timing results are recorded in this file after each SQL change.

## 7. Fix Applied On 2026-06-09

Changed file:

- `StoredProcedureMigrations\sp_AccountSummaryReport_pagination.sql`

Changes:

- Normalize `@FormType` and `@SakhanId` at procedure start.
- Add early `AccountTransaction.TransactionFormType = @FormType` filtering when `@FormType` is not blank.
- Add `OPTION (RECOMPILE)` so SQL Server can build a plan for the current `@FormType`, `@SakhanId`, and date parameters.
- Add `TransactionId` as an included column to `IX_payments_Order`.

Manual database action:

- Applied to `TradeNetDBTest` by executing `StoredProcedureMigrations\sp_AccountSummaryReport_pagination.sql`.
- This is not automatic from `dotnet build`.
- To apply in UAT/production, manually run the same SQL file against that database.

Before / after timing:

| Test | Before | After |
| --- | --- | --- |
| `@FormType = N'Import Licence'`, `@IncludeTotalCount = 0`, 2026-06-01 | Timed out at about 60s | 1,100 ms, 3 rows |
| `@FormType = N''`, `@IncludeTotalCount = 0`, 2026-06-01 | Timed out at about 60s | 3,031 ms, 3 rows |
| `@FormType = N'Import Licence'`, `@IncludeTotalCount = 1`, 2026-06-01 | Not passing before fix | 981 ms, 3 rows, `TotalCount=3` |
| `@FormType = N''`, `@IncludeTotalCount = 1`, 2026-06-01 | Not passing before fix | 3,099 ms, 3 rows, `TotalCount=3` |

Rows returned after fix for `2026-06-01`, `Import Licence`:

| VoucherNo | PaymentDate | Company | TransactionTitle | Amount |
| --- | --- | --- | --- | --- |
| `U06202600001` | `2026-06-01 15:37:42` | `SD02Co., Ltd.` | `Application Form Fees` | `10000` |
| `U06202600002` | `2026-06-01 15:55:19` | `SD02Co., Ltd.` | `သွင်းကုန်လိုင်စင်ကြေး` | `60000` |
| `U06202600003` | `2026-06-01 16:54:06` | `SD02Co., Ltd.` | `Application Form Fees` | `10000` |

Remaining checks:

- [ ] Re-test more dates with larger row counts.
- [ ] Compare old `dbo.sp_AccountSummaryReport` vs new pagination procedure for a small window where the old procedure completes.
- [ ] Test border `SakhanId` filters with real border data.
- [ ] Confirm API response metadata shows `isTotalCountExact=false` when `IncludeTotalCount=false`.

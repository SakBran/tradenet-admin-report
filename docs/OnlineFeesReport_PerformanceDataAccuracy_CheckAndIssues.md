# OnlineFeesReport Performance And Data Accuracy Check / Issue List

Created: 2026-06-09
Last updated: 2026-06-09 after applying OnlineFees fixes to `TradeNetDBTest`

Scope:

- Report: `OnlineFeesReport`
- Old project: `C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin`
- Old RDLC: `ReportControl\OnlineFeesReport.rdlc`
- Old view: `Views\Reports\OnlineFeesReport.cshtml`
- Old data call: `Business\Reports.cs` -> `dbo.sp_OnlineFeesReport`
- New controller: `Backend\Controllers\Report\OnlineFeesReportController.cs`
- New LINQ/stored-procedure wrapper: `Backend\StoredProcedureToLinq\sp_OnlineFeesReport.cs`
- New pagination procedure: `StoredProcedureMigrations\sp_OnlineFeesReport_pagination.sql`
- New frontend config: `Frontend\src\Report\config\reportConfigs.ts`

## 1. Current Summary

### Fix Applied On 2026-06-09

Files changed:

- `Frontend\src\Report\config\reportConfigs.ts`
  - Changed `OnlineFeesReport` `FormType` from free-text input to old-equivalent `Certificate Type` dropdown.
  - Added old-compatible certificate values, including border licence/permit values and display label `Alcoholic Beverages Importation` with value `Wine Importation`.
  - Made `SakhanId` lookup explicit with `lookupName: 'sakhans'`.
- `StoredProcedureMigrations\sp_OnlineFeesReport_pagination.sql`
  - Normalizes `@FormType` and `@SakhanId`.
  - Applies `@FormType` while loading `#OnlineFeeRows`, before branch joins and final paging.
  - Adds `OPTION (RECOMPILE)` to the fee-row load and final dynamic query.
  - Adds `IX_OnlineFeeRows_Order` temp-table index for final ordering.

Manual database action:

- Applied `StoredProcedureMigrations\sp_OnlineFeesReport_pagination.sql` to `TradeNetDBTest` on 2026-06-09.
- UAT/production still need manual execution of `StoredProcedureMigrations\sp_OnlineFeesReport_pagination.sql`.
- If missing in UAT/production, DBAs should review and apply `IX_AccountTransaction_OnlineFees` from `StoredProcedureMigrations\IndexedMigrations\TradeNetReportIndexes_Production.sql`.

Post-fix performance results on `TradeNetDBTest`:

| Test | Before Fix | After Fix | Result |
| --- | ---: | ---: | --- |
| All forms, `2026-06-01` to `2026-06-03`, `IncludeTotalCount=0` | 4,411 ms, 3 rows | 363 ms, 3 rows | PASS |
| All forms, same window, `IncludeTotalCount=1` | 1,575 ms, 3 rows, `TotalCount=3` | 308 ms, 3 rows, `TotalCount=3` | PASS |
| `Import Licence`, same window, `IncludeTotalCount=0` | Not captured before fix | 304 ms, 2 rows | PASS |
| `Import Licence`, same window, `IncludeTotalCount=1` | Not captured before fix | 314 ms, 2 rows, `TotalCount=2` | PASS |

Build verification:

- [x] `dotnet build Backend\API.csproj` passed.
- [x] `npm run build` from `Frontend` passed.
- [ ] Vite still reports an existing large chunk warning; build output is valid.

### Static Parity

| Area | Result | Notes |
| --- | --- | --- |
| Columns | PASS | Old RDLC columns match new config columns. `No` is rendered by `showRowNumber`. |
| Date filters | PASS | Old has `FromDate`/`ToDate`; new has date range with `FromDate`/`ToDate`. |
| Sakhan filter | PASS | New config now declares `lookupName: 'sakhans'` explicitly. |
| FormType filter | PASS | New config now uses a curated dropdown labeled `Certificate Type` with old-equivalent values. |
| Grand total footer | PASS | Old RDLC has no total footer; new response has no `ColumnTotals`. |

### Current Performance Evidence

Live DB tests used `Backend/appsettings.json -> ConnectionStrings:TradeNetDBTest`.

| Test | Result |
| --- | --- |
| Procedure existence | PASS: `sp_OnlineFeesReport` and `sp_OnlineFeesReport_pagination` both exist. |
| Latest source rows query | 14,533 ms for top 5 online-fee rows ordered by latest voucher date. |
| `sp_OnlineFeesReport_pagination`, `2026-06-01` to `2026-06-03`, all form types, `IncludeTotalCount=0` | Before fix: 4,411 ms, 3 rows. After fix: 363 ms, 3 rows. |
| Same window, `IncludeTotalCount=1` | Before fix: 1,575 ms, 3 rows, `TotalCount=3`. After fix: 308 ms, 3 rows, `TotalCount=3`. |
| Same window, `FormType='Import Licence'`, `IncludeTotalCount=0` | After fix: 304 ms, 2 rows. |
| Same window, `FormType='Import Licence'`, `IncludeTotalCount=1` | After fix: 314 ms, 2 rows, `TotalCount=2`. |

Previous audit evidence:

- `ReportTesting_PerfAndUiParity_2026-06-06.md` recorded `OnlineFeesReport` as a 500 / SQL timeout around 35 seconds.
- `docs\ReportIndexAudit.md` says an earlier Online Fees query-shape fix reduced warm isolated POST smoke to about 2,067 ms median before installing `IX_AccountTransaction_OnlineFees`.

## 2. Issue List

| Priority | Issue | Evidence | Impact | Suggested Fix |
| --- | --- | --- | --- | --- |
| P0 | FormType value parity defect | Old UI uses dropdown from `CardTypeRepository.GetAll()` plus four border card types; new UI used free text. | Users could enter invalid values; could not reliably reproduce old report options. | FIXED: `OnlineFeesReport` now uses old-equivalent dropdown/options. |
| P1 | FormType label mismatch | Old label uses resource `CardType`, meaning `Certificate Type`; new label was `Form Type`. | UI did not match old report wording. | FIXED: visible label is now `Certificate Type`. |
| P1 | Source online-fee scan is still slow | Top 5 latest online-fee source query took 14,533 ms. | Search/export can still degrade on broader date ranges. | Verify `IX_AccountTransaction_OnlineFees` exists and is used; inspect execution plan/logical reads. |
| P1 | Fast page still above target for small 3-row window | Before fix, `IncludeTotalCount=0` took 4,411 ms for only 3 returned rows. After fix, it took 363 ms. | First-page load target is now met for tested narrow window. | FIXED for tested window; continue wider-window testing. |
| P1 | `COUNT(*) OVER()` exists in pagination procedure | Current SQL uses `COUNT(*) OVER()` when `@IncludeTotalCount=1`. | On wide ranges, exact count can force full materialization. | Split count into separate scalar only when requested, or keep default UI `IncludeTotalCount=false`. |
| P2 | Possible old export behavior mismatch | Old MVC created `uploads/OnlineFees.xlsx` from template during POST; new uses background Excel job. | Output format/template may differ from old Excel export. | Compare old template output columns/format to new generated Excel. |
| P2 | Data accuracy not fully proven across all branches | Current live check covered `Import Licence` and `Export Permit` rows only. | Branch-specific joins may hide mismatches. | Run branch coverage tests for all report form types. |
| P2 | Sakhan dropdown inferred, not explicit | New config used `type: 'number'` with no explicit `lookupName`. | Future GenericReportPage changes could break dropdown behavior. | FIXED: `lookupName: 'sakhans'` is explicit. |

## 3. Old vs New Filter Parity Test List

Old filter box:

- `FromTime`
- `ToTime`
- `SakhanId` dropdown from `SakhanRepository.GetAll()`
- `FormType` dropdown from:
  - `CardTypeRepository.GetAll()`
  - plus `Border Export Licence`
  - plus `Border Import Licence`
  - plus `Border Export Permit`
  - plus `Border Import Permit`
  - `WineImportation` displayed as `Alcoholic Beverages Importation`
  - leading `- All -` empty option

New filter box:

- `FromDate`
- `ToDate`
- `SakhanId`
- `FormType`

Checks:

- [x] Configure `SakhanId` with explicit lookup source.
- [ ] Verify `SakhanId` renders as searchable dropdown, not raw number input.
- [ ] Verify `SakhanId` options match old `SakhanRepository.GetAll()` values.
- [x] Change `FormType` to dropdown or add lookup.
- [x] Include leading `All` empty option for `FormType`.
- [x] Include four border certificate types in `FormType`.
- [x] Relabel `Wine Importation` option to `Alcoholic Beverages Importation`.
- [x] Rename visible label from `Form Type` to `Certificate Type` if strict old parity is required.

## 4. Column / Output Data Accuracy Test List

Old RDLC columns:

`No`, `Entry Date`, `Company Registration No`, `Company Name`, `Transaction Title`, `Deducted Fees`, `Remark`

New config columns:

`No`, `Entry Date`, `Company Registration No`, `Company Name`, `Transaction Title`, `Deducted Fees`, `Remark`

Checks:

- [x] Column count matches.
- [x] Header text matches.
- [x] Language matches.
- [ ] Confirm `Transaction Title` maps to old `FormType` field.
- [ ] Confirm `Remark` is blank like old output.
- [ ] Confirm no total footer appears.
- [ ] Confirm date display matches old `dd/MM/yyyy` expectation.
- [ ] Confirm amount formatting matches old RDLC / Excel template.

Compare fields:

- [ ] `VoucherDate`
- [ ] `CompanyRegistrationNo`
- [ ] `CompanyName`
- [ ] `FormType`
- [ ] `Amount`
- [ ] `Remark`

## 5. Stored Procedure Performance Test List

Use the configured DB connection from `Backend/appsettings.json`.

### Procedure Existence

```sql
SELECT name
FROM sys.procedures
WHERE name IN ('sp_OnlineFeesReport', 'sp_OnlineFeesReport_pagination')
ORDER BY name;
```

Expected:

- [x] `sp_OnlineFeesReport`
- [x] `sp_OnlineFeesReport_pagination`

### Fast Page Test

```sql
EXEC dbo.sp_OnlineFeesReport_pagination
    @FromDate = '2026-06-01 00:00:00',
    @ToDate = '2026-06-03 23:59:59',
    @FormType = N'',
    @SakhanId = 0,
    @SortColumn = NULL,
    @SortOrder = NULL,
    @PageIndex = 0,
    @PageSize = 10,
    @IncludeTotalCount = 0;
```

Acceptance:

- [x] Under 3 seconds for small/narrow window.
- [x] No timeout.
- [x] `TotalCount` is NULL.
- [x] Returns page rows.

Result:

- Before fix: 4,411 ms, 3 rows.
- After fix: 363 ms, 3 rows. Target met.

### Exact Count Test

```sql
EXEC dbo.sp_OnlineFeesReport_pagination
    @FromDate = '2026-06-01 00:00:00',
    @ToDate = '2026-06-03 23:59:59',
    @FormType = N'',
    @SakhanId = 0,
    @SortColumn = NULL,
    @SortOrder = NULL,
    @PageIndex = 0,
    @PageSize = 10,
    @IncludeTotalCount = 1;
```

Acceptance:

- [x] Under 10 seconds for tested narrow window.
- [x] Exact `TotalCount` returned.
- [x] No timeout.

Result:

- Before fix: 1,575 ms, 3 rows, `TotalCount=3`.
- After fix: 308 ms, 3 rows, `TotalCount=3`.

### Branch Coverage Tests

Run fast page and exact count tests for:

- [x] `FormType = ''` - 363 ms fast page, 308 ms exact count, `TotalCount=3`.
- [x] `FormType = 'Import Licence'` - 304 ms fast page, 314 ms exact count, `TotalCount=2`.
- [ ] `FormType = 'Import Permit'`
- [ ] `FormType = 'Export Licence'`
- [ ] `FormType = 'Export Permit'`
- [ ] `FormType = 'Border Import Licence'`
- [ ] `FormType = 'Border Import Permit'`
- [ ] `FormType = 'Border Export Licence'`
- [ ] `FormType = 'Border Export Permit'`
- [ ] `FormType = 'Pa Tha Ka'`
- [ ] `FormType = 'Member'`
- [ ] `FormType = 'Wine Importation'`
- [ ] sale/show room/retail/wholesale registration types present in card types

For each:

- [ ] elapsed ms
- [ ] row count
- [ ] first 5 rows
- [ ] exact count if requested
- [ ] old vs new row comparison for small windows

## 6. SQL Investigation List

### Verify Index Exists

```sql
SELECT
    i.name,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS KeyColumns
FROM sys.indexes i
JOIN sys.index_columns ic
    ON i.object_id = ic.object_id
   AND i.index_id = ic.index_id
   AND ic.is_included_column = 0
JOIN sys.columns c
    ON ic.object_id = c.object_id
   AND ic.column_id = c.column_id
WHERE OBJECT_NAME(i.object_id) = 'AccountTransaction'
  AND i.name = 'IX_AccountTransaction_OnlineFees'
GROUP BY i.name;
```

Expected:

- [ ] `IX_AccountTransaction_OnlineFees` exists in target DB.
- [ ] Execution plan uses it for online-fee scan.

### Logical Reads / Time

```sql
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

EXEC dbo.sp_OnlineFeesReport_pagination
    @FromDate = '2026-06-01 00:00:00',
    @ToDate = '2026-06-03 23:59:59',
    @FormType = N'',
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

- [ ] logical reads on `AccountTransaction`
- [ ] logical reads on `AccountTransactionDetail`
- [ ] logical reads on `AccountTitle`
- [ ] logical reads on each registration branch table
- [ ] CPU time
- [ ] elapsed time
- [ ] memory grant warnings / spills from actual plan

### Query Shape Checks

- [ ] Does `#OnlineFeeRows` filter by `@FormType` early?
- [ ] Does it filter by `@SakhanId` early for border branches?
- [ ] Does `@IncludeTotalCount=0` still sort/materialize all rows?
- [ ] Does `COUNT(*) OVER()` cause full materialization on exact count?
- [ ] Does the final `ORDER BY` use a supporting temp index?
- [ ] Are branch tables joined only after online-fee rows are narrowed?

## 7. Recommended Improvement Tasks

### P0 / P1

1. Fix `FormType` UI parity:
   - Change from text input to dropdown.
   - Use old option source/values.
   - Label as `Certificate Type` if matching old UI exactly.

2. Confirm or deploy `IX_AccountTransaction_OnlineFees`:
   - Source file reference: `StoredProcedureMigrations\IndexedMigrations\TradeNetReportIndexes_Production.sql`.
   - This may need manual DB execution in each environment.

3. Optimize `sp_OnlineFeesReport_pagination`:
   - Normalize `@FormType`/`@SakhanId`.
   - Push `@FormType` into `#OnlineFeeRows` when possible:
     `AND (@FormType = N'' OR AccountTransaction.TransactionFormType = @FormType)`
   - Add `OPTION (RECOMPILE)` to the initial fee-row insert and dynamic final query.
   - Keep monitoring `COUNT(*) OVER()` on wider windows; tested narrow exact-count window is now under target.

4. Add repeatable old-vs-new data comparison script:
   - Compare old `sp_OnlineFeesReport` vs new `sp_OnlineFeesReport_pagination`.
   - Use small windows where old procedure completes.

### P2

5. Make `SakhanId` lookup explicit in `reportConfigs.ts`:
   - Add `lookupName: 'sakhans'`.

6. Compare old Excel template output vs new Excel job output:
   - Old template: `Content/excel-template/TransactionFees.xlsx`.
   - Old generated path: `uploads/OnlineFees.xlsx`.

7. Add endpoint timing logs:
   - Report key
   - user
   - parameters excluding secrets
   - elapsed ms
   - row count
   - `IncludeTotalCount`

## 8. Acceptance Criteria

Performance:

- [ ] First page under 3 seconds for one-day/small windows.
- [ ] First page under 10 seconds for wider normal business windows.
- [ ] No 30s/35s/60s timeout.
- [ ] Exact count path only runs when requested.
- [ ] Excel export uses background queue and streams rows.

Data accuracy:

- [ ] Column headers match old RDLC.
- [ ] Filter options match old UI.
- [ ] First page rows match old stored procedure for same parameters.
- [ ] Amount totals match for small windows.
- [ ] Branch coverage passes for every form type.

Operational:

- [x] `dotnet build Backend\API.csproj` passes after backend changes.
- [x] `npm run build` from `Frontend` passes after frontend config changes.
- [x] SQL procedure change applied to `TradeNetDBTest` and recorded here.
- [ ] SQL procedure/index changes are manually applied to UAT/production target DBs and recorded here.

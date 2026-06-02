# TradeNet Report Index Audit

Updated: 2026-06-02

## Scope

This audit covers the `TradeNetDB` report APIs only. It does not change public
API contracts, drop existing indexes, or include `TemplateDB`.

The repository scan included the report controllers, active LINQ query helpers,
stored-procedure pagination wrappers, existing index migration SQL, indexed
views, and the deployed `TradeNetDB` catalog.

## Inventory

| Area | Current inventory |
| --- | ---: |
| Report controllers under `Backend/Controllers/Report` | 158 |
| Active report query classes | 47 |
| Deployed `*_pagination` procedures | 22 |
| Existing report indexed views | 3 |
| Deployed foreign keys in `TradeNetDB` | 0 |

The earlier `125 controllers` references in pagination and smoke-test notes are
historical checkpoints from a smaller scope. The current full report API
inventory is 158 controllers.

## Existing Coverage

The live catalog already contains:

- `IX_vw_ExportPermitItemTotalByCurrency`
- `IX_vw_ImportLicenceItemTotalByCurrency`
- `IX_vw_ImportPermitItemTotalByCurrency`
- `NonClusteredIndex-20230523-154403` on
  `ImportPermitItem(ImportPermitId, HSCodeId, ItemNo)` with the report item
  projection included.
- `NonClusteredIndex-20230523-1540342` on
  `ImportLicenceItem(ImportLicenceId, Id, UniqueId, HSCodeId, ItemNo)` with
  `HSCode`, `HSYear`, `Description`, `UnitId`, `Price`, `Quantity`, `Amount`,
  `CurrencyId`, `ParentId`, `CheckId`, and `CreatedDate` included.

The existing `ImportLicenceItem` index is wider than the earlier proposed
`IX_ImportLicenceItem_LicenceId_Cover`, so the canonical migration deliberately
does not create that duplicate.

## Added Indexes

The production package is:

`StoredProcedureMigrations/IndexedMigrations/TradeNetReportIndexes_Production.sql`

It creates these indexes only when an equivalent left-prefix covering index is
not already installed:

| Batch | Index | Purpose |
| --- | --- | --- |
| 1 | `IX_PaThaKaPermitBusiness_PaThaKaId` | Clear the PaThaKa permit-business lookup gap. |
| 1 | `IX_ExportPermitItem_ReportCover` | Cover export permit detail and HS-code item joins. |
| 1 | `IX_BorderImportPermitItem_ReportCover` | Cover border import permit detail and HS-code item joins. |
| 1 | `IX_BorderExportPermitItem_ReportCover` | Cover border export permit detail and HS-code item joins. |
| 2 | `IX_ImportLicence_PendingReport` | Support pending-report status filtering and stable application-date paging. |

The nearby deployed `ImportLicence` index begins with
`(Status, ApplicationDate, ApplyType, ApplicationNo)`. It is not equivalent to
the pending-report index because `ApplyType` interrupts the required paging
order before `ApplicationNo`.

## Production Behavior

The canonical SQL file:

- runs only against `TradeNetDB`;
- verifies the required tables, columns, 22 pagination procedures, and three
  indexed views;
- aborts when online index creation is unsupported;
- sets the ANSI options required by indexed-view environments;
- creates each index independently with `ONLINE = ON` and low-priority waiting;
- prints created and skipped decisions;
- returns installed index definitions for DBA review;
- includes commented rollback statements only.

## Validation Record

The canonical SQL file was executed twice against the configured TradeNet test
connection:

| Pass | Result | Duration |
| --- | --- | ---: |
| First | Created all five indexes online. | 856.028 s |
| Second | Skipped all five indexes through semantic equivalent-index checks. | 0.431 s |

The installed index definitions matched the requested keys and included
columns. No existing index was dropped or rebuilt.

### Warm Paging Benchmark

The `sp_PendingReport_pagination` database path was measured over the full
available application-date span with `PageSize = 10` and
`IncludeTotalCount = false`. Each page was warmed before three timed runs.

| Page | Warm timed runs | Median |
| --- | --- | ---: |
| 0 | 355.587 ms, 274.557 ms, 316.863 ms | 316.863 ms |
| 1 | 218.337 ms, 260.392 ms, 227.785 ms | 227.785 ms |

Both pages remained below the 5 s target. Repeated executions returned stable
rows. The raw wrapper returns one overlapping sentinel row because the fast
path fetches `PageSize + 1`; `ApiResult.CreateFastPageFromRows` removes that
sentinel before returning the API response.

Representative warm query-shape checks for the other new indexes also remained
below 5 s:

| Query shape | Three warm runs | Median |
| --- | --- | ---: |
| PaThaKa permit-business lookup | 14.488 ms, 13.771 ms, 84.522 ms | 14.488 ms |
| Export permit item page | 362.880 ms, 22.612 ms, 25.445 ms | 25.445 ms |
| Border import permit item page | 18.613 ms, 15.314 ms, 15.445 ms | 15.445 ms |
| Border export permit item page | 21.310 ms, 80.777 ms, 17.046 ms | 21.310 ms |

These checks measure the database work used by the report helpers. They do not
include HTTP transport or authentication overhead.

### Logical Reads And Plans

Forced old-path versus new-path comparisons used identical page queries:

| Query shape | Old logical reads | New logical reads |
| --- | ---: | ---: |
| PaThaKa permit-business lookup | 76 | 41 |
| Export permit item page | 1,252 | 904 |
| Border import permit item page | 216 | 154 |
| Border export permit item page | 41 | 30 |
| Pending report, representative one-month range | 76 | 11 |

The pending one-month query improved from 186.393 ms to 39.015 ms. A forced
broad pending query through the nearby old index exceeded a 240 s timeout,
while the normal full-span wrapper path with the new index remained below
400 ms in each warm run.

`SHOWPLAN_XML` review confirmed that unhinted representative queries use each
of the five new indexes. The three permit-item plans use index seeks without a
sort. Old-path versus new-path result comparisons returned zero differing rows
for all five query shapes.

### Backend Tests

The backend project compiled during `dotnet test`. The database-independent
test slice passed:

`323 passed, 0 failed`

The complete suite is not green in this workstation environment for existing
database-fixture reasons:

- seeded smoke tests target a local `TradeNetDBTest` database that is not
  available;
- localdb endpoint smoke tests do not have the pagination procedures deployed;
- the existing `TempSectionValidation` broad section checks exceed their
  command timeout.

No targeted query-shape source fix was required because every affected
database path measured in this rollout remained below the 5 s threshold.

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

## Repeatable Audit

Run the reusable audit from the repository root:

```powershell
./tools/audit-tradenet-report-indexes.ps1
```

The script reads the selected connection string from configuration without
printing credentials. It writes ignored local evidence under
`artifacts/report-index-audit/`, including CSV and JSON snapshots for table
sizes, index definitions and usage, duplicate-key candidates, missing-index
DMVs, indexed views, pagination procedures, runtime stats, controller mappings,
and query-class mappings. The query-class inventory is also exported as a
readable `query-class-inventory.md` snapshot with controllers, tables, request
predicates, static join targets, and ordering columns.

The 2026-06-02 snapshot recorded 229 tables, 665 indexes, 32 duplicate-key
candidate pairs, 169 missing-index DMV suggestions, 3 indexed views, 22
pagination wrappers, and 75 procedure-runtime rows. DMV suggestions remain
evidence inputs; the migration does not drop historical indexes.

## Query-Class Review

The generated inventory contains the detailed controller, table, predicate,
join, and ordering map. This committed summary records every active shared
query class and the controller branches that were scanned:

| Query class | Controller branches | Review action |
| --- | --- | --- |
| `sp_AccountSummaryReport` | AccountSummaryReport | Measured below 5 s; add proactive payment-date cover. |
| `sp_ActualAmendReport` | Border Export Licence; Border Export Permit; Border Import Licence; Border Import Permit; Export Licence; Export Permit; Import Licence; Import Permit | Use Batch 1 item covers; monitor shared base-table indexes. |
| `sp_AmendReport` | Border Export Licence; Border Export Permit; Border Import Licence; Border Import Permit; Export Licence; Export Permit; Import Licence; Import Permit | Use Batch 1 item covers; monitor shared base-table indexes. |
| `sp_BusinessServiceAgencyRegistrationReport` | BusinessServiceAgencyRegistrationByVoucher | Existing voucher coverage; no extra DDL. |
| `sp_BusinessServiceAgencyReport` | BusinessServiceAgencyDetailReport; BusinessServiceAgencySummaryReport | Existing registration coverage; no extra DDL. |
| `sp_CancelReport` | Border Export Licence; Border Export Permit; Border Import Licence; Border Import Permit; Export Licence; Export Permit; Import Licence; Import Permit | Use Batch 1 item covers; monitor shared base-table indexes. |
| `sp_CardListsByPaThaKaReport` | CardListsByCompanyRegistrationNumber | Existing lookup coverage; no extra DDL. |
| `sp_ChequeNoReport` | ChequeNoReport | Existing account coverage; MPU reference cover is available for related lookups. |
| `sp_CompanyProfileReport` | CompanyProfile | Measured below 1 s; add proactive Company Profile cover. |
| `sp_DirectorListReport` | ListOfDirectors; ListOfDirectorsByCompanyRegistrationNo | Existing lookup coverage; no extra DDL. |
| `sp_DutyFreeShopRegistrationReport` | DutyFreeShopRegistrationByVoucher | Existing voucher coverage; no extra DDL. |
| `sp_DutyFreeShopReport` | DutyFreeShopDetailReport; DutyFreeShopSummaryReport | Existing registration coverage; no extra DDL. |
| `sp_EVCycleShowRoomRegistrationReport` | EVCycleShowRoomRegistrationByVoucher | Existing voucher coverage; no extra DDL. |
| `sp_EVCycleShowRoomReport` | EVCycleShowRoomDetailReport; EVCycleShowRoomSummaryReport | Existing registration coverage; no extra DDL. |
| `sp_EVShowRoomRegistrationReport` | EVShowRoomRegistrationByVoucher | Existing voucher coverage; no extra DDL. |
| `sp_EVShowRoomReport` | EVShowRoomDetailReport; EVShowRoomSummaryReport | Existing registration coverage; no extra DDL. |
| `sp_ExportLicenceDetailReport_Fast` | Border Export Licence; Export Licence | Existing licence-item cover; no extra DDL. |
| `sp_ExportPermitDetailReport_Fast` | Border Export Permit; Export Permit | Add Batch 1 permit-item covers. |
| `sp_ExtensionReport` | Border Export Licence; Border Export Permit; Border Import Licence; Border Import Permit; Export Licence; Export Permit; Import Licence; Import Permit | Use Batch 1 item covers; monitor shared base-table indexes. |
| `sp_HSCodeReport` | Border Export Licence; Border Export Permit; Border Import Licence; Border Import Permit; Export Licence; Export Permit; Import Licence; Import Permit | Add Batch 1 permit-item covers; retain existing licence covers. |
| `sp_ImportLicenceDetailReport_Fast` | Border Import Licence; Import Licence | Existing licence-item cover; no duplicate item DDL. |
| `sp_ImportLicencePendingDetailReport` | Import Licence | Add Batch 2 pending index. |
| `sp_ImportLicencePendingDetailReport_Fast` | Border Import Licence | Add proactive border-pending index. |
| `sp_ImportPermitDetailReport_Fast` | Border Import Permit; Import Permit | Existing import-permit item cover; add border permit cover. |
| `sp_MemberRegistrationReport` | MemberRegistrationReport | Existing registration coverage; no extra DDL. |
| `sp_MPUReport` | MPUReport | Measured below 200 ms; add proactive MPU reference cover. |
| `sp_MPUReport_V3` | MPUReportV3 | Measured below 100 ms; retain existing response/date cover. |
| `sp_NewReport` | Border Export Licence; Border Export Permit; Border Import Licence; Border Import Permit; Export Licence; Export Permit; Import Licence; Import Permit | Use Batch 1 item covers; escalate query shape only if a branch exceeds 5 s. |
| `sp_OGARecommendationListReport` | OGARecommendationReport | Existing recommendation coverage; no extra DDL. |
| `sp_OnlineFeesReport` | OnlineFeesReport | Collapse repeated fee scans; add Batch 4 online-fees cover. |
| `sp_PaThaKaAllReport` | ListOfCompany | Existing registration coverage; no extra DDL. |
| `sp_PathakaBindReport` | EIRCardBindReport | Existing PaThaKa coverage; no extra DDL. |
| `sp_PaThaKaByBusinessTypeReport` | RegistrationByBusinessType | Existing PaThaKa coverage; no extra DDL. |
| `sp_PaThaKaRegistrationReport` | RegistrationByVoucher | Existing PaThaKa coverage; no extra DDL. |
| `sp_PaThaKaReport` | ListOfTopCapitalCompany; PaThaKaRegisteredBusinessOrganizationReport | Existing PaThaKa coverage; no extra DDL. |
| `sp_PaThaKaValidInvalidReport` | ListOfValidAndInvalidCompany | Existing PaThaKa coverage; no extra DDL. |
| `sp_PendingReport` | Border Import Licence; Import Licence | Add Batch 2 import and proactive border pending covers. |
| `sp_ReExportReport` | ReExportDetailReport; ReExportSummaryReport | Existing registration coverage; no extra DDL. |
| `sp_SaleCenterRegistrationReport` | SaleCenterRegistrationByVoucher | Existing voucher coverage; no extra DDL. |
| `sp_SaleCenterReport` | SaleCenterDetailReport; SaleCenterSummaryReport | Existing registration coverage; no extra DDL. |
| `sp_ShowRoomRegistrationReport` | ShowRoomRegistrationByVoucher | Existing voucher coverage; no extra DDL. |
| `sp_ShowRoomReport` | ShowRoomDetailReport; ShowRoomSummaryReport | Existing registration coverage; no extra DDL. |
| `sp_VoucherReport` | Border Export Licence; Border Export Permit; Border Import Licence; Border Import Permit; Export Licence; Export Permit; Import Licence; Import Permit | Use Batch 1 item covers; monitor shared base-table indexes. |
| `sp_WholeSaleRetailRegistrationReport` | RetailRegistrationByVoucher; WholeSaleAndRetailRegistrationByVoucher; WholeSaleRegistrationByVoucher | Existing voucher coverage; no extra DDL. |
| `sp_WholeSaleRetailReport` | RetailDetailReport; RetailSummaryReport; WholeSaleAndRetailDetailReport; WholeSaleAndRetailSummaryReport; WholeSaleDetailReport; WholeSaleSummaryReport | Existing registration coverage; no extra DDL. |
| `sp_WineImportationRegistrationReport_Fast` | AlcoholicBeveragesImportationRegistrationByVoucher | Existing voucher coverage; no extra DDL. |
| `sp_WineImportationReport_Fast` | AlcoholicBeveragesImportationDetailReport; AlcoholicBeveragesImportationSummaryReport | Existing registration coverage; no extra DDL. |

`ImportLicenceBySectionReportController` is the one custom direct-query
controller outside the 47 shared query classes. It is included in the generated
controller map.

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
| 3 | `IX_BorderImportLicence_PendingReport` | Add forward capacity for border pending-report paging. |
| 3 | `IX_AccountTransaction_PaymentReport` | Add the payment-date access order requested for future payment-report growth. |
| 3 | `IX_MPUPaymentTransaction_TransactionRefNo` | Support MPU transaction-reference lookups. |
| 3 | `IX_PaThaKaRegistration_CompanyProfile` | Add a Company Profile lookup path by registration number, apply type, and status. |
| 4 | `IX_AccountTransaction_OnlineFees` | Cover the online-fees transaction scan after its query-shape fix. |

The nearby deployed `ImportLicence` index begins with
`(Status, ApplicationDate, ApplyType, ApplicationNo)`. It is not equivalent to
the pending-report index because `ApplyType` interrupts the required paging
order before `ApplicationNo`.

The first five indexes were validated and installed through the configured
test connection. The five Batch 3 and Batch 4 additions are proactive,
manual-rollout candidates requested for future growth. They remain protected
by the same semantic left-prefix covering checks and have not been applied
automatically by this repository change.

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
connection before the proactive Batch 3 and Batch 4 additions:

| Pass | Result | Duration |
| --- | --- | ---: |
| First | Created all five indexes online. | 856.028 s |
| Second | Skipped all five indexes through semantic equivalent-index checks. | 0.431 s |

The installed index definitions matched the requested keys and included
columns. No existing index was dropped or rebuilt.

The expanded ten-index script was syntax-checked as seven SQL batches without
executing its new DDL. Production application remains a manual DBA action.

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

### Proactive Candidate Benchmark

The Batch 3 paths were measured over a representative May 2026 window with
`PageSize = 10`, `IncludeTotalCount = false`, and three warm timed runs. They
already passed before the proactive indexes are installed:

| Query shape | Warm median |
| --- | ---: |
| Account Summary, page 0 | 2,156.556 ms |
| Account Summary, page 1 | 2,081.537 ms |
| MPU, page 0 | 116.090 ms |
| MPU, page 1 | 115.051 ms |
| MPU V3, page 0 | 56.746 ms |
| Company Profile, page 0 | 649.792 ms |
| Border pending base page, page 0 | 16.429 ms |

These indexes are therefore capacity additions, not claims that every current
plan requires a new index.

### Online Fees Query Fix

The configured reflection smoke initially timed out after 30 seconds in
`OnlineFeesReportController`. The LINQ helper repeated the same account-fee
scan in every registration union branch. It now builds one registration union
and joins the account-fee rows once.

On the existing catalog, before installing `IX_AccountTransaction_OnlineFees`,
the isolated POST smoke measured 2,069.738 ms, 2,067.095 ms, and 2,014.061 ms
across three warm runs: a 2,067.095 ms median.

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

The configuration-aware endpoint reflection smoke passed against the selected
TradeNet connection:

`159 passed, 0 failed` (`158` report POST endpoints plus one availability check)

The local controller payload/default slice also passed:

`267 passed, 0 failed`

The complete suite is still not claimed green in the default workstation
environment for existing database-fixture reasons:

- seeded smoke tests target a local `TradeNetDBTest` database unless
  `TRADENET_REPORT_TEST_CONNECTION_STRING` is supplied;
- localdb endpoint smoke tests do not have the pagination procedures deployed;
- broad section checks are tracked separately from the bounded POST smoke.

The test helper now also accepts `TRADENET_REPORT_TEST_FROM_DATE` and
`TRADENET_REPORT_TEST_TO_DATE`. A previously embedded shared-database
credential in `TempSectionValidation` was removed; shared-database checks now
require the environment variable.

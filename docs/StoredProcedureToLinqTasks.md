# Stored Procedure To LINQ Tasks

## Verified Request

- Use the local SQL Server database named `TradeNetDBTest`.
- Source stored procedures come from `TradeNetDBTest`.
- Target EF models are the scaffolded classes under `Backend/Model/TradeNet`.
- Target DbContext is `Backend/DBContext/TradeNetDbContext.cs`.
- Create `Backend/StoredProcedureToLinq` if it does not already exist.
- Create one C# file per stored procedure, named with the stored procedure name, for example `sp_ActualAmendReport.cs`.
- Each converted LINQ file must expose an `IQueryable` query. Do not materialize with `ToList`, `AsEnumerable`, or other execution calls inside the converter.
- If a stored procedure has parameters, add a request/filter class in the same LINQ file with C# properties matching the parameters, for example `FormType string`, `FromDate DateTime`, `ToDate DateTime`, and so on.
- Use LINQ over `TradeNetDbContext` DbSets and the `Backend/Model/TradeNet` entity classes.

## Local Verification

- `docs/` already exists.
- `Backend/StoredProcedureToLinq/` did not exist and must be created for this work.
- `Backend/appsettings.json` already contains a `TradeNetDBTest` connection string pointing to `Server=localhost;Initial Catalog=TradeNetDBTest`.
- Local SQL access is available through `sqlcmd`.
- `TradeNetDBTest` currently has 108 non-system stored procedures.
- Initial name-based classification:
  - 83 likely query/report/list/search/dashboard procedures.
  - 24 likely non-`IQueryable` helper or mutation procedures.
  - 1 manual-review procedure: `sp_ApplicationHistory`.

## Conversion Progress Tracker

Last refreshed from `TradeNetDBTest`: 2026-05-27.

Status values:

- `Converted`: LINQ file exists and compiles.
- `In Process`: LINQ file is being worked on.
- `To Do`: likely query/report stored procedure that still needs a LINQ file.
- `Manual Review`: inspect SQL definition before deciding if pure `IQueryable` is possible.
- `Not IQueryable`: helper/mutation/session-state procedure; do not create a LINQ file unless query-only behavior is identified.

Progress counts:

- Database procedures discovered: 108.
- Likely LINQ files to create: 83.
- Converted: 1.
- In process: 0.
- To do: 82.
- Manual review: 1.
- Not `IQueryable` / excluded for now: 24.

| Stored procedure | Target `.cs` file | Status | Note |
| --- | --- | --- | --- |
| CreateTempTables | CreateTempTables.cs | Not IQueryable | Temp/session helper; no query-only LINQ file planned. |
| DeleteExpiredSessions | DeleteExpiredSessions.cs | Not IQueryable | Session cleanup mutation; no query-only LINQ file planned. |
| GetHashCode | GetHashCode.cs | Not IQueryable | ASP.NET session helper/output procedure; no query-only LINQ file planned. |
| GetMajorVersion | GetMajorVersion.cs | Not IQueryable | Helper/output procedure; no query-only LINQ file planned. |
| GetRequestAutoApproveDescriptions | GetRequestAutoApproveDescriptions.cs | To Do | Likely query procedure. |
| GetRequestAutoApproveDescriptionsImport | GetRequestAutoApproveDescriptionsImport.cs | To Do | Likely query procedure. |
| GetRequestById | GetRequestById.cs | To Do | Likely query procedure. |
| GetRequestByIdImport | GetRequestByIdImport.cs | To Do | Likely query procedure. |
| sp_AccountSummaryReport | sp_AccountSummaryReport.cs | To Do | Report procedure. |
| sp_ActualAmendReport | sp_ActualAmendReport.cs | Converted | LINQ conversion exists in `Backend/StoredProcedureToLinq/sp_ActualAmendReport.cs`. |
| sp_AmendReport | sp_AmendReport.cs | To Do | Report procedure. |
| sp_ApplicationHistory | sp_ApplicationHistory.cs | Manual Review | Not matched by report/list/search naming; inspect SQL definition first. |
| sp_AutoCancelDataList | sp_AutoCancelDataList.cs | To Do | List/query procedure. |
| sp_BusinessServiceAgencyByPaThakaReport | sp_BusinessServiceAgencyByPaThakaReport.cs | To Do | Report procedure. |
| sp_BusinessServiceAgencyRegistrationReport | sp_BusinessServiceAgencyRegistrationReport.cs | To Do | Report procedure. |
| sp_BusinessServiceAgencyReport | sp_BusinessServiceAgencyReport.cs | To Do | Report procedure. |
| sp_CancelReport | sp_CancelReport.cs | To Do | Report procedure. |
| sp_CardListsByPaThaKaReport | sp_CardListsByPaThaKaReport.cs | To Do | Report/list procedure. |
| sp_ChequeNoDetailReport | sp_ChequeNoDetailReport.cs | To Do | Report procedure. |
| sp_ChequeNoReport | sp_ChequeNoReport.cs | To Do | Report procedure. |
| sp_CompanyProfileReport | sp_CompanyProfileReport.cs | To Do | Report procedure. |
| sp_DashboardCompleted | sp_DashboardCompleted.cs | To Do | Dashboard query procedure. |
| sp_DashboardFeedback | sp_DashboardFeedback.cs | To Do | Dashboard query procedure. |
| sp_DashboardPayment | sp_DashboardPayment.cs | To Do | Dashboard query procedure. |
| sp_DashboardProgress | sp_DashboardProgress.cs | To Do | Dashboard query procedure. |
| sp_DirectorByPaThaKaReport | sp_DirectorByPaThaKaReport.cs | To Do | Report procedure. |
| sp_DirectorListReport | sp_DirectorListReport.cs | To Do | Report/list procedure. |
| sp_DutyFreeShopByReport | sp_DutyFreeShopByReport.cs | To Do | Report procedure. |
| sp_DutyFreeShopRegistrationReport | sp_DutyFreeShopRegistrationReport.cs | To Do | Report procedure. |
| sp_DutyFreeShopReport | sp_DutyFreeShopReport.cs | To Do | Report procedure. |
| sp_EICCBalanceCertificateList | sp_EICCBalanceCertificateList.cs | To Do | List/query procedure. |
| sp_EICCPendingCertificateList | sp_EICCPendingCertificateList.cs | To Do | List/query procedure. |
| sp_EICCReport | sp_EICCReport.cs | To Do | Report procedure. |
| sp_EICCSubmitBorderLicencePermitList | sp_EICCSubmitBorderLicencePermitList.cs | To Do | List/query procedure. |
| sp_EICCSubmitCertificateList | sp_EICCSubmitCertificateList.cs | To Do | List/query procedure. |
| sp_EICCSubmitLicencePermitList | sp_EICCSubmitLicencePermitList.cs | To Do | List/query procedure. |
| sp_EVCycleShowRoomRegistrationReport | sp_EVCycleShowRoomRegistrationReport.cs | To Do | Report procedure. |
| sp_EVCycleShowRoomReport | sp_EVCycleShowRoomReport.cs | To Do | Report procedure. |
| sp_EVShowRoomRegistrationReport | sp_EVShowRoomRegistrationReport.cs | To Do | Report procedure. |
| sp_EVShowRoomReport | sp_EVShowRoomReport.cs | To Do | Report procedure. |
| sp_ExportLicenceDetailReport | sp_ExportLicenceDetailReport.cs | To Do | Report procedure. |
| sp_ExportPermitDetailReport | sp_ExportPermitDetailReport.cs | To Do | Report procedure. |
| sp_ExtensionReport | sp_ExtensionReport.cs | To Do | Report procedure. |
| sp_GetChekApproveNotiList | sp_GetChekApproveNotiList.cs | To Do | List/query procedure. |
| sp_HSCodeReport | sp_HSCodeReport.cs | To Do | Report procedure. |
| sp_HSCodeSearch | sp_HSCodeSearch.cs | To Do | Search/query procedure. |
| sp_ImportLicenceDaily_Detail_Report | sp_ImportLicenceDaily_Detail_Report.cs | To Do | Report procedure. |
| sp_ImportLicenceDetailReport | sp_ImportLicenceDetailReport.cs | To Do | Report procedure. |
| sp_ImportLicencePendingDetailReport | sp_ImportLicencePendingDetailReport.cs | To Do | Report procedure. |
| sp_ImportPermitDetailReport | sp_ImportPermitDetailReport.cs | To Do | Report procedure. |
| sp_LicencePermitSearch | sp_LicencePermitSearch.cs | To Do | Search/query procedure. |
| sp_LicencePermitSearch_old | sp_LicencePermitSearch_old.cs | To Do | Search/query procedure; legacy version. |
| sp_MemberRegistrationReport | sp_MemberRegistrationReport.cs | To Do | Report procedure. |
| sp_MPUReport | sp_MPUReport.cs | To Do | Report procedure. |
| sp_MPUReport_Seperated_OnineFee | sp_MPUReport_Seperated_OnineFee.cs | To Do | Report procedure; preserve original spelling. |
| sp_MPUReport_V3 | sp_MPUReport_V3.cs | To Do | Report procedure. |
| sp_MPUReportV2 | sp_MPUReportV2.cs | To Do | Report procedure. |
| sp_NewReport | sp_NewReport.cs | To Do | Report procedure. |
| sp_NewReport_old | sp_NewReport_old.cs | To Do | Report procedure; legacy version. |
| sp_NotificationDataList | sp_NotificationDataList.cs | To Do | List/query procedure. |
| sp_OGARecommendationHistoryReport | sp_OGARecommendationHistoryReport.cs | To Do | Report procedure. |
| sp_OGARecommendationListReport | sp_OGARecommendationListReport.cs | To Do | Report/list procedure. |
| sp_OGARecommendationReport | sp_OGARecommendationReport.cs | To Do | Report procedure. |
| sp_OnlineFeesReport | sp_OnlineFeesReport.cs | To Do | Report procedure. |
| sp_PaThaKaAllReport | sp_PaThaKaAllReport.cs | To Do | Report procedure. |
| sp_PathakaBindReport | sp_PathakaBindReport.cs | To Do | Report procedure. |
| sp_PaThaKaByBusinessTypeReport | sp_PaThaKaByBusinessTypeReport.cs | To Do | Report procedure. |
| sp_PaThaKaRegistrationReport | sp_PaThaKaRegistrationReport.cs | To Do | Report procedure. |
| sp_PaThaKaReport | sp_PaThaKaReport.cs | To Do | Report procedure. |
| sp_PaThaKaValidInvalidReport | sp_PaThaKaValidInvalidReport.cs | To Do | Report procedure. |
| sp_PendingReport | sp_PendingReport.cs | To Do | Report procedure. |
| sp_PermitBusinessByPaThaKaReport | sp_PermitBusinessByPaThaKaReport.cs | To Do | Report procedure. |
| sp_ReExportByPaThaKaReport | sp_ReExportByPaThaKaReport.cs | To Do | Report procedure. |
| sp_ReExportReport | sp_ReExportReport.cs | To Do | Report procedure. |
| sp_SaleCenterByPaThaKaReport | sp_SaleCenterByPaThaKaReport.cs | To Do | Report procedure. |
| sp_SaleCenterRegistrationReport | sp_SaleCenterRegistrationReport.cs | To Do | Report procedure. |
| sp_SaleCenterReport | sp_SaleCenterReport.cs | To Do | Report procedure. |
| sp_ShowRoomByPaThaKaReport | sp_ShowRoomByPaThaKaReport.cs | To Do | Report procedure. |
| sp_ShowRoomRegistrationReport | sp_ShowRoomRegistrationReport.cs | To Do | Report procedure. |
| sp_ShowRoomReport | sp_ShowRoomReport.cs | To Do | Report procedure. |
| sp_TestReport | sp_TestReport.cs | To Do | Report procedure; inspect before prioritizing. |
| sp_VoucherReport | sp_VoucherReport.cs | To Do | Report procedure. |
| sp_WholeSaleAndRetailByPaThaKaReport | sp_WholeSaleAndRetailByPaThaKaReport.cs | To Do | Report procedure. |
| sp_WholeSaleRetailRegistrationReport | sp_WholeSaleRetailRegistrationReport.cs | To Do | Report procedure. |
| sp_WholeSaleRetailReport | sp_WholeSaleRetailReport.cs | To Do | Report procedure. |
| sp_WineImportationByPaThaKaReport | sp_WineImportationByPaThaKaReport.cs | To Do | Report procedure. |
| sp_WineImportationRegistrationReport | sp_WineImportationRegistrationReport.cs | To Do | Report procedure. |
| sp_WineImportationReport | sp_WineImportationReport.cs | To Do | Report procedure. |
| TempGetAppID | TempGetAppID.cs | Not IQueryable | ASP.NET session-state helper/output procedure. |
| TempGetStateItem | TempGetStateItem.cs | Not IQueryable | ASP.NET session-state helper/output procedure. |
| TempGetStateItem2 | TempGetStateItem2.cs | Not IQueryable | ASP.NET session-state helper/output procedure. |
| TempGetStateItem3 | TempGetStateItem3.cs | Not IQueryable | ASP.NET session-state helper/output procedure. |
| TempGetStateItemExclusive | TempGetStateItemExclusive.cs | Not IQueryable | ASP.NET session-state helper/output procedure. |
| TempGetStateItemExclusive2 | TempGetStateItemExclusive2.cs | Not IQueryable | ASP.NET session-state helper/output procedure. |
| TempGetStateItemExclusive3 | TempGetStateItemExclusive3.cs | Not IQueryable | ASP.NET session-state helper/output procedure. |
| TempGetVersion | TempGetVersion.cs | Not IQueryable | ASP.NET session-state helper/output procedure. |
| TempInsertStateItemLong | TempInsertStateItemLong.cs | Not IQueryable | ASP.NET session-state insert mutation. |
| TempInsertStateItemShort | TempInsertStateItemShort.cs | Not IQueryable | ASP.NET session-state insert mutation. |
| TempInsertUninitializedItem | TempInsertUninitializedItem.cs | Not IQueryable | ASP.NET session-state insert mutation. |
| TempReleaseStateItemExclusive | TempReleaseStateItemExclusive.cs | Not IQueryable | ASP.NET session-state update mutation. |
| TempRemoveStateItem | TempRemoveStateItem.cs | Not IQueryable | ASP.NET session-state delete mutation. |
| TempResetTimeout | TempResetTimeout.cs | Not IQueryable | ASP.NET session-state update mutation. |
| TempUpdateStateItemLong | TempUpdateStateItemLong.cs | Not IQueryable | ASP.NET session-state update mutation. |
| TempUpdateStateItemLongNullShort | TempUpdateStateItemLongNullShort.cs | Not IQueryable | ASP.NET session-state update mutation. |
| TempUpdateStateItemShort | TempUpdateStateItemShort.cs | Not IQueryable | ASP.NET session-state update mutation. |
| TempUpdateStateItemShortNullLong | TempUpdateStateItemShortNullLong.cs | Not IQueryable | ASP.NET session-state update mutation. |
| UpdateAdditionalDescription | UpdateAdditionalDescription.cs | Not IQueryable | Data update mutation; no query-only LINQ file planned. |
| UpdateAdditionalDescriptionImport | UpdateAdditionalDescriptionImport.cs | Not IQueryable | Data update mutation; no query-only LINQ file planned. |

## Conversion Rules

- Return type must be `IQueryable<T>`.
- Procedure filter inputs should be represented as a parameter class named `{StoredProcedureName}Request`.
- Result projection classes should be named `{StoredProcedureName}Result` when the procedure returns an anonymous/report shape that does not match an existing entity.
- Query builder classes should be named exactly like the stored procedure name when valid in C#, for example `sp_ActualAmendReport`.
- Query methods should accept `TradeNetDbContext db` and the request class, then return `IQueryable<{StoredProcedureName}Result>` or `IQueryable<TEntity>`.
- Keep SQL semantics intact: joins, left joins, unions, grouping, date filters, status filters, string filters, and sort order must match the procedure.
- Keep all query logic deferred. Any count, sum, group, select, join, concat, or union must remain in the returned query expression.
- Do not use `FromSql`, `ExecuteSql`, raw SQL strings, or stored procedure calls in the converted LINQ.
- Do not convert procedures that mutate data, manage ASP.NET session state, write temp tables without a final query shape, or depend on procedural side effects unless they are first split into a query-only equivalent.

## Batch Guidance For LLM

- Recommended default batch size: convert 3 stored procedures per work session.
- Simple list/search/report procedures can be converted in batches of 5-10 if their SQL is short and has no branching, temp tables, dynamic SQL, unions, or grouping.
- Medium reports with multiple joins, optional filters, grouping, or report projections should be converted in batches of 3-5.
- Large procedures with many `IF` branches, `UNION`/`UNION ALL`, repeated projections, or different result shapes should be converted in batches of 1-2.
- After every batch:
  - Run a build or compile validation.
  - Update the tracker statuses and counts in this document.
  - Add notes for any procedure that needs manual review, cannot be represented as pure `IQueryable`, or has a behavior difference from SQL.
- Prefer smaller batches when unsure. Correctness is more important than converting many files at once.

## Procedure Classification Tasks

- [x] Connect to `TradeNetDBTest` and count stored procedures.
- [x] Read procedure names and parameter metadata.
- [x] Run an initial name-based classification pass.
- [ ] Export procedure definitions for review.
- [ ] Classify each procedure as:
  - Query/report procedure suitable for `IQueryable`.
  - Query procedure requiring manual review because it uses temp tables, dynamic SQL, cursors, or procedural branching.
  - Non-query procedure that should not be converted to `IQueryable`.
- [ ] Prioritize `sp_*Report`, list/search, dashboard, and request lookup procedures first.
- [ ] Exclude or document ASP.NET session state procedures such as `TempGetStateItem*`, `TempInsertStateItem*`, `TempUpdateStateItem*`, and similar non-report procedures.

## Implementation Tasks

- [x] Create `Backend/StoredProcedureToLinq/`.
- [x] Convert `sp_ActualAmendReport` into `Backend/StoredProcedureToLinq/sp_ActualAmendReport.cs`.
- [x] Add `sp_ActualAmendReportRequest` with the stored procedure parameters.
- [x] Add `sp_ActualAmendReportResult` for the report projection.
- [ ] Create one `.cs` file for each convertible stored procedure.
- [ ] Add request classes for procedures with parameters.
- [ ] Add result classes for report projections that do not map directly to an EF entity.
- [ ] Convert SQL joins into LINQ joins or navigation-property queries.
- [ ] Convert optional filters such as empty strings, zero ids, and null dates according to each stored procedure's SQL logic.
- [ ] Convert SQL `CASE` expressions into conditional projections.
- [ ] Convert `UNION` and `UNION ALL` into `Union` or `Concat` as appropriate.
- [ ] Preserve sorting with `OrderBy`, `ThenBy`, `OrderByDescending`, or `ThenByDescending`.
- [ ] Add short comments only where a SQL behavior is non-obvious.

## Validation Tasks

- [x] Run `dotnet build Backend/API.csproj` against a separate output directory because a local `API` process is locking the normal `bin` output.
- [ ] Compare generated SQL for representative LINQ queries with stored procedure logic using `ToQueryString()` where useful.
- [ ] For selected procedures, compare row counts and key result columns against the original stored procedure using the same parameters.
- [ ] Fix any mismatched null handling, string trimming, date range behavior, or join cardinality.
- [ ] Keep a list of procedures that cannot be represented as pure `IQueryable` without changing behavior.

## Current Notes

- The database includes both business/report procedures and ASP.NET session-state style procedures. The latter are not valid `IQueryable` conversions because they update or manage state.
- Some stored procedures may return columns that are not represented by existing EF entities. Those need dedicated result projection classes in their own procedure file.
- `IQueryable` output means the caller is responsible for execution, paging, and materialization.
- `sp_ActualAmendReport` returns different column counts by form type in SQL. The LINQ conversion uses one superset result class with nullable Sakhan fields for a stable typed `IQueryable` result.
- Normal build output is currently locked by process `API (8076)`. Verification build succeeded with `dotnet build Backend\API.csproj -p:OutputPath=C:\Code\Ministry_of_Commerce_Tradenet_build_verify\API\`.

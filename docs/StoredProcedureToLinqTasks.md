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
- Converted: 55.
- In process: 0.
- To do: 28.
- Manual review: 1.
- Not `IQueryable` / excluded for now: 24.

| Stored procedure | Target `.cs` file | Status | Note |
| --- | --- | --- | --- |
| CreateTempTables | CreateTempTables.cs | Not IQueryable | Temp/session helper; no query-only LINQ file planned. |
| DeleteExpiredSessions | DeleteExpiredSessions.cs | Not IQueryable | Session cleanup mutation; no query-only LINQ file planned. |
| GetHashCode | GetHashCode.cs | Not IQueryable | ASP.NET session helper/output procedure; no query-only LINQ file planned. |
| GetMajorVersion | GetMajorVersion.cs | Not IQueryable | Helper/output procedure; no query-only LINQ file planned. |
| GetRequestAutoApproveDescriptions | GetRequestAutoApproveDescriptions.cs | Converted | Entity-shaped LINQ conversion exists. |
| GetRequestAutoApproveDescriptionsImport | GetRequestAutoApproveDescriptionsImport.cs | Converted | Entity-shaped LINQ conversion exists. |
| GetRequestById | GetRequestById.cs | Converted | Entity-shaped LINQ conversion exists; SQL `TOP 1` represented with `Take(1)`. |
| GetRequestByIdImport | GetRequestByIdImport.cs | Converted | Entity-shaped LINQ conversion exists; SQL `TOP 1` represented with `Take(1)`. |
| sp_AccountSummaryReport | sp_AccountSummaryReport.cs | Converted | LINQ conversion exists; large `UNION ALL` report represented with `Concat` branches. |
| sp_ActualAmendReport | sp_ActualAmendReport.cs | Converted | LINQ conversion exists in `Backend/StoredProcedureToLinq/sp_ActualAmendReport.cs`. |
| sp_AmendReport | sp_AmendReport.cs | Converted | LINQ conversion exists; one stored procedure Sakhan filter oddity is preserved in the border export licence Pa Tha Ka branch. |
| sp_ApplicationHistory | sp_ApplicationHistory.cs | Manual Review | Not matched by report/list/search naming; inspect SQL definition first. |
| sp_AutoCancelDataList | sp_AutoCancelDataList.cs | Converted | LINQ conversion exists; SQL `DATEDIFF(day, ApproveDate, CURRENT_TIMESTAMP)` represented with `EF.Functions.DateDiffDay`. |
| sp_BusinessServiceAgencyByPaThakaReport | sp_BusinessServiceAgencyByPaThakaReport.cs | Converted | LINQ conversion exists. |
| sp_BusinessServiceAgencyRegistrationReport | sp_BusinessServiceAgencyRegistrationReport.cs | Converted | LINQ conversion exists. |
| sp_BusinessServiceAgencyReport | sp_BusinessServiceAgencyReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
| sp_CancelReport | sp_CancelReport.cs | Converted | LINQ conversion exists; large branch-heavy cancellation report represented with typed result projection. |
| sp_CardListsByPaThaKaReport | sp_CardListsByPaThaKaReport.cs | Converted | LINQ conversion exists. |
| sp_ChequeNoDetailReport | sp_ChequeNoDetailReport.cs | To Do | Report procedure. |
| sp_ChequeNoReport | sp_ChequeNoReport.cs | Converted | LINQ conversion exists. |
| sp_CompanyProfileReport | sp_CompanyProfileReport.cs | Converted | LINQ conversion exists; SQL scalar function `fn_GetPermitBusiness` represented with a LINQ string aggregation. |
| sp_DashboardCompleted | sp_DashboardCompleted.cs | Converted | LINQ conversion exists; preserves SQL month/year filtering on nullable `CreatedDate`. |
| sp_DashboardFeedback | sp_DashboardFeedback.cs | Converted | LINQ conversion exists; dashboard counts are grouped with `Concat` branches matching SQL `UNION ALL`. |
| sp_DashboardPayment | sp_DashboardPayment.cs | Converted | LINQ conversion exists; dashboard counts are grouped with `Concat` branches matching SQL `UNION ALL`. |
| sp_DashboardProgress | sp_DashboardProgress.cs | Converted | LINQ conversion exists; dashboard counts are grouped with `Concat` branches matching SQL `UNION ALL`. |
| sp_DirectorByPaThaKaReport | sp_DirectorByPaThaKaReport.cs | Converted | LINQ conversion exists. |
| sp_DirectorListReport | sp_DirectorListReport.cs | Converted | LINQ conversion exists; preserves the stored procedure's `CASE` filter behavior for company registration in the list branch. |
| sp_DutyFreeShopByReport | sp_DutyFreeShopByReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` expanded with prefix joins. |
| sp_DutyFreeShopRegistrationReport | sp_DutyFreeShopRegistrationReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` expanded with prefix joins. |
| sp_DutyFreeShopReport | sp_DutyFreeShopReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
| sp_EICCBalanceCertificateList | sp_EICCBalanceCertificateList.cs | To Do | List/query procedure. |
| sp_EICCPendingCertificateList | sp_EICCPendingCertificateList.cs | To Do | List/query procedure. |
| sp_EICCReport | sp_EICCReport.cs | To Do | Report procedure. |
| sp_EICCSubmitBorderLicencePermitList | sp_EICCSubmitBorderLicencePermitList.cs | To Do | List/query procedure. |
| sp_EICCSubmitCertificateList | sp_EICCSubmitCertificateList.cs | To Do | List/query procedure. |
| sp_EICCSubmitLicencePermitList | sp_EICCSubmitLicencePermitList.cs | To Do | List/query procedure. |
| sp_EVCycleShowRoomRegistrationReport | sp_EVCycleShowRoomRegistrationReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` and business-service-agency lookup expanded with LINQ. |
| sp_EVCycleShowRoomReport | sp_EVCycleShowRoomReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
| sp_EVShowRoomRegistrationReport | sp_EVShowRoomRegistrationReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` and business-service-agency lookup expanded with LINQ. |
| sp_EVShowRoomReport | sp_EVShowRoomReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
| sp_ExportLicenceDetailReport | sp_ExportLicenceDetailReport.cs | To Do | Report procedure. |
| sp_ExportPermitDetailReport | sp_ExportPermitDetailReport.cs | To Do | Report procedure. |
| sp_ExtensionReport | sp_ExtensionReport.cs | To Do | Report procedure. |
| sp_GetChekApproveNotiList | sp_GetChekApproveNotiList.cs | Converted | LINQ conversion exists; branch-specific notification filters preserved. |
| sp_HSCodeReport | sp_HSCodeReport.cs | To Do | Report procedure. |
| sp_HSCodeSearch | sp_HSCodeSearch.cs | Converted | LINQ conversion exists; preserves stored procedure's literal export-section LIKE pattern. |
| sp_ImportLicenceDaily_Detail_Report | sp_ImportLicenceDaily_Detail_Report.cs | To Do | Report procedure. |
| sp_ImportLicenceDetailReport | sp_ImportLicenceDetailReport.cs | To Do | Report procedure. |
| sp_ImportLicencePendingDetailReport | sp_ImportLicencePendingDetailReport.cs | To Do | Report procedure. |
| sp_ImportPermitDetailReport | sp_ImportPermitDetailReport.cs | To Do | Report procedure. |
| sp_LicencePermitSearch | sp_LicencePermitSearch.cs | Converted | LINQ conversion exists; SQL `UNION ALL`, `ORDER BY CreatedDate DESC`, and `TOP 1` represented with `Concat`, `OrderByDescending`, and `Take(1)`. |
| sp_LicencePermitSearch_old | sp_LicencePermitSearch_old.cs | Converted | LINQ conversion exists; delegates to current search conversion because SQL differs only by index hints. |
| sp_MemberRegistrationReport | sp_MemberRegistrationReport.cs | Converted | LINQ conversion exists; preserves different `IssuedDate` projection behavior in `All` vs `Extension` branches. |
| sp_MPUReport | sp_MPUReport.cs | To Do | Report procedure. |
| sp_MPUReport_Seperated_OnineFee | sp_MPUReport_Seperated_OnineFee.cs | To Do | Report procedure; preserve original spelling. |
| sp_MPUReport_V3 | sp_MPUReport_V3.cs | To Do | Report procedure. |
| sp_MPUReportV2 | sp_MPUReportV2.cs | To Do | Report procedure. |
| sp_NewReport | sp_NewReport.cs | To Do | Report procedure. |
| sp_NewReport_old | sp_NewReport_old.cs | To Do | Report procedure; legacy version. |
| sp_NotificationDataList | sp_NotificationDataList.cs | Converted | LINQ conversion exists; SQL date-warning logic represented with `EF.Functions.DateDiffDay`. |
| sp_OGARecommendationHistoryReport | sp_OGARecommendationHistoryReport.cs | To Do | Report procedure. |
| sp_OGARecommendationListReport | sp_OGARecommendationListReport.cs | To Do | Report/list procedure. |
| sp_OGARecommendationReport | sp_OGARecommendationReport.cs | To Do | Report procedure. |
| sp_OnlineFeesReport | sp_OnlineFeesReport.cs | To Do | Report procedure. |
| sp_PaThaKaAllReport | sp_PaThaKaAllReport.cs | Converted | LINQ conversion exists; owner `fn_GetNRCNo` expanded with prefix joins. |
| sp_PathakaBindReport | sp_PathakaBindReport.cs | Converted | LINQ conversion exists. |
| sp_PaThaKaByBusinessTypeReport | sp_PaThaKaByBusinessTypeReport.cs | Converted | LINQ conversion exists. |
| sp_PaThaKaRegistrationReport | sp_PaThaKaRegistrationReport.cs | Converted | LINQ conversion exists. |
| sp_PaThaKaReport | sp_PaThaKaReport.cs | Converted | LINQ conversion exists. |
| sp_PaThaKaValidInvalidReport | sp_PaThaKaValidInvalidReport.cs | Converted | LINQ conversion exists. |
| sp_PendingReport | sp_PendingReport.cs | To Do | Report procedure. |
| sp_PermitBusinessByPaThaKaReport | sp_PermitBusinessByPaThaKaReport.cs | Converted | LINQ conversion exists; SQL full joins represented with PaThaKa-rooted left joins. |
| sp_ReExportByPaThaKaReport | sp_ReExportByPaThaKaReport.cs | Converted | LINQ conversion exists; preserves SQL address alias behavior. |
| sp_ReExportReport | sp_ReExportReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
| sp_SaleCenterByPaThaKaReport | sp_SaleCenterByPaThaKaReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` and business-service-agency lookup expanded with LINQ. |
| sp_SaleCenterRegistrationReport | sp_SaleCenterRegistrationReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` and business-service-agency lookup expanded with LINQ. |
| sp_SaleCenterReport | sp_SaleCenterReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
| sp_ShowRoomByPaThaKaReport | sp_ShowRoomByPaThaKaReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` and business-service-agency lookup expanded with LINQ. |
| sp_ShowRoomRegistrationReport | sp_ShowRoomRegistrationReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` and business-service-agency lookup expanded with LINQ. |
| sp_ShowRoomReport | sp_ShowRoomReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
| sp_TestReport | sp_TestReport.cs | To Do | Report procedure; inspect before prioritizing. |
| sp_VoucherReport | sp_VoucherReport.cs | To Do | Report procedure. |
| sp_WholeSaleAndRetailByPaThaKaReport | sp_WholeSaleAndRetailByPaThaKaReport.cs | Converted | LINQ conversion exists. |
| sp_WholeSaleRetailRegistrationReport | sp_WholeSaleRetailRegistrationReport.cs | Converted | LINQ conversion exists; SQL `UNION ALL` represented with `Concat`. |
| sp_WholeSaleRetailReport | sp_WholeSaleRetailReport.cs | Converted | LINQ conversion exists; preserves SQL output alias spelling `WholeSaleRetailostalCode`. |
| sp_WineImportationByPaThaKaReport | sp_WineImportationByPaThaKaReport.cs | Converted | LINQ conversion exists; multiple SQL `fn_GetNRCNo` calls and wine type aggregation expanded with LINQ. |
| sp_WineImportationRegistrationReport | sp_WineImportationRegistrationReport.cs | Converted | LINQ conversion exists; multiple SQL `fn_GetNRCNo` calls and wine type aggregation expanded with LINQ. |
| sp_WineImportationReport | sp_WineImportationReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
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
- [x] Convert `GetRequestAutoApproveDescriptions` into `Backend/StoredProcedureToLinq/GetRequestAutoApproveDescriptions.cs`.
- [x] Convert `GetRequestAutoApproveDescriptionsImport` into `Backend/StoredProcedureToLinq/GetRequestAutoApproveDescriptionsImport.cs`.
- [x] Convert `GetRequestById` into `Backend/StoredProcedureToLinq/GetRequestById.cs`.
- [x] Convert `GetRequestByIdImport` into `Backend/StoredProcedureToLinq/GetRequestByIdImport.cs`.
- [x] Convert `sp_AccountSummaryReport` into `Backend/StoredProcedureToLinq/sp_AccountSummaryReport.cs`.
- [x] Convert `sp_AmendReport` into `Backend/StoredProcedureToLinq/sp_AmendReport.cs`.
- [x] Convert `sp_AutoCancelDataList` into `Backend/StoredProcedureToLinq/sp_AutoCancelDataList.cs`.
- [x] Convert `sp_BusinessServiceAgencyByPaThakaReport` into `Backend/StoredProcedureToLinq/sp_BusinessServiceAgencyByPaThakaReport.cs`.
- [x] Convert `sp_BusinessServiceAgencyRegistrationReport` into `Backend/StoredProcedureToLinq/sp_BusinessServiceAgencyRegistrationReport.cs`.
- [x] Convert `sp_BusinessServiceAgencyReport` into `Backend/StoredProcedureToLinq/sp_BusinessServiceAgencyReport.cs`.
- [x] Convert `sp_CancelReport` into `Backend/StoredProcedureToLinq/sp_CancelReport.cs`.
- [x] Convert `sp_CardListsByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_CardListsByPaThaKaReport.cs`.
- [x] Convert `sp_ChequeNoReport` into `Backend/StoredProcedureToLinq/sp_ChequeNoReport.cs`.
- [x] Convert `sp_CompanyProfileReport` into `Backend/StoredProcedureToLinq/sp_CompanyProfileReport.cs`.
- [x] Convert `sp_DashboardCompleted` into `Backend/StoredProcedureToLinq/sp_DashboardCompleted.cs`.
- [x] Convert `sp_DashboardFeedback` into `Backend/StoredProcedureToLinq/sp_DashboardFeedback.cs`.
- [x] Convert `sp_DashboardPayment` into `Backend/StoredProcedureToLinq/sp_DashboardPayment.cs`.
- [x] Convert `sp_DashboardProgress` into `Backend/StoredProcedureToLinq/sp_DashboardProgress.cs`.
- [x] Convert `sp_DirectorByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_DirectorByPaThaKaReport.cs`.
- [x] Convert `sp_DirectorListReport` into `Backend/StoredProcedureToLinq/sp_DirectorListReport.cs`.
- [x] Convert `sp_DutyFreeShopByReport` into `Backend/StoredProcedureToLinq/sp_DutyFreeShopByReport.cs`.
- [x] Convert `sp_DutyFreeShopRegistrationReport` into `Backend/StoredProcedureToLinq/sp_DutyFreeShopRegistrationReport.cs`.
- [x] Convert `sp_DutyFreeShopReport` into `Backend/StoredProcedureToLinq/sp_DutyFreeShopReport.cs`.
- [x] Convert `sp_EVCycleShowRoomReport` into `Backend/StoredProcedureToLinq/sp_EVCycleShowRoomReport.cs`.
- [x] Convert `sp_EVCycleShowRoomRegistrationReport` into `Backend/StoredProcedureToLinq/sp_EVCycleShowRoomRegistrationReport.cs`.
- [x] Convert `sp_EVShowRoomReport` into `Backend/StoredProcedureToLinq/sp_EVShowRoomReport.cs`.
- [x] Convert `sp_EVShowRoomRegistrationReport` into `Backend/StoredProcedureToLinq/sp_EVShowRoomRegistrationReport.cs`.
- [x] Convert `sp_GetChekApproveNotiList` into `Backend/StoredProcedureToLinq/sp_GetChekApproveNotiList.cs`.
- [x] Convert `sp_HSCodeSearch` into `Backend/StoredProcedureToLinq/sp_HSCodeSearch.cs`.
- [x] Convert `sp_LicencePermitSearch` into `Backend/StoredProcedureToLinq/sp_LicencePermitSearch.cs`.
- [x] Convert `sp_LicencePermitSearch_old` into `Backend/StoredProcedureToLinq/sp_LicencePermitSearch_old.cs`.
- [x] Convert `sp_MemberRegistrationReport` into `Backend/StoredProcedureToLinq/sp_MemberRegistrationReport.cs`.
- [x] Convert `sp_NotificationDataList` into `Backend/StoredProcedureToLinq/sp_NotificationDataList.cs`.
- [x] Convert `sp_PathakaBindReport` into `Backend/StoredProcedureToLinq/sp_PathakaBindReport.cs`.
- [x] Convert `sp_PaThaKaAllReport` into `Backend/StoredProcedureToLinq/sp_PaThaKaAllReport.cs`.
- [x] Convert `sp_PaThaKaByBusinessTypeReport` into `Backend/StoredProcedureToLinq/sp_PaThaKaByBusinessTypeReport.cs`.
- [x] Convert `sp_PaThaKaRegistrationReport` into `Backend/StoredProcedureToLinq/sp_PaThaKaRegistrationReport.cs`.
- [x] Convert `sp_PaThaKaReport` into `Backend/StoredProcedureToLinq/sp_PaThaKaReport.cs`.
- [x] Convert `sp_PaThaKaValidInvalidReport` into `Backend/StoredProcedureToLinq/sp_PaThaKaValidInvalidReport.cs`.
- [x] Convert `sp_PermitBusinessByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_PermitBusinessByPaThaKaReport.cs`.
- [x] Convert `sp_ReExportByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_ReExportByPaThaKaReport.cs`.
- [x] Convert `sp_ReExportReport` into `Backend/StoredProcedureToLinq/sp_ReExportReport.cs`.
- [x] Convert `sp_SaleCenterByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_SaleCenterByPaThaKaReport.cs`.
- [x] Convert `sp_SaleCenterRegistrationReport` into `Backend/StoredProcedureToLinq/sp_SaleCenterRegistrationReport.cs`.
- [x] Convert `sp_SaleCenterReport` into `Backend/StoredProcedureToLinq/sp_SaleCenterReport.cs`.
- [x] Convert `sp_ShowRoomByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_ShowRoomByPaThaKaReport.cs`.
- [x] Convert `sp_ShowRoomRegistrationReport` into `Backend/StoredProcedureToLinq/sp_ShowRoomRegistrationReport.cs`.
- [x] Convert `sp_ShowRoomReport` into `Backend/StoredProcedureToLinq/sp_ShowRoomReport.cs`.
- [x] Convert `sp_WholeSaleAndRetailByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_WholeSaleAndRetailByPaThaKaReport.cs`.
- [x] Convert `sp_WholeSaleRetailRegistrationReport` into `Backend/StoredProcedureToLinq/sp_WholeSaleRetailRegistrationReport.cs`.
- [x] Convert `sp_WholeSaleRetailReport` into `Backend/StoredProcedureToLinq/sp_WholeSaleRetailReport.cs`.
- [x] Convert `sp_WineImportationByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_WineImportationByPaThaKaReport.cs`.
- [x] Convert `sp_WineImportationRegistrationReport` into `Backend/StoredProcedureToLinq/sp_WineImportationRegistrationReport.cs`.
- [x] Convert `sp_WineImportationReport` into `Backend/StoredProcedureToLinq/sp_WineImportationReport.cs`.
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
- Latest batch verification succeeded with the same alternate output path. The build still reports existing migration naming warnings for `intial`; no converter errors were reported.
- `sp_AmendReport` mostly mirrors `sp_ActualAmendReport` with `ApplyType='Amend'`. Its SQL compares `BorderExportLicence.ExportImportSectionId` to Sakhan values in one border export licence Pa Tha Ka branch; the LINQ conversion preserves that behavior and documents it in the tracker.
- `sp_AccountSummaryReport` uses many `UNION ALL` branches. The LINQ conversion uses `Concat` and preserves branch labels, including the original SQL spelling `Wine Imporation`, final `FormType` filtering, final `SakhanId` filtering, and ordering by `PaymentDate` then account-title `SortOrder`.
- `sp_AutoCancelDataList` uses database current timestamp semantics in SQL. The LINQ conversion uses `DateTime.Now` inside `EF.Functions.DateDiffDay` so SQL Server can translate the date difference in the query.
- `sp_BusinessServiceAgencyReport` returns summary rows for `@Type='Summary'` and detail rows otherwise. The LINQ conversion uses a single superset result type with nullable fields to preserve one `IQueryable` return type.
- `sp_CancelReport` follows the licence/permit branch pattern and includes a `Remark` column from each source table. The LINQ conversion uses one superset result type with nullable Sakhan fields for a stable typed `IQueryable` result.
- `sp_CompanyProfileReport` calls `dbo.fn_GetPermitBusiness` in SQL. The LINQ conversion expands that function into a subquery over `PaThaKaPermitBusinesses` and `PermitBusinesses`, ordered by permit-business sort order and joined with commas.
- `sp_ChequeNoDetailReport` was inspected and is a large payment-detail `UNION ALL` report; leave it for a dedicated batch.
- `sp_DashboardPayment`, `sp_DashboardProgress`, `sp_DashboardFeedback`, and `sp_DashboardCompleted` follow the same grouped dashboard pattern: each source table branch remains deferred, groups by `ApplyType` and form label, and combines branch result sets with `Concat` to preserve SQL `UNION ALL` semantics.
- `sp_DashboardCompleted` keeps SQL `MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year` semantics with nullable `CreatedDate` checks inside each `IQueryable` branch.
- `sp_DirectorListReport` expands `dbo.fn_GetNRCNo` with LINQ joins to `Nrcprefixes` and `NrcprefixCodes`. In the `Director List` branch, a non-empty `CompanyRegistrationNo` still returns no rows because the original SQL `CASE` expression has no `ELSE`.
- `sp_DutyFreeShopByReport`, `sp_DutyFreeShopRegistrationReport`, and `sp_DutyFreeShopReport` expand `dbo.fn_GetNRCNo` with LINQ joins to `Nrcprefixes` and `NrcprefixCodes`.
- `sp_DutyFreeShopByReport` and the detail branches of `sp_DutyFreeShopReport` preserve the stored procedure's selected columns where `DutyFreeShop*` address aliases come from `PaThaKa` address fields, not `DutyFreeShop.Location*` fields.
- `sp_ReExportByPaThaKaReport`, `sp_SaleCenterByPaThaKaReport`, and `sp_ShowRoomByPaThaKaReport` preserve stored procedure address alias behavior even when the aliases point back to `PaThaKa` address columns.
- `sp_ReExportReport` follows the summary/detail pattern and preserves stored procedure address alias behavior where `ReExport*` address aliases point back to `PaThaKa` address fields.
- `sp_SaleCenterByPaThaKaReport` and `sp_ShowRoomByPaThaKaReport` expand `dbo.fn_GetNRCNo` and the `BusinessServiceAgencyNo` subquery into deferred LINQ joins.
- `sp_SaleCenterReport` follows the summary/detail pattern, expands `dbo.fn_GetNRCNo` and `BusinessServiceAgencyNo`, and preserves address aliases that point back to `PaThaKa` fields in detail branches.
- `sp_SaleCenterRegistrationReport` and `sp_ShowRoomRegistrationReport` expand `dbo.fn_GetNRCNo` and the `BusinessServiceAgencyNo` subquery into deferred LINQ joins.
- `sp_ShowRoomReport` follows the summary/detail pattern, expands `dbo.fn_GetNRCNo` and `BusinessServiceAgencyNo`, and preserves address aliases that point back to `PaThaKa` fields in detail branches.
- `sp_WineImportationRegistrationReport` expands four `dbo.fn_GetNRCNo` calls and represents the SQL XML wine-type concatenation with a `string.Join` subquery over `WineTypes`.
- `sp_WineImportationByPaThaKaReport` uses the same four `dbo.fn_GetNRCNo` expansions and wine-type `string.Join` pattern as `sp_WineImportationRegistrationReport`.
- `sp_WineImportationReport` follows the summary/detail pattern and uses the same four `dbo.fn_GetNRCNo` expansions plus wine-type `string.Join` pattern as the other wine reports.
- `sp_EVCycleShowRoomRegistrationReport` and `sp_EVShowRoomRegistrationReport` mirror the show-room registration payment pattern with EV-specific DbSets.
- `sp_EVCycleShowRoomReport` mirrors `sp_ShowRoomReport` with EV cycle-specific DbSets, including `dbo.fn_GetNRCNo`, `BusinessServiceAgencyNo`, and PaThaKa-based address aliases.
- `sp_EVShowRoomReport` mirrors `sp_ShowRoomReport` with EV-specific DbSets, including `dbo.fn_GetNRCNo`, `BusinessServiceAgencyNo`, and PaThaKa-based address aliases.
- `sp_GetChekApproveNotiList` preserves the stored procedure's branch-specific omissions: the check-user `Retail` wholesale/retail branch has no `Status='Pending'` filter, and the approve-user `Border Import Licence` branch has no `Status='Pending'` filter.
- `sp_HSCodeSearch` preserves the stored procedure's export-branch section-filter oddity: for `ExportImportSectionId > 0`, the SQL pattern is a literal `%'+@ExportImportSectionCode+'%` rather than concatenating the variable.
- `sp_LicencePermitSearch_old` delegates to `sp_LicencePermitSearch` because the SQL body is equivalent except for `WITH(INDEX(...))` hints, which are not represented in provider-neutral LINQ.
- `sp_MemberRegistrationReport` preserves the stored procedure behavior where the `All` branch projects `ExtensionDate` as `IssuedDate` for extension rows, while the `Extension` branch projects the actual `IssuedDate`.
- `sp_NotificationDataList` uses `EF.Functions.DateDiffDay` to preserve SQL `DATEDIFF(day, EndDate, CURRENT_TIMESTAMP)` semantics and keeps warning-date calculation in the query projection.
- `sp_PermitBusinessByPaThaKaReport` uses a PaThaKa-rooted left-join shape to preserve the effective rows of the stored procedure's `FULL JOIN` plus `WHERE pathaka.CompanyRegistrationNo=...` filter.
- `sp_PaThaKaAllReport` expands the owner `dbo.fn_GetNRCNo` call with LINQ joins to `Nrcprefixes` and `NrcprefixCodes`.
- `sp_WholeSaleRetailRegistrationReport` uses `Concat` to preserve SQL `UNION ALL`; its second branch joins through `PaThaKaRegistration` and account transactions for wholesale Pa Tha Ka payments.
- `sp_WholeSaleRetailReport` preserves the stored procedure's typo alias `WholeSaleRetailostalCode` and its address aliases that point back to `PaThaKa` fields in detail branches.

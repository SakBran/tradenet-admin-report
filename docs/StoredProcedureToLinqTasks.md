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
- 84 likely query/report/list/search/dashboard procedures.
- 24 likely non-`IQueryable` helper or mutation procedures.
- 0 manual-review procedures.

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
- Likely LINQ files to create: 84.
- Converted: 84.
- In process: 0.
- To do: 0.
- Manual review: 0.
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
| sp_ApplicationHistory | sp_ApplicationHistory.cs | Converted | LINQ conversion exists; approved/non-approved date branches are factored through a shared deferred source projection. |
| sp_AutoCancelDataList | sp_AutoCancelDataList.cs | Converted | LINQ conversion exists; SQL `DATEDIFF(day, ApproveDate, CURRENT_TIMESTAMP)` represented with `EF.Functions.DateDiffDay`. |
| sp_BusinessServiceAgencyByPaThakaReport | sp_BusinessServiceAgencyByPaThakaReport.cs | Converted | LINQ conversion exists. |
| sp_BusinessServiceAgencyRegistrationReport | sp_BusinessServiceAgencyRegistrationReport.cs | Converted | LINQ conversion exists. |
| sp_BusinessServiceAgencyReport | sp_BusinessServiceAgencyReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
| sp_CancelReport | sp_CancelReport.cs | Converted | LINQ conversion exists; large branch-heavy cancellation report represented with typed result projection. |
| sp_CardListsByPaThaKaReport | sp_CardListsByPaThaKaReport.cs | Converted | LINQ conversion exists. |
| sp_ChequeNoDetailReport | sp_ChequeNoDetailReport.cs | Converted | LINQ conversion exists; shared cheque/payment/MPU base rows feed each form branch. |
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
| sp_EICCBalanceCertificateList | sp_EICCBalanceCertificateList.cs | Converted | LINQ conversion exists; repeated certificate branches share a normalized deferred row projection. |
| sp_EICCPendingCertificateList | sp_EICCPendingCertificateList.cs | Converted | LINQ conversion exists; preserves `Certificate`, `LicencePermit`, and `BorderLicencePermit` type branches. |
| sp_EICCReport | sp_EICCReport.cs | Converted | LINQ conversion exists; preserves EICC type branches and product-filter differences. |
| sp_EICCSubmitBorderLicencePermitList | sp_EICCSubmitBorderLicencePermitList.cs | Converted | LINQ conversion exists; Approved status keeps the extra `IsApprove=0` filter from the stored procedure branch. |
| sp_EICCSubmitCertificateList | sp_EICCSubmitCertificateList.cs | Converted | LINQ conversion exists; preserves Business Service Agency approved-branch omission of `IsApprove=0`. |
| sp_EICCSubmitLicencePermitList | sp_EICCSubmitLicencePermitList.cs | Converted | LINQ conversion exists; Approved status keeps the extra `IsApprove=0` filter from the stored procedure branch. |
| sp_EVCycleShowRoomRegistrationReport | sp_EVCycleShowRoomRegistrationReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` and business-service-agency lookup expanded with LINQ. |
| sp_EVCycleShowRoomReport | sp_EVCycleShowRoomReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
| sp_EVShowRoomRegistrationReport | sp_EVShowRoomRegistrationReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` and business-service-agency lookup expanded with LINQ. |
| sp_EVShowRoomReport | sp_EVShowRoomReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
| sp_ExportLicenceDetailReport | sp_ExportLicenceDetailReport.cs | Converted | LINQ conversion exists; border Pa Tha Ka and Individual Trading branches are kept separate. |
| sp_ExportPermitDetailReport | sp_ExportPermitDetailReport.cs | Converted | LINQ conversion exists; comma-separated country/port lookups and NRC function are expanded in LINQ. |
| sp_ExtensionReport | sp_ExtensionReport.cs | Converted | LINQ conversion exists; mirrors the licence/permit extension branches without an amend-remark filter. |
| sp_GetChekApproveNotiList | sp_GetChekApproveNotiList.cs | Converted | LINQ conversion exists; branch-specific notification filters preserved. |
| sp_HSCodeReport | sp_HSCodeReport.cs | Converted | LINQ conversion exists; shared HS-code prefix/suffix filter preserves `Start` and end-match behavior. |
| sp_HSCodeSearch | sp_HSCodeSearch.cs | Converted | LINQ conversion exists; preserves stored procedure's literal export-section LIKE pattern. |
| sp_ImportLicenceDaily_Detail_Report | sp_ImportLicenceDaily_Detail_Report.cs | Converted | LINQ conversion exists; projects the daily-detail shape from the approved import licence detail query. |
| sp_ImportLicenceDetailReport | sp_ImportLicenceDetailReport.cs | Converted | LINQ conversion exists; border Pa Tha Ka and Individual Trading branches are kept separate. |
| sp_ImportLicencePendingDetailReport | sp_ImportLicencePendingDetailReport.cs | Converted | LINQ conversion exists; pending rows use `ApplicationDate` filtering. |
| sp_ImportPermitDetailReport | sp_ImportPermitDetailReport.cs | Converted | LINQ conversion exists; comma-separated country/port lookups and NRC function are expanded in LINQ. |
| sp_LicencePermitSearch | sp_LicencePermitSearch.cs | Converted | LINQ conversion exists; SQL `UNION ALL`, `ORDER BY CreatedDate DESC`, and `TOP 1` represented with `Concat`, `OrderByDescending`, and `Take(1)`. |
| sp_LicencePermitSearch_old | sp_LicencePermitSearch_old.cs | Converted | LINQ conversion exists; delegates to current search conversion because SQL differs only by index hints. |
| sp_MemberRegistrationReport | sp_MemberRegistrationReport.cs | Converted | LINQ conversion exists; preserves different `IssuedDate` projection behavior in `All` vs `Extension` branches. |
| sp_MPUReport | sp_MPUReport.cs | Converted | LINQ conversion exists; preserves pre/post 2025-11-15 online-fee threshold branches. |
| sp_MPUReport_Seperated_OnineFee | sp_MPUReport_Seperated_OnineFee.cs | Converted | LINQ conversion exists; preserves original stored procedure spelling and unused `ReportType` parameter. |
| sp_MPUReport_V3 | sp_MPUReport_V3.cs | Converted | LINQ conversion exists; SQL `ROW_NUMBER()` pairing represented with deferred correlated row-number counts. |
| sp_MPUReportV2 | sp_MPUReportV2.cs | Converted | LINQ conversion exists; account UNION branches feed a deferred left join to MPU transactions. |
| sp_NewReport | sp_NewReport.cs | Converted | LINQ conversion exists; preserves branch-specific `auto`, `quota`, and commodity fields. |
| sp_NewReport_old | sp_NewReport_old.cs | Converted | LINQ conversion exists; legacy first-item amount behavior and commented auto filters are preserved. |
| sp_NotificationDataList | sp_NotificationDataList.cs | Converted | LINQ conversion exists; SQL date-warning logic represented with `EF.Functions.DateDiffDay`. |
| sp_OGARecommendationHistoryReport | sp_OGARecommendationHistoryReport.cs | Converted | LINQ conversion exists; licence/permit history branches are combined with `Concat` to preserve SQL `UNION ALL`. |
| sp_OGARecommendationListReport | sp_OGARecommendationListReport.cs | Converted | LINQ conversion exists; SQL date-string columns are represented with deferred date-part string projections. |
| sp_OGARecommendationReport | sp_OGARecommendationReport.cs | Converted | LINQ conversion exists; `ReferenceNo` uses `"0"` as the no-filter sentinel matching the SQL comparison to `0`. |
| sp_OnlineFeesReport | sp_OnlineFeesReport.cs | Converted | LINQ conversion exists; online-fee account rows are reused across transaction branches. |
| sp_PaThaKaAllReport | sp_PaThaKaAllReport.cs | Converted | LINQ conversion exists; owner `fn_GetNRCNo` expanded with prefix joins. |
| sp_PathakaBindReport | sp_PathakaBindReport.cs | Converted | LINQ conversion exists. |
| sp_PaThaKaByBusinessTypeReport | sp_PaThaKaByBusinessTypeReport.cs | Converted | LINQ conversion exists. |
| sp_PaThaKaRegistrationReport | sp_PaThaKaRegistrationReport.cs | Converted | LINQ conversion exists. |
| sp_PaThaKaReport | sp_PaThaKaReport.cs | Converted | LINQ conversion exists. |
| sp_PaThaKaValidInvalidReport | sp_PaThaKaValidInvalidReport.cs | Converted | LINQ conversion exists. |
| sp_PendingReport | sp_PendingReport.cs | Converted | LINQ conversion exists; preserves the stored procedure's three-form branching and scalar item subqueries. |
| sp_PermitBusinessByPaThaKaReport | sp_PermitBusinessByPaThaKaReport.cs | Converted | LINQ conversion exists; SQL full joins represented with PaThaKa-rooted left joins. |
| sp_ReExportByPaThaKaReport | sp_ReExportByPaThaKaReport.cs | Converted | LINQ conversion exists; preserves SQL address alias behavior. |
| sp_ReExportReport | sp_ReExportReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
| sp_SaleCenterByPaThaKaReport | sp_SaleCenterByPaThaKaReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` and business-service-agency lookup expanded with LINQ. |
| sp_SaleCenterRegistrationReport | sp_SaleCenterRegistrationReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` and business-service-agency lookup expanded with LINQ. |
| sp_SaleCenterReport | sp_SaleCenterReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
| sp_ShowRoomByPaThaKaReport | sp_ShowRoomByPaThaKaReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` and business-service-agency lookup expanded with LINQ. |
| sp_ShowRoomRegistrationReport | sp_ShowRoomRegistrationReport.cs | Converted | LINQ conversion exists; SQL `fn_GetNRCNo` and business-service-agency lookup expanded with LINQ. |
| sp_ShowRoomReport | sp_ShowRoomReport.cs | Converted | LINQ conversion exists; summary/detail result shapes represented by one superset result class. |
| sp_TestReport | sp_TestReport.cs | Converted | LINQ conversion exists; preserves the stored procedure's 461 hard-coded company-registration filters. |
| sp_VoucherReport | sp_VoucherReport.cs | Converted | LINQ conversion exists; raw voucher rows are projected once before final date-string formatting. |
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
- [x] Convert `sp_ApplicationHistory` into `Backend/StoredProcedureToLinq/sp_ApplicationHistory.cs`.
- [x] Convert `sp_AutoCancelDataList` into `Backend/StoredProcedureToLinq/sp_AutoCancelDataList.cs`.
- [x] Convert `sp_BusinessServiceAgencyByPaThakaReport` into `Backend/StoredProcedureToLinq/sp_BusinessServiceAgencyByPaThakaReport.cs`.
- [x] Convert `sp_BusinessServiceAgencyRegistrationReport` into `Backend/StoredProcedureToLinq/sp_BusinessServiceAgencyRegistrationReport.cs`.
- [x] Convert `sp_BusinessServiceAgencyReport` into `Backend/StoredProcedureToLinq/sp_BusinessServiceAgencyReport.cs`.
- [x] Convert `sp_CancelReport` into `Backend/StoredProcedureToLinq/sp_CancelReport.cs`.
- [x] Convert `sp_CardListsByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_CardListsByPaThaKaReport.cs`.
- [x] Convert `sp_ChequeNoDetailReport` into `Backend/StoredProcedureToLinq/sp_ChequeNoDetailReport.cs`.
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
- [x] Convert `sp_EICCBalanceCertificateList` into `Backend/StoredProcedureToLinq/sp_EICCBalanceCertificateList.cs`.
- [x] Convert `sp_EICCPendingCertificateList` into `Backend/StoredProcedureToLinq/sp_EICCPendingCertificateList.cs`.
- [x] Convert `sp_EICCReport` into `Backend/StoredProcedureToLinq/sp_EICCReport.cs`.
- [x] Convert `sp_EVCycleShowRoomReport` into `Backend/StoredProcedureToLinq/sp_EVCycleShowRoomReport.cs`.
- [x] Convert `sp_EVCycleShowRoomRegistrationReport` into `Backend/StoredProcedureToLinq/sp_EVCycleShowRoomRegistrationReport.cs`.
- [x] Convert `sp_EVShowRoomReport` into `Backend/StoredProcedureToLinq/sp_EVShowRoomReport.cs`.
- [x] Convert `sp_EVShowRoomRegistrationReport` into `Backend/StoredProcedureToLinq/sp_EVShowRoomRegistrationReport.cs`.
- [x] Convert `sp_ExportLicenceDetailReport` into `Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReport.cs`.
- [x] Convert `sp_ExportPermitDetailReport` into `Backend/StoredProcedureToLinq/sp_ExportPermitDetailReport.cs`.
- [x] Convert `sp_ExtensionReport` into `Backend/StoredProcedureToLinq/sp_ExtensionReport.cs`.
- [x] Convert `sp_GetChekApproveNotiList` into `Backend/StoredProcedureToLinq/sp_GetChekApproveNotiList.cs`.
- [x] Convert `sp_HSCodeReport` into `Backend/StoredProcedureToLinq/sp_HSCodeReport.cs`.
- [x] Convert `sp_HSCodeSearch` into `Backend/StoredProcedureToLinq/sp_HSCodeSearch.cs`.
- [x] Convert `sp_ImportLicenceDaily_Detail_Report` into `Backend/StoredProcedureToLinq/sp_ImportLicenceDaily_Detail_Report.cs`.
- [x] Convert `sp_ImportLicenceDetailReport` into `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport.cs`.
- [x] Convert `sp_ImportLicencePendingDetailReport` into `Backend/StoredProcedureToLinq/sp_ImportLicencePendingDetailReport.cs`.
- [x] Convert `sp_ImportPermitDetailReport` into `Backend/StoredProcedureToLinq/sp_ImportPermitDetailReport.cs`.
- [x] Convert `sp_LicencePermitSearch` into `Backend/StoredProcedureToLinq/sp_LicencePermitSearch.cs`.
- [x] Convert `sp_LicencePermitSearch_old` into `Backend/StoredProcedureToLinq/sp_LicencePermitSearch_old.cs`.
- [x] Convert `sp_MemberRegistrationReport` into `Backend/StoredProcedureToLinq/sp_MemberRegistrationReport.cs`.
- [x] Convert `sp_MPUReport` into `Backend/StoredProcedureToLinq/sp_MPUReport.cs`.
- [x] Convert `sp_MPUReport_Seperated_OnineFee` into `Backend/StoredProcedureToLinq/sp_MPUReport_Seperated_OnineFee.cs`.
- [x] Convert `sp_MPUReport_V3` into `Backend/StoredProcedureToLinq/sp_MPUReport_V3.cs`.
- [x] Convert `sp_MPUReportV2` into `Backend/StoredProcedureToLinq/sp_MPUReportV2.cs`.
- [x] Convert `sp_NewReport` into `Backend/StoredProcedureToLinq/sp_NewReport.cs`.
- [x] Convert `sp_NewReport_old` into `Backend/StoredProcedureToLinq/sp_NewReport_old.cs`.
- [x] Convert `sp_NotificationDataList` into `Backend/StoredProcedureToLinq/sp_NotificationDataList.cs`.
- [x] Convert `sp_OnlineFeesReport` into `Backend/StoredProcedureToLinq/sp_OnlineFeesReport.cs`.
- [x] Convert `sp_EICCSubmitBorderLicencePermitList` into `Backend/StoredProcedureToLinq/sp_EICCSubmitBorderLicencePermitList.cs`.
- [x] Convert `sp_EICCSubmitCertificateList` into `Backend/StoredProcedureToLinq/sp_EICCSubmitCertificateList.cs`.
- [x] Convert `sp_EICCSubmitLicencePermitList` into `Backend/StoredProcedureToLinq/sp_EICCSubmitLicencePermitList.cs`.
- [x] Convert `sp_OGARecommendationHistoryReport` into `Backend/StoredProcedureToLinq/sp_OGARecommendationHistoryReport.cs`.
- [x] Convert `sp_OGARecommendationListReport` into `Backend/StoredProcedureToLinq/sp_OGARecommendationListReport.cs`.
- [x] Convert `sp_OGARecommendationReport` into `Backend/StoredProcedureToLinq/sp_OGARecommendationReport.cs`.
- [x] Convert `sp_PathakaBindReport` into `Backend/StoredProcedureToLinq/sp_PathakaBindReport.cs`.
- [x] Convert `sp_PaThaKaAllReport` into `Backend/StoredProcedureToLinq/sp_PaThaKaAllReport.cs`.
- [x] Convert `sp_PaThaKaByBusinessTypeReport` into `Backend/StoredProcedureToLinq/sp_PaThaKaByBusinessTypeReport.cs`.
- [x] Convert `sp_PaThaKaRegistrationReport` into `Backend/StoredProcedureToLinq/sp_PaThaKaRegistrationReport.cs`.
- [x] Convert `sp_PaThaKaReport` into `Backend/StoredProcedureToLinq/sp_PaThaKaReport.cs`.
- [x] Convert `sp_PaThaKaValidInvalidReport` into `Backend/StoredProcedureToLinq/sp_PaThaKaValidInvalidReport.cs`.
- [x] Convert `sp_PermitBusinessByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_PermitBusinessByPaThaKaReport.cs`.
- [x] Convert `sp_PendingReport` into `Backend/StoredProcedureToLinq/sp_PendingReport.cs`.
- [x] Convert `sp_ReExportByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_ReExportByPaThaKaReport.cs`.
- [x] Convert `sp_ReExportReport` into `Backend/StoredProcedureToLinq/sp_ReExportReport.cs`.
- [x] Convert `sp_SaleCenterByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_SaleCenterByPaThaKaReport.cs`.
- [x] Convert `sp_SaleCenterRegistrationReport` into `Backend/StoredProcedureToLinq/sp_SaleCenterRegistrationReport.cs`.
- [x] Convert `sp_SaleCenterReport` into `Backend/StoredProcedureToLinq/sp_SaleCenterReport.cs`.
- [x] Convert `sp_ShowRoomByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_ShowRoomByPaThaKaReport.cs`.
- [x] Convert `sp_ShowRoomRegistrationReport` into `Backend/StoredProcedureToLinq/sp_ShowRoomRegistrationReport.cs`.
- [x] Convert `sp_ShowRoomReport` into `Backend/StoredProcedureToLinq/sp_ShowRoomReport.cs`.
- [x] Convert `sp_TestReport` into `Backend/StoredProcedureToLinq/sp_TestReport.cs`.
- [x] Convert `sp_VoucherReport` into `Backend/StoredProcedureToLinq/sp_VoucherReport.cs`.
- [x] Convert `sp_WholeSaleAndRetailByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_WholeSaleAndRetailByPaThaKaReport.cs`.
- [x] Convert `sp_WholeSaleRetailRegistrationReport` into `Backend/StoredProcedureToLinq/sp_WholeSaleRetailRegistrationReport.cs`.
- [x] Convert `sp_WholeSaleRetailReport` into `Backend/StoredProcedureToLinq/sp_WholeSaleRetailReport.cs`.
- [x] Convert `sp_WineImportationByPaThaKaReport` into `Backend/StoredProcedureToLinq/sp_WineImportationByPaThaKaReport.cs`.
- [x] Convert `sp_WineImportationRegistrationReport` into `Backend/StoredProcedureToLinq/sp_WineImportationRegistrationReport.cs`.
- [x] Convert `sp_WineImportationReport` into `Backend/StoredProcedureToLinq/sp_WineImportationReport.cs`.
- [x] Create one `.cs` file for each convertible stored procedure.
- [x] Add request classes for procedures with parameters.
- [x] Add result classes for report projections that do not map directly to an EF entity.
- [x] Convert SQL joins into LINQ joins or navigation-property queries.
- [x] Convert optional filters such as empty strings, zero ids, and null dates according to each stored procedure's SQL logic.
- [x] Convert SQL `CASE` expressions into conditional projections.
- [x] Convert `UNION` and `UNION ALL` into `Union` or `Concat` as appropriate.
- [x] Preserve sorting with `OrderBy`, `ThenBy`, `OrderByDescending`, or `ThenByDescending`.
- [x] Add short comments only where a SQL behavior is non-obvious.

## Validation Tasks

- [x] Run `dotnet build Backend/API.csproj` against a separate output directory because a local `API` process is locking the normal `bin` output.
- [x] Run scan for raw SQL/stored-procedure execution/materialization calls in `Backend/StoredProcedureToLinq`.
- [ ] Compare generated SQL for representative LINQ queries with stored procedure logic using `ToQueryString()` where useful.
- [ ] For selected procedures, compare row counts and key result columns against the original stored procedure using the same parameters.
- [ ] Fix any mismatched null handling, string trimming, date range behavior, or join cardinality.
- [ ] Keep a list of procedures that cannot be represented as pure `IQueryable` without changing behavior.

## Current Notes

- The database includes both business/report procedures and ASP.NET session-state style procedures. The latter are not valid `IQueryable` conversions because they update or manage state.
- Some stored procedures may return columns that are not represented by existing EF entities. Those need dedicated result projection classes in their own procedure file.
- `IQueryable` output means the caller is responsible for execution, paging, and materialization.
- `sp_ActualAmendReport` returns different column counts by form type in SQL. The LINQ conversion uses one superset result class with nullable Sakhan fields for a stable typed `IQueryable` result.
- `sp_ApplicationHistory` factors the repeated approved/non-approved application-history branches through shared deferred source projections. Approved rows use `CreatedDate`; all other non-empty statuses use `ApplicationDate`.
- Normal build output is currently locked by process `API (8076)`. Verification build succeeded with `dotnet build Backend\API.csproj -p:OutputPath=C:\Code\Ministry_of_Commerce_Tradenet_build_verify\API\`.
- Latest batch verification succeeded with the same alternate output path. The build still reports existing migration naming warnings for `intial`; no converter errors were reported.
- `sp_AmendReport` mostly mirrors `sp_ActualAmendReport` with `ApplyType='Amend'`. Its SQL compares `BorderExportLicence.ExportImportSectionId` to Sakhan values in one border export licence Pa Tha Ka branch; the LINQ conversion preserves that behavior and documents it in the tracker.
- `sp_AccountSummaryReport` uses many `UNION ALL` branches. The LINQ conversion uses `Concat` and preserves branch labels, including the original SQL spelling `Wine Imporation`, final `FormType` filtering, final `SakhanId` filtering, and ordering by `PaymentDate` then account-title `SortOrder`.
- `sp_AutoCancelDataList` uses database current timestamp semantics in SQL. The LINQ conversion uses `DateTime.Now` inside `EF.Functions.DateDiffDay` so SQL Server can translate the date difference in the query.
- `sp_BusinessServiceAgencyReport` returns summary rows for `@Type='Summary'` and detail rows otherwise. The LINQ conversion uses a single superset result type with nullable fields to preserve one `IQueryable` return type.
- `sp_CancelReport` follows the licence/permit branch pattern and includes a `Remark` column from each source table. The LINQ conversion uses one superset result type with nullable Sakhan fields for a stable typed `IQueryable` result.
- `sp_CompanyProfileReport` calls `dbo.fn_GetPermitBusiness` in SQL. The LINQ conversion expands that function into a subquery over `PaThaKaPermitBusinesses` and `PermitBusinesses`, ordered by permit-business sort order and joined with commas.
- `sp_ChequeNoDetailReport` shares the account-transaction/account-title/cheque/MPU payment base query and feeds it into each card/licence/permit branch with `Concat`.
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
- `sp_TestReport` preserves the stored procedure's static company-registration filter list. Its address projection keeps the SQL behavior where a null `UnitLevel` makes the concatenated address null.
- `sp_WineImportationRegistrationReport` expands four `dbo.fn_GetNRCNo` calls and represents the SQL XML wine-type concatenation with a `string.Join` subquery over `WineTypes`.
- `sp_WineImportationByPaThaKaReport` uses the same four `dbo.fn_GetNRCNo` expansions and wine-type `string.Join` pattern as `sp_WineImportationRegistrationReport`.
- `sp_WineImportationReport` follows the summary/detail pattern and uses the same four `dbo.fn_GetNRCNo` expansions plus wine-type `string.Join` pattern as the other wine reports.
- `sp_EVCycleShowRoomRegistrationReport` and `sp_EVShowRoomRegistrationReport` mirror the show-room registration payment pattern with EV-specific DbSets.
- `sp_EVCycleShowRoomReport` mirrors `sp_ShowRoomReport` with EV cycle-specific DbSets, including `dbo.fn_GetNRCNo`, `BusinessServiceAgencyNo`, and PaThaKa-based address aliases.
- `sp_EVShowRoomReport` mirrors `sp_ShowRoomReport` with EV-specific DbSets, including `dbo.fn_GetNRCNo`, `BusinessServiceAgencyNo`, and PaThaKa-based address aliases.
- `sp_GetChekApproveNotiList` preserves the stored procedure's branch-specific omissions: the check-user `Retail` wholesale/retail branch has no `Status='Pending'` filter, and the approve-user `Border Import Licence` branch has no `Status='Pending'` filter.
- `sp_HSCodeSearch` preserves the stored procedure's export-branch section-filter oddity: for `ExportImportSectionId > 0`, the SQL pattern is a literal `%'+@ExportImportSectionCode+'%` rather than concatenating the variable.
- `sp_ExportPermitDetailReport` and `sp_ImportPermitDetailReport` expand `dbo.fn_GetNRCNo` with NRC prefix joins and represent SQL XML comma-list aggregation for port/country fields with deferred `string.Join` subqueries.
- `sp_ExportLicenceDetailReport` and `sp_ImportLicenceDetailReport` preserve separate border branches for Pa Tha Ka and Individual Trading card types and use deferred `string.Join` subqueries for multi-country/port list fields.
- `sp_ImportLicenceDetailReport` preserves the stored procedure's separate border branches for Pa Tha Ka and Individual Trading card types and uses deferred `string.Join` subqueries for consigned/country-of-origin lists.
- `sp_LicencePermitSearch_old` delegates to `sp_LicencePermitSearch` because the SQL body is equivalent except for `WITH(INDEX(...))` hints, which are not represented in provider-neutral LINQ.
- `sp_MemberRegistrationReport` preserves the stored procedure behavior where the `All` branch projects `ExtensionDate` as `IssuedDate` for extension rows, while the `Extension` branch projects the actual `IssuedDate`.
- `sp_MPUReport` follows the stored procedure threshold switch at `2025-11-15`: before that date it separates `3000`, and from that date onward it separates `10000`. `MOCAmount` is scaffolded as a string, so the LINQ filter compares that amount column to the same string values.
- `sp_MPUReport_Seperated_OnineFee` keeps the stored procedure's unused `@ReportType` parameter in the request class and filters only the import-side form types used by the SQL.
- `sp_MPUReport_V3` represents the SQL CTE `ROW_NUMBER()` matching by correlated counts over each `TransactionId`, ordered by MPU transaction date/id and account transaction created date/id.
- `sp_MPUReportV2` keeps the stored procedure's account-transaction branch set and performs the MPU transaction match as a deferred left join on `TransactionId` and `MOCAmount`.
- `sp_NewReport` preserves the current stored procedure's summed item amounts plus branch-specific `auto`, `quota`, and `CommodityType` projections.
- `sp_NewReport_old` preserves the legacy stored procedure's first-item amount behavior and the branches where `auto` filters are commented out in SQL.
- `sp_ExtensionReport` mirrors the eight licence/permit extension branches and uses `LastDate` for non-border display dates while border branches use `CreatedDate`.
- `sp_HSCodeReport` collapses repeated SQL branches into shared LINQ HS-code filtering while preserving `Start` prefix matching versus suffix matching.
- `sp_ImportLicenceDaily_Detail_Report` projects the daily report shape from the approved import licence detail query and keeps border-only consigned/country-origin fields nullable for oversea rows.
- `sp_ImportLicencePendingDetailReport` mirrors the import licence detail report but uses `Status='Pending'` and `ApplicationDate` filtering.
- `sp_VoucherReport` builds raw voucher rows per form type, then formats payment/licence/voucher date strings in the final deferred projection.
- `sp_NotificationDataList` uses `EF.Functions.DateDiffDay` to preserve SQL `DATEDIFF(day, EndDate, CURRENT_TIMESTAMP)` semantics and keeps warning-date calculation in the query projection.
- `sp_EICCSubmitLicencePermitList` and `sp_EICCSubmitBorderLicencePermitList` preserve the stored procedure branch behavior where `@EICCStatus='Approved'` adds an `IsApprove=0` filter, while other statuses do not.
- `sp_EICCBalanceCertificateList` normalizes all certificate/licence/permit branches before the final date-string projection; the final form-type filter preserves the stored procedure's prefix match when `@FormType` is provided.
- `sp_EICCPendingCertificateList` keeps the stored procedure's three `@Type` branches. Its `Certificate` branch includes the Show Room-specific `FormType LIKE 'Show Room%'` condition.
- `sp_EICCReport` keeps the stored procedure's EICC status/date filters and branch-specific product filters: the export-licence licence/permit branch requires exact product ids, while the other licence/permit branches use zero-id optional filters.
- `sp_EICCSubmitCertificateList` preserves the stored procedure's approved-branch oddity: the Business Service Agency branch does not add `IsApprove=0`, but the other certificate branches do.
- `sp_OGARecommendationHistoryReport` preserves the stored procedure's eight licence/permit branches and final `CreatedDate` ordering.
- `sp_OGARecommendationListReport` keeps the stored procedure's exact filters and ordering; SQL `CONVERT(varchar, date, 103)` display columns are represented as deferred day/month/year string projections.
- `sp_OGARecommendationReport` treats request `ReferenceNo == "0"` as the no-filter sentinel, matching the stored procedure's `@ReferenceNo=0` comparison despite the SQL parameter being `nvarchar`.
- `sp_OnlineFeesReport` factors the account-transaction/account-title online-fee filter into a deferred base query and reuses it across all transaction-form branches.
- `sp_PendingReport` only returns rows for the three stored procedure branches (`Import Licence`, `Export Licence`, and `Border Import Licence`); other form types return an empty deferred query.
- `sp_PermitBusinessByPaThaKaReport` uses a PaThaKa-rooted left-join shape to preserve the effective rows of the stored procedure's `FULL JOIN` plus `WHERE pathaka.CompanyRegistrationNo=...` filter.
- `sp_PaThaKaAllReport` expands the owner `dbo.fn_GetNRCNo` call with LINQ joins to `Nrcprefixes` and `NrcprefixCodes`.
- `sp_WholeSaleRetailRegistrationReport` uses `Concat` to preserve SQL `UNION ALL`; its second branch joins through `PaThaKaRegistration` and account transactions for wholesale Pa Tha Ka payments.
- `sp_WholeSaleRetailReport` preserves the stored procedure's typo alias `WholeSaleRetailostalCode` and its address aliases that point back to `PaThaKa` fields in detail branches.

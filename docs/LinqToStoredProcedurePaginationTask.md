# LINQ → Stored Procedure (Paginated) Conversion Task

How to convert a report API that currently runs an in-process **LINQ** query
(`API.StoredProcedureToLinq`) into one that calls a **paginated stored
procedure** directly, so the API returns exactly what the source stored
procedure returns, with pagination pushed into SQL Server.

This is the playbook for the work already done on
`PaThaKaRegisteredBusinessOrganizationReportController` (the reference example).

---

## Why this conversion

The `StoredProcedureToLinq` layer reimplements each stored procedure as a
deferred `IQueryable`. For most reports it matches, but some diverge from the
original SQL (join cardinality, null handling, type coercion, ordering). When a
report's results must match the source stored procedure **exactly**, call the
stored procedure instead of the LINQ copy.

The wrinkle: EF Core **cannot** compose `Skip`/`Take`/`Count`/`Where`/`OrderBy`
on top of `EXEC <proc>` — SQL Server stored procedures are not composable. So
pagination has to live **inside** a procedure (OFFSET/FETCH). We do that in a
**new** procedure and never touch the original.

---

## Golden rules

1. **Never modify the existing stored procedure.** If the source is
   `sp_existing`, create a new `sp_existing_pagination` and put the pagination
   logic there. The converted API calls `sp_existing_pagination`. The original
   keeps serving any legacy callers (RDLC reports, other apps) unchanged.
2. **New procedures live in `StoredProcedureMigrations/`** — one `.sql` file per
   procedure, `CREATE OR ALTER` so re-running is safe. This folder is the source
   of truth for **production** deployment.
3. **Do not edit `docs/StoredProcedureDefinitions.sql`** for the new procedure.
   That file is a snapshot of the *existing* procedures and must keep matching
   production. New `_pagination` procedures belong only in
   `StoredProcedureMigrations/`.
4. **Development = direct DB change + test.** For the current `appsettings.json`
   dev database (`TradeNetDBTest` → `Server=203.81.66.111,14330;Database=TradeNetDB`),
   apply the new procedure directly to the DB and verify it before wiring the API.
   `sqlcmd` is not installed locally; `pymssql` is available and is what we use.
5. **The procedure returns the page + an inline `TotalCount`** (via
   `COUNT(*) OVER()`) so the API gets the page and the grand total in one call.
6. **Keep the source SQL semantics identical** — copy the original SELECT / JOINs
   / WHERE verbatim, then add only sorting + paging + `TotalCount`.

---

## The paginated procedure contract

New procedure `sp_<name>_pagination` takes the original filter parameters, then
appends these optional parameters (defaults keep older call shapes working):

| Param | Type | Meaning |
| --- | --- | --- |
| `@SortColumn` | `nvarchar(128) = NULL` | Output column to sort by; validated against a whitelist, falls back to the source procedure's default ORDER BY. |
| `@SortOrder` | `nvarchar(4) = NULL` | `ASC` / `DESC` (default `ASC`). |
| `@PageIndex` | `int = NULL` | Zero-based page index. |
| `@PageSize` | `int = NULL` | `> 0` → one page via OFFSET/FETCH; `NULL`/`<= 0` → **all rows** (for Excel export). |

Output = the report columns **plus** `COUNT(*) OVER() AS TotalCount`.

Sorting must be a **whitelist** (column name → real column) to avoid SQL
injection, with a deterministic tie-breaker (e.g. a unique/registration column)
so paging is stable. Pass filter values as parameters to `sp_executesql`; only
the validated column/direction are concatenated into the SQL text.

See [`StoredProcedureMigrations/sp_PaThaKaReport_pagination.sql`](../StoredProcedureMigrations/sp_PaThaKaReport_pagination.sql)
for the canonical implementation.

---

## Step-by-step

### 1. Write the new procedure
- Copy the source procedure's SELECT/JOIN/WHERE/ORDER BY exactly.
- Add `COUNT(*) OVER() AS TotalCount` to the projection.
- Add the whitelisted `ORDER BY <col> <dir>, <tiebreaker> <dir>`.
- Wrap in `sp_executesql`; append OFFSET/FETCH only when `@PageSize > 0`.
- Save as `StoredProcedureMigrations/sp_<name>_pagination.sql` with
  `CREATE OR ALTER PROCEDURE [dbo].[sp_<name>_pagination]`.

### 2. Deploy to the dev DB and test
Apply directly (dev) and verify behaviour with `pymssql`:
```python
import pymssql, re
sql = re.sub(r'\n\s*GO\s*$', '\n', open('StoredProcedureMigrations/sp_<name>_pagination.sql').read(), flags=re.I)
conn = pymssql.connect(server='203.81.66.111', port='14330', user='sa',
                       password='<see appsettings>', database='TradeNetDB', timeout=60)
cur = conn.cursor(as_dict=True)
cur.execute(sql); conn.commit()
# page 0 vs page 1 differ; TotalCount present; NULL page size returns all rows
```
Confirm: page 0 and page 1 return different rows, every row carries `TotalCount`,
and the all-rows mode (`NULL` page size) row count equals the original
procedure's row count for the same filters.

### 3. Replace the LINQ data layer with an EXEC call
In `Backend/StoredProcedureToLinq/sp_<name>.cs`:
- Keep the `…Request` and `…Result` classes.
- Add a `…Row` class = `…Result` fields **plus** `int TotalCount`, with a
  `ToResult()` projection.
- Remove the LINQ `Query(...)` method; add:
```csharp
public static async Task<List<sp_<name>Row>> ExecuteAsync(
    TradeNetDbContext db, sp_<name>Request request,
    string? sortColumn = null, string? sortOrder = null,
    int? pageIndex = null, int? pageSize = null)
{
    var parameters = new[] { new SqlParameter("@FromDate", request.FromDate), /* ... */
        new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
        new SqlParameter("@SortOrder",  (object?)sortOrder  ?? DBNull.Value),
        new SqlParameter("@PageIndex",  (object?)pageIndex  ?? DBNull.Value),
        new SqlParameter("@PageSize",   (object?)pageSize   ?? DBNull.Value) };
    const string sql = "EXEC dbo.sp_<name>_pagination @FromDate, /* ... */, @SortColumn, @SortOrder, @PageIndex, @PageSize";
    return await db.Database.SqlQueryRaw<sp_<name>Row>(sql, parameters).ToListAsync();
}
```
`SqlQueryRaw<T>` maps result columns to `T` by name — the `…Row` property names
must match the procedure's output columns (incl. `TotalCount`). Needs
`using Microsoft.Data.SqlClient;` and `using Microsoft.EntityFrameworkCore;`.

> If more than one controller shares the source procedure, they share the one
> `_pagination` procedure and the one `ExecuteAsync` — convert them together.

### 4. Rewrite the controller
- `Post`: clamp `pageIndex`/`pageSize` (default 10, max 1000), normalize blank
  sort to `null`, call `ExecuteAsync(..., pageIndex, pageSize)`, read
  `totalCount = rows.Count > 0 ? rows[0].TotalCount : 0`, map rows to results,
  and return `ApiResult<…Result>.CreatePageFromRows(data, totalCount, …)`.
- `Excel`: call `ExecuteAsync(..., pageIndex: null, pageSize: null)` (all rows),
  guard the Excel row cap, then `ExcelGenerator.CreateWorkbook(data, "<sheet>")`.

See [`Backend/Controllers/Report/PaThaKaRegisteredBusinessOrganizationReportController.cs`](../Backend/Controllers/Report/PaThaKaRegisteredBusinessOrganizationReportController.cs).

### 5. Build and verify
- `dotnet build Backend/API.csproj` (use a separate `-p:OutputPath` if the dev
  server is locking `bin`).
- Run the report from the UI: paging, total count, and Excel export.

### 6. Record it
- Tick the procedure and its controller(s) in the tracker below.
- Commit the `StoredProcedureMigrations/*.sql` so it ships to production.

---

## Deployment summary

| Environment | How the new procedure gets there |
| --- | --- |
| **Development** | Applied **directly** to the dev DB (`TradeNetDB` on `203.81.66.111,14330`) via `pymssql` and **tested**, as part of doing the conversion. |
| **Production** | Run the `StoredProcedureMigrations/*.sql` files through the normal release process. They are `CREATE OR ALTER` and only **add** new `_pagination` procedures — existing procedures are never altered. |

---

## Frontend filter boxes — reference the OLD MVC app

When converting a report, also align its **frontend filter boxes** to the old
ASP.NET MVC app. The old app is the source of truth for what each filter is and
what values its dropdowns offer.

**Old app location:** `/Users/saobranaung/Code/Ministry of Commerce/tradenet-2.0-admin/TradenetAdmin`
- **Views:** `Views/Reports/*.cshtml` — the filter form at the top of each report
  view (look for `@Html.DropDownListFor`, `@Html.TextBoxFor`, radio inputs).
- **Controller:** `Controllers/ReportsController.cs` — the GET/POST action for each
  report shows where each dropdown's options come from (a fixed list, an enum, or
  a DB lookup repository).

Because the old app is MVC, **check both the View and the Controller** — the View
shows the field + default option label (e.g. `"--- All ---"`), the Controller
shows the data source (e.g. `StateRepository.GetAll()`).

### Rules
- **Match the select VALUES to the old MVC**, not just the labels. A filter sent
  to the stored procedure must use the same string the procedure compares against
  (e.g. `State` is filtered by region **Name**, `Status` by **Code**, `Type` by
  `valid`/`invalid`, `ApplyType` by `New`/`Amend`/…).
- **DB-lookup dropdowns are automatic.** In the new frontend, any filter whose
  `name` ends with `Id` **and** is registered in `idFilterLookups`
  (`Frontend/src/Report/Page/GenericReportPage.tsx`) renders as a searchable
  lookup `<Select>` with an `All` (value `0`) option — no per-report config
  needed. Covers `BusinessTypeId`, `LineofBusinessId`, `NRCPrefixId`,
  `NRCPrefixCodeId`, `ChequeNoId`, country/section/method ids, etc. Keep the
  filter `name` exactly matching the lookup key.
- **Fixed-list dropdowns use `type: 'select'`** with an explicit `options` array
  (`{ label, value }[]`) in `reportConfigs.ts`. `--- All ---` uses value `''`.
  The `select` renderer lives in `renderFilter` in `GenericReportPage.tsx`.
- Use shared option constants for values reused across reports (e.g.
  `pathakaStateFilterOptions`, `pathakaStatusFilterOptions`).
- Scope edits to the target report only — many reports share an identical filter
  block, so anchor edits on unique surrounding context (don't `replace_all`).

### PaThaKa-category filter sources (from old MVC, already applied)
| Filter | Old MVC source | New frontend representation |
| --- | --- | --- |
| `BusinessTypeId` | `BusinessTypeRepository.GetAll(PaThaKa)` (Id→Name) | auto lookup `businessTypes` |
| `LineofBusinessId` | `LineofBusinessRepository.GetAll()` (Id→Name) | auto lookup `lineofBusinesses` |
| `NRCPrefixId` / `NRCPrefixCodeId` | `NrcPrefixRepository` / `NrcPrefixCodeRepository` | auto lookups `nrcprefixes` / `nrcprefixCodes` |
| `State` | `StateRepository.GetAll()` (Name) | `select` → `pathakaStateFilterOptions` (18 regions + `--- All ---`) |
| `Status` | `PaThaKaStatusRepository.GetAll()` (Code) | `select` → `pathakaStatusFilterOptions` (Suspension, Extension, Un_suspension, Blacklist, New, Amend, test + `--- All ---`) |
| `Type` (valid/invalid) | fixed list `Valid`/`Invalid` | `select` values `valid`/`invalid`, default `valid` |
| `ApplyType` | `CommonRepository.GetApplyTypeList()` minus Fine, plus De-Cancel | `select`: New, Amend, Extension, Cancel, Actual Amend, De-Cancel (default New) |
| `PaymentType` | `PaymentTypeRepository.GetAll()` (Name) | `select`: Cash, MPU, Citizen Pay + `--- All ---` |
| `FromDate`/`ToDate` | text date inputs | `dateRange` |
| `CompanyRegistrationNo` / `Name` / `Nationality` / `NRCNo` | text inputs | `text` |

> `PaymentType` values were read from the live `PaymentType` table (Cash, MPU,
> Citizen Pay). If a future report's lookup table is large or changes often,
> prefer registering a real lookup in `idFilterLookups` over a static list.

---

## Conversion tracker

Status: ✅ Done · ⬜ To Do.

Grouped by source stored procedure → new `_pagination` procedure. Converting one
group converts every controller that shares that procedure.

| ✓ | Source procedure | New procedure | Controller(s) |
| --- | --- | --- | --- |
| ✅ | `sp_PaThaKaReport` | `sp_PaThaKaReport_pagination` | PaThaKaRegisteredBusinessOrganizationReport, ListOfTopCapitalCompany |
| ✅ | `sp_PaThaKaValidInvalidReport` | `sp_PaThaKaValidInvalidReport_pagination` | ListOfValidAndInvalidCompany |
| ✅ | `sp_PaThaKaAllReport` | `sp_PaThaKaAllReport_pagination` | ListOfCompany |
| ✅ | `sp_DirectorListReport` | `sp_DirectorListReport_pagination` | ListOfDirectors, ListOfDirectorsByCompanyRegistrationNo |
| ✅ | `sp_CardListsByPaThaKaReport` | `sp_CardListsByPaThaKaReport_pagination` | CardListsByCompanyRegistrationNumber |
| ✅ | `sp_CompanyProfileReport` | `sp_CompanyProfileReport_pagination` | CompanyProfile |
| ✅ | `sp_PathakaBindReport` | `sp_PathakaBindReport_pagination` | EIRCardBindReport |
| ✅ | `sp_PaThaKaByBusinessTypeReport` | `sp_PaThaKaByBusinessTypeReport_pagination` | RegistrationByBusinessType |
| ✅ | `sp_PaThaKaRegistrationReport` | `sp_PaThaKaRegistrationReport_pagination` | RegistrationByVoucher |
| ⬜ | `sp_AccountSummaryReport` | `sp_AccountSummaryReport_pagination` | AccountSummaryReport |
| ⬜ | `sp_ActualAmendReport` | `sp_ActualAmendReport_pagination` | BorderExportLicenceActualAmendmentReport, BorderExportPermitActualAmendmentReport, BorderImportLicenceActualAmendmentReport, BorderImportPermitActualAmendmentReport, ExportLicenceActualAmendmentReport, ExportPermitActualAmendmentReport, ImportLicenceActualAmendmentReport, ImportPermitActualAmendmentReport |
| ⬜ | `sp_AmendReport` | `sp_AmendReport_pagination` | BorderExportLicenceAmendmentReport, BorderExportPermitAmendmentReport, BorderImportLicenceAmendmentReport, BorderImportPermitAmendmentReport, ExportLicenceAmendmentReport, ExportPermitAmendmentReport, ImportLicenceAmendmentReport, ImportPermitAmendmentReport |
| ⬜ | `sp_HSCodeReport` | `sp_HSCodeReport_pagination` | BorderExportLicenceByHSCodeReport, BorderExportPermitByHSCodeReport, BorderImportLicenceByHSCodeReport, BorderImportPermitByHSCodeReport, ExportLicenceByHSCodeReport, ExportPermitByHSCodeReport, ImportLicenceByHSCodeReport, ImportPermitByHSCodeReport |
| ⬜ | `sp_ExportLicenceDetailReport_Fast` | `sp_ExportLicenceDetailReport_Fast_pagination` | BorderExportLicenceByMethodReport, BorderExportLicenceBySectionReport, BorderExportLicenceBySellerCountryReport, BorderExportLicenceCompanyListReport, BorderExportLicenceDailyReportNewLicenceReport, BorderExportLicenceDetailReport, BorderExportLicenceTotalValueLicencesReport, ExportLicenceByMethodReport, ExportLicenceBySectionReport, ExportLicenceBySellerCountryReport, ExportLicenceCompanyListReport, ExportLicenceDailyReportNewLicenceReport, ExportLicenceDetailReport, ExportLicenceTotalValueLicencesReport |
| ⬜ | `sp_ImportLicenceDetailReport_Fast` | `sp_ImportLicenceDetailReport_Fast_pagination` | BorderImportLicenceByMethodReport, BorderImportLicenceBySectionReport, BorderImportLicenceBySellerCountryReport, BorderImportLicenceCompanyListReport, BorderImportLicenceDailyReportNewLicenceReport, BorderImportLicenceDetailReport, BorderImportLicenceTotalValueLicencesReport, ImportLicenceByMethodReport, ImportLicenceBySectionReport, ImportLicenceBySellerCountryReport, ImportLicenceCompanyListReport, ImportLicenceDailyReportNewLicenceReport, ImportLicenceDetailReport, ImportLicenceTotalValueLicencesReport |
| ⬜ | `sp_ExportPermitDetailReport_Fast` | `sp_ExportPermitDetailReport_Fast_pagination` | BorderExportPermitBySectionReport, BorderExportPermitBySellerCountryReport, BorderExportPermitCompanyListReport, BorderExportPermitDailyReportNewPermitReport, BorderExportPermitDetailReport, ExportPermitBySectionReport, ExportPermitBySellerCountryReport, ExportPermitCompanyListReport, ExportPermitDailyReportNewPermitReport, ExportPermitDetailReport |
| ⬜ | `sp_ImportPermitDetailReport_Fast` | `sp_ImportPermitDetailReport_Fast_pagination` | BorderImportPermitBySectionReport, BorderImportPermitBySellerCountryReport, BorderImportPermitCompanyListReport, BorderImportPermitDailyReportNewPermitReport, BorderImportPermitDetailReport, ImportPermitBySectionReport, ImportPermitBySellerCountryReport, ImportPermitCompanyListReport, ImportPermitDailyReportNewPermitReport, ImportPermitDetailReport |
| ⬜ | `sp_CancelReport` | `sp_CancelReport_pagination` | BorderExportLicenceCancellationReport, BorderExportPermitCancellationReport, BorderImportLicenceCancellationReport, BorderImportPermitCancellationReport, ExportLicenceCancellationReport, ExportPermitCancellationReport, ImportLicenceCancellationReport, ImportPermitCancellationReport |
| ⬜ | `sp_ExtensionReport` | `sp_ExtensionReport_pagination` | BorderExportLicenceExtensionReport, BorderExportPermitExtensionReport, BorderImportLicenceExtensionReport, BorderImportPermitExtensionReport, ExportLicenceExtensionReport, ExportPermitExtensionReport, ImportLicenceExtensionReport, ImportPermitExtensionReport |
| ⬜ | `sp_NewReport` | `sp_NewReport_pagination` | BorderExportLicenceNewReportNewReport, BorderExportPermitNewReportNewReport, BorderImportLicenceNewReportNewReport, BorderImportPermitNewReportNewReport, ExportLicenceNewReportNewReport, ExportPermitNewReportNewReport, ImportLicenceNewReportNewReport, ImportPermitNewReportNewReport |
| ⬜ | `sp_VoucherReport` | `sp_VoucherReport_pagination` | BorderExportLicenceVoucherReport, BorderExportPermitVoucherReport, BorderImportLicenceVoucherReport, BorderImportPermitVoucherReport, ExportLicenceVoucherReport, ExportPermitVoucherReport, ImportLicenceVoucherReport, ImportPermitVoucherReport |
| ⬜ | `sp_ImportLicencePendingDetailReport_Fast` | `sp_ImportLicencePendingDetailReport_Fast_pagination` | BorderImportLicenceDetailReportPending, ImportLicenceDetailReportPending |
| ⬜ | `sp_PendingReport` | `sp_PendingReport_pagination` | BorderImportLicencePendingReport, ImportLicencePendingReport |
| ⬜ | `sp_ChequeNoReport` | `sp_ChequeNoReport_pagination` | ChequeNoReport |
| ⬜ | `sp_MPUReport` | `sp_MPUReport_pagination` | MPUReport |
| ⬜ | `sp_MPUReport_V3` | `sp_MPUReport_V3_pagination` | MPUReportV3 |
| ⬜ | `sp_MemberRegistrationReport` | `sp_MemberRegistrationReport_pagination` | MemberRegistrationReport |
| ⬜ | `sp_OnlineFeesReport` | `sp_OnlineFeesReport_pagination` | OnlineFeesReport |

**Totals:** 28 source procedures · 125 controllers. Done: 9 procedures / 11
controllers (all PaThaKa-category reports). Remaining: 19 procedures / 114
controllers.

> Notes
> - `*DetailReport_Fast` procedures back several report variants (ByMethod,
>   BySection, BySellerCountry, CompanyList, DailyReport, Detail, TotalValue) that
>   differ only by frontend column selection — one `_pagination` procedure serves
>   them all.
> - Confirm each controller's actual converter type in
>   `Backend/StoredProcedureToLinq/` before converting; a few reports point at a
>   `_Fast`/variant class rather than the same-named procedure.

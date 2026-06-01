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
5. **The procedure returns the page + an *opt-in* `TotalCount`.** The total is
   computed **only when `@IncludeTotalCount = 1`**, as a *separate scalar*
   `(SELECT COUNT(*) <base>)` over the un-paged base — **never** `COUNT(*) OVER()`
   on the hot path (see "Performance" below). When `@IncludeTotalCount = 0` the
   proc fetches `@PageSize + 1` rows (a sentinel to detect a next page) and
   returns `TotalCount = NULL`.
6. **Keep the source SQL semantics identical** — copy the original SELECT / JOINs
   / WHERE verbatim, then add only sorting + paging + `TotalCount`.

---

## Performance — page-first + fast pagination (READ THIS)

The whole point of pagination here is **loading time**, not just a tidy grid.
Two traps make a "paginated" proc *slower* than the original, both observed on
the Import Licence family:

1. **`COUNT(*) OVER()` is catastrophic.** Windowing the count over the result
   forces SQL Server to scan/materialize *every* matching row on every page.
   Measured on `sp_ImportLicenceDetailReport`: **146 s**. Compute the total as a
   **separate scalar** over the un-paged base, and only when asked
   (`@IncludeTotalCount = 1`).
2. **Correlated select-in-select runs per row.** A `FOR XML PATH('')` country
   resolver or `(SELECT TOP 1 …)` currency/amount subquery in the SELECT list
   runs once **per output row**. Over a full result set that dominates runtime.

**Page-first pattern:** page the base query (joins + WHERE, **no subqueries**)
with `ORDER BY … OFFSET/FETCH` in a derived table `pg`, then run any correlated
subquery **only on the ~10 page rows** in the outer SELECT. Detail page-1 went
**25 s → ~2 s** this way.

**The frontend always sends `includeTotalCount: false` and no `sortColumn`**
(see `BasicTable.tsx`). So the hot path must:

- thread `@IncludeTotalCount` all the way through: `ExecuteAsync(..., bool
  includeTotalCount)` → `new SqlParameter("@IncludeTotalCount", …)` →
  `… , @IncludeTotalCount` in the `EXEC` string, and the controller passes
  `request.IncludeTotalCount`;
- branch the result: `request.IncludeTotalCount ? CreatePageFromRows(data,
  rows[0].TotalCount ?? 0, …) : CreateFastPageFromRows(data, …)` (the proc
  already returned `PageSize + 1` rows for the fast branch);
- make `Row.TotalCount` **`int?`** — the fast branch returns `NULL` and EF's
  `SqlQueryRaw` throws mapping `DBNull → int`.

> A proc that defaults `@IncludeTotalCount = 1` but whose `ExecuteAsync` never
> passes it will run the 146 s COUNT on every page. That was the original
> "pagination is slower" bug.

**Cache-bound reference columns (country names).** Resolving a CSV of country
ids to names is pure reference data → resolve it in C# from the one-day
`ReportLookupCache` (`GetCountryNamesAsync` + `ResolveCsv`) **after**
materialization, never as a per-row SQL subquery. The Detail report uses the
`_Fast` LINQ path (`sp_ImportLicenceDetailReport_Fast.CreatePagedResultAsync`)
which pages base rows with no select-in-select and binds country names from the
cache; the 6 aggregate Detail reports share the same `_Fast` source.

Measured fast-path (one month, `@IncludeTotalCount = 0`): Amend 0.20 s, Cancel
0.36 s, Extension 0.42 s, Pending 0.49 s, Voucher 3.65 s. Voucher's remaining
cost is its `ORDER BY ApplicationNo, LicenceNo` sort over the matching payment
rows (an index question), not COUNT or subqueries.

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

## Two flavours of conversion

There are **two** procedure shapes, and they need different recipes.

### A. Simple single-shape procedures (the PaThaKa recipe)
One procedure → one result-set shape regardless of parameters. The original
SELECT/JOIN/WHERE can be copied verbatim into the `_pagination` proc and wrapped
with a whitelisted `ORDER BY` + OFFSET/FETCH + `COUNT(*) OVER()`. This is the
"Step-by-step" recipe above. All 9 PaThaKa procedures were done this way.

### B. Multi-form / branchy procedures (the INSERT-EXEC recipe)
Some procedures (`sp_ActualAmendReport`, `sp_AmendReport`, `sp_HSCodeReport`,
`sp_CancelReport`, `sp_ExtensionReport`, `sp_NewReport`, `sp_VoucherReport`, …)
are **multi-form**: they take a `@FormType` (`'Import Licence'`, `'Export
Licence'`, `'Import Permit'`, …) and/or `@Type` (`'Oversea'`/`'Border'`) and
branch with `IF` + `UNION ALL` into different SELECTs. Measured shape of these
procs: ~16–32 KB of body, 2–6 `UNION ALL`, 7–10 `IF` branches, ~32 SELECTs each.

Copying their SELECT verbatim is impractical and fragile. Instead, **reuse the
original proc whole** with an INSERT-EXEC wrapper:

```sql
CREATE OR ALTER PROCEDURE [dbo].[sp_<name>_pagination]
    @FormType nvarchar(100) = NULL, /* …all original params… */,
    @SortColumn nvarchar(128) = NULL, @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL, @PageSize int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    CREATE TABLE #r ( /* exact columns the original proc returns for THIS form */ );
    INSERT INTO #r EXEC dbo.sp_<name> @FormType, /* …original params… */;
    SELECT *, COUNT(*) OVER() AS TotalCount
    FROM #r
    ORDER BY
        CASE WHEN @SortColumn = N'Col1' AND @SortOrder = N'ASC'  THEN ... END,
        /* …whitelist… */
        [<unique tiebreaker>]
    OFFSET (ISNULL(@PageIndex,0) * ISNULL(NULLIF(@PageSize,0), 2147483647)) ROWS
    FETCH NEXT ISNULL(NULLIF(@PageSize,0), 2147483647) ROWS ONLY;
END
```

INSERT-EXEC rules / gotchas:
- The `#r` column list **must exactly match** (count, order, types) the columns
  the original proc returns for the form you call it with — INSERT-EXEC binds by
  position, and a mismatch throws `Column name or number of supplied values does
  not match table definition`.
- INSERT-EXEC **cannot be nested**: if the original proc itself does INSERT-EXEC
  (or `SET FMTONLY`-incompatible work), this fails. None of the Import Licence
  procs nest, so it's safe there — verify per proc.
- Because the wrapper calls the original proc unchanged, results match the
  original **by construction** — no re-implementation risk.

---

## Discovering a branchy proc's output columns (introspection caveat)

To build `#r` you need the exact output columns. Two ways to get them, with
gotchas learned the hard way:

**1. Live EXEC introspection (works, but finicky).** EXEC the proc with the
target form's params via `pymssql` and read the column names — but:
- `cursor.description` is often `None` **immediately after `execute()`** for
  these procs (the proc's first statement isn't the result SELECT). You must
  iterate/`fetchall()` the result set before column metadata appears. An early
  "all procs returned `None`" pass was this mistake.
- **Big procs time out.** `sp_HSCodeReport` (the largest, ~32 KB / 6 UNION ALL)
  timed out and killed the connection over a **full-year** range. Use a **1-day**
  date window — only column metadata is needed, not rows.
- `sys.dm_exec_describe_first_result_set` returns nothing useful when the first
  statement isn't the result SELECT or the proc branches.

**2. Read the proc body (reliable fallback).**
1. Pull the definition: `SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.sp_<name>'))`.
2. Find the `IF`/`UNION ALL` branch matching the call's `@FormType`/`@Type`.
3. Transcribe that branch's final SELECT column list (aliases = output names)
   into the `#r` table, matching the underlying column types.

Either way, **validate by running the wrapper on the dev DB** — a real
INSERT-EXEC throws immediately if the `#r` shape is wrong.

### Captured import-form output shapes (FormType=`Import Licence`, Type=`Oversea`)
| Proc | Out cols | Columns |
| --- | --- | --- |
| `sp_ActualAmendReport` | 16 | Date, SectionCode, SectionName, OldLicenceNo, LicenceNo, sDate, CompanyRegistrationNo, CompanyName, UnitLevel, StreetNumberStreetName, QuarterCityTownship, State, Country, PostalCode, Currency, Amount |
| `sp_AmendReport` | 16 | (identical to `sp_ActualAmendReport`) |
| `sp_PendingReport` | 13 | Status, ApplyType, ApplicationDate, ApplicationNo, SectionCode, SectionName, CompanyRegistrationNo, CompanyName, Currency, AdditionalDescription, Amount, CommodityType, HSCode |
| `sp_HSCodeReport`, `sp_CancelReport`, `sp_ExtensionReport`, `sp_NewReport`, `sp_VoucherReport` | TBD | introspect with 1-day window or read proc body |

---

## `_Fast` procedures are LINQ-only — they do NOT exist in the DB

Several data-layer classes are named `…DetailReport_Fast` (e.g.
`sp_ImportLicenceDetailReport_Fast.cs`). **These are hand-written LINQ
implementations with NO matching stored procedure.** Confirmed:
`sp_ImportLicenceDetailReport_Fast` and `sp_ImportLicencePendingDetailReport_Fast`
are **MISSING** from the DB, while the real `sp_ImportLicenceDetailReport` and
`sp_ImportLicencePendingDetailReport` **EXIST**.

Implication: converting a `_Fast`-backed report to "use the stored procedure"
means wiring it to the **real** proc (`sp_ImportLicenceDetailReport`, etc.). The
real proc is the authoritative source, but its output can differ from the
`_Fast` LINQ result — that divergence is the whole reason for the migration.
**Flag this behaviour change to the user before converting `_Fast` reports.**

---

## Dual-path coexistence for shared multi-family procs

The branchy procs are shared across **many** report families — the same
`sp_AmendReport` backs Import Licence, Export Licence, Import Permit, Export
Permit, and their Border variants (8 controllers each). If you delete the LINQ
`Query(...)` to convert just one family, every sibling controller stops
compiling (CS0117) and you're forced to convert the entire cross-product at once.

To convert one family at a time **without** the cascade:
- **Keep** the existing LINQ `Query(...)` method on the shared data-layer class.
- **Add** a parallel `ExecuteAsync(...)` (calling the `_pagination` wrapper)
  alongside it.
- Wire only the target family's controllers to `ExecuteAsync`; leave the rest on
  `Query`. They keep building and behaving exactly as before.
- This dual path is **intentional and temporary** — remove `Query` once the last
  family on that proc is converted.

Contrast with single-family procs (PaThaKa), where `Query` is removed outright
because no other controller shares the proc.

---

## Conversion tracker

Status: ✅ Done · 🟡 Partial (one report family converted; LINQ `Query` kept for the rest) · ⬜ To Do.

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
| 🟡 | `sp_ActualAmendReport` | `sp_ActualAmendReport_pagination` | BorderExportLicenceActualAmendmentReport, BorderExportPermitActualAmendmentReport, BorderImportLicenceActualAmendmentReport, BorderImportPermitActualAmendmentReport, ExportLicenceActualAmendmentReport, ExportPermitActualAmendmentReport, ImportLicenceActualAmendmentReport, ImportPermitActualAmendmentReport |
| 🟡 | `sp_AmendReport` | `sp_AmendReport_pagination` | BorderExportLicenceAmendmentReport, BorderExportPermitAmendmentReport, BorderImportLicenceAmendmentReport, BorderImportPermitAmendmentReport, ExportLicenceAmendmentReport, ExportPermitAmendmentReport, ImportLicenceAmendmentReport, ImportPermitAmendmentReport |
| 🟡 | `sp_HSCodeReport` | `sp_HSCodeReport_pagination` | BorderExportLicenceByHSCodeReport, BorderExportPermitByHSCodeReport, BorderImportLicenceByHSCodeReport, BorderImportPermitByHSCodeReport, ExportLicenceByHSCodeReport, ExportPermitByHSCodeReport, ImportLicenceByHSCodeReport, ImportPermitByHSCodeReport |
| ⬜ | `sp_ExportLicenceDetailReport_Fast` | `sp_ExportLicenceDetailReport_Fast_pagination` | BorderExportLicenceByMethodReport, BorderExportLicenceBySectionReport, BorderExportLicenceBySellerCountryReport, BorderExportLicenceCompanyListReport, BorderExportLicenceDailyReportNewLicenceReport, BorderExportLicenceDetailReport, BorderExportLicenceTotalValueLicencesReport, ExportLicenceByMethodReport, ExportLicenceBySectionReport, ExportLicenceBySellerCountryReport, ExportLicenceCompanyListReport, ExportLicenceDailyReportNewLicenceReport, ExportLicenceDetailReport, ExportLicenceTotalValueLicencesReport |
| 🟡 | `sp_ImportLicenceDetailReport` (real proc; `_Fast` was LINQ-only) | `sp_ImportLicenceDetailReport_pagination` | BorderImportLicenceByMethodReport, BorderImportLicenceBySectionReport, BorderImportLicenceBySellerCountryReport, BorderImportLicenceCompanyListReport, BorderImportLicenceDailyReportNewLicenceReport, BorderImportLicenceDetailReport, BorderImportLicenceTotalValueLicencesReport, ImportLicenceByMethodReport, ImportLicenceBySectionReport, ImportLicenceBySellerCountryReport, ImportLicenceCompanyListReport, ImportLicenceDailyReportNewLicenceReport, ImportLicenceDetailReport, ImportLicenceTotalValueLicencesReport |
| ⬜ | `sp_ExportPermitDetailReport_Fast` | `sp_ExportPermitDetailReport_Fast_pagination` | BorderExportPermitBySectionReport, BorderExportPermitBySellerCountryReport, BorderExportPermitCompanyListReport, BorderExportPermitDailyReportNewPermitReport, BorderExportPermitDetailReport, ExportPermitBySectionReport, ExportPermitBySellerCountryReport, ExportPermitCompanyListReport, ExportPermitDailyReportNewPermitReport, ExportPermitDetailReport |
| ⬜ | `sp_ImportPermitDetailReport_Fast` | `sp_ImportPermitDetailReport_Fast_pagination` | BorderImportPermitBySectionReport, BorderImportPermitBySellerCountryReport, BorderImportPermitCompanyListReport, BorderImportPermitDailyReportNewPermitReport, BorderImportPermitDetailReport, ImportPermitBySectionReport, ImportPermitBySellerCountryReport, ImportPermitCompanyListReport, ImportPermitDailyReportNewPermitReport, ImportPermitDetailReport |
| 🟡 | `sp_CancelReport` | `sp_CancelReport_pagination` | BorderExportLicenceCancellationReport, BorderExportPermitCancellationReport, BorderImportLicenceCancellationReport, BorderImportPermitCancellationReport, ExportLicenceCancellationReport, ExportPermitCancellationReport, ImportLicenceCancellationReport, ImportPermitCancellationReport |
| 🟡 | `sp_ExtensionReport` | `sp_ExtensionReport_pagination` | BorderExportLicenceExtensionReport, BorderExportPermitExtensionReport, BorderImportLicenceExtensionReport, BorderImportPermitExtensionReport, ExportLicenceExtensionReport, ExportPermitExtensionReport, ImportLicenceExtensionReport, ImportPermitExtensionReport |
| 🟡 | `sp_NewReport` | `sp_NewReport_pagination` | BorderExportLicenceNewReportNewReport, BorderExportPermitNewReportNewReport, BorderImportLicenceNewReportNewReport, BorderImportPermitNewReportNewReport, ExportLicenceNewReportNewReport, ExportPermitNewReportNewReport, ImportLicenceNewReportNewReport, ImportPermitNewReportNewReport |
| 🟡 | `sp_VoucherReport` | `sp_VoucherReport_pagination` | BorderExportLicenceVoucherReport, BorderExportPermitVoucherReport, BorderImportLicenceVoucherReport, BorderImportPermitVoucherReport, ExportLicenceVoucherReport, ExportPermitVoucherReport, ImportLicenceVoucherReport, ImportPermitVoucherReport |
| ✅ | `sp_ImportLicencePendingDetailReport` (real proc; `_Fast` was LINQ-only) | `sp_ImportLicencePendingDetailReport_pagination` | BorderImportLicenceDetailReportPending, ImportLicenceDetailReportPending |
| 🟡 | `sp_PendingReport` | `sp_PendingReport_pagination` | BorderImportLicencePendingReport, ImportLicencePendingReport |
| ⬜ | `sp_ChequeNoReport` | `sp_ChequeNoReport_pagination` | ChequeNoReport |
| ⬜ | `sp_MPUReport` | `sp_MPUReport_pagination` | MPUReport |
| ⬜ | `sp_MPUReport_V3` | `sp_MPUReport_V3_pagination` | MPUReportV3 |
| ⬜ | `sp_MemberRegistrationReport` | `sp_MemberRegistrationReport_pagination` | MemberRegistrationReport |
| ⬜ | `sp_OnlineFeesReport` | `sp_OnlineFeesReport_pagination` | OnlineFeesReport |

**Totals:** 28 source procedures · 125 controllers. Done: 9 PaThaKa procedures /
11 controllers + the **Import Licence menu (16 controllers)** across 10 procedures
(those 10 procedures' other report families remain on LINQ — see 🟡 rows).

### Import Licence menu — converted (16 controllers)

All 16 `ImportLicence*` controllers now read from `*_pagination` wrappers (INSERT-EXEC
over the untouched originals). The shared multi-form procs keep their LINQ `Query`
for the not-yet-converted Export/Permit/Border families (**dual-path**).

**Row reports (9)** — controller → `ExecuteAsync` → `CreatePageFromRows` + Excel:
ImportLicenceActualAmendmentReport (`sp_ActualAmendReport`), ImportLicenceAmendmentReport
(`sp_AmendReport`), ImportLicenceCancellationReport (`sp_CancelReport`),
ImportLicenceExtensionReport (`sp_ExtensionReport`), ImportLicenceNewReportNewReport
(`sp_NewReport`), ImportLicenceVoucherReport (`sp_VoucherReport`), ImportLicencePendingReport
(`sp_PendingReport`), ImportLicenceDetailReport (`sp_ImportLicenceDetailReport`),
ImportLicenceDetailReportPending (`sp_ImportLicencePendingDetailReport`).

**Aggregate reports (7)** — keep `ReportAggregationService` but source `AggregateSourceRow`
from the real proc instead of LINQ:
- ByMethod, BySection, BySellerCountry, CompanyList, DailyReport, TotalValue — converted by
  swapping the source in `sp_ImportLicenceDetailReport_Fast.AggregateSourceRowsAsync` to
  `sp_ImportLicenceDetailReport.ExecuteAsync` (import-only class → no cascade).
- ByHSCode — `sp_HSCodeReport` is shared across all 8 families, so a **dedicated** proc path
  (`ExecuteAsync` + `CreateAggregateResultFromProcAsync` / `…ExcelWorkbookFromProcAsync`) was
  added and only `ImportLicenceByHSCodeReportController` repointed to it; the other 7 families
  keep the LINQ `CreateAggregateResultAsync`.

**Verification:** all 10 wrappers row-count-matched their originals over real date ranges
(e.g. HSCode 60,915, Extension 1,744, New 5,086, Pending 57) with correct inline `TotalCount`
and working paging. Backend builds 0 errors; frontend typechecks 0 errors.

**Behaviour change to note:** the detail/aggregate reports previously ran hand-written `_Fast`
LINQ (no DB proc existed); they now reflect stored-procedure truth, so their numbers may shift
to match the real procs. Dropped LINQ-only columns not returned by the procs (e.g. `Sakhan*`,
some `HSCode`) now come back null.

#### ImportLicenceVoucherReport — indexed-view optimization + UI alignment

`sp_VoucherReport_pagination` for the **Import Licence** form was moved off the generic
INSERT-EXEC wrapper to a **direct page-first** query: the base joins+WHERE are paged via OFFSET/FETCH inside a
`pg` derived table, then per-licence `Currency` / `TotalAmount` are resolved on the ~PageSize
page rows only.

- **Correlated subqueries → indexed view.** The original per-row `(SELECT TOP 1 currency.Code …)`
  and `(SELECT SUM(Amount) …)` over `ImportLicenceItem` are now two `OUTER APPLY` joins to the
  **materialized** view `vw_ImportLicenceItemTotalByCurrency` (grouped by `(ImportLicenceId,
  CurrencyId)`) with `WITH (NOEXPAND)`. `OUTER APPLY` (not a plain JOIN) keeps one row per
  licence — the view is per-currency, so a JOIN would multiply rows for multi-currency licences.
- **View must be materialized.** The view ships in
  `StoredProcedureMigrations/Views/vw_ImportLicenceItemTotalByCurrency.sql` **with** its unique
  clustered index `IX_vw_…(ImportLicenceId, CurrencyId)`. `NOEXPAND` errors at runtime if the
  index is missing — deploy the index, not just the view definition. (Creating it needs
  `ARITHABORT`/`QUOTED_IDENTIFIER`/`ANSI_*` ON in the session.)
- **Verified** on dev DB: NOEXPAND resolves (view = 768,062 rows), proc ~1.8s on a 1-month
  window (fast path), and view-based `Currency`/`TotalAmount` match the original subquery logic
  exactly (4/4 cross-check).

**Frontend filter boxes + columns** (`reportConfigs.ts` → `ImportLicenceVoucherReport`), aligned
to old MVC `Views/Reports/ImportLicenceVoucherReport.cshtml` + `ReportControl/VoucherReport.rdlc`:

- Filters: From/To Date, **Import Section** (`ExportImportSectionId` → auto lookup
  `exportImportSections`), **Apply Type**, **Payment Type**, **Company Registration No**.
  Removed the bogus `FormType` text box (controller hardcodes `Import Licence`) and `SakhanId`
  (Sakhan is Border-only).
- Columns: cleaned single licence-no column; dropped RDLC junk titles (`=Parameters!header…`)
  and columns absent from the old report (`ApplicationDate`, `CommodityType`, `TotalCIF`,
  `ExchangeRate`). **Fixed swapped mapping** — `Lic Value` = `totalAmount` (sum of items),
  `Total Amount` = `amount` (voucher payment), per `Business/Reports.cs` (`sLicenceValue =
  TotalAmount`). Date columns use the preformatted `sLicenceDate`/`sVoucherDate` strings so they
  render `dd/MM/yyyy` (the `date` dataType would show `YYYY-MM-DD`).

**Remaining:** the Export/Permit/Border families on the 🟡 procs, plus AccountSummary,
ChequeNo, MPU, MPU_V3, MemberRegistration, OnlineFees.

> Notes
> - **`*_Fast` tracker rows are LINQ-only — no such DB proc exists.** The
>   `…DetailReport_Fast` classes back several report variants (ByMethod, BySection,
>   BySellerCountry, CompanyList, DailyReport, Detail, TotalValue) that differ only
>   by frontend column selection. Converting them means wiring to the **real**
>   proc (`sp_ImportLicenceDetailReport`, `sp_ImportLicencePendingDetailReport`,
>   and the Export/Permit equivalents) — a behaviour change to flag to the user.
>   See "‘_Fast’ procedures are LINQ-only" above.
> - The multi-form procs (`sp_ActualAmendReport`, `sp_AmendReport`,
>   `sp_HSCodeReport`, `sp_CancelReport`, `sp_ExtensionReport`, `sp_NewReport`,
>   `sp_VoucherReport`) are shared across Import/Export × Licence/Permit × Border
>   (8 controllers each). Convert one family at a time via the **dual-path**
>   coexistence rule (keep `Query`, add `ExecuteAsync`) and the **INSERT-EXEC**
>   wrapper recipe above — don't re-implement their branchy SQL.
> - Confirm each controller's actual converter type in
>   `Backend/StoredProcedureToLinq/` before converting; a few reports point at a
>   `_Fast`/variant class rather than the same-named procedure.

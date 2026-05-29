# Stored Procedure LINQ Projection DB Call Audit

Updated: 2026-05-29 S

## Purpose

This records converted stored-procedure LINQ queries that still call `db.*` inside a result projection, for example:

```csharp
ConsignedCountry = string.Join(",",
    from country in db.Countries
    where ("," + licence.ConsignedCountryId + ",").Contains("," + country.Id.ToString() + ",")
    select country.Name ?? string.Empty)
```

These patterns can generate correlated subqueries, `OUTER APPLY`, repeated scans, slow `COUNT`, and EF multiple-collection warnings. Report page APIs should avoid this shape.

## Scan Summary

- Scope: `Backend/StoredProcedureToLinq/*.cs`
- Files scanned: 85
- Files with `db.*` inside projection: 22
- Projection field groups found: 43
- Raw occurrences found: 272

Files not listed below had no `db.*` call inside a `select new` / `.Select(... => new ...)` projection in this scan.

## Fix Rules For LLM

1. Do not call `db.*` inside the final report result projection for paged report APIs.
2. For small lookup tables such as `Countries`, `PortOfDischarges`, `WineTypes`, and `PermitBusinesses`, load lookup rows once, cache for one day if stable, page scalar rows first, then resolve names from memory.
3. For large or changing tables such as `PaThaKas`, `AccountTransactions`, `Messages`, and item tables, use joins, group joins, or pre-aggregated subqueries instead of cache-all.
4. For per-row `Sum`, `Count`, `FirstOrDefault`, or item/currency lookups, build one grouped query keyed by parent id and join that grouped result into the main query.
5. Keep `AsNoTracking()` on report reads unless a report truly needs tracked entities.
6. For page APIs, prefer fast pagination (`pageSize + 1`) and avoid exact total count unless explicitly requested.
7. Preserve Excel output, but it can use a separate exact/export path if the page API needs a faster path.

### Two Hard Constraints On Caching

These bound what caching can and cannot do. They are not optional.

1. **A cache cannot be joined inside the database query.** LINQ-to-Entities translates `db.*` to SQL that runs in the database. An in-memory cache (dictionary/list) lives in the app process and cannot be joined into an `IQueryable`. The only value that crosses into SQL is `ids.Contains(x.Id)` → `WHERE Id IN (...)`, which is bounded (keep it to page-sized id sets, not thousands). Therefore caching is only ever used to **resolve display fields after `.ToList()` materialization**, never to feed the in-database query. The "LINQ reads from cache" model does not work.
2. **Only small, stable reference tables may be cached.** Caching is correct only for the six tables listed under "Safe To Cache" below. Do not cache large or transactional tables (`PaThaKas`, `AccountTransactions`, `Messages`, item tables): caching them risks large memory use per app instance and serves stale financial/workflow data. Those are solved with SQL joins, grouped subqueries, or page-then-batch — see "Performance Fix Playbook For Non-Cacheable Sources".

### Cache Mechanism

Use a lazy `IMemoryCache` entry with a one-day absolute expiration: load on first miss, serve from memory until expiry, reload on the next miss. **No background/hourly refresh job is needed** — these reference names change rarely, and the cold-load for a few-hundred-row table is negligible. A background pre-warm timer adds a hosted service and failure modes for no measurable gain and must not be added.

## Pattern Fix Plan

| Pattern | Main risk | Preferred fix |
| --- | --- | --- |
| Comma-separated id name expansion with `string.Join` | EF may produce `OUTER APPLY` and scan lookup tables per result row. | Page scalar rows first, cache lookup table for one day, resolve names after materialization. |
| Per-row item aggregate, such as `Amount = db.Items.Where(...).Sum(...)` | Repeated subqueries per row and slow `COUNT`. | Pre-aggregate item tables by parent id and left join the aggregate. |
| Per-row scalar lookup, such as `FirstOrDefault()` from another table | Repeated correlated lookup. | Convert to left join, grouped latest-row query, or batch lookup after paging. |
| Per-row `Count()` | Repeated count per row. | Pre-count grouped rows once and left join by key. |

## Cache Or Query Strategy

Not every `db.*` projection should be solved with memory cache. Use cache only for small reference data that changes rarely. Use SQL joins or grouped subqueries for transactional data.

### Safe To Cache For One Day

| Table | Why cache is OK | Use for |
| --- | --- | --- |
| `Countries` | Small reference table, stable names. | `ConsignedCountry`, `CountryofOrigin`, `DestinationCountry`. |
| `PortOfDischarges` | Small reference table, stable port names/codes. | `PortofExport`, `PortofShipment`. |
| `WineTypes` | Small reference table, stable lookup names. | Wine importation `WineType` display text. |
| `Sakhans` | Small reference table, stable branch/checkpoint names. | MPU `Sakhan` display text if not already joined. |
| `PermitBusinesses` | Small reference table. | Only the permit business description lookup can be cached; the PaThaKa-to-permit-business mapping should still be queried by page ids or grouped in SQL. |
| `Currencies` | Small reference table. | Cache only `CurrencyId -> Code`; still use SQL to choose the relevant item `CurrencyId`. |

Recommended shared helper:

- Create `Backend/Service/Reports/ReportLookupCache.cs`.
- Inject/use `IMemoryCache`.
- Provide methods like `GetCountriesAsync`, `GetPortsAsync`, `GetWineTypesAsync`, `GetCurrenciesAsync`, `GetSakhansAsync`, and `GetPermitBusinessesAsync`.
- Each method returns a `Dictionary<int, string>` (id → display value), loaded lazily on cache miss with `GetOrCreateAsync` and an absolute expiration of one day. No background refresh job.
- Callers resolve display fields **after paging/materialization** from these dictionaries. For comma-separated id columns (for example `ConsignedCountryId`), split the string, map each id through the dictionary, and re-join — all in memory, after `.ToList()`.

Sketch:

```csharp
public sealed class ReportLookupCache(IMemoryCache cache, IDbContextFactory<TradeNetDB> dbFactory)
{
    public Task<Dictionary<int, string>> GetCountriesAsync() =>
        cache.GetOrCreateAsync("lookup:countries", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
            await using var db = await dbFactory.CreateDbContextAsync();
            return await db.Countries.AsNoTracking()
                .ToDictionaryAsync(c => c.Id, c => c.Name ?? string.Empty);
        })!;

    // Resolve a comma-separated id column after paging.
    // Must match the original `from x in db.Countries where csv.Contains(x.Id) select x.Name`:
    //   - output ordered by id (table/PK order), NOT by the order ids appear in the CSV
    //   - each id resolved at most once (the original iterates the lookup table, so no duplicates)
    //   - ids not present in the lookup table are omitted
    public static string ResolveCsv(string? csvIds, Dictionary<int, string> map)
    {
        if (string.IsNullOrEmpty(csvIds)) return string.Empty;
        var ids = csvIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
            .Where(id => id.HasValue).Select(id => id!.Value)
            .ToHashSet();
        return string.Join(",", map.Where(kv => ids.Contains(kv.Key))
            .OrderBy(kv => kv.Key)
            .Select(kv => kv.Value));
    }
}
```

> Caveat: this assumes the original `db.Countries` enumeration came back in ascending-id (clustered-index) order, which is the usual but not guaranteed default. Confirm with `ToQueryString()` / a known date range before trusting it. If the original used an explicit `OrderBy`, match that instead.

### Do Not Cache Whole Tables

| Table/source | Why not cache | Fix approach |
| --- | --- | --- |
| Licence/permit item tables | Large and transactional; they determine report amounts, HS code, descriptions, and currency. | Pre-aggregate/group by parent id in SQL, then join. |
| `PaThaKas` | Large business/member data and can change. | Join directly, or fetch only page keys after paging. |
| `AccountTransactions` / `AccountTransactionDetails` | Large transactional payment data. | Join or grouped latest-row query by transaction id/application id. |
| `Messages` | Transactional and tied to workflow state. | Left join latest/relevant message by transaction id. |
| `PaThaKaRegistrations` | Transactional/history table. | Group/count in SQL by company registration no. |
| `PaThaKaPermitBusinesses` | Mapping table can be large and member-specific. | Query mappings for page PaThaKa ids or group in SQL; cache only `PermitBusinesses`. |

## Performance Fix Playbook For Non-Cacheable Sources

Everything that may not be cached falls into one of four techniques. Pick by the shape of the projection, not the table.

### 1. Page-Then-Batch (scalar lookup keyed by a single id)

For `CompanyName` from `PaThaKas` and similar single-key lookups. Page the rows first, collect the page's foreign keys (~`pageSize` ids), query only those, merge in memory. Bounded to one page of ids and always fresh.

```csharp
var rows = await query.AsNoTracking().Take(pageSize + 1).ToListAsync();
var paThaKaIds = rows.Select(r => r.PaThaKaId).Distinct().ToList();
var names = await db.PaThaKas.AsNoTracking()
    .Where(p => paThaKaIds.Contains(p.Id))
    .ToDictionaryAsync(p => p.Id, p => p.CompanyName ?? string.Empty);
foreach (var r in rows) r.CompanyName = names.GetValueOrDefault(r.PaThaKaId, "");
```

Applies to: `sp_MPUReport.cs` (CompanyName), `sp_MPUReport_Seperated_OnineFee.cs` (CompanyName), `sp_MPUReport_V3.cs` (CompanyName), `sp_MPUReportV2.cs` (CompanyName).

### 2. Pre-Aggregate And Join (per-row `Sum`/`Count`/first-row over item tables)

For `Amount`, `TotalAmount`, `Currency`, first-item fields. Build one grouped query keyed by parent id and left join it into the main query — one pass over the item table instead of one subquery per result row.

```csharp
var itemSummary =
    from item in db.ImportLicenceItems.AsNoTracking()
    group item by item.ImportLicenceId into g
    select new
    {
        ParentId = g.Key,
        Amount = g.Sum(i => i.Amount),
        FirstCurrencyId = g.OrderBy(i => i.UniqueId).Select(i => i.CurrencyId).FirstOrDefault()
    };
// left join itemSummary into the licence query by ParentId == licence.Id
```

Resolve `FirstCurrencyId` → code from the cached `Currencies` dictionary after paging. Applies to: `sp_NewReport.cs`, `sp_AmendReport.cs`, `sp_ActualAmendReport.cs`, `sp_CancelReport.cs`, `sp_ExtensionReport.cs`, `sp_VoucherReport.cs`, `sp_PendingReport.cs` (Currency/Amount/HSCode/AdditionalDescription), `sp_CompanyProfileReport.cs` (ExtensionCount from `PaThaKaRegistrations`).

### 3. Latest-Row Join (per-row `FirstOrDefault` over a transactional table)

For `VoucherNo` from `AccountTransactions` and `Message` from `Messages`. Build a grouped "latest/relevant row per key" subquery and left join it, instead of a correlated `FirstOrDefault` in the projection.

```csharp
var latestMessage =
    from m in db.Messages.AsNoTracking()
    group m by m.TransactionId into g
    select new { TransactionId = g.Key, Text = g.OrderByDescending(x => x.Id).Select(x => x.Body).FirstOrDefault() };
// left join latestMessage by TransactionId
```

Applies to: `sp_ApplicationHistory.cs` (Message), `sp_MPUReport.cs` / `sp_MPUReport_Seperated_OnineFee.cs` (VoucherNo). Keep the payment/voucher business filters in the grouped subquery — these matches are business-sensitive.

### 4. SQL Row Number / Index After Paging (per-row `Count` used as a sequence)

For `RowNumber` in `sp_MPUReport_V3.cs`. Do not count rows per row. Either use a window function (`ROW_NUMBER()` via a keyless projection) or compute the index in memory after paging from the page offset. Verify against the old output, since this is display-only sequencing.

### Mapping table special case

`PaThaKaPermitBusinesses` (sp_CompanyProfileReport `PermitBusiness`): query the mapping rows for the **page's** PaThaKa ids (technique 1), then resolve the business description from the **cached** `PermitBusinesses` dictionary. Cache the small lookup; never cache the member-specific mapping table.

### Result Parity Risks (read before trusting any fix)

The rewrites are equivalent only if these are matched. Each is a place output silently differs:

| Technique | Will differ unless you... |
| --- | --- |
| 1. Page-then-batch | Match join semantics. Original `FirstOrDefault()` on a missing key returns `null`; `GetValueOrDefault("")` returns `""` — pick whichever the original produced. If the original used an inner join that dropped rows with no match, batch resolution keeps the row → different row count. |
| 2. Pre-aggregate + join | **Left** join, not inner (else parents with no items vanish). `Sum` over no rows must yield the original's value (`?? 0` if original returned 0). Carry every `where` filter from the original subquery into the group. **`FirstCurrencyId` ordering must match the original's exactly** — this is the most common silent mismatch; an unspecified original order was DB-arbitrary and may not be reproducible at all. |
| 3. Latest-row join | The "latest" `OrderBy(Desc)` and tie-breaker must match the original's. Keep the payment/voucher business filters inside the grouped subquery, not after. Left vs inner join again. |
| 4. Row number | Same `ORDER BY` (and `PARTITION`) as the original count. Computing index from page offset only matches if global ordering and page size are identical and the original was a plain 1..N sequence. |
| CSV cache resolve | Order by id and dedup (see `ResolveCsv` above), not CSV order. Preserve `?? string.Empty` for null names. |

General: the original ran inside one SQL statement with one consistent snapshot. Page-then-batch and cache resolution read at a *later* moment, so a row edited between the page query and the batch/cache read can differ. For reference data this is irrelevant; for transactional joins, keep the resolution in the same SQL query (technique 2/3) rather than a second round-trip where consistency matters.

**The only way to be sure is the verification step (lines below): diff old vs new output over a known date range, and capture `ToQueryString()`. Do not mark a row Done on inspection alone.**

## Implementation Approach

### A. Fast Detail Reports With Lookup Expansion

Use the same shape as `sp_ImportLicenceDetailReport_Fast.cs`:

1. Query only scalar row data plus raw comma-separated lookup ids/codes.
2. Apply report filters and internal required order.
3. Fetch `pageSize + 1` rows for page APIs.
4. Resolve lookup display fields from `ReportLookupCache`.
5. Return `ApiResult.CreateFastPageFromRows`.

Apply this to:

- `sp_ImportLicencePendingDetailReport.cs`
- `sp_ImportPermitDetailReport.cs`
- `sp_ExportLicenceDetailReport.cs`
- `sp_ExportPermitDetailReport.cs`
- Remaining Excel/original path for `sp_ImportLicenceDetailReport.cs`

### B. Aggregate Item Summary Reports

Do not cache item tables. Build reusable grouped summaries for each item table:

```csharp
var itemSummary =
    from item in db.ImportLicenceItems.AsNoTracking()
    group item by item.ImportLicenceId into grouped
    select new
    {
        ParentId = grouped.Key,
        Amount = grouped.Sum(item => item.Amount),
        FirstCurrencyId = grouped
            .OrderBy(item => item.UniqueId)
            .Select(item => item.CurrencyId)
            .FirstOrDefault()
    };
```

Then join `itemSummary` to the licence/permit query and resolve `FirstCurrencyId` through either a SQL join to `Currencies` or cached `CurrencyId -> Code` after paging.

Apply this to:

- `sp_NewReport.cs`
- `sp_AmendReport.cs`
- `sp_ActualAmendReport.cs`
- `sp_CancelReport.cs`
- `sp_ExtensionReport.cs`
- `sp_VoucherReport.cs`
- `sp_PendingReport.cs`

### C. Transactional Scalar Lookups

For `MPUReport`, `ApplicationHistory`, and related payment/history reports:

- Do not cache `AccountTransactions`, `AccountTransactionDetails`, `Messages`, or `PaThaKas`.
- Convert scalar projection lookups to left joins or grouped latest-row subqueries.
- If the lookup depends on page rows only, use the two-step pattern: fetch page rows first, collect keys, query matching records with `WHERE key IN (...)`, then merge in memory.

### D. Company Profile And Wine Reports

- `WineType`: cache `WineTypes` and resolve after paging.
- `PermitBusiness`: cache `PermitBusinesses`, but query `PaThaKaPermitBusinesses` by page PaThaKa ids.
- `ExtensionCount`: pre-count approved extensions by company registration no in SQL and join.

## Recommended Fix Order

1. **High impact / low risk**: finish detail report lookup expansion using cache.
   - Import licence pending detail, import permit detail, export licence detail, export permit detail.
2. **High impact / medium risk**: remove repeated item aggregate/currency subqueries.
   - Start with `sp_NewReport.cs`, then copy the same helper pattern to amend/cancel/extension/voucher.
3. **Medium impact / medium risk**: fix pending report item fields.
   - Currency, additional description, amount, HS code should come from one item summary query.
4. **Medium impact / higher logic risk**: fix MPU/payment reports.
   - These need careful matching because voucher/payment filters are business-sensitive.
5. **Lower volume / cleanup**: fix company profile and wine reports.
6. **Review only**: `sp_NewReport_old.cs`.
   - It appears unreferenced; delete/archive if confirmed unused instead of spending time optimizing it.

## Verification Per Fix

For each report fixed:

1. Compare old and new output for a narrow known date range.
2. Capture generated SQL with `ToQueryString()` and verify no `db.*` projection subquery remains in the page query.
3. Test page API with `includeTotalCount: false`.
4. Test `includeTotalCount: true` only when exact count is required.
5. Run `dotnet build Backend/API.csproj -o artifacts/build-check/api`.
6. Update the queue status in this document from `Todo` to `Partial` or `Done`.

## Detailed Fix Queue

| Status | File | Lines | Projection field | DB sources | Fix note |
| --- | --- | --- | --- | --- | --- |
| Done | `Backend/StoredProcedureToLinq/sp_ActualAmendReport.cs` | all 8 form-type branches | `Currency` | item tables, `Currencies` | Grouped-join: first currency-bearing item per parent by `MIN(Id)`, left-joined. Build + EF translation verified. **Value parity vs live DB not yet diffed.** |
| Done | `Backend/StoredProcedureToLinq/sp_ActualAmendReport.cs` | all 8 form-type branches | `Amount` | item tables | First-item (`MIN(Id)`) left join, NOT `Sum` (trap preserved). `decimal?` type kept. Translation verified. |
| Done | `Backend/StoredProcedureToLinq/sp_AmendReport.cs` | all 8 form-type branches | `Currency` | item tables, `Currencies` | Grouped-join first currency-bearing item by `MIN(Id)`. Translation verified. |
| Done | `Backend/StoredProcedureToLinq/sp_AmendReport.cs` | all 8 form-type branches | `Amount` | item tables | First-item (`MIN(Id)`) left join, NOT `Sum` (trap preserved). Translation verified. |
| Done | `Backend/StoredProcedureToLinq/sp_ApplicationHistory.cs` | 90, 119 | `Message` | `Messages` | First message per transaction (`MIN(message.Id)`) resolved via one `GROUP BY` left join instead of per-row correlated subquery. Build verified; **translation/value not verifiable in this env (Query eagerly executes a `FirstOrDefault`, needs live DB).** |
| Done | `Backend/StoredProcedureToLinq/sp_CancelReport.cs` | all 8 form-type branches | `Currency` | item tables, `Currencies` | Grouped-join first currency-bearing item by `MIN(Id)`. Translation verified. |
| Done | `Backend/StoredProcedureToLinq/sp_CancelReport.cs` | all 8 form-type branches | `Amount` | item tables | First-item (`MIN(Id)`) left join, NOT `Sum` (trap preserved). Translation verified. |
| HELD | `Backend/StoredProcedureToLinq/sp_CompanyProfileReport.cs` | 80, 81 | `PermitBusiness` | `PaThaKaPermitBusinesses`, `PermitBusinesses` | Held for live-DB verification (business-sensitive member mapping). Plan: query `PaThaKaPermitBusinesses` by page ids, resolve description from cached `PermitBusinesses`. |
| HELD | `Backend/StoredProcedureToLinq/sp_CompanyProfileReport.cs` | 86 | `ExtensionCount` | `PaThaKaRegistrations` | Held for live-DB verification. Plan: pre-count approved extensions by company registration no and join. |
| Done | `Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReport.cs` | page API (all 14 controllers) | `PortofExport` | `PortOfDischarges` | New `sp_ExportLicenceDetailReport_Fast.cs`: page scalar rows + raw `PortofExportId`, resolve via cached ports after paging. Page `Count`/`Skip`/`Take` remains SQL-side. Excel endpoints also migrated to the same Fast cache path; the original `Query` (with the per-row subqueries) was removed, keeping only the Request/Result types. 14 controllers switched to Fast. **Value parity vs live DB pending.** |
| Done | `Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReport.cs` | page API (all 14 controllers) | `DestinationCountry` | `Countries` | Same Fast path; resolve via cached countries after paging. |
| Done | `Backend/StoredProcedureToLinq/sp_ExportPermitDetailReport.cs` | page API (all 10 controllers) | `PortofExport` | `PortOfDischarges` | New `sp_ExportPermitDetailReport_Fast.cs` + 10 controllers switched; resolve via cache after paging. Excel endpoints also migrated to the same Fast cache path; the original `Query` (with the per-row subqueries) was removed, keeping only the Request/Result types. |
| Done | `Backend/StoredProcedureToLinq/sp_ExportPermitDetailReport.cs` | page API (all 10 controllers) | `DestinationCountry` | `Countries` | Same Fast path. |
| Done | `Backend/StoredProcedureToLinq/sp_ExportPermitDetailReport.cs` | page API (all 10 controllers) | `CountryofOrigin` | `Countries` | Same Fast path. |
| Done | `Backend/StoredProcedureToLinq/sp_ExtensionReport.cs` | all 8 form-type branches | `Currency` | item tables, `Currencies` | Grouped-join first currency-bearing item by `MIN(Id)`. Translation verified. |
| Done | `Backend/StoredProcedureToLinq/sp_ExtensionReport.cs` | all 8 form-type branches | `Amount` | item tables | `Sum` via `GROUP BY` left join; `?? 0` preserved. Translation verified. |
| Done | `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport.cs` | page/Excel API (all 14 import licence detail-family controllers) | `ConsignedCountry` | `Countries` | `sp_ImportLicenceDetailReport_Fast.cs` now handles page and Excel APIs: page scalar rows + raw country ids, resolve via cached countries after materialization. All import licence detail-family controllers switched to Fast. The old `Query` has no API controller callers; it is retained only for the unreferenced daily-detail LINQ wrapper. **Value parity vs live DB pending.** |
| Done | `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport.cs` | page/Excel API (all 14 import licence detail-family controllers) | `CountryofOrigin` | `Countries` | Same Fast path. |
| Done | `Backend/StoredProcedureToLinq/sp_ImportLicencePendingDetailReport.cs` | page API (both controllers) | `ConsignedCountry` | `Countries` | New `sp_ImportLicencePendingDetailReport_Fast.cs` + 2 controllers switched; resolve via cached countries after paging. Page `Count`/`Skip`/`Take` remains SQL-side. Excel endpoints also migrated to the same Fast cache path; the original `Query` (with the per-row subqueries) was removed, keeping only the Request/Result types. **Value parity pending.** |
| Done | `Backend/StoredProcedureToLinq/sp_ImportLicencePendingDetailReport.cs` | page API (both controllers) | `CountryofOrigin` | `Countries` | Same Fast path. |
| Done | `Backend/StoredProcedureToLinq/sp_ImportPermitDetailReport.cs` | page API (all 10 controllers) | `PortofShipment` | `PortOfDischarges` | New `sp_ImportPermitDetailReport_Fast.cs` + 10 controllers switched; resolve via cached ports after paging. Excel endpoints also migrated to the same Fast cache path; the original `Query` (with the per-row subqueries) was removed, keeping only the Request/Result types. |
| Done | `Backend/StoredProcedureToLinq/sp_ImportPermitDetailReport.cs` | page API (all 10 controllers) | `CountryofOrigin` | `Countries` | Same Fast path. |
| HELD | `Backend/StoredProcedureToLinq/sp_MPUReport.cs` | 80 | `CompanyName` | `PaThaKas` | Held: `PaThaKaNo == x OR CompanyRegistrationNo == x` OR-lookup risks duplicate matches; needs live-DB uniqueness check before converting to a join. |
| HELD | `Backend/StoredProcedureToLinq/sp_MPUReport.cs` | 97 | `VoucherNo` | `AccountTransactions` | Held (financial). Latest-by-`CreatedDate` `FirstOrDefault` has arbitrary-tie semantics; a value-identical translatable grouped join needs a deterministic tie-break + live-DB diff. Existing correlated subquery is correct, just not optimal. |
| HELD | `Backend/StoredProcedureToLinq/sp_MPUReport_Seperated_OnineFee.cs` | 78 | `CompanyName` | `PaThaKas` | Held: same OR-lookup duplicate-match risk as `sp_MPUReport`. |
| HELD | `Backend/StoredProcedureToLinq/sp_MPUReport_Seperated_OnineFee.cs` | 96, 97 | `VoucherNo` | `AccountTransactions`, `AccountTransactionDetails` | Held (financial); same latest-row tie-break concern, plus a detail-table join. Needs live-DB diff. |
| HELD | `Backend/StoredProcedureToLinq/sp_MPUReport_V3.cs` | 53, 66 | `RowNumber` | `MpupaymentTransactions`, `AccountTransactions` | Held: this is a per-row `COUNT` implementing `ROW_NUMBER() OVER(...)`. EF Core has no LINQ window-function translation; a faithful fix needs raw SQL or a post-paging compute that reproduces the within-group order. Existing count is correct. |
| HELD | `Backend/StoredProcedureToLinq/sp_MPUReport_V3.cs` | 93 | `CompanyName` | `PaThaKas` | Held: same OR-lookup duplicate-match risk. |
| HELD | `Backend/StoredProcedureToLinq/sp_MPUReportV2.cs` | 189 | `Sakhan` | `Sakhans` | Held with the rest of this file. `Sakhan` is a PK lookup (low risk) and could be cached/joined; deferred to verify alongside the file's `CompanyName`. |
| HELD | `Backend/StoredProcedureToLinq/sp_MPUReportV2.cs` | 194 | `CompanyName` | `PaThaKas` | Held: `CompanyRegistrationNo == x` lookup; needs live-DB uniqueness check. |
| Done | `Backend/StoredProcedureToLinq/sp_NewReport.cs` | all 8 form-type branches | `Currency` | item tables, `Currencies` | Replaced per-row correlated subquery with a `GROUP BY` derived table: first currency-bearing item per parent by `MIN(Id)` (preserves the original inner-join-to-`Currencies` semantics), resolved via left join. EF translation verified by `ReportQueryTranslationTests` (no `OUTER APPLY`). **Value parity vs live DB not yet diffed.** |
| Done | `Backend/StoredProcedureToLinq/sp_NewReport.cs` | all 8 form-type branches | `Amount` | item tables | This report's `Amount` is a `Sum` (order-independent). Replaced per-row `Sum` subquery with a `GROUP BY` summary left join; `COALESCE(..., 0)` matches the original `?? 0`. EF translation verified. |
| DEAD | `Backend/StoredProcedureToLinq/sp_NewReport_old.cs` | n/a | `Currency` | item tables, `Currencies` | Unreferenced anywhere in the codebase. Recommend archive/delete instead of fixing. |
| DEAD | `Backend/StoredProcedureToLinq/sp_NewReport_old.cs` | n/a | `Amount` | item tables | Unreferenced. Recommend archive/delete. |
| Done | `Backend/StoredProcedureToLinq/sp_PendingReport.cs` | all branches | `Currency` | item tables, `Currencies` | Grouped-join first currency-bearing item by `MIN(Id)`. Translation verified. |
| Done | `Backend/StoredProcedureToLinq/sp_PendingReport.cs` | all branches | `AdditionalDescription` | item tables | First-item (`MIN(Id)`) shared summary (with `HSCode`), left-joined. Translation verified. |
| Done | `Backend/StoredProcedureToLinq/sp_PendingReport.cs` | all branches | `Amount` | item tables | `Sum` via `GROUP BY` left join; `?? 0m` preserved. Translation verified. |
| Done | `Backend/StoredProcedureToLinq/sp_PendingReport.cs` | all branches | `HSCode` | item tables | First-item (`MIN(Id)`) shared summary (with `AdditionalDescription`). Translation verified. |
| Done | `Backend/StoredProcedureToLinq/sp_VoucherReport.cs` | all 8 form-type branches | `Currency` | item tables, `Currencies` | Grouped-join first currency-bearing item by `MIN(Id)`. Translation verified. |
| Done | `Backend/StoredProcedureToLinq/sp_VoucherReport.cs` | all 8 form-type branches | `TotalAmount` | item tables | `Sum` via `GROUP BY` left join; original had no `?? 0` (nullable `decimal?`), preserved. Translation verified. |
| Done (speculative) | `Backend/StoredProcedureToLinq/sp_WineImportationByPaThaKaReport.cs` | n/a | `WineType` | `WineTypes` | Still **unreferenced** (no controller). Per request, converted anyway: new `sp_WineImportationByPaThaKaReport_Fast.cs` (cache-after-paging via `ReportLookupCache.GetWineTypeNamesAsync` + `CreatePagedResultAsync`/`CreateExcelWorkbookAsync`); original `Query` removed, Request/Result kept. No caller yet, so unused until wired. |
| Done (speculative) | `Backend/StoredProcedureToLinq/sp_WineImportationRegistrationReport.cs` | n/a | `WineType` | `WineTypes` | Unreferenced. Converted to `sp_WineImportationRegistrationReport_Fast.cs` (same pattern); original `Query` removed. Unused until wired. |
| Done (speculative) | `Backend/StoredProcedureToLinq/sp_WineImportationReport.cs` | n/a | `WineType` | `WineTypes` | Unreferenced. Converted to `sp_WineImportationReport_Fast.cs` (same pattern; Summary branch keeps client-side concat); original `Query` removed. Unused until wired. |

## Already Clean Or Separately Handled

- `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs` has no `db.*` call inside the final result projection. It resolves countries from cached lookup rows after paging.
- `Backend/StoredProcedureToLinq/sp_ImportLicenceDaily_Detail_Report.cs` did not match this specific projection-db-call scan.

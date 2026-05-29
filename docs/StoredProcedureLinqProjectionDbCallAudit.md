# Stored Procedure LINQ Projection DB Call Audit

Updated: 2026-05-29

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
| Todo | `Backend/StoredProcedureToLinq/sp_ActualAmendReport.cs` | 111, 112, 164, 165, 217, 218, 270, 271, 326, 327, 380, 381, 441, 442, 495, 496, 555, 556, 613, 614 | `Currency` | item tables, `Currencies` | Precompute first currency per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_ActualAmendReport.cs` | 115, 168, 221, 274, 330, 384, 445, 499, 559, 617 | `Amount` | item tables | **NOT a sum** — original is `.Select(Amount).FirstOrDefault()` (first item). Use a first-row-per-parent subquery, NOT `Sum`. Original has no `OrderBy`, so pick a stable order (e.g. item `Id`). |
| Todo | `Backend/StoredProcedureToLinq/sp_AmendReport.cs` | 111, 112, 164, 165, 217, 218, 270, 271, 328, 329, 382, 383, 443, 444, 497, 498, 557, 558, 615, 616 | `Currency` | item tables, `Currencies` | Precompute first currency per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_AmendReport.cs` | 115, 168, 221, 274, 332, 386, 447, 501, 561, 619 | `Amount` | item tables | **NOT a sum** — original is `.Select(Amount).FirstOrDefault()` (first item). Use a first-row-per-parent subquery, NOT `Sum`. Original has no `OrderBy`, so pick a stable order (e.g. item `Id`). |
| Todo | `Backend/StoredProcedureToLinq/sp_ApplicationHistory.cs` | 90, 119 | `Message` | `Messages` | Left join latest/relevant message by transaction id instead of projecting `db.Messages`. |
| Todo | `Backend/StoredProcedureToLinq/sp_CancelReport.cs` | 110, 111, 163, 164, 216, 217, 269, 270, 325, 326, 379, 380, 440, 441, 494, 495, 554, 555, 612, 613 | `Currency` | item tables, `Currencies` | Precompute first currency per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_CancelReport.cs` | 114, 167, 220, 273, 329, 383, 444, 498, 558, 616 | `Amount` | item tables | **NOT a sum** — original is `.Select(Amount).FirstOrDefault()` (first item). Use a first-row-per-parent subquery, NOT `Sum`. Original has no `OrderBy`, so pick a stable order (e.g. item `Id`). |
| Todo | `Backend/StoredProcedureToLinq/sp_CompanyProfileReport.cs` | 80, 81 | `PermitBusiness` | `PaThaKaPermitBusinesses`, `PermitBusinesses` | Cache permit-business lookup or pre-join and group before final projection. |
| Todo | `Backend/StoredProcedureToLinq/sp_CompanyProfileReport.cs` | 86 | `ExtensionCount` | `PaThaKaRegistrations` | Pre-count approved extensions by company registration no and join. |
| Todo | `Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReport.cs` | 148, 232, 316 | `PortofExport` | `PortOfDischarges` | Cache port lookup for one day and resolve after paging. |
| Todo | `Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReport.cs` | 155, 239, 323 | `DestinationCountry` | `Countries` | Cache country lookup for one day and resolve after paging. |
| Todo | `Backend/StoredProcedureToLinq/sp_ExportPermitDetailReport.cs` | 138, 228 | `PortofExport` | `PortOfDischarges` | Cache port lookup for one day and resolve after paging. |
| Todo | `Backend/StoredProcedureToLinq/sp_ExportPermitDetailReport.cs` | 143, 233 | `DestinationCountry` | `Countries` | Cache country lookup for one day and resolve after paging. |
| Todo | `Backend/StoredProcedureToLinq/sp_ExportPermitDetailReport.cs` | 149, 239 | `CountryofOrigin` | `Countries` | Cache country lookup for one day and resolve after paging. |
| Todo | `Backend/StoredProcedureToLinq/sp_ExtensionReport.cs` | 109, 110, 160, 161, 211, 212, 262, 263, 316, 317, 368, 369, 427, 428, 479, 480, 537, 538, 593, 594 | `Currency` | item tables, `Currencies` | Precompute first currency per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_ExtensionReport.cs` | 113, 164, 215, 266, 320, 372, 431, 483, 541, 597 | `Amount` | item tables | Pre-aggregate amount per parent id, then join. |
| Partial | `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport.cs` | 155, 240, 325 | `ConsignedCountry` | `Countries` | Page API fixed through `sp_ImportLicenceDetailReport_Fast.cs`; original/Excel path still needs replacement. |
| Partial | `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport.cs` | 159, 244, 329 | `CountryofOrigin` | `Countries` | Page API fixed through `sp_ImportLicenceDetailReport_Fast.cs`; original/Excel path still needs replacement. |
| Todo | `Backend/StoredProcedureToLinq/sp_ImportLicencePendingDetailReport.cs` | 150, 233, 316 | `ConsignedCountry` | `Countries` | Create pending-report fast path or cache country lookup after paging. |
| Todo | `Backend/StoredProcedureToLinq/sp_ImportLicencePendingDetailReport.cs` | 154, 237, 320 | `CountryofOrigin` | `Countries` | Create pending-report fast path or cache country lookup after paging. |
| Todo | `Backend/StoredProcedureToLinq/sp_ImportPermitDetailReport.cs` | 135, 219 | `PortofShipment` | `PortOfDischarges` | Cache port lookup for one day and resolve after paging. |
| Todo | `Backend/StoredProcedureToLinq/sp_ImportPermitDetailReport.cs` | 140, 224 | `CountryofOrigin` | `Countries` | Cache country lookup for one day and resolve after paging. |
| Todo | `Backend/StoredProcedureToLinq/sp_MPUReport.cs` | 80 | `CompanyName` | `PaThaKas` | Left join or batch lookup after paging; do not cache all PaThaKas blindly. |
| Todo | `Backend/StoredProcedureToLinq/sp_MPUReport.cs` | 97 | `VoucherNo` | `AccountTransactions` | Join latest matching account transaction by transaction id/payment filter. |
| Todo | `Backend/StoredProcedureToLinq/sp_MPUReport_Seperated_OnineFee.cs` | 78 | `CompanyName` | `PaThaKas` | Left join or batch lookup after paging; do not cache all PaThaKas blindly. |
| Todo | `Backend/StoredProcedureToLinq/sp_MPUReport_Seperated_OnineFee.cs` | 96, 97 | `VoucherNo` | `AccountTransactions`, `AccountTransactionDetails` | Join/group by transaction id before projection. |
| Todo | `Backend/StoredProcedureToLinq/sp_MPUReport_V3.cs` | 53, 66 | `RowNumber` | `MpupaymentTransactions`, `AccountTransactions` | Avoid per-row count; use SQL row number or calculate row index after paging if acceptable. |
| Todo | `Backend/StoredProcedureToLinq/sp_MPUReport_V3.cs` | 93 | `CompanyName` | `PaThaKas` | Left join or batch lookup after paging; do not cache all PaThaKas blindly. |
| Todo | `Backend/StoredProcedureToLinq/sp_MPUReportV2.cs` | 189 | `Sakhan` | `Sakhans` | Cache `Sakhans` for one day or join once. |
| Todo | `Backend/StoredProcedureToLinq/sp_MPUReportV2.cs` | 194 | `CompanyName` | `PaThaKas` | Left join or batch lookup after paging; do not cache all PaThaKas blindly. |
| Done | `Backend/StoredProcedureToLinq/sp_NewReport.cs` | all 8 form-type branches | `Currency` | item tables, `Currencies` | Replaced per-row correlated subquery with a `GROUP BY` derived table: first currency-bearing item per parent by `MIN(Id)` (preserves the original inner-join-to-`Currencies` semantics), resolved via left join. EF translation verified by `ReportQueryTranslationTests` (no `OUTER APPLY`). **Value parity vs live DB not yet diffed.** |
| Done | `Backend/StoredProcedureToLinq/sp_NewReport.cs` | all 8 form-type branches | `Amount` | item tables | This report's `Amount` is a `Sum` (order-independent). Replaced per-row `Sum` subquery with a `GROUP BY` summary left join; `COALESCE(..., 0)` matches the original `?? 0`. EF translation verified. |
| Review | `Backend/StoredProcedureToLinq/sp_NewReport_old.cs` | 111, 112, 162, 163, 212, 213, 262, 263, 315, 316, 367, 368, 426, 427, 477, 478, 534, 535, 589, 590 | `Currency` | item tables, `Currencies` | Appears unreferenced; delete/archive if unused, otherwise fix like `sp_NewReport.cs`. |
| Review | `Backend/StoredProcedureToLinq/sp_NewReport_old.cs` | 115, 166, 216, 266, 319, 371, 430, 481, 538, 593 | `Amount` | item tables | Appears unreferenced; delete/archive if unused, otherwise fix like `sp_NewReport.cs`. |
| Todo | `Backend/StoredProcedureToLinq/sp_PendingReport.cs` | 64, 65, 103, 104, 142, 143 | `Currency` | item tables, `Currencies` | Precompute first currency per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_PendingReport.cs` | 69, 108, 147 | `AdditionalDescription` | item tables | Precompute first item fields per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_PendingReport.cs` | 73, 112, 151 | `Amount` | item tables | Pre-aggregate amount per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_PendingReport.cs` | 78, 117, 156 | `HSCode` | item tables | Precompute first item fields per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_VoucherReport.cs` | 168, 169, 215, 216, 264, 265, 311, 312, 364, 365, 412, 413, 467, 468, 517, 518, 573, 574, 625, 626 | `Currency` | item tables, `Currencies` | Precompute first currency per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_VoucherReport.cs` | 172, 219, 268, 315, 368, 416, 471, 521, 577, 629 | `TotalAmount` | item tables | Pre-aggregate total amount per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_WineImportationByPaThaKaReport.cs` | 111 | `WineType` | `WineTypes` | Cache wine type lookup for one day and resolve after paging. |
| Todo | `Backend/StoredProcedureToLinq/sp_WineImportationRegistrationReport.cs` | 125 | `WineType` | `WineTypes` | Cache wine type lookup for one day and resolve after paging. |
| Todo | `Backend/StoredProcedureToLinq/sp_WineImportationReport.cs` | 143, 277 | `WineType` | `WineTypes` | Cache wine type lookup for one day and resolve after paging. |

## Already Clean Or Separately Handled

- `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs` has no `db.*` call inside the final result projection. It resolves countries from cached lookup rows after paging.
- `Backend/StoredProcedureToLinq/sp_ImportLicenceDaily_Detail_Report.cs` did not match this specific projection-db-call scan.

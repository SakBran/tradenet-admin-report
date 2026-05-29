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
- Cache entries should use absolute expiration of one day.
- Keep cache values as dictionaries/lists that can resolve comma-separated ids/codes after paging.

### Do Not Cache Whole Tables

| Table/source | Why not cache | Fix approach |
| --- | --- | --- |
| Licence/permit item tables | Large and transactional; they determine report amounts, HS code, descriptions, and currency. | Pre-aggregate/group by parent id in SQL, then join. |
| `PaThaKas` | Large business/member data and can change. | Join directly, or fetch only page keys after paging. |
| `AccountTransactions` / `AccountTransactionDetails` | Large transactional payment data. | Join or grouped latest-row query by transaction id/application id. |
| `Messages` | Transactional and tied to workflow state. | Left join latest/relevant message by transaction id. |
| `PaThaKaRegistrations` | Transactional/history table. | Group/count in SQL by company registration no. |
| `PaThaKaPermitBusinesses` | Mapping table can be large and member-specific. | Query mappings for page PaThaKa ids or group in SQL; cache only `PermitBusinesses`. |

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
| Todo | `Backend/StoredProcedureToLinq/sp_ActualAmendReport.cs` | 115, 168, 221, 274, 330, 384, 445, 499, 559, 617 | `Amount` | item tables | Pre-aggregate amount per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_AmendReport.cs` | 111, 112, 164, 165, 217, 218, 270, 271, 328, 329, 382, 383, 443, 444, 497, 498, 557, 558, 615, 616 | `Currency` | item tables, `Currencies` | Precompute first currency per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_AmendReport.cs` | 115, 168, 221, 274, 332, 386, 447, 501, 561, 619 | `Amount` | item tables | Pre-aggregate amount per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_ApplicationHistory.cs` | 90, 119 | `Message` | `Messages` | Left join latest/relevant message by transaction id instead of projecting `db.Messages`. |
| Todo | `Backend/StoredProcedureToLinq/sp_CancelReport.cs` | 110, 111, 163, 164, 216, 217, 269, 270, 325, 326, 379, 380, 440, 441, 494, 495, 554, 555, 612, 613 | `Currency` | item tables, `Currencies` | Precompute first currency per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_CancelReport.cs` | 114, 167, 220, 273, 329, 383, 444, 498, 558, 616 | `Amount` | item tables | Pre-aggregate amount per parent id, then join. |
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
| Todo | `Backend/StoredProcedureToLinq/sp_NewReport.cs` | 112, 113, 162, 163, 214, 215, 263, 264, 316, 317, 368, 369, 426, 427, 478, 479, 536, 537, 590, 591 | `Currency` | item tables, `Currencies` | Precompute first currency per parent id, then join. |
| Todo | `Backend/StoredProcedureToLinq/sp_NewReport.cs` | 116, 166, 218, 267, 320, 372, 430, 482, 540, 594 | `Amount` | item tables | Pre-aggregate amount per parent id, then join. |
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

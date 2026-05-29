# Search Filter Id Select Fix Plan

## Goal

Every report filter whose field name ends with `Id` should render as a select box instead of a number input. The user-facing label must remove the trailing `Id` text, for example `PaThaKaTypeId` becomes `PaThaKa Type`.

Lookup values must come from the backend TradeNet EF models. For lookup rows with a `Code` column, the select option keeps the code available and displays the descriptive text. The report request should still submit the numeric id because the current report controllers and LINQ report queries compare these fields as `int` ids.

## Scan Result

Scan source: `Frontend/src/Report/config/reportConfigs.ts`

Status: implemented, build verified

- Total `*Id` filter occurrences: 398
- Reports affected: 118
- Unique filter names to support: 13

## Unique Filter Inventory

| Filter name | Occurrences | UI label | Lookup source | Display text | Code field | Request value | Fix scope | Status |
|---|---:|---|---|---|---|---|---|---|
| `AmendRemarkId` | 16 | Amend Remark | `TradeNetDbContext.LicencePermitAmendRemarks` | `Name` | `Code` | `Id` | API + frontend | DONE |
| `BusinessTypeId` | 5 | Business Type | `TradeNetDbContext.BusinessTypes` | `Name` | `Code` | `Id` | API + frontend | DONE |
| `BuyerCountryId` | 24 | Buyer Country | `TradeNetDbContext.Countries` | `Name` | `Code` | `Id` | API + frontend | DONE |
| `ChequeNoId` | 1 | Cheque No | `TradeNetDbContext.ChequeNos` | `Name` | `Code` | `Id` | API + frontend | DONE |
| `ExportImportIncotermId` | 30 | Export Import Incoterm | `TradeNetDbContext.ExportImportIncoterms` | `Name` | `Code` | `Id` | API + frontend | DONE |
| `ExportImportMethodId` | 30 | Export Import Method | `TradeNetDbContext.ExportImportMethods` | `Name` | `Code` | `Id` | API + frontend | DONE |
| `ExportImportSectionId` | 100 | Export Import Section | `TradeNetDbContext.ExportImportSections` | `Name` | `Code` | `Id` | API + frontend | DONE |
| `LineofBusinessId` | 4 | Lineof Business | `TradeNetDbContext.LineofBusinesses` | `Name` | `Code` | `Id` | API + frontend | DONE |
| `NRCPrefixCodeId` | 2 | NRC Prefix Code | `TradeNetDbContext.NrcprefixCodes` | `Description` | `Code` | `Id` | API + frontend | DONE |
| `NRCPrefixId` | 2 | NRC Prefix | `TradeNetDbContext.Nrcprefixes` | `StatePrefix/TownshipPrefix` | `TownshipPrefix` | `Id` | API + frontend | DONE |
| `PaThaKaTypeId` | 50 | PaThaKa Type | `TradeNetDbContext.PaThaKaTypes` | `Description` | `Code` | `Id` | API + frontend | DONE |
| `SakhanId` | 108 | Sakhan | `TradeNetDbContext.Sakhans` | `Name` | `Code` | `Id` | API + frontend | DONE |
| `SellerCountryId` | 26 | Seller Country | `TradeNetDbContext.Countries` | `Name` | `Code` | `Id` | API + frontend | DONE |

## Implementation Instructions For LLM

1. Add one backend lookup endpoint instead of one controller per table.
   - Recommended route: `GET api/ReportLookups/{lookupName}`.
   - Return a stable shape: `{ id: number, code: string, label: string }[]`.
   - Filter inactive/deleted rows where `IsActive` and `IsDeleted` exist.
   - Sort by `SortOrder` where available, otherwise by label/code.
   - Cache each lookup response in `IMemoryCache` for one day to reduce reference-table database reads.

2. Add reusable frontend lookup support in `GenericReportPage`.
   - Do not hand-edit all 398 report filter entries.
   - Add a central mapping from filter name to lookup name and clean label.
   - Any mapped filter ending with `Id` should render an Ant Design `Select`.
   - Include an `All` option that submits `0` to preserve current backend behavior.
   - Show user-friendly labels such as `PaThaKa Type`, not `PaThaKa Type Id`.

3. Preserve current report API contracts.
   - Existing report request DTOs use `int` properties such as `PaThaKaTypeId`.
   - Existing LINQ reports compare database ids, for example `paThaKaType.Id == request.PaThaKaTypeId`.
   - Even if the UI exposes lookup code in the option metadata, submit `Id` to report APIs unless the backend report queries are intentionally changed.

4. Mark status in this file as work is completed.
   - Change inventory row status from `TODO` to `DONE` only after the lookup renders and the request payload still sends the correct numeric id.
   - Add verification notes at the bottom with build/test command results.

## Rescan Command

Run this from the repo root to refresh the count:

```powershell
$path='Frontend/src/Report/config/reportConfigs.ts'
$current=''
$rows=@()
Get-Content $path | ForEach-Object {
  if ($_ -match '^\s{2}([A-Za-z0-9_]+):\s*\{') { $current=$Matches[1] }
  if ($_ -match "name:\s*'([^']*Id)'") {
    $rows += [pscustomobject]@{ Report=$current; Name=$Matches[1] }
  }
}
"TotalIdFilters=$($rows.Count)"
"UniqueFilterNames=$((($rows | Select-Object -ExpandProperty Name -Unique).Count))"
"ReportsWithIdFilters=$((($rows | Select-Object -ExpandProperty Report -Unique).Count))"
$rows | Group-Object Name | Sort-Object Name | Format-Table Name,Count -AutoSize
```

## Verification Notes

- DONE: `dotnet build Backend/API.csproj --no-restore -o Backend/bin/CheckBuild` succeeded.
- DONE: `npm run build` succeeded in `Frontend`.
- DONE: `ReportLookupsController` uses `IMemoryCache` with a one-day absolute expiration per lookup name.
- Note: normal `dotnet build Backend/API.csproj` was blocked by the currently running `API.exe` process locking `Backend/bin/Debug/net8.0/API.exe` and `API.dll`; the alternate output build was used for compile verification.

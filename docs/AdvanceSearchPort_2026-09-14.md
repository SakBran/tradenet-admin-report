# Advance Search — ported from Tradenet 2.0 Admin (2026-09-14)

> **Superseded in part — see [AdvanceSearchDataCorrectness_2026-09-14.md](AdvanceSearchDataCorrectness_2026-09-14.md).**
> The customer reported that the results did not match the search. The two "Still owed" decisions
> below (Office, multi-select country) are now taken, the date range binds `LicenceDate` instead
> of `IssuedDate`, and several legacy quirks recorded here as deliberate have since been repaired.
> This document still describes the port itself accurately; where the two disagree about current
> behaviour, the newer one wins.

The old admin had an **Advance Search** feature that had never been brought over. It is not a
panel on a report: it was its own top-nav entry (`_Layout.cshtml:1262-1277` on `origin/master`)
opening `Reports/AdvanceSearch?type=menu`, a page of **eight tiles**
(`Views/Reports/AdvanceSearch.cshtml:269-408`). Each tile opened the same screen scoped to one
licence/permit type: a sixteen-field search form over a twenty-three-column, item-grain grid.

All old-admin line numbers below are **on `origin/master`** of `tradenet-2.0-admin` unless said
otherwise; its working tree is the stale 2022 `OGA_Terminate` branch. For the Advance Search
view, JS and DTOs the two happen to be identical, and the four `tradenet-2.0-api` files cited
here are identical to `origin/master` too.

## Where the code actually was

The admin screen is only a shell. `Business/AdvanceSearchRepository.cs:20-39` HTTP-POSTs to
`AppConfig.AdvanceSearchURI` = `apiUrl + "AdvanceSearch/Search"`. The query lived in a **separate
repo**, `tradenet-2.0-api`:

| File | What it is |
| --- | --- |
| `API/Controllers/AdvanceSearchController.cs:14-31` | the endpoint — no `[Authorize]`, wide-open CORS |
| `API/Business/AdvanceSearchRepository.cs:78-1042` | the query: 2,859 lines, EF6 LINQ, eight copy-pasted `if/else if` blocks. **No stored procedure and no raw SQL anywhere.** |
| `API/Models/DTO/AdvanceSearchDTO.cs`, `AdvanceSearchResultDTO.cs` | request and row |

Ported to `Backend/StoredProcedureToLinq/sp_AdvanceSearch.cs`. (It sits beside the `sp_*` files
and takes their naming for consistency, but it never was a procedure — the file says so.)

## Shape

Eight reports, one per tile, as with the EICC port: a report key is what names the route, the
sidebar entry and the Excel export job.

| Report key | Legacy type |
| --- | --- |
| `AdvanceSearchImportLicence` | Import Licence |
| `AdvanceSearchExportLicence` | Export Licence |
| `AdvanceSearchImportPermit` | Import Permit |
| `AdvanceSearchExportPermit` | Export Permit |
| `AdvanceSearchBorderImportLicence` | Border Import Licence |
| `AdvanceSearchBorderExportLicence` | Border Export Licence |
| `AdvanceSearchBorderImportPermit` | Border Import Permit |
| `AdvanceSearchBorderExportPermit` | Border Export Permit |

They group under one **Advance Search** sidebar entry, placed last — where the old nav had it,
straight after Payment.

## The customer's two decisions

1. **Fix only the crashes.** Keep the legacy behaviour otherwise, so row counts stay as close to
   the old screen as possible.
2. **Hide filter boxes whose column does not exist** on that type, rather than rendering them
   dead as the old screen did.

## The legacy defects, and what happened to each

| Legacy behaviour | Now |
| --- | --- |
| `countryReplace` (`:19-28`) called unconditionally at `:1038` on a `ConsignedCountry` the four **Permit** branches never assign → NullReferenceException | blank cell. **This is what made the four Permit screens usable at all** — they returned HTTP 400 for any non-empty result |
| `countryReplace("")` → `Convert.ToInt32("")` → FormatException | blank cell |
| `countryReplace` `.First()` on an id no country carries | that id is skipped |
| `modeOfTransportFun` (`:32-74`) reads `modeList[3]` of a three-element list (`:56`) → picking all three transport modes was a 400 | all six permutations; the one- and two-mode cases are unchanged |
| `Convert.ToInt32("5,12")` on the two `int` country columns → multi-selecting Country of Origin / Consigned Country on Export Licence, Border Export Licence, Export Permit or Border Export Permit was a 400 | matches nothing (`NoMatchId`). Widening those two to "any of the selected countries" is one line, but it moves row counts, so it needs its own decision |
| `.First()` on the PaThaKa lookup (`:118` etc.) → an unknown PaThaKa No was a 400 | empty grid |
| no `else` branch → an unrecognised type left the query null and the pager threw | empty grid |
| **`data.Office` and `data.Airport` have zero references in the whole 2,859-line file** | unchanged. See below |

### Office does not filter — deliberately

The Office (Sakhan) box on the four Border screens has never filtered anything; the legacy
repository joins Sakhan for display only. Decision 1 was specifically about this box, so it is
kept and still inert. **This is the most likely follow-up complaint** — "Sakhan နဲ့ ရှာရင် data
မထွက်" has been filed against this project before. Making it work is one predicate
(`x.SakhanId == Office`) on the four Border branches, but it *will* change row counts.

`Airport` is gone entirely: the old DTO carried it and the old JS read `$("#Airport").val()`, but
the markup has no such control, so it always posted `undefined`.

## Filter box

Labels verbatim from `AdvanceSearch.cshtml:20-154` — including **"Method of Import"**, which the
old view hard-coded for all eight types, Export screens included.

| Box | IL | EL | IP | EP | BIL | BEL | BIP | BEP |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| From / To Date | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| PaThaKa No | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Section | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Seller Country | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Port Of Discharge | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Mode of Transport | ✓ | ✓ | — | ✓ | ✓ | ✓ | — | ✓ |
| Country of Origin | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Consigned Country | ✓ | ✓ | — | ✓ | ✓ | ✓ | — | ✓ |
| Additional Description | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Method of Import | ✓ | ✓ | — | — | ✓ | ✓ | — | — |
| Incoterms | ✓ | ✓ | — | — | ✓ | ✓ | — | — |
| Statement Code | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Office *(inert)* | — | — | — | — | ✓ | ✓ | ✓ | ✓ |
| Application Type | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

Box counts: IL/EL 14 · IP 10 · EP 12 · BIL/BEL 15 · BIP 11 · BEP 13.

Things that bite:

- **"All" is `''` for Section and Application Type, `0` for the rest**
  (`AdvanceSearchreports.js:141-159`). Section is therefore a *string* filter — a literal `0` is
  a real section id to the query and would match nothing.
- **Mode of Transport posts the option text** (`Sea`/`Road`/`Air`), never the `S`/`R`/`A` codes:
  the legacy list was bound `new SelectList(transportList, "Text", "Text")`.
- **A multi-mode selection means "carries exactly these modes", in any order** — the column is a
  comma-joined string and the predicate matches it whole. Not "any of them".
- **Port Of Discharge is an exact match** on the name column.
- **The date range binds `IssuedDate`**, never `LicenceDate`, and is always applied.
- Both date boxes default to **today** (`defaultDateRangeMonths: 0`, new).

### Lookups

Every Section lookup already existed (eight of them, keyed FormType × Oversea/Border), as did
the Method and Incoterm lookups — the legacy model builder keyed those only on
Import/Export × Oversea/Border, never on licence-vs-permit, so the existing
`importlicencemethods` / `exportlicencemethods` / `border*` names serve unchanged.

Two are new, in `ReportLookupsController`: **`importstatementcodes`** and
**`exportstatementcodes`** = `ProductItem` where `Type == "Import"/"Export" && !IsDeleted`. Note
the missing `IsActive` test — the legacy list did not have one, so a de-activated statement code
stays pickable, as before.

## Table

All 23 headers verbatim (`AdvanceSearch.cshtml:163-188`), `Sakhan` on the Border types only:

`Section` · *(`Sakhan`)* · `LicenceNo` · `LicenceDate` · `CompanyRegistrationNo` · `CompanyName` ·
`CompanyAddress` · `SellerName` · `SellerAddress` · `SellerCountry` · `PortOfDischarge` ·
`Last Date` · `Method` · `ConsignedCountry` · `CountryOfOrigin` · `HSCode` · `Description` ·
`AorU` · `Price` · `Qty` · `Value` · `Currency` · `Conditions`

They are unspaced identifiers everywhere except `Last Date`. Grain is one row **per item line**.
`defaultPageSize` is 1000, the legacy page size.

Details worth keeping in mind:

- `Section`, `Sakhan`, `AorU` and `Currency` are all the **`Code`** column, not `Name`.
- The party columns differ per type: Seller (Import Licence, Border Import Licence), Buyer
  (Export Licence, Border Export Licence), Consignee (Export Permit, Border Export Permit),
  Authorised Agent (Import Permit, Border Import Permit).
- `SellerCountry` resolves from `SellerCountryId`, except the Export types (`BuyerCountryId`) and
  Export Permit / Border Export Permit (`ConsignedCountryId` — the legacy projection's own
  inconsistency, since the box beside it filters the buyer).
- `Method` and `ConsignedCountry` are **blank on the four Permit types** — the legacy projection
  left them unset.
- Country columns are resolved to names **after paging**, as the legacy did, because six of the
  eight store a comma-joined id list no SQL here can split. The legacy helper *prepended* each
  name, so `"5,12"` prints as the name of 12 then 5; that reversal is preserved.
- **Every join is INNER**, as before: a licence with no item lines, or an unresolvable
  unit/currency/PaThaKa/Sakhan, does not appear. This is load-bearing for row-count parity.

## Deliberate deviations beyond the crash fixes

Three, all display-side:

1. **`CompanyAddress` uses `string.Concat`**, so a null `UnitLevel` yields the rest of the
   address. The legacy built it with `+` in SQL, which yields NULL if any part is null.
2. **A row-number column**, which the legacy table did not have — every report here has one and a
   thousand-row dump is hard to talk about without it.
3. **The To date is inclusive to 23:59:59**, this app's standard `dateRange` behaviour. The
   legacy screen posted a bare date, so `IssuedDate <= @EndDate` dropped almost everything on the
   last day of the range. Expect the new grid to show *more* rows on that day.

`Price`/`Qty`/`Value` print as `#,##0.0000` — four decimals as the legacy `.ToString()` on a
`decimal(18,4)` gave, plus this app's thousands separators.

## Files

**Backend** — `StoredProcedureToLinq/sp_AdvanceSearch.cs` (the query, the country-name resolver,
the mode permutations), `Service/Reports/AdvanceSearchReport.cs` (paging + Excel streaming, both
ending in the country-name step), `Controllers/Report/AdvanceSearch*Controller.cs` × 8,
`Controllers/ReportLookupsController.cs` (+2 lookups).

**Frontend** — `Report/config/advanceSearchConfigs.ts` (new), `Report/config/reportTypes.ts`
(`multiSelect` filter type, `defaultDateRangeMonths: 0`), `Report/Page/GenericReportPage.tsx`
(render/normalize/initial-value for `multiSelect`; `normalizeFilters` and `getInitialFilterValue`
exported for tests), `Report/Page/AdvanceSearch*.tsx` × 8, `Report/reportRoutes.tsx`,
`Report/reportNavItems.tsx`.

The `multiSelect` type is new and generic: the box posts one comma-joined string, takes its
options from `options` or `lookupName`, and gets **no "All" entry** — empty already means all.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build` (API + tests) | 0 errors, no new warnings |
| Backend suite, `_gate-common.md` §3a DB-skip filter | **7 failures, 1975 passed.** All 7 match `known-failures.json` → `pre-existing-request-factory-and-filter-contract`; identical set before this work |
| `Backend.Tests/AdvanceSearchContractTests.cs` | 64 passed |
| `Backend.Tests/AdvanceSearchQueryTranslationTests.cs` | 33 passed |
| `npx vitest run` | **6 failures, 1710 passed.** The 6 are `known-failures.json` → `reportconfig-parity-assertions`, unchanged |
| `reportConfigs.advanceSearch.test.ts` | 48 passed |
| `multiSelectFilter.test.ts` | 7 passed |
| `npm run build` | passed |
| **Live data** | **not run.** The report database is CGNAT-internal and unreachable here, and the new endpoints are not deployed, so there are no row counts and no old-vs-new comparison |

`AdvanceSearchQueryTranslationTests` is the substitute for the missing database: `ToQueryString()`
runs EF Core's whole translation pipeline without opening a connection, so all eight branches are
proven to produce SQL with every filter applied — which is the real risk in moving EF6 LINQ to
EF Core. It also pins that the Border types join Sakhan, the oversea types do not, and no join
became a LEFT JOIN.

## Still owed

1. **Row counts against the old screen.** The legacy `AdvanceSearch/Search` endpoint is the
   oracle and takes no auth, so this is a straight comparison wherever both are reachable.
   Expect equality except on the last day of a range (deviation 3) and on anything the old
   screen crashed on.
2. **A decision on Office.** It is inert today, faithfully. If the customer expects Sakhan to
   filter on the four Border screens, that is one predicate and a row-count change.
3. **A decision on multi-select country for the four Export types.** Today more than one
   selection matches nothing there; the string-column types keep the legacy whole-string match,
   which only hits a row whose stored list is byte-identical.

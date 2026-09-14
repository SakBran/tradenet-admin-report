# EICC Report — ported from Tradenet 2.0 Admin (2026-09-14)

The old admin had an **EICC Report** that had never been brought over. `docs/ReportColumnComparison.md`
listed `EICCReport.rdlc` under "Old RDLC Files Not Mapped To New Frontend"; the only groundwork in the
repo was an unreferenced LINQ conversion of its stored procedure
(`Backend/StoredProcedureToLinq/sp_EICCReport.cs`).

All old-code line numbers below are **on `origin/master`** of `tradenet-2.0-admin`. The checked-out
working tree there is the stale 2022 `OGA_Terminate` branch and its line numbers differ.

## What the old report was

One view, `Views/EICC/EICCReport.cshtml`, reached from three sidebar entries that differ only by a
`type` query parameter (`Views/Shared/_Layout.cshtml:2052, 2075, 2089`):

| Old sidebar path | `type` | New report |
| --- | --- | --- |
| EICC › Certificates › Reports | `Certificate` | `EICCCertificateReport` |
| EICC › Licence & Permit › Reports | `LicencePermit` | `EICCLicencePermitReport` |
| EICC › Border Licence & Permit › Reports | `BorderLicencePermit` | `EICCBorderLicencePermitReport` |

They are three reports here rather than one with a Type dropdown because a report key is what names
the route, the sidebar entry and the Excel export job. Two configs cannot share a `controllerName`
without colliding in the menu (see `docs/` note on aliased controller names).

Data: `dbo.sp_EICCReport` (`Business/Reports.cs:5353`), already converted to LINQ.

## Filter box parity

| Old control (`EICCReport.cshtml`) | New filter | Notes |
| --- | --- | --- |
| EICC Date, required, dd/MM/yyyy, defaults today (:26) | `Date` | Posted as `Date`, not `EICCDate`, so `ExcelRequestDates.Describe` finds it and the sheet prints its "Date:" line. Label stays "EICC Date". |
| EICC Status, Pending / Approved, defaults Pending (:32-49) | `EICCStatus` | No "all" option in either: the procedure compares `Status` with `=`. |
| Card Type (:55-83) | `FormType` | Certificate: from the `eiccCardTypes` lookup. Licence/Permit and Border: the four values the old controller hardcoded (`EICCController.cs:400`, `:455`). |
| Product Group (:96), Licence/Permit + Border only | `ProductGroupId` | Cascades from `FormType`. |
| Product Item (:105), Licence/Permit + Border only | `ProductItemId` | Cascades from `ProductGroupId`. |

The Certificate screen hides both product boxes (`eicc-reports.js:26-33`); its config simply omits them
and its request DTO has no such properties.

### The card-type list

`EICCController.cs:409`: every card type except `Member` and anything whose name contains `Licence` or
`Permit` — i.e. the registration types. `Wine Importation` is the stored value but the dropdown showed
`Alcoholic Beverages Importation` (`EICCReport.cshtml:63`). Both are reproduced in the new
`eiccCardTypes` lookup, which posts the Description because the procedure matches it as
`FormType LIKE @FormType + '%'`.

### The product cascade

`eicc-reports.js:64-143` refetched both lists over AJAX: the group list narrowed to the Export or
Import side of the chosen card type (`formType.includes("Export") ? "Export" : "Import"`), the item
list to the chosen group. The new lookups ship once and cascade on the client: a Product Group option
carries `parentCode` ("Import"/"Export"), a Product Item carries `parentId` (its group).
`filterCascade.ts` gained a text-parent branch for the first of those.

**Deliberate deviation:** with Card Type on `--- All ---`, the old screen fell through its
`includes("Export")` test and listed the **Import** groups only, so an Export group could not be picked
under "All". The new list shows both sides there. Everything else matches.

## Table columns

All ten `EICCReport.rdlc:205-756` headers, in tablix order, English, no footer and no drill-through
(the RDLC has neither a `Sum(` nor a `Drillthrough`):

`No` · `EICC No` · `Date` · `Status` · `Form Type` · `Application Type` · `Company Registration No` ·
`Company Name` · `Company Address` · `Remark`

Four RDLC headers are split over two cells ("Application " / "Type"); each pair is one phrase in the
grid. `Company Address` is composed with `LegacyCompanyAddress.Compose`, the old
`CommonRepository.GetAddress` reproduced byte for byte, as a computed property on the row so the grid
and the sheet print the same string.

`defaultPageSize` is 1000: the RDLC printed every row on one scrolling page.

## Changes to the shared query

`sp_EICCReport.Query` kept every filter and branch shape verbatim, with two changes that cannot alter
which rows come back:

1. **Branch dispatch.** The procedure UNIONed all three `@Type` branches behind `@Type = '...'`
   predicates, so every run built all of them (the Certificate branch alone is eight joins and unions).
   Only one branch can return rows, so only that one is built now. Each branch keeps its own
   `request.Type == type` guard, which is what makes an unrecognised Type return nothing.
2. **Deterministic order.** `ORDER BY CreatedDate` gained a `ThenBy(EICCId)` tiebreak, so the grid's
   OFFSET paging cannot repeat or drop a row between pages. The old report was never paged.

## Preserved as-is (watch items)

- **The EICC date is matched exactly** (`EICCDate >= @EICCDate AND EICCDate <= @EICCDate`), not as a
  whole-day window. If any `EICCCertificate.EICCDate` carries a time of day, that row is invisible to
  both the old and the new report. The controller truncates the request date to midnight so the new
  one behaves identically. **Not verified against real data** — the report database is unreachable
  from this machine. If the customer reports "no data" for a date they expect rows on, this is the
  first thing to check, and widening to `>= @Date AND < @Date + 1` is the fix.
- **The Export Licence branch demands an exact product match.** `LicencePermitBaseRows(..., strictProductFilter: true)`
  applies only to Export Licence: with Product Group / Item left on "All" (0) it matches only
  certificates whose `ProductGroupId` and `ProductItemId` are both 0. Every other branch treats 0 as
  "all". That asymmetry is the old procedure's and was not corrected.
- **Card types are filtered `IsActive && !IsDeleted`**, matching every other master-data lookup in
  `ReportLookupsController`. The old list came from an API endpoint whose own filtering could not be
  read from this repo.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build Backend` | 0 errors |
| Backend gate (`_gate-common.md` §3a DB-skip filter) | 7 failures, all in `known-failures.json` (`ReportEndpointPayloadFixtureTests`, `ReportControllerBranchDefaultsTests` — three controllers with no `TryCreateReportRequest`); unchanged from the pre-change baseline |
| `Backend.Tests/EICCReportContractTests.cs` | 17 passed |
| `npm test` | 6 failures, identical to the pre-change baseline (Border Export Permit / Border Import Licence / Export Licence config parity, `known-failures.json`) |
| `npm run build` | passed |
| Live data | **not run** — the report database is CGNAT-internal and unreachable here. No row counts, no old-vs-new comparison. |

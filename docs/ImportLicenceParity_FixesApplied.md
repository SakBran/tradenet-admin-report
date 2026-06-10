# Import Licence — Report Parity Fixes Applied (2026-06-06)

Customer complaint: the new (fast) Import Licence reports are **not the same** as Tradenet 2.0
in **Filter box**, **Table structure**, and **Total**. Parity check done against the old
`tradenet-2.0-admin/TradenetAdmin` code (RDLC headers + filter `.cshtml` forms + `Resources.resx`)
per `CLAUDE.md`. Backend `dotnet build` ✅ and Frontend `tsc --noEmit` ✅ after the changes.

## Summary of what was verified vs. what was wrong

| Axis | Verdict | Action |
|---|---|---|
| **Table columns** | ✅ Already at parity for **all 16** non-border Import Licence reports (`ReportColumnComparison.md` = Need:None / Extra:None). | No change. |
| **Filter box — filters present** | ✅ Match (Type is correctly hidden/derived to `Oversea`; Section pinned to import-only lookup; Sakhan correctly absent on non-border). | No change. |
| **Filter box — labels** | ❌ 4 labels did not match the old `Resources.resx` text. | **Fixed (frontend).** |
| **Total row** | ❌ Only the Daily report had a Total; old RDLCs also have a TOTAL row on By Method / By Section / By Seller Country / Company List. | **Fixed (backend).** |
| **Total USD Value (Daily)** | ⚠️ Column is empty (new backend has no FX conversion). Pre-existing data gap. | Documented — needs a decision (see below). |

## 1. Filter-box label fixes — `Frontend/src/Report/config/reportConfigs.ts`

Old labels come from `tradenet-2.0-admin` `Resources/Resources.resx`, confirmed against the old
filter forms (`Views/Reports/ImportLicence*.cshtml`).

| Filter (shared const) | Old label (2.0) | Was (new) | Now (new) |
|---|---|---|---|
| `importLicencePaThaKaTypeFilter` | `PaThaKaType` = **EIR Card Type** | PaThaKa Type | **EIR Card Type** |
| `importLicenceMethodFilter` | `ImportMethod` = **Method of Import** | Import Method | **Method of Import** |
| `importLicenceCompanyMethodFilter` (Company List) | `ExportMethod` = **Method of export** | Export Method | **Method of export** |
| `importLicenceIncotermFilter` | `ImportIncoterms` = **Method of Import According to Incoterms** | Import Incoterms | **Method of Import According to Incoterms** |

These are shared filter consts, so the corrected labels apply to every Import Licence report that
uses them (Daily, Detail, By Method, By Section, By Seller Country, Company List, Total Value, …).

## 2. Grand-total ("TOTAL") footer rows — backend

The old RDLCs render a grand **TOTAL** row = `CountDistinct(LicenceNo)` (→ No of Licences) +
`Sum(Amount)` (→ Total Value), across all groups. The new app drives this via
`ApiResult.ColumnTotals` (keyed by column `dataIndex`) → `BasicTable` `<tfoot>` "Total" row.
Only the Daily report set it. Added it to the other summary reports that had a TOTAL row in 2.0:

- **Shared helper** `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs` —
  `CreateAggregateResultAsync(...)` gained an opt-in `bool includeColumnTotals = false`. When true it
  sets `ColumnTotals { noOfLicences = Σ NoOfLicences, totalValue = Σ TotalValue }` over **all** groups
  (computed before paging, so it's the true cross-page total, not just the current page).
- `ImportLicenceByMethodReportController.cs` → `includeColumnTotals: true`
- `ImportLicenceBySellerCountryReportController.cs` → `includeColumnTotals: true`
- `ImportLicenceCompanyListReportController.cs` → `includeColumnTotals: true`
- `ImportLicenceBySectionReportController.cs` — uses its own cached in-memory paging path; added the
  same `result.ColumnTotals` block summing across all cached section rows.
- `ImportLicenceDailyReportNewLicenceReportController.cs` — already had it (commit `f1ce97f`); unchanged.

## 3. Already-correct items (no change — earlier "stale build" complaints)

- **Section dropdown showing 1,2,3** — `importLicenceSectionFilter` already pins
  `lookupName: 'importLicenceSections'` (import-only: `Type='Import Licence' && IsOversea`). The leaky
  generic `exportImportSections` lookup is only the fallback when no `lookupName` is set.
- **Remove Sakhan** — non-border `importLicence*` filter arrays already omit `SakhanId` (it belongs to
  Border reports only).
- If a customer still sees either, it is a **pre-`643afe3` deployed build**, not current source.

## 4. Open item — Total USD Value (Daily report)

Old `ImportLicenceByDailyReport.rdlc` shows a per-currency **Total USD Value** column and totals it in
the footer (`Sum(totalUSDAmount)`). The new backend leaves `TotalUSDValue = null`, so the column and its
total render blank. The old USD figure is computed in `Business/Reports.cs` from an **ExchangeRate** table:

```
ori_rate = rate(licenceDate, itemCurrency);  usd_rate = rate(licenceDate, "USD")
USD == currency        -> Amount
KRW / JPY              -> Amount * ((ori_rate / usd_rate) / 100)
otherwise             -> Amount * (ori_rate / usd_rate)
```

Implementing this requires joining the ExchangeRate data per (LicenceDate, Currency) in the new
aggregation. **Decision needed**: implement the FX conversion (backend + verify ExchangeRate data exists
in TradeNetDB) or leave the USD column blank. Not changed here. Note: the current Daily Total therefore
totals **Total Value** (mixed currency) instead of the old USD total, since USD is unavailable.

---

# Import Permit + Border Export Licence (2026-06-06, "complaint-scoped" per user)

Scope chosen by the user: fix what customers complain about + safe parity (Sakhan, labels, Total
rows, Section dropdown leak). KEEP the extra filters the new app added; do NOT add the readonly
Company Name field; skip structural items (HSCode missing Export Section, FilterType→dropdown,
TotalValue missing count column). Those are listed as "deferred" below.

## Filter-box labels (frontend, file-wide, display-only — verified vs old `Resources.resx`)
Applied with `replace_all` because each is the correct old label for its whole domain:
- `'PaThaKa Type'` → `'EIR Card Type'` (42 sites, all groups — `PaThaKaType` resx = "EIR Card Type").
- `'Export Method'` → `'Method of export'` (14 sites, all export-domain reports — `ExportMethod` resx).
- `'Export Incoterms'` → `'Method of export According to Incoterms'` (14 sites — `ExportIncoterms` resx).
- `'Amend Remark'` → `'Remark'` (amend/actual-amend reports — `Remark`/`AmendRemark` resx = "Remark").
- `'Filter Type'` → `'Filter By'` (HS Code reports — `FilterBy` resx = "Filter By").

## Stray Sakhan removed — Import Permit (frontend)
Old NON-border permit forms have NO Sakhan dropdown (Sakhan = border station). Removed the
`SakhanId/'Sakhan'` filter from all **12** non-border `ImportPermit*` reports (scoped script,
Border reports untouched — they keep Sakhan legitimately; file-wide Sakhan count 93 → 81).

## Section dropdown leak fixed — Permit + Border Export (backend + frontend)
The Section filters fell back to the generic `exportImportSections` lookup, which returns ALL
sections (DB: 4 Import Licence + 1 Import Permit + 1 Export Licence + 1 Export Permit = 7) instead
of the type-appropriate ones. **DB verified** (`ExportImportSection`): every active section row has
`IsOversea=1 AND IsBorder=1`, so **Type** is the real discriminator.
- New lookups in `Backend/Controllers/ReportLookupsController.cs`:
  - `importPermitSections` → `Type=="Import Permit" && IsOversea` (legacy `GetAll(AppConfig.ImportPermit)`).
  - `borderExportLicenceSections` → `Type=="Export Licence" && IsBorder` (legacy `GetAll(AppConfig.ExportLicence)` border).
- Pinned `lookupName` on the Section filters: 11 ImportPermit reports + 13 BorderExportLicence reports
  (the one HSCode report in each group has no Section filter). (Lookups are cached ~1 day in-memory —
  restart backend to see the change.)

## Grand-total ("TOTAL") footer rows added (backend)
`ReportAggregationService.CreatePagedResult(...)` gained the same opt-in `includeColumnTotals` flag
(sums `noOfLicences` + `totalValue` across ALL groups), and both `sp_ImportPermitDetailReport_Fast`
and `sp_ExportLicenceDetailReport_Fast` `CreateAggregateResultAsync` forward it. Flipped
`includeColumnTotals: true` on the summary controllers whose old RDLC has a TOTAL row:
- Permit: BySection, BySellerCountry, CompanyList, Daily.
- Border Export: BySection, ByMethod, BySellerCountry, CompanyList, Daily.
- (Permit/Border-Export Detail has no old TOTAL → left off.)

## Report title added (frontend) — was missing on Permit + Border Export
Every Import Licence report already had a `reportSubtitle` (the in-grid report title, e.g.
"List of Import Licences By Daily From (date) To (date)"), but **all 12 Import Permit and all 14
Border Export Licence reports had none** — so they showed no title. Added `reportSubtitle:
importLicenceRangeSubtitle('<label>', <includeFrom>)` to all 26, using each report's **old
Tradenet 2.0 `header1` text** (e.g. "List of Import Permit By Daily", "List of Border Export Licences
By Section", "Import Permit Voucher List", "Border Export Licences Total Value & Licences"). The
title renders once filters are applied, matching the old ReportViewer. (Vite HMR picks this up — just
refresh; no backend restart needed.)

## Deferred (NOT changed — out of "complaint-scoped"; documented for a later pass)
- Extra filters the new app added that the old forms lack (Seller/Buyer Country, Incoterms,
  Company Reg No on some summary reports; a stray `Auto` filter+column on the New reports).
- Missing readonly **Company Name** field on reports whose old form had it (~8 permit + ~8 border).
- **HSCode** reports: missing Export Section filter; FilterType should be a dropdown; and the
  HSCode **Total row** (sp_HSCodeReport pages in SQL with a windowed count — needs an extra
  aggregate query, so not a one-flag change). Same for the per-licence **listing** reports'
  Total rows (Amendment/Extension/Cancellation/New/Voucher) which need per-controller cross-page sums.
- BorderExportLicence **Total USD Value** (Daily) — empty, same FX gap as Import Licence Daily.
- `BorderExportLicenceBySellerCountryReport` key/title says "Seller" but it's an export "Buyer
  Country" report (filter wiring already uses BuyerCountryId — naming only).

## Files changed (full task)
- `Frontend/src/Report/config/reportConfigs.ts` — labels (all groups), Sakhan removal (12 permit),
  Section `lookupName` pins (11 permit + 13 border export).
- `Backend/Controllers/ReportLookupsController.cs` — 2 new section lookups + 2 Type constants.
- `Backend/Service/Reports/ReportAggregationService.cs` — `CreatePagedResult` opt-in totals.
- `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs`,
  `sp_ImportPermitDetailReport_Fast.cs`, `sp_ExportLicenceDetailReport_Fast.cs` — opt-in totals param.
- Controllers flipped to `includeColumnTotals: true`: Import Licence (ByMethod, BySection,
  BySellerCountry, CompanyList) + Import Permit (BySection, BySellerCountry, CompanyList, Daily) +
  Border Export Licence (BySection, ByMethod, BySellerCountry, CompanyList, Daily); Import Licence
  BySection sets ColumnTotals inline (custom cached path).

Verified: backend `dotnet build` 0 errors, frontend `tsc --noEmit` clean.
</content>
</invoke>

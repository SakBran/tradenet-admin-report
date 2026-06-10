# Export Permit — Report Parity Audit & Fix Plan (2026-06-10)

> **Status:** **APPLIED 2026-06-10** (full complaint-scoped). Frontend `tsc --noEmit` ✅ and backend
> `dotnet build` ✅ (0 errors). See **§0 Implementation status** for exactly what shipped vs. deferred.
> This was the to-do plan, mirroring the prior Import Licence / Import Permit complaint passes.

## 0. Implementation status (applied 2026-06-10)

**Done (full complaint-scoped):**
- **Filters** — Section `lookupName` pinned on all 22 reports that have a Section filter
  (`exportPermitSections` / `borderExportPermitSections`); the 2 ByHSCode reports correctly have no
  Section filter (HS-code/FilterType-driven, matching the old form). Stray **Sakhan** removed from all
  12 non-border reports (Border kept). Stray `Auto` / `BuyerCountryId` removed. `FilterType` → Start/End
  `select` on both ByHSCode reports.
- **Backend lookups** — `ExportPermitFormType` const + `GetExportPermitSections` (`IsOversea`) +
  `GetBorderExportPermitSections` (`IsBorder`) + 2 switch keys in `ReportLookupsController.cs`.
- **Columns** — duplicate Licence binding fixed (`oldLicenceNo`) on the 7 listed reports; stray
  columns removed / `hsCode` titles fixed per §3; Border Voucher `header2/3` wired to
  `resolveImportLicenceVoucherColumns`.
- **Footer totals** — `currencyTotalsColumns` (FE) + backend `CurrencyTotals` for the listing/voucher
  reports (New / Amend / **ActualAmend** / **Cancel** / Extension / Voucher, both families); the
  ActualAmend + Cancel branches were added to `sp_ExportPermitListingCurrencyTotals.sql` and the 4
  controllers wired (closing a gap left by the first implementation pass). `includeColumnTotals: true`
  on the By* + Daily controllers (both families); `sp_ExportPermitDetailReport_Fast` gained the
  `includeColumnTotals` param.
- **Nav** — `reportSubtitle` added to the 22 reports per §5 (exact old `header1` text).
- **Drilldown** — Section / Country / Company → Detail on the 6 summary reports (`CountryId =
  row.BuyerCountryId` mapped in `sp_ExportPermitDetailReport_Fast` so the country drilldown is populated).

**Deploy note:** new procs `sp_ExportPermitListingCurrencyTotals.sql` + `sp_ExportPermitVoucherCurrencyTotals.sql`
must be applied to the DB by hand (footer degrades to empty until then — no wrong data). Backend
lookups are cached ~1 day → **restart the backend** to see the Section dropdown fix.

**Deferred (still open — §8):** Total USD Value FX gap (Daily); `includeColumnTotals` over-totals Total
Value on By* reports; BorderExportPermitBySection footer intent; Voucher extra columns; Border
`Type`/`FormType` visible filter (family-wide pass); Voucher PaymentType casing; HS Code drill-downs
(blocked on building an HS-code detail report key); readonly CompanyName autofill.

---

> **Customer complaint:** the new (fast) Export Permit reports are **not the same** as Tradenet 2.0
> in **navigation/title**, **table footer (Total)**, **filter values**, **table structure (columns)**,
> and **table-link navigation (drill-down)**.
> **Parity check** done against the old `tradenet-2.0-admin/TradenetAdmin` code per `CLAUDE.md`:
> `.rdlc` (table columns + TOTAL footers + Drillthrough), `Views/Reports/*.cshtml` (filter box),
> `Controllers/ReportsController.cs` (filter dropdown sources + `header1`), `Resources/Resources.resx` (labels).
> Findings below are the **adversarially-verified** set (verdict = `confirmed` or `adjusted`); `refuted`
> items are dropped.

## Scope — 24 reports

| Group | Reports |
|---|---|
| **Non-border (Oversea)** — 12 | ExportPermitActualAmendmentReport, ExportPermitAmendmentReport, ExportPermitByHSCodeReport, ExportPermitBySectionReport, ExportPermitBySellerCountryReport, ExportPermitCancellationReport, ExportPermitCompanyListReport, ExportPermitDailyReportNewPermitReport, ExportPermitDetailReport, ExportPermitExtensionReport, ExportPermitNewReportNewReport, ExportPermitVoucherReport |
| **Border** — 12 | BorderExportPermitActualAmendmentReport, BorderExportPermitAmendmentReport, BorderExportPermitByHSCodeReport, BorderExportPermitBySectionReport, BorderExportPermitBySellerCountryReport, BorderExportPermitCancellationReport, BorderExportPermitCompanyListReport, BorderExportPermitDailyReportNewPermitReport, BorderExportPermitDetailReport, BorderExportPermitExtensionReport, BorderExportPermitNewReportNewReport, BorderExportPermitVoucherReport |

The non-border controllers scope to `IsOversea` (old `model.Type = AppConfig.Oversea`); the Border
controllers scope to `IsBorder` (`model.Type = AppConfig.Border`). This distinction drives **every**
systematic fix below (Sakhan presence, the Section lookup `Type='Export Permit' && IsOversea` vs
`&& IsBorder`).

---

## 1. Summary of diffs by axis

| Axis | Reports affected | Highest severity | Nature of fix |
|---|---|---|---|
| **Filters** | **24 / 24** | high | Section dropdown leak (all 24); stray Sakhan on 12 non-border; FilterType free-text→Start/End dropdown (HSCode); stray `Auto`/`BuyerCountryId` filters; visible `Type`/`FormType` (Border); missing readonly CompanyName |
| **Columns** | **11** | high | Duplicate Licence/OldLicenceNo binding; stray `hsCode`/`Auto`/`CommodityType`/`ApplicationDate` columns; lowercase `hsCode` titles; Voucher `=Parameters!header2/3.Value` literals |
| **Footer Total** | **22** | high | Per-currency + grand TOTAL footer (listing reports); grand No-of-Licences `columnTotals` (By* summary reports); Voucher single `SUM(Amount)` |
| **Nav-Title** | **22** | medium | Missing `reportSubtitle` reproducing old `header1` text |
| **Drilldown** | **5** | high | Section / Country / Company / HSCode cell → Detail/HSCodeDetail report |

> The two **systematic** items (every report has the Section-lookup leak; every non-border report
> has a stray Sakhan) are the same defect class fixed in `ImportLicenceParity_FixesApplied.md`
> (lines 95–110) for the Import Permit / Border Export Licence families.

---

## 2. Filters

### 2a. Section dropdown leak — **all 24 reports** (systematic, high)

**Problem (verified):** every Export-Permit report's `ExportImportSectionId` filter has **no
`lookupName`**. `GenericReportPage.tsx` `getLookupFilter()` (line 392) falls back, for any `*Id`
filter, to `idFilterLookups['ExportImportSectionId']` (lines 92–95) = `{ lookupName:
'exportImportSections' }`. That resolves to `GetExportImportSections` (`ReportLookupsController.cs`
lines 190–197), whose `Where` is **only** `item.IsActive && !item.IsDeleted` — **no `Type`, no
`IsOversea`/`IsBorder`** — so it returns every section across all FormTypes (Import Licence + Import
Permit + Export Licence + Export Permit) and both border + oversea. Old controllers scope tightly:

- Non-border: `exportImportSectionRepository.GetAll(AppConfig.ExportPermit).Where(IsActive && IsOversea)`
- Border: `exportImportSectionRepository.GetAll(AppConfig.ExportPermit).Where(IsActive && IsBorder)`

`AppConfig.ExportPermit = "Export Permit"` (`AppConfig.cs:1196`). The `ReportLookupsController.cs`
switch (lines 49–69) has `importlicencesections`, `importpermitsections`,
`borderimportpermitsections`, `borderexportlicencesections` — but **no export-permit case at all**,
and there is **no `ExportPermitFormType` constant** (only ImportLicence/ImportPermit/ExportLicence
exist at lines 27–30). So pinning alone 404s — **the backend cases must be created first.**

**Fix (backend `ReportLookupsController.cs`):** add `const string ExportPermitFormType = "Export Permit"`,
then mirror the existing `GetImportPermitSections` (lines 241–252) / `GetBorderExportLicenceSections`
(lines 271–282):

| New method | Predicate | Switch key | Pin on |
|---|---|---|---|
| `GetExportPermitSections()` | `IsActive && !IsDeleted && Type == ExportPermitFormType && IsOversea` | `"exportpermitsections"` | 12 non-border reports → `lookupName: 'exportPermitSections'` |
| `GetBorderExportPermitSections()` | `IsActive && !IsDeleted && Type == ExportPermitFormType && IsBorder` | `"borderexportpermitsections"` | 12 Border reports → `lookupName: 'borderExportPermitSections'` |

> **Do NOT reuse `borderExportLicenceSections`** for the Permit reports — it filters
> `Type=='Export Licence'`, the wrong set (confirmed for BorderExportPermitByHSCode/Cancellation/Voucher).
> Per `ImportLicenceParity_FixesApplied.md:102-105`, every active section row has `IsOversea=1 AND
> IsBorder=1`, so **`Type` is the real discriminator** — confirm `IsOversea`/`IsBorder` is still
> meaningful on live data before relying on it (`Type=="Export Permit"` alone may suffice).
> Restart the backend after the change (lookups cached ~1 day).

**Note on ByHSCode/Cancellation:** `ExportPermitByHSCodeReport`, `ExportPermitCancellationReport`, and
`BorderExportPermitByHSCodeReport` are **missing the Section filter entirely** (not merely unpinned) —
the new config has no `ExportImportSectionId` filter. Add it (label `'Export Section'`,
`type:'select'`) pinned to the scoped lookup. (Per the BorderImportPermit precedent, if the backend
proc/request model has no `@ExportImportSectionId` for the HSCode path, a frontend-only filter would
be a dead control — verify the proc accepts the param before adding.)

### 2b. Stray Sakhan filter — **12 non-border reports** (systematic, high/medium)

Old NON-border forms have **no Sakhan dropdown** (Sakhan = border station). Every non-border
Export-Permit config block carries a stray `{ name:'SakhanId', label:'Sakhan', type:'number',
defaultValue:0 }`. Remove it from all 12; Border reports keep Sakhan legitimately (their old forms
render it and the controllers populate `SakhanList`). This mirrors the 12-report Sakhan removal in
`ImportLicenceParity_FixesApplied.md:95-98`.

| Report | Stray SakhanId at |
|---|---|
| ExportPermitActualAmendmentReport | reportConfigs.ts 8402–8407 |
| ExportPermitAmendmentReport | 8515–8518 |
| ExportPermitByHSCodeReport | 8620–8625 (also `initialSortColumn:'SakhanId'` at 8589 — border artifact) |
| ExportPermitBySectionReport | 8709–8714 |
| ExportPermitBySellerCountryReport | 8788–8793 |
| ExportPermitCancellationReport | 8855–8860 |
| ExportPermitCompanyListReport | 8978–8983 |
| ExportPermitDailyReportNewPermitReport | 9057–9062 |
| ExportPermitDetailReport | 9143–9148 |
| ExportPermitExtensionReport | 9330–9335 |
| ExportPermitNewReportNewReport | 9431–9436 |
| ExportPermitVoucherReport | 9574–9579 |

> **Refuted (do NOT touch):** all 12 **Border** reports legitimately keep Sakhan (old cshtml renders
> it; verified for BorderExportPermit ActualAmendment/Amendment/ByHSCode/BySection/BySellerCountry/
> Cancellation/CompanyList/Daily/Detail/Extension/New/Voucher).

### 2c. Other stray filters — non-border (medium)

- **`Auto`** on `ExportPermitNewReportNewReport` (9437–9442): no counterpart in old form;
  `sp_NewReport` non-border path accepts no `@Auto`. Remove.
- **`BuyerCountryId`** on `ExportPermitCompanyListReport` (8966–8971) and
  `ExportPermitDailyReportNewPermitReport` (9045–9050): old forms have no Buyer Country dropdown;
  controllers never populate one. Remove. (These were missed by the original audit but confirmed
  during verification.)

### 2d. FilterType free-text → Start/End dropdown — HSCode reports (medium)

`ExportPermitByHSCodeReport` (8608–8613) and `BorderExportPermitByHSCodeReport` (2322–2327) render
`FilterType` as `type:'text'`. Old form is a `DropDownListFor(model.FilterType, Model.FilterTypeList)`
populated from `CommonRepository.GetFilterType()` (`CommonRepository.cs:580-593`) → exactly two values
`AppConfig.Start = 'Start'` / `AppConfig.End = 'End'` (`AppConfig.cs:1337-1338`), bound `Value→Value`.
Convert to `type:'select'` with static options `Start` / `End` (value == label). Keep label `'Filter By'`
(matches `Resources.resx` `FilterBy`).

### 2e. Visible `Type` / `FormType` on Border reports (low)

Old Border forms render `@Html.HiddenFor(model => model.Type)` and the controller pins
`model.Type = AppConfig.Border`. The new config exposes a **visible editable** `type:'text'`
`defaultValue:''` filter on BorderExportPermit BySection (2393–2398), BySellerCountry (2472–2477),
CompanyList (2667–2672), Daily (2746–2751), Detail (2832–2837); `BorderExportPermitCancellationReport`
exposes the same for `FormType` (2551–2556). The backend controllers hard-code the value server-side,
so the box is **inert/cosmetic**. This is a **platform-wide convention** across every Border family
block — **handle consistently across the whole Border family in one pass** (hide it / drop it from
`filters[]` / confirm backend defaulting), **not per report**. Low priority.

### 2f. Missing readonly CompanyName autofill — (low)

Old forms with a Company Registration No typeahead also render a readonly `CompanyName` echo
(auto-filled via `GetByCompanyRegistrationNo`). The new config omits it on
`ExportPermitCancellationReport`, `ExportPermitDailyReportNewPermitReport`,
`BorderExportPermitActualAmendmentReport`, `BorderExportPermitAmendmentReport`,
`BorderExportPermitDailyReportNewPermitReport`. Add a filter
`{ type:'readonlyText', populateFromCompanyRegistrationNo:true, excludeFromRequest:true }` (reuse the
shared `importLicenceCompanyNameFilter` const at reportConfigs.ts 168–175). Display-only, not a query
param — low priority.

### 2g. Filter labels — verified OK

`Export Section` / `Remark` / `Company Registration No` / `Filter By` already match `Resources.resx`
(ExportSection=480-482, Remark=828-829, CompanyRegistrationNo=330, FilterBy=1248-1250). The
`PaThaKaType` label resolves to **`EIR Card Type`** (`Resources.resx:762`) — the new
`PaThaKaTypeId` filters already use `'EIR Card Type'`, matching. **No label change needed.**

---

## 3. Columns

### 3a. Duplicate Licence binding — both columns map to `licenceNo` (high)

Old RDLCs place the **`Licence No`** header over `=Fields!OldLicenceNo.Value` (the original) and the
amendment/extension/cancellation number header over `=Fields!LicenceNo.Value`. The new config binds
**both** columns to `dataIndex:'licenceNo'`, so the original number is never shown and the value
duplicates. Fix: change the first column's `dataIndex` from `'licenceNo'` to `'oldLicenceNo'`; leave
the second on `'licenceNo'`.

| Report | "Licence No" col → should be | Notes |
|---|---|---|
| ExportPermitActualAmendmentReport | `oldLicenceNo` (key `LicenceNo`, 8416–8418) | verify DTO exposes `oldLicenceNo` |
| ExportPermitAmendmentReport | `oldLicenceNo` (Licence No vs Licence Amendment No) | flagged outside finding |
| ExportPermitCancellationReport | `oldLicenceNo` (CancellationNo at 8879–8882 reuses `licenceNo`) | |
| BorderExportPermitActualAmendmentReport | `oldLicenceNo` (2124–2133) | backend already returns `oldLicenceNo` (`sp_ActualAmendReport.cs:27/394`) — frontend-only |
| BorderExportPermitAmendmentReport | `oldLicenceNo` (2242–2250) | RDLC OldLicenceNo line 992 / LicenceNo line 1045 |
| BorderExportPermitCancellationReport | `oldLicenceNo` (2589/2594) | |
| BorderExportPermitExtensionReport | `oldLicenceNo` (3068–3070) | backend already returns `oldLicenceNo` (`sp_ExtensionReport.cs:27/73`, controller 408) — frontend-only |

### 3b. Stray `hsCode` column not in old RDLC (medium)

The amend/cancel listing RDLCs have **no HS Code** field/header. The new config inserts
`{ key:'hsCode', dataIndex:'hsCode', title:'hsCode' }` with the raw lowercase key as the title.

| Report | hsCode col at | Backend sources HSCode? | Action |
|---|---|---|---|
| ExportPermitActualAmendmentReport | 8458–8462 | — | Remove (strict parity); else title `'HS Code'` |
| ExportPermitAmendmentReport | 8570–8574 | — | Remove |
| ExportPermitCancellationReport | 8863–8867 (leading col) | — | Remove |
| BorderExportPermitActualAmendmentReport | 2167–2171 | **yes** (`sp_ActualAmendReport.cs:39/87/414`) | Live intentional col → at minimum fix title `'HS Code'`; removal is a product call |
| BorderExportPermitAmendmentReport | 2284–2288 | — | Remove |
| BorderExportPermitCancellationReport | 2636–2640 | **yes** (`sp_CancelReport.cs:38/252`) | Fix title `'HS Code'`; removal is a product call |
| BorderExportPermitDetailReport | 2965–2969 | yes (legit col) | Title casing only: `'hsCode'` → `'HSCode'` (matches RDLC `<Value>HSCode</Value>` line 1407) |

### 3c. Extra columns on summary/listing reports (medium)

- **`ExportPermitByHSCodeReport`** — stray `Company Name` column (8653–8657); HSCodeReport.rdlc renders
  exactly 6 columns (Sr.No., HS Code, Description, No of Licences, Total Value, Currency). Remove.
- **`ExportPermitNewReportNewReport`** — four stray columns `CommodityType` (9489–9493), `hsCode`
  (9494–9498), `Quota` (9499–9503), `Auto` (9504–9508); `NewLicenceReport.rdlc` has exactly 8 columns
  and the SP projects none of the four. Remove all four. Also correct
  `docs/ReportColumnComparison.md:780-781` (wrongly lists 12).
- **`BorderExportPermitNewReportNewReport`** — stray `Auto` column (3218–3222); `BorderNewReport.rdlc`
  has 9 columns and no `auto` header (only `<AutoRefresh>`). Remove. Correct
  `docs/ReportColumnComparison.md:258` (lists a spurious 'auto'; old has 9 not 10).
- **`BorderExportPermitVoucherReport`** — `Application Date` (3305–3310) + `Commodity Type` (3347–3351)
  absent from the 11-column `BorderVoucherReport.rdlc`. **Confirm-before-drop** — both exist in the
  sibling `ImportLicenceVoucherReport` new config, so they may be intentional cross-family enrichment.

### 3d. Voucher dynamic header columns (high)

`ExportPermitVoucherReport` and `BorderExportPermitVoucherReport` ship literal SSRS expression titles.
Old `VoucherReport.rdlc` col-2/col-4 headers are `=Parameters!header2.Value` / `=Parameters!header3.Value`,
resolved by `ApplyType` in the controller (New→`Licence No`/`Licence Date`; Amend→`Licence Amendment
No`/`Amendment Date`; Extension/Cancel/Actual Amend similarly).

- **ExportPermitVoucherReport** — static titles `'Licence No'`/`'Licence Date'` (9585/9595) **match the
  default `ApplyType='New'`**, so default-case parity is OK. Optional fidelity only (non-default
  ApplyType diverges). Low.
- **BorderExportPermitVoucherReport** — titles are the raw `'=Parameters!header2.Value'` /
  `'=Parameters!header3.Value'` (keys `ParametersHeader2Value`/`ParametersHeader3Value`, 3317/3322).
  Wire `resolveColumns: resolveImportLicenceVoucherColumns` (reportConfigs.ts 388–407) **and** rename
  the two keys to `'LicenceNo'`/`'LicenceDate'` so the resolver matches (mirror ImportLicenceVoucher
  10770/10780). De-dupe against the existing static `LicenceNo` column (3301) first. Note
  `importLicenceVoucherHeaders` (354–363) has **no De-Cancel** mapping while the new ApplyType filter
  offers De-Cancel — it falls through to the New labels (old controller also has no De-Cancel branch). High.

---

## 4. Footer Total

The old RDLCs render a footer in one of three shapes. Match the shape the specific RDLC uses — do
**not** blanket-apply per-currency sums where the old footer left Total Value blank.

### 4a. Per-currency + grand TOTAL (listing reports) — `currencyTotalsColumns` + backend `currencyTotals`

Old RDLC (Amend / ActualAmend / Cancel / New / Extension) has a Currency-grouped footer:
`<CUR>: CountDistinct(LicenceNo) licence(s)` + `<CUR>: FORMAT(Sum(Amount),"N4")`, then a grand
`TOTAL` + `"Total:" CountDistinct(LicenceNo) licence(s)`. Add to the config block:
`currencyTotalsColumns: { labelColumnKey: 'LicenceNo', valueColumnKey: 'TotalValue' }` (the established
sibling shape — `ImportLicenceParity_FixesApplied.md`; e.g. reportConfigs.ts 1628). **Backend must
emit `data.currencyTotals`** — `BasicTable.tsx` renders the per-currency footer only when the API
returns it (lines 419–423); `currencyTotalsColumns` only controls placement. For the list procs that
lack a currency-totals method (e.g. `sp_NewReport` has no `ExecuteCurrencyTotalsAsync`), add one
mirroring `sp_ExtensionReport.ExecuteCurrencyTotalsAsync` and have the controller populate
`result.CurrencyTotals` (copy the sibling Extension controller).

Reports: ExportPermitActualAmendmentReport, ExportPermitAmendmentReport, ExportPermitCancellationReport,
ExportPermitNewReportNewReport, BorderExportPermitActualAmendmentReport, BorderExportPermitAmendmentReport,
BorderExportPermitCancellationReport, **BorderExportPermitNewReportNewReport** (needs backend method).

### 4b. Grand No-of-Licences TOTAL only (By* summary reports) — `includeColumnTotals` (Total Value blank)

Old RDLC (BySection / BySellerCountry / CompanyList / ByHSCode) footer = `TOTAL` label +
`CountDistinct(LicenceNo)` under No-of-Licences; **Total Value / Currency cells are intentionally
BLANK**. So use the **`columnTotals`** mechanism (backend `includeColumnTotals: true` → `BasicTable`
renders the Total row, BasicTable.tsx 403–413) and **do NOT** add `currencyTotalsColumns` summing
Total Value.

> **Backend plumbing caveat** (confirmed): the Export-Permit aggregate path
> `sp_ExportPermitDetailReport_Fast.CreateAggregateResultAsync` does **not** currently accept
> `includeColumnTotals` (unlike the Import/Export-Licence/Import-Permit `_Fast` procs). Add a
> `bool includeColumnTotals = false` param and forward it into
> `ReportAggregationService.CreatePagedResult` / `CreatePagedResultFromGroups`, then pass
> `includeColumnTotals: true` from the controllers. `BuildColumnTotals` sums **both** `noOfLicences`
> and `totalValue` — the old footer leaves Total Value blank, so this slightly over-totals vs strict
> RDLC parity but matches the established new-app convention (`ImportLicenceBySellerCountryReport` ships
> this way). Flag for the team.

Reports: ExportPermitByHSCodeReport, ExportPermitBySectionReport, ExportPermitBySellerCountryReport,
ExportPermitCompanyListReport, BorderExportPermitByHSCodeReport, BorderExportPermitBySectionReport
(*verify product intent — the sibling `ImportPermitBySectionReport` intentionally ships **no** footer*),
BorderExportPermitBySellerCountryReport, BorderExportPermitCompanyListReport.

### 4c. Daily — grand No-of-Licences + USD `columnTotals` (medium/high)

`ExportPermitDailyReportNewPermitReport` and `BorderExportPermitDailyReportNewPermitReport`: old RDLC
footer = `TOTAL` + `CountDistinct(LicenceNo)` under No-of-Licences + `FORMAT(Sum(totalUSDAmount),"N4")`
under Total USD Value; the Total Value cell stays blank. Backend returns `columnTotals { noOfLicences,
totalUSDValue }` (omit `totalValue`). **See §6 — the USD column itself is currently null (FX gap).**

### 4d. Voucher — single grand `SUM(Amount)` (high)

`ExportPermitVoucherReport` (Amount col 9633–9638) and `BorderExportPermitVoucherReport` (Total Amount):
old `VoucherReport.rdlc` footer is a single `=FORMAT(SUM(Amount),"N0")` (not per-currency). Prefer the
`columnTotals`/`showTotalRow` path keyed on the amount `dataIndex`; backend must emit the totals
payload (config alone won't render it).

---

## 5. Nav-Title (`reportSubtitle`)

Old controllers set `header1` (the bold centered in-grid title rendered by the RDLC `=Parameters!header1.Value`
textbox); the new config blocks have no `reportSubtitle`, so no in-grid title renders. Add
`reportSubtitle: importLicenceRangeSubtitle('<label>', <includeFrom>)` (helper at reportConfigs.ts
370–378 → `${label} From (${from}) To (${to})` when `includeFrom=true`, else `${label} (${from}) To (${to})`).
Match the **exact old `header1` string** — including its quirks (e.g. the Amend reports literally say
"Export Licence" even though they are Export **Permit** amend reports).

| Report | Old header1 text | includeFrom |
|---|---|---|
| ExportPermitActualAmendmentReport | `List of Export Licence Report` (controller 6340) | true |
| ExportPermitAmendmentReport | `List of Export Licence Report` (6239) | true |
| ExportPermitByHSCodeReport | `List of Export Permit By HS Code` (6832) | true |
| ExportPermitBySectionReport | `List of Export Permit By Section` (6040) | true |
| ExportPermitBySellerCountryReport | `List of Export Permit By Buyer Country` (6634) | true |
| ExportPermitCancellationReport | `List of Export Permit Report` (6530) | false |
| ExportPermitCompanyListReport | `List of Export Permit By Company` (6739) | true |
| ExportPermitDailyReportNewPermitReport | `List of Export Permit By Daily` (6140) | true |
| ExportPermitDetailReport | `List of Export Permit By Detail` (5878) | true |
| ExportPermitNewReportNewReport | `List of Export Permit Report` (7063) | false |
| ExportPermitVoucherReport | `Export Permit Voucher List` (6941) | false |
| BorderExportPermitActualAmendmentReport | `List of Border Export Permit Report` (12297) | false |
| BorderExportPermitAmendmentReport | `List of Border Export Permit Report` (12190) | false |
| BorderExportPermitByHSCodeReport | `List of Border Export Permit By HS Code` (12812) | true |
| BorderExportPermitBySectionReport | `List of Border Export Permit By Section` (11981) | true |
| BorderExportPermitBySellerCountryReport | `List of Export Permit By Buyer Country` (12605) | true |
| BorderExportPermitCancellationReport | `List of Border Export Permit Report` (12498) | false |
| BorderExportPermitCompanyListReport | `List of Border Export Permit By Company` (12714) | true* |
| BorderExportPermitDailyReportNewPermitReport | `List of Border Export Permit By Daily` (12086) | true |
| BorderExportPermitDetailReport | `List of Border Export Permit By Detail` (11817) | true |
| BorderExportPermitNewReportNewReport | `List of Border Export Permit Report` (13052) | false |
| BorderExportPermitVoucherReport | `Border Export Permit Voucher List` (12925) | false |

> *Verify the helper's bracket style against the exact old `(date) To (date)` literal where
> `includeFrom` is ambiguous. The two HSCode reports (`ExportPermitDetailReport`,
> `ExportPermitExtensionReport`) — no nav-title finding was raised for `ExportPermitExtensionReport`
> (only 2 findings: stray Sakhan + section leak); confirm against its old `header1` if adding.

---

## 6. Drilldown (table-link navigation)

Old RDLCs put an `ActionInfo`/`Hyperlink` on a dimension cell that opens the Detail report carrying
the row's id. Add `drilldown: { targetReportKey, carryFilters, rowParams }` on that column.

| Report | Cell | Target | carryFilters | rowParams | Notes |
|---|---|---|---|---|---|
| ExportPermitBySectionReport | Section (8717–8721) | `ExportPermitDetailReport` (exists, 9094) | FromDate, ToDate, PaThaKaTypeId | `{ ExportImportSectionId: 'exportImportSectionId' }` | backend row payload must expose numeric `exportImportSectionId` (RDLC drills on `Fields!ExportImportSectionId.Value`, not `sectionName`) |
| ExportPermitBySellerCountryReport | Country (8796–8800) | `ExportPermitDetailReport` | FromDate, ToDate, PaThaKaTypeId, ExportImportSectionId | `{ BuyerCountryId: 'countryId' }` | **backend:** `AggregateSourceRowsAsync` (`sp_ExportPermitDetailReport_Fast.cs:194-209`) must map `CountryId = row.BuyerCountryId` (currently null); dataIndex is `countryId` (not `buyerCountryId`) |
| ExportPermitCompanyListReport | CompanyName (8986–8990) | `ExportPermitDetailReport` | FromDate, ToDate, Type, PaThaKaTypeId, ExportImportSectionId | `{ CompanyRegistrationNo: 'companyRegistrationNo' }` | mirror `ImportPermitCompanyListReport` (11444–11448); ensure carryFilters reference real source filters |
| BorderExportPermitBySectionReport | Section (2431–2435) | `BorderExportPermitDetailReport` (exists, 2813) | FromDate, ToDate, Type, PaThaKaTypeId | `{ ExportImportSectionId: 'sectionId' }` | mirror `ImportPermitBySectionReport` 11186–11190; do **not** carry SakhanId |
| BorderExportPermitBySellerCountryReport | Country (2510–2514) | `BorderExportPermitDetailReport` | FromDate, ToDate, PaThaKaTypeId, ExportImportSectionId, SakhanId | `{ BuyerCountryId: 'countryId' }` | aggregate exposes `CountryId`→`countryId`, not `buyerCountryId` |
| BorderExportPermitByHSCodeReport | HS Code (2342–2346) | **Border HS Code detail** (must be added) | FromDate, ToDate, FormType, FilterType, SakhanId | `{ hsCode: 'hsCode' }` | old url carries **no** section; if no Border HSCode detail report key exists yet, add it first |
| BorderExportPermitCompanyListReport | CompanyName (2705–2709) | `BorderExportPermitDetailReport` | FromDate, ToDate, PaThaKaTypeId, ExportImportSectionId, SakhanId | `{ CompanyRegistrationNo: 'companyRegistrationNo' }` | old url carries pathakatype/section/sakhan/companyregistrationno — **drop BuyerCountryId** |

> **Blocked / prerequisite:** `ExportPermitByHSCodeReport` (and the Border twin) need an HS Code
> **detail** report key in the new app first (`HSCodeDetailReport` / `BorderHSCodeDetailReport` do not
> exist in `reportConfigs.ts`). Lower priority than the rest until that detail report is built. The
> Amend/Cancel/Voucher/Detail/New RDLCs have **zero** Drillthrough — no drill-down parity gap there.

---

## 7. Files that will change

**Frontend**
- `Frontend/src/Report/config/reportConfigs.ts` — Section `lookupName` pins (24), Sakhan removal (12
  non-border), stray-filter removals (`Auto`, `BuyerCountryId`), FilterType→select (2 HSCode), Licence
  `dataIndex` fixes (7), stray-column removals + title casing, Voucher `resolveColumns` + key rename,
  `reportSubtitle` additions (22), `currencyTotalsColumns` (listing/voucher), drilldown configs (7).
  *(Vite HMR — refresh, no backend restart for FE-only edits.)*

**Backend**
- `Backend/Controllers/ReportLookupsController.cs` — `const ExportPermitFormType = "Export Permit"`;
  new `GetExportPermitSections()` + `GetBorderExportPermitSections()`; 2 switch keys. **Restart backend
  (lookups cached ~1 day).**
- `Backend/StoredProcedureToLinq/sp_ExportPermitDetailReport_Fast.cs` — add `includeColumnTotals` param
  forwarded to `CreatePagedResult`/`CreatePagedResultFromGroups`; map `CountryId = row.BuyerCountryId`
  in `AggregateSourceRowsAsync` (drilldown).
- `Backend/StoredProcedureToLinq/sp_NewReport.cs` — add `ExecuteCurrencyTotalsAsync` (for
  BorderExportPermitNewReport currency footer).
- Controllers — `includeColumnTotals: true` on the By* summary controllers (BySection,
  BySellerCountry, CompanyList, Daily — both families); populate `result.CurrencyTotals` on the listing
  controllers (Amend/ActualAmend/Cancel/New/Extension/Voucher).
- After backend edits: `dotnet build Backend/API.csproj`; type-check FE.

---

## 8. Deferred / needs decision

1. **Total USD Value FX gap (Daily, both families).** Old Daily footer totals
   `Sum(totalUSDAmount)`, but the new backend leaves `TotalUSDValue = null`, so the USD column + its
   total render blank — same gap as Import Licence / Border Export Licence Daily. The CBM FX formula
   (per-currency, `KRW/JPY` divided by 100) is recovered in memory `daily-report-usd-value-fx-conversion`
   (`ReportUsdConversionService`). Decision: wire the FX conversion or leave the USD column blank.
2. **`includeColumnTotals` over-totals Total Value** on By* reports (BasicTable/`BuildColumnTotals`
   sums both `noOfLicences` and `totalValue`; old RDLC leaves Total Value blank). Accept the convention
   or implement a count-only footer mode.
3. **BorderExportPermitBySectionReport footer** — sibling `ImportPermitBySectionReport` intentionally
   ships **no** footer. Confirm product intent before adding.
4. **Voucher extra columns** (`Application Date`, `Commodity Type` on Border Voucher) — present in the
   ImportLicenceVoucher new config; confirm-before-drop.
5. **HS Code drill-downs** blocked on building the (Border) HS Code **detail** report keys first.
6. **Border `Type`/`FormType` visible filter** — handle across the whole Border family in one pass
   (hide/derive), not per report; confirm backend defaulting.
7. **Voucher PaymentType** (Border) — hardcoded static options vs old DB-driven `paymentTypes` lookup;
   casing diverges from the Licence sibling (`'Citizen Pay'` vs `'CitizenPay'`). Switch to
   `lookupName:'paymentTypes'` or reconcile the literal token. Low.
8. **`BorderExportPermitCompanyListReport` extra `BuyerCountryId` filter** + dropped old readonly
   CompanyName — reconcile against the new SP before treating as parity-correct.

---

## 9. Per-report appendix

Legend: ✓ = at parity / no fix; ✗ = fix needed; — = N/A (axis not applicable to this report).

### Non-border (Oversea)

| Report | Filters | Columns | Footer | Nav | Drilldown |
|---|---|---|---|---|---|
| ExportPermitActualAmendmentReport | ✗ Sakhan + section | ✗ dup Licence, hsCode | ✗ per-cur + grand | ✗ subtitle | — |
| ExportPermitAmendmentReport | ✗ Sakhan + section | ✗ dup Licence, hsCode | ✗ per-cur + grand | ✗ subtitle | — |
| ExportPermitByHSCodeReport | ✗ missing section, Sakhan, FilterType | ✗ extra CompanyName | ✗ grand count (TV blank) | ✗ subtitle | ✗ HS Code (blocked) |
| ExportPermitBySectionReport | ✗ Sakhan + section | ✓ | ✗ grand count | ✗ subtitle | ✗ Section→Detail |
| ExportPermitBySellerCountryReport | ✗ Sakhan + section | ✓ | ✗ grand count | ✗ subtitle | ✗ Country→Detail |
| ExportPermitCancellationReport | ✗ Sakhan, missing section, CompanyName | ✗ hsCode, dup Cancel | ✗ per-cur + grand | ✗ subtitle | — |
| ExportPermitCompanyListReport | ✗ Sakhan + BuyerCountry + section | ✓ | ✗ grand count | ✗ subtitle | ✗ Company→Detail |
| ExportPermitDailyReportNewPermitReport | ✗ Sakhan + BuyerCountry + section + CompanyName | ✓ | ✗ grand + USD | ✗ subtitle | — |
| ExportPermitDetailReport | ✗ Sakhan + section | ✓ | — | ✗ subtitle | — |
| ExportPermitExtensionReport | ✗ Sakhan + section | ✓ | — | — | — |
| ExportPermitNewReportNewReport | ✗ Sakhan + Auto + section | ✗ 4 stray cols | ✗ per-cur + grand | ✗ subtitle | — |
| ExportPermitVoucherReport | ✗ Sakhan + section(scope/label) | ✓ (default New) | ✗ grand SUM(Amount) | ✗ subtitle | — |

### Border

| Report | Filters | Columns | Footer | Nav | Drilldown |
|---|---|---|---|---|---|
| BorderExportPermitActualAmendmentReport | ✗ section | ✗ dup Licence, hsCode title | ✗ per-cur + grand (backend) | ✗ subtitle | — |
| BorderExportPermitAmendmentReport | ✗ section, CompanyName | ✗ dup Licence, hsCode | ✗ per-cur + grand (backend) | ✗ subtitle | — |
| BorderExportPermitByHSCodeReport | ✗ missing section, FilterType | ✓ (CompanyName kept) | ✗ grand count (TV blank) | ✗ subtitle | ✗ HS Code (add detail key) |
| BorderExportPermitBySectionReport | ✗ section, Type(family) | ✓ | ✗ grand count (verify intent) | ✗ subtitle | ✗ Section→Detail |
| BorderExportPermitBySellerCountryReport | ✗ section, Type(family) | ✓ | ✗ grand count (backend flag) | ✗ subtitle | ✗ Country→Detail |
| BorderExportPermitCancellationReport | ✗ section, FormType | ✗ hsCode, dup Cancel | ✗ per-cur + grand | ✗ subtitle | — |
| BorderExportPermitCompanyListReport | ✗ section, Type(family) | ✓ | ✗ grand count | ✗ subtitle | ✗ Company→Detail |
| BorderExportPermitDailyReportNewPermitReport | ✗ section, Type(family), CompanyName | ✓ | ✗ grand + USD | ✗ subtitle | — |
| BorderExportPermitDetailReport | ✗ section, Type | ✗ hsCode title casing | — | ✗ subtitle | — |
| BorderExportPermitExtensionReport | ✗ section | ✗ dup Licence (FE-only) | — | — | — |
| BorderExportPermitNewReportNewReport | ✗ section, Auto | ✗ Auto col | ✗ per-cur + grand (backend method) | ✗ subtitle | — |
| BorderExportPermitVoucherReport | ✗ section(scope), PaymentType | ✗ header2/3 literals, extra cols | ✗ grand SUM(Amount) | ✗ subtitle | — |

> Section-leak fix applies to **all 24** (✗ Filters everywhere). Sakhan removal applies to the 12
> non-border only; Border reports keep Sakhan (correct). Conventions per `CLAUDE.md` and
> `ImportLicenceParity_FixesApplied.md` (labels vs `Resources.resx`, `reportSubtitle` from old
> `header1`, `currencyTotalsColumns`/`includeColumnTotals` totals, drilldown→Detail).

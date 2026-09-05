# Import Permit parity — round 1 (2026-09-05)

Six customer complaints on the non-Border Import Permit reports. Ground truth throughout is
the old RDLC XML under
`tradenet-2.0-admin/TradenetAdmin/ReportControl/` and the old views/controllers — **not**
`docs/ReportColumnComparison.md`, which is wrong in places.

## What the old RDLCs actually do

| Old RDLC | Row group | Footer row |
|---|---|---|
| `VoucherReport.rdlc` | none | `TOTAL` (ColSpan 11) + `=FORMAT(SUM(Fields!Amount.Value),"N0")` under **Total Amount** (rdlc:1709/1828). The file's **only** aggregate. |
| `ImportPermitBySectionReport.rdlc` | SectionName + Currency (:1068-69) | `TOTAL` (:841) + `=CountDistinct(Fields!LicenceNo.Value)` (:895); Total Value and Currency cells `<Value />` blank |
| `ImportPermitBySellerCountryReport.rdlc` | SellerCountry + Currency (:1066-67) | same shape (:839 / :893) |
| `ImportPermitByCompanyReport.rdlc` | **CompanyRegistrationNo** + Currency (:1066-67), displays CompanyName | same shape (:839 / :893) |
| `ImportPermitByDailyReport.rdlc` | sLicenceDate + Currency | `TOTAL` + CountDistinct + **blank Total Value** + literal `Total USD Value` (:1145) + `Sum(totalUSDAmount)` (:1200) |
| `HSCodeReport.rdlc` | **HSCodeId + Currency — no company** (:1152-53) | `TOTAL` (:870) + CountDistinct (:978); Total Value blank |
| `HSCodeDetailReport.rdlc` | HSCodeId + CompanyRegistrationNo, no currency (:1263-64) | `TOTAL` + CountDistinct (:1184) |
| `CancelReport.rdlc` | Tablix1 ungrouped; Tablix2 groups on Currency (:1837) | per currency `"<CUR>:N licence(s)"` (:1557) and `"<CUR>:FORMAT(Sum(Amount),'N4')"` (:1611), then `TOTAL` (:1669) + `"Total:N licence(s)"` (:1723), grand money cell blank (:1778) |

**No By-X RDLC in any family ever sums Total Value.** The old date window is inclusive
`>= FromDate 00:00:00 … <= ToDate 23:59:59` (`Business/Reports.cs:938-948`), not `DATEADD`.

## Fixes

1. **Voucher — missing Application Date.** Commit `36eaa18` (2026-06-01) deleted both the
   `ApplicationDate` column and the second, original-licence-no column from this config.
   Restored, along with `resolveColumns: resolveImportLicenceVoucherColumns` so the
   `header2` / `header3` titles follow ApplyType (`ReportsController.cs:8239-8261`) and the
   duplicate Licence No column hides for `ApplyType='New'` (rdlc:1883). Backend already
   returned `applicationDate`. The column set now matches its two already-corrected
   siblings, `ExportPermitVoucherReport` and `ImportLicenceVoucherReport`, exactly.
2. **Voucher — wrong footer.** `currencyTotalsColumns` **and** the controller's
   `ImportPermitListingCurrencyTotals.ExecuteVoucherAsync` call both had to go: BasicTable
   falls back to the first non-numeric + first numeric column when the config key is absent,
   which for this config is `LicenceNo` / `LicValue` — i.e. the rejected footer would have
   rendered anyway. Replaced with `ColumnTotals["amount"] = Round(total, 0)` from
   `sp_VoucherReport.ExecuteAmountTotalAsync`, matching rdlc:1828. Commit `6d75eed` did this
   for seven sibling voucher reports and skipped this one.
3. **By Section / By Seller Country / Company List / Daily — no Total Value in the footer.**
   Reused `ReportColumnTotalsMode.CountOnly`, the mechanism the Export Permit round-3 work
   added, on the four Import Permit and four Border Import Permit controllers.
   `BuildColumnTotals` was restructured so `CountOnly` keeps the Daily `totalUSDValue`
   roll-up, which the Daily RDLC does print (:1200) — no existing caller is affected.
4. **Footer licence count.** The RDLC footer is a dataset-scoped `CountDistinct(LicenceNo)`;
   the code summed the per-`(group, currency)` counts, double-counting a permit whose items
   span two currencies. `sp_ImportPermitDetailReport_Fast.CreateAggregateResultAsync` now
   overrides `noOfLicences` from the already-materialised source rows — no extra query, and
   scoped to the Import Permit family because that shim serves only those controllers.
5. **Cancellation.** Dropped the leading `hsCode` column (Tablix1 has 11 columns, none
   HS-related); un-swapped `Licence No` ↔ `Cancellation No` (rdlc:366/:986 vs :421/:1039 —
   every sibling already binds it the RDLC's way); restored the per-currency footer via a
   new `Cancel` branch in `sp_ImportPermitListingCurrencyTotals`.
6. **By HS Code — duplicate rows.** Both data paths grouped on the buyer company;
   `HSCodeReport.rdlc` groups on `HSCodeId + Currency` only. Fixed in
   `sp_HSCodeReport_pagination` (the `@FormType='Import Permit'` branch only) **and** in
   `sp_HSCodeReport.AggregateQuery`, its LINQ twin — the Excel streaming path
   (`WriteRowsAsync` → `GetAggregateRowsAsync`) never touches the procedure, so fixing one
   and not the other would make grid and `.xlsx` disagree. The other seven form types keep
   the company key: their `*HSCodeDetailReport` configs render Company Name off it.

## Parity tail (same six reports)

* `rowNumberTitle` added to `ReportPageConfig`: the By-X RDLCs head the row-number column
  `Sr.No.`, the listing RDLCs `No.`; `resolveRowNumberTitle` now prefers it. Both the grid
  and the Excel spec read that one value.
* `numberFormat` is now applied by the grid, not only exported: `toTableColumn` renders a
  numeric column with the column's own `FORMAT(..., "Nx")`. The N4 money columns use
  `dataType: 'money'` + `#,##0.0000`, which is the `Money4` cell format in the `.xlsx`, so
  the two surfaces print the same string.
* Footer label `Total` → `TOTAL` (`BasicTable.tsx`, `ExcelFooterBuilder.cs`), matching every
  RDLC grand row. `AccountSummaryReport`'s own `TotalsRowLabel` is a per-report layout
  setting and stays `Total`.
* Filter boxes: the free-text `Type` / `FormType` boxes are gone — the old views render them
  as `@Html.HiddenFor` and every controller hard-codes the value; the readonly Company Name
  field is back where the old view has one; `PaThaKaTypeId` / `SellerCountryId` /
  `ExportImportSectionId` carry explicit `lookupName`s; the By HS Code report regains its
  Import Section filter.
* Drill-downs open in a new tab, as the old `window.open(..., '_blank')` hyperlinks did.

## Deliberately not changed

* **`Curency`** — `CancelReport.rdlc:710` misspells the header. Not reproducing a typo;
  raise it with the customer.
* **`reportHeading`** — the Ministry/Directorate lines are a new-system convention, not in
  these RDLCs, and are pinned by `reportConfigs.importPermit.test.ts`.
* **Role-based section scoping** — the old app narrows both the rows and the section
  dropdown to the signed-in user's sections for `CheckUser` / `ApproveUser`
  (e.g. `ReportsController.cs:8020`). The new backend has no equivalent, so for those user
  types old and new still differ. The one data divergence this round does not close.
* **`ImportLicenceVoucherReport`** still emits both `ColumnTotals["amount"]` and a
  per-currency footer. That footer was added deliberately on request — but it is the same
  shape the customer has now rejected on Import Permit, so it is worth re-confirming.
* **Import Permit HS Code drill-through.** The old report drills into `HSCodeDetailReport`
  (HS Code / Description / Company Name / No of Licences, grouped HSCodeId +
  CompanyRegistrationNo, no currency). The drill still re-filters the same report by HS code
  instead. Building the real detail report needs a grouping mode on
  `sp_HSCodeReport_pagination`; shipping it against the current company-free Import Permit
  branch would render a blank Company Name column.
* **The other 23 By-X controllers** outside the Import Permit family carry the identical
  mixed-currency Total Value footer, and every one of their RDLCs leaves that cell blank.
  Scoped out of this round on purpose.

## Verification

* `cd Frontend && npx vitest run` — 1491 pass. The 8 failures in
  `reportConfigs.borderExportPermit` / `borderImportLicence` / `exportLicence` are
  **pre-existing**: they fail identically on a clean `HEAD` worktree.
* `dotnet test Backend.Tests/Backend.Tests.csproj --filter "FullyQualifiedName!~LiveDb&FullyQualifiedName!~SeededDatabase&FullyQualifiedName!~SystemTests&FullyQualifiedName!~SmokeTests&FullyQualifiedName!~EndpointTests"`
  — 1637 pass, the same 10 pre-existing failures as `HEAD`.
* Excel fixtures and `docs/ExcelParity/manifest.json` are generated:
  `npm run fixtures:excel` and `npx vite-node scripts/excelParityManifest.ts`. The
  `formatVersion` inside `Backend.Tests/Fixtures/ExcelSpecs/*.json` is the spec **schema**
  version and must stay 1; the per-report `[ExcelFormatVersion]` is the controller attribute.
* **Not yet run against a database.** The two procedures need hand-deploying
  (`StoredProcedureMigrations/Deployments/2026-09-05_ImportPermitParityRound1/`), and its
  `VerifyDeployment.sql` holds the old-vs-new checks. Use **2025** date ranges — 2026 is
  mostly empty. Read that folder's README before comparing: if
  `docs/sp_HSCodeReport_AggregatePagination.sql` was ever run, the legacy
  `dbo.sp_HSCodeReport` carries the same bad `GROUP BY` and the "old shows one row"
  baseline is contaminated.

# Import Licence drill-down parity — 2026-09-17

Branch `fix/import-licence-drill-detail-parity`, off `main` @ `ea0409f`. Commit `edd8385`.

Customer complaint (Burmese):

> import licence မှာ import licence company list report မှာ ကျလာတဲ့ company ကို click
> လိုက်ရင် ကျလာတဲ့ detail licence မှာ column တွေ ကျန်နေလို့ပါ အရင်အဟောင်းနဲ့ လုံးဝမတူဘဲ
> column တိုင်တွေကျန်ခဲ့တာပါ
>
> (link တွေကို click လိုက်ရင် new tab နဲ့သွားပေးပါရန်)

Two asks: the drill-down detail report is **missing columns** and is nothing like the old one;
and **links should open in a new tab**. Both are regressions against Tradenet 2.0.

| # | Complaint | Root cause | Status |
| --- | --- | --- | --- |
| 1 | Company click → detail has columns missing | Drill re-pointed at a 9-column config with no legacy counterpart | Fixed |
| 2 | Links should open in a new tab | `openInNewTab` missing on 6 of 8 Import Licence / Border Import Licence drills | Fixed |

---

## 1. The drill target was an invented report

In Tradenet 2.0 the Company Name cell opened the **full per-item Import Licence Detail Report**:

- `ImportLicenceByCompanyReport.rdlc:592` —
  `="javascript:void(window.open('"&Parameters!url.Value+"&header=company&filter="+Fields!CompanyRegistrationNo.Value&"', '_blank'))"`
- `ReportsController.cs:5915` (origin/master) builds that url as
  `Reports/ImportLicenceDetailReport?fdate=…&tdate=…&pathakatype=…&section=…&method=…&companyregistrationno=…`
- `ReportsController.cs:4682-4718` receives it: on `header=company` it hides the filter box and
  sets `CompanyRegistrationNo = QueryString["filter"]`.
- `ImportLicenceDetailReport.rdlc` has **26 columns** — `grep -c '<TablixColumn>'` = 26, headers at
  `rdlc:365,423,479,534,589,644,699,754,823,892,961,1030,1085,1140,1195,1250,1305,1360,1415,1470,1525,1580,1635,1690,1745,1800`.

The new app instead drilled into `ImportLicenceDetailByLicenceReport` — **9 columns**, no entry in
any parity doc, introduced so the drill's record count would equal the clicked "No of Licences"
(its own comment said so). That count-parity was an internal design choice; no doc records it as a
customer requirement, and it is **not** what the old system did.

**The same defect affected all four** By-X summaries — the old controller builds the same
`Reports/ImportLicenceDetailReport` url at `:5101` (By Section), `:5207` (By Method),
`:5806` (By Seller Country), `:5915` (Company List).

### Fix

Re-pointed all four drills at `ImportLicenceDetailReport`. The `Currency` rowParam was dropped:
`ImportLicenceDetailReportRequest` has no `Currency` property, and the legacy url passed none.

`carryFilters` are unchanged. Note the old app put `&method=` in the url but **never read it back**
on the company path (`ReportsController.cs:4708` consumes `filter` only when `header == "method"`),
so its drill silently dropped the Method filter. That is a legacy bug; carrying it keeps the detail
consistent with the summary the user is looking at, so it was kept.

**Frontend only.** `ImportLicenceDetailReport` already renders all 26 legacy columns and already
honours the filter params. `Company Address` is composed client-side from the six address parts via
`fallbackDataIndexes` (`GenericReportPage.tsx:413-429`). Drill params reach the API even with no
matching filter box — `setFilters({ ...derivedFilterValues, ...drill })` at
`GenericReportPage.tsx:1059` — which is how `CompanyRegistrationNo` survives, exactly as the old
app's hidden-filter drill did.

`ImportLicenceDetailByLicenceReport` is retained (hidden, route-reachable). Deleting it would churn
`reportRoutes.tsx` and the `Backend.Tests` ExcelSpecs fixtures for no user-visible gain; its comment
now records that it is no longer a drill target.

## 2. The new-tab flag

Every old By-X rdlc wraps the cell in `window.open(..., '_blank')` — verified on all four Import
Licence and all four Border Import Licence rdlcs. Only `ImportLicenceBySectionReport` and
`BorderImportLicenceBySectionReport` set `openInNewTab`. Added to the other six.

`BasicTable`'s drill cell is an `<a>` with no `href` (`BasicTable.tsx:480-512`), so Cmd-click
cannot substitute — `openInNewTab` is the only mechanism.

Border Import Licence already targeted the correct full detail report, so those three are a
flag-only change.

---

## Production verification

Measured on `reportapi.myanmartradenet.com` with a hand-minted JWT, `2025-01-01 … 2025-03-31`,
using the exact request the re-pointed drills send (carryFilters + `SortColumn: PaThaKaTypeId`).
Unfiltered `ImportLicenceDetailReport` totalCount for the window: **269,643**.

| Drill | Param | totalCount | grouping column over sampled rows |
| --- | --- | --- | --- |
| Company List | `CompanyRegistrationNo=103687241` | 139 | homogeneous — ` 24 HOUR MINING & INDUSTRY COMPANY LIMITED ` |
| By Section | `ExportImportSectionId=2` | 12,299 | homogeneous — `1` |
| By Method | `ExportImportMethodId=33` | 2,050 | homogeneous — `Capital In Kind` |
| By Seller Country | `SellerCountryId=1` | 27 | homogeneous — `AFGHANISTAN` |

Every row carries **43 fields**, backing all 26 legacy columns with no gaps — checked explicitly for
`applicationDate`, `applicationNo`, `companyAddress` parts, `sellerName`, `sellerAddress`,
`portofDischarge`, `lastDate`, `consignedCountry`, `countryofOrigin`, `hsCode`, `hsDescription`,
`unit`, `price`, `quantity`, `amount`, `currency`, `commodityType`, `conditions`.

Sample row: `OVSIL22425057811 | 8474900000 | 2500.0 KG | 8585.25 USD | BRAND NEW CEMENTS MACHINERY ITEMS`.

### Expected and correct: the row count changes

The drill now shows **item** rows (139 for that company), not the clicked "No of Licences" (4).
That is exactly what Tradenet 2.0 did — the summary counts licences, the detail lists HS lines.
Worth stating in the reply to the customer so it is not filed as a new defect.

## Test state

- `src/Report`: **1720 passed / 6 failed**, against a **1718 / 6** baseline taken at `ea0409f` in a
  detached worktree — the same 6 pre-existing failures in
  `reportConfigs.{borderExportPermit,borderImportLicence,exportLicence}.test.ts` (action-report
  subtitle wording, voucher `PaymentType` lookup). +2 new tests, no new failures.
- New: `reportConfigs.importLicence.test.ts` asserts all four drills target
  `ImportLicenceDetailReport` with `openInNewTab` and the right rowParams, and that the target still
  carries all 26 rdlc columns. `reportConfigs.borderImportLicence.test.ts`'s existing
  `summary reports link` test gained the three `openInNewTab` expectations.
- `ExcelSpecContractTests`: **1286 passed / 0 failed**. `drilldown` is explicitly excluded from the
  Excel spec (`buildExcelPresentation.ts:43`), so no fixture regeneration and **no
  `[ExcelFormatVersion]` bump** is needed.
- `npm run build` passes (pre-existing chunk-size warning only).
- The DB-bound suites were not run, per the owner's standing decision — `TradeNetDBTest` is not
  reachable from this machine.

## Deployment

**No stored procedure change.** Frontend only; merging to `main` is sufficient for the auto-deploy
watcher to ship it.

## Out of scope — flagged, not changed

- `ImportLicenceByHSCodeReport` drills to **itself** and has no new-tab flag. Tradenet 2.0 has no
  `ImportLicenceByHSCode` rdlc (the old Import Licence family has only 7), so there is no legacy
  behaviour to restore — needs a separate decision.
- On any drill target, pressing **Search** again rebuilds filters from the visible form and loses
  `CompanyRegistrationNo` (the Detail report has no company box). Pre-existing and app-wide; the old
  app sidestepped it by hiding the filter box entirely.
- The old drill-down title rendered as `List of Import Licences By  From (…)` — `model.CompanyName`
  was never populated on that GET path (`ReportsController.cs:4815`). Not reproducing that bug.

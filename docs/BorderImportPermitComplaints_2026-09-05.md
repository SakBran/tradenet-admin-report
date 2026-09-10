# Border Import Permit — customer complaints, 2026-09-05

Source: `Border Import Permit To Fix List.md` (repo root), six reports.

The list asked to **re-test with 2025 data and report back** before fixing anything. The live
production API (`reportapi.myanmartradenet.com`) answers a minted JWT from this machine, so every
complaint was measured end-to-end against the real deployment rather than reasoned about. `pymssql`
cannot log in to `tn2db` from the Mac — TCP opens, the TDS/TLS handshake stalls — so the API, not
direct SQL, is the harness here.

**A 2025 window reproduces the customer's numbers exactly.** All figures below are
`2025-01-01 → 2025-12-31`, all-Sakhan vs Sakhan = TCL (`Sakhan.Id` 4).

| Report | all Sakhan | TCL | customer said | verdict |
|---|---|---|---|---|
| Detail | 70 rows | 20 rows | 70 all / old TCL 20 | matches the old report exactly |
| By Section | 3 rows, 18 licences | 3 rows, 4 licences | 18 all / old TCL 4 | matches exactly |
| Company List | 13 rows, 18 licences | 3 rows, 4 licences | old 13, UI 10, Excel 13 | data right, grid wrong |
| By HS Code | 31 rows, 18 licences | 4 rows, 4 licences | UI 29, Excel 31 | grid loses 2 rows |
| Voucher (ApplyType New) | 28 rows | 8 rows | 28 all / old TCL 8 | matches exactly |
| New Report | 18 permits | 4 permits | — | no TOTAL footer at all |

## "Sakhan ဖြင့်ရှာရင် data မထွက်" — not a filter bug

The Sakhan chain is correct at every hop (`Sakhan.Id` from the lookup → `SakhanId: int` on the
request → the same `CASE WHEN @SakhanId=0` predicate the legacy procedures use), and TCL returns
precisely the numbers the customer quotes from the old report: Detail 20, By Section 4, Voucher 8.

The bug was in the grid. `BasicTable` kept `pageIndex` in state and only the pager ever wrote it;
applying filters merely bumped `refreshKey`, and the table is never remounted. So browsing Detail
all-Sakhan (70 rows = 7 pages), clicking to page 3, then picking a Sakhan (20 rows = 2 pages)
re-requested **page 3 of a 2-page result** and rendered nothing. Voucher: 28 rows → 8. This affected
every report in the application, not this family.

Fixed in `Frontend/src/components/My Components/Table/BasicTable.tsx` by adjusting `pageIndex`
during render when `refreshKey` changes (an effect would fire one wasted request for the stale page
first). Guarded by `Frontend/src/components/BasicTablePaging.test.ts`.

## "Old report shows 997 licences / 856 rows" — a Tradenet 2.0 bug

`ReportsController.cs:15465` sets `model.FormType = AppConfig.ImportPermit` — `"Import Permit"`, not
`"Border Import Permit"` — so the old Border Import Permit By HS Code screen queries the **oversea**
`ImportPermit` / `ImportPermitItem` tables and ignores `@SakhanId` entirely. Its Sakhan dropdown is
decorative. 997/856 are oversea numbers; 18 licences is the correct border figure, and it agrees
with By Section and Company List. The Border Export Permit twin has the same defect
(`ReportsController.cs:12754`); the two Border *Licence* HS Code reports are correct.

Decision taken with the owner on the first pass: keep the new report border-only, fix the real
paging bug, and tell the customer why the old number was larger. **Reversed the same day** — see
"Round 2" below.

## Fixes

| Fix | What | Where |
|---|---|---|
| A | grid returns to page 1 when filters change | `BasicTable.tsx` |
| B | New Report per-currency TOTAL block (`BorderNewReport.rdlc` Tablix2), in the grid and the .xlsx | `sp_ImportPermitListingCurrencyTotals.sql` new Border `New` branch + `BorderImportPermitNewReportNewReportController` + `[ExcelFormatVersion(2)]` |
| C | By HS Code lost one row per page boundary (10+10+9 = 29 of 31) | `@FetchSize` in `sp_HSCodeReport_pagination.sql`; `sp_HSCodeReport.cs` stops inflating `@PageSize` |
| D | summary reports print on one page like the RDLC | `defaultPageSize: 1000` on By HS Code, HS Code Detail, By Section, By Seller Country, Company List |
| E | New Report admitted one extra day | `sp_NewReport_pagination.sql` Border branches → `DATEADD(day, 1, CONVERT(date, @ToDate))` |
| F | By HS Code split rows by company; the RDLC groups on (HSCodeId, Currency) | proc `@HSCode=''` branch + `sp_HSCodeReport.GroupsByCompany` + config column |
| G | Sakhan dropdown hid inactive stations the old one showed; Voucher footer LINQ lacked the `TransactionFormType` discriminator | `ReportLookupsController.cs`, `sp_VoucherReport.cs` |

### C — the deploy-order trap

`sp_HSCodeReport.cs` used to send `pageSize + 1` as `@PageSize` so `CreateFastPageFromRows` could
tell whether a next page exists without a `COUNT(*)`. The procedure derives its `OFFSET` from
`@PageSize` too, so page 2 of a 10-row page started at row 12. Measured before the fix: pages of
10 / 10 / 9 over a 31-row set.

The sentinel now widens `FETCH NEXT` only (`@FetchSize`). The C# side was also made independent
of it: seven of the eight FormType branches compute `COUNT(*) OVER()` whether or not the caller
asks, so `sp_HSCodeReport.cs` now builds an exact page from that count — correct against an old or
new procedure, and the pager gains a reachable last page. Only Export Licence's count-less fast
page still needs `@FetchSize` deployed. The procedure serves all eight families, so re-check paging
on every HS Code report (`VerifyDeployment.sql` §5).

### E — measured, not inferred

`@ToDate = 2025-01-12 23:59:59` returned 2 permits, both dated 2025-01-13, while By Section (LINQ,
`<= ToDate`) returned 0 for the same window. Legacy `sp_NewReport` used `CreatedDate <= @ToDate`.
Border Export Permit carried the identical defect and was flipped in the same pass, together with
its footer in `sp_ExportPermitListingCurrencyTotals.sql` (which held a `TODO` to stay in step).

### F — 31 rows becomes 16

`BorderHSCodeReport.rdlc` groups on `HSCodeId` + `Currency` (rdlc:1157-1169) and renders no company
column. Grouping by company as well split one HS code into a row per buyer, each with a partial
Total Value. The `Start`/`End` sub-branches keep the company: they also serve
`BorderImportPermitHSCodeDetailReport`, whose `HSCodeDetailReport.rdlc` does render Company Name.
The same fix was applied to the plain `Import Permit` branch in round 1; Border was missed.

## Not done, deliberately

* **LEFT JOIN on `Sakhan`.** Considered as hardening, then rejected: every legacy procedure uses
  `INNER JOIN Sakhan` (`docs/StoredProcedureDefinitions.sql:5763`, `:6782`, `:8957`), so a LEFT JOIN
  would show rows the old report never showed. Count orphan/NULL `BorderImportPermit.SakhanId` rows
  before revisiting.
* **Voucher default `ApplyType`.** Already `'New'` in the config; the procedure's
  `ApplyType = @ApplyType` is a strict equality, so a blank value legitimately returns nothing.
* Reproducing the old 997/856 figures — see above.

## Deployment

`StoredProcedureMigrations/Deployments/2026-09-05_BorderImportPermitComplaints/` — four procedures,
`CaptureRollback.sql` first, then `00_RunAll.sql`, then `VerifyDeployment.sql`, then the application.
The copies in `2026-09-04_AmendActualAmendParity`, `2026-09-05_ImportPermitParityRound1` and
`2026-09-06_ExportPermitItemOrder` were re-synced because they ship the same procedures.

## Test baseline

Unchanged from `9fb4b55`: backend non-DB suites 11 failed / 1662 passed; `Frontend` 8 failed / 1508
passed. The Excel spec fixtures were regenerated (`npm run fixtures:excel`), which also picked up two
`ExportLicence*` fixtures left stale by the previous round.

## Round 2 (2026-09-05, later) — By HS Code must print the OLD report's result

The owner came back with the customer's verdict: the report must show the **same result as the old
one**, 997 / 856 included. Re-measured first: the other five reports already equal the old figures
(Detail 70 / TCL 20, By Section 18 / 4, Company List 13, Voucher 28 / 8, New Report 18 + TOTAL);
By HS Code was the only one apart (16 rows / 18 licences vs 856 / 997). The owner confirmed the
target is the old behaviour, bug for bug.

**What changed — C# and config only, no SQL:**

* `BorderImportPermitByHSCodeReportController` now sends `FormType = "Import Permit"`, exactly what
  legacy `ReportsController.cs:15465` does. The summary therefore runs `sp_HSCodeReport_pagination`'s
  Import Permit branch (oversea `ImportPermit` tables, `LicenceDate` window, Sakhan ignored, grouped
  on HSCodeId + Currency — already the round-1 grouping) and the footer is `CountDistinct(LicenceNo)`
  over the same oversea rows. The Sakhan and Import Section dropdowns stay as the dead controls the
  old form had. `[ExcelFormatVersion(2)]` because the row set changed for an unchanged payload.
* The HS Code **detail drill** (`BorderImportPermitHSCodeDetailReport`, same controller) posts a
  pinned `GroupBy: 'Company'` — a new `constantValue` filter property resolved by
  `getDerivedFilterValues`, so it is never rendered, survives drill-in, and reaches the Excel spec.
  The backend maps it to `sp_HSCodeReportRequest.GroupByCompany`, which forces the LINQ path and a
  grouping on **(HSCodeId, CompanyRegistrationNo)** — `HSCodeDetailReport.rdlc`'s key (rdlc:1263-1264),
  no Currency, no Total Value. Without the flag the summary would company-split the moment a user
  typed an HS-code prefix (the round-1 symptom), and the proc cannot tell the two apart.
* Company List: `ReportAggregationService.BuildKey` keys the Company dimension on the registration
  number + currency, as all eight legacy `*ByCompanyReport.rdlc` do; the name is displayed, not
  keyed. Same rows today (13), one fewer difference in general.
* `ReportControllerBranchDefaultsTests` gained a `LegacyFormTypeOverrides` entry; new
  `BorderImportPermitByHSCodeLegacyParityTests` pins the routing, the drill flag and both grouping
  keys via `ToQueryString()`.

**What this does and does not prove.** The 997 / 856 figures are not reproducible on the database
reachable from here — the oversea query gives 538 rows / 604 licences over 2025 and 2,447 / 2,763
over 2020-2026 — so the customer's old system reads a database with different oversea content.
After this change the new report is the old query by construction; the harness check is
`BorderImportPermitByHSCodeReport ≡ ImportPermitByHSCodeReport` (rows, totalCount, footer) for the
same window, plus `SakhanId 4` ≡ `SakhanId 0`. The final old-vs-new comparison has to be run on the
customer's environment. Residual, non-data differences left alone: legacy row order is `ORDER BY
HSCode.Id` first-appearance vs the new `HSCode, Currency`; the new proc trims `@HSCode`.

Border **Export** Permit By HS Code has the identical legacy bug (`ReportsController.cs:14120`) but is
NOT a one-line switch — the proc's Export Permit `@HSCode=''` sub-branch still groups by company —
so it was left for a follow-up.


## Round 3 (2026-09-06) — Detail Report must be byte-identical to the old one

Owner's instruction: "For Border Import Permit Detail Report make, even it is wrong in old code. I
want to get byte identical result as the old report." Measured first on production over
`2025-01-01 → 2025-12-31`: 70 rows / 18 permits (all Sakhan), the customer's figure — so the row
**set** already matched; what did not match was everything around it.

**Old report, as built.** Legacy `ReportsController.cs:14455-14620` runs
`dbo.sp_ImportPermitDetailReport @Type='Border'` with `FromDate " 00:00:00"` / `ToDate " 23:59:59"`
and renders `BorderImportPermitDetailReport.rdlc`: `header1` = `List of Border Import Permit By
Detail From (dd/MM/yyyy) To (dd/MM/yyyy)`, row label `Sr.No.`, 23 columns, no group, no sort, no
footer. `Permit Date` / `Last Date` are the model's `.ToString("dd/MM/yyyy")` strings, `Price` and
`Value` are `FORMAT(...,"N4")`, `Qty` is `"N2"`, `Company Address` is `CommonRepository.GetAddress`
(`"State,"` with no space when there is no postal code; a trailing `", "` when the country is blank).
The filter form is From Date, To Date, Sakhan, EIR Card Type, Import Section.

**Differences found in the new report, all fixed:**

| Old | New (before) | Fix |
|---|---|---|
| rows from `sp_ImportPermitDetailReport 'Border'`, incl. `fn_GetNRCNo` and the FOR XML CSV expanders | LINQ twin (NRC composed in C#, CSV names from a cached lookup) | **new `sp_BorderImportPermitDetailReport_pagination`** — the legacy Border query verbatim, key-paged; grid AND Excel use it; 2812 fallback to the LINQ twin |
| deterministic-looking order (plan order: permits as created, items in line order) | `OFFSET/FETCH` over an unordered join (a row could show on two pages or none) | proc and LINQ both order by `CreatedDate, permit Id, ItemNo, item UniqueId` |
| `Company Address` = `GetAddress` string | grid re-joined six columns with `", "` in a different order | API now sends `companyAddress` = `LegacyCompanyAddress.Compose` (both paths) |
| `13/01/2025` | `2025-01-13` | new column option `dateFormat: 'DD/MM/YYYY'` (`formatDateCell`); Excel already prints `dd/mm/yyyy` |
| `4.0000`, `200.00`, `800.0000` | `4`, `200`, `800` | `dataType: 'money'` + `numberFormat` `#,##0.0000` / `#,##0.00` / `#,##0.0000` (grid and sheet) |
| header `HSCode`, row label `Sr.No.`, `header1` line | `hsCode`, `No.`, no header line | title, `rowNumberTitle`, `reportSubtitle` |
| every row on one page | 10 per page | `defaultPageSize: 1000` |
| filter box From/To/Sakhan/EIR Card Type/Import Section (dropdowns) | plus Seller Country + Company Registration No boxes; Sakhan and card type were bare number inputs | box = the old five, with `sakhans` / `paThaKaTypes` / `borderImportPermitSections` lookups; the two removed filters stay on the DTO for drill-downs |

`[ExcelFormatVersion(2)]` on the controller (row text changed for an unchanged payload) and
`IExcelNoFooterReport` (the RDLC has no total row).

**Deployment:** `StoredProcedureMigrations/Deployments/2026-09-06_BorderImportPermitDetailLegacyParity/`
— one NEW procedure, nothing altered. `VerifyDeployment.sql` section 3 is the proof: the legacy
procedure and the new one, same window, every row, `EXCEPT` both ways over all 38 legacy columns
must be empty. Until it is applied the app falls back to the LINQ twin, which now differs from the
old report only in how the NRC string is composed (the function body is not in any repository
reachable from here).

**Deliberately reproduced, not fixed:** `CreatedDate <= @ToDate` (the listing reports use the
calendar-day window; the Detail legacy does not), the CSV expander's exact-token `LIKE` match, the
unqualified `ApplyType='New'`, the `Decription` / `Country of Orign` header typos, `GetAddress`'s
punctuation. **Not reproduced:** the legacy C# wraps the whole mapping in `try/catch` and would show
an EMPTY report if any row had a NULL `LastDate`/`LicenceDate`/`Price` — measured on production,
no Border Import Permit row in 2020-2026 (2,515 rows) has one, so this cannot change the result;
and the legacy role filter for CheckUser/ApproveUser accounts, which the new admin does not model.
Row order is the one thing that cannot be proven from code: the legacy query has no `ORDER BY`.

Tests: `Backend.Tests/BorderImportPermitDetailLegacyParityTests.cs` (procedure text verbatim,
wrapper parameters, `GetAddress` cases, UI columns/filters/formats vs the RDLC),
`reportConfigs.borderImportPermit.test.ts` (Detail block), `reportPresentation.test.ts`
(`formatDateCell`). The Excel spec fixture for the report was regenerated.

## Round 3b (2026-09-06) — By Section Report must be byte-identical to the old one

Same instruction for `BorderImportPermitBySectionReport`. Measured first on production over 2025:
3 rows (section "4" × CNY / THB / USD: 9 / 3 / 6 licences), footer 18 — the numbers were already
right; the shape was not.

**Old report, as built.** Legacy `ReportsController.cs:14622-14724` fetches the SAME
`sp_ImportPermitDetailReport 'Border'` rows as the Detail report and renders
`BorderImportPermitBySectionReport.rdlc`: `header1` = `List of Border Import Permit By Section
From (…) To (…)`; one row per **(SectionName, Currency)** group (rdlc:1080-1081) with **no
SortExpressions**, so groups print in the order their first row arrives; `Sr.No.` is a `Code`
group counter; `No of Licences` = `CountDistinct(LicenceNo)`; `Total Value` =
`FORMAT(Sum(Amount),"N4")`; the `TOTAL` footer (under Section, right-aligned) carries only the
whole-dataset `CountDistinct(LicenceNo)`; the Section cell is a `window.open(…,'_blank')`
hyperlink into the Detail report with `header=section&filter=<ExportImportSectionId>` plus the
search's dates, card type and Sakhan. Filter form = From, To, Sakhan, EIR Card Type, Import Section.

**Differences found, all fixed:**

| Old | New (before) | Fix |
|---|---|---|
| groups in first-appearance order of the detail rows | alphabetical by section then currency | new `ReportAggregateOrdering.SourceOrder` (`ReportAggregationService`), fed from the ordered detail rows (`OrderedRows`); grid and Excel both use it, the controller no longer re-sorts the sheet |
| `2,994,220.0000` | `2994220` | `dataType: 'money'`, `numberFormat: '#,##0.0000'` (grid and sheet) |
| `Sr.No.`, `header1` line | `No.`, no header | `rowNumberTitle`, `reportSubtitle` |
| Section → Detail drill in a new window | no drill | `drilldown` to `BorderImportPermitDetailReport` carrying FromDate / ToDate / PaThaKaTypeId / SakhanId + the row's `sectionId`, `openInNewTab` |
| filter box From/To/Sakhan/EIR Card Type/Import Section (dropdowns) | plus Seller Country + Company Registration No boxes; Sakhan and card type bare inputs | the old five with lookups; the two removed stay on the DTO for drill-downs |

Already identical: grouping on the section **name** (not id) + currency, the distinct count per
group, the count-only TOTAL footer, one page. `[ExcelFormatVersion(3)]` because the sheet's row
order and the Total Value cell format changed for an unchanged payload.

Not reproduced: the legacy Detail drill's header reads `List of Border Import Permit By
<SectionName> …` where `model.SectionName` is never assigned — the new Detail keeps its own
subtitle. Row order rests on the same assumption as the Detail report (the legacy query has no
`ORDER BY`; first appearance is taken over permits in creation order, items in line order).
Only `ReportAggregateOrdering.Canonical` (the previous behaviour) is used by every other
aggregate report; nothing else changed for them.

Tests: `Backend.Tests/BorderImportPermitBySectionLegacyParityTests.cs`,
`ReportAggregationServiceTests.SourceOrder_keeps_groups_in_first_appearance_order`, and the
By Section block in `reportConfigs.borderImportPermit.test.ts`. Excel spec fixture regenerated.

## Round 3c (2026-09-06) — Company List Report must be byte-identical to the old one

Same instruction for `BorderImportPermitCompanyListReport`. Measured first on production over 2025:
13 rows / footer 18 — the customer's figure; the shape was not the old report's.

**Old report, as built.** Legacy `ReportsController.cs:15347-15430` fetches the SAME
`sp_ImportPermitDetailReport 'Border'` rows and renders `BorderImportPermitByCompanyReport.rdlc`:
one row per **(CompanyRegistrationNo, Currency)** group (rdlc:1078-1079), **no SortExpressions**
(first-appearance order), `Company Name` = the group's first row (`Fields!CompanyName.Value`),
`Sr.No.` group counter, `No of Licences` = `CountDistinct(LicenceNo)`, `Total Value` =
`FORMAT(Sum(Amount),"N4")`, count-only `TOTAL` footer, Company Name hyperlink → Detail in a new
window (`header=company&filter=<CompanyRegistrationNo>` + dates, card type, section, Sakhan).
**Its header is wrong**: `ReportsController.cs:15425` builds `"List of Import Permit By Company
(" + FromDate + ") To (" + ToDate + ")"` — no "Border", no "From" — on this Border screen. Filter
form = From, To, Sakhan, EIR Card Type, Import Section, Company Registration No, Company Name (readonly).

**Differences found, all fixed:**

| Old | New (before) | Fix |
|---|---|---|
| groups in first-appearance order; name = first row's | alphabetical by name; name = max of the group | `ReportAggregateOrdering.SourceOrder` on grid and Excel; `Aggregate` shows `group.First().CompanyName` under SourceOrder (Canonical keeps the max) |
| `36,000.0000` | `36000` | `money` + `#,##0.0000` |
| `Sr.No.`, header `List of Import Permit By Company (…) To (…)` | `No.`, no header | `rowNumberTitle`; `reportSubtitle` = the legacy wording **verbatim, wrong "Import Permit" included** |
| Company Name → Detail drill in a new window | no drill | `drilldown` → `BorderImportPermitDetailReport` carrying FromDate / ToDate / PaThaKaTypeId / ExportImportSectionId / SakhanId + the row's `companyRegistrationNo`, `openInNewTab` |
| filter box with Sakhan / card type dropdowns and a readonly Company Name | bare number inputs, an extra Seller Country box, no Company Name | the old seven (`importLicenceCompanyNameFilter` for the readonly, auto-filled name); Seller Country stays on the DTO |

Already identical: grouping on the registration number + currency, the distinct count per row,
the count-only footer, one page. `[ExcelFormatVersion(3)]`.

Not reproduced: the legacy Detail drill header (`By <CompanyName>`, never filled in by the old
code). Row order rests on the same no-`ORDER BY` assumption as the other two reports.

Tests: `Backend.Tests/BorderImportPermitCompanyListLegacyParityTests.cs`,
`ReportAggregationServiceTests.SourceOrder_company_rows_show_the_first_name_in_row_order`, the
Company List block in `reportConfigs.borderImportPermit.test.ts`. Excel spec fixture regenerated.

---

**Superseded 2026-09-10 — By HS Code now reads the Border tables.** The bug-for-bug decision recorded
above left a Sakhan dropdown that could not work. The customer asked for it to work and said the old
report is the thing that is wrong, so the report was switched off the oversea query: 1,014 rows /
1,328 permits -> 31 / 112 on their window. See `docs/BorderPermitByHSCodeSakhanSwitch_2026-09-10.md`.

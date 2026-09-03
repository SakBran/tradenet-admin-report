# Excel Export Parity — Contract

Single source of truth for every agent working on the Excel-export parity change. Plan of record:
`~/.claude/plans/excel-report-function-must-excel-wondrous-firefly.md` (approved 2026-09-02). Manifest:
`docs/ExcelParity/manifest.json` (regenerate: `node tools/build-excel-parity-manifest.mjs`). Prompts: `docs/ExcelParity/Prompts/`.

## 1. Purpose and the 5 rules

Every report Excel export (160 controllers implementing `IStreamingExcelReport`; 167 frontend configs) must satisfy:

| # | Rule | Verified by (`rulesVerified` flag) |
|---|---|---|
| M1 | Title row; a date row; **From Date / To Date** rows when the report has a date range | `title`, `date`, `fromTo` |
| M2 | Only the columns the UI grid shows | `columnsExact` |
| M3 | Every UI column present | `columnsExact` |
| M4 | Never a column the UI does not show | `columnsExact` |
| M5 | Footer totals when the UI shows them (Total row; per-currency rows + grand TOTAL) | `footer` |

Flag definitions are in `Prompts/_preamble.md` (same text for every agent).

Today (baseline): `StreamingExcelWriter` infers columns by reflection over the first appended row → C# property names as
headers, every public property as a column, no title/date rows, no footer. The only per-report opt-out is the peer's
typed `IExcelReportLayoutProvider` (used by `AccountSummaryReportController`, commit `7e02910`).

## 2. Target sheet shape

```
A1  <reportHeading lines…>                    merged across all columns, bold 14, centered   (only if configured)
    <config.title>                             same style; OMITTED when another header line already contains it (Ordinal)
    <reportSubtitle(filters)>                  same style; the legacy RDLC header1 line, e.g. "Account Summary Report (01/06/2026) To (03/06/2026)"
    From Date: 01/06/2026                      bold 11, left, unmerged  — backend, from the request DTO's FromDate/ToDate
    To Date: 30/06/2026                        (single `Date` prop → one row "Date: dd/MM/yyyy"; no date props → no date rows)
    Exported: 02/09/2026 19:40                 bold 11, left, unmerged  — backend, generation time (server local), InvariantCulture
    No | <UI column titles, UI order…>         bold header row; "No" only when showRowNumber (label = rowNumberTitle); freeze pane here
    1  | …data rows…                            streamed in chunks; sheet rollover at 1,048,576 rows repeats the whole block above; "No" continues across sheets
    Total | …                                  columnTotals footer (see §5)
    USD:3 licence(s) | … | USD:1,234.0000      currencyTotals rows (see §5)
    TOTAL | Total:7 licence(s)                 grand row (see §5); footer only on the last sheet
```

### Header-block algorithm (who emits what)

- **Frontend** `buildReportHeaderLines(config, applied)` (shared by the grid and the spec) =
  `[...(config.reportHeading ?? []), config.reportSubtitle ? config.reportSubtitle(applied) : config.title]` with blank/whitespace lines removed.
  It does NOT emit From/To rows (the grid never had them; the backend derives them from the request).
- **Backend** `ExcelLayoutBuilder.WithStandardHeaderBlock(layout, spec, fallbackTitle, request, exportedAt)` applied to EVERY layout (typed or generic):
  1. `title = SanitizeTitle(spec?.Title) ?? fallbackTitle` — add a Title line unless any existing `TitleLines`/header line **contains** it (Ordinal).
  2. add each `spec.HeaderLines` entry not already present as a Heading line (Title/Heading lines are merged+centered).
  3. `ExcelRequestDates.Describe(request)`: `FromDate`+`ToDate` → Meta lines `From Date: dd/MM/yyyy`, `To Date: dd/MM/yyyy`; single `Date` → `Date: dd/MM/yyyy`; none → nothing.
  4. Meta line `Exported: dd/MM/yyyy HH:mm` (`TimeProvider.GetLocalNow()`).
  Dates always `CultureInfo.InvariantCulture` (in a .NET custom format `/` is a culture placeholder).
- Title/Heading lines: `mergeCells` A{r}:{last}{r}; Meta lines: single cell A{r}, no merge. Header row index = preamble length + 1.
- Freeze pane: `<sheetViews><sheetView workbookViewId="0"><pane ySplit="{headerRow}" topLeftCell="A{headerRow+1}" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>` written BEFORE `<cols>` (CT_Worksheet order: sheetViews, cols, sheetData, mergeCells).

## 3. Canonical spec (frontend ⇄ backend)

TypeScript (`Frontend/src/Report/excel/excelTypes.ts`) ⇄ C# (`Backend/Model/ExcelExport/ExcelPresentationSpec.cs`,
classes `ExcelPresentationSpec`, `ExcelSpecColumn`, `ExcelCurrencyTotalsPlacement`, `ExcelSpecSection`, `ExcelSpecSummaryLine`).
JSON = System.Text.Json Web defaults (camelCase, case-insensitive read). The frontend builds the object from ONE
literal so key order is stable → the dedup hash (`ExcelExportHasher`, spec is inside the hashed request JSON, NOT in `IgnoredFields`) is deterministic.

```ts
interface ExcelPresentationSpec {
  formatVersion: number;          // EXCEL_PRESENTATION_FORMAT_VERSION = 1
  configKey: string;              // reportConfigs key (≠ controllerName for the 4 *HSCodeDetailReport aliases)
  controllerName: string;         // == backend ReportKey (class name minus "Controller"); mismatch → 400
  title: string;                  // config.title → job.ReportTitle (Exports drive) + worksheet name + title line
  fileName: string;               // config.excelFileName → job.FileName base (sanitized) + "_yyyyMMdd_HHmmss.xlsx"
  headerLines: string[];          // from buildReportHeaderLines; backend appends From/To|Date + Exported
  showRowNumber: boolean;
  rowNumberTitle: string;         // 'No' | 'No.' (legacyReportViewer = controllerName starts with ImportLicence or BorderImportPermit) | bespoke labels ('Sr.No.', 'စဥ်')
  columns: { key: string; dataIndex: string; title: string; dataType?: 'string'|'number'|'date'|'dateTime'|'boolean'|'money'; fallbackDataIndexes?: string[]; numberFormat?: string }[];
  currencyTotalsColumns?: { labelColumnKey: string; valueColumnKey: string };   // column KEYS (not dataIndexes)
  sections?: { key: string; title: string; dataPath: string; showRowNumber: boolean; rowNumberTitle: string; columns: /*same as above*/ [] }[];  // composites only
  summaryLines?: { label: string; dataPath: string; numberFormat?: string }[];   // e.g. { label: 'Total USD Value', dataPath: 'totalUsdValue', numberFormat: '#,##0.0000' }
}
```
Validation (`ExcelPresentationSpecValidator.ValidateAndSanitize(spec, requireColumns)`): title ≤ 200 (stored ≤ 256, `ReportTitle nvarchar(256)`),
fileName ≤ 120 after stripping `.xlsx` and any char outside `[A-Za-z0-9 _.-]`, ≤ 12 headerLines × 300 chars, ≤ 100 columns,
column title ≤ 200, `key`/`dataIndex`/fallbacks match `^[A-Za-z0-9_.]+$` (≤ 100 chars, ≤ 10 fallbacks), `dataType` in the whitelist,
≤ 5 sections, control characters stripped, whitespace collapsed. `columns` may be empty only when `sections` is non-empty or the
controller is a typed layout provider. `drilldown` and `hidden` columns never reach the spec (drilldown columns export the underlying value).

## 4. Cell rules — `ExcelLayoutBuilder.Build(spec, rowType)` must mirror the UI

UI sources: `Frontend/src/Report/Page/GenericReportPage.tsx` (`toTableColumn` ~360-418; `formatTransactionAmount`/`toTransactionAmountNumber` ~152-170;
`getMpuAmount`/`getAmountDiff` ~363-381; `hasValue`) and `Frontend/src/components/My Components/Table/BasicTable.tsx` (cell path ~381-413: `row[dataIndex ?? key]`, `render ?? value?.toString() ?? 'N/A'`).

| UI | Excel |
|---|---|
| property lookup by `dataIndex` (camelCase JSON key) | `ExcelRowPropertyMap.For(rowType)`: `JsonNamingPolicy.CamelCase.ConvertName(prop.Name)` → compiled accessor; exact match, then OrdinalIgnoreCase. `NRCNo→nrcNo`, `TotalUSDValue→totalUSDValue`, `HSCode→hsCode`, `Id→id`. No `[JsonPropertyName]` exists in Backend. |
| `fallbackDataIndexes`: primary if `hasValue`, else non-blank fallbacks joined with `", "` | identical; `hasValue(v)` = `v != null && v.ToString().Trim() != ""` |
| `dataType 'date'` → `dayjs(v).format('YYYY-MM-DD')` | real date serial, style `dd/mm/yyyy` (`ExcelCellFormat.Date`); unparsable → text |
| `'dateTime'` → `'YYYY-MM-DD HH:mm:ss'` | date serial, style `yyyy-mm-dd hh:mm:ss` (`ExcelCellFormat.DateTime`) |
| `'boolean'` → `Yes`/`No` | text `Yes`/`No` (bool or "true"/"false" strings); else raw text |
| `'money'` → `Number(strip commas).toFixed(2)` | numeric, style `#,##0.00` (`Money`); `numberFormat: '#,##0.0000'` → `Money4` |
| `'number'` → raw | numeric (`Number`) when numeric CLR value or parsable; else raw text |
| `dataIndex === 'transactionAmount' && money` | integer string = minor units (`/100`), string with `.` parsed; commas stripped |
| `dataIndex === 'mpuAmount'` | value if `hasValue` else `transactionAmount − mocAmount − imAmount` (siblings via the map, missing → 0); Money |
| `dataIndex === 'amountDiff'` | value if `hasValue` else `transactionAmount − mocAmount`; Money |
| blank → `'N/A'` | **text columns**: `N/A` (constant `NullText`); **numeric/date columns**: EMPTY cell — deliberate deviation so SUM/sort work |
| `No` column = `index + 1 + pageIndex*pageSize` | `ExcelColumn.RowNumber(rowNumberTitle)` when `showRowNumber`: 1..N across chunks and sheets (per-section restart for composites) |
| unknown `dataIndex` (no fallback resolves) | blank column with the UI title + `ILogger` warning (intentionally unbound columns exist, e.g. AccountSummary "Remark"; the contract test checks them against `allowlist.json`) |

Widths heuristic: date 12, dateTime 20, money 16, number 12, text `clamp(title.Length + 4, 12, 40)`. Every generic column is
`.Bind(key, dataIndex)` so the footer builder can place totals. In generic mode the handler asserts the first appended row is
assignable to the resolved row type (clear message otherwise).

Row type resolution (`ExcelRowTypeResolver.Resolve(controllerType)`): `T` from the bare `[HttpPost] Task<ActionResult<ApiResult<T>>> Post(...)`
(156 controllers), else `IExcelRowTypeProvider.ExcelRowType`, else null. Never a recording sink: empty exports never call `Append<T>` and the header must be written before the first row.

## 5. Footer rules — `ExcelFooterBuilder.Build(layout, totals, dataRowCount)` mirrors `BasicTable.tsx`

Source of truth: `BasicTable.tsx` columnTotals ~421-434 + 640-676; currencyTotals ~436-459 + 678-746. `[]` when `dataRowCount == 0` or `totals == null`.

1. **Total row** (when `ColumnTotals` has a key matching a data column's `dataIndex`): label `"Total"` in the FIRST data column
   that has NO total (row-number cell blank); each totalled column gets a numeric cell (bold; Money → `#,##0.00`, Number → general, else text).
2. **Per-currency rows** (when `CurrencyTotals.Currencies` non-empty), one per entry: under `labelColumnKey` the text
   `"{Currency}:{NoOfLicences} licence(s)"`, under `valueColumnKey` the text `"{Currency}:{TotalValue:N4 en-US}"`; other cells blank.
   Keys = `layout.CurrencyTotalsColumns` (from the spec) or the fallback: first non-numeric data column key / first numeric data column key. Key comparison is Ordinal.
3. **Grand row**: `"TOTAL"` in the row-number cell (if no row-number column, in data column 0 unless that is the label column);
   `"Total:{GrandTotalLicences} licence(s)"` under `labelColumnKey`.
Order: Total row, currency rows, grand row. Footer rows are bold, not counted in `TotalDataRows`, written via `StreamingExcelWriter.AppendFooterRows`
(which then skips the peer's summed `WriteTotalsRow`).

Source of the values — `IExcelFooterTotalsResolver`:
- `IExcelFooterTotalsProvider.GetExcelFooterTotalsAsync(object request, ct)` on the controller (explicit override; `[NonAction]`), else
- `DefaultExcelFooterTotalsResolver`: JSON-clone the request (Web options), set `PageIndex = 0, PageSize = 1, IncludeTotalCount = true, Excel = null`,
  find the bare `[HttpPost]` `Post` whose single parameter accepts the request type (`ExcelRowTypeResolver.FindBarePost`), invoke it on the
  `ActivatorUtilities`-created controller (no `HttpContext`; verified no `Post` touches `HttpContext`/`User`), await, unwrap `ActionResult<T>`
  via `IConvertToActionResult.Convert()` → `ObjectResult` (status null or < 400) whose `Value is IReportTotals` → `new ReportFooterTotals(ColumnTotals, CurrencyTotals)`;
  `Post` returning a non-`IReportTotals` type → null (no footer); `BadRequest`/exception → throw.
- `ExcelExportOptions.FooterTotals`: `Required` (default — a failed probe fails the job, retried per `MaxAttempts`) | `BestEffort` (log + omit footer).
- This is exactly the grid's lazy exact-count request (`BasicTable` `includeTotalCount: true`, `lazyColumnTotals`/`lazyCurrencyTotals` take precedence).
- `ApiResult<T>` implements the new `IReportTotals { ColumnTotals; CurrencyTotals }` (`Backend/Model/APIResult.cs` members already exist ~399-409).

## 6. Layout precedence, rejection, cache

`ControllerStreamingExcelReportJobHandler.GenerateAsync`:
1. deserialize `RequestJson` into `ExcelRequestType`; `spec = (request as ReportQueryRequest)?.Excel`.
2. layout = controller is `IExcelReportLayoutProvider` → `GetExcelLayout(request)` (typed; AccountSummary, CompanyProfile, the 4 TotalValueLicences)
   else `spec != null` → `ExcelLayoutBuilder.Build(spec, RowType)` else **throw** `"Export '<key>' was queued without an Excel presentation spec. Refresh the page and export again."` — the reflection dump is never produced.
3. `layout = WithStandardHeaderBlock(...)`; `totals = await resolver.ResolveAsync(...)` BEFORE streaming (fail fast, same snapshot).
4. `StreamingExcelWriter(context.Output, report.ExcelWorksheetTitle, layout)`; `WriteRowsAsync`; `AppendFooterRows(ExcelFooterBuilder.Build(...))`; `Finish()`.

`RequireExcelPresentationSpecFilter` (`IAsyncActionFilter`, registered globally via `services.Configure<MvcOptions>(o => o.Filters.Add<…>())` inside
`AddExcelExportQueue` — `Program.cs` untouched): for action `Excel` on an `IStreamingExcelReport` controller with a bound `ReportQueryRequest` argument:
`Excel == null && !typedLayout` → 400 `"Excel presentation spec missing — refresh the page and try again."`; `Excel != null` → `ValidateAndSanitize`
(400 with `errors[]` on failure); maps `ExcelPresentationSpecException` (thrown defensively by `EnqueueAsync`) to 400. Grid `Post` requests are untouched.

Enqueue (`ExcelExportJobService.EnqueueAsync`): `ReportTitle = SanitizeTitle(spec?.Title) ?? handler.DefaultTitle`;
`FileName = $"{SanitizeFileNameBase(spec?.FileName) ?? handler.FileNameBase}_{now:yyyyMMdd_HHmmss}.xlsx"`. Hash unchanged in code — the spec is part of `requestJson`.
Version: `RegisterReportHandlers` uses `formatVersion = (attr?.Version ?? 1) + ExcelExportFormat.Generation` (Generation = 1) → every handler ≥ 2, AccountSummary 3;
all cached title-less files are invalidated on deploy. Bump `Generation` whenever the shared sheet shape changes again.

## 7. Backend files (Phase 0, SHARED — only core/repair/applier agents edit these)

1. `Backend/Model/ExcelExport/ExcelPresentationSpec.cs` (new) — DTOs of §3.
2. `Backend/Model/ExcelExport/ExcelPresentationSpecValidator.cs` (new) — `ValidateAndSanitize`, `SanitizeTitle`, `SanitizeFileNameBase`, `ExcelPresentationSpecException`.
3. `Backend/Model/ReportQueryRequest.cs` — `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ExcelPresentationSpec? Excel { get; set; }`.
4. `Backend/Model/IReportTotals.cs` (new); `Backend/Model/APIResult.cs` — `public class ApiResult<T> : IReportTotals`.
5. `Backend/Service/ExcelExport/ExcelReportLayout.cs` — extend: `ExcelCellFormat.DateTime = 5`, `.Money4 = 6`; `ExcelColumn.Key/DataIndex/IsRowNumber/IsNumeric`, `.Bind(key, dataIndex)`, `Untyped(...)`, `Money4<T>(...)`; `ExcelHeaderLine(Text, Kind: Title|Heading|Meta)`; `ExcelReportSection { Title, Columns }`; `ExcelReportLayout.HeaderBlock`, `.CurrencyTotalsColumns`, `.Sections`, `.FreezeHeader = true`, `.With(...)`; `ExcelFooterRow { Cells: ExcelFooterCell?[] }`, `ExcelFooterCell(Value, Format)`. Existing members/defaults unchanged.
6. `Backend/Service/ExcelExport/IStreamingExcelReport.cs` — `IExcelRowSink.BeginSection(int) {}` and `.AppendNote(string) {}` (default interface members); `IExcelFooterTotalsProvider`; `IExcelRowTypeProvider { Type ExcelRowType }`; `record ReportFooterTotals(IReadOnlyDictionary<string,decimal>? ColumnTotals, ReportCurrencyTotalsSummary? CurrencyTotals)`; `static class ExcelExportFormat { const int Generation = 1; }`; `StreamingExcelWriterSink` forwards the new members.
7. `Backend/Service/ExcelExport/ExcelRowPropertyMap.cs` (new), `ExcelRowTypeResolver.cs` (new), `ExcelRequestDates.cs` (new).
8. `Backend/Service/ExcelExport/ExcelLayoutBuilder.cs` (new) — `Build`, `WithStandardHeaderBlock`, `NullText`.
9. `Backend/Service/ExcelExport/ExcelFooterBuilder.cs` (new).
10. `Backend/Service/ExcelExport/ExcelFooterTotalsResolver.cs` (new) — `IExcelFooterTotalsResolver`, `DefaultExcelFooterTotalsResolver`; `ExcelExportOptions.FooterTotals` (`FooterTotalsPolicy`).
11. `Backend/Service/ExcelExport/StreamingExcelWriter.cs` — preamble (TitleLines + HeaderBlock), freeze pane, `AppendFooterRows`, `BeginSection`, `AppendNote`, styles (numFmt 166 `yyyy-mm-dd hh:mm:ss`, 167 `#,##0.0000`; cellXfs 7 meta, 8 total-number, 9 datetime, 10 money4). Byte-identical output when the new members are empty.
12. `Backend/Service/ExcelExport/ControllerStreamingExcelReportJobHandler.cs` — §6 flow; lazy `RowType`; expose `ControllerType`/`HasTypedLayout`.
13. `Backend/Service/ExcelExport/RequireExcelPresentationSpecFilter.cs` (new); `ExcelExportJobService.cs`; `ExcelExportOptions.cs`; `ExcelExportServiceCollectionExtensions.cs` (resolver scoped, `TryAddSingleton(TimeProvider.System)`, filter, `+ Generation`).
14. Tests: `Backend.Tests/ExcelPresentationSpecValidatorTests.cs`, `ExcelRowPropertyMapTests.cs`, `ExcelLayoutBuilderTests.cs`, `ExcelFooterBuilderTests.cs`, `DefaultExcelFooterTotalsResolverTests.cs`, `ExcelRowTypeResolverTests.cs`, `RequireExcelPresentationSpecFilterTests.cs`, `ExcelExportJobServiceSpecTests.cs`, `ExcelExportRegistrationTests.cs`, `ExcelSpecContractTests.cs`, `ExcelFooterParityLiveDbTests.cs`; additions to `StreamingExcelWriterTests.cs` and `ExcelExportLayoutContractTests.cs`; `Backend.Tests.csproj` copies `Fixtures/**/*.json`; `Backend.Tests/Fixtures/ExcelSpecs/allowlist.json`.
Per-controller (fan-out agents; each edits ONLY its own file): `Backend/Controllers/Report/<Name>Controller.cs` (+ `Backend.Tests/ExcelParity/<Name>ControllerLayoutTests.cs` for group D).

## 8. Frontend files (Phase 0, SHARED)

1. `Frontend/src/Report/config/reportTypes.ts` — `hidden?: boolean` on `ReportColumnConfig`.
2. `Frontend/src/Report/reportPresentation.ts` (new) — `formTypePrefixes`, `getDerivedFilterValues` (moved from `GenericReportPage.tsx` ~52-61, ~262-291), `formatLegacyReportDate` (`DD/MM/YYYY`), `buildReportHeaderLines`, `isLegacyReportViewer` (~605-608), `resolveRowNumberTitle` (~1021), `resolveReportColumns` (`resolveColumns` + `!hidden`, ~594-600), `getReportConfigKey` (reverse map by object identity, fallback `controllerName`).
3. `Frontend/src/Report/excel/excelTypes.ts` (new); `excel/buildExcelPresentation.ts` (new: `toPresentationColumn`, `buildExcelPresentationFromInput`, `buildExcelPresentation`); `excel/excelEnqueue.ts` (new: `enqueueExcelExport`, `ExcelSpecRejectedError`).
4. `Frontend/src/Report/Page/GenericReportPage.tsx` — use shared helpers; `generateExcel` (~732-777) posts `{ ...buildRequest(currentFilters, query), excel: spec }` via `enqueueExcelExport`; `reportHeaderLines` (~932-940) via `buildReportHeaderLines` (still gated on `hasAppliedFilters`).
5. `Frontend/src/components/My Components/Table/BasicTable.tsx` — remove the dead `xlsx` DOM-scrape path (import ~20, `exportClientTableToExcel` ~345-353, `!onExcel` branch ~356-359); `npm uninstall xlsx`.
6. `Frontend/src/Report/excel/bespoke/index.ts` (registry `bespokeSpecBuilders: Record<configKey, (applied) => ExcelPresentationSpec>`) + per-page modules (Phase 3).
7. `Frontend/src/Report/excel/buildExcelPresentation.test.ts`, `excel/exportExcelSpecFixtures.test.ts` (vitest; `include: src/**/*.test.{ts,tsx}`, environment node); `package.json` script `"fixtures:excel": "EXCEL_SPEC_FIXTURES=write vitest run src/Report/excel/exportExcelSpecFixtures.test.ts"`.
Bespoke pages (Phase 3, one agent each): `Frontend/src/Report/Page/{MemberRegistrationReport,ListOfDirectors,CompanyProfile,ImportLicenceTotalValueLicencesReport,BorderImportLicenceTotalValueLicencesReport,ExportLicenceTotalValueLicencesReport,BorderExportLicenceTotalValueLicencesReport}.tsx`.

## 9. Per-report recipes

- **A/B/E/F (verify-only, 112):** see `Prompts/implement-verify-only.md`. Row type = what `WriteRowsAsync` appends (A: `sp_X.ExecuteQueryable/Query` + `ToResult()`; B: `ChunkAsync`/`StreamResolvedChunksAsync` chunks; E: `GetLicenceListRowsAsync` → `ReportLicenceListResult`; F: `SummaryRowAsync` → `RegistrationSummaryRow`).
- **C (44):** `Backend/Service/Reports/ReportAggregationService.cs:282` —
  `public static List<ReportAggregateResult> OrderGroups(IReadOnlyList<ReportAggregateResult> grouped, ReportAggregateDimension dimension, bool includeSakhan)` (same `Order` as the JSON path).
  Edit: `sink.Append(ReportAggregationService.OrderGroups(rows, ReportAggregateDimension.<X>, includeSakhan: <b>));` with `<X>/<b>` copied from the controller's `Post`.
- **D (4 `*TotalValueLicencesReportController`):** `Post` returns `ActionResult<ImportLicenceTotalValueLicencesSummary>` from
  `sp_ImportLicenceDetailReport_Fast.GetTotalValueLicencesSummaryAsync` / `sp_ExportLicenceDetailReport_Fast.GetTotalValueLicencesSummaryAsync`.
  Summary (`Backend/Service/Reports/ImportLicenceTotalValueLicencesSummary.cs`): `TotalValueByCurrency: List<TotalValueByCurrencyRow { Currency, TotalValue }>`,
  `TotalLicencesByPaThaKaType: List<TotalLicencesByPaThaKaTypeRow { PaThaKaType, NoOfLicences }>`, `TotalUsdValue: decimal`.
  Typed layout with two `ExcelReportSection`s (Sr.No. | Total Value (Money4) | Currency; Sr.No. | Total Licences | Pa Tha Ka Type), `WriteRowsAsync` →
  `BeginSection(0)/Append`, `BeginSection(1)/Append`, `AppendNote("Total USD Value: N4")`; `[ExcelFormatVersion(2)]`; no footer.
- **Footer check (43 ColumnTotals + 34 CurrencyTotals, 1 both):** `Prompts/footer-check.md`. Default probe unless totals are page-dependent or the probe path times out
  (candidates with cheap `ExecuteColumnTotalsAsync`: AccountSummary, MPU, MPUV3, ChequeNo, OnlineFees) → `IExcelFooterTotalsProvider`.
- **AccountSummary (typed, peer):** `.Bind("DeductedFees", "amount")` on the Deducted Fees column so the footer uses `Post`'s `ColumnTotals["amount"]`.
- **CompanyProfile:** keep/add a typed layout (composed "RegNo (dd/MM/yyyy)" cell, joined address, 2-row header group); headers must equal the page's leaf titles.

## 10. Fixtures and the oracle

- Fixture files: `Backend.Tests/Fixtures/ExcelSpecs/<ConfigKey>.json` (the spec object exactly as the frontend would post it) and
  `<ConfigKey>.ApplyType-<opt>.json` for the 5 voucher configs × ApplyType options (New, Amend, Extension, Cancel, Actual Amend → spaces become `_`).
  Sample filters: `FromDate 2026-02-01T00:00:00`, `ToDate 2026-02-28T23:59:59`, `Date 2026-02-15T00:00:00`, `ApplyType 'New'`, plus derived hidden `FormType`/`Type`.
- `index.json`: `{ formatVersion, source, sampleFilters, entries: [{ configKey, controllerName, file, variant, hasDateRange, hasSingleDate, hasCurrencyTotalsColumns, showRowNumber, rowNumberTitle, isBespokePage, isComposite, hasExcelButton, columnCount }] }` sorted by `file`, no timestamps.
- `allowlist.json`: `[{ "controller": "AccountSummaryReport", "dataIndex": "remark", "reason": "RDLC-unbound blank column" }]` sorted by controller, dataIndex.
- `ExcelSpecContractTests` (theory over fixture files, deserialized into the production DTO): (a) every `IStreamingExcelReport` controller has ≥ 1 fixture (exclusions: CardListsByCompanyRegistrationNumber, DataImport, ImportLicenceDataImport; aliases have 2); (b) every column `dataIndex` or a fallback resolves on `ExcelRowTypeResolver.Resolve(controller)` — unresolvable ⊆ allowlist; composites: `sections[].dataPath`/`summaryLines[].dataPath` resolve on the summary type; (c) `hasDateRange` == `ExcelRequestDates.Describe(requestType)` has From/To; (d) `currencyTotalsColumns` keys ∈ column keys; (e) built layout header cells == `[rowNumberTitle?] + titles` and preamble == expected lines incl. `From Date: 01/02/2026` / `To Date: 28/02/2026` (fixed `TimeProvider` for `Exported`); typed providers: header texts == spec titles.
- `ExcelFooterParityLiveDbTests`: uses `TRADENET_REPORT_TEST_CONNECTION_STRING` (export from `Backend/appsettings.json` `ConnectionStrings:TradeNetDBTest`, never print); `ReportSqlServerFixture` is `(localdb)` Windows-only — do not use on macOS; use **2025** date ranges (the DB data lives in 2025); when the DB is unreachable report `unverified-nodb`, never silently pass.

## 11. Manifest

`docs/ExcelParity/manifest.json` — regenerate with `node tools/build-excel-parity-manifest.mjs` (runs `Frontend/scripts/excelParityManifest.ts` via vite-node; imports `reportConfigs` at runtime because `newReportConfigs.ts` builds configs through factories).
Item fields: `controller, reportKey, controllerFile, requestType, group, shape, configKeys[], aliasConfigKeys[], configTitle, excelWorksheetTitle, titleMismatch, subtitleSample, reportHeading[], showRowNumber, dateShape, requestDateProps[], dateShapeDisagrees, hasColumnTotals, hasCurrencyTotals, currencyTotalsColumns, variants[], columnCount, bespokePage, excelPagePattern, hasLayoutProvider, formatVersion, fixtureFiles[]`.
Group classifier (first `await` in the private `WriteRowsAsync`): name ends `TotalValueLicencesReportController` → D; `SummaryRowAsync(` → F; `GetLicenceListRowsAsync(` → E; `GetAggregateRowsAsync(|GetSummaryRowsAsync(|GetSectionRowsAsync(` → C; `await foreach` over `ExecuteQueryable(`/`.Query(` → A; other `await foreach` → B.
Counts at HEAD 261ec10: total 160; A 68, B 33, C 44, D 4, E 1, F 10; hasColumnTotals 43; hasCurrencyTotals 34; both 1 (ImportLicenceVoucherReport); aliases 4; variant configs 5; titleMismatch 8; bespoke pages 8 (7 with an Excel button; `ListOfDirectorsByCompanyRegistrationNo` has none).

## 12. Hard rules for all agents

Never `git commit/push/merge/checkout <branch>/stash/reset` (the repo auto-commits Claude's edits; the build server auto-deploys `origin/main` — do not merge until `docs/ExcelParity/Status.md` says `MERGEABLE: yes`).
Never write `Status.md` except the status-writer agent. Never print connection strings. Per-controller agents edit only their own file(s); shared files only via `sharedEditRequests` → the applier/core-repair agent. Nobody but gate agents runs `dotnet`/`npm` during the fan-out.

## 13. Known facts and gotchas

- 4 alias configs share a controller with a different column set: `BorderExportPermitHSCodeDetailReport`, `BorderImportLicenceHSCodeDetailReport`, `BorderImportPermitHSCodeDetailReport`, `ExportLicenceHSCodeDetailReport` → the `*ByHSCodeReport` controllers. Columns come from the spec; `configKey` is in the spec/hash.
- 5 voucher configs use `resolveColumns` (header text by ApplyType; raw config titles include `'=Parameters!header2.Value'` placeholders that MUST be resolved before export): BorderExportLicenceVoucherReport, BorderExportPermitVoucherReport, BorderImportLicenceVoucherReport, ExportLicenceVoucherReport, ImportLicenceVoucherReport.
- `ExcelWorksheetTitle ≠ config.title` for 8 controllers (see manifest `titleMismatch`), incl. BorderExportLicenceBySellerCountryReport, ExportPermitBySellerCountryReport, ImportLicenceDetailByLicenceReport, ImportLicenceDetailReportPending, ImportLicenceNewReportNewReport, PaThaKaRegisteredBusinessOrganizationReport.
- `dataIndex == CamelCase(C# property)` everywhere (no `[JsonPropertyName]`); `key` often differs from `dataIndex` (e.g. `key: 'LicenceNo'`, `dataIndex: 'oldLicenceNo'`).
- `columnTotals` are keyed by `dataIndex`; `currencyTotalsColumns` by column `key`.
- Group C Excel rows are unordered today (JSON path orders via `CreatePagedResultFromGroups`). `ReportAggregateResult` is a 17-property union DTO (3–7 used per report); ids `SectionId/MethodId/CountryId` are drill-down plumbing, never UI columns.
- `MemberRegistrationReport.tsx` still uses a legacy synchronous blob download while its controller enqueues a job (broken today).
- The 4 `*TotalValueLicencesReport.tsx` pages render two tables + a "Total USD Value" line; today's Excel streams only the TotalValue aggregate rows.
- `ReportSqlServerFixture` = `(localdb)\mssqllocaldb` (Windows only). Live checks need `TRADENET_REPORT_TEST_CONNECTION_STRING`; DB data lives in 2025.
- `newReportConfigs.ts` configs are factory-built — read configs at runtime (vitest/vite-node), never via the TypeScript AST.
- `Frontend/vitest.config.ts`: environment `node`, include `src/**/*.test.{ts,tsx}`; `vite-node` and `vitest` exist, `tsx` does not; python3 + openpyxl 3.1.5 available; dotnet SDK 10 (projects target net8.0); solution file `tradenet-admin-report.sln` at the repo root.
- Peer commit `7e02910` (2026-09-02 19:45) is the base: `ExcelReportLayout`, `IExcelReportLayoutProvider`, `ExcelFormatVersionAttribute`, hasher version salt, writer layout mode, AccountSummary opt-in, `StreamingExcelWriterTests` layout facts, `ExcelExportLayoutContractTests`. Extend it — never redesign it. Other sessions edit this repo concurrently: re-read a file before editing if it changed unexpectedly.

## 14. Verification scope (recorded decision, 2026-09-03)

Adversarial skeptics run for the **98** reports where judgment can change the answer: any report with a
`ColumnTotals` or `CurrencyTotals` footer, every group C report (its row order changes), every group D composite,
every controller whose `ExcelWorksheetTitle` differs from the config title, the 4 alias configs, the 5
filter-dependent voucher configs, and the bespoke pages. The remaining **62** (24 group A, 28 group B, 10 group F)
are mechanical-only: no footer, no ordering change, no header nuance. For those, `ExcelSpecContractTests` is the
sole evidence, and it proves the whole rule set for them — header block, exact UI columns in UI order, every
`dataIndex` binding to a real property on the streamed row type, and the date shape. `Status.md` records
`skepticScope: not-required` for them so the gap is visible rather than implied.

Waves are 32 controllers, so 5 gate cycles instead of 8. A gate is a full build plus test run, and it is the most
expensive serial step in the workflow; wave size trades gate count against how early a systemic problem surfaces.

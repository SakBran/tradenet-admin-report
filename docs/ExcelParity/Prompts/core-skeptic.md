# Core skeptic (one lens per agent) — read `_preamble.md`, then `Contract.md`

Read-only. Try to **refute** that the core implementation satisfies the contract for your lens. Cite `path:line`.
Default to `refuted: true` when uncertain. Do not run builds or tests.

Lenses:
- **header-and-dates** — `WithStandardHeaderBlock`: title added only when no line contains it; spec `headerLines`
  deduped; `From Date:`/`To Date:` from the request DTO (`FromDate`/`ToDate`), `Date:` for single, nothing for none;
  `Exported: dd/MM/yyyy HH:mm`; InvariantCulture everywhere (`/` is a culture placeholder in .NET); meta lines
  unmerged/left, title lines merged; the block repeats on rollover; `_headerRowIndex` math vs row `r` attributes
  (Excel reports a corrupt file on duplicates); freeze pane emitted before `<cols>`.
- **columns-and-render-rules** — `ExcelLayoutBuilder` vs `GenericReportPage.toTableColumn` + `BasicTable`: camelCase
  map (`NRCNo→nrcNo`), fallback join `", "` only when primary blank, `N/A` for blank text and EMPTY for blank numeric/date,
  date serials + `dd/mm/yyyy`, dateTime format, boolean Yes/No, money 2dp numeric, the 3 special dataIndexes
  (`transactionAmount` minor units when integer string; `mpuAmount`; `amountDiff`), `No` column ordinal across chunks/sheets,
  unknown dataIndex → blank + warning, `hidden`/drilldown never exported, widths, `columns: []` only with sections.
- **footer-totals-resolver** — `DefaultExcelFooterTotalsResolver`: clone keeps every filter, sets `PageIndex 0 / PageSize 1 / IncludeTotalCount true / Excel null`;
  `FindBarePost` picks the `[HttpPost]` with empty template and the right parameter type; unwrapping `ActionResult<T>`
  via `IConvertToActionResult` handles `Ok(...)`, bare `Value`, `BadRequest`; `IReportTotals` on `ApiResult<T>`;
  override precedence; `FooterTotalsPolicy`; `ExcelFooterBuilder` placement rules vs `BasicTable.tsx` (Total label
  column, currency rows by KEY, grand row `TOTAL` cell, N4 formatting, nothing when 0 rows).
- **cache-version-and-enqueue** — every handler `FormatVersion >= 2` (`+ ExcelExportFormat.Generation`); the spec is
  inside the hashed JSON and NOT in `IgnoredFields`; `ReportTitle`/`FileName` from the sanitized spec with defaults;
  `RequireExcelPresentationSpecFilter` 400 rules (missing spec on non-typed controller; invalid spec; typed controllers
  pass without spec) and its registration via `Configure<MvcOptions>` inside `AddExcelExportQueue`; grid POSTs unaffected;
  `ExcelPresentationSpecException` → 400; the peer's `ExcelExportLayoutContractTests` still hold.

Return `VERDICT` with `controller: "core"`, `refutedRules` naming the contract sections/rules broken, `evidence`
(`path:line — …`), `suggestedFix`. If nothing is wrong, `refuted: false` and list what you checked.

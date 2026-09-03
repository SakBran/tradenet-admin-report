# Footer check (appended to every implement prompt) — read `Contract.md` §5 first

The Excel footer must equal the grid footer. The grid gets its footer from the JSON `Post` response of a request
with `includeTotalCount: true` (BasicTable's lazy exact-count call). The default backend
`ExcelFooterTotalsResolver` reproduces that by invoking the controller's bare `[HttpPost] Post` with a cloned request
`{ PageIndex 0, PageSize 1, IncludeTotalCount true, Excel null }` and reading `ColumnTotals` / `CurrencyTotals`.

For your controller:
1. If `hasColumnTotals` (manifest): find in `Post` how `ColumnTotals` is produced — `CreatePagedResultFromGroups(..., includeColumnTotals: true)`
   → `ReportAggregationService.BuildColumnTotals` (keys `noOfLicences`, `totalValue`, + `totalUSDValue` for Daily), or an
   explicit dictionary (`["companyCount"] = grandTotal`, `["amount"] = …`). Write down the keys and whether each is
   computed from the whole filtered set (cross-page) or only from the returned page rows.
2. If `hasCurrencyTotals`: find the `*CurrencyTotals.ExecuteAsync(...)` call and its arguments; note any guard such as
   `if (data.Count > 0)` (satisfied by PageSize 1 whenever the export has rows) or a guard on `request.IncludeTotalCount`.
3. Decide: would the probe `Post(PageIndex 0, PageSize 1, IncludeTotalCount true)` return exactly the totals the grid
   shows? It would NOT when totals are computed from the page rows only, when they depend on `SortColumn`/`PageSize`,
   when `Post` needs `HttpContext`/`User` (none do today — verify yours), or when the probe path is known to time out
   (the probe runs the exact `COUNT(*)` — AccountSummary, MPU, MPUV3, ChequeNo, OnlineFees have cheap
   `ExecuteColumnTotalsAsync` helpers instead).
4. Only if the probe cannot reproduce the grid, implement the override in the controller (mark it `[NonAction]`):
   ```csharp
   [NonAction]
   public async Task<ReportFooterTotals?> GetExcelFooterTotalsAsync(object request, CancellationToken ct)
   {
       TryCreateReportRequest((<Name>Request)request, out var p, out _);
       var columnTotals = await sp_X.ExecuteColumnTotalsAsync(_context, p!);      // the same helper Post uses
       var currencyTotals = await XCurrencyTotals.ExecuteAsync(_context, ...);      // if applicable
       return new ReportFooterTotals(columnTotals, currencyTotals);
   }
   ```
   and add `IExcelFooterTotalsProvider` to the class declaration. Read `IStreamingExcelReport.cs` for the exact
   record/interface names the core created.
5. Fixture placement check: for `hasCurrencyTotals`, the fixture's `currencyTotalsColumns.labelColumnKey` /
   `valueColumnKey` must be column `key`s present in `columns[]` (not dataIndexes). For `hasColumnTotals`, every
   totals key must be a column `dataIndex` in the fixture, otherwise the grid never showed that total either — note it.

Record in `notes`: `footer: default-probe` or `footer: override(<helper>)`, the keys, and the guards you saw.
Set `rulesVerified.footer` = true only if you are confident the Excel footer will equal the grid footer.

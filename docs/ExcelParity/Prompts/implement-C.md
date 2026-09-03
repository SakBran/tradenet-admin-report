# Implement — group C (aggregate / By-X / Daily / Summary-rows reports) — read `_preamble.md`, then `Contract.md`

These controllers append `ReportAggregateResult` groups from `GetAggregateRowsAsync` / `GetSummaryRowsAsync` /
`GetSectionRowsAsync`. The JSON path orders them (`ReportAggregationService.CreatePagedResultFromGroups` →
`Order`), the Excel path does not — so Excel row order differs from the grid. You may edit **only** `controllerFile`.
Do not run dotnet/npm.

1. In `Post`, note the aggregation call and its arguments: `ReportAggregateDimension.<X>`, `includeSakhan: <b>`,
   `includeColumnTotals: <c>` (or `sp_HSCodeReport.CreateAggregateResultAsync` → dimension HSCode, includeSakhan false;
   or `*V2.GetSummaryRowsAsync(..., dimension, ...)` → the dimension variable already in scope).
2. In the private `WriteRowsAsync`, wrap the appended list with the **same ordering the JSON path uses**:
   ```csharp
   sink.Append(ReportAggregationService.OrderGroups(rows, ReportAggregateDimension.<X>, includeSakhan: <b>));
   ```
   Read `Backend/Service/Reports/ReportAggregationService.cs` first and use the real public method name/signature
   (`OrderGroups` per the contract; if it is named differently or not public, do NOT change that file — return
   `status:"blocked"` with a `sharedEditRequests` entry asking to expose it). Add the `using` if needed.
   Do not change `Post`. Do not add `IExcelReportLayoutProvider` — the generic spec path must serve group C.
3. Columns: open each fixture in `fixtureFiles`; confirm every `dataIndex` (`sectionName`, `noOfLicences`,
   `totalValue`, `totalUSDValue`, `hsCode`, `hsDescription`, `companyName`, `date`, …) resolves on
   `ReportAggregateResult` public properties (camelCase). Unresolved → `sharedEditRequests` as in
   `implement-verify-only.md` step 2. For alias configs (`aliasConfigKeys` non-empty) check both fixtures.
4. Title: if `titleMismatch`, align `ExcelWorksheetTitle` to `configTitle`.
5. Footer: run `footer-check.md` (most group C controllers have `includeColumnTotals: true` →
   `BuildColumnTotals` keys `noOfLicences`, `totalValue`, and `totalUSDValue` for Daily).

Return `REPORT` (shape in `implement-verify-only.md`); put the exact before/after `sink.Append` lines in `edits`.

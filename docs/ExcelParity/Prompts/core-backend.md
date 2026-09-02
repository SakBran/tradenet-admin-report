# Core — backend (read `_preamble.md`, then `Contract.md` in full)

Implement the one-time backend core exactly as `Contract.md` §3–§7 and §10 specify. You own the shared backend
files; do not touch `Frontend/` and do not touch any `Backend/Controllers/Report/*.cs` except
`AccountSummaryReportController.cs` (only to `.Bind("DeductedFees", "amount")` its Deducted Fees column, §9).

Order of work (each step compiles on its own):
1. `Backend/Model/ExcelExport/ExcelPresentationSpec.cs` + `ExcelPresentationSpecValidator.cs` (+ exception).
2. `Backend/Model/ReportQueryRequest.cs` → `[JsonIgnore(WhenWritingNull)] ExcelPresentationSpec? Excel`.
3. `Backend/Model/IReportTotals.cs`; `ApiResult<T> : IReportTotals`.
4. Extend `Backend/Service/ExcelExport/ExcelReportLayout.cs` (formats `DateTime`, `Money4`; `ExcelColumn.Key/DataIndex/IsRowNumber/IsNumeric/Bind/Untyped/Money4<T>`; `ExcelHeaderLine`; `ExcelReportSection`; layout `HeaderBlock/CurrencyTotalsColumns/Sections/FreezeHeader/With`; `ExcelFooterRow/Cell`). Keep every existing member and default so the peer's tests still pass unchanged.
5. `IStreamingExcelReport.cs`: `IExcelRowSink.BeginSection/AppendNote` default members; `IExcelFooterTotalsProvider`; `IExcelRowTypeProvider`; `ReportFooterTotals`; `ExcelExportFormat.Generation`.
6. `ExcelRowPropertyMap.cs`, `ExcelRowTypeResolver.cs`, `ExcelRequestDates.cs`.
7. `ExcelLayoutBuilder.cs` (`Build`, `WithStandardHeaderBlock`) — implement the cell rules of §4 literally; `NullText` constant.
8. `ExcelFooterBuilder.cs` — §5 literally.
9. `ExcelFooterTotalsResolver.cs` (`IExcelFooterTotalsResolver`, `DefaultExcelFooterTotalsResolver`) + `FooterTotalsPolicy` in `ExcelExportOptions.cs`.
10. `StreamingExcelWriter.cs` — preamble/header block, freeze pane before `<cols>`, `AppendFooterRows`, `BeginSection`, `AppendNote`, new styles; with empty new members the output must be byte-identical to today.
11. `ControllerStreamingExcelReportJobHandler.cs` — precedence typed > spec > throw; header block; footer resolution before streaming; `AppendFooterRows`.
12. `RequireExcelPresentationSpecFilter.cs`; `ExcelExportJobService.cs` (title/fileName from spec, defensive spec check); `ExcelExportServiceCollectionExtensions.cs` (resolver, `TimeProvider`, filter via `Configure<MvcOptions>`, `formatVersion + Generation`).
13. Tests listed in `Contract.md` §10 and the plan (unit tests + `ExcelSpecContractTests` + `ExcelFooterParityLiveDbTests` honouring `TRADENET_REPORT_TEST_CONNECTION_STRING`, reporting `unverified-nodb` instead of failing when the DB is unreachable). `Backend.Tests.csproj`: copy `Fixtures/**/*.json` to output. Create `Backend.Tests/Fixtures/ExcelSpecs/allowlist.json` with the AccountSummary `remark` entry (the frontend agent generates the other fixture files; if they are not there yet, make the theory skip with a clear message rather than fail).

You MAY run `dotnet build Backend/API.csproj` and `dotnet build Backend.Tests/Backend.Tests.csproj` once at the end (fix compile errors you caused). Do not run the tests (the gate does). Do not run npm.

Return `REPORT` with `controller: "core-backend"`, `edits` listing every file created/modified, `concerns` for any contract point you could not implement as written (and what you did instead), `notes` summarising the public API surface the fan-out agents will use (`OrderGroups` signature, `Money4<T>`, `ExcelReportSection`, `BeginSection/AppendNote`, `IExcelFooterTotalsProvider`, `ReportFooterTotals`).

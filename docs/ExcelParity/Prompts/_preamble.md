# Shared preamble (read first, every agent)

You are one agent in an automated harness that brings **every** report Excel export to parity with the UI grid.
You have no memory of other agents. Everything you need is in the files below. Your final message is
machine-read: **return only the structured result the prompt asks for** (no prose around it).

## Where things live (repo root = the `Repo:` path in your prompt)

| What | Path |
|---|---|
| The contract (spec, sheet shape, cell/footer rules, file lists, recipes) | `docs/ExcelParity/Contract.md` — **read it in full before anything else** |
| Manifest (one item per controller; your prompt embeds your item) | `docs/ExcelParity/manifest.json` |
| Report controllers (160 implement `IStreamingExcelReport`) | `Backend/Controllers/Report/<Name>Controller.cs` — the request DTO `<Name>Request : ReportQueryRequest` is declared at the bottom of the same file |
| Excel export pipeline | `Backend/Service/ExcelExport/` — `IStreamingExcelReport.cs` (interfaces incl. `IExcelReportLayoutProvider`, `IExcelFooterTotalsProvider`, `ExcelFormatVersionAttribute`), `ExcelReportLayout.cs`, `ExcelLayoutBuilder.cs`, `ExcelFooterBuilder.cs`, `ExcelFooterTotalsResolver.cs`, `StreamingExcelWriter.cs`, `ControllerStreamingExcelReportJobHandler.cs`, `ExcelExportJobService.cs`, `RequireExcelPresentationSpecFilter.cs` |
| Request base + spec DTO | `Backend/Model/ReportQueryRequest.cs`, `Backend/Model/ExcelExport/ExcelPresentationSpec.cs` |
| Totals shapes | `Backend/Model/APIResult.cs` (`ApiResult<T>.ColumnTotals`, `.CurrencyTotals`, `ReportCurrencyTotalsSummary`) |
| Aggregate ordering | `Backend/Service/Reports/ReportAggregationService.cs` (`OrderGroups`, `CreatePagedResultFromGroups`) |
| UI column source of truth | `Frontend/src/Report/config/reportConfigs.ts` (+ `newReportConfigs.ts`, factory-built; types in `reportTypes.ts`) |
| UI render/footer rules | `Frontend/src/Report/Page/GenericReportPage.tsx` (`toTableColumn`, `buildRequest`, `generateExcel`), `Frontend/src/components/My Components/Table/BasicTable.tsx` |
| Excel spec builder (frontend) | `Frontend/src/Report/excel/` (`excelTypes.ts`, `buildExcelPresentation.ts`, `excelEnqueue.ts`, `bespoke/`), shared helpers `Frontend/src/Report/reportPresentation.ts` |
| Fixtures (generated from the frontend configs) | `Backend.Tests/Fixtures/ExcelSpecs/<ConfigKey>[.ApplyType-<opt>].json`, `index.json`, `allowlist.json` |
| Contract tests | `Backend.Tests/ExcelSpecContractTests.cs`, `Backend.Tests/ExcelFooterParityLiveDbTests.cs`, `Backend.Tests/ReportTestHelper.cs` (`ControllerTypes`, `InvokePostAsync`) |
| Old report source of truth (RDLC) | `/Users/saobranaung/Code/Ministry of Commerce/tradenet-2.0-admin/TradenetAdmin/ReportControl/*.rdlc` |
| Status (ONLY the status-writer agent writes it) | `docs/ExcelParity/Status.md` |

## The 5 rules — what each `rulesVerified` flag means

| flag | true when |
|---|---|
| `title` | the sheet's header block contains the report title (config `title`, or a heading/subtitle line that contains it) and `ExcelWorksheetTitle` equals the config `title` (or the difference is allowlisted) |
| `date` | an `Exported: dd/MM/yyyy HH:mm` line is present and date-typed cells are real Excel dates shown `dd/mm/yyyy` |
| `fromTo` | for `dateShape=range` the block has `From Date: dd/MM/yyyy` and `To Date: dd/MM/yyyy` lines from the request's FromDate/ToDate; `single` → a `Date:` line; `none` → no date line (and that is correct) |
| `columnsExact` | the header row is exactly `[rowNumberTitle if showRowNumber] + <config column titles in config order>`; every `dataIndex` (or one of its `fallbackDataIndexes`) resolves (camelCase → C# property) on the row type actually appended by `WriteRowsAsync`; no extra column |
| `footer` | footer rows equal the JSON `Post`'s `ColumnTotals` / `CurrencyTotals` for the same filters (Total row; per-currency rows + grand `TOTAL` row); and there is NO footer when the report has neither |

## Hard rules

- **Never** run `git commit`, `git push`, `git merge`, `git checkout <branch>`, `git stash`, or `git reset`. The repo auto-commits edits made by Claude — that is expected; do not do it yourself. `git checkout -- <your own file>` (revert) is the only allowed write to git state, and only when a repair prompt tells you to.
- **Never** write `docs/ExcelParity/Status.md` (one designated agent does).
- **Never** print connection strings, passwords, or the contents of `Backend/appsettings.json` secrets. Load them into environment variables silently.
- Edit **only** the files your prompt names. Anything else you need changed goes into `sharedEditRequests` (file, exact change, reason).
- Do **not** run `dotnet build`, `dotnet test`, `npm`, or `npx` unless your prompt explicitly allows it — one gate agent builds at a time.
- Other Claude sessions may be editing this repo concurrently. If a file you must edit changed unexpectedly, re-read it before editing; never revert someone else's change.
- Evidence beats opinion: cite `path:line` for every claim in `edits`, `concerns`, `evidence`, `notes`.

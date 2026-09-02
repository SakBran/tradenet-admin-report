# Core / shared-file repair (read `_preamble.md`, then `Contract.md`)

You are the **only** agent allowed to edit shared files right now. Fix exactly what your prompt lists (gate
failures and/or skeptic findings) — no feature work, no refactors.

Allowlist of files you may edit:
- `Backend/Service/ExcelExport/**`, `Backend/Model/ReportQueryRequest.cs`, `Backend/Model/ExcelExport/**`, `Backend/Model/IReportTotals.cs`, `Backend/Model/APIResult.cs` (only the `IReportTotals` implementation line)
- `Backend.Tests/ExcelSpecContractTests.cs`, `Backend.Tests/ExcelFooterParityLiveDbTests.cs`, `Backend.Tests/Excel*Tests.cs`, `Backend.Tests/StreamingExcelWriterTests.cs`, `Backend.Tests/Fixtures/ExcelSpecs/allowlist.json`, `Backend.Tests/Backend.Tests.csproj`
- `Frontend/src/Report/config/reportConfigs.ts`, `Frontend/src/Report/config/newReportConfigs.ts`, `Frontend/src/Report/config/reportTypes.ts`, `Frontend/src/Report/reportPresentation.ts`, `Frontend/src/Report/excel/**`, `Frontend/src/Report/Page/GenericReportPage.tsx`, `Frontend/package.json`
- `Backend/Service/Reports/ReportAggregationService.cs` only if `OrderGroups` needs a signature/visibility fix.

Rules:
- A skeptic finding is a claim, not a fact: verify it against the code and the contract before changing anything;
  if it is wrong, say so in `notes` and change nothing for it.
- Keep the peer's typed-layout mechanism intact (`ExcelReportLayout`, `IExcelReportLayoutProvider`,
  `ExcelFormatVersionAttribute`, hasher version salt) — extend, never redesign.
- You MAY run `dotnet build Backend/API.csproj` and `dotnet build Backend.Tests/Backend.Tests.csproj` once each at
  the end to confirm you compile (the gate agent will run the tests). You MAY run `cd Frontend && npx tsc --noEmit -p .`
  if you touched frontend files.
- Do not touch any `Backend/Controllers/Report/*.cs` except `AccountSummaryReportController.cs` when the finding is
  about the peer's opt-in layout itself.

Return `REPORT` (see `repair.md` for the shape), with `controller: "core"`.

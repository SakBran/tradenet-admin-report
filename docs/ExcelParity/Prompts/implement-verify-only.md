# Implement — groups A / B / E / F (verify-only) — read `_preamble.md`, then `Contract.md`

Your manifest item is embedded in your prompt. These groups already stream the same row DTO the grid receives, so
the generic spec-driven layout serves them; your job is to **prove** that for this one controller and fix only
what lives in the controller file. Do not run dotnet/npm.

1. **Row type.** Open `controllerFile`. In the private `WriteRowsAsync`, identify the element type actually
   appended to `sink.Append(...)`:
   - A: element type of `sp_X.ExecuteQueryable(...)` / `sp_X.Query(...)` after `.Select(r => r.ToResult())` (→ `sp_XResult`) or raw;
   - B: element type of `ChunkAsync` / `StreamResolvedChunksAsync` chunks (open the `sp_*` file to confirm);
   - E: element type of `GetLicenceListRowsAsync` (`ReportLicenceListResult`);
   - F: the single `SummaryRowAsync` row type (`RegistrationSummaryRow`).
   Confirm it equals `T` in `Post`'s `Task<ActionResult<ApiResult<T>>>` (the contract test resolves the row type
   from `Post`). If they differ, that is a `blocked` finding — report both type names in `notes`.
2. **Columns.** Open every file in `fixtureFiles`. For each spec column, resolve `dataIndex` against the row type's
   public properties using camelCase (`JsonNamingPolicy.CamelCase`: `NRCNo→nrcNo`, `HSCode→hsCode`, `Id→id`), then
   each `fallbackDataIndexes` entry. List unresolved ones. For each unresolved column decide:
   - intentionally unbound in the old RDLC (header with a blank body, e.g. AccountSummary "Remark") → request an
     `allowlist.json` entry via `sharedEditRequests` `{ file: "Backend.Tests/Fixtures/ExcelSpecs/allowlist.json", change: "add {controller, dataIndex, reason}", reason }`;
   - a wrong `dataIndex` in the frontend config (the UI column is blank today too) → request the exact
     `reportConfigs.ts` fix (`dataIndex: 'x'` → `'y'`, with the line) via `sharedEditRequests`. Never rename C# properties.
3. **Title.** If `titleMismatch`, change `ExcelWorksheetTitle` in the controller to `configTitle` (verbatim), unless
   the config title is clearly the wrong one (e.g. the RDLC and the controller agree and the config is a typo) — then
   request the config change instead and explain.
4. **Dates.** Check `dateShape` vs `requestDateProps` and the fixture's `hasDateRange`. If the request DTO lacks the
   date properties the header needs, record a `concern` (do not add properties).
5. **Footer.** Run the steps in `footer-check.md`.
6. **Row number.** Note `showRowNumber`/`rowNumberTitle` from the fixture; nothing to edit.

Return `REPORT`:
```json
{ "controller": "<Name>Controller", "status": "ok|needs-shared-edit|blocked",
  "rulesVerified": { "title": bool, "date": bool, "fromTo": bool, "columnsExact": bool, "footer": bool },   // predicted from static inspection
  "edits": ["path:line — what"], "sharedEditRequests": [...], "concerns": ["..."],
  "notes": "row type <T>; unresolved columns [...]; footer decision ...; anything the skeptic should look at" }
```

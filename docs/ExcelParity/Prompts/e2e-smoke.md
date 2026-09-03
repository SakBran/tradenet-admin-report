# End-to-end smoke (read `_preamble.md`, then `Contract.md` §2, §5, §10)

Generate REAL exports for the controllers listed in your prompt (one per group A–F plus any extras) and verify the
.xlsx files with openpyxl. Never print connection strings.

Setup:
- Load the dev/test DB connection into env silently:
  `export TRADENET_REPORT_TEST_CONNECTION_STRING="$(python3 -c "import json;print(json.load(open('Backend/appsettings.json'))['ConnectionStrings']['TradeNetDBTest'])")"`.
  If the DB is unreachable (`ReportSeededDatabaseSmokeTests.TradeNetDBTest_database_is_available` fails), return `ran: false` with the reason.
- Preferred harness: an in-process `WebApplicationFactory` test modelled on `Backend.Tests/ImportPermitSystemTests.cs`
  (`ConnectionStrings:TradeNetDBTest` from env, `ExcelExport:Storage=Local`, `ExcelExport:StorageRoot=<your scratchpad dir>`,
  worker enabled). Put it in `Backend.Tests/ExcelParity/ExcelExportE2ESmokeTests.cs` marked to run only when an env var
  `EXCEL_E2E=1` is set (so the normal suite skips it). You may run `dotnet test --filter FullyQualifiedName~ExcelExportE2ESmokeTests`.
- Alternative if the factory pattern is not viable: `dotnet run --project Backend --no-launch-profile` on a free port and drive it with `curl` (login first via `api/Auth`).

Per controller:
1. Load its fixture (`Backend.Tests/Fixtures/ExcelSpecs/<ConfigKey>.json`) as the `excel` spec; use a **2025** date range
   (the DB's data lives in 2025), small enough to finish quickly (e.g. one week).
2. `POST api/<ReportKey>/Excel` with the fixture's filters + `excel` spec → job id; poll `GET api/ExcelExport/{id}` until `Completed`/`Failed`; download `GET api/ExcelExport/{id}/download`.
3. `POST api/<ReportKey>` with the same filters + `pageIndex 0, pageSize 1, includeTotalCount true` → `columnTotals`/`currencyTotals`.
4. Verify with `python3 tools/verify-excel-spec.py <file.xlsx> <fixture.json> --totals <totals.json>` (create this script if
   missing — openpyxl 3.1.5 is installed): header block rows == fixture `headerLines` (+ the backend's title-if-missing rule)
   followed by `From Date:`/`To Date:` (or `Date:`) and an `Exported:` line; header row == `[rowNumberTitle?] + titles`
   exactly (no extra cells); no data row wider than the header; footer rows equal the JSON totals (Total row; per-currency
   rows `{cur}:{n} licence(s)` / `{cur}:{N4}`; grand `TOTAL` row); composites: both section blocks + `Total USD Value` line;
   empty result → title + headers only, no footer.
5. `DELETE api/ExcelExport/{id}` for every job you created (jobs land in the shared TemplateDB).

Return `E2E`:
```json
{ "ran": bool, "passed": ["<Controller>"], "failed": ["<Controller>: <what mismatched>"], "notes": "harness used, date ranges, row counts, anything odd" }
```

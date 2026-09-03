# Gate agent — common procedure (read `_preamble.md` first)

You are the **only** process building or testing right now. You **fix nothing** — you produce evidence.
Run every step from the repo root, in order. If a step fails, still run the later steps that can run
(`--no-build` tests cannot run after a failed build; frontend steps can), and return the structured result.

## 1. Backend build → `buildOk`, `failures[]`

```bash
dotnet build tradenet-admin-report.sln --nologo -v q 2>&1 | tail -120
```
(If the `.sln` name differs, use the one in the repo root; fall back to `dotnet build Backend/API.csproj && dotnet build Backend.Tests/Backend.Tests.csproj`.)
Parse each `path(line,col): error CSxxxx: message` into `failures[{ file, error, controller? }]`; set `controller` to
the file's basename without `.cs` when the file is under `Backend/Controllers/Report/`. Deduplicate identical lines.

## 2. Database availability → `dbAvailable`

```bash
export TRADENET_REPORT_TEST_CONNECTION_STRING="$(python3 -c "import json;print(json.load(open('Backend/appsettings.json'))['ConnectionStrings']['TradeNetDBTest'])")"
dotnet test Backend.Tests/Backend.Tests.csproj --no-build --nologo --filter "FullyQualifiedName~ReportSeededDatabaseSmokeTests.TradeNetDBTest_database_is_available" 2>&1 | tail -15
```
`dbAvailable` = that test passed. **Never echo the variable.** `ReportSqlServerFixture` targets `(localdb)` and is
Windows-only; do not try to use it on this machine.

This single probe (~15s when the DB is down) is the ONLY DB-bound test you may run. See §3a.

## 3a. MANDATORY: skip the DB-bound suites (owner decision, 2026-09-03)

Every `dotnet test` command you run — except the §2 probe — MUST append this to its `--filter`, joined with `&`:

```
FullyQualifiedName!~ReportEndpointSmokeTests&FullyQualifiedName!~ReportSeededDatabaseSmokeTests&FullyQualifiedName!~BorderImportPermitEndpointTests&FullyQualifiedName!~TempSectionValidation&FullyQualifiedName!~StoredProcedureSmokeTests&FullyQualifiedName!~LiveDb&FullyQualifiedName!~BorderImportLicenceParityTests&FullyQualifiedName!~ImportPermitSystemTests
```

Why: the report DB is CGNAT-internal, so each of those tests burns a 15-second pre-login handshake timeout and fails.
Unfiltered the suite is **698 failures in 13m49s**; with this filter it is **9 failures in 4s** over 1538 tests, and the
9 are all in `known-failures.json`. Do not run the full suite "just to check", and do not narrow this filter.

Consequence you MUST report, never hide: footer parity is `unverified-nodb` for every report with totals, because
`ExcelFooterParityLiveDbTests` is one of the skipped suites. Say so in `summary`.

## 3. Contract tests → `results[]`

Use the filter your gate prompt specifies (wave: the listed controllers; core: the Excel unit tests + the contract
theory; final: everything that is not DB-bound) **combined with the §3a DB-skip clauses** and a trx logger, then parse
the trx with python `xml.etree`:

```bash
dotnet test Backend.Tests/Backend.Tests.csproj --no-build --nologo --filter "<FILTER>" --logger "trx;LogFileName=<tag>.trx" 2>&1 | tail -40
```
Map each `UnitTestResult` to a controller by the test's display name / data row (the theories are parameterised by
controller type or fixture file; the fixture's `controllerName` is the join key). Per controller produce
`{ controller, passed, header, columns, footer, error }` where `header`/`columns` come from the assertion names
(`Header…`/`Preamble…` → header; `Columns…`/`Resolves…` → columns) and
`footer` ∈ `ok` (live-DB parity passed) | `mismatch` (failed) | `unverified-nodb` (`dbAvailable=false` or test skipped) | `n/a` (manifest says no totals).
Every controller listed in your prompt MUST get a results entry, even if its tests did not run (then `passed:false`, `error:"not run: <why>"`).

## 4. Frontend (when your gate prompt says so)

```bash
cd Frontend && npm run build 2>&1 | tail -30 && npm run lint 2>&1 | tail -30 && npx vitest run 2>&1 | tail -40; cd ..
git status --porcelain Backend.Tests/Fixtures/ExcelSpecs
```
The fixture generator runs inside vitest; a non-empty `git status` under `Backend.Tests/Fixtures/ExcelSpecs`
means fixture drift → a failure `{ file: "Backend.Tests/Fixtures/ExcelSpecs", error: "fixtures drifted: <files>" }`.

**TRAP — rebuild after any fixture change.** `Backend.Tests.csproj` COPIES `Fixtures/**/*.json` into
`bin/Debug/net8.0/Fixtures/`, and `ExcelSpecContractTests` reads the copy. So if fixtures were regenerated (here, or by
a frontend/shared agent) you MUST run `dotnet build Backend.Tests/Backend.Tests.csproj` before any `--no-build` test
run, or the contract theory silently judges the OLD specs. A contract failure naming a dataIndex that the current
`reportConfigs.ts` no longer contains is this trap, not a real defect — rebuild and re-run before reporting it.
Confirm `index.json` has the expected number of entries when the prompt gives one.

## 4b. Known pre-existing failures (classify, never fix)

Load `docs/ExcelParity/known-failures.json`. A failure whose file path or error text matches any entry's
`match` strings is PRE-EXISTING: still list it in `failures[]`, prefix its `error` with `PRE-EXISTING(<id>): `,
and do **not** let it set `ok=false`. Everything else is real and blocking. Never edit a file to silence a
known entry, and never widen an entry. If a step cannot run at all because of a known entry (lint), say so in
`summary` and treat that step as neither pass nor fail.

`dbAvailable=false` is likewise not a failure: footer checks degrade to `unverified-nodb` and `summary` must
state that no footer parity claim can be made this cycle.

## 5. Result

```json
{ "ok": <buildOk && every listed controller passed && no NON-pre-existing failure in any step that ran>,
  "buildOk": ..., "dbAvailable": ..., "results": [...], "failures": [...],
  "summary": "<counts, durations, which filters ran, anything odd>" }
```
Do not edit files. Do not retry flaky tests more than once. Report the truth even if `ok` is false.

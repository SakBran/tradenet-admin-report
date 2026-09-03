# Gate: final (read `_preamble.md`, then `_gate-common.md`)

Purpose: the merge gate. Everything must be green at once.

Specifics:
- §1 full solution build.
- §3: run the whole `Backend.Tests` suite **minus the DB-bound suites** — i.e. `--filter` is exactly the §3a
  DB-skip clause set and nothing else — with a trx logger. Expect ~1538 tests in a few seconds; if the run takes
  minutes you forgot the filter, so kill it and re-run. Produce one `results` entry per controller in
  `docs/ExcelParity/manifest.json` (160). `footer` for controllers with totals is `unverified-nodb` on this machine
  (`ExcelFooterParityLiveDbTests` is skipped by §3a) — never report it as `ok` here, and say so in `summary`.
  Pre-existing unrelated test failures (not Excel-related) must be listed in `failures[]` with the test name so the
  lead can judge; they do not set `ok=false` by themselves — put `"preExistingFailures": [...]` in the summary text.
- §4 frontend: REQUIRED, including `git status --porcelain Backend.Tests/Fixtures/ExcelSpecs` empty.
- Also verify and report in `summary`: `grep -c "IExcelReportLayoutProvider" Backend/Controllers/Report/*.cs | grep -vc ':0'`
  (typed providers — expect AccountSummary, CompanyProfile, the 4 TotalValueLicences), and that no controller
  still lacks `[NonAction]` on `GetExcelLayout`/`GetExcelFooterTotalsAsync` (the contract test covers it; quote the result).
- `ok` = build green ∧ all Excel-related tests green ∧ every manifest controller `passed` ∧ frontend green ∧ no fixture drift.

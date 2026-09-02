# Gate: final (read `_preamble.md`, then `_gate-common.md`)

Purpose: the merge gate. Everything must be green at once.

Specifics:
- §1 full solution build.
- §3: run the **full** `Backend.Tests` suite (no filter) with a trx logger. Produce one `results` entry per
  controller in `docs/ExcelParity/manifest.json` (160). `footer` for controllers with totals: `ok` only when
  `ExcelFooterParityLiveDbTests` passed for them; `unverified-nodb` when the DB was unreachable — say so in `summary`.
  Pre-existing unrelated test failures (not Excel-related) must be listed in `failures[]` with the test name so the
  lead can judge; they do not set `ok=false` by themselves — put `"preExistingFailures": [...]` in the summary text.
- §4 frontend: REQUIRED, including `git status --porcelain Backend.Tests/Fixtures/ExcelSpecs` empty.
- Also verify and report in `summary`: `grep -c "IExcelReportLayoutProvider" Backend/Controllers/Report/*.cs | grep -vc ':0'`
  (typed providers — expect AccountSummary, CompanyProfile, the 4 TotalValueLicences), and that no controller
  still lacks `[NonAction]` on `GetExcelLayout`/`GetExcelFooterTotalsAsync` (the contract test covers it; quote the result).
- `ok` = build green ∧ all Excel-related tests green ∧ every manifest controller `passed` ∧ frontend green ∧ no fixture drift.

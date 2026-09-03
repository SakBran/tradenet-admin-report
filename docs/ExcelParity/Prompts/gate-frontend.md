# Gate: frontend (read `_preamble.md`, then `_gate-common.md` §4–§5)

Purpose: after the bespoke-page agents edited their pages, prove the frontend builds, lints, tests, and the
fixtures regenerate with no drift.

Steps:
```bash
cd Frontend && npm run build 2>&1 | tail -40 && npm run lint 2>&1 | tail -40 && npx vitest run 2>&1 | tail -60; cd ..
git status --porcelain Backend.Tests/Fixtures/ExcelSpecs
```
- Parse TypeScript/ESLint/vitest failures into `failures[{ file, error }]` (file = the reported source path).
- `results[]`: one entry per **page** in your prompt (`controller` field = the page name), `passed` = no failure
  mentions that page's file(s); `header`/`columns` = true when the page's fixture (bespoke builder) exists and the
  vitest parity test passed; `footer` = `n/a`.
- `buildOk` = `npm run build` passed; `dbAvailable` = false (not applicable; say so in summary).
- `ok` = build ∧ lint ∧ vitest ∧ no fixture drift.
Do not edit files.

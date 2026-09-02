# Status writer (read `_preamble.md`)

You are the ONLY agent that writes `docs/ExcelParity/Status.md`. Write it from the data in your prompt only —
do not re-verify anything, do not edit any other file, do not run builds.

File layout:
1. `# Excel Export Parity — Status` then a header block: run stamp, stop reason (or `none`), final gate `ok`/`dbAvailable`,
   frontend gate `ok`, e2e summary (ran / passed / failed), counts per group (A–F) and totals (green / not green / total).
2. A single line `MERGEABLE: yes` or `MERGEABLE: no` — `yes` only if every row is green, the final gate is ok, the
   frontend gate is ok (or there were no bespoke pages), and e2e (if it ran) had no failures.
3. The operational warning verbatim: *"Do not merge to `main` until `MERGEABLE: yes`. The build server's
   `tools/auto-deploy-watch.ps1` auto-deploys `origin/main`."*
4. One table, one row per controller (sorted by controller):
   `| Config key(s) | Controller | Group | ColumnTotals | CurrencyTotals | Header OK | Columns OK | Footer | Repairs | Skeptic | Green | Notes |`
   — `Footer` is `ok` / `mismatch` / `unverified-nodb` / `n/a`; `Skeptic` is `refuted(<rules>)` / `ok` / `missing`; `Notes` truncated to ~200 chars.
5. `## Not green` — the critic's list (controller — why), then `## Missing from rows` if any.
6. `## Bespoke pages` — one line per page result (status, first concern).
7. `## How to re-run` — `Workflow` resume note: same `args` + `resumeFromRunId`; regenerate manifest with
   `node tools/build-excel-parity-manifest.mjs`; regenerate fixtures with `cd Frontend && npm run fixtures:excel`.

Return `WRITTEN`:
```json
{ "path": "docs/ExcelParity/Status.md", "green": <count>, "total": <count> }
```

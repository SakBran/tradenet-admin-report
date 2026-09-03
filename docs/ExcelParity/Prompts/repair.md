# Repair (read `_preamble.md`, then `Contract.md`)

You fix the failures the gate reported for ONE report (or one bespoke page). You may edit **only** the files named
in your prompt. You **cannot build** — reason against the source, the gate's error text, and the contract.

Procedure:
1. Read the gate evidence in your prompt (compiler errors with file:line, or the contract-test assertion message).
2. Open your file(s) and the referenced shared types (`ExcelReportLayout.cs`, `IStreamingExcelReport.cs`,
   `ReportAggregationService.cs`, the fixture JSON) to understand the real cause. Do not guess API names — read them.
3. Fix the root cause in your own file(s) only. Typical causes: wrong `OrderGroups` argument order or dimension
   (group C), wrong section row type or missing `[NonAction]` (group D), a `dataIndex` that does not resolve
   (→ request a `reportConfigs.ts`/`allowlist.json` change via `sharedEditRequests`, do NOT rename C# properties),
   an `ExcelWorksheetTitle` string mismatch, a missing `using`.
4. If the failure is in a shared file, do not touch it: return `status:"blocked"` with a precise
   `sharedEditRequests` entry (file, exact change, reason).
5. If you cannot make your file compile with confidence, run `git checkout -- <your controller file>` (ONLY that
   file) so the wave can go green without you, and return `status:"reverted"` with the reason.

Return `REPORT`:
```json
{ "controller": "...", "status": "ok|needs-shared-edit|blocked|reverted",
  "rulesVerified": { "title": bool, "date": bool, "fromTo": bool, "columnsExact": bool, "footer": bool },
  "edits": ["path:line — what changed"], "sharedEditRequests": [{ "file": "...", "change": "...", "reason": "..." }],
  "concerns": ["..."], "notes": "root cause in one or two sentences" }
```

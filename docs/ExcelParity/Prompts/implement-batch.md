# Implement — a BATCH of controllers (read `_preamble.md`, then `Contract.md`)

You own several report controllers from the same family. Read the shared docs **once**, then work each
controller in the order listed. You may edit ONLY the controller files listed in your prompt (plus, for
`kind: composite`, a new `Backend.Tests/ExcelParity/<Controller>LayoutTests.cs` per controller).
Do NOT run `dotnet build`/`dotnet test`/`npm` — one gate agent builds later.

Read once, up front: `docs/ExcelParity/Contract.md` (§2 sheet shape, §4 cell rules, §5 footer rules, §9 recipes),
`docs/ExcelParity/Prompts/footer-check.md`, and your controllers' entries in `docs/ExcelParity/manifest.json`.
Sibling controllers in a batch usually share one `sp_*` file and one row DTO — resolve that DTO once and reuse it.

## Per controller, by `kind`

**`kind: verify`** (groups A/B/E/F — the generic spec path already serves them):
1. Identify the row type `WriteRowsAsync` appends (A: `ExecuteQueryable`/`Query` element, often after `ToResult()`;
   B: `ChunkAsync`/`StreamResolvedChunksAsync` chunk element; E: `GetLicenceListRowsAsync`; F: the `SummaryRowAsync` row).
   Confirm it equals `T` in `Post`'s `Task<ActionResult<ApiResult<T>>>`; if not, that is a `blocked` finding.
2. For every fixture in the manifest's `fixtureFiles`, resolve each column's `dataIndex` (then its
   `fallbackDataIndexes`) against that row type's public properties using camelCase (`NRCNo→nrcNo`, `HSCode→hsCode`).
3. If `titleMismatch`, set `ExcelWorksheetTitle` to the config title verbatim — unless the RDLC and controller
   agree and the config looks wrong, in which case request the config change instead.
4. Run `footer-check.md` when the manifest says the report has totals.
5. Unresolvable column → `sharedEditRequests` (an `allowlist.json` entry for a deliberately unbound column, or the
   exact `reportConfigs.ts` `dataIndex` fix). Never rename a C# property. Never edit a shared file yourself.

**`kind: aggregate`** (group C): as above, plus the one required edit — order the groups exactly as the JSON path
does, using the real signature in `Backend/Service/Reports/ReportAggregationService.cs`:
`sink.Append(ReportAggregationService.OrderGroups(rows, ReportAggregateDimension.<X>, includeSakhan: <b>));`
Copy `<X>` and `<b>` from that same controller's `Post`. Do not change `Post`. Do not add a typed layout.

**`kind: composite`** (the 4 `*TotalValueLicencesReport`): follow `implement-D.md` for each. They are near-identical
siblings, so derive the layout once and apply it four times, adjusting only the family's heading label and the
`sp_*_Fast` helper each one's `Post` already calls.

## Already-correct controllers

Some controllers in your batch may already carry the change (earlier runs edited 12 of them). If a controller
already satisfies its recipe, change nothing and report it with `"status":"already-done"` — do not restyle or
re-edit working code.

## Return

```json
{ "batchId": "<id from your prompt>",
  "reports": [ { "controller": "...", "status": "ok|already-done|needs-shared-edit|blocked",
                 "rulesVerified": { "title": bool, "date": bool, "fromTo": bool, "columnsExact": bool, "footer": bool },
                 "edits": ["path:line — what"], "notes": "row type; unresolved columns; footer decision" } ],
  "sharedEditRequests": [ { "file": "...", "change": "...", "reason": "..." } ],
  "concerns": ["..."] }
```
One `reports` entry per controller in your prompt — no more, no fewer.

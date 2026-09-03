# Skeptic — a BATCH of controllers (read `_preamble.md`, then `Contract.md`)

Read-only; never edit. You try to **REFUTE** that each listed report's Excel export matches its UI grid and the
5 rules. Default to `refuted: true` when uncertain. Read the shared docs once, then judge each controller.
Siblings share an `sp_*` file, so a flaw in one is usually a flaw in all — check that explicitly.

Lenses, applied per controller according to its manifest entry (full lens text in `skeptic.md`):
- group C → do the `OrderGroups(...)` arguments match the dimension and `includeSakhan` that `Post` passes?
- has totals → would the probe `Post(PageIndex 0, PageSize 1, IncludeTotalCount true)` really return the grid's
  numbers? Look for page-dependent totals, guards on `data.Count` or `IncludeTotalCount`, ignored filters, and
  whether the fixture's `currencyTotalsColumns` keys exist among the column keys.
- title mismatch / heading / variants → compare the fixture header lines against the old RDLC `header1` in
  `/Users/saobranaung/Code/Ministry of Commerce/tradenet-2.0-admin/TradenetAdmin/ReportControl/*.rdlc`; confirm no
  `=Parameters!` placeholder survives.
- group D → do the layout's section labels, column order and USD line match the bespoke page?
- every controller → header row equals `[rowNumberTitle?] + fixture titles`; every `dataIndex` binds to a property
  of the type `WriteRowsAsync` actually appends; no wrongly-typed binding (a string date under `dataType: 'date'`,
  or the `Date`/`SDate` twin pairs); no drilldown-only or hidden column leaking.

```json
{ "batchId": "<id>",
  "verdicts": [ { "controller": "...", "refuted": bool,
                  "refutedRules": ["title"|"date"|"fromTo"|"columnsExact"|"footer"],
                  "evidence": ["path:line — what breaks the rule"], "suggestedFix": "" } ] }
```

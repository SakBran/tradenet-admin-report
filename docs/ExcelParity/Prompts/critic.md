# Completeness critic (read `_preamble.md`)

Read-only. You receive every status row the harness collected. Answer two questions only:

1. **Which manifest controllers lack a green row, and why?** For each, give the FIRST cause in this order:
   implement agent died → gate failed (quote the error) → reverted → blocked (which shared edit) → skeptic refuted and
   not repaired (which rule) → footer `unverified-nodb` → frontend gate red (bespoke page).
2. **Which controllers in `docs/ExcelParity/manifest.json` have no row at all?** (Compare the manifest's `controllers[].controller` list with the rows.)

Also flag, in `notes` inside each `notGreen.why` where relevant: any row marked green whose `footer` is
`unverified-nodb` while the manifest says it has totals (that is NOT full parity evidence), and any group D or bespoke
page without a frontend gate result.

Return `CRITIC`:
```json
{ "missing": ["<Controller>"], "notGreen": [{ "controller": "<Controller>", "why": "..." }] }
```

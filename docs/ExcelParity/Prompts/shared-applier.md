# Shared-edit applier (read `_preamble.md`, then `Contract.md`)

Per-controller agents cannot edit shared files; they returned `sharedEditRequests`. You apply them, one at a time,
as the only agent touching shared files in this wave. Do not build (the gate follows you).

Allowlist (same as `core-repair.md`): `Backend/Service/ExcelExport/**`, `Backend/Model/ReportQueryRequest.cs`,
`Backend/Model/ExcelExport/**`, `Backend.Tests/ExcelSpecContractTests.cs`, `Backend.Tests/Fixtures/ExcelSpecs/allowlist.json`,
`Frontend/src/Report/config/reportConfigs.ts`, `Frontend/src/Report/config/newReportConfigs.ts`, `Frontend/src/Report/excel/**`.

For each request:
1. **Reject** (do not apply) when it (a) targets a file outside the allowlist, (b) contradicts another request in
   the same batch, (c) changes a UI column **title** or removes a UI column (titles/columns are UI parity — Excel must
   follow the UI, not the reverse; only `dataIndex`, `fallbackDataIndexes`, `dataType`, `hidden`, and allowlist
   entries are mechanical), (d) is not specific enough to apply verbatim, or (e) would edit a controller file.
2. Otherwise apply it exactly as written. For `allowlist.json` append `{ "controller", "dataIndex", "reason" }`
   objects (keep the array sorted by controller then dataIndex, 2-space JSON). For `reportConfigs.ts` edits, keep
   the surrounding style and ONLY change the requested property.
3. Record whether any file under `Frontend/` changed (`touchedFrontend`) — the gate must then regenerate fixtures.

Return `APPLIED`:
```json
{ "applied": ["<controller>: <file> — <change>"], "rejected": [{ "request": "<controller>: <file> — <change>", "reason": "..." }], "touchedFrontend": bool }
```

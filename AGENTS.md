# Agent Instructions (Claude + Codex)

These instructions apply to all AI agents working in this repo.

## Trigger: "customer complaint" → Report Parity Check vs Tradenet 2.0

When the user reports a **customer complaint** about a report (any phrasing like
"a customer complained about report X", "complaints like that", "customer says
report Y is wrong"), do **not** just patch the visible symptom. First compare the
new report against the **old Tradenet 2.0 Admin** code and confirm parity on the
two axes below, then report the diffs before changing anything.

### Where the code lives

Old (source of truth): `/Users/saobranaung/Code/Ministry of Commerce/tradenet-2.0-admin/TradenetAdmin`
- Table columns: `ReportControl/*.rdlc` — the visible RDLC table headers are the
  column source of truth.
- Filter box: the old report's filter/search form (the ASPX page / view that
  renders the parameters for that report).

New:
- Columns + filters config: `Frontend/src/Report/config/reportConfigs.ts`
- Table rendering (incl. the conditional `No` column): `Frontend/src/components/My Components/Table/BasicTable.tsx`

### Checklist for the complained-about report

1. **Filter box** — confirm the new filter box has the **same filters** as the old
   code, and that each filter offers the **same options/values** (dropdown values,
   date ranges, etc.). Note: BusinessType dropdowns must filter `FormType='Pa Tha Ka'`.
2. **Table columns** — confirm the **same set of columns**, the **same header text**,
   and the **same language** (English vs Myanmar) as the old RDLC report.
3. Report every diff — missing / extra / renamed columns, wrong or missing filter
   values — **before** making changes.

### Reference

`ReportColumnComparison.md` already tracks column parity for ~134 reports against
the old RDLC sources. Cross-check / extend it (and verify filters) when handling a
complaint.

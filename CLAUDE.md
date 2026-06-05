# Claude Instructions

See [AGENTS.md](AGENTS.md) for the full shared agent instructions. Key workflow:

## Trigger: "customer complaint" → Report Parity Check vs Tradenet 2.0

When the user reports a **customer complaint** about a report (any phrasing like
"a customer complained about report X" or "complaints like that"), compare the new
report against the **old Tradenet 2.0 Admin** code before patching anything:

- Old code (source of truth): `/Users/saobranaung/Code/Ministry of Commerce/tradenet-2.0-admin/TradenetAdmin`
  (table columns = `ReportControl/*.rdlc` headers; filter box = the old report's filter/search form)
- New code: `Frontend/src/Report/config/reportConfigs.ts` + `Frontend/src/components/My Components/Table/BasicTable.tsx`

Confirm parity on two axes, then report diffs before changing anything:
1. **Filter box** — same filters AND same option/values as old code
   (BusinessType dropdowns must filter `FormType='Pa Tha Ka'`).
2. **Table columns** — same columns, same header text, same language (English/Myanmar) as the old RDLC.

Reference: `ReportColumnComparison.md` tracks column parity for ~134 reports.

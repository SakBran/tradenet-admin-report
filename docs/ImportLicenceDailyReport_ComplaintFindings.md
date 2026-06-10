# Customer Complaint Findings — Import Licence Daily Report (New Licence Report)

> **Status: RESEARCH / DOCUMENTATION ONLY — no code changed.**
> Report Parity Check (per `CLAUDE.md`) of the new report against the Tradenet 2.0 admin
> source of truth and the database, to explain the 3 customer complaints.

## 1. Report identity

| | |
|---|---|
| **Report name (sheet)** | Import Licence Daily Report  (New Licence Report) |
| **Stored procedure** | `dbo.sp_ImportLicenceDetailReport` (Oversea branch) / `sp_ImportLicenceDetailReport_pagination` |
| **New config key** | `ImportLicenceDailyReportNewLicenceReport` — `Frontend/src/Report/config/reportConfigs.ts:9940` |
| **New controller** | `Backend/Controllers/Report/ImportLicenceDailyReportNewLicenceReportController.cs` |
| **Old action (source of truth)** | `ReportsController.ImportLicenceByDailyReport` (GET `:4637`, POST `:4670`) → `ReportControl/ImportLicenceByDailyReport.rdlc` |
| **Old filter form** | `Views/Reports/ImportLicenceByDailyReport.cshtml` |
| **Section setup table** | `dbo.ExportImportSection` |

> ⚠️ **Naming caveat to confirm with the team.** The sheet title conflates two old reports.
> `ImportLicenceByDailyReport.rdlc` is the **date/currency summary** (the layout the new report
> follows — Date / No of Licences / Total Value / Currency / Total USD Value). `NewLicenceReport.rdlc`
> is a **separate per-licence listing** used by the old `ImportLicenceNewReport` action (No. / Section /
> Licence No / Company Reg No / Company Name / Company Address / Currency / Total Value). The new report
> currently matches the **Daily summary** RDLC. If the customer actually expects the per-licence
> "New Licence" listing, that is a different (larger) change — clarify before fixing.

## 2. Source of the complaint

From the shared Google Sheet *“Stored Procedure Tradenet 2.0”* (owner: htetlintun@gmail.com),
row **Import Licence → “Import Licence Daily Report (New Licence Report)”**, feedback tab content
(Burmese, translated):

| # | Complaint (Burmese) | Translation |
|---|---|---|
| 1 | Total ပေါင်းထည့်ပေးရန်။ | Add a **Total** summary (sum). |
| 2 | Import Section dropdown တွင် 4 တစ်ခုပဲ Export/Import Sections setup တွင်ထည့်ထားပါတယ်။ သို့သော် 1,2,3 စသည်ဖြင့် dropdown တွင်ပြနေပါတယ်။ | Only **4 import sections** are configured in the Export/Import Sections setup, but the Import Section dropdown is showing **1, 2, 3 …** (wrong/extra values). |
| 3 | Sakhan dropdown မထည့်ပါနဲ့။ | **Remove the Sakhan dropdown.** |

> 🖼️ **Images note.** The sheet has screenshots embedded *inside the cells*. Embedded
> Google-Sheet images are not stored as separate Drive files and cannot be pulled through the
> Drive API/tools available here (a Drive image search returned nothing). The findings below rely on
> the explicit text feedback cross-checked against code + DB. If the screenshots show anything beyond
> the three text points, please export them as image files (or paste them) and I’ll re-check.

## 3. Parity check summary (the two axes from CLAUDE.md)

| Axis | Verdict |
|---|---|
| **Table columns** | ✅ **Match.** Old RDLC headers = New config columns = `Date`, `No of Licences`, `Total Value`, `Currency`, `Total USD Value` — same text, same order, all English. `ReportColumnComparison.md:866` confirms Need=None, Extra=None. *(One data gap, not a header gap — see complaint #1.)* |
| **Filter box** | ⚠️ Mostly matches. Old form = From Date, To Date, Import Section, EIR Card Type (PaThaKaType), Company Reg No, Company Name. New = Date range, Type, Import Section, PaThaKa Type, Company Reg No, Company Name. No Method/Incoterm/Seller-Country/Sakhan on either side. The only live issue is **which lookup feeds the Import Section dropdown** (complaint #2). |

---

## 4. Complaint-by-complaint findings

### Complaint #1 — Missing Total summary  →  REAL GAP (fix needed, backend)

**Old behaviour (source of truth).** `ImportLicenceByDailyReport.rdlc` groups by `(sLicenceDate, Currency)`
and renders a grand **TOTAL** footer row:
- `ImportLicenceByDailyReport.rdlc:985` literal `TOTAL`
- `:1039` `=CountDistinct(Fields!LicenceNo.Value)` → total number of licences
- `:1201` `=FORMAT(Sum(Fields!totalUSDAmount.Value),"N4")` → total USD value

**New behaviour.** The aggregate pipeline emits **per-(Date, Currency) rows only — no grand-total row.**
- `Backend/Service/Reports/ReportAggregationService.cs:88-124` — groups by Date+Currency,
  `NoOfLicences` = distinct count, `TotalValue` = `Sum(Amount)`, and `TotalUSDValue = null` (intentional,
  comment at `:70-75` — FX conversion not derivable in this repo).
- The Daily controller `ImportLicenceDailyReportNewLicenceReportController.cs:40-41` calls
  `CreateAggregateResultAsync(..., ReportAggregateDimension.Daily, includeSakhan: false)` and **never sets
  `ColumnTotals`.**

**How the new UI renders totals.** `BasicTable` already supports a bold footer `Total` row, driven entirely
by a backend-supplied `data.columnTotals` map keyed by column `dataIndex`:
- `Frontend/src/components/My Components/Table/BasicTable.tsx:289-302` (gate) and `:456-483` (`<tfoot class="report-total-row">`).
- The API envelope already exposes the channel: `Backend/Model/APIResult.cs:400`
  `public IReadOnlyDictionary<string, decimal>? ColumnTotals { get; set; }`.

**Proposed fix (NOT applied).** Backend only — no frontend config change.
Populate `result.ColumnTotals` in the Daily controller keyed by the serialized column `dataIndex`
(`noOfLicences`, `totalValue`, and `totalUSDValue` once/if USD is computed).
There is an exact in-repo precedent to copy:
`Backend/Controllers/Report/RegistrationByBusinessTypeController.cs:69`
```csharp
result.ColumnTotals = new Dictionary<string, decimal>
{
    ["companyCount"] = grandTotal,
};
```
Once set, `BasicTable` auto-renders the `Total` row.

> **Tell the customer:** a faithful currency-wise total of **No. of Licences** and **Total Value** (the
> values the new SP actually produces) is achievable now. Matching the old **Total USD Value** sum
> additionally requires the FX conversion the new backend does not yet compute (`TotalUSDValue` is
> currently `null`, so that column also renders empty in data rows — a related, separate gap).

---

### Complaint #2 — Import Section dropdown shows 1, 2, 3 …  →  ALREADY FIXED IN SOURCE (verify deployed build)

**Root cause (verified).** There are **two** section lookups in the backend:

| Lookup | Filter | Result |
|---|---|---|
| `GetExportImportSections` (key `exportImportSections`) — `ReportLookupsController.cs:185-192` | `IsActive && !IsDeleted` only — **no Type, no IsOversea** | Returns **all** sections (Import + Export, Oversea + Border) → the leak / wrong values |
| `GetImportLicenceSections` (key `importLicenceSections`) — `ReportLookupsController.cs:220-231` | `IsActive && !IsDeleted && Type == "Import Licence" && IsOversea` | Returns exactly the configured **Oversea Import** sections |

The dropdown renders the section **Name** as label and submits the **Id** (never a raw number) when fed
correct data — `GenericReportPage.tsx:398-400`. So “1, 2, 3” appears only when the dropdown is fed the
**generic leaky `exportImportSections`** lookup (Export rows / ordinal-looking names leaking in).

**Why it is already fixed in current source.** The filter config explicitly pins the correct lookup:
- `reportConfigs.ts:100-106` — `importLicenceSectionFilter` sets `lookupName: 'importLicenceSections'`.
- `GenericReportPage.tsx:383-390` — `resolveLookup` returns `filter.lookupName` **first**; the leaky
  fallback `idFilterLookups['ExportImportSectionId'] → 'exportImportSections'` (`:90-91`) is used **only when
  no explicit `lookupName` is set**.

The explicit lookup wiring + the import-only `GetImportLicenceSections` were added in commit
**`643afe3` “feat: Enhance Import Licence Reports UI and Configuration”** (touched both
`ReportLookupsController.cs` and `reportConfigs.ts`).

**Action (NOT applied).** No code change needed for this report — **confirm the deployed/UAT build
includes commit `643afe3`** and rebuild/redeploy. If the customer still sees “1, 2, 3”, it is a
**pre-fix / stale build**, not the current source.

> ✅ **Important correction — do NOT “fix” the Type literal.** The investigation initially flagged that
> Sections filter by `Type == "Import Licence"` while Methods/Incoterms filter by `Type == "Import"`
> (`ReportLookupsController.cs:28-29`). This is **intentional and matches the old admin**, where Sections
> used `AppConfig.ImportLicence = "Import Licence"` (`ReportsController.cs:4650/4722` →
> `GetAll(AppConfig.ImportLicence)`) and Methods used `AppConfig.Import = "Import"`. Changing
> `GetImportLicenceSections` to `"Import"` would **break** it. (See §6 for the one DB sanity check that
> would give 100% confirmation.)

---

### Complaint #3 — Remove Sakhan dropdown  →  ALREADY CORRECT IN SOURCE (no UI Sakhan present)

**Old behaviour.** The old Daily form has **no Sakhan/Office dropdown**
(`Views/Reports/ImportLicenceByDailyReport.cshtml` — only FromDate, ToDate, Import Section,
EIR Card Type, Company Reg No, Company Name). Old Sakhan/“Office” dropdown is set **only for the four
`Border …` report types**. Sakhan = border station — irrelevant to this Oversea (non-border) report.

**New behaviour.** The new Daily filter list already omits Sakhan:
- `reportConfigs.ts:266-273` — `importLicenceDailyFilters` = `[dateRange, OverseaType, Section, PaThaKaType,
  CompanyRegistrationNo, CompanyName]` — **no `SakhanId`**. `SakhanId` filters appear only on `Border*`
  configs.

**Harmless residue (optional cleanup, NOT a bug).** The controller still hardcodes `Type = "Oversea"` and
copies `request.SakhanId` into the SP request (`ImportLicenceDailyReportNewLicenceReportController.cs:116,125`;
DTO field `:143`), with `includeSakhan: false` (`:41`). The Oversea SP/aggregate path has **no `SakhanId`
predicate** (`sp_ImportLicenceDetailReport_pagination.sql` WHERE block has none), so the parameter is dead.

**Action (NOT applied).** No functional change needed — if a Sakhan dropdown is visibly appearing for the
customer, it is again a **stale pre-fix build**. Optionally drop the unused `SakhanId` from the request DTO
for cleanliness.

---

## 5. What actually needs to change (proposals only)

| # | Complaint | Verdict | Where the fix lives | Action |
|---|---|---|---|---|
| 1 | Add Total | **Real gap** | Backend controller / aggregation | Set `result.ColumnTotals` for `noOfLicences` + `totalValue` (USD later). Pattern: `RegistrationByBusinessTypeController.cs:69`. |
| 2 | Section dropdown 1,2,3 | **Already fixed in source** (commit `643afe3`) | `reportConfigs.ts:100` + `ReportLookupsController.cs:220` | Verify deployed build, rebuild/redeploy. Do **not** change the `"Import Licence"` Type literal. |
| 3 | Remove Sakhan | **Already correct in source** | `reportConfigs.ts:266` | Verify deployed build. Optional: drop dead `SakhanId` from DTO. |

**Net:** the only genuine code change is **complaint #1 (add the Total footer row)**. #2 and #3 look like a
**stale deployed build** predating commit `643afe3` — please confirm the running build before any patch.

## 6. Open items / recommended verification

1. **DB sanity check** for complaint #2 (gives 100% confirmation, not required to act):
   ```sql
   SELECT Type, IsOversea, IsBorder, IsActive, IsDeleted, COUNT(*)
   FROM ExportImportSection
   GROUP BY Type, IsOversea, IsBorder, IsActive, IsDeleted;
   -- The non-border Import dropdown should be exactly:
   SELECT Id, Code, Name FROM ExportImportSection
   WHERE IsActive = 1 AND IsDeleted = 0 AND Type = 'Import Licence' AND IsOversea = 1
   ORDER BY SortOrder, Name;   -- expected: the 4 sections the client describes
   ```
   This confirms the section `Type` column stores `'Import Licence'` (not `'Import'`).
2. **Confirm deployed build** includes commit `643afe3` (the section + Sakhan fixes are source-side).
3. **Clarify the report layout** the customer expects (Daily summary RDLC vs. the per-licence
   `NewLicenceReport.rdlc` listing) — see the naming caveat in §1.
4. **`Total USD Value`** column currently renders empty (backend leaves `TotalUSDValue = null`); decide
   whether the FX conversion must be implemented to match the old RDLC’s `Sum(totalUSDAmount)`.

## 7. Group-wide note

The same two root causes recur across the whole **Import Licence** report group in the sheet:
- *“Import Section/Method/Incoterms dropdown တွင် Export တွေပါ ပါနေပါတယ်”* (Export leaking into Import dropdowns)
  → same generic-vs-import-only lookup issue; the import-only resolvers (`GetImportLicence{Sections,Methods,Incoterms}`)
  already exist in `ReportLookupsController.cs` and just need each report’s filter config to pin the right
  `lookupName`.
- *“Sakhan dropdown မပါရပါ”* (Sakhan should not appear) on every non-border report.
- *“Total ပေါင်းပြပေးရန်”* (add currency-wise totals) on the summary/value reports → `ColumnTotals` pattern.

Worth a single sweep across the Import (and Export) Licence/Permit configs rather than per-report patches.

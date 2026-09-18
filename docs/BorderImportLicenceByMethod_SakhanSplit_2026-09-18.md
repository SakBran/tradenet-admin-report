# Border Import Licence By Method — same Method + same Currency split across rows

Date: 2026-09-18
Complaint: "Border Import Licence By Method Report မှာ Method တူနေတာကိုမှ currency တူတာတွေ
ပေါင်းဖော်ပြရမှာကို ခွဲပြီး ပြနေတယ်" — one `Normal TT` / `THB` row was showing as four.

## Root cause

`BorderImportLicenceByMethodReportController` passed `includeSakhan: true`, which adds
`SakhanCode` to the grouping key
(`Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs:638`) and a
`.ThenBy(SakhanCode)` tie-break (`Backend/Service/Reports/ReportAggregationService.cs:404-407`).
The grid has no Sakhan column, so the split was invisible: one row per border office,
interleaved by an unseen code.

The old report groups on **two** keys only —
`ReportControl/BorderImportLicenceByMethodReport.rdlc:1078-1079`:

```xml
<GroupExpression>=Fields!MethodName.Value</GroupExpression>
<GroupExpression>=Fields!Currency.Value</GroupExpression>
```

Sakhan is a *filter* on the old search form
(`Views/Reports/BorderImportLicenceByMethodReport.cshtml`), never a group key, and the tablix
has 5 columns (`rdlc:281-556`: Sr.No., Method, No of Licences, Total Value, Currency) — no
Sakhan. The old controller aggregates nothing; it hands raw detail rows to the RDLC
(`Controllers/ReportsController.cs:11570-11596`).

### Where the flag came from

The **only** old Border RDLC that really groups by Sakhan is
`BorderExportLicenceByDailyReport.rdlc:1445-1447` (Date, Currency, **SakhanId**) — and that
report does render a Sakhan column. `includeSakhan: true` was copied from it onto reports
whose RDLCs group without it:

| Old RDLC | group keys | Sakhan column? | new flag | status |
|---|---|---|---|---|
| BorderImportLicenceByMethodReport.rdlc:1078 | Method, Currency | no | was `true` | **fixed here** |
| BorderImportLicenceByDailyReport.rdlc:1270 | Date, Currency | no | `true` | same bug, **deferred by owner** |
| BorderImportPermitByDailyReport.rdlc:1269 | Date, Currency | no | `true` | same bug, **deferred by owner** |
| BorderExportPermitByDailyReport.rdlc:1277 | Date, Currency | no | `true` | same bug, **deferred by owner** |
| BorderExportLicenceByDailyReport.rdlc:1445 | Date, Currency, SakhanId | yes | `true` | correct, leave |
| BorderExportLicenceByMethodReport.rdlc:1240 | Method, Currency | yes (arbitrary row) | `true` | legacy quirk, owner said leave |

## Measured on PROD before the fix

`POST https://reportapi.myanmartradenet.com/api/BorderImportLicenceByMethodReport`,
01/09/2026–15/09/2026, all filters blank (2026-09-18):

12 rows, every duplicate a different border office — KTH, MWD, MYI, TCL, YGN. The payload's
`methodId` is **1:1 with `methodName`** (CMP=15, Normal LC OR TT=13, Normal TT=12), so the
`LabelId = row.ExportImportMethodId` that also sits in the group key is **not** a second
splitter and was left alone.

Expected after the fix — 5 rows, ordered by Method then Currency:

| Method | No of Licences | Total Value | Currency |
|---|---:|---:|---|
| CMP | 103 | 13,147,976.6689 | USD |
| Normal LC OR TT | 1 | 242,355.0000 | USD |
| Normal TT | 5 | 1,748,768.6000 | CNY |
| Normal TT | 168 | 263,238,426.6161 | THB |
| Normal TT | 24 | 3,690,075.5300 | USD |
| **TOTAL** | **301** | *(blank)* | |

## Changes

`Backend/Controllers/Report/BorderImportLicenceByMethodReportController.cs`

1. `includeSakhan: false` on all three surfaces — `Post`, `GetAggregateRowsAsync` and
   `OrderGroups` — so the Excel export keeps matching the grid.
2. `columnTotalsMode: ReportColumnTotalsMode.CountOnly`. The old TOTAL row prints
   `=CountDistinct(Fields!LicenceNo.Value)` under *No of Licences* (`rdlc:905`) and leaves the
   *Total Value* and *Currency* cells empty (Textbox7/Textbox8); the grid was showing
   282,067,602.4150, a sum of THB + USD + CNY. Same call as the 2026-09-10 Oversea round.
   `IExcelNoFooterReport` deliberately **not** added — it would hide the surviving count from
   the sheet.
3. `[ExcelFormatVersion(2)]` added (the class had none, so it defaulted to 1). Without it the
   export queue keeps serving the cached pre-fix `.xlsx`.

Footer count is unchanged at 301: a licence belongs to exactly one Sakhan, so the per-group
distinct counts never overlapped.

No frontend change — the config already has the 4 RDLC columns and no Sakhan column.

`Backend.Tests/BorderImportLicenceByMethodLegacyParityTests.cs` (new) pins all of it,
including a replay of the 12 measured PROD rows collapsing to the 5 rows above with a
count-only footer.

## Still to verify after deploy

The DB is not reachable from the dev Mac (`pymssql` and the EF tests both time out on
`tn2db.myanmartradenet.com,14133`), so the end-to-end run has to happen on the deployed build:
re-run the same window and confirm 5 rows, the figures above, a blank footer Total Value, a
freshly generated (not cached) Excel file, and that the Method drill-through still opens the
Detail report in a new tab across all offices.

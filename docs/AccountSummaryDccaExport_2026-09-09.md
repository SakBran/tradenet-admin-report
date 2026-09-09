# Account Summary Report — DCCA export (second Excel button)

Date: 2026-09-09

## Request

Customer (translated):

> In the **OldReport → AccountSummary** screen, after searching, pressing the **black Export
> button** produces the format used to **import into DCCA**.
> In the new Report's AccountSummary report, the Excel that the UI and the export show is
> fine. Please also provide **one more Excel format so it can be imported into DCCA**.

Not a parity defect — the customer says the current sheet is correct. This is a **new feature**:
a second export button beside the existing one.

## What the old system did

The old Account Summary screen produced **two different files**:

1. **The on-screen report + its ReportViewer toolbar export** — `ReportControl/AccountSummaryReport.rdlc`,
   8 columns (`No`, `Entry Date`, `Company Registration No`, `Company Name`, `Voucher No`,
   `Transaction Title`, `Deducted Fees`, `Remark`). **The new report already reproduces this.**
2. **The black `btn-dark` "Export" button** — the DCCA import file, 9 columns. This is what was
   missing.

The DCCA file is *not* rendered from the RDLC. `ReportsController.AccountSummaryReport` (POST,
`origin/master`, `Controllers/ReportsController.cs:16359-16461`) writes it as a **side effect of
the search**, using **EPPlus 4.1** to fill a pre-made template workbook
(`Content/excel-template/TransactionFees.xlsx`) and saving to the fixed, shared path
`~/uploads/AccountSummary.xlsx`. The view then renders the black button as a plain `<a href>`
pointing at that file, only once `Model.FileName != null` (i.e. after a search):

```razor
@if (Model.FileName != null)
{
    <a href="@Model.FileName" class="btn btn-dark" id="btnExport"> … Export</a>
}
```

The cell writes, verbatim:

```csharp
int row = 2;                 // data starts at row 3; row 2 stays blank
foreach (var item in lstData)
{
    row++; srno++;
    ws.Cells[row, 1].Value = srno;
    ws.Cells[row, 2].Value = item.VoucherDate.ToString("MM/dd/yyyy");
    ws.Cells[row, 3].Value = item.CompanyRegistrationNo + "@" + item.VoucherNo;
    ws.Cells[row, 4].Value = item.CompanyName;
    ws.Cells[row, 5].Value = item.TransactionTitle;
    ws.Cells[row, 6].Value = item.Amount;
    ws.Cells[row, 7].Value = "";
    ws.Cells[row, 8].Value = item.AccountTitleCode;
    ws.Cells[row, 9].Value = item.LocationCode;
}
```

The **headers are never written by code** — they come from the template's row 1.

## Column mapping

| Col | Header (verbatim from the template) | Source | Cell type |
|---|---|---|---|
| A | `No` | running row number | number |
| B | `Entry Date` | `VoucherDate.ToString("MM/dd/yyyy")` | **text** (template col B is numFmt 49) |
| C | `HtaThaKa No` | `CompanyRegistrationNo + "@" + VoucherNo` | text |
| D | `Company Name` | `CompanyName` | text |
| E | `Transation Title` **(sic — one "c")** | `TransactionTitle` | text |
| F | `Deducted Fees` | `Amount` | number, General (no separator) |
| G | `Remark` | literal `""` | text |
| H | `Account Code` | `AccountTitleCode` = `AccountTitle.Code` (a setup master, not constants) | text |
| I | `Location Code` | `LocationCode` — `'NPT'`, or `Sakhan.Code` on border branches | text |

**No SQL or stored-procedure change was needed.** `sp_AccountSummaryReportResult` already carries
`AccountTitleCode` and `LocationCode` (`Backend/StoredProcedureToLinq/sp_AccountSummaryReport.cs:19-34`),
and `sp_AccountSummaryReport_pagination.sql` already selects them (lines 80, 97). They were simply
never rendered.

## Two claims that were checked, and one that was wrong

**`HtaThaKa No` is `registration@voucher`, not the reverse.** Confirmed twice: from the old
controller source, and from the data — the customer's sample shows `@U03202600003` with a blank
Company Name, and the `Member` branch is the only one that hardcodes *both*
`CompanyRegistrationNo = N''` and `CompanyName = N''` (`sp_AccountSummaryReport_pagination.sql:100-104`).

**The old report did NOT drop border rows when Sakhan = "All".** An initial reading of the old
proc's final filter claimed it did. It does not:

```sql
AND tmp.SakhanId = (CASE WHEN @SakhanId='' THEN tmp.SakhanId ELSE @SakhanId END)
```

`@SakhanId` is `int`, so `@SakhanId=''` compares as `@SakhanId = 0`. With "All" that is **true**,
so the CASE yields `tmp.SakhanId` and the predicate `tmp.SakhanId = tmp.SakhanId` matches every
row. The new proc reaches the same result by a different route (`(@SakhanId = 0)` on the NPT
branches, `(@SakhanId = 0 OR Sakhan.Id = @SakhanId)` on the border branches). **The row sets
already agree; no Sakhan special-casing was added, and adding one would have made the DCCA file
differ from the old one.** The shipped sample being all-`NPT` just reflects a day with no border
payments.

## Implementation

One controller, one report key, **a variant flag on the request DTO**. Handler keys are derived
from the controller class name and there is exactly one `GetExcelLayout` per controller, so the
selector has to travel on the request — which also gives the two formats different export dedup
hashes (`ExcelExportHasher`), without which the second button would be served the first one's
cached file.

### Shared Excel plumbing (inert unless a layout opts in)

- `ExcelReportLayout.SuppressStandardHeaderBlock` — skips the shared title/From-To/Exported
  preamble. Honoured at the top of `ExcelLayoutBuilder.WithStandardHeaderBlock`.
- `ExcelReportLayout.BlankRowsAfterHeader` — spacer rows between the header row and the first
  data row, re-emitted on every rolled-over sheet.
- `ExcelReportLayout.WorksheetTitle` — overrides the controller's `ExcelWorksheetTitle`, honoured
  in `ControllerStreamingExcelReportJobHandler`.

A regression test asserts that a layout leaving all three at their defaults writes **byte-identical**
output, so the other ~160 reports are unaffected.

### The report

- `AccountSummaryReportRequest.ExportFormat` (`"Dcca"`, case-insensitive), marked
  `[JsonIgnore(WhenWritingNull)]` exactly like `ReportQueryRequest.Excel` — so the normal export's
  request JSON, and therefore its warm cache, is unchanged.
- `GetExcelLayout` now picks between `StandardLayout` and `DccaLayout`.
- `GetExcelFooterTotalsAsync` returns `null` for the DCCA variant, which both suppresses the Total
  row and skips a cross-page `SUM` the file has no use for.
- `WriteRowsAsync` is unchanged — same rows, same order.
- **`[ExcelFormatVersion]` was deliberately NOT bumped**: the standard sheet's shape does not
  change, and the DCCA hash is new, so nothing stale can be served. Bump it if the DCCA layout is
  later revised — the attribute is per class and moves both variants.

### Frontend

- `ReportPageConfig.secondaryExcel` (`{ label, fileName, title, requestOverrides }`) — generic,
  currently used only by Account Summary.
- `BasicTable` gained optional `onSecondaryExcel` / `secondaryExcelLabel`; the second button
  renders only when set.
- `GenericReportPage.generateSecondaryExcel` reuses `enqueueExcelExport` unchanged, merging
  `requestOverrides` into the body and overriding the spec's `title`/`fileName`.
  `spec.controllerName` must stay `AccountSummaryReport` — both the edge filter and the enqueue
  service reject a mismatch.

## Verification

Old file (`git show origin/master:TradenetAdmin/uploads/AccountSummary.xlsx`) versus a file
generated from the new layout, compared cell by cell **keyed by row number**:

```
sheet name   old=['Sheet1']  new=['Sheet1']   MATCH
row 1  MATCH   (all 9 headers, including "Transation Title")
row 2  MATCH   (empty)
row 3  MATCH   1 | 09/01/2021 | 149290753@U09202100001 | … | 3000 | | 002 | NPT
row 4  MATCH
row 5  MATCH
row 6  MATCH
cell types (row 3)  old=[num,str,str,str,str,num,str,str,str]
                    new=[num,str,str,str,str,num,str,str,str]   MATCH
```

Note the one benign structural difference: the old file **omits** the row-2 element, while the new
one emits an empty `<row r="2"/>`. Both present row 2 as empty to Excel and to any OOXML reader;
the data still begins on row 3 in both.

Test runs (2026-09-09):
- Backend, excluding the DB-bound suites: **21 failed / 1749 passed**, against a `main` baseline of
  **21 failed / 1736 passed** — the failing set is *byte-identical*, and the +13 are the new tests.
  (The 21 are the repo's known red baseline.)
- Frontend: `npx tsc --noEmit` clean, `npm run build` clean, `npx vitest run src/Report/excel/`
  1429 passed — the spec fixture `Backend.Tests/Fixtures/ExcelSpecs/AccountSummaryReport.json` is
  unchanged, as `secondaryExcel` does not feed `buildExcelPresentation`.

## Still owed

- A customer trial-import of one generated file into DCCA before rollout. Everything above
  verifies the file against the *old export*; only DCCA itself can confirm the importer accepts it.
- The template's header **styling** (Arial 10 bold, thin borders, `wrapText`, row height 18) is not
  reproduced; the writer uses its own header style. Values, order and cell types match, which is
  what an importer reads. If DCCA turns out to be style-sensitive this needs a dedicated `cellXfs`
  entry (and its `count` bumped) in `StreamingExcelWriter.StylesXml`.
- `Amount` is `double` in `sp_AccountSummaryReportResult` where the old model used `decimal`;
  worth a spot-check on large fee values.

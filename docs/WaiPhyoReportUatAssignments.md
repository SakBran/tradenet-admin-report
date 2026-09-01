# Wai Phyo — Report UAT Assignments

Source: [TN2.0-Report-UAT-Testing](https://docs.google.com/document/d/1i9U5LLQ3if7wBsNvcFiGe_q1x13BqyAXeLQFBIJGu94/edit)

Work through these items one at a time. Do not begin the next item until the
current item has been checked against the old Tradenet 2.0 Admin report and
verified in the UAT configuration.

## No. 12 — Import Licence Daily Report (New Licence Report)

**UAT finding:** The Excel export does not include totals. The Testing Admin
export format is the expected result.

**Investigation (2026-09-01):** Confirmed. The API/screen response already
populates a grand-total footer for `No of Licences`, `Total Value`, and `Total
USD Value`, but the Excel export streams only the aggregate data rows. The
streaming Excel sink and writer have no way to append a total row, so the
workbook cannot contain those totals. This is an Excel-only gap.

**Work required:**

- Compare the UAT export with the old Tradenet 2.0 / Testing Admin export.
- Identify every required total (count, value, and currency totals where
  applicable).
- Add the missing totals to the UAT Excel export.
- Verify the exported totals against the displayed report and UAT data.

**Planned code scope:** Create one shared Daily-report total-row calculation,
then append that row after the existing Excel data rows. Add regression tests
for both the calculation and generated workbook row.

**Implemented (2026-09-01):** The Daily Report now appends a final `TOTAL` row
to non-empty Excel exports. The API footer and Excel row are derived from one
shared calculation for No of Licences, Total Value, and Total USD Value.
Focused unit/workbook tests pass and the backend builds successfully. The full
repository test suite is presently blocked by pre-existing failing fixtures and
unavailable local SQL test infrastructure; those failures are outside this
assignment.

**Done when:** UAT Excel contains the same relevant total rows and values as
Testing Admin for the same filters/date range.

## No. 13 — Import Licence Detail Report

**UAT finding:** The displayed/exported columns do not match Testing Admin, and
UAT currently shows two Import Licence Detail report variants.

**Investigation (2026-09-01):** `ImportLicenceDetailReport` is the intended
legacy-parity report: it has the same 26 columns and visible filter box as the
old `ImportLicenceDetailReport.rdlc` and old Admin page. The apparent duplicate
was `ImportLicenceDetailByLicenceReport`, a required nine-column, one-row-per-
licence drill-down used by the By Section, Method, Seller Country, and Company
reports. It was incorrectly given the exact same title and subtitle as the
legacy detail report, causing the UAT confusion. It must remain available for
those drill-downs, but must have a distinct UI and Excel identity.

**Work required:**

- Compare the old RDLC table headers and filter box with the new report config.
- List all column/filter differences before changing code.
- Keep the correct report variant and distinguish the necessary drill-down
  report without changing its route or drill-down links.
- Make the UAT table and Excel export match the approved Testing Admin layout.

**Done when:** The 26-column legacy-parity report remains unchanged, while the
nine-column drill-down is clearly identified as `Import Licence Detail (By
Licence)` in its UI and Excel download.

## No. 14 — Import Licence Extension Report

**UAT finding:** The Excel export does not include the Total Value.

**Work required:**

- Compare the UAT Excel export with the old Tradenet 2.0 / Testing Admin
  export.
- Identify the expected Total Value calculation and placement.
- Add Total Value to the UAT Excel export.
- Verify the value against the filtered UAT report data.

**Done when:** UAT Excel includes the correct Total Value for the same filters
and date range as Testing Admin.

## Execution order

1. No. 12 — Daily Report totals
2. No. 13 — Detail Report parity and duplicate cleanup
3. No. 14 — Extension Report Total Value

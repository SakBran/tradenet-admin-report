# Border Import Licence Parity Fixes and Task List

Date: 2026-06-10

Source audit:

- `docs/BorderImportLicence_ReportParity_Audit_2026-06-10.md`

Reports fixed:

- Border Import Licence By Method Report
- Border Import Licence By Section Report
- Border Import Licence By Seller Country Report
- Border Import Licence Company List Report
- Border Import Licence Daily Report
- Border Import Licence Detail Report

## Fixes Applied

### UI/filter parity

- [x] Removed the visible `Type` filter from the six Border Import Licence reports. The API controllers still force `Type = "Border"` server-side.
- [x] Removed extra filters that did not exist in the old Tradenet 2.0 Admin filter boxes.
- [x] Restored Company Name readonly helper behavior for Company List and Daily by using the existing `CompanyName` readonly filter populated from `CompanyRegistrationNo`.
- [x] Pinned Border Import Licence section/method/incoterm filters to dedicated lookup endpoints instead of the generic export/import lookup lists.

Current report filter sets:

| Report | Current filters |
| --- | --- |
| By Method | From/To Date, Sakhan, EIR Card Type, Import Section, Import Method |
| By Section | From/To Date, Sakhan, EIR Card Type, Import Section, Import Method |
| By Seller Country | From/To Date, Sakhan, EIR Card Type, Import Section, Import Method, Seller Country |
| Company List | From/To Date, EIR Card Type, Sakhan, Import Section, Import Method, Company Registration No, Company Name |
| Daily | From/To Date, Sakhan, Import Section, EIR Card Type, Company Registration No, Company Name |
| Detail | From/To Date, Sakhan, EIR Card Type, Import Section, Import Method, Import Incoterms |

### Lookup/data accuracy

- [x] Added `ReportLookups/borderImportLicenceSections`.
- [x] Added `ReportLookups/borderImportLicenceMethods`.
- [x] Added `ReportLookups/borderImportLicenceIncoterms`.
- [x] Scoped section lookup to active, non-deleted `Type == "Import Licence"` and `IsBorder == true`.
- [x] Scoped method/incoterm lookups to active, non-deleted `Type == "Import"` and `IsBorder == true`.
- [x] Left other report families untouched, including Border Export Licence and Border Import Permit lookup behavior.

### Total row parity

- [x] Enabled `includeColumnTotals: true` for Border Import Licence By Method.
- [x] Enabled `includeColumnTotals: true` for Border Import Licence By Section.
- [x] Enabled `includeColumnTotals: true` for Border Import Licence By Seller Country.
- [x] Enabled `includeColumnTotals: true` for Border Import Licence Company List.
- [x] Enabled `includeColumnTotals: true` for Border Import Licence Daily.
- [x] Left Border Import Licence Detail unchanged because the old RDLC has no grand-total footer.

### Report title and Detail column parity

- [x] Added old-admin style report subtitles to the six created Border Import Licence reports.
- [x] Corrected Border Import Licence Detail date headers to match the old RDLC: `Create Date` and `Approve Date`.
- [x] Kept the old data mapping for `Create Date`: the legacy RDLC label is `Create Date`, while its dataset field is formatted from `LicenceDate`.
- [x] Added the missing `Approve Date` UI column using the existing `approveDate` API field.

### Regression tests

- [x] Added explicit tests that the six Border Import Licence controllers force `Type = "Border"`.
- [x] Added lookup scoping tests proving the new Border Import Licence lookup endpoints do not return oversea, permit, or export rows.
- [x] Added frontend config tests for the six created Border Import Licence report subtitles and Detail date-column parity.

## Verification

Passed:

```powershell
dotnet test Backend.Tests/Backend.Tests.csproj --filter "FullyQualifiedName~BorderImportLicenceParityTests|FullyQualifiedName~ReportControllerBranchDefaultsTests" --no-restore
```

Result: 118 passed, 0 failed.

Passed:

```powershell
npm run build
```

Result: `tsc && vite build` completed successfully. Vite still reports the existing large bundle warning.

Not run:

- Live database endpoint timing/data comparison against the old Tradenet 2.0 Admin output. This needs a known production-like date range and matching old/new database data.
- Browser UI screenshot/manual QA. The build validates the config shape, but visual QA still needs a running app and login.

## Remaining QA Task List

- [ ] Open each fixed Border Import Licence report page in the app.
- [ ] Confirm filter controls match the old Tradenet 2.0 Admin pages.
- [ ] Confirm Border Import Licence section dropdown shows only `Import Licence + IsBorder` values.
- [ ] Confirm Border Import Licence method/incoterm dropdowns show only `Import + IsBorder` values.
- [ ] Run a known date range with data for each report.
- [ ] Compare row counts against old Tradenet 2.0 Admin RDLC output.
- [ ] Compare total rows for By Method, By Section, By Seller Country, Company List, and Daily.
- [ ] Confirm Daily report includes `Total USD Value` and grand total.
- [ ] Confirm Detail report shows `Create Date` and `Approve Date`, and has no grand-total footer.
- [ ] Export Excel for each report and compare headers, row counts, and totals.

## Performance Notes

- The changes keep the current fast data path: `sp_ImportLicenceDetailReport_Fast`.
- Summary reports continue to aggregate in SQL through `AggregateInSqlAsync`.
- Excel export remains queued/streaming.
- Removing unsupported extra filters reduces invalid filter combinations and should improve data accuracy without widening query scope.

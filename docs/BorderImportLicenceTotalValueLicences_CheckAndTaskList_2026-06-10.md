# Border Import Licence Total Value & Licences Check and Task List

Date: 2026-06-10

## Scope

Report under review:

- `BorderImportLicenceTotalValueLicencesReport`

References:

- Old admin view: `Views/Reports/BorderImportLicenceByTotalValueLicenceReport.cshtml`
- Old RDLC: `ReportControl/BorderImportLicenceByTotalValueLicenceReport.rdlc`
- New reference implementation: `ImportLicenceTotalValueLicencesReportController`
- New reference UI: `Frontend/src/Report/Page/ImportLicenceTotalValueLicencesReport.tsx`

## Parity Findings Before Fix

### Filter/UI parity

Old Border Import Licence filter box:

- From Date
- To Date
- Sakhan
- EIR Card Type
- Import Section

Current new config before fix:

- From Date / To Date
- Type
- EIR Card Type
- Import Section
- Import Method
- Import Incoterms
- Seller Country
- Company Registration No
- Sakhan

Diff:

- Missing parity: Border Import should show `Sakhan`, unlike oversea Import Licence.
- Extra visible filters: `Type`, `Import Method`, `Import Incoterms`, `Seller Country`, `Company Registration No`.
- Import Section lookup must be Border Import Licence scoped.
- Appearance should match the custom Import Licence Total Value page, with the additional old Border-only `Sakhan` filter.

### RDLC/data-shape parity

Old Border Import RDLC layout:

- Section 1 table: `Sr.No.`, `Total Value`, `Currency`
- Section 2 table: `Sr.No.`, `Total Licences`, `Pa Tha Ka Type`
- Footer value: `Totol USD Value` using `totalUSDAmount`
- Header parameter: `Border Import Licences Total Value & Licences (FromDate) To (ToDate)`

Current new behavior before fix:

- Controller returns `ApiResult<ReportAggregateResult>`, which supports only the first generic aggregate table.
- UI uses `GenericReportPage`, so it cannot render the second `Total Licences` table or the `Total USD Value` value.
- Reference `ImportLicenceTotalValueLicencesReportController` already returns `ImportLicenceTotalValueLicencesSummary`, which is the correct composite shape.

### Data accuracy requirements

- Keep `Type = "Border"` server-side; do not trust a visible/requested Type filter.
- Use the same total summary service as Import Licence, but with Border data.
- Count licences by Pa Tha Ka Type using Border Import Licence data only.
- Total value by currency and Total USD Value must honor Date, Sakhan, EIR Card Type, and Import Section filters.
- Do not change Border Export Licence, Import Licence, or Permit report behavior.

## Fix Task List

- [x] Compare old Border Import view filters against the new config.
- [x] Compare old Border Import RDLC sections against the current API/UI shape.
- [x] Confirm Import Licence reference uses the composite summary endpoint/page.
- [x] Change `BorderImportLicenceTotalValueLicencesReportController` to return `ImportLicenceTotalValueLicencesSummary`.
- [x] Keep `Type = "Border"` forced in the controller request mapping.
- [x] Change Border Import Total Value config filters to the old filter set.
- [x] Add old-admin report subtitle: `Border Import Licences Total Value & Licences`.
- [x] Replace the Border Import Total Value page with a custom page matching Import Licence appearance plus `Sakhan`.
- [x] Use Border Import Licence scoped section lookup.
- [x] Add backend tests for the forced Border type and summary return contract.
- [x] Add frontend config/static tests for the filter set and custom page expectations.
- [x] Run focused backend tests.
- [x] Run focused frontend tests.
- [x] Run frontend build.

## Verification

Passed:

```powershell
dotnet test Backend.Tests/Backend.Tests.csproj --filter "FullyQualifiedName~BorderImportLicenceParityTests|FullyQualifiedName~ReportControllerBranchDefaultsTests" --no-restore -p:OutDir=<temp>
```

Result: 120 passed, 0 failed. The default output directory was locked by a running `API.exe`, so the test was compiled to a temp output directory.

Passed:

```powershell
npm test -- reportConfigs.borderImportLicence.test.ts
```

Result: 3 passed, 0 failed.

Passed:

```powershell
npm run build
```

Result: `tsc && vite build` completed successfully. Vite still reports the existing large bundle warning.

## Remaining Manual QA

- [ ] Open the Border Import Licence Total Value & Licences page.
- [ ] Confirm filters render as From/To Date, Sakhan, EIR Card Type, Import Section.
- [ ] Run a known date range and compare old/new row counts for Total Value by Currency.
- [ ] Compare Total Licences by Pa Tha Ka Type against old RDLC.
- [ ] Compare Total USD Value against old RDLC.
- [ ] Export Excel and compare headers/data with old report output.

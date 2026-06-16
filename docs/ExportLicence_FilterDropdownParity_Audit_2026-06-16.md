# Export Licence Filter Dropdown Parity Audit

Date: 2026-06-16

## Scope

Checked these current reports:

- `ExportLicenceDailyReportNewLicenceReport`
- `ExportLicenceDetailReport`
- `ExportLicenceBySectionReport`
- `ExportLicenceByMethodReport`
- `ExportLicenceCompanyListReport`
- `ExportLicenceBySellerCountryReport`

I also checked the matching `BorderExportLicence...` reports because the same report names exist in both oversea and border families, and the customer wording did not explicitly say "Border".

## Source Availability

The configured old Tradenet 2.0 Admin checkout was not available on this machine:

- `C:\Users\saobranaung\Code\Ministry of Commerce\tradenet-2.0-admin\TradenetAdmin`: not found
- `/Users/saobranaung/Code/Ministry of Commerce/tradenet-2.0-admin/TradenetAdmin`: not found

Old RDLC column parity was checked from the already-generated `docs/ReportColumnComparison.md`, which records the old RDLC sources. Old business logic was checked from `docs/StoredProcedureDefinitions.sql`, especially `dbo.sp_ExportLicenceDetailReport`, plus comments in `Backend/Controllers/ReportLookupsController.cs` that document the legacy lookup filters.

## Table Column Parity

No table-column diffs were found for the six regular Export Licence reports. `docs/ReportColumnComparison.md` says each old RDLC table matches the current React table columns, aside from the normal `Sr.No.` / `No` generated row-number naming.

| Report | Old RDLC source | Column result |
| --- | --- | --- |
| Export Licence By Method | `ExportLicenceByMethodReport.rdlc` | Match |
| Export Licence By Section | `ExportLicenceBySectionReport.rdlc` | Match |
| Export Licence By Seller Country | `ExportLicenceByBuyerCountryReport.rdlc` | Match |
| Export Licence Company List | `ExportLicenceByCompanyReport.rdlc` | Match |
| Export Licence Daily | `ExportLicenceByDailyReport.rdlc` | Match |
| Export Licence Detail | `ExportLicenceDetailReport.rdlc` | Match |

The same is true for the equivalent `BorderExportLicence...` reports listed in the column comparison doc.

## Old Business Logic

The old stored procedure `dbo.sp_ExportLicenceDetailReport` used these parameters:

- `@Type`: `Oversea` or `Border`
- `@FromDate`
- `@ToDate`
- `@PaThaKaTypeId`
- `@ExportImportSectionId`
- `@ExportImportMethodId`
- `@ExportImportIncotermId`
- `@BuyerCountryId`
- `@CompanyRegistrationNo`
- `@SakhanId`

For oversea export licence data, old logic filtered:

- `ExportLicence.ApplyType = 'New'`
- `ExportLicence.Status = 'Approved'`
- `ExportLicence.CreatedDate` between from/to dates
- `PaThaKaType`
- `ExportImportSectionId`
- `ExportImportMethodId`
- `ExportImportIncotermId`
- `BuyerCountryId`
- `CompanyRegistrationNo`

For border export licence data, old logic used the same pattern against `BorderExportLicence`, plus `SakhanId`.

Legacy lookup intent documented in current code:

- Export Licence sections: active, not deleted, `Type = 'Export Licence'`, `IsOversea = true`
- Border Export Licence sections: active, not deleted, `Type = 'Export Licence'`, `IsBorder = true`
- Export methods/incoterms: active, not deleted, `Type = 'Export'`, `IsOversea = true`

## Current Regular Export Licence Filters

### ExportLicenceByMethodReport

Current visible filters:

- From Date / To Date
- EIR Card Type
- Export Section: `lookupName = exportLicenceSections`
- Method of export: `lookupName = exportLicenceMethods`

Backend forces `Type = "Oversea"` and maps section/method/card type into the summary query.

Diffs:

- Missing old-capable filters in UI: `ExportImportIncotermId`, `BuyerCountryId`, `CompanyRegistrationNo`
- `exportLicenceMethods` lookup is requested by the frontend but is not registered in `ReportLookupsController` switch, so the dropdown request will return 404.

### ExportLicenceBySectionReport

Current visible filters:

- From Date / To Date
- EIR Card Type
- Export Section: `lookupName = exportLicenceSections`
- Method of export: `lookupName = exportLicenceMethods`

Backend forces `Type = "Oversea"` and maps section/method/card type into the summary query.

Diffs:

- Missing old-capable filters in UI: `ExportImportIncotermId`, `BuyerCountryId`, `CompanyRegistrationNo`
- `exportLicenceMethods` lookup is requested by the frontend but is not registered, so the dropdown request will return 404.

### ExportLicenceBySellerCountryReport

Current visible filters:

- From Date / To Date
- EIR Card Type
- Export Section: `lookupName = exportLicenceSections`
- Method of export: `lookupName = exportLicenceMethods`
- Buyer Country

Backend forces `Type = "Oversea"` and maps section/method/card type/buyer country into the summary query.

Diffs:

- Missing old-capable filters in UI: `ExportImportIncotermId`, `CompanyRegistrationNo`
- `exportLicenceMethods` lookup is requested by the frontend but is not registered, so the dropdown request will return 404.
- `Buyer Country` is configured as a plain numeric field, not a country dropdown. The old business logic expects a country id, so this should use the `countries` lookup.

### ExportLicenceCompanyListReport

Current visible filters:

- From Date / To Date
- EIR Card Type
- Export Section: `lookupName = exportLicenceSections`
- Method of export: `lookupName = exportLicenceMethods`
- Company Registration No
- Company Name: readonly, populated from registration no, excluded from request

Backend forces `Type = "Oversea"` and maps section/method/card type/company registration into the summary query.

Diffs:

- Missing old-capable filters in UI: `ExportImportIncotermId`, `BuyerCountryId`
- `exportLicenceMethods` lookup is requested by the frontend but is not registered, so the dropdown request will return 404.

### ExportLicenceDailyReportNewLicenceReport

Current visible filters:

- From Date / To Date
- Export Section: `lookupName = exportLicenceSections`
- EIR Card Type
- Company Registration No
- Auto / None Auto: `--- All ---`, `auto`, `none-auto`
- Company Name: readonly, populated from registration no, excluded from request

Backend forces `Type = "Oversea"` and maps section/card type/company registration/auto into the daily summary query.

Diffs:

- Missing old-capable filters in UI: `ExportImportMethodId`, `ExportImportIncotermId`, `BuyerCountryId`
- Extra new filter compared with the old procedure contract: `Auto / None Auto`
- Current `Auto` filter works in the new `sp_ExportLicenceSummaryReport` daily branch, but it is not part of old `sp_ExportLicenceDetailReport`.

### ExportLicenceDetailReport

Current visible filters:

- From Date / To Date
- EIR Card Type
- Export Section: `lookupName = exportLicenceSections`
- Method of export: `lookupName = exportLicenceMethods`
- Method of export According to Incoterms: `lookupName = exportLicenceIncoterms`
- Auto / None Auto: `--- All ---`, `auto`, `none-auto`

Backend forces `Type = "Oversea"` and maps section/method/incoterm/card type/auto. The request class still accepts `BuyerCountryId`, `CompanyRegistrationNo`, and `SakhanId`, but they are not visible filters on the detail page.

Diffs:

- Missing old-capable filters in UI: `BuyerCountryId`, `CompanyRegistrationNo`
- Extra new visible filter compared with old procedure contract: `Auto / None Auto`
- `exportLicenceMethods` and `exportLicenceIncoterms` are requested by the frontend but are not registered in `ReportLookupsController` switch, so both dropdown requests will return 404.

## Current Border Export Licence Filters

The border family has a bigger dropdown mismatch.

Common current visible filters across the checked border reports:

- From Date / To Date
- Type: text, default empty
- EIR Card Type: number
- Export Section: `lookupName = borderExportLicenceSections`
- Method of export: number
- Method of export According to Incoterms: number
- Buyer Country: number
- Company Registration No: text
- Sakhan: number

Backend ignores the posted `Type` and forces `Type = "Border"`.

Diffs:

- `Type` is visible but functionally ignored. It should be hidden/removed or replaced with a meaningful fixed value.
- `Method of export`, `Method of export According to Incoterms`, `Buyer Country`, `EIR Card Type`, and `Sakhan` are plain numeric inputs, not dropdowns.
- `ReportLookupsController` only exposes `borderExportLicenceSections`; it does not expose border export licence methods/incoterms.
- `Buyer Country` should use `countries`.
- `EIR Card Type` should use `paThaKaTypes`.
- `Sakhan` should use `sakhans`.

## Most Likely Cause Of "Filter Data Is Not Same"

1. The regular Export Licence frontend asks for `exportLicenceMethods` and `exportLicenceIncoterms`, but `ReportLookupsController` does not route those lookup names. The helper methods exist, but the switch omits them.
2. Seller-country and several border filters are numeric inputs instead of lookup-backed dropdowns, so users cannot see/select the same option list as old Tradenet.
3. Some current pages expose fewer filters than the old stored procedure/business logic supports. The biggest regular-report gaps are `BuyerCountryId`, `ExportImportIncotermId`, and `CompanyRegistrationNo` depending on the report.
4. Detail and Daily have a new `Auto / None Auto` filter. It is implemented in the new backend path, but it is not part of the old stored procedure contract, so it is an intentional extension unless the old ASPX form also had it.

## Recommended Fix List

Do these after confirming the old ASPX filter forms if the old checkout becomes available:

1. Register `exportLicenceMethods` and `exportLicenceIncoterms` in `ReportLookupsController`.
2. Add border export licence lookup routes for methods/incoterms:
   - active
   - not deleted
   - `Type = 'Export'`
   - `IsBorder = true`
3. Convert numeric filter fields to dropdowns:
   - `PaThaKaTypeId` -> `paThaKaTypes`
   - `BuyerCountryId` -> `countries`
   - `SakhanId` -> `sakhans`
   - border `ExportImportMethodId` -> new `borderExportLicenceMethods`
   - border `ExportImportIncotermId` -> new `borderExportLicenceIncoterms`
4. Remove or hide visible `Type` from border export licence reports because controllers force `Type = "Border"`.
5. Decide whether regular Export Licence summary pages must show all old-capable filters (`Incoterm`, `Buyer Country`, `Company Registration No`) or whether each old ASPX form intentionally showed a smaller filter set.
6. Keep `Auto / None Auto` only if product confirms it is a new required filter; otherwise remove it for strict old-form parity.

## Files Checked

- `Frontend/src/Report/config/reportConfigs.ts`
- `Backend/Controllers/ReportLookupsController.cs`
- `Backend/Controllers/Report/ExportLicence*ReportController.cs`
- `Backend/Controllers/Report/BorderExportLicence*ReportController.cs`
- `Backend/StoredProcedureToLinq/sp_ExportLicenceDetailReportV2.cs`
- `StoredProcedureMigrations/sp_ExportLicenceSummaryReport.sql`
- `docs/StoredProcedureDefinitions.sql`
- `docs/ReportColumnComparison.md`

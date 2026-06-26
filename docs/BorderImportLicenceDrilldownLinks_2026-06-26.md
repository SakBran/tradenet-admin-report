# Border Import Licence Drilldown Links

Date: 2026-06-26

## Scope

Only the following Border Import Licence summary reports were changed:

- Border Import Licence By Section Report
- Border Import Licence By Method Report
- Border Import Licence By Seller Country Report
- Border Import Licence Company List Report

No controller, business logic, stored procedure, or unrelated report configuration was changed for this request.

## Reference Reports Checked

The implementation follows the existing Import Licence report drilldown pattern:

- Import Licence By Section Report links the `Section` column to detail with `ExportImportSectionId`.
- Import Licence By Method Report links the `Method` column to detail with `ExportImportMethodId`.
- Import Licence By Seller Country Report links the `Country` column to detail with `SellerCountryId`.
- Import Licence Company List Report links the `Company Name` column to detail with `CompanyRegistrationNo`.

## Changes Made

File changed:

- `Frontend/src/Report/config/reportConfigs.ts`

The following Border Import Licence columns now render as report drilldown links:

| Report | Linked column | Target report | Row parameter |
| --- | --- | --- | --- |
| Border Import Licence By Section Report | Section | BorderImportLicenceDetailReport | ExportImportSectionId from `sectionId` |
| Border Import Licence By Method Report | Method | BorderImportLicenceDetailReport | ExportImportMethodId from `methodId` |
| Border Import Licence By Seller Country Report | Country | BorderImportLicenceDetailReport | SellerCountryId from `countryId` |
| Border Import Licence Company List Report | Company Name | BorderImportLicenceDetailReport | CompanyRegistrationNo from `companyRegistrationNo` |

All four links also carry `Currency` from the clicked row.

## Carried Filters

The drilldown keeps the active filter context:

- `FromDate`
- `ToDate`
- `SakhanId`
- `PaThaKaTypeId`
- `ExportImportSectionId`, where applicable
- `ExportImportMethodId`, where applicable

`SakhanId` is included because Border Import Licence reports have Sakhan filtering and the detail controller already accepts it.

## Tests

Focused test coverage was added in:

- `Frontend/src/Report/config/reportConfigs.borderImportLicence.test.ts`

The test verifies that the four Border Import Licence summary reports link to `BorderImportLicenceDetailReport` with the expected carried filters and row parameter names.

Commands run:

- `npm test -- --run src/Report/config/reportConfigs.borderImportLicence.test.ts -t "summary reports link"`: passed, 1 test passed and 9 skipped.
- `npm run build`: passed. Vite reported the existing large chunk warning.

Additional note:

- Running the full `reportConfigs.borderImportLicence.test.ts` file currently has two unrelated existing failures around action report subtitle wording and voucher `PaymentType` lookup expectations. The new drilldown test itself passes.

# Report API Unit Test Todo

Last updated: 2026-05-28.

## Completed

- [x] Reproduced the HS Code report failure path: blank `FormType` selected the empty LINQ branch and dynamic sorting by `SakhanId` could not translate.
- [x] Fixed report-specific controllers so generated endpoints set their own stored-procedure `FormType` branch instead of forwarding a blank request value.
- [x] Fixed report-specific detail controllers so generated endpoints set `Type` to `Oversea` or `Border` from the endpoint name instead of forwarding a blank request value.
- [x] Made the HS Code empty branch translatable when dynamic sorting is applied.
- [x] Made the Voucher empty branch translatable when sorting is applied.
- [x] Made the remaining empty switch branches translatable for dynamic sorting:
  - `sp_AmendReport`
  - `sp_ActualAmendReport`
  - `sp_CancelReport`
  - `sp_ExtensionReport`
  - `sp_NewReport`
- [x] Fixed detail report query selection so endpoint-specific `Type` values avoid unnecessary mixed branch set operations.
- [x] Fixed border licence detail branch combination by explicitly combining the already-filtered card-type branches client-side after EF executes each branch.
- [x] Added `Backend.Tests` pinned to `net8.0`.
- [x] Added unit coverage for all switch-backed report controller branch defaults:
  - 58 `FormType` controller cases.
  - 50 `Type` controller cases.
- [x] Added the HS Code dynamic-sort translation regression test.
- [x] Added authenticated route/API smoke tests for every `/api/{ReportController}` POST endpoint.
- [x] Added authenticated Excel smoke tests for every `/api/{ReportController}/Excel` endpoint.
- [x] Added a report payload fixture test that records and validates the request payload used for every endpoint.
- [x] Ran seeded `TradeNetDBTest` POST smoke tests for every report endpoint:
  - 125 endpoints tested.
  - 9 data-returning endpoints.
  - 116 empty-by-fixture endpoints.
  - 0 failing endpoints.
- [x] Added representative seeded row-count assertions across report modules.
- [x] Ran `dotnet test Backend.Tests\Backend.Tests.csproj -p:UseAppHost=false -p:BaseOutputPath=C:\Code\Ministry_of_Commerce_Tradenet_test_build\`: 762 passed, 0 failed.

## Seeded Data-Returning Endpoints

- [x] `CompanyProfileController`: 3 rows.
- [x] `ListOfCompanyController`: 1 row.
- [x] `ListOfDirectorsByCompanyRegistrationNoController`: 3 rows.
- [x] `ListOfDirectorsController`: 3 rows.
- [x] `ListOfTopCapitalCompanyController`: 1 row.
- [x] `ListOfValidAndInvalidCompanyController`: 1 row.
- [x] `MemberRegistrationReportController`: 52 rows.
- [x] `PaThaKaRegisteredBusinessOrganizationReportController`: 1 row.
- [x] `RegistrationByBusinessTypeController`: 1 row.

## Next Todo

- [x] All todo items in this file are complete as of 2026-05-28.

## Current Coverage Rule

- [x] Report-specific endpoint identity must win over blank UI branch fields.
- [x] Blank `FormType` and `Type` request values must not force report-specific endpoints into empty generated branches.
- [x] Any fix is only marked complete after the automated test passes.

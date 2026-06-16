# Export Licence Total Value & Licences Report Parity Audit - 2026-06-16

## Scope

Report: `ExportLicenceTotalValueLicencesReport`
Reference new implementation: `ImportLicenceTotalValueLicencesReport`
Old-admin source path from `AGENTS.md` was not available locally, so old behavior is taken from the checked-in parity docs and existing report notes.

## Old / Reference Behavior

From `docs/ReportColumnComparison.md` and `docs/WaiPhyoReportDbWorkPlan.md`:

- Old RDLC: `ExportLicenceByTotalValueLicenceReport.rdlc`
- Visible filters: `From Date`, `To Date`, `PaThaKa Type` / EIR Card Type, `Export Section`
- Main table columns: `Sr.No.`, `Total Value`, `Currency`
- Import reference UI additionally renders the old Total Value & Licences composite layout:
  - Total Value by Currency table
  - Total Licences by Pa Tha Ka Type table
  - Total USD Value summary value

## Current Export Differences Found Before Fix

### Filter Box

Current `ExportLicenceTotalValueLicencesReport` had extra visible filters:

- `Type`
- `Method of export`
- `Method of export According to Incoterms`
- `Buyer Country`
- `Company Registration No`
- `Sakhan`

Current required/valid old filters are only:

- `dateRange` (`FromDate`, `ToDate`)
- `PaThaKaTypeId`
- `ExportImportSectionId`

Current filter data also lacked scoped lookups:

- `PaThaKaTypeId` had no `paThaKaTypes` lookup.
- `ExportImportSectionId` had no `exportLicenceSections` lookup.

### Result Shape / Business Logic

Current Export UI used `GenericReportPage`, so it rendered only one paged table with `Total Value` and `Currency`.

Import reference uses a dedicated page and summary endpoint returning:

- `totalValueByCurrency`
- `totalLicencesByPaThaKaType`
- `totalUsdValue`

Current Export backend returned `ApiResult<ReportAggregateResult>` from `CreateAggregateResultAsync`, so it could not feed the Import-style composite UI.

## Fix Plan

- Make Export filters match old/reference: date range, EIR Card Type, Export Section only.
- Add scoped lookup data for EIR Card Type and Export Section.
- Change Export page to use the same composite UI as Import, adjusted for Export text/lookups.
- Add Export backend summary method equivalent to Import `GetTotalValueLicencesSummaryAsync`.
- Change Export controller `POST` response to the composite summary model.
- Add regression tests for the filter shape and controller response contract.

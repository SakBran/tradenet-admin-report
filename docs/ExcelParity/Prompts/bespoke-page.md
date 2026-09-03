# Bespoke frontend page — read `_preamble.md`, then `Contract.md` §3, §8 and the plan's Phase 3

Bring ONE bespoke page onto the queued, spec-driven Excel flow. You may edit only
`Frontend/src/Report/Page/<Page>.tsx` and create/edit `Frontend/src/Report/excel/bespoke/<page>.ts` (+ register it in
`Frontend/src/Report/excel/bespoke/index.ts` — the registry file is the ONE shared file you may touch, add your entry only).
For `MemberRegistrationReport` you may also edit its entry in `Frontend/src/Report/config/reportConfigs.ts`. Do not run npm.

Recipes:
- **MemberRegistrationReport.tsx** (legacy synchronous blob download — broken; the controller enqueues a job):
  edit `reportConfigs.ts` `MemberRegistrationReport` (`ApplyType` → `type: 'select'`, options All/New/Extension with
  `defaultValue: 'All'`; `initialSortColumn: 'IssuedDate'`; `dataType: 'date'` on IssuedDate/StartDate/EndDate — check the
  page's current `formatDate` usage to match), then replace the page body with the standard wrapper
  `<GenericReportPage config={reportConfigs.MemberRegistrationReport} />`. If that is impossible (a filter the generic page cannot render), keep it bespoke and follow the generic recipe below.
- **ListOfDirectors.tsx**: move its column list and header lines into `bespoke/listOfDirectors.ts` as
  `listOfDirectorsColumns: ReportColumnConfig[]` and `buildListOfDirectorsSpec({ FromDate, ToDate })` (via
  `buildExcelPresentationFromInput`; `rowNumberTitle 'No.'`; header lines = its existing lines without From/To — the backend adds them);
  `generateExcel` → `enqueueExcelExport(EXCEL_ROUTE, buildRequest(f, query), spec, EXCEL_FILE_NAME)`; grid columns and
  `reportHeaderLines` from the same module.
- **CompanyProfile.tsx**: `bespoke/companyProfile.ts` with the flat leaf headers in UI order (Myanmar titles) and
  `rowNumberTitle 'စဥ်'`; post the spec via `enqueueExcelExport`. The backend keeps a typed layout for this report
  (composed/merged cells); your spec's titles must equal the page's leaf `<th>` texts exactly.
- **\*TotalValueLicencesReport.tsx** (4 pages): `bespoke/totalValueLicences.ts` exporting
  `buildTotalValueLicencesSpec(configKey, { FromDate, ToDate })` with `columns: []`, two `sections`
  (`totalValueByCurrency`: Total Value `#,##0.0000`, Currency; `totalLicencesByPaThaKaType`: Total Licences, Pa Tha Ka Type;
  `rowNumberTitle 'Sr.No.'`) and `summaryLines: [{ label: 'Total USD Value', dataPath: 'totalUsdValue', numberFormat: '#,##0.0000' }]`;
  header lines via `buildReportHeaderLines(config, applied)`; wire `generateExcel` through `enqueueExcelExport`; render the
  page heading from the same header lines. All four pages share the module; each passes its own configKey.
- Generic recipe (fallback): build `ExcelPresentationInput` from the page's own columns/labels, call
  `buildExcelPresentationFromInput`, post with `enqueueExcelExport`.

Register the builder under the config key in `bespoke/index.ts` so the fixture generator emits this page's fixture.
Keep filters, grid, and drill behaviour untouched. Remove the page-local copies of `ExcelEnqueueResult`/`downloadBlob`.

Return `PAGE`:
```json
{ "page": "<Page>", "status": "ok|blocked", "edits": ["path:line — what"], "concerns": ["..."] }
```

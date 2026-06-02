# Frontend Report Implementation Guide For LLMs

Use this guide to create React TypeScript report pages for the report API controllers under `Backend/Controllers/Report`.

## Goal

Create one frontend report page for one report API controller.

- One report API controller equals one frontend page.
- Pages must live under `Frontend/src/Report/Page`.
- Pages must consume the authenticated API through `Frontend/src/services/AxiosInstance.ts`.
- Pages must use the reusable table in `Frontend/src/components/My Components/Table/BasicTable.tsx`.
- Pages must support POST pagination, sorting, searching/filtering, and Excel generation.
- Routes must be added only under the existing `ProtectedRoute` block so the page can be opened only after login.
- Add a side navigation entry only after the protected route exists.

## Current Status

- Total report API controllers: 158
- Frontend completed: 158
- Frontend remaining: 0

Status values:

- `To Do`: frontend page is not implemented yet.
- `Completed`: page exists, uses `BasicTable`, calls the POST report API, calls the POST Excel API, is inside the protected route, appears in navigation, and `npm run build` succeeds.
- `Blocked`: page could not be completed; write a short reason in the status column before moving on.

## Batch Execution Mode

When the user asks to build frontend report pages from this guide, do not stop after one page unless the user explicitly asks for only one.

Work continuously in the same session:

1. Pick the next `To Do` row from the tracker in this file.
2. Read the matching backend controller and stored-procedure LINQ result type.
3. Create the React page.
4. Add the protected route.
5. Add navigation.
6. Run `npm run build` from `Frontend`.
7. If the build succeeds, mark the tracker row `Completed` and update completed/remaining counts.
8. Move immediately to the next `To Do` row.
9. Continue until no `To Do` rows remain, a real blocker is found, or context/tool limits make it unsafe to continue.

Do not ask the user to repeat the command for each report.

## Required Files To Read First

Before creating a frontend report page, read these files:

1. `Backend/Controllers/Report/{ControllerName}Controller.cs`
   - Confirm the exact route.
   - Confirm the POST body DTO.
   - Confirm the Excel endpoint is `[HttpPost("Excel")]`.
   - Confirm the controller uses `[Authorize]`.

2. `Backend/StoredProcedureToLinq/{StoredProcedureName}.cs`
   - Confirm real request fields.
   - Confirm real result fields.
   - Do not invent fields from the report name.

3. `Frontend/src/Report/Page/MemberRegistrationReport.tsx`
   - Use this as the reference implementation.

4. `Frontend/src/components/My Components/Table/BasicTable.tsx`
   - Use `fetchData` for report POST calls.
   - Use `onExcel` for backend Excel generation.
   - Use `columns` for typed display/sort/filter mapping.

5. `Frontend/src/routes/routes.tsx`
   - Add report pages under the existing `ProtectedRoute` children only.

6. `Frontend/src/layouts/app/SideNav.tsx`
   - Add report navigation after the route exists.

7. `Frontend/src/constants/routes.ts`
   - Add reusable path constants.

8. `Frontend/src/Report/Page/GenericReportPage.tsx`
   - Reuse this generated report page shell for normal report pages.
   - It renders business filters, calls POST report APIs, calls POST Excel APIs, and passes data into `BasicTable`.

9. `Frontend/src/Report/config/reportConfigs.ts`
   - Use this generated config as the source for columns, filters, route names, and Excel file names.
   - Confirm values are generated from the real backend controllers and LINQ result classes before changing them.

10. `Frontend/src/Report/reportRoutes.tsx`
    - Contains protected child routes for report pages.

11. `Frontend/src/Report/reportNavItems.tsx`
    - Contains side navigation entries for reports.

## Anti-Hallucination Checks

Run these checks before implementing a page:

```powershell
Test-Path Backend\Controllers\Report\{ControllerName}Controller.cs
rg "class {ControllerName}Controller|HttpPost|HttpPost\\(\"" Backend\Controllers\Report\{ControllerName}Controller.cs
rg "class .*Result|public .* Query|public .*Request" Backend\StoredProcedureToLinq -n
```

Rules:

- Do not create a page for a controller that does not exist.
- Do not guess request filters. Read the controller request DTO.
- Do not guess table columns. Read the LINQ result type.
- Do not use GET for report pages. Report APIs use POST.
- Do not build Excel in the browser for report pages. Use `POST /api/{ControllerName}/Excel`.
- Do not put report routes outside `ProtectedRoute`.
- Do not add `[AllowAnonymous]` or frontend public routes for reports.

## Table Mapping Rules

Backend sort/filter fields are C# property names, usually PascalCase. API JSON row fields are camelCase.

Use this `BasicTable` column pattern:

```tsx
const columns: BasicTableColumn<MyRow>[] = [
  {
    key: 'MemberCode',
    dataIndex: 'memberCode',
    title: 'Member Code',
  },
];
```

- `key`: backend sort/filter column name.
- `dataIndex`: JSON row property name.
- `title`: visible table header.
- `render`: use for dates, numbers, or custom display.
- Set `showActions={false}` for pure report tables.

## API Call Pattern

Use `AxiosInstance`; it adds the base URL and bearer token.

```tsx
const fetchRows = async (query: BasicTableQuery) => {
  const response = await axiosInstance.post<PaginationType<MyRow>>(
    'ControllerName',
    {
      ...filters,
      pageIndex: query.pageIndex,
      pageSize: query.pageSize,
      sortColumn: query.sortColumn,
      sortOrder: query.sortOrder.toUpperCase(),
      filterColumn: query.filterColumn,
      filterQuery: query.filterQuery,
    }
  );

  return response.data;
};
```

Excel pattern:

```tsx
const generateExcel = async (query: BasicTableQuery) => {
  const response = await axiosInstance.post(
    'ControllerName/Excel',
    buildRequest(filters, query),
    { responseType: 'blob' }
  );
};
```

## Route Pattern

Add report routes inside the existing `ProtectedRoute` children:

```tsx
{
  path: `/Report`,
  element: (
    <PageWrapper>
      <DashboardLayout />
    </PageWrapper>
  ),
  errorElement: <ErrorPage />,
  children: [
    {
      path: 'MemberRegistrationReport',
      element: <MemberRegistrationReport />,
    },
  ],
}
```

## Completion Checklist

Before marking a tracker row complete:

1. Page exists under `Frontend/src/Report/Page`.
2. Page uses `BasicTable`.
3. Page uses POST JSON endpoint.
4. Page uses POST Excel endpoint with `responseType: 'blob'`.
5. Page supports pagination through `BasicTableQuery`.
6. Page supports sorting through `sortColumn` and `sortOrder`.
7. Page supports search through `filterColumn` and `filterQuery`.
8. Page includes real business filters from the backend request DTO only.
9. Route is inside `ProtectedRoute`.
10. Navigation points to the protected route.
11. `npm run build` succeeds from `Frontend`.
12. Update this tracker row to `Completed`.

## Completed Reference

- Page: `Frontend/src/Report/Page/MemberRegistrationReport.tsx`
- Route: `/Report/MemberRegistrationReport`
- API: `POST /api/MemberRegistrationReport`
- Excel API: `POST /api/MemberRegistrationReport/Excel`
- Table: `Frontend/src/components/My Components/Table/BasicTable.tsx`
- Status: `Completed`

All report controllers currently have frontend pages generated and included in protected routes.

## Frontend Report Tracker

| No. | Controller | API Route | Page File | Frontend Status |
| --: | ---------- | --------- | --------- | --------------- |
| 1 | AccountSummaryReport | `/api/AccountSummaryReport` | `Frontend/src/Report/Page/AccountSummaryReport.tsx` | Completed |
| 2 | BorderExportLicenceActualAmendmentReport | `/api/BorderExportLicenceActualAmendmentReport` | `Frontend/src/Report/Page/BorderExportLicenceActualAmendmentReport.tsx` | Completed |
| 3 | BorderExportLicenceAmendmentReport | `/api/BorderExportLicenceAmendmentReport` | `Frontend/src/Report/Page/BorderExportLicenceAmendmentReport.tsx` | Completed |
| 4 | BorderExportLicenceByHSCodeReport | `/api/BorderExportLicenceByHSCodeReport` | `Frontend/src/Report/Page/BorderExportLicenceByHSCodeReport.tsx` | Completed |
| 5 | BorderExportLicenceByMethodReport | `/api/BorderExportLicenceByMethodReport` | `Frontend/src/Report/Page/BorderExportLicenceByMethodReport.tsx` | Completed |
| 6 | BorderExportLicenceBySectionReport | `/api/BorderExportLicenceBySectionReport` | `Frontend/src/Report/Page/BorderExportLicenceBySectionReport.tsx` | Completed |
| 7 | BorderExportLicenceBySellerCountryReport | `/api/BorderExportLicenceBySellerCountryReport` | `Frontend/src/Report/Page/BorderExportLicenceBySellerCountryReport.tsx` | Completed |
| 8 | BorderExportLicenceCancellationReport | `/api/BorderExportLicenceCancellationReport` | `Frontend/src/Report/Page/BorderExportLicenceCancellationReport.tsx` | Completed |
| 9 | BorderExportLicenceCompanyListReport | `/api/BorderExportLicenceCompanyListReport` | `Frontend/src/Report/Page/BorderExportLicenceCompanyListReport.tsx` | Completed |
| 10 | BorderExportLicenceDailyReportNewLicenceReport | `/api/BorderExportLicenceDailyReportNewLicenceReport` | `Frontend/src/Report/Page/BorderExportLicenceDailyReportNewLicenceReport.tsx` | Completed |
| 11 | BorderExportLicenceDetailReport | `/api/BorderExportLicenceDetailReport` | `Frontend/src/Report/Page/BorderExportLicenceDetailReport.tsx` | Completed |
| 12 | BorderExportLicenceExtensionReport | `/api/BorderExportLicenceExtensionReport` | `Frontend/src/Report/Page/BorderExportLicenceExtensionReport.tsx` | Completed |
| 13 | BorderExportLicenceNewReportNewReport | `/api/BorderExportLicenceNewReportNewReport` | `Frontend/src/Report/Page/BorderExportLicenceNewReportNewReport.tsx` | Completed |
| 14 | BorderExportLicenceTotalValueLicencesReport | `/api/BorderExportLicenceTotalValueLicencesReport` | `Frontend/src/Report/Page/BorderExportLicenceTotalValueLicencesReport.tsx` | Completed |
| 15 | BorderExportLicenceVoucherReport | `/api/BorderExportLicenceVoucherReport` | `Frontend/src/Report/Page/BorderExportLicenceVoucherReport.tsx` | Completed |
| 16 | BorderExportPermitActualAmendmentReport | `/api/BorderExportPermitActualAmendmentReport` | `Frontend/src/Report/Page/BorderExportPermitActualAmendmentReport.tsx` | Completed |
| 17 | BorderExportPermitAmendmentReport | `/api/BorderExportPermitAmendmentReport` | `Frontend/src/Report/Page/BorderExportPermitAmendmentReport.tsx` | Completed |
| 18 | BorderExportPermitByHSCodeReport | `/api/BorderExportPermitByHSCodeReport` | `Frontend/src/Report/Page/BorderExportPermitByHSCodeReport.tsx` | Completed |
| 19 | BorderExportPermitBySectionReport | `/api/BorderExportPermitBySectionReport` | `Frontend/src/Report/Page/BorderExportPermitBySectionReport.tsx` | Completed |
| 20 | BorderExportPermitBySellerCountryReport | `/api/BorderExportPermitBySellerCountryReport` | `Frontend/src/Report/Page/BorderExportPermitBySellerCountryReport.tsx` | Completed |
| 21 | BorderExportPermitCancellationReport | `/api/BorderExportPermitCancellationReport` | `Frontend/src/Report/Page/BorderExportPermitCancellationReport.tsx` | Completed |
| 22 | BorderExportPermitCompanyListReport | `/api/BorderExportPermitCompanyListReport` | `Frontend/src/Report/Page/BorderExportPermitCompanyListReport.tsx` | Completed |
| 23 | BorderExportPermitDailyReportNewPermitReport | `/api/BorderExportPermitDailyReportNewPermitReport` | `Frontend/src/Report/Page/BorderExportPermitDailyReportNewPermitReport.tsx` | Completed |
| 24 | BorderExportPermitDetailReport | `/api/BorderExportPermitDetailReport` | `Frontend/src/Report/Page/BorderExportPermitDetailReport.tsx` | Completed |
| 25 | BorderExportPermitExtensionReport | `/api/BorderExportPermitExtensionReport` | `Frontend/src/Report/Page/BorderExportPermitExtensionReport.tsx` | Completed |
| 26 | BorderExportPermitNewReportNewReport | `/api/BorderExportPermitNewReportNewReport` | `Frontend/src/Report/Page/BorderExportPermitNewReportNewReport.tsx` | Completed |
| 27 | BorderExportPermitVoucherReport | `/api/BorderExportPermitVoucherReport` | `Frontend/src/Report/Page/BorderExportPermitVoucherReport.tsx` | Completed |
| 28 | BorderImportLicenceActualAmendmentReport | `/api/BorderImportLicenceActualAmendmentReport` | `Frontend/src/Report/Page/BorderImportLicenceActualAmendmentReport.tsx` | Completed |
| 29 | BorderImportLicenceAmendmentReport | `/api/BorderImportLicenceAmendmentReport` | `Frontend/src/Report/Page/BorderImportLicenceAmendmentReport.tsx` | Completed |
| 30 | BorderImportLicenceByHSCodeReport | `/api/BorderImportLicenceByHSCodeReport` | `Frontend/src/Report/Page/BorderImportLicenceByHSCodeReport.tsx` | Completed |
| 31 | BorderImportLicenceByMethodReport | `/api/BorderImportLicenceByMethodReport` | `Frontend/src/Report/Page/BorderImportLicenceByMethodReport.tsx` | Completed |
| 32 | BorderImportLicenceBySectionReport | `/api/BorderImportLicenceBySectionReport` | `Frontend/src/Report/Page/BorderImportLicenceBySectionReport.tsx` | Completed |
| 33 | BorderImportLicenceBySellerCountryReport | `/api/BorderImportLicenceBySellerCountryReport` | `Frontend/src/Report/Page/BorderImportLicenceBySellerCountryReport.tsx` | Completed |
| 34 | BorderImportLicenceCancellationReport | `/api/BorderImportLicenceCancellationReport` | `Frontend/src/Report/Page/BorderImportLicenceCancellationReport.tsx` | Completed |
| 35 | BorderImportLicenceCompanyListReport | `/api/BorderImportLicenceCompanyListReport` | `Frontend/src/Report/Page/BorderImportLicenceCompanyListReport.tsx` | Completed |
| 36 | BorderImportLicenceDailyReportNewLicenceReport | `/api/BorderImportLicenceDailyReportNewLicenceReport` | `Frontend/src/Report/Page/BorderImportLicenceDailyReportNewLicenceReport.tsx` | Completed |
| 37 | BorderImportLicenceDetailReport | `/api/BorderImportLicenceDetailReport` | `Frontend/src/Report/Page/BorderImportLicenceDetailReport.tsx` | Completed |
| 38 | BorderImportLicenceDetailReportPending | `/api/BorderImportLicenceDetailReportPending` | `Frontend/src/Report/Page/BorderImportLicenceDetailReportPending.tsx` | Completed |
| 39 | BorderImportLicenceExtensionReport | `/api/BorderImportLicenceExtensionReport` | `Frontend/src/Report/Page/BorderImportLicenceExtensionReport.tsx` | Completed |
| 40 | BorderImportLicenceNewReportNewReport | `/api/BorderImportLicenceNewReportNewReport` | `Frontend/src/Report/Page/BorderImportLicenceNewReportNewReport.tsx` | Completed |
| 41 | BorderImportLicencePendingReport | `/api/BorderImportLicencePendingReport` | `Frontend/src/Report/Page/BorderImportLicencePendingReport.tsx` | Completed |
| 42 | BorderImportLicenceTotalValueLicencesReport | `/api/BorderImportLicenceTotalValueLicencesReport` | `Frontend/src/Report/Page/BorderImportLicenceTotalValueLicencesReport.tsx` | Completed |
| 43 | BorderImportLicenceVoucherReport | `/api/BorderImportLicenceVoucherReport` | `Frontend/src/Report/Page/BorderImportLicenceVoucherReport.tsx` | Completed |
| 44 | BorderImportPermitActualAmendmentReport | `/api/BorderImportPermitActualAmendmentReport` | `Frontend/src/Report/Page/BorderImportPermitActualAmendmentReport.tsx` | Completed |
| 45 | BorderImportPermitAmendmentReport | `/api/BorderImportPermitAmendmentReport` | `Frontend/src/Report/Page/BorderImportPermitAmendmentReport.tsx` | Completed |
| 46 | BorderImportPermitByHSCodeReport | `/api/BorderImportPermitByHSCodeReport` | `Frontend/src/Report/Page/BorderImportPermitByHSCodeReport.tsx` | Completed |
| 47 | BorderImportPermitBySectionReport | `/api/BorderImportPermitBySectionReport` | `Frontend/src/Report/Page/BorderImportPermitBySectionReport.tsx` | Completed |
| 48 | BorderImportPermitBySellerCountryReport | `/api/BorderImportPermitBySellerCountryReport` | `Frontend/src/Report/Page/BorderImportPermitBySellerCountryReport.tsx` | Completed |
| 49 | BorderImportPermitCancellationReport | `/api/BorderImportPermitCancellationReport` | `Frontend/src/Report/Page/BorderImportPermitCancellationReport.tsx` | Completed |
| 50 | BorderImportPermitCompanyListReport | `/api/BorderImportPermitCompanyListReport` | `Frontend/src/Report/Page/BorderImportPermitCompanyListReport.tsx` | Completed |
| 51 | BorderImportPermitDailyReportNewPermitReport | `/api/BorderImportPermitDailyReportNewPermitReport` | `Frontend/src/Report/Page/BorderImportPermitDailyReportNewPermitReport.tsx` | Completed |
| 52 | BorderImportPermitDetailReport | `/api/BorderImportPermitDetailReport` | `Frontend/src/Report/Page/BorderImportPermitDetailReport.tsx` | Completed |
| 53 | BorderImportPermitExtensionReport | `/api/BorderImportPermitExtensionReport` | `Frontend/src/Report/Page/BorderImportPermitExtensionReport.tsx` | Completed |
| 54 | BorderImportPermitNewReportNewReport | `/api/BorderImportPermitNewReportNewReport` | `Frontend/src/Report/Page/BorderImportPermitNewReportNewReport.tsx` | Completed |
| 55 | BorderImportPermitVoucherReport | `/api/BorderImportPermitVoucherReport` | `Frontend/src/Report/Page/BorderImportPermitVoucherReport.tsx` | Completed |
| 56 | CardListsByCompanyRegistrationNumber | `/api/CardListsByCompanyRegistrationNumber` | `Frontend/src/Report/Page/CardListsByCompanyRegistrationNumber.tsx` | Completed |
| 57 | ChequeNoReport | `/api/ChequeNoReport` | `Frontend/src/Report/Page/ChequeNoReport.tsx` | Completed |
| 58 | CompanyProfile | `/api/CompanyProfile` | `Frontend/src/Report/Page/CompanyProfile.tsx` | Completed |
| 59 | EIRCardBindReport | `/api/EIRCardBindReport` | `Frontend/src/Report/Page/EIRCardBindReport.tsx` | Completed |
| 60 | ExportLicenceActualAmendmentReport | `/api/ExportLicenceActualAmendmentReport` | `Frontend/src/Report/Page/ExportLicenceActualAmendmentReport.tsx` | Completed |
| 61 | ExportLicenceAmendmentReport | `/api/ExportLicenceAmendmentReport` | `Frontend/src/Report/Page/ExportLicenceAmendmentReport.tsx` | Completed |
| 62 | ExportLicenceByHSCodeReport | `/api/ExportLicenceByHSCodeReport` | `Frontend/src/Report/Page/ExportLicenceByHSCodeReport.tsx` | Completed |
| 63 | ExportLicenceByMethodReport | `/api/ExportLicenceByMethodReport` | `Frontend/src/Report/Page/ExportLicenceByMethodReport.tsx` | Completed |
| 64 | ExportLicenceBySectionReport | `/api/ExportLicenceBySectionReport` | `Frontend/src/Report/Page/ExportLicenceBySectionReport.tsx` | Completed |
| 65 | ExportLicenceBySellerCountryReport | `/api/ExportLicenceBySellerCountryReport` | `Frontend/src/Report/Page/ExportLicenceBySellerCountryReport.tsx` | Completed |
| 66 | ExportLicenceCancellationReport | `/api/ExportLicenceCancellationReport` | `Frontend/src/Report/Page/ExportLicenceCancellationReport.tsx` | Completed |
| 67 | ExportLicenceCompanyListReport | `/api/ExportLicenceCompanyListReport` | `Frontend/src/Report/Page/ExportLicenceCompanyListReport.tsx` | Completed |
| 68 | ExportLicenceDailyReportNewLicenceReport | `/api/ExportLicenceDailyReportNewLicenceReport` | `Frontend/src/Report/Page/ExportLicenceDailyReportNewLicenceReport.tsx` | Completed |
| 69 | ExportLicenceDetailReport | `/api/ExportLicenceDetailReport` | `Frontend/src/Report/Page/ExportLicenceDetailReport.tsx` | Completed |
| 70 | ExportLicenceExtensionReport | `/api/ExportLicenceExtensionReport` | `Frontend/src/Report/Page/ExportLicenceExtensionReport.tsx` | Completed |
| 71 | ExportLicenceNewReportNewReport | `/api/ExportLicenceNewReportNewReport` | `Frontend/src/Report/Page/ExportLicenceNewReportNewReport.tsx` | Completed |
| 72 | ExportLicenceTotalValueLicencesReport | `/api/ExportLicenceTotalValueLicencesReport` | `Frontend/src/Report/Page/ExportLicenceTotalValueLicencesReport.tsx` | Completed |
| 73 | ExportLicenceVoucherReport | `/api/ExportLicenceVoucherReport` | `Frontend/src/Report/Page/ExportLicenceVoucherReport.tsx` | Completed |
| 74 | ExportPermitActualAmendmentReport | `/api/ExportPermitActualAmendmentReport` | `Frontend/src/Report/Page/ExportPermitActualAmendmentReport.tsx` | Completed |
| 75 | ExportPermitAmendmentReport | `/api/ExportPermitAmendmentReport` | `Frontend/src/Report/Page/ExportPermitAmendmentReport.tsx` | Completed |
| 76 | ExportPermitByHSCodeReport | `/api/ExportPermitByHSCodeReport` | `Frontend/src/Report/Page/ExportPermitByHSCodeReport.tsx` | Completed |
| 77 | ExportPermitBySectionReport | `/api/ExportPermitBySectionReport` | `Frontend/src/Report/Page/ExportPermitBySectionReport.tsx` | Completed |
| 78 | ExportPermitBySellerCountryReport | `/api/ExportPermitBySellerCountryReport` | `Frontend/src/Report/Page/ExportPermitBySellerCountryReport.tsx` | Completed |
| 79 | ExportPermitCancellationReport | `/api/ExportPermitCancellationReport` | `Frontend/src/Report/Page/ExportPermitCancellationReport.tsx` | Completed |
| 80 | ExportPermitCompanyListReport | `/api/ExportPermitCompanyListReport` | `Frontend/src/Report/Page/ExportPermitCompanyListReport.tsx` | Completed |
| 81 | ExportPermitDailyReportNewPermitReport | `/api/ExportPermitDailyReportNewPermitReport` | `Frontend/src/Report/Page/ExportPermitDailyReportNewPermitReport.tsx` | Completed |
| 82 | ExportPermitDetailReport | `/api/ExportPermitDetailReport` | `Frontend/src/Report/Page/ExportPermitDetailReport.tsx` | Completed |
| 83 | ExportPermitExtensionReport | `/api/ExportPermitExtensionReport` | `Frontend/src/Report/Page/ExportPermitExtensionReport.tsx` | Completed |
| 84 | ExportPermitNewReportNewReport | `/api/ExportPermitNewReportNewReport` | `Frontend/src/Report/Page/ExportPermitNewReportNewReport.tsx` | Completed |
| 85 | ExportPermitVoucherReport | `/api/ExportPermitVoucherReport` | `Frontend/src/Report/Page/ExportPermitVoucherReport.tsx` | Completed |
| 86 | ImportLicenceActualAmendmentReport | `/api/ImportLicenceActualAmendmentReport` | `Frontend/src/Report/Page/ImportLicenceActualAmendmentReport.tsx` | Completed |
| 87 | ImportLicenceAmendmentReport | `/api/ImportLicenceAmendmentReport` | `Frontend/src/Report/Page/ImportLicenceAmendmentReport.tsx` | Completed |
| 88 | ImportLicenceByHSCodeReport | `/api/ImportLicenceByHSCodeReport` | `Frontend/src/Report/Page/ImportLicenceByHSCodeReport.tsx` | Completed |
| 89 | ImportLicenceByMethodReport | `/api/ImportLicenceByMethodReport` | `Frontend/src/Report/Page/ImportLicenceByMethodReport.tsx` | Completed |
| 90 | ImportLicenceBySectionReport | `/api/ImportLicenceBySectionReport` | `Frontend/src/Report/Page/ImportLicenceBySectionReport.tsx` | Completed |
| 91 | ImportLicenceBySellerCountryReport | `/api/ImportLicenceBySellerCountryReport` | `Frontend/src/Report/Page/ImportLicenceBySellerCountryReport.tsx` | Completed |
| 92 | ImportLicenceCancellationReport | `/api/ImportLicenceCancellationReport` | `Frontend/src/Report/Page/ImportLicenceCancellationReport.tsx` | Completed |
| 93 | ImportLicenceCompanyListReport | `/api/ImportLicenceCompanyListReport` | `Frontend/src/Report/Page/ImportLicenceCompanyListReport.tsx` | Completed |
| 94 | ImportLicenceDailyReportNewLicenceReport | `/api/ImportLicenceDailyReportNewLicenceReport` | `Frontend/src/Report/Page/ImportLicenceDailyReportNewLicenceReport.tsx` | Completed |
| 95 | ImportLicenceDetailReport | `/api/ImportLicenceDetailReport` | `Frontend/src/Report/Page/ImportLicenceDetailReport.tsx` | Completed |
| 96 | ImportLicenceDetailReportPending | `/api/ImportLicenceDetailReportPending` | `Frontend/src/Report/Page/ImportLicenceDetailReportPending.tsx` | Completed |
| 97 | ImportLicenceExtensionReport | `/api/ImportLicenceExtensionReport` | `Frontend/src/Report/Page/ImportLicenceExtensionReport.tsx` | Completed |
| 98 | ImportLicenceNewReportNewReport | `/api/ImportLicenceNewReportNewReport` | `Frontend/src/Report/Page/ImportLicenceNewReportNewReport.tsx` | Completed |
| 99 | ImportLicencePendingReport | `/api/ImportLicencePendingReport` | `Frontend/src/Report/Page/ImportLicencePendingReport.tsx` | Completed |
| 100 | ImportLicenceTotalValueLicencesReport | `/api/ImportLicenceTotalValueLicencesReport` | `Frontend/src/Report/Page/ImportLicenceTotalValueLicencesReport.tsx` | Completed |
| 101 | ImportLicenceVoucherReport | `/api/ImportLicenceVoucherReport` | `Frontend/src/Report/Page/ImportLicenceVoucherReport.tsx` | Completed |
| 102 | ImportPermitActualAmendmentReport | `/api/ImportPermitActualAmendmentReport` | `Frontend/src/Report/Page/ImportPermitActualAmendmentReport.tsx` | Completed |
| 103 | ImportPermitAmendmentReport | `/api/ImportPermitAmendmentReport` | `Frontend/src/Report/Page/ImportPermitAmendmentReport.tsx` | Completed |
| 104 | ImportPermitByHSCodeReport | `/api/ImportPermitByHSCodeReport` | `Frontend/src/Report/Page/ImportPermitByHSCodeReport.tsx` | Completed |
| 105 | ImportPermitBySectionReport | `/api/ImportPermitBySectionReport` | `Frontend/src/Report/Page/ImportPermitBySectionReport.tsx` | Completed |
| 106 | ImportPermitBySellerCountryReport | `/api/ImportPermitBySellerCountryReport` | `Frontend/src/Report/Page/ImportPermitBySellerCountryReport.tsx` | Completed |
| 107 | ImportPermitCancellationReport | `/api/ImportPermitCancellationReport` | `Frontend/src/Report/Page/ImportPermitCancellationReport.tsx` | Completed |
| 108 | ImportPermitCompanyListReport | `/api/ImportPermitCompanyListReport` | `Frontend/src/Report/Page/ImportPermitCompanyListReport.tsx` | Completed |
| 109 | ImportPermitDailyReportNewPermitReport | `/api/ImportPermitDailyReportNewPermitReport` | `Frontend/src/Report/Page/ImportPermitDailyReportNewPermitReport.tsx` | Completed |
| 110 | ImportPermitDetailReport | `/api/ImportPermitDetailReport` | `Frontend/src/Report/Page/ImportPermitDetailReport.tsx` | Completed |
| 111 | ImportPermitExtensionReport | `/api/ImportPermitExtensionReport` | `Frontend/src/Report/Page/ImportPermitExtensionReport.tsx` | Completed |
| 112 | ImportPermitNewReportNewReport | `/api/ImportPermitNewReportNewReport` | `Frontend/src/Report/Page/ImportPermitNewReportNewReport.tsx` | Completed |
| 113 | ImportPermitVoucherReport | `/api/ImportPermitVoucherReport` | `Frontend/src/Report/Page/ImportPermitVoucherReport.tsx` | Completed |
| 114 | ListOfCompany | `/api/ListOfCompany` | `Frontend/src/Report/Page/ListOfCompany.tsx` | Completed |
| 115 | ListOfDirectorsByCompanyRegistrationNo | `/api/ListOfDirectorsByCompanyRegistrationNo` | `Frontend/src/Report/Page/ListOfDirectorsByCompanyRegistrationNo.tsx` | Completed |
| 116 | ListOfDirectors | `/api/ListOfDirectors` | `Frontend/src/Report/Page/ListOfDirectors.tsx` | Completed |
| 117 | ListOfTopCapitalCompany | `/api/ListOfTopCapitalCompany` | `Frontend/src/Report/Page/ListOfTopCapitalCompany.tsx` | Completed |
| 118 | ListOfValidAndInvalidCompany | `/api/ListOfValidAndInvalidCompany` | `Frontend/src/Report/Page/ListOfValidAndInvalidCompany.tsx` | Completed |
| 119 | MemberRegistrationReport | `/api/MemberRegistrationReport` | `Frontend/src/Report/Page/MemberRegistrationReport.tsx` | Completed |
| 120 | MPUReport | `/api/MPUReport` | `Frontend/src/Report/Page/MPUReport.tsx` | Completed |
| 121 | MPUReportV3 | `/api/MPUReportV3` | `Frontend/src/Report/Page/MPUReportV3.tsx` | Completed |
| 122 | OnlineFeesReport | `/api/OnlineFeesReport` | `Frontend/src/Report/Page/OnlineFeesReport.tsx` | Completed |
| 123 | PaThaKaRegisteredBusinessOrganizationReport | `/api/PaThaKaRegisteredBusinessOrganizationReport` | `Frontend/src/Report/Page/PaThaKaRegisteredBusinessOrganizationReport.tsx` | Completed |
| 124 | RegistrationByBusinessType | `/api/RegistrationByBusinessType` | `Frontend/src/Report/Page/RegistrationByBusinessType.tsx` | Completed |
| 125 | RegistrationByVoucher | `/api/RegistrationByVoucher` | `Frontend/src/Report/Page/RegistrationByVoucher.tsx` | Completed |
| 126 | WholeSaleSummaryReport | `/api/WholeSaleSummaryReport` | `Frontend/src/Report/Page/WholeSaleSummaryReport.tsx` | Completed |
| 127 | WholeSaleDetailReport | `/api/WholeSaleDetailReport` | `Frontend/src/Report/Page/WholeSaleDetailReport.tsx` | Completed |
| 128 | WholeSaleRegistrationByVoucher | `/api/WholeSaleRegistrationByVoucher` | `Frontend/src/Report/Page/WholeSaleRegistrationByVoucher.tsx` | Completed |
| 129 | RetailSummaryReport | `/api/RetailSummaryReport` | `Frontend/src/Report/Page/RetailSummaryReport.tsx` | Completed |
| 130 | RetailDetailReport | `/api/RetailDetailReport` | `Frontend/src/Report/Page/RetailDetailReport.tsx` | Completed |
| 131 | RetailRegistrationByVoucher | `/api/RetailRegistrationByVoucher` | `Frontend/src/Report/Page/RetailRegistrationByVoucher.tsx` | Completed |
| 132 | WholeSaleAndRetailSummaryReport | `/api/WholeSaleAndRetailSummaryReport` | `Frontend/src/Report/Page/WholeSaleAndRetailSummaryReport.tsx` | Completed |
| 133 | WholeSaleAndRetailDetailReport | `/api/WholeSaleAndRetailDetailReport` | `Frontend/src/Report/Page/WholeSaleAndRetailDetailReport.tsx` | Completed |
| 134 | WholeSaleAndRetailRegistrationByVoucher | `/api/WholeSaleAndRetailRegistrationByVoucher` | `Frontend/src/Report/Page/WholeSaleAndRetailRegistrationByVoucher.tsx` | Completed |
| 135 | AlcoholicBeveragesImportationSummaryReport | `/api/AlcoholicBeveragesImportationSummaryReport` | `Frontend/src/Report/Page/AlcoholicBeveragesImportationSummaryReport.tsx` | Completed |
| 136 | AlcoholicBeveragesImportationDetailReport | `/api/AlcoholicBeveragesImportationDetailReport` | `Frontend/src/Report/Page/AlcoholicBeveragesImportationDetailReport.tsx` | Completed |
| 137 | AlcoholicBeveragesImportationRegistrationByVoucher | `/api/AlcoholicBeveragesImportationRegistrationByVoucher` | `Frontend/src/Report/Page/AlcoholicBeveragesImportationRegistrationByVoucher.tsx` | Completed |
| 138 | DutyFreeShopSummaryReport | `/api/DutyFreeShopSummaryReport` | `Frontend/src/Report/Page/DutyFreeShopSummaryReport.tsx` | Completed |
| 139 | DutyFreeShopDetailReport | `/api/DutyFreeShopDetailReport` | `Frontend/src/Report/Page/DutyFreeShopDetailReport.tsx` | Completed |
| 140 | DutyFreeShopRegistrationByVoucher | `/api/DutyFreeShopRegistrationByVoucher` | `Frontend/src/Report/Page/DutyFreeShopRegistrationByVoucher.tsx` | Completed |
| 141 | ReExportSummaryReport | `/api/ReExportSummaryReport` | `Frontend/src/Report/Page/ReExportSummaryReport.tsx` | Completed |
| 142 | ReExportDetailReport | `/api/ReExportDetailReport` | `Frontend/src/Report/Page/ReExportDetailReport.tsx` | Completed |
| 143 | BusinessServiceAgencySummaryReport | `/api/BusinessServiceAgencySummaryReport` | `Frontend/src/Report/Page/BusinessServiceAgencySummaryReport.tsx` | Completed |
| 144 | BusinessServiceAgencyDetailReport | `/api/BusinessServiceAgencyDetailReport` | `Frontend/src/Report/Page/BusinessServiceAgencyDetailReport.tsx` | Completed |
| 145 | BusinessServiceAgencyRegistrationByVoucher | `/api/BusinessServiceAgencyRegistrationByVoucher` | `Frontend/src/Report/Page/BusinessServiceAgencyRegistrationByVoucher.tsx` | Completed |
| 146 | SaleCenterSummaryReport | `/api/SaleCenterSummaryReport` | `Frontend/src/Report/Page/SaleCenterSummaryReport.tsx` | Completed |
| 147 | SaleCenterDetailReport | `/api/SaleCenterDetailReport` | `Frontend/src/Report/Page/SaleCenterDetailReport.tsx` | Completed |
| 148 | SaleCenterRegistrationByVoucher | `/api/SaleCenterRegistrationByVoucher` | `Frontend/src/Report/Page/SaleCenterRegistrationByVoucher.tsx` | Completed |
| 149 | ShowRoomSummaryReport | `/api/ShowRoomSummaryReport` | `Frontend/src/Report/Page/ShowRoomSummaryReport.tsx` | Completed |
| 150 | ShowRoomDetailReport | `/api/ShowRoomDetailReport` | `Frontend/src/Report/Page/ShowRoomDetailReport.tsx` | Completed |
| 151 | ShowRoomRegistrationByVoucher | `/api/ShowRoomRegistrationByVoucher` | `Frontend/src/Report/Page/ShowRoomRegistrationByVoucher.tsx` | Completed |
| 152 | EVCycleShowRoomSummaryReport | `/api/EVCycleShowRoomSummaryReport` | `Frontend/src/Report/Page/EVCycleShowRoomSummaryReport.tsx` | Completed |
| 153 | EVCycleShowRoomDetailReport | `/api/EVCycleShowRoomDetailReport` | `Frontend/src/Report/Page/EVCycleShowRoomDetailReport.tsx` | Completed |
| 154 | EVCycleShowRoomRegistrationByVoucher | `/api/EVCycleShowRoomRegistrationByVoucher` | `Frontend/src/Report/Page/EVCycleShowRoomRegistrationByVoucher.tsx` | Completed |
| 155 | EVShowRoomSummaryReport | `/api/EVShowRoomSummaryReport` | `Frontend/src/Report/Page/EVShowRoomSummaryReport.tsx` | Completed |
| 156 | EVShowRoomDetailReport | `/api/EVShowRoomDetailReport` | `Frontend/src/Report/Page/EVShowRoomDetailReport.tsx` | Completed |
| 157 | EVShowRoomRegistrationByVoucher | `/api/EVShowRoomRegistrationByVoucher` | `Frontend/src/Report/Page/EVShowRoomRegistrationByVoucher.tsx` | Completed |
| 158 | OGARecommendationReport | `/api/OGARecommendationReport` | `Frontend/src/Report/Page/OGARecommendationReport.tsx` | Completed |

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

- Total report API controllers: 125
- Frontend completed: 1
- Frontend remaining: 124

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

## Frontend Report Tracker

| No. | Controller | API Route | Page File | Frontend Status |
| --: | ---------- | --------- | --------- | --------------- |
| 1 | AccountSummaryReport | `/api/AccountSummaryReport` | `Frontend/src/Report/Page/AccountSummaryReport.tsx` | To Do |
| 2 | BorderExportLicenceActualAmendmentReport | `/api/BorderExportLicenceActualAmendmentReport` | `Frontend/src/Report/Page/BorderExportLicenceActualAmendmentReport.tsx` | To Do |
| 3 | BorderExportLicenceAmendmentReport | `/api/BorderExportLicenceAmendmentReport` | `Frontend/src/Report/Page/BorderExportLicenceAmendmentReport.tsx` | To Do |
| 4 | BorderExportLicenceByHSCodeReport | `/api/BorderExportLicenceByHSCodeReport` | `Frontend/src/Report/Page/BorderExportLicenceByHSCodeReport.tsx` | To Do |
| 5 | BorderExportLicenceByMethodReport | `/api/BorderExportLicenceByMethodReport` | `Frontend/src/Report/Page/BorderExportLicenceByMethodReport.tsx` | To Do |
| 6 | BorderExportLicenceBySectionReport | `/api/BorderExportLicenceBySectionReport` | `Frontend/src/Report/Page/BorderExportLicenceBySectionReport.tsx` | To Do |
| 7 | BorderExportLicenceBySellerCountryReport | `/api/BorderExportLicenceBySellerCountryReport` | `Frontend/src/Report/Page/BorderExportLicenceBySellerCountryReport.tsx` | To Do |
| 8 | BorderExportLicenceCancellationReport | `/api/BorderExportLicenceCancellationReport` | `Frontend/src/Report/Page/BorderExportLicenceCancellationReport.tsx` | To Do |
| 9 | BorderExportLicenceCompanyListReport | `/api/BorderExportLicenceCompanyListReport` | `Frontend/src/Report/Page/BorderExportLicenceCompanyListReport.tsx` | To Do |
| 10 | BorderExportLicenceDailyReportNewLicenceReport | `/api/BorderExportLicenceDailyReportNewLicenceReport` | `Frontend/src/Report/Page/BorderExportLicenceDailyReportNewLicenceReport.tsx` | To Do |
| 11 | BorderExportLicenceDetailReport | `/api/BorderExportLicenceDetailReport` | `Frontend/src/Report/Page/BorderExportLicenceDetailReport.tsx` | To Do |
| 12 | BorderExportLicenceExtensionReport | `/api/BorderExportLicenceExtensionReport` | `Frontend/src/Report/Page/BorderExportLicenceExtensionReport.tsx` | To Do |
| 13 | BorderExportLicenceNewReportNewReport | `/api/BorderExportLicenceNewReportNewReport` | `Frontend/src/Report/Page/BorderExportLicenceNewReportNewReport.tsx` | To Do |
| 14 | BorderExportLicenceTotalValueLicencesReport | `/api/BorderExportLicenceTotalValueLicencesReport` | `Frontend/src/Report/Page/BorderExportLicenceTotalValueLicencesReport.tsx` | To Do |
| 15 | BorderExportLicenceVoucherReport | `/api/BorderExportLicenceVoucherReport` | `Frontend/src/Report/Page/BorderExportLicenceVoucherReport.tsx` | To Do |
| 16 | BorderExportPermitActualAmendmentReport | `/api/BorderExportPermitActualAmendmentReport` | `Frontend/src/Report/Page/BorderExportPermitActualAmendmentReport.tsx` | To Do |
| 17 | BorderExportPermitAmendmentReport | `/api/BorderExportPermitAmendmentReport` | `Frontend/src/Report/Page/BorderExportPermitAmendmentReport.tsx` | To Do |
| 18 | BorderExportPermitByHSCodeReport | `/api/BorderExportPermitByHSCodeReport` | `Frontend/src/Report/Page/BorderExportPermitByHSCodeReport.tsx` | To Do |
| 19 | BorderExportPermitBySectionReport | `/api/BorderExportPermitBySectionReport` | `Frontend/src/Report/Page/BorderExportPermitBySectionReport.tsx` | To Do |
| 20 | BorderExportPermitBySellerCountryReport | `/api/BorderExportPermitBySellerCountryReport` | `Frontend/src/Report/Page/BorderExportPermitBySellerCountryReport.tsx` | To Do |
| 21 | BorderExportPermitCancellationReport | `/api/BorderExportPermitCancellationReport` | `Frontend/src/Report/Page/BorderExportPermitCancellationReport.tsx` | To Do |
| 22 | BorderExportPermitCompanyListReport | `/api/BorderExportPermitCompanyListReport` | `Frontend/src/Report/Page/BorderExportPermitCompanyListReport.tsx` | To Do |
| 23 | BorderExportPermitDailyReportNewPermitReport | `/api/BorderExportPermitDailyReportNewPermitReport` | `Frontend/src/Report/Page/BorderExportPermitDailyReportNewPermitReport.tsx` | To Do |
| 24 | BorderExportPermitDetailReport | `/api/BorderExportPermitDetailReport` | `Frontend/src/Report/Page/BorderExportPermitDetailReport.tsx` | To Do |
| 25 | BorderExportPermitExtensionReport | `/api/BorderExportPermitExtensionReport` | `Frontend/src/Report/Page/BorderExportPermitExtensionReport.tsx` | To Do |
| 26 | BorderExportPermitNewReportNewReport | `/api/BorderExportPermitNewReportNewReport` | `Frontend/src/Report/Page/BorderExportPermitNewReportNewReport.tsx` | To Do |
| 27 | BorderExportPermitVoucherReport | `/api/BorderExportPermitVoucherReport` | `Frontend/src/Report/Page/BorderExportPermitVoucherReport.tsx` | To Do |
| 28 | BorderImportLicenceActualAmendmentReport | `/api/BorderImportLicenceActualAmendmentReport` | `Frontend/src/Report/Page/BorderImportLicenceActualAmendmentReport.tsx` | To Do |
| 29 | BorderImportLicenceAmendmentReport | `/api/BorderImportLicenceAmendmentReport` | `Frontend/src/Report/Page/BorderImportLicenceAmendmentReport.tsx` | To Do |
| 30 | BorderImportLicenceByHSCodeReport | `/api/BorderImportLicenceByHSCodeReport` | `Frontend/src/Report/Page/BorderImportLicenceByHSCodeReport.tsx` | To Do |
| 31 | BorderImportLicenceByMethodReport | `/api/BorderImportLicenceByMethodReport` | `Frontend/src/Report/Page/BorderImportLicenceByMethodReport.tsx` | To Do |
| 32 | BorderImportLicenceBySectionReport | `/api/BorderImportLicenceBySectionReport` | `Frontend/src/Report/Page/BorderImportLicenceBySectionReport.tsx` | To Do |
| 33 | BorderImportLicenceBySellerCountryReport | `/api/BorderImportLicenceBySellerCountryReport` | `Frontend/src/Report/Page/BorderImportLicenceBySellerCountryReport.tsx` | To Do |
| 34 | BorderImportLicenceCancellationReport | `/api/BorderImportLicenceCancellationReport` | `Frontend/src/Report/Page/BorderImportLicenceCancellationReport.tsx` | To Do |
| 35 | BorderImportLicenceCompanyListReport | `/api/BorderImportLicenceCompanyListReport` | `Frontend/src/Report/Page/BorderImportLicenceCompanyListReport.tsx` | To Do |
| 36 | BorderImportLicenceDailyReportNewLicenceReport | `/api/BorderImportLicenceDailyReportNewLicenceReport` | `Frontend/src/Report/Page/BorderImportLicenceDailyReportNewLicenceReport.tsx` | To Do |
| 37 | BorderImportLicenceDetailReport | `/api/BorderImportLicenceDetailReport` | `Frontend/src/Report/Page/BorderImportLicenceDetailReport.tsx` | To Do |
| 38 | BorderImportLicenceDetailReportPending | `/api/BorderImportLicenceDetailReportPending` | `Frontend/src/Report/Page/BorderImportLicenceDetailReportPending.tsx` | To Do |
| 39 | BorderImportLicenceExtensionReport | `/api/BorderImportLicenceExtensionReport` | `Frontend/src/Report/Page/BorderImportLicenceExtensionReport.tsx` | To Do |
| 40 | BorderImportLicenceNewReportNewReport | `/api/BorderImportLicenceNewReportNewReport` | `Frontend/src/Report/Page/BorderImportLicenceNewReportNewReport.tsx` | To Do |
| 41 | BorderImportLicencePendingReport | `/api/BorderImportLicencePendingReport` | `Frontend/src/Report/Page/BorderImportLicencePendingReport.tsx` | To Do |
| 42 | BorderImportLicenceTotalValueLicencesReport | `/api/BorderImportLicenceTotalValueLicencesReport` | `Frontend/src/Report/Page/BorderImportLicenceTotalValueLicencesReport.tsx` | To Do |
| 43 | BorderImportLicenceVoucherReport | `/api/BorderImportLicenceVoucherReport` | `Frontend/src/Report/Page/BorderImportLicenceVoucherReport.tsx` | To Do |
| 44 | BorderImportPermitActualAmendmentReport | `/api/BorderImportPermitActualAmendmentReport` | `Frontend/src/Report/Page/BorderImportPermitActualAmendmentReport.tsx` | To Do |
| 45 | BorderImportPermitAmendmentReport | `/api/BorderImportPermitAmendmentReport` | `Frontend/src/Report/Page/BorderImportPermitAmendmentReport.tsx` | To Do |
| 46 | BorderImportPermitByHSCodeReport | `/api/BorderImportPermitByHSCodeReport` | `Frontend/src/Report/Page/BorderImportPermitByHSCodeReport.tsx` | To Do |
| 47 | BorderImportPermitBySectionReport | `/api/BorderImportPermitBySectionReport` | `Frontend/src/Report/Page/BorderImportPermitBySectionReport.tsx` | To Do |
| 48 | BorderImportPermitBySellerCountryReport | `/api/BorderImportPermitBySellerCountryReport` | `Frontend/src/Report/Page/BorderImportPermitBySellerCountryReport.tsx` | To Do |
| 49 | BorderImportPermitCancellationReport | `/api/BorderImportPermitCancellationReport` | `Frontend/src/Report/Page/BorderImportPermitCancellationReport.tsx` | To Do |
| 50 | BorderImportPermitCompanyListReport | `/api/BorderImportPermitCompanyListReport` | `Frontend/src/Report/Page/BorderImportPermitCompanyListReport.tsx` | To Do |
| 51 | BorderImportPermitDailyReportNewPermitReport | `/api/BorderImportPermitDailyReportNewPermitReport` | `Frontend/src/Report/Page/BorderImportPermitDailyReportNewPermitReport.tsx` | To Do |
| 52 | BorderImportPermitDetailReport | `/api/BorderImportPermitDetailReport` | `Frontend/src/Report/Page/BorderImportPermitDetailReport.tsx` | To Do |
| 53 | BorderImportPermitExtensionReport | `/api/BorderImportPermitExtensionReport` | `Frontend/src/Report/Page/BorderImportPermitExtensionReport.tsx` | To Do |
| 54 | BorderImportPermitNewReportNewReport | `/api/BorderImportPermitNewReportNewReport` | `Frontend/src/Report/Page/BorderImportPermitNewReportNewReport.tsx` | To Do |
| 55 | BorderImportPermitVoucherReport | `/api/BorderImportPermitVoucherReport` | `Frontend/src/Report/Page/BorderImportPermitVoucherReport.tsx` | To Do |
| 56 | CardListsByCompanyRegistrationNumber | `/api/CardListsByCompanyRegistrationNumber` | `Frontend/src/Report/Page/CardListsByCompanyRegistrationNumber.tsx` | To Do |
| 57 | ChequeNoReport | `/api/ChequeNoReport` | `Frontend/src/Report/Page/ChequeNoReport.tsx` | To Do |
| 58 | CompanyProfile | `/api/CompanyProfile` | `Frontend/src/Report/Page/CompanyProfile.tsx` | To Do |
| 59 | EIRCardBindReport | `/api/EIRCardBindReport` | `Frontend/src/Report/Page/EIRCardBindReport.tsx` | To Do |
| 60 | ExportLicenceActualAmendmentReport | `/api/ExportLicenceActualAmendmentReport` | `Frontend/src/Report/Page/ExportLicenceActualAmendmentReport.tsx` | To Do |
| 61 | ExportLicenceAmendmentReport | `/api/ExportLicenceAmendmentReport` | `Frontend/src/Report/Page/ExportLicenceAmendmentReport.tsx` | To Do |
| 62 | ExportLicenceByHSCodeReport | `/api/ExportLicenceByHSCodeReport` | `Frontend/src/Report/Page/ExportLicenceByHSCodeReport.tsx` | To Do |
| 63 | ExportLicenceByMethodReport | `/api/ExportLicenceByMethodReport` | `Frontend/src/Report/Page/ExportLicenceByMethodReport.tsx` | To Do |
| 64 | ExportLicenceBySectionReport | `/api/ExportLicenceBySectionReport` | `Frontend/src/Report/Page/ExportLicenceBySectionReport.tsx` | To Do |
| 65 | ExportLicenceBySellerCountryReport | `/api/ExportLicenceBySellerCountryReport` | `Frontend/src/Report/Page/ExportLicenceBySellerCountryReport.tsx` | To Do |
| 66 | ExportLicenceCancellationReport | `/api/ExportLicenceCancellationReport` | `Frontend/src/Report/Page/ExportLicenceCancellationReport.tsx` | To Do |
| 67 | ExportLicenceCompanyListReport | `/api/ExportLicenceCompanyListReport` | `Frontend/src/Report/Page/ExportLicenceCompanyListReport.tsx` | To Do |
| 68 | ExportLicenceDailyReportNewLicenceReport | `/api/ExportLicenceDailyReportNewLicenceReport` | `Frontend/src/Report/Page/ExportLicenceDailyReportNewLicenceReport.tsx` | To Do |
| 69 | ExportLicenceDetailReport | `/api/ExportLicenceDetailReport` | `Frontend/src/Report/Page/ExportLicenceDetailReport.tsx` | To Do |
| 70 | ExportLicenceExtensionReport | `/api/ExportLicenceExtensionReport` | `Frontend/src/Report/Page/ExportLicenceExtensionReport.tsx` | To Do |
| 71 | ExportLicenceNewReportNewReport | `/api/ExportLicenceNewReportNewReport` | `Frontend/src/Report/Page/ExportLicenceNewReportNewReport.tsx` | To Do |
| 72 | ExportLicenceTotalValueLicencesReport | `/api/ExportLicenceTotalValueLicencesReport` | `Frontend/src/Report/Page/ExportLicenceTotalValueLicencesReport.tsx` | To Do |
| 73 | ExportLicenceVoucherReport | `/api/ExportLicenceVoucherReport` | `Frontend/src/Report/Page/ExportLicenceVoucherReport.tsx` | To Do |
| 74 | ExportPermitActualAmendmentReport | `/api/ExportPermitActualAmendmentReport` | `Frontend/src/Report/Page/ExportPermitActualAmendmentReport.tsx` | To Do |
| 75 | ExportPermitAmendmentReport | `/api/ExportPermitAmendmentReport` | `Frontend/src/Report/Page/ExportPermitAmendmentReport.tsx` | To Do |
| 76 | ExportPermitByHSCodeReport | `/api/ExportPermitByHSCodeReport` | `Frontend/src/Report/Page/ExportPermitByHSCodeReport.tsx` | To Do |
| 77 | ExportPermitBySectionReport | `/api/ExportPermitBySectionReport` | `Frontend/src/Report/Page/ExportPermitBySectionReport.tsx` | To Do |
| 78 | ExportPermitBySellerCountryReport | `/api/ExportPermitBySellerCountryReport` | `Frontend/src/Report/Page/ExportPermitBySellerCountryReport.tsx` | To Do |
| 79 | ExportPermitCancellationReport | `/api/ExportPermitCancellationReport` | `Frontend/src/Report/Page/ExportPermitCancellationReport.tsx` | To Do |
| 80 | ExportPermitCompanyListReport | `/api/ExportPermitCompanyListReport` | `Frontend/src/Report/Page/ExportPermitCompanyListReport.tsx` | To Do |
| 81 | ExportPermitDailyReportNewPermitReport | `/api/ExportPermitDailyReportNewPermitReport` | `Frontend/src/Report/Page/ExportPermitDailyReportNewPermitReport.tsx` | To Do |
| 82 | ExportPermitDetailReport | `/api/ExportPermitDetailReport` | `Frontend/src/Report/Page/ExportPermitDetailReport.tsx` | To Do |
| 83 | ExportPermitExtensionReport | `/api/ExportPermitExtensionReport` | `Frontend/src/Report/Page/ExportPermitExtensionReport.tsx` | To Do |
| 84 | ExportPermitNewReportNewReport | `/api/ExportPermitNewReportNewReport` | `Frontend/src/Report/Page/ExportPermitNewReportNewReport.tsx` | To Do |
| 85 | ExportPermitVoucherReport | `/api/ExportPermitVoucherReport` | `Frontend/src/Report/Page/ExportPermitVoucherReport.tsx` | To Do |
| 86 | ImportLicenceActualAmendmentReport | `/api/ImportLicenceActualAmendmentReport` | `Frontend/src/Report/Page/ImportLicenceActualAmendmentReport.tsx` | To Do |
| 87 | ImportLicenceAmendmentReport | `/api/ImportLicenceAmendmentReport` | `Frontend/src/Report/Page/ImportLicenceAmendmentReport.tsx` | To Do |
| 88 | ImportLicenceByHSCodeReport | `/api/ImportLicenceByHSCodeReport` | `Frontend/src/Report/Page/ImportLicenceByHSCodeReport.tsx` | To Do |
| 89 | ImportLicenceByMethodReport | `/api/ImportLicenceByMethodReport` | `Frontend/src/Report/Page/ImportLicenceByMethodReport.tsx` | To Do |
| 90 | ImportLicenceBySectionReport | `/api/ImportLicenceBySectionReport` | `Frontend/src/Report/Page/ImportLicenceBySectionReport.tsx` | To Do |
| 91 | ImportLicenceBySellerCountryReport | `/api/ImportLicenceBySellerCountryReport` | `Frontend/src/Report/Page/ImportLicenceBySellerCountryReport.tsx` | To Do |
| 92 | ImportLicenceCancellationReport | `/api/ImportLicenceCancellationReport` | `Frontend/src/Report/Page/ImportLicenceCancellationReport.tsx` | To Do |
| 93 | ImportLicenceCompanyListReport | `/api/ImportLicenceCompanyListReport` | `Frontend/src/Report/Page/ImportLicenceCompanyListReport.tsx` | To Do |
| 94 | ImportLicenceDailyReportNewLicenceReport | `/api/ImportLicenceDailyReportNewLicenceReport` | `Frontend/src/Report/Page/ImportLicenceDailyReportNewLicenceReport.tsx` | To Do |
| 95 | ImportLicenceDetailReport | `/api/ImportLicenceDetailReport` | `Frontend/src/Report/Page/ImportLicenceDetailReport.tsx` | To Do |
| 96 | ImportLicenceDetailReportPending | `/api/ImportLicenceDetailReportPending` | `Frontend/src/Report/Page/ImportLicenceDetailReportPending.tsx` | To Do |
| 97 | ImportLicenceExtensionReport | `/api/ImportLicenceExtensionReport` | `Frontend/src/Report/Page/ImportLicenceExtensionReport.tsx` | To Do |
| 98 | ImportLicenceNewReportNewReport | `/api/ImportLicenceNewReportNewReport` | `Frontend/src/Report/Page/ImportLicenceNewReportNewReport.tsx` | To Do |
| 99 | ImportLicencePendingReport | `/api/ImportLicencePendingReport` | `Frontend/src/Report/Page/ImportLicencePendingReport.tsx` | To Do |
| 100 | ImportLicenceTotalValueLicencesReport | `/api/ImportLicenceTotalValueLicencesReport` | `Frontend/src/Report/Page/ImportLicenceTotalValueLicencesReport.tsx` | To Do |
| 101 | ImportLicenceVoucherReport | `/api/ImportLicenceVoucherReport` | `Frontend/src/Report/Page/ImportLicenceVoucherReport.tsx` | To Do |
| 102 | ImportPermitActualAmendmentReport | `/api/ImportPermitActualAmendmentReport` | `Frontend/src/Report/Page/ImportPermitActualAmendmentReport.tsx` | To Do |
| 103 | ImportPermitAmendmentReport | `/api/ImportPermitAmendmentReport` | `Frontend/src/Report/Page/ImportPermitAmendmentReport.tsx` | To Do |
| 104 | ImportPermitByHSCodeReport | `/api/ImportPermitByHSCodeReport` | `Frontend/src/Report/Page/ImportPermitByHSCodeReport.tsx` | To Do |
| 105 | ImportPermitBySectionReport | `/api/ImportPermitBySectionReport` | `Frontend/src/Report/Page/ImportPermitBySectionReport.tsx` | To Do |
| 106 | ImportPermitBySellerCountryReport | `/api/ImportPermitBySellerCountryReport` | `Frontend/src/Report/Page/ImportPermitBySellerCountryReport.tsx` | To Do |
| 107 | ImportPermitCancellationReport | `/api/ImportPermitCancellationReport` | `Frontend/src/Report/Page/ImportPermitCancellationReport.tsx` | To Do |
| 108 | ImportPermitCompanyListReport | `/api/ImportPermitCompanyListReport` | `Frontend/src/Report/Page/ImportPermitCompanyListReport.tsx` | To Do |
| 109 | ImportPermitDailyReportNewPermitReport | `/api/ImportPermitDailyReportNewPermitReport` | `Frontend/src/Report/Page/ImportPermitDailyReportNewPermitReport.tsx` | To Do |
| 110 | ImportPermitDetailReport | `/api/ImportPermitDetailReport` | `Frontend/src/Report/Page/ImportPermitDetailReport.tsx` | To Do |
| 111 | ImportPermitExtensionReport | `/api/ImportPermitExtensionReport` | `Frontend/src/Report/Page/ImportPermitExtensionReport.tsx` | To Do |
| 112 | ImportPermitNewReportNewReport | `/api/ImportPermitNewReportNewReport` | `Frontend/src/Report/Page/ImportPermitNewReportNewReport.tsx` | To Do |
| 113 | ImportPermitVoucherReport | `/api/ImportPermitVoucherReport` | `Frontend/src/Report/Page/ImportPermitVoucherReport.tsx` | To Do |
| 114 | ListOfCompany | `/api/ListOfCompany` | `Frontend/src/Report/Page/ListOfCompany.tsx` | To Do |
| 115 | ListOfDirectorsByCompanyRegistrationNo | `/api/ListOfDirectorsByCompanyRegistrationNo` | `Frontend/src/Report/Page/ListOfDirectorsByCompanyRegistrationNo.tsx` | To Do |
| 116 | ListOfDirectors | `/api/ListOfDirectors` | `Frontend/src/Report/Page/ListOfDirectors.tsx` | To Do |
| 117 | ListOfTopCapitalCompany | `/api/ListOfTopCapitalCompany` | `Frontend/src/Report/Page/ListOfTopCapitalCompany.tsx` | To Do |
| 118 | ListOfValidAndInvalidCompany | `/api/ListOfValidAndInvalidCompany` | `Frontend/src/Report/Page/ListOfValidAndInvalidCompany.tsx` | To Do |
| 119 | MemberRegistrationReport | `/api/MemberRegistrationReport` | `Frontend/src/Report/Page/MemberRegistrationReport.tsx` | Completed |
| 120 | MPUReport | `/api/MPUReport` | `Frontend/src/Report/Page/MPUReport.tsx` | To Do |
| 121 | MPUReportV3 | `/api/MPUReportV3` | `Frontend/src/Report/Page/MPUReportV3.tsx` | To Do |
| 122 | OnlineFeesReport | `/api/OnlineFeesReport` | `Frontend/src/Report/Page/OnlineFeesReport.tsx` | To Do |
| 123 | PaThaKaRegisteredBusinessOrganizationReport | `/api/PaThaKaRegisteredBusinessOrganizationReport` | `Frontend/src/Report/Page/PaThaKaRegisteredBusinessOrganizationReport.tsx` | To Do |
| 124 | RegistrationByBusinessType | `/api/RegistrationByBusinessType` | `Frontend/src/Report/Page/RegistrationByBusinessType.tsx` | To Do |
| 125 | RegistrationByVoucher | `/api/RegistrationByVoucher` | `Frontend/src/Report/Page/RegistrationByVoucher.tsx` | To Do |

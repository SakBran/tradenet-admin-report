/**
 * Company Profile — bespoke Excel presentation spec.
 *
 * `CompanyProfile.tsx` is a hand-built page: its grid is NOT
 * `reportConfigs.CompanyProfile.columns` (that config exists only to register
 * the route/filters — see the note above `reportConfigs.CompanyProfile`). Since the
 * 2026-09-25 complaint it prints the layout the customer sends to the 11
 * ministries: composed company cells merged over each company's director rows,
 * and Name / NRC No. under a "Board of Director" band.
 *
 * A flat spec cannot describe that sheet, so the controller's typed layout draws
 * it (`CompanyProfileController.GetExcelLayout`: merged cells, the banded
 * two-row header, wrapped multi-line cells). This spec still rides on the Excel
 * POST and is the column contract: `ExcelSpecContractTests` compares the typed
 * layout's headers to `rowNumberTitle` + these titles, in order — the band label
 * is not a column, so the leaf titles "Name" and "NRC No." stand for it — and
 * every `dataIndex` must exist on `sp_CompanyProfileReportResult`.
 *
 *   - header lines  → `CompanyProfile.tsx` `reportHeaderLines`
 *   - row number    → the page's "No" (one per company)
 *   - no footer: the page has no totals row.
 */
import { formatLegacyReportDate } from '../../reportPresentation';
import { buildExcelPresentationFromInput } from '../buildExcelPresentation';
import { ExcelPresentationSpec, ExcelSpecColumn } from '../excelTypes';

/** The page's 10 header cells, minus the row-number column, in page order. */
const columns: ExcelSpecColumn[] = [
  {
    // name / reg no / (registration date) — composed by the typed layout.
    key: 'CompanyName',
    dataIndex: 'companyName',
    title: "Company's Name",
  },
  {
    key: 'CompanyAddress',
    dataIndex: 'companyAddress',
    title: 'Address',
  },
  {
    // reg no / "d-M-yyyy to d-M-yyyy" — composed by the typed layout.
    key: 'EirValidity',
    dataIndex: 'eirValidity',
    title: 'EIR No. & Date',
  },
  {
    key: 'BusinessType',
    dataIndex: 'businessType',
    title: 'Type of Organization',
  },
  {
    key: 'PermitBusiness',
    dataIndex: 'permitBusiness',
    title: 'လုပ်ငန်းရည်ရွယ်ချက်',
  },
  {
    key: 'CapitalText',
    dataIndex: 'capitalText',
    title: 'Capital',
  },
  {
    key: 'DirectorName',
    dataIndex: 'directorName',
    title: 'Name',
  },
  {
    key: 'DirectorNrc',
    dataIndex: 'directorNrc',
    title: 'NRC No.',
  },
  {
    key: 'DirectorTitle',
    dataIndex: 'directorTitle',
    title: 'Title',
  },
];

export const buildCompanyProfileExcelSpec = (
  applied: Record<string, unknown>
): ExcelPresentationSpec =>
  buildExcelPresentationFromInput({
    configKey: 'CompanyProfile',
    controllerName: 'CompanyProfile',
    title: 'Company Profile',
    fileName: 'CompanyProfile.xlsx',
    headerLines: [
      'Ministry of Commerce',
      'Directorate of Trade',
      `Company Profile (${formatLegacyReportDate(
        applied.FromDate
      )}) To (${formatLegacyReportDate(applied.ToDate)})`,
    ],
    showRowNumber: true,
    rowNumberTitle: 'No',
    columns,
  });

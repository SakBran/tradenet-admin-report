/**
 * Company Profile — bespoke Excel presentation spec.
 *
 * `CompanyProfile.tsx` is a hand-built page: its grid is NOT
 * `reportConfigs.CompanyProfile.columns` (that config exists only to register
 * the route/filters — see the note at `reportConfigs.ts:6495`), it renders 12
 * hand-written `<th>`s with Myanmar labels and a 2-row director group, plus the
 * two ministry heading lines above the table. So the generic
 * `buildExcelPresentation(config, applied)` would describe columns the UI never
 * shows (rules M2/M4) and drop the page's real header block (M1).
 *
 * This builder mirrors the page exactly:
 *   - header lines  → `CompanyProfile.tsx:339-351` (`reportHeaderLines`)
 *   - row number    → `CompanyProfile.tsx:471` (`<th rowSpan={2}>စဥ်</th>`)
 *   - the 11 data columns, in page order, with the leaf header texts of the
 *     director group → `CompanyProfile.tsx:471-487`
 *   - no footer: the page has no totals row, so no `currencyTotalsColumns` and
 *     no `summaryLines`.
 *
 * Two cells the page COMPOSES cannot be expressed as spec columns — the
 * "ပသက / အမှတ်/ရက်စွဲ" cell prints `CompanyRegistrationNo` above
 * `(CompanyRegistrationDate)` (`CompanyProfile.tsx:513-521`) and
 * "လုပ်ငန်းရည်ရွယ်ချက်" splits `PermitBusiness` on commas onto separate lines
 * (`CompanyProfile.tsx:146-153`). Those need the controller's typed
 * `IExcelReportLayoutProvider` (Contract.md §6/§9); this spec is the column
 * contract that layout must match (`ExcelSpecContractTests` compares a typed
 * layout's header texts to the spec's titles).
 */
import { formatLegacyReportDate } from '../../reportPresentation';
import { buildExcelPresentationFromInput } from '../buildExcelPresentation';
import { ExcelPresentationSpec, ExcelSpecColumn } from '../excelTypes';

/** The page's 12 header cells, minus the row-number column. */
const columns: ExcelSpecColumn[] = [
  {
    key: 'CompanyRegistrationNo',
    dataIndex: 'companyRegistrationNo',
    title: 'ပသက / အမှတ်/ရက်စွဲ',
  },
  {
    key: 'EndDate',
    dataIndex: 'endDate',
    title: 'သက်တမ်းကုန်ဆုံးရက်',
    dataType: 'date',
  },
  {
    key: 'CompanyName',
    dataIndex: 'companyName',
    title: 'ကုမ္ပဏီအမည်',
  },
  {
    // One combined cell, exactly like the page's joinAddress helper
    // (CompanyProfile.tsx:133-144): the row type has no CompanyAddress
    // property, so the fallbacks supply the parts joined with ", ".
    key: 'CompanyAddress',
    dataIndex: 'companyAddress',
    title: 'ကုမ္ပဏီလိပ်စာ',
    fallbackDataIndexes: [
      'unitLevel',
      'streetNumberStreetName',
      'quarterCityTownship',
      'state',
      'country',
      'postalCode',
    ],
  },
  {
    key: 'BusinessType',
    dataIndex: 'businessType',
    title: 'ကုမ္ပဏီအမျိုးအစား',
  },
  {
    key: 'PermitBusiness',
    dataIndex: 'permitBusiness',
    title: 'လုပ်ငန်းရည်ရွယ်ချက်',
  },
  {
    key: 'Capital',
    dataIndex: 'capital',
    title: 'မတည်ငွေရင်း',
    dataType: 'number',
  },
  {
    key: 'ExtensionCount',
    dataIndex: 'extensionCount',
    title: 'ပသက သက်တမ်းတိုး',
    dataType: 'number',
  },
  {
    key: 'DirectorName',
    dataIndex: 'directorName',
    title: 'အမည်',
  },
  {
    key: 'DirectorNrc',
    dataIndex: 'directorNrc',
    title: 'နိုင်ငံသားအမှတ်',
  },
  {
    key: 'DirectorPosition',
    dataIndex: 'directorPosition',
    title: 'ရာထူး',
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
    rowNumberTitle: 'စဥ်',
    columns,
  });

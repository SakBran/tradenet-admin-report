import dayjs from 'dayjs';
import {
  ReportColumnConfig,
  ReportFilterConfig,
  ReportPageConfig,
} from './reportTypes';

const registrationApplyTypeOptions = [
  { label: 'New', value: 'New' },
  { label: 'Amend', value: 'Amend' },
  { label: 'Extension', value: 'Extension' },
  { label: 'Cancel', value: 'Cancel' },
  { label: 'Actual Amend', value: 'Actual Amend' },
];

const detailApplyTypeOptions = [
  ...registrationApplyTypeOptions,
  { label: 'Valid', value: 'Valid' },
  { label: 'Invalid', value: 'Invalid' },
];

const voucherPaymentTypeOptions = [
  { label: '--- All ---', value: '' },
  { label: 'Cash', value: 'Cash' },
  { label: 'MPU', value: 'MPU' },
  { label: 'Citizen Pay', value: 'Citizen Pay' },
];

const dateRangeFilter: ReportFilterConfig = {
  name: 'dateRange',
  label: 'From Date / To Date',
  type: 'dateRange',
  fromName: 'FromDate',
  toName: 'ToDate',
  fromLabel: 'From Date',
  toLabel: 'To Date',
  required: true,
};

const detailFilters: ReportFilterConfig[] = [
  dateRangeFilter,
  {
    name: 'ApplyType',
    label: 'Apply Type',
    type: 'select',
    defaultValue: 'New',
    options: detailApplyTypeOptions,
  },
];

const voucherFilters: ReportFilterConfig[] = [
  dateRangeFilter,
  {
    name: 'PaymentType',
    label: 'Payment Type',
    type: 'select',
    defaultValue: '',
    options: voucherPaymentTypeOptions,
  },
  {
    name: 'ApplyType',
    label: 'Apply Type',
    type: 'select',
    defaultValue: 'New',
    options: registrationApplyTypeOptions,
  },
];

// Form Type dropdowns. Values are the exact DB `RegistrationType` strings
// (verified against TradeNetDB); '--- All ---' (value '') shows every sub-type
// of that report's family.
const formTypeFilter = (
  options: { label: string; value: string }[]
): ReportFilterConfig => ({
  name: 'FormType',
  label: 'Form Type',
  type: 'select',
  defaultValue: '',
  options: [{ label: '--- All ---', value: '' }, ...options],
});

const showRoomFormTypeFilter = formTypeFilter([
  {
    label: 'Show Room for Brand New Motor Vehicles',
    value: 'Show Room for Brand New Motor Vehicles',
  },
  {
    label: 'Show Room for Machinery and Mechanical',
    value: 'Show Room for Machinery and Mechanical',
  },
]);
const saleCenterFormTypeFilter = formTypeFilter([
  {
    label: 'Sale Center for Motor Vehicles',
    value: 'Sale Center for Motor Vehicles',
  },
  {
    label: 'Sale Center for Commercial Vehicles',
    value: 'Sale Center for Commercial Vehicles',
  },
]);
const evShowRoomFormTypeFilter = formTypeFilter([
  {
    label: 'Show Room for Electric Vehicles',
    value: 'Show Room for Electric Vehicles',
  },
]);
const evCycleShowRoomFormTypeFilter = formTypeFilter([
  {
    label: 'Show Room for Electric Cycles',
    value: 'Show Room for Electric Cycles',
  },
]);

// Inserts the Form Type dropdown right after the date range (before ApplyType /
// PaymentType), matching the legacy filter-form ordering.
const withFormType = (
  config: ReportPageConfig,
  filter: ReportFilterConfig
): ReportPageConfig => ({
  ...config,
  filters: [config.filters[0], filter, ...config.filters.slice(1)],
});

const lowerFirst = (value: string) =>
  value.length ? value[0].toLowerCase() + value.slice(1) : value;

const column = (
  key: string,
  title: string,
  dataType?: ReportColumnConfig['dataType']
): ReportColumnConfig => ({
  key,
  dataIndex: lowerFirst(key),
  title,
  dataType,
});

const addressColumn = (
  key: string,
  title: string,
  prefix = '',
  suffix = ''
): ReportColumnConfig => ({
  ...column(key, title),
  fallbackDataIndexes: [
    `${prefix}${prefix ? 'UnitLevel' : 'unitLevel'}${suffix}`,
    `${prefix}${prefix ? 'StreetNumberStreetName' : 'streetNumberStreetName'}${suffix}`,
    `${prefix}${prefix ? 'QuarterCityTownship' : 'quarterCityTownship'}${suffix}`,
    `${prefix}${prefix ? 'State' : 'state'}${suffix}`,
    `${prefix}${prefix ? 'Country' : 'country'}${suffix}`,
    `${prefix}${prefix ? 'PostalCode' : 'postalCode'}${suffix}`,
  ],
});

// The backend result property is `NRCNo`; ASP.NET's camelCase JSON policy emits
// it as `nrcNo` (the leading acronym run is lowercased), NOT lowerFirst('NRCNo')
// === 'nRCNo'. Pin the dataIndex to the serialized key so the column is populated.
// (FL11/FL4/FL5NRCNo are unaffected — the digit breaks the acronym run.)
const nrcColumn: ReportColumnConfig = { ...column('NRCNo', 'NRC No'), dataIndex: 'nrcNo' };

// --- Legacy RDLC in-grid report header (centered lines above the grid) ---
const formatLegacyReportDate = (value: unknown) => {
  const parsed = dayjs(String(value ?? ''));
  return parsed.isValid() ? parsed.format('DD/MM/YYYY') : String(value ?? '');
};

const dateRangeSubtitle = (filters: Record<string, unknown>) =>
  `(${formatLegacyReportDate(filters.FromDate)}) To (${formatLegacyReportDate(filters.ToDate)})`;

// FormType-driven reports (Show Room / Sale Center): prefix the selected sub-type
// (blank when '--- All ---'), matching the legacy per-FormType report title.
const formTypeSubtitle = (filters: Record<string, unknown>) => {
  const formType = String(filters.FormType ?? '').trim();
  return formType
    ? `${formType} ${dateRangeSubtitle(filters)}`
    : dateRangeSubtitle(filters);
};

type SubtitleFn = (filters: Record<string, unknown>) => string;

const summaryHeading = (family: string) => [
  'Statement of Registered',
  `${family} at`,
  'Ministry of Commerce',
];
const detailHeading = (family: string) => [
  'Statement of Registered',
  family,
  'Ministry of Commerce',
];
// Voucher reports use the generic Directorate header (the family/sub-type appears
// in the dynamic subtitle, matching the legacy RegistrationByVoucher RDLCs).
const voucherHeading = (_family: string) => [
  'Ministry of Commerce',
  'Directorate of Trade',
];

const companyColumns = [
  column('CompanyRegistrationNo', 'Company Registration No'),
  column('CompanyName', 'Company Name'),
  addressColumn('CompanyAddress', 'Company Address'),
];

// Legacy RDLC summary grid: a single row with six count columns. Maps to the
// backend RegistrationSummaryRow (NewCount/CancelCount/ExtensionCount/ValidCount/
// InvalidCount/TotalNumber). The date range that the old dynamic headers embedded
// is shown in the report subtitle instead.
const summaryColumns = [
  column('NewCount', 'Number of Register', 'number'),
  column('CancelCount', 'Number of De-Register', 'number'),
  column('ExtensionCount', 'Number of Extension', 'number'),
  column('ValidCount', 'Total Number Still Valid', 'number'),
  column('InvalidCount', 'Total Number Invalid', 'number'),
  column('TotalNumber', 'Total Number', 'number'),
];

// Legacy RDLC voucher tail order: Total Amount, then Payment Type, Voucher No, Voucher Date.
const paymentColumns = [
  column('TotalAmount', 'Total Amount', 'number'),
  column('PaymentType', 'Payment Type'),
  column('VoucherNo', 'Voucher No'),
  column('VoucherDate', 'Voucher Date', 'date'),
];

const reportConfig = (
  controllerName: string,
  title: string,
  filters: ReportFilterConfig[],
  columns: ReportColumnConfig[],
  initialSortColumn?: string,
  reportHeading?: string[],
  reportSubtitle?: SubtitleFn
): ReportPageConfig => ({
  controllerName,
  title,
  apiRoute: controllerName,
  excelRoute: `${controllerName}/Excel`,
  excelFileName: `${controllerName}.xlsx`,
  initialSortColumn,
  showRowNumber: true,
  filters,
  columns,
  reportHeading,
  reportSubtitle,
});

const summaryConfig = (
  controllerName: string,
  title: string,
  family: string,
  subtitle: SubtitleFn = dateRangeSubtitle
): ReportPageConfig => ({
  ...reportConfig(
    controllerName,
    title,
    [dateRangeFilter],
    summaryColumns,
    undefined,
    summaryHeading(family),
    subtitle
  ),
  // The legacy summary is a single aggregate row — no row-number column.
  showRowNumber: false,
});

const detailConfig = (
  controllerName: string,
  title: string,
  family: string,
  columns: ReportColumnConfig[],
  subtitle: SubtitleFn = dateRangeSubtitle
) =>
  reportConfig(
    controllerName,
    title,
    detailFilters,
    columns,
    undefined,
    detailHeading(family),
    subtitle
  );

const voucherConfig = (
  controllerName: string,
  title: string,
  family: string,
  columns: ReportColumnConfig[],
  subtitle: SubtitleFn = dateRangeSubtitle
) =>
  reportConfig(
    controllerName,
    title,
    voucherFilters,
    columns,
    'Date',
    voucherHeading(family),
    subtitle
  );

// Column sets trimmed to match the old Tradenet 2.0 RDLCs' DISPLAYED columns
// (order + header text). The row-number "No." column is provided by showRowNumber.
// NRC ('NRC No', dataIndex nrcNo) is retained where the old report showed it (Wine)
// or where the customer explicitly complained the NRC column was empty (Sale Center,
// Show Room and its EV/EVCycle siblings, Duty Free); it is dropped where the old RDLC
// lacked it and there was no NRC complaint (BSA, Whole Sale/Retail).
const wineNrcColumn: ReportColumnConfig = { ...nrcColumn, title: 'NRC' };

const wineDetailColumns = [
  companyColumns[0],
  column('WineImportationNo', 'Alcoholic Beverages Importation No'),
  companyColumns[1],
  companyColumns[2],
  column('IssuedDate', 'Issued Date', 'date'),
  column('EndDate', 'Valid Date', 'date'),
  column('Name', 'Name'),
  wineNrcColumn,
  column('FL11Name', 'FL11 Name'),
  column('FL11NRCNo', 'FL11 NRC'),
  column('FL4Name', 'FL4 Name'),
  column('FL4NRCNo', 'FL4 NRC'),
  column('FL5Name', 'FL5 Name'),
  column('FL5NRCNo', 'FL5 NRC'),
  column('WineType', 'Type of Alcoholic Beverages'),
];

const wineVoucherColumns = [
  column('Date', 'Date', 'date'),
  companyColumns[0],
  companyColumns[1],
  companyColumns[2],
  column('WineImportationNo', 'Alcoholic Beverages Importation No'),
  column('Name', 'Name'),
  wineNrcColumn,
  ...paymentColumns,
];

// Name + NRC (dataIndex nrcNo) placed after Company Name, before Company Address —
// mirroring the Sale Center / Show Room sibling layout. Re-added after a customer
// complaint that the NRC column had no data; the backend already projects NRCNo
// (Current/Old build logic in sp_DutyFreeShopReport).
const dutyFreeDetailColumns = [
  companyColumns[0],
  column('DutyFreeShopNo', 'Duty Free Shop No'),
  companyColumns[1],
  column('Name', 'Name'),
  nrcColumn,
  companyColumns[2],
  addressColumn('DutyFreeShopAddress', 'Duty Free Shop Address', 'dutyFreeShop'),
  column('IssuedDate', 'Issued Date', 'date'),
  column('EndDate', 'Valid Date', 'date'),
];

const dutyFreeVoucherColumns = [
  column('Date', 'Date', 'date'),
  companyColumns[0],
  companyColumns[1],
  column('Name', 'Name'),
  nrcColumn,
  companyColumns[2],
  column('DutyFreeShopNo', 'Duty Free Shop No'),
  addressColumn('DutyFreeShopAddress', 'Duty Free Shop Address', 'dutyFreeShop'),
  ...paymentColumns,
];

const reExportDetailColumns = [
  companyColumns[0],
  column('ReExportNo', 'Re-Export No'),
  companyColumns[1],
  companyColumns[2],
  addressColumn('ReExportAddress', 'Re-Export Address', 'reExport'),
  column('IssuedDate', 'Issued Date', 'date'),
  column('EndDate', 'Valid Date', 'date'),
];

const businessServiceAgencyDetailColumns = [
  companyColumns[0],
  column('BusinessServiceAgencyNo', 'Sa Ka No'),
  companyColumns[1],
  companyColumns[2],
  column('AuthorizeCompany', 'Agent of Authorize Company'),
  column('IssuedDate', 'Issued Date', 'date'),
  column('EndDate', 'Valid Date', 'date'),
];

const businessServiceAgencyVoucherColumns = [
  column('Date', 'Date', 'date'),
  companyColumns[0],
  companyColumns[1],
  companyColumns[2],
  column('BusinessServiceAgencyNo', 'BSA No'),
  column('AuthorizeCompany', 'Agent of Authorize Company'),
  ...paymentColumns,
];

const saleCenterDetailColumns = [
  companyColumns[0],
  column('SaleCenterNo', 'Sale Center No'),
  column('BusinessServiceAgencyNo', 'BSA No'),
  companyColumns[1],
  column('Name', 'Name'),
  nrcColumn,
  companyColumns[2],
  addressColumn('SaleCenterAddress', 'Sale Center Address', 'saleCenter'),
  column('IssuedDate', 'Issued Date', 'date'),
  column('EndDate', 'Valid Date', 'date'),
];

const saleCenterVoucherColumns = [
  column('Date', 'Date', 'date'),
  companyColumns[0],
  companyColumns[1],
  column('Name', 'Name'),
  nrcColumn,
  companyColumns[2],
  column('SaleCenterNo', 'Sale Center No'),
  addressColumn('SaleCenterAddress', 'Sale Center Address', 'saleCenter'),
  ...paymentColumns,
];

const showRoomDetailColumns = [
  companyColumns[0],
  column('ShowRoomNo', 'Show Room No'),
  column('BusinessServiceAgencyNo', 'BSA No'),
  companyColumns[1],
  column('Name', 'Name'),
  nrcColumn,
  companyColumns[2],
  addressColumn('ShowRoomAddress', 'Show Room Address', 'showRoom'),
  column('IssuedDate', 'Issued Date', 'date'),
  column('EndDate', 'Valid Date', 'date'),
];

const showRoomVoucherColumns = [
  column('Date', 'Date', 'date'),
  companyColumns[0],
  companyColumns[1],
  column('Name', 'Name'),
  nrcColumn,
  companyColumns[2],
  column('ShowRoomNo', 'Show Room No'),
  addressColumn('ShowRoomAddress', 'Show Room Address 1', 'showRoom'),
  addressColumn('ShowRoomAddress2', 'Show Room Address 2', 'showRoom', '2'),
  addressColumn('ShowRoomAddress3', 'Show Room Address 3', 'showRoom', '3'),
  addressColumn('ShowRoomAddress4', 'Show Room Address 4', 'showRoom', '4'),
  addressColumn('ShowRoomAddress5', 'Show Room Address 5', 'showRoom', '5'),
  ...paymentColumns,
];

// List report columns matching the old OGARecommendationListReport.rdlc (the raw
// Id / OGADepartmentId / OGASectionId fields are intentionally NOT shown). The "No."
// column comes from showRowNumber. SDate/SFromDate/SToDate are the pre-formatted
// (dd/m/yyyy, "-" for null) strings. OGADepartmentName/OGASectionName need an explicit
// dataIndex: the API camelCases them to ogaDepartmentName/ogaSectionName (the leading
// acronym is lowercased), which lowerFirst() would not produce. The final "View Detail"
// column is the legacy RDLC blue link that drills into the recommendation's usage history.
const ogaRecommendationColumns: ReportColumnConfig[] = [
  column('SDate', 'Date'),
  { key: 'OGADepartmentName', dataIndex: 'ogaDepartmentName', title: 'Department' },
  { key: 'OGASectionName', dataIndex: 'ogaSectionName', title: 'Section' },
  column('ReferenceNo', 'Reference No'),
  column('SFromDate', 'From Date'),
  column('SToDate', 'To Date'),
  column('Allowance', 'Allowance'),
  column('Terminate', 'Terminate'),
  column('IsUsedOnce', 'Used Once'),
  // Dedicated "View Detail" link (old RDLC's last column). Uses referenceNo as the
  // cell value so the link renders, but shows the static "View Detail" text and
  // carries the (unique) Reference No into the history report's search.
  {
    key: 'ViewDetail',
    dataIndex: 'referenceNo',
    title: 'View Detail',
    drilldown: {
      targetReportKey: 'OGARecommendationHistoryReport',
      rowParams: { ReferenceNo: 'referenceNo' },
      openInNewTab: true,
      linkText: 'View Detail',
    },
  },
];

// Drill target: a single recommendation's usage history (old OGARecommendationHistoryReport.rdlc).
const ogaHistoryColumns = [
  column('SDate', 'Date'),
  column('LicenceNo', 'Licence No'),
  column('Remark', 'Remark'),
  column('Balance', 'Balance'),
  column('FullName', 'Full Name'),
  column('Position', 'Position'),
];

export const newReportConfigs: Record<string, ReportPageConfig> = {
  AlcoholicBeveragesImportationSummaryReport: summaryConfig(
    'AlcoholicBeveragesImportationSummaryReport',
    'Alcoholic Beverages Importation Summary Report',
    'Alcoholic Beverages Importation'
  ),
  AlcoholicBeveragesImportationDetailReport: detailConfig(
    'AlcoholicBeveragesImportationDetailReport',
    'Alcoholic Beverages Importation Detail Report',
    'Alcoholic Beverages Importation',
    wineDetailColumns
  ),
  AlcoholicBeveragesImportationRegistrationByVoucher: voucherConfig(
    'AlcoholicBeveragesImportationRegistrationByVoucher',
    'Alcoholic Beverages Importation Registration By Voucher',
    'Alcoholic Beverages Importation',
    wineVoucherColumns
  ),
  DutyFreeShopSummaryReport: summaryConfig(
    'DutyFreeShopSummaryReport',
    'Duty Free Shop Summary Report',
    'Duty Free Shop'
  ),
  DutyFreeShopDetailReport: detailConfig(
    'DutyFreeShopDetailReport',
    'Duty Free Shop Detail Report',
    'Duty Free Shop',
    dutyFreeDetailColumns
  ),
  DutyFreeShopRegistrationByVoucher: voucherConfig(
    'DutyFreeShopRegistrationByVoucher',
    'Duty Free Shop Registration By Voucher',
    'Duty Free Shop',
    dutyFreeVoucherColumns
  ),
  ReExportSummaryReport: summaryConfig(
    'ReExportSummaryReport',
    'Re-Export Summary Report',
    'Re-Export'
  ),
  ReExportDetailReport: detailConfig(
    'ReExportDetailReport',
    'Re-Export Detail Report',
    'Re-Export',
    reExportDetailColumns
  ),
  BusinessServiceAgencySummaryReport: summaryConfig(
    'BusinessServiceAgencySummaryReport',
    'Business Service Agency Summary Report',
    'Business Representative'
  ),
  BusinessServiceAgencyDetailReport: detailConfig(
    'BusinessServiceAgencyDetailReport',
    'Business Service Agency Detail Report',
    'Business Representative',
    businessServiceAgencyDetailColumns
  ),
  BusinessServiceAgencyRegistrationByVoucher: voucherConfig(
    'BusinessServiceAgencyRegistrationByVoucher',
    'Business Service Agency Registration By Voucher',
    'Business Representative',
    businessServiceAgencyVoucherColumns
  ),
  SaleCenterSummaryReport: withFormType(
    summaryConfig(
      'SaleCenterSummaryReport',
      'Sale Center Summary Report',
      'Sale Center',
      formTypeSubtitle
    ),
    saleCenterFormTypeFilter
  ),
  SaleCenterDetailReport: withFormType(
    detailConfig(
      'SaleCenterDetailReport',
      'Sale Center Detail Report',
      'Sale Center',
      saleCenterDetailColumns,
      formTypeSubtitle
    ),
    saleCenterFormTypeFilter
  ),
  SaleCenterRegistrationByVoucher: withFormType(
    voucherConfig(
      'SaleCenterRegistrationByVoucher',
      'Sale Center Registration By Voucher',
      'Sale Center',
      saleCenterVoucherColumns,
      formTypeSubtitle
    ),
    saleCenterFormTypeFilter
  ),
  ShowRoomSummaryReport: withFormType(
    summaryConfig(
      'ShowRoomSummaryReport',
      'Show Room Summary Report',
      'Show Room',
      formTypeSubtitle
    ),
    showRoomFormTypeFilter
  ),
  ShowRoomDetailReport: withFormType(
    detailConfig(
      'ShowRoomDetailReport',
      'Show Room Detail Report',
      'Show Room',
      showRoomDetailColumns,
      formTypeSubtitle
    ),
    showRoomFormTypeFilter
  ),
  ShowRoomRegistrationByVoucher: withFormType(
    voucherConfig(
      'ShowRoomRegistrationByVoucher',
      'Show Room Registration By Voucher',
      'Show Room',
      showRoomVoucherColumns,
      formTypeSubtitle
    ),
    showRoomFormTypeFilter
  ),
  EVCycleShowRoomSummaryReport: withFormType(
    summaryConfig(
      'EVCycleShowRoomSummaryReport',
      'EVCycle Show Room Summary Report',
      'Show Room for Electric Cycles'
    ),
    evCycleShowRoomFormTypeFilter
  ),
  EVCycleShowRoomDetailReport: withFormType(
    detailConfig(
      'EVCycleShowRoomDetailReport',
      'EVCycle Show Room Detail Report',
      'Show Room for Electric Cycles',
      showRoomDetailColumns
    ),
    evCycleShowRoomFormTypeFilter
  ),
  EVCycleShowRoomRegistrationByVoucher: withFormType(
    voucherConfig(
      'EVCycleShowRoomRegistrationByVoucher',
      'EVCycle Show Room Registration By Voucher',
      'Show Room for Electric Cycles',
      showRoomVoucherColumns
    ),
    evCycleShowRoomFormTypeFilter
  ),
  EVShowRoomSummaryReport: withFormType(
    summaryConfig(
      'EVShowRoomSummaryReport',
      'EV Show Room Summary Report',
      'Show Room for Electric Vehicles'
    ),
    evShowRoomFormTypeFilter
  ),
  EVShowRoomDetailReport: withFormType(
    detailConfig(
      'EVShowRoomDetailReport',
      'EV Show Room Detail Report',
      'Show Room for Electric Vehicles',
      showRoomDetailColumns
    ),
    evShowRoomFormTypeFilter
  ),
  EVShowRoomRegistrationByVoucher: withFormType(
    voucherConfig(
      'EVShowRoomRegistrationByVoucher',
      'EV Show Room Registration By Voucher',
      'Show Room for Electric Vehicles',
      showRoomVoucherColumns
    ),
    evShowRoomFormTypeFilter
  ),
  OGARecommendationReport: reportConfig(
    'OGARecommendationReport',
    'OGA Recommendation Report',
    [
      dateRangeFilter,
      // OGA Department / Section render as dropdowns (lookups resolve via lookupName);
      // 0 = '--- All ---'. Section cascades from Department (dependsOn): selecting a
      // department narrows the section list to that department's sections and resets
      // the section, mirroring the legacy GetOGASectionList AJAX cascade.
      {
        name: 'OGADepartmentId',
        label: 'OGA Department',
        type: 'number',
        defaultValue: 0,
        lookupName: 'ogaDepartments',
      },
      {
        name: 'OGASectionId',
        label: 'OGA Section',
        type: 'number',
        defaultValue: 0,
        lookupName: 'ogaSections',
        dependsOn: 'OGADepartmentId',
      },
      {
        name: 'ReferenceNo',
        label: 'Reference No',
        type: 'text',
        defaultValue: '',
      },
      {
        name: 'CompanyRegistrationNo',
        label: 'Company Registration No',
        type: 'text',
        defaultValue: '',
      },
      {
        name: 'FilterBy',
        label: 'Filter By',
        type: 'select',
        defaultValue: 'List',
        options: [
          { label: 'List', value: 'List' },
          { label: 'Group By', value: 'GroupBy' },
        ],
      },
    ],
    ogaRecommendationColumns,
    undefined,
    // Legacy visible title: "OGA Recommendation Report (FromDate) To (ToDate)".
    ['OGA Recommendation Report'],
    (filters) => dateRangeSubtitle(filters)
  ),
  // Drill target of the OGA list's "Reference No" cell — a recommendation's usage
  // history (searched by the human-readable Reference No; opened in a new tab).
  OGARecommendationHistoryReport: reportConfig(
    'OGARecommendationHistoryReport',
    'OGA Recommendation History',
    [
      {
        name: 'ReferenceNo',
        label: 'Reference No',
        type: 'text',
        defaultValue: '',
        required: true,
      },
    ],
    ogaHistoryColumns,
    undefined,
    ['Ministry of Commerce', 'Directorate of Trade']
  ),
};

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

const companyColumns = [
  column('CompanyRegistrationNo', 'Company Registration No'),
  column('CompanyName', 'Company Name'),
  addressColumn('CompanyAddress', 'Company Address'),
];

const summaryColumns = [
  column('ApplyType', 'Apply Type'),
  column('ApplicationCount', 'Application Count', 'number'),
];

const paymentColumns = [
  column('PaymentType', 'Payment Type'),
  column('VoucherNo', 'Voucher No'),
  column('VoucherDate', 'Voucher Date', 'date'),
  column('TotalAmount', 'Total Amount', 'number'),
];

const reportConfig = (
  controllerName: string,
  title: string,
  filters: ReportFilterConfig[],
  columns: ReportColumnConfig[],
  initialSortColumn?: string
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
});

const summaryConfig = (controllerName: string, title: string) =>
  reportConfig(controllerName, title, [dateRangeFilter], summaryColumns);

const detailConfig = (
  controllerName: string,
  title: string,
  columns: ReportColumnConfig[]
) => reportConfig(controllerName, title, detailFilters, columns);

const voucherConfig = (
  controllerName: string,
  title: string,
  columns: ReportColumnConfig[]
) => reportConfig(controllerName, title, voucherFilters, columns, 'Date');

const wineDetailColumns = [
  companyColumns[0],
  column('WineImportationNo', 'Wine Importation No'),
  ...companyColumns.slice(1),
  column('Name', 'Name'),
  column('NRCNo', 'NRC No'),
  column('FL11Name', 'FL11 Name'),
  column('FL11NRCNo', 'FL11 NRC No'),
  column('FL4Name', 'FL4 Name'),
  column('FL4NRCNo', 'FL4 NRC No'),
  column('FL5Name', 'FL5 Name'),
  column('FL5NRCNo', 'FL5 NRC No'),
  column('WineType', 'Wine Type'),
  column('IssuedDate', 'Issued Date', 'date'),
  column('EndDate', 'End Date', 'date'),
];

const wineVoucherColumns = [
  column('Date', 'Date', 'date'),
  ...wineDetailColumns.filter(
    ({ key }) => key !== 'IssuedDate' && key !== 'EndDate'
  ),
  ...paymentColumns,
];

const dutyFreeDetailColumns = [
  companyColumns[0],
  column('DutyFreeShopNo', 'Duty Free Shop No'),
  ...companyColumns.slice(1),
  column('Name', 'Name'),
  column('NRCNo', 'NRC No'),
  addressColumn('DutyFreeShopAddress', 'Duty Free Shop Address', 'dutyFreeShop'),
  column('IssuedDate', 'Issued Date', 'date'),
  column('EndDate', 'End Date', 'date'),
];

const dutyFreeVoucherColumns = [
  column('Date', 'Date', 'date'),
  ...dutyFreeDetailColumns.filter(
    ({ key }) => key !== 'IssuedDate' && key !== 'EndDate'
  ),
  ...paymentColumns,
];

const reExportDetailColumns = [
  companyColumns[0],
  column('ReExportNo', 'Re-Export No'),
  ...companyColumns.slice(1),
  addressColumn('ReExportAddress', 'Re-Export Address', 'reExport'),
  column('IssuedDate', 'Issued Date', 'date'),
  column('EndDate', 'End Date', 'date'),
];

const businessServiceAgencyDetailColumns = [
  companyColumns[0],
  column('BusinessServiceAgencyNo', 'Business Service Agency No'),
  ...companyColumns.slice(1),
  column('AuthorizeCompany', 'Authorize Company'),
  column('IssuedDate', 'Issued Date', 'date'),
  column('EndDate', 'End Date', 'date'),
];

const businessServiceAgencyVoucherColumns = [
  column('Date', 'Date', 'date'),
  ...businessServiceAgencyDetailColumns.filter(
    ({ key }) => key !== 'IssuedDate' && key !== 'EndDate'
  ),
  ...paymentColumns,
];

const saleCenterDetailColumns = [
  companyColumns[0],
  column('SaleCenterNo', 'Sale Center No'),
  ...companyColumns.slice(1),
  column('Name', 'Name'),
  column('NRCNo', 'NRC No'),
  column('BusinessServiceAgencyNo', 'Business Service Agency No'),
  addressColumn('SaleCenterAddress', 'Sale Center Address', 'saleCenter'),
  column('IssuedDate', 'Issued Date', 'date'),
  column('EndDate', 'End Date', 'date'),
];

const saleCenterVoucherColumns = [
  column('Date', 'Date', 'date'),
  ...saleCenterDetailColumns.filter(
    ({ key }) => key !== 'IssuedDate' && key !== 'EndDate'
  ),
  ...paymentColumns,
];

const showRoomDetailColumns = [
  companyColumns[0],
  column('ShowRoomNo', 'Show Room No'),
  ...companyColumns.slice(1),
  column('Name', 'Name'),
  column('NRCNo', 'NRC No'),
  column('BusinessServiceAgencyNo', 'Business Service Agency No'),
  addressColumn('ShowRoomAddress', 'Show Room Address', 'showRoom'),
  column('IssuedDate', 'Issued Date', 'date'),
  column('EndDate', 'End Date', 'date'),
];

const showRoomVoucherColumns = [
  column('Date', 'Date', 'date'),
  ...showRoomDetailColumns.filter(
    ({ key }) => key !== 'IssuedDate' && key !== 'EndDate'
  ),
  addressColumn('ShowRoomAddress2', 'Show Room Address 2', 'showRoom', '2'),
  addressColumn('ShowRoomAddress3', 'Show Room Address 3', 'showRoom', '3'),
  addressColumn('ShowRoomAddress4', 'Show Room Address 4', 'showRoom', '4'),
  addressColumn('ShowRoomAddress5', 'Show Room Address 5', 'showRoom', '5'),
  ...paymentColumns,
];

const ogaRecommendationColumns = [
  column('Id', 'Id'),
  column('SDate', 'Date'),
  column('CompanyRegistrationNo', 'Company Registration No'),
  column('OGADepartmentId', 'OGA Department Id', 'number'),
  column('OGASectionId', 'OGA Section Id', 'number'),
  column('OGADepartmentName', 'OGA Department'),
  column('OGASectionName', 'OGA Section'),
  column('ReferenceNo', 'Reference No'),
  column('FromDate', 'From Date', 'date'),
  column('ToDate', 'To Date', 'date'),
  column('Allowance', 'Allowance'),
  column('Terminate', 'Terminate'),
  column('IsUsedOnce', 'Is Used Once'),
];

export const newReportConfigs: Record<string, ReportPageConfig> = {
  AlcoholicBeveragesImportationSummaryReport: summaryConfig(
    'AlcoholicBeveragesImportationSummaryReport',
    'Alcoholic Beverages Importation Summary Report'
  ),
  AlcoholicBeveragesImportationDetailReport: detailConfig(
    'AlcoholicBeveragesImportationDetailReport',
    'Alcoholic Beverages Importation Detail Report',
    wineDetailColumns
  ),
  AlcoholicBeveragesImportationRegistrationByVoucher: voucherConfig(
    'AlcoholicBeveragesImportationRegistrationByVoucher',
    'Alcoholic Beverages Importation Registration By Voucher',
    wineVoucherColumns
  ),
  DutyFreeShopSummaryReport: summaryConfig(
    'DutyFreeShopSummaryReport',
    'Duty Free Shop Summary Report'
  ),
  DutyFreeShopDetailReport: detailConfig(
    'DutyFreeShopDetailReport',
    'Duty Free Shop Detail Report',
    dutyFreeDetailColumns
  ),
  DutyFreeShopRegistrationByVoucher: voucherConfig(
    'DutyFreeShopRegistrationByVoucher',
    'Duty Free Shop Registration By Voucher',
    dutyFreeVoucherColumns
  ),
  ReExportSummaryReport: summaryConfig(
    'ReExportSummaryReport',
    'Re-Export Summary Report'
  ),
  ReExportDetailReport: detailConfig(
    'ReExportDetailReport',
    'Re-Export Detail Report',
    reExportDetailColumns
  ),
  BusinessServiceAgencySummaryReport: summaryConfig(
    'BusinessServiceAgencySummaryReport',
    'Business Service Agency Summary Report'
  ),
  BusinessServiceAgencyDetailReport: detailConfig(
    'BusinessServiceAgencyDetailReport',
    'Business Service Agency Detail Report',
    businessServiceAgencyDetailColumns
  ),
  BusinessServiceAgencyRegistrationByVoucher: voucherConfig(
    'BusinessServiceAgencyRegistrationByVoucher',
    'Business Service Agency Registration By Voucher',
    businessServiceAgencyVoucherColumns
  ),
  SaleCenterSummaryReport: summaryConfig(
    'SaleCenterSummaryReport',
    'Sale Center Summary Report'
  ),
  SaleCenterDetailReport: detailConfig(
    'SaleCenterDetailReport',
    'Sale Center Detail Report',
    saleCenterDetailColumns
  ),
  SaleCenterRegistrationByVoucher: voucherConfig(
    'SaleCenterRegistrationByVoucher',
    'Sale Center Registration By Voucher',
    saleCenterVoucherColumns
  ),
  ShowRoomSummaryReport: summaryConfig(
    'ShowRoomSummaryReport',
    'Show Room Summary Report'
  ),
  ShowRoomDetailReport: detailConfig(
    'ShowRoomDetailReport',
    'Show Room Detail Report',
    showRoomDetailColumns
  ),
  ShowRoomRegistrationByVoucher: voucherConfig(
    'ShowRoomRegistrationByVoucher',
    'Show Room Registration By Voucher',
    showRoomVoucherColumns
  ),
  EVCycleShowRoomSummaryReport: summaryConfig(
    'EVCycleShowRoomSummaryReport',
    'EVCycle Show Room Summary Report'
  ),
  EVCycleShowRoomDetailReport: detailConfig(
    'EVCycleShowRoomDetailReport',
    'EVCycle Show Room Detail Report',
    showRoomDetailColumns
  ),
  EVCycleShowRoomRegistrationByVoucher: voucherConfig(
    'EVCycleShowRoomRegistrationByVoucher',
    'EVCycle Show Room Registration By Voucher',
    showRoomVoucherColumns
  ),
  EVShowRoomSummaryReport: summaryConfig(
    'EVShowRoomSummaryReport',
    'EV Show Room Summary Report'
  ),
  EVShowRoomDetailReport: detailConfig(
    'EVShowRoomDetailReport',
    'EV Show Room Detail Report',
    showRoomDetailColumns
  ),
  EVShowRoomRegistrationByVoucher: voucherConfig(
    'EVShowRoomRegistrationByVoucher',
    'EV Show Room Registration By Voucher',
    showRoomVoucherColumns
  ),
  OGARecommendationReport: reportConfig(
    'OGARecommendationReport',
    'OGA Recommendation Report',
    [
      dateRangeFilter,
      {
        name: 'OGADepartmentId',
        label: 'OGA Department',
        type: 'number',
        defaultValue: 0,
      },
      {
        name: 'OGASectionId',
        label: 'OGA Section',
        type: 'number',
        defaultValue: 0,
      },
      {
        name: 'CompanyRegistrationNo',
        label: 'Company Registration No',
        type: 'text',
        defaultValue: '',
      },
      {
        name: 'ReferenceNo',
        label: 'Reference No',
        type: 'text',
        defaultValue: '',
      },
    ],
    ogaRecommendationColumns
  ),
};

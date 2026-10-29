import dayjs from 'dayjs';
import { ReportColumnConfig, ReportPageConfig } from './reportTypes';

const columns: ReportColumnConfig[] = [
  { key: 'SectionCode', dataIndex: 'sectionCode', title: 'Section' },
  { key: 'Status', dataIndex: 'status', title: 'Status' },
  { key: 'ApplyType', dataIndex: 'applyType', title: 'Apply Type' },
  { key: 'ApplicationNo', dataIndex: 'applicationNo', title: 'Application No' },
  {
    key: 'ApplicationDate',
    dataIndex: 'applicationDate',
    title: 'Application Date',
    dataType: 'date',
    dateFormat: 'DD/MM/YYYY',
  },
  { key: 'LicenceNo', dataIndex: 'licenceNo', title: 'License No' },
  {
    key: 'LicenceDate',
    dataIndex: 'licenceDate',
    title: 'Licence Date',
    dataType: 'date',
    dateFormat: 'DD/MM/YYYY',
  },
  {
    key: 'CompanyRegistrationNo',
    dataIndex: 'companyRegistrationNo',
    title: 'Company Registration No',
  },
  { key: 'CompanyName', dataIndex: 'companyName', title: 'Company Name' },
  { key: 'CommodityType', dataIndex: 'commodityType', title: 'Commodity Type' },
  { key: 'HSCode', dataIndex: 'hsCode', title: 'hsCode' },
  {
    key: 'AdditionalDescription',
    dataIndex: 'additionalDescription',
    title: 'Additional Description',
  },
  { key: 'Currency', dataIndex: 'currency', title: 'Currency' },
  {
    key: 'Amount',
    dataIndex: 'amount',
    title: 'Total Value',
    dataType: 'number',
    numberFormat: '#,##0.0000',
  },
  {
    key: 'TurndownCount',
    dataIndex: 'turndownCount',
    title: 'Turn Down အကြိမ်ရေ',
    dataType: 'number',
    numberFormat: '#,##0',
  },
  {
    key: 'TurndownDate',
    dataIndex: 'turndownDate',
    title: 'Turn Down Date',
    dataType: 'date',
    dateFormat: 'DD/MM/YYYY',
  },
  {
    key: 'TurndownRemark',
    dataIndex: 'turndownRemark',
    title: 'Turn Down Remark',
  },
];

const createConfig = (border: boolean): ReportPageConfig => {
  const controllerName = `${border ? 'Border' : ''}ImportLicenceTurndownReport`;
  const name = `${border ? 'Border ' : ''}Import Licence`;
  return {
    controllerName,
    title: `${name} Turndown Report`,
    apiRoute: controllerName,
    excelRoute: `${controllerName}/Excel`,
    excelFileName: `${controllerName}.xlsx`,
    initialSortColumn: 'TurndownDate',
    showRowNumber: true,
    rowNumberTitle: 'No.',
    reportSubtitle: (filters) =>
      `List of ${name} Report (${dayjs(String(filters.FromDate)).format('DD/MM/YYYY')}) To (${dayjs(String(filters.ToDate)).format('DD/MM/YYYY')})`,
    filters: [
      {
        name: 'dateRange',
        label: 'Turndown - From Date / To Date',
        type: 'dateRange',
        fromName: 'FromDate',
        toName: 'ToDate',
        required: true,
        displayFormat: 'DD/MM/YYYY',
      },
      {
        name: 'ExportImportSectionId',
        label: 'Import Section',
        type: 'number',
        defaultValue: 0,
        lookupName: border
          ? 'borderImportLicenceSections'
          : 'importLicenceSections',
      },
      {
        name: 'CompanyRegistrationNo',
        label: 'Company Registration No',
        type: 'text',
        defaultValue: '',
      },
      {
        name: 'CompanyName',
        label: 'Company Name',
        type: 'text',
        defaultValue: '',
      },
      {
        name: 'ApplyType',
        label: 'Apply Type',
        type: 'select',
        defaultValue: '',
        lookupName: border
          ? 'borderImportLicenceTurndownApplyTypes'
          : 'importLicenceTurndownApplyTypes',
      },
    ],
    columns,
  };
};

export const turndownReportConfigs: Record<string, ReportPageConfig> = {
  ImportLicenceTurndownReport: createConfig(false),
  BorderImportLicenceTurndownReport: createConfig(true),
};

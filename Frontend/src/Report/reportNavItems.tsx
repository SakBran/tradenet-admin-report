import { MenuProps } from 'antd';
import { Link } from 'react-router-dom';
import { reportConfigList } from './config/reportConfigs';

type ReportCategory = {
  key: string;
  title: string;
  matches: (controllerName: string) => boolean;
};

const reportCategoryDefinitions: ReportCategory[] = [
  {
    key: 'report-member',
    title: 'Member',
    matches: (controllerName) => controllerName === 'MemberRegistrationReport',
  },
  {
    key: 'report-pathaka',
    title: 'Pathaka',
    matches: (controllerName) =>
      [
        'PaThaKaRegisteredBusinessOrganizationReport',
        'ListOfValidAndInvalidCompany',
        'ListOfDirectorsByCompanyRegistrationNo',
        'ListOfTopCapitalCompany',
        'ListOfCompany',
        'ListOfDirectors',
        'RegistrationByVoucher',
        'RegistrationByBusinessType',
        'CompanyProfile',
        'EIRCardBindReport',
        'CardListsByCompanyRegistrationNumber',
      ].includes(controllerName),
  },
  {
    key: 'report-import-licence',
    title: 'Import Licence',
    matches: (controllerName) => controllerName.startsWith('ImportLicence'),
  },
  {
    key: 'report-import-permit',
    title: 'Import Permit',
    matches: (controllerName) => controllerName.startsWith('ImportPermit'),
  },
  {
    key: 'report-border-import-licence',
    title: 'Border Import Licence',
    matches: (controllerName) =>
      controllerName.startsWith('BorderImportLicence'),
  },
  {
    key: 'report-border-import-permit',
    title: 'Border Import Permit',
    matches: (controllerName) =>
      controllerName.startsWith('BorderImportPermit'),
  },
  {
    key: 'report-border-export-permit',
    title: 'Border Export Permit',
    matches: (controllerName) =>
      controllerName.startsWith('BorderExportPermit'),
  },
  {
    key: 'report-export-licence',
    title: 'Export Licence',
    matches: (controllerName) => controllerName.startsWith('ExportLicence'),
  },
  {
    key: 'report-export-permit',
    title: 'Export Permit',
    matches: (controllerName) => controllerName.startsWith('ExportPermit'),
  },
  {
    key: 'report-border-export-licence',
    title: 'Border Export Licence',
    matches: (controllerName) =>
      controllerName.startsWith('BorderExportLicence'),
  },
  {
    key: 'report-payment',
    title: 'Payment',
    matches: (controllerName) =>
      [
        'MPUReport',
        'ChequeNoReport',
        'OnlineFeesReport',
        'AccountSummaryReport',
        'MPUReportV3',
      ].includes(controllerName),
  },
];

const createReportItem = (config: (typeof reportConfigList)[number]) => ({
  key: config.controllerName,
  label: <Link to={`/Report/${config.controllerName}`}>{config.title}</Link>,
});

export const getReportCategoryKey = (controllerName: string) =>
  reportCategoryDefinitions.find((category) => category.matches(controllerName))
    ?.key;

export const reportNavItems: Required<MenuProps>['items'] = [
  ...reportCategoryDefinitions
    .map((category) => ({
      key: category.key,
      label: category.title,
      children: reportConfigList
        .filter((config) => category.matches(config.controllerName))
        .map(createReportItem),
    }))
    .filter((category) => category.children.length > 0),
  ...reportConfigList
    .filter((config) => !getReportCategoryKey(config.controllerName))
    .map(createReportItem),
];

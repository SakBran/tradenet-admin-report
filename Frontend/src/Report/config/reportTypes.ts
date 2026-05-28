export type ReportColumnDataType = 'string' | 'number' | 'date' | 'boolean';

export type ReportFilterType =
  | 'text'
  | 'number'
  | 'date'
  | 'dateRange'
  | 'boolean';

export interface ReportColumnConfig {
  key: string;
  dataIndex: string;
  title: string;
  dataType?: ReportColumnDataType;
}

export interface ReportFilterConfig {
  name: string;
  label: string;
  type: ReportFilterType;
  fromName?: string;
  toName?: string;
  defaultValue?: string | number | boolean;
  required?: boolean;
}

export interface ReportPageConfig {
  controllerName: string;
  title: string;
  apiRoute: string;
  excelRoute: string;
  excelFileName: string;
  columns: ReportColumnConfig[];
  filters: ReportFilterConfig[];
  initialSortColumn?: string;
}

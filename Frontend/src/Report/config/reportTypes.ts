export type ReportColumnDataType = 'string' | 'number' | 'date' | 'boolean';

export type ReportFilterType =
  | 'text'
  | 'number'
  | 'date'
  | 'dateRange'
  | 'boolean'
  | 'select';

export interface ReportFilterOption {
  label: string;
  value: string | number;
}

export interface ReportColumnConfig {
  key: string;
  dataIndex: string;
  title: string;
  dataType?: ReportColumnDataType;
  fallbackDataIndexes?: string[];
}

export interface ReportFilterConfig {
  name: string;
  label: string;
  type: ReportFilterType;
  fromName?: string;
  toName?: string;
  fromLabel?: string;
  toLabel?: string;
  defaultValue?: string | number | boolean;
  required?: boolean;
  options?: ReportFilterOption[];
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
  showRowNumber?: boolean;
}

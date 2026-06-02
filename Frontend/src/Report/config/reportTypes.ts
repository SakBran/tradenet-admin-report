export type ReportColumnDataType =
  | 'string'
  | 'number'
  | 'date'
  | 'boolean'
  | 'money';

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
  /**
   * Optional centered heading lines shown above the report grid once filters
   * are applied (e.g. ['Ministry of Commerce', 'Directorate of Trade']),
   * mirroring the legacy RDLC report header.
   */
  reportHeading?: string[];
  /**
   * Optional dynamic subtitle rendered under the heading lines. Receives the
   * applied (normalized) filter values so it can reflect the chosen Type/Date,
   * e.g. `${Type} Company Business Organization (${Date})`.
   */
  reportSubtitle?: (filters: Record<string, unknown>) => string;
}

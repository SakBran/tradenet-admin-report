export type ReportColumnDataType =
  | 'string'
  | 'number'
  | 'date'
  | 'dateTime'
  | 'boolean'
  | 'money';

export type ReportFilterType =
  | 'text'
  | 'readonlyText'
  | 'number'
  | 'date'
  | 'dateRange'
  | 'boolean'
  | 'select';

export interface ReportFilterOption {
  label: string;
  value: string | number;
}

/**
 * Makes a column cell a clickable drill-down link that navigates to another
 * report (the legacy RDLC "blue cell" hyperlinks: By Section/Method/Seller
 * Country/Company/HS Code → a pre-filtered Detail report).
 */
export interface ReportColumnDrilldown {
  /** Target report key — its reportConfigs key AND its route path under /Report. */
  targetReportKey: string;
  /** Filter names to carry from the CURRENT applied report filters into the target. */
  carryFilters?: string[];
  /** Map target param name → clicked row's dataIndex, e.g. { ExportImportSectionId: 'sectionId' }. */
  rowParams?: Record<string, string>;
  /** Params always applied on the target (e.g. { Type: 'Oversea' }). */
  staticParams?: Record<string, string | number>;
  /**
   * Open the target report in a NEW browser tab instead of navigating in place.
   * The drill params ride in a `?drill=<json>` query string (router state does not
   * survive a new-tab load); the target page reads + auto-applies them on mount.
   */
  openInNewTab?: boolean;
  /**
   * Static link label for the cell (e.g. 'View Detail'), instead of rendering the
   * cell's own value as the link text. Mirrors the legacy RDLC "View Detail" column.
   */
  linkText?: string;
}

export interface ReportColumnConfig {
  key: string;
  dataIndex: string;
  title: string;
  dataType?: ReportColumnDataType;
  fallbackDataIndexes?: string[];
  drilldown?: ReportColumnDrilldown;
}

export interface ReportFilterConfig {
  name: string;
  label: string;
  type: ReportFilterType;
  lookupName?: string;
  lookupLabel?: string;
  /**
   * Name of a sibling filter this one cascades from. The dependent select only
   * lists lookup options whose `parentId` equals the parent filter's value
   * (e.g. OGA Section depends on OGA Department); selecting/clearing the parent
   * resets this field. When the parent is unset ("All"), all options are shown.
   */
  dependsOn?: string;
  fromName?: string;
  toName?: string;
  fromLabel?: string;
  toLabel?: string;
  showTime?: boolean;
  defaultValue?: string | number | boolean;
  defaultDateRangeMonths?: number;
  required?: boolean;
  excludeFromRequest?: boolean;
  populateFromCompanyRegistrationNo?: boolean;
  options?: ReportFilterOption[];
}

export interface ReportPageConfig {
  controllerName: string;
  title: string;
  apiRoute: string;
  excelRoute: string;
  excelFileName: string;
  columns: ReportColumnConfig[];
  resolveColumns?: (
    filters: Record<string, unknown>,
    columns: ReportColumnConfig[]
  ) => ReportColumnConfig[];
  filters: ReportFilterConfig[];
  initialSortColumn?: string;
  showRowNumber?: boolean;
  disableLazyTotalCount?: boolean;
  /**
   * Renders the result grid in a legacy RDLC ReportViewer-like shell.
   * Used only where we are intentionally matching the old admin report UI.
   */
  legacyReportViewer?: boolean;
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
  /**
   * Placement for the currency-grouped summary footer (when the backend sends
   * `currencyTotals`). `labelColumnKey` is the column.key under which the
   * `<CUR>: N licence(s)` text and the grand `Total: N licence(s)` go; the
   * summed value goes under `valueColumnKey`. Mirrors the legacy
   * ExtensionReport.rdlc currency footer.
   */
  currencyTotalsColumns?: {
    labelColumnKey: string;
    valueColumnKey: string;
  };
}

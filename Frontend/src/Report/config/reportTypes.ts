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
  /**
   * Number format for a numeric column, when the `dataType` default is not the
   * legacy one: '#,##0.0000' is the RDLC `FORMAT(..., "N4")` value format and
   * '#,##0' is `"N0"`. Applied by the grid (BasicTable) and carried into the
   * Excel presentation spec, so both surfaces print the same string.
   */
  numberFormat?: string;
  drilldown?: ReportColumnDrilldown;
  /**
   * Column carried in the config for drill-down/lookup plumbing but NOT rendered
   * by the grid and NOT exported to Excel. `BasicTable` already drops hidden
   * columns; `resolveReportColumns` (Report/reportPresentation.ts) drops them
   * from the Excel presentation spec so the sheet can never show a column the
   * UI does not.
   */
  hidden?: boolean;
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
   * Ask for the exact `COUNT(*)` in the grid's own page request instead of
   * letting it arrive later from the background count.
   *
   * The grid normally requests a "fast page" that skips the count, so the pager
   * only knows there is *one more* page and Ant Design cannot offer a last page
   * until the background count lands — and if that single request fails, it
   * never does. Set this on a report whose count is cheap enough to sit on the
   * critical path; the background count then skips itself, because the page
   * already came back with `isTotalCountExact: true`.
   */
  eagerTotalCount?: boolean;
  /**
   * Initial rows-per-page for the result grid (default 10). The legacy RDLC
   * reports printed every row on one scrolling page, so the small summary
   * reports (Daily / By X) set this high enough that a normal date range fits
   * on one page — otherwise the grid's first page looks like missing data next
   * to the old report. The backend caps a page at 1000 rows.
   */
  defaultPageSize?: number;
  /**
   * Renders the result grid in a legacy RDLC ReportViewer-like shell.
   * Used only where we are intentionally matching the old admin report UI.
   */
  legacyReportViewer?: boolean;
  /**
   * Header text of the row-number column, exactly as the legacy RDLC prints it.
   * The RDLCs are not consistent: the listing reports use `No.`
   * (VoucherReport.rdlc:253, CancelReport.rdlc:256) while the summary "By X"
   * reports use `Sr.No.` (ImportPermitBySectionReport.rdlc:249). Overrides the
   * `legacyReportViewer` default of `No.` / `No`.
   */
  rowNumberTitle?: string;
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

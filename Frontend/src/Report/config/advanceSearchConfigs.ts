import { formatLegacyReportDate } from '../reportPresentation';
import {
  ReportColumnConfig,
  ReportFilterConfig,
  ReportPageConfig,
} from './reportTypes';

// --- Advance Search -------------------------------------------------------
//
// The old admin's Advance Search was its own top-nav screen, not a panel on a report:
// `Reports/AdvanceSearch?type=menu` (Views/Reports/AdvanceSearch.cshtml:269-408) showed a page
// of eight tiles, and each tile opened the same screen scoped to one licence/permit type. Each
// tile is a report here, because a report key is what names the route, the sidebar entry and
// the Excel export job.
//
// The query lived in a separate Web API (`apiUrl + "AdvanceSearch/Search"`); it is ported in
// Backend/StoredProcedureToLinq/sp_AdvanceSearch.cs, where the legacy behaviour — and which of
// its defects were repaired — is documented.

/**
 * Dates print `DD/MM/YYYY`, matching the picker (`advanceSearchDateRange.displayFormat`).
 *
 * The legacy screen reformatted its rows in C# after paging, to `MM/dd/yyyy HH:mm:ss`, and this
 * grid copied that. Against an antd picker showing `YYYY-MM-DD` it meant 3 February printed as
 * `02/03/2026` beside a box reading `2026-02-03` — read here as 2 March, which was part of the
 * 2026-09 "the dates are not the ones I searched" complaint.
 */
const advanceSearchDateFormat = 'DD/MM/YYYY HH:mm:ss';

/**
 * The legacy grid's 23 headers, in order (AdvanceSearch.cshtml:163-188), plus `Issued Date`.
 * They are unspaced identifiers (`LicenceNo`, not `Licence No`) everywhere except the dates;
 * that is how the old screen printed them, so that is what they say here.
 *
 * `Sakhan` appears on the four Border types only — the old markup emitted that column under
 * `@if (ViewBag.type.StartsWith("Border"))`.
 */
const advanceSearchColumns = (withSakhan: boolean): ReportColumnConfig[] => [
  { key: 'Section', dataIndex: 'section', title: 'Section' },
  ...(withSakhan
    ? [{ key: 'Sakhan', dataIndex: 'sakhan', title: 'Sakhan' }]
    : []),
  { key: 'LicenceNo', dataIndex: 'licenceNo', title: 'LicenceNo' },
  {
    key: 'LicenceDate',
    dataIndex: 'licenceDate',
    // The column the date range binds. Named in full, and in the filter's label, so the grid
    // says which of its two dates answered the search.
    title: 'Licence Date',
    dataType: 'date',
    dateFormat: advanceSearchDateFormat,
  },
  {
    key: 'IssuedDate',
    dataIndex: 'issuedDate',
    // Not a legacy column. An amended or extended licence keeps its original Licence Date while
    // carrying a later Issued Date; with only one of the two on screen, that read as "I searched
    // 2026 and got 2025 data". Shown beside it, the two explain each other.
    title: 'Issued Date',
    dataType: 'date',
    dateFormat: advanceSearchDateFormat,
  },
  {
    key: 'CompanyRegistrationNo',
    dataIndex: 'companyRegistrationNo',
    title: 'CompanyRegistrationNo',
  },
  { key: 'CompanyName', dataIndex: 'companyName', title: 'CompanyName' },
  {
    key: 'CompanyAddress',
    dataIndex: 'companyAddress',
    title: 'CompanyAddress',
    fallbackDataIndexes: [
      'unitLevel',
      'streetNumberStreetName',
      'quarterCityTownship',
      'state',
      'country',
    ],
  },
  { key: 'SellerName', dataIndex: 'sellerName', title: 'SellerName' },
  { key: 'SellerAddress', dataIndex: 'sellerAddress', title: 'SellerAddress' },
  { key: 'SellerCountry', dataIndex: 'sellerCountry', title: 'SellerCountry' },
  {
    key: 'PortOfDischarge',
    dataIndex: 'portOfDischarge',
    title: 'PortOfDischarge',
  },
  {
    key: 'LastDate',
    dataIndex: 'lastDate',
    title: 'Last Date',
    dataType: 'date',
    dateFormat: advanceSearchDateFormat,
  },
  { key: 'Method', dataIndex: 'method', title: 'Method' },
  {
    key: 'ConsignedCountry',
    dataIndex: 'consignedCountry',
    title: 'ConsignedCountry',
  },
  {
    key: 'CountryOfOrigin',
    dataIndex: 'countryOfOrigin',
    title: 'CountryOfOrigin',
  },
  { key: 'HSCode', dataIndex: 'hsCode', title: 'HSCode' },
  { key: 'Description', dataIndex: 'description', title: 'Description' },
  { key: 'AorU', dataIndex: 'aorU', title: 'AorU' },
  {
    key: 'Price',
    dataIndex: 'price',
    title: 'Price',
    dataType: 'number',
    numberFormat: '#,##0.0000',
  },
  {
    key: 'Qty',
    dataIndex: 'qty',
    title: 'Qty',
    dataType: 'number',
    numberFormat: '#,##0.0000',
  },
  {
    key: 'Value',
    dataIndex: 'value',
    title: 'Value',
    dataType: 'number',
    numberFormat: '#,##0.0000',
  },
  { key: 'Currency', dataIndex: 'currency', title: 'Currency' },
  { key: 'Conditions', dataIndex: 'conditions', title: 'Conditions' },
];

/**
 * From Date / To Date (AdvanceSearch.cshtml:23-35), binding `LicenceDate`.
 *
 * Three departures from the legacy screen, all from the 2026-09 complaint that the results did
 * not match the search:
 * - **The labels name the column.** The old boxes said only "From Date"/"To Date", so nothing on
 *   the screen said which of the row's dates was being searched.
 * - **`DD/MM/YYYY`**, so the picker and the grid agree — see `advanceSearchDateFormat`.
 * - **The range opens on the current month**, as every other report in this app does. The legacy
 *   screen seeded both boxes with `DateTime.Now` (`defaultDateRangeMonths: 0`), so the first
 *   search covered a single day and usually came back empty.
 */
const advanceSearchDateRange: ReportFilterConfig = {
  name: 'dateRange',
  label: 'Licence Date (From / To)',
  type: 'dateRange',
  fromName: 'FromDate',
  toName: 'ToDate',
  fromLabel: 'Licence From Date',
  toLabel: 'Licence To Date',
  displayFormat: 'DD/MM/YYYY',
  required: true,
};

/**
 * Section. Its "all" is `''`, not `0` (AdvanceSearchreports.js:141-159) — the query treats a
 * literal 0 as a real section id and matches nothing — so this is a string filter with a
 * lookup rather than the usual `type: 'number'` one.
 */
const sectionFilter = (lookupName: string): ReportFilterConfig => ({
  name: 'Section',
  label: 'Section',
  type: 'text',
  defaultValue: '',
  lookupName,
});

const sellerCountryFilter: ReportFilterConfig = {
  name: 'SellerCountry',
  label: 'Seller Country',
  type: 'number',
  defaultValue: 0,
  lookupName: 'countries',
};

const pathakaFilter: ReportFilterConfig = {
  name: 'Pathaka',
  label: 'PaThaKa No',
  type: 'text',
};

/** Matches any port whose name contains what is typed; the legacy predicate was exact. */
const portOfDischargeFilter: ReportFilterConfig = {
  name: 'PortOfDischarge',
  label: 'Port Of Discharge',
  type: 'text',
};

/**
 * Mode of Transport. A multi-select whose values are the option *text* — the legacy list was
 * bound `new SelectList(transportList, "Text", "Text")`, so it posted `Sea`/`Road`/`Air` and
 * never the `S`/`R`/`A` codes beside them.
 *
 * Picking more than one means "carries any of them". The legacy predicate matched the
 * comma-joined column whole, so it meant "carries exactly this set" and almost never matched.
 */
const modeOfTransportFilter: ReportFilterConfig = {
  name: 'ModeOfTransport',
  label: 'Mode of Transport',
  type: 'multiSelect',
  options: [
    { label: 'Sea', value: 'Sea' },
    { label: 'Road', value: 'Road' },
    { label: 'Air', value: 'Air' },
  ],
};

/**
 * Country of Origin / Consigned Country. Selecting several means "any of them".
 *
 * The legacy query compared the whole picked string to the whole stored column, so it only ever
 * matched a row whose country list was byte-identical — and on the two types whose column is a
 * real `int` (Export Licence, Border Export Licence) more than one selection matched nothing at
 * all. Both are fixed in sp_AdvanceSearch.cs.
 */
const countryOfOriginFilter: ReportFilterConfig = {
  name: 'CountryOfOrigin',
  label: 'Country of Origin',
  type: 'multiSelect',
  lookupName: 'countries',
};

const consignedCountryFilter: ReportFilterConfig = {
  name: 'ConsignedCountry',
  label: 'Consigned Country',
  type: 'multiSelect',
  lookupName: 'countries',
};

const descriptionFilter: ReportFilterConfig = {
  name: 'Description',
  label: 'Additional Description',
  type: 'text',
};

/**
 * "Method of Import" is the label on the Export screens too — the old view hard-coded that one
 * string for all eight types (AdvanceSearch.cshtml:94). Kept verbatim.
 */
const methodFilter = (lookupName: string): ReportFilterConfig => ({
  name: 'MethodOfImportExport',
  label: 'Method of Import',
  type: 'number',
  defaultValue: 0,
  lookupName,
});

const incotermFilter = (lookupName: string): ReportFilterConfig => ({
  name: 'Incoterm',
  label: 'Incoterms',
  type: 'number',
  defaultValue: 0,
  lookupName,
});

const statementCodeFilter = (lookupName: string): ReportFilterConfig => ({
  name: 'StatementCode',
  label: 'Statement Code',
  type: 'number',
  defaultValue: 0,
  lookupName,
});

/**
 * Office (Sakhan), on the four Border screens only — the legacy model builder left the list
 * empty for the oversea types.
 *
 * It filters `SakhanId` now. The legacy repository never referenced its own `data.Office`, so
 * the box sat on the screen doing nothing, which is the same defect as the rest of this
 * complaint seen from the other side: the results did not answer the search.
 */
const officeFilter: ReportFilterConfig = {
  name: 'Office',
  label: 'Office',
  type: 'number',
  defaultValue: 0,
  lookupName: 'sakhans',
};

/** AdvanceSearchRepository.cs:72-104 on the admin side — hard-coded, "All" posts ''. */
const applyTypeFilter: ReportFilterConfig = {
  name: 'ApplyType',
  label: 'Application Type',
  type: 'select',
  defaultValue: '',
  options: [
    { label: 'All', value: '' },
    { label: 'Actual Amend', value: 'Actual Amend' },
    { label: 'Amend', value: 'Amend' },
    { label: 'New', value: 'New' },
    { label: 'Extension', value: 'Extension' },
    { label: 'Cancel', value: 'Cancel' },
  ],
};

interface AdvanceSearchOptions {
  /** Lookup feeding the Section box — one per type, keyed FormType x Oversea/Border. */
  sectionLookup: string;
  /** Lookup feeding Method of Import and Incoterms; omitted on the four Permit types. */
  methodLookup?: string;
  incotermLookup?: string;
  /** `importstatementcodes` or `exportstatementcodes`. */
  statementCodeLookup: string;
  /** The four Border types show the (inert) Office box and the Sakhan column. */
  border?: boolean;
  /** ImportPermit and BorderImportPermit carry no such column. */
  hasModeOfTransport?: boolean;
  hasConsignedCountry?: boolean;
}

/**
 * The filter box, in the order the old form laid it out (AdvanceSearch.cshtml:20-154), minus
 * the boxes this type cannot filter on.
 *
 * The legacy screen rendered all sixteen on every type even where the entity had no such
 * column and the predicate was commented out, so e.g. the Import Permit screen offered a
 * Method of Import box that did nothing. Those are dropped here rather than shown dead.
 * `Airport` is gone too: the old DTO carried it but the markup had no such control.
 */
const advanceSearchFilters = (o: AdvanceSearchOptions): ReportFilterConfig[] => [
  advanceSearchDateRange,
  pathakaFilter,
  sectionFilter(o.sectionLookup),
  sellerCountryFilter,
  portOfDischargeFilter,
  ...(o.hasModeOfTransport ? [modeOfTransportFilter] : []),
  countryOfOriginFilter,
  ...(o.hasConsignedCountry ? [consignedCountryFilter] : []),
  descriptionFilter,
  ...(o.methodLookup ? [methodFilter(o.methodLookup)] : []),
  ...(o.incotermLookup ? [incotermFilter(o.incotermLookup)] : []),
  statementCodeFilter(o.statementCodeLookup),
  ...(o.border ? [officeFilter] : []),
  applyTypeFilter,
];

/**
 * The header line under the title, echoing the range that produced the rows on screen.
 *
 * The old screen printed nothing of the sort, which is half of why the 2026-09 complaint was
 * "I do not know what I searched or what came out": the boxes could be edited without pressing
 * Filter, and nothing on the grid said which range it answered.
 */
const advanceSearchSubtitle =
  (title: string) => (filters: Record<string, unknown>) =>
    `${title} — Licence Date (${formatLegacyReportDate(
      filters.FromDate
    )}) To (${formatLegacyReportDate(filters.ToDate)})`;

const advanceSearchConfig = (
  controllerName: string,
  title: string,
  options: AdvanceSearchOptions
): ReportPageConfig => ({
  controllerName,
  title,
  apiRoute: controllerName,
  excelRoute: `${controllerName}/Excel`,
  excelFileName: `${controllerName}.xlsx`,
  reportSubtitle: advanceSearchSubtitle(title),
  // The legacy grid pulled 1000 rows at a time (AdvanceSearchreports.js); a 10-row first page
  // would read as missing data beside it.
  defaultPageSize: 1000,
  // The legacy table had no row-number column. Kept anyway, as every report here has one and a
  // thousand-row dump is hard to talk about without it — the only display-side deviation.
  showRowNumber: true,
  // The legacy request asked for SortColumn "Description", SortOrder "ASC"; the query applies
  // that ordering itself, with a unique tail so paging is stable.
  initialSortColumn: 'Description',
  filters: advanceSearchFilters(options),
  columns: advanceSearchColumns(options.border === true),
});

export const advanceSearchConfigs: Record<string, ReportPageConfig> = {
  AdvanceSearchImportLicence: advanceSearchConfig(
    'AdvanceSearchImportLicence',
    'Advance Search for Import Licence',
    {
      sectionLookup: 'importlicencesections',
      methodLookup: 'importlicencemethods',
      incotermLookup: 'importlicenceincoterms',
      statementCodeLookup: 'importstatementcodes',
      hasModeOfTransport: true,
      hasConsignedCountry: true,
    }
  ),
  AdvanceSearchExportLicence: advanceSearchConfig(
    'AdvanceSearchExportLicence',
    'Advance Search for Export Licence',
    {
      sectionLookup: 'exportlicencesections',
      methodLookup: 'exportlicencemethods',
      incotermLookup: 'exportlicenceincoterms',
      statementCodeLookup: 'exportstatementcodes',
      hasModeOfTransport: true,
      hasConsignedCountry: true,
    }
  ),
  AdvanceSearchImportPermit: advanceSearchConfig(
    'AdvanceSearchImportPermit',
    'Advance Search for Import Permit',
    {
      sectionLookup: 'importpermitsections',
      statementCodeLookup: 'importstatementcodes',
    }
  ),
  AdvanceSearchExportPermit: advanceSearchConfig(
    'AdvanceSearchExportPermit',
    'Advance Search for Export Permit',
    {
      sectionLookup: 'exportpermitsections',
      statementCodeLookup: 'exportstatementcodes',
      hasModeOfTransport: true,
      hasConsignedCountry: true,
    }
  ),
  AdvanceSearchBorderImportLicence: advanceSearchConfig(
    'AdvanceSearchBorderImportLicence',
    'Advance Search for Border Import Licence',
    {
      sectionLookup: 'borderimportlicencesections',
      methodLookup: 'borderimportlicencemethods',
      incotermLookup: 'borderimportlicenceincoterms',
      statementCodeLookup: 'importstatementcodes',
      border: true,
      hasModeOfTransport: true,
      hasConsignedCountry: true,
    }
  ),
  AdvanceSearchBorderExportLicence: advanceSearchConfig(
    'AdvanceSearchBorderExportLicence',
    'Advance Search for Border Export Licence',
    {
      sectionLookup: 'borderexportlicencesections',
      methodLookup: 'borderexportlicencemethods',
      incotermLookup: 'borderexportlicenceincoterms',
      statementCodeLookup: 'exportstatementcodes',
      border: true,
      hasModeOfTransport: true,
      hasConsignedCountry: true,
    }
  ),
  AdvanceSearchBorderImportPermit: advanceSearchConfig(
    'AdvanceSearchBorderImportPermit',
    'Advance Search for Border Import Permit',
    {
      sectionLookup: 'borderimportpermitsections',
      statementCodeLookup: 'importstatementcodes',
      border: true,
    }
  ),
  AdvanceSearchBorderExportPermit: advanceSearchConfig(
    'AdvanceSearchBorderExportPermit',
    'Advance Search for Border Export Permit',
    {
      sectionLookup: 'borderexportpermitsections',
      statementCodeLookup: 'exportstatementcodes',
      border: true,
      hasModeOfTransport: true,
      hasConsignedCountry: true,
    }
  ),
};

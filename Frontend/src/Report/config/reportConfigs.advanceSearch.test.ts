import { describe, expect, it } from 'vitest';
import { reportConfigs } from './reportConfigs';
import { reportNavItems } from '../reportNavItems';
import { reportRoutes } from '../reportRoutes';

const OVERSEA_KEYS = [
  'AdvanceSearchImportLicence',
  'AdvanceSearchExportLicence',
  'AdvanceSearchImportPermit',
  'AdvanceSearchExportPermit',
] as const;

const BORDER_KEYS = [
  'AdvanceSearchBorderImportLicence',
  'AdvanceSearchBorderExportLicence',
  'AdvanceSearchBorderImportPermit',
  'AdvanceSearchBorderExportPermit',
] as const;

const ALL_KEYS = [...OVERSEA_KEYS, ...BORDER_KEYS];

// AdvanceSearch.cshtml:163-188 — the legacy <th> text, verbatim, with two departures from the
// 2026-09 "the results do not match what I searched" round:
//   - `LicenceDate` is spelled `Licence Date`, because the filter label now names it too;
//   - `Issued Date` is new. The range binds Licence Date, but an amended or extended licence
//     carries a later Issued Date, and showing only one of the two is what made a correct row
//     look like it answered the wrong year.
// Unspaced identifiers otherwise; `Sakhan` is emitted only under
// `@if (ViewBag.type.StartsWith("Border"))`.
const LEGACY_COLUMN_TITLES = [
  'Section',
  'LicenceNo',
  'Licence Date',
  'Issued Date',
  'CompanyRegistrationNo',
  'CompanyName',
  'CompanyAddress',
  'SellerName',
  'SellerAddress',
  'SellerCountry',
  'PortOfDischarge',
  'Last Date',
  'Method',
  'ConsignedCountry',
  'CountryOfOrigin',
  'HSCode',
  'Description',
  'AorU',
  'Price',
  'Qty',
  'Value',
  'Currency',
  'Conditions',
];

// The filters each type keeps, in render order. A box is dropped where the entity has no such
// column and the legacy branch had the predicate commented out.
const EXPECTED_FILTERS: Record<string, string[]> = {
  AdvanceSearchImportLicence: [
    'dateRange',
    'Pathaka',
    'Section',
    'SellerCountry',
    'PortOfDischarge',
    'ModeOfTransport',
    'CountryOfOrigin',
    'ConsignedCountry',
    'Description',
    'MethodOfImportExport',
    'Incoterm',
    'StatementCode',
    'ApplyType',
  ],
  AdvanceSearchExportLicence: [
    'dateRange',
    'Pathaka',
    'Section',
    'SellerCountry',
    'PortOfDischarge',
    'ModeOfTransport',
    'CountryOfOrigin',
    'ConsignedCountry',
    'Description',
    'MethodOfImportExport',
    'Incoterm',
    'StatementCode',
    'ApplyType',
  ],
  // No Mode of Transport, Method, Incoterm or Consigned Country column.
  AdvanceSearchImportPermit: [
    'dateRange',
    'Pathaka',
    'Section',
    'SellerCountry',
    'PortOfDischarge',
    'CountryOfOrigin',
    'Description',
    'StatementCode',
    'ApplyType',
  ],
  // No Method or Incoterm column.
  AdvanceSearchExportPermit: [
    'dateRange',
    'Pathaka',
    'Section',
    'SellerCountry',
    'PortOfDischarge',
    'ModeOfTransport',
    'CountryOfOrigin',
    'ConsignedCountry',
    'Description',
    'StatementCode',
    'ApplyType',
  ],
  AdvanceSearchBorderImportLicence: [
    'dateRange',
    'Pathaka',
    'Section',
    'SellerCountry',
    'PortOfDischarge',
    'ModeOfTransport',
    'CountryOfOrigin',
    'ConsignedCountry',
    'Description',
    'MethodOfImportExport',
    'Incoterm',
    'StatementCode',
    'Office',
    'ApplyType',
  ],
  AdvanceSearchBorderExportLicence: [
    'dateRange',
    'Pathaka',
    'Section',
    'SellerCountry',
    'PortOfDischarge',
    'ModeOfTransport',
    'CountryOfOrigin',
    'ConsignedCountry',
    'Description',
    'MethodOfImportExport',
    'Incoterm',
    'StatementCode',
    'Office',
    'ApplyType',
  ],
  AdvanceSearchBorderImportPermit: [
    'dateRange',
    'Pathaka',
    'Section',
    'SellerCountry',
    'PortOfDischarge',
    'CountryOfOrigin',
    'Description',
    'StatementCode',
    'Office',
    'ApplyType',
  ],
  AdvanceSearchBorderExportPermit: [
    'dateRange',
    'Pathaka',
    'Section',
    'SellerCountry',
    'PortOfDischarge',
    'ModeOfTransport',
    'CountryOfOrigin',
    'ConsignedCountry',
    'Description',
    'StatementCode',
    'Office',
    'ApplyType',
  ],
};

const SECTION_LOOKUPS: Record<string, string> = {
  AdvanceSearchImportLicence: 'importlicencesections',
  AdvanceSearchExportLicence: 'exportlicencesections',
  AdvanceSearchImportPermit: 'importpermitsections',
  AdvanceSearchExportPermit: 'exportpermitsections',
  AdvanceSearchBorderImportLicence: 'borderimportlicencesections',
  AdvanceSearchBorderExportLicence: 'borderexportlicencesections',
  AdvanceSearchBorderImportPermit: 'borderimportpermitsections',
  AdvanceSearchBorderExportPermit: 'borderexportpermitsections',
};

const STATEMENT_CODE_LOOKUPS: Record<string, string> = {
  AdvanceSearchImportLicence: 'importstatementcodes',
  AdvanceSearchExportLicence: 'exportstatementcodes',
  AdvanceSearchImportPermit: 'importstatementcodes',
  AdvanceSearchExportPermit: 'exportstatementcodes',
  AdvanceSearchBorderImportLicence: 'importstatementcodes',
  AdvanceSearchBorderExportLicence: 'exportstatementcodes',
  AdvanceSearchBorderImportPermit: 'importstatementcodes',
  AdvanceSearchBorderExportPermit: 'exportstatementcodes',
};

const collectMenuKeys = (
  items: NonNullable<typeof reportNavItems>
): string[] =>
  items.flatMap((item) => {
    if (!item || typeof item !== 'object' || !('key' in item)) {
      return [];
    }

    const key = String(item.key);
    const children =
      'children' in item && Array.isArray(item.children)
        ? collectMenuKeys(item.children as NonNullable<typeof reportNavItems>)
        : [];

    return [key, ...children];
  });

describe('Advance Search report configs', () => {
  it.each(ALL_KEYS)('%s prints the legacy grid headers', (key) => {
    const config = reportConfigs[key];
    const isBorder = (BORDER_KEYS as readonly string[]).includes(key);

    const expected = isBorder
      ? [
          LEGACY_COLUMN_TITLES[0],
          'Sakhan',
          ...LEGACY_COLUMN_TITLES.slice(1),
        ]
      : LEGACY_COLUMN_TITLES;

    expect(config.columns.map((column) => column.title)).toEqual(expected);
  });

  it('shows the Sakhan column on the four Border types only', () => {
    const hasSakhan = (key: string) =>
      reportConfigs[key].columns.some((column) => column.key === 'Sakhan');

    expect(BORDER_KEYS.every(hasSakhan)).toBe(true);
    expect(OVERSEA_KEYS.some(hasSakhan)).toBe(false);
  });

  it.each(ALL_KEYS)('%s offers only the filters it can apply', (key) => {
    expect(reportConfigs[key].filters.map((filter) => filter.name)).toEqual(
      EXPECTED_FILTERS[key]
    );
  });

  it.each(ALL_KEYS)('%s wires its own Section lookup', (key) => {
    const section = reportConfigs[key].filters.find(
      (filter) => filter.name === 'Section'
    );

    expect(section?.lookupName).toBe(SECTION_LOOKUPS[key]);
    // "" is this box's "all", not 0 — the query treats a literal 0 as a real section id.
    expect(section?.defaultValue).toBe('');
    expect(section?.type).not.toBe('number');
  });

  it.each(ALL_KEYS)('%s wires the Import or Export statement codes', (key) => {
    expect(
      reportConfigs[key].filters.find(
        (filter) => filter.name === 'StatementCode'
      )?.lookupName
    ).toBe(STATEMENT_CODE_LOOKUPS[key]);
  });

  it.each(ALL_KEYS)('%s posts its three list boxes as multiSelect', (key) => {
    const multi = reportConfigs[key].filters.filter(
      (filter) => filter.type === 'multiSelect'
    );

    expect(multi.map((filter) => filter.name)).toEqual(
      EXPECTED_FILTERS[key].filter((name) =>
        ['ModeOfTransport', 'CountryOfOrigin', 'ConsignedCountry'].includes(name)
      )
    );
    // Empty means "all", so a multi-select must not carry an "All" option of its own.
    expect(
      multi.every((filter) => filter.defaultValue === undefined)
    ).toBe(true);
  });

  it('posts Mode of Transport as the option text, not the S/R/A codes', () => {
    const mode = reportConfigs.AdvanceSearchImportLicence.filters.find(
      (filter) => filter.name === 'ModeOfTransport'
    );

    expect(mode?.options).toEqual([
      { label: 'Sea', value: 'Sea' },
      { label: 'Road', value: 'Road' },
      { label: 'Air', value: 'Air' },
    ]);
  });

  it('labels the method box "Method of Import" on the Export screens too', () => {
    expect(
      reportConfigs.AdvanceSearchExportLicence.filters.find(
        (filter) => filter.name === 'MethodOfImportExport'
      )?.label
    ).toBe('Method of Import');
  });

  it('opens the date range on the current month, not the legacy single day', () => {
    ALL_KEYS.forEach((key) => {
      const range = reportConfigs[key].filters.find(
        (filter) => filter.type === 'dateRange'
      );

      expect(range?.required).toBe(true);
      // The legacy screen seeded both boxes with DateTime.Now (defaultDateRangeMonths: 0), so
      // the first search covered one day and usually came back empty. Left unset, the shared
      // default of 1 opens on the start of this month, as every other report here does.
      expect(range?.defaultDateRangeMonths).toBeUndefined();
      expect(range?.fromName).toBe('FromDate');
      expect(range?.toName).toBe('ToDate');
    });
  });

  it('names the column the date range binds, in the labels and the subtitle', () => {
    ALL_KEYS.forEach((key) => {
      const config = reportConfigs[key];
      const range = config.filters.find((filter) => filter.type === 'dateRange');

      // "From Date"/"To Date" alone never said which of the row's dates was searched.
      expect(range?.label).toBe('Licence Date (From / To)');
      expect(range?.fromLabel).toBe('Licence From Date');
      expect(range?.toLabel).toBe('Licence To Date');

      // The picker printed antd's YYYY-MM-DD beside a grid printing MM/DD/YYYY, so 3 February
      // showed as 02/03/2026 next to a box reading 2026-02-03.
      expect(range?.displayFormat).toBe('DD/MM/YYYY');

      expect(
        config.reportSubtitle?.({
          FromDate: '2026-02-03T00:00:00',
          ToDate: '2026-02-28T23:59:59',
        })
      ).toBe(`${config.title} — Licence Date (03/02/2026) To (28/02/2026)`);
    });
  });

  it('prints every date column in the same order as the picker', () => {
    ALL_KEYS.forEach((key) => {
      reportConfigs[key].columns
        .filter((column) => column.dataType === 'date')
        .forEach((column) => {
          expect(column.dateFormat).toBe('DD/MM/YYYY HH:mm:ss');
        });
    });
  });

  it('never offers the Airport box the legacy DTO carried', () => {
    ALL_KEYS.forEach((key) => {
      expect(
        reportConfigs[key].filters.some((filter) => filter.name === 'Airport')
      ).toBe(false);
    });
  });

  it('pages 1000 rows at a time, as the legacy grid did', () => {
    ALL_KEYS.forEach((key) => {
      expect(reportConfigs[key].defaultPageSize).toBe(1000);
    });
  });

  it('routes and names every one of the eight tiles', () => {
    ALL_KEYS.forEach((key) => {
      const config = reportConfigs[key];

      expect(config.controllerName).toBe(key);
      expect(config.apiRoute).toBe(key);
      expect(config.excelRoute).toBe(`${key}/Excel`);
      // The file the user saves must name the report, not just its family.
      expect(config.excelFileName).toBe(`${key}.xlsx`);
      expect(reportRoutes.some((route) => route.path === key)).toBe(true);
    });
  });

  it('groups all eight under one Advance Search menu entry', () => {
    const keys = collectMenuKeys(reportNavItems ?? []);

    expect(keys).toContain('report-advance-search');
    ALL_KEYS.forEach((key) => {
      // Exactly one row per report: two configs sharing a menu key render duplicates.
      expect(keys.filter((menuKey) => menuKey === key)).toHaveLength(1);
    });
  });
});

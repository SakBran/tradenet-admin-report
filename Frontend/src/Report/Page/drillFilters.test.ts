import { describe, expect, it } from 'vitest';

import {
  extractDrillOnlyFilters,
  formBackedRequestKeys,
} from './drillFilters';
import { reportConfigs } from '../config/reportConfigs';
import type {
  ReportColumnDrilldown,
  ReportFilterConfig,
} from '../config/reportTypes';

// The drill payload GenericReportPage.handleDrill builds: the carried filters,
// plus one entry per rowParams key taken from the clicked row.
const buildDrillPayload = (
  drilldown: ReportColumnDrilldown,
  row: Record<string, unknown>
) => {
  const payload: Record<string, unknown> = {
    ...(drilldown.staticParams ?? {}),
  };
  (drilldown.carryFilters ?? []).forEach((name) => {
    payload[name] = name.endsWith('Date') ? '2025-01-01' : 0;
  });
  Object.entries(drilldown.rowParams ?? {}).forEach(([target, rowKey]) => {
    payload[target] = row[rowKey];
  });
  return payload;
};

const findDrilldown = (reportKey: string, columnKey: string) => {
  const drilldown = reportConfigs[reportKey].columns.find(
    (column) => column.key === columnKey
  )?.drilldown;
  if (!drilldown) {
    throw new Error(`No drilldown on ${reportKey}.${columnKey}`);
  }
  return drilldown;
};

// Complaint 2026-09-22: drilling a company from the Import Licence Company List
// showed the right rows, but the Excel download held other companies. The Excel
// body is rebuilt from the filter FORM, and the Detail report has no Company
// Registration No box -- so CompanyRegistrationNo was dropped, the proc's
// `== string.Empty` branch matched every company, and (because the export dedup
// hash is a hash of that body) two companies' exports even shared one file.
describe('extractDrillOnlyFilters (Import Licence Company List -> Detail)', () => {
  const drilldown = findDrilldown('ImportLicenceCompanyListReport', 'CompanyName');
  const drill = buildDrillPayload(drilldown, {
    companyRegistrationNo: '123456789',
  });
  const target = reportConfigs.ImportLicenceDetailReport.filters;

  it('keeps the drilled company, which the target has no filter box for', () => {
    expect(extractDrillOnlyFilters(target, drill)).toEqual({
      CompanyRegistrationNo: '123456789',
    });
  });

  it('does NOT keep the filters the form re-supplies on its own', () => {
    const extras = extractDrillOnlyFilters(target, drill);

    for (const name of [
      'FromDate',
      'ToDate',
      'PaThaKaTypeId',
      'ExportImportSectionId',
      'ExportImportMethodId',
    ]) {
      expect(extras).not.toHaveProperty(name);
    }
  });
});

describe('extractDrillOnlyFilters (other Import Licence By-X drills)', () => {
  it('keeps SellerCountryId, which the Detail report also has no box for', () => {
    const drilldown = findDrilldown(
      'ImportLicenceBySellerCountryReport',
      'Country'
    );
    const drill = buildDrillPayload(drilldown, { countryId: 42 });

    expect(
      extractDrillOnlyFilters(
        reportConfigs.ImportLicenceDetailReport.filters,
        drill
      )
    ).toEqual({ SellerCountryId: 42 });
  });

  it('keeps nothing for a drill whose params are all filters on the target', () => {
    const drilldown = findDrilldown('ImportLicenceBySectionReport', 'Section');
    const drill = buildDrillPayload(drilldown, { sectionId: 7 });

    // ExportImportSectionId IS a box on the Detail report, so the form rebuilds it.
    expect(
      extractDrillOnlyFilters(
        reportConfigs.ImportLicenceDetailReport.filters,
        drill
      )
    ).toEqual({});
  });
});

describe('extractDrillOnlyFilters (Border Import Licence twin)', () => {
  it('keeps the company AND the currency the Border Detail report has no boxes for', () => {
    const drilldown = findDrilldown(
      'BorderImportLicenceCompanyListReport',
      'CompanyName'
    );
    const drill = buildDrillPayload(drilldown, {
      companyRegistrationNo: '987654321',
      currency: 'USD',
    });

    expect(
      extractDrillOnlyFilters(
        reportConfigs.BorderImportLicenceDetailReport.filters,
        drill
      )
    ).toEqual({ CompanyRegistrationNo: '987654321', Currency: 'USD' });
  });
});

describe('formBackedRequestKeys', () => {
  it('counts a dateRange under the names it POSTS, not its own name', () => {
    const filters: ReportFilterConfig[] = [
      { name: 'ChequeDateRange', label: 'Cheque Date', type: 'dateRange' },
    ];

    expect(formBackedRequestKeys(filters)).toEqual(
      new Set(['FromDate', 'ToDate'])
    );
  });

  it('honours a custom fromName/toName', () => {
    const filters: ReportFilterConfig[] = [
      {
        name: 'Range',
        label: 'Range',
        type: 'dateRange',
        fromName: 'StartDate',
        toName: 'EndDate',
      },
    ];

    expect(formBackedRequestKeys(filters)).toEqual(
      new Set(['StartDate', 'EndDate'])
    );
  });

  // normalizeFilters never emits an excludeFromRequest filter (the read-only
  // Company Name box that mirrors the registration no), so a drill carrying that
  // key has to ride along as drill-only or the grid and the sheet diverge again.
  it('treats an excludeFromRequest filter as NOT form-backed', () => {
    const filters: ReportFilterConfig[] = [
      { name: 'CompanyRegistrationNo', label: 'Reg No', type: 'text' },
      {
        name: 'CompanyName',
        label: 'Company Name',
        type: 'text',
        excludeFromRequest: true,
      },
    ];

    expect(formBackedRequestKeys(filters)).toEqual(
      new Set(['CompanyRegistrationNo'])
    );
    expect(
      extractDrillOnlyFilters(filters, { CompanyName: 'Colgate Palmolive' })
    ).toEqual({ CompanyName: 'Colgate Palmolive' });
  });
});

// Every drill param across the whole app that NO filter box on the target report
// can reproduce. These are the drills that depend on the page merging
// `drillOnlyFilters` back in: get that wrong and the grid stays scoped while the
// sheet -- and the export dedup hash, which is a hash of the request body --
// silently widens to every company/country.
//
// Frozen on purpose. A new entry appearing here is not a failure to paper over:
// it means a new drill has joined that set, and whoever adds it should confirm
// the scope really does reach the Excel request (devtools: the /Excel POST body).
// `Auto` is the Export Licence "Auto / None Auto" filter (ExportLicenceDetailReport
// has no box for it but ExportLicenceDetailReportRequest.Auto binds it); `Currency`
// binds to nothing on the Border Import Licence detail DTO and is harmless.
const DRILL_ONLY_SCOPES = [
  ['BorderExportLicenceBySellerCountryReport', 'Country', ['BuyerCountryId']],
  ['BorderExportPermitBySectionReport', 'Section', ['Type']],
  ['BorderExportPermitBySellerCountryReport', 'Country', ['BuyerCountryId']],
  ['BorderExportPermitCompanyListReport', 'CompanyName', ['CompanyRegistrationNo']],
  ['BorderImportLicenceByMethodReport', 'Method', ['Currency']],
  ['BorderImportLicenceBySectionReport', 'Section', ['Currency']],
  ['BorderImportLicenceBySellerCountryReport', 'Country', ['SellerCountryId', 'Currency']],
  ['BorderImportLicenceCompanyListReport', 'CompanyName', ['CompanyRegistrationNo', 'Currency']],
  ['BorderImportPermitCompanyListReport', 'CompanyName', ['CompanyRegistrationNo']],
  ['ExportLicenceByMethodReport', 'Method', ['Auto']],
  ['ExportLicenceBySectionReport', 'Section', ['Auto']],
  ['ExportLicenceBySellerCountryReport', 'Country', ['Auto', 'BuyerCountryId']],
  ['ExportLicenceCompanyListReport', 'CompanyName', ['Auto']],
  ['ImportLicenceBySellerCountryReport', 'Country', ['SellerCountryId']],
  ['ImportLicenceCompanyListReport', 'CompanyName', ['CompanyRegistrationNo']],
  ['ImportPermitBySellerCountryReport', 'Country', ['SellerCountryId']],
  ['ImportPermitCompanyListReport', 'CompanyName', ['CompanyRegistrationNo']],
] as const;

describe('the drills that depend on the drill-only merge', () => {
  it('are exactly the frozen inventory, params included', () => {
    const found: string[] = [];

    for (const [reportKey, config] of Object.entries(reportConfigs)) {
      for (const column of config.columns) {
        const drilldown = column.drilldown;
        if (!drilldown) {
          continue;
        }

        const target = reportConfigs[drilldown.targetReportKey];
        expect(
          target,
          `${reportKey}.${column.key} drills into unknown report ${drilldown.targetReportKey}`
        ).toBeDefined();

        const drill = buildDrillPayload(
          drilldown,
          Object.fromEntries(
            Object.values(drilldown.rowParams ?? {}).map((rowKey) => [
              rowKey,
              'value',
            ])
          )
        );
        const extras = Object.keys(
          extractDrillOnlyFilters(target.filters, drill)
        ).sort();

        if (extras.length > 0) {
          found.push(`${reportKey}.${column.key}: ${extras.join(', ')}`);
        }
      }
    }

    expect(found.sort()).toEqual(
      DRILL_ONLY_SCOPES.map(
        ([reportKey, columnKey, params]) =>
          `${reportKey}.${columnKey}: ${[...params].sort().join(', ')}`
      ).sort()
    );
  });
});

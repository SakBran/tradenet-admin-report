// Drill-in params that the target report's filter FORM cannot reproduce.
//
// A drilldown hands the target report more than its own filter boxes hold: the
// Company List report drills into Import Licence Detail carrying
// `CompanyRegistrationNo`, and Detail has no Company Registration No box (by
// design — its filter box has to stay identical to the old Tradenet 2.0 one).
//
// That is safe for the grid, which posts the applied `filters` state verbatim,
// but `normalizeFilters` (GenericReportPage) is a reduce over `config.filters`,
// i.e. a whitelist: anything rebuilt from the form loses the drill param. That is
// how the Excel export came to hold every company while the grid on screen showed
// one — and, because the export dedup hash is a hash of the request body, how two
// different companies' exports hashed the same and one got served the other's file.
//
// So the page keeps the drill-only params aside and merges them back into every
// request it builds from the form. These helpers decide which params those are.
import type { ReportFilterConfig } from '../config/reportTypes';

/**
 * Every request key `normalizeFilters` can emit for this report — the keys the
 * filter form owns and will re-supply on its own.
 *
 * A `dateRange` filter posts under `fromName`/`toName` (defaulting to
 * `FromDate`/`ToDate`), not under its own `name`.
 *
 * An `excludeFromRequest` filter (e.g. the read-only Company Name box that mirrors
 * the registration no) is deliberately NOT counted: `normalizeFilters` never emits
 * it, so if a drill carries that key it has to ride along as drill-only or the
 * grid and the sheet diverge again.
 */
export const formBackedRequestKeys = (
  filters: ReportFilterConfig[]
): Set<string> =>
  filters.reduce<Set<string>>((keys, filter) => {
    if (filter.excludeFromRequest) {
      return keys;
    }

    if (filter.type === 'dateRange') {
      keys.add(filter.fromName ?? 'FromDate');
      keys.add(filter.toName ?? 'ToDate');
      return keys;
    }

    keys.add(filter.name);
    return keys;
  }, new Set<string>());

/**
 * The drill params the form cannot reproduce. These are what has to be merged on
 * top of the normalized form values so the Excel body matches the grid body.
 */
export const extractDrillOnlyFilters = (
  filters: ReportFilterConfig[],
  drill: Record<string, unknown>
): Record<string, unknown> => {
  const formBacked = formBackedRequestKeys(filters);

  return Object.entries(drill).reduce<Record<string, unknown>>(
    (extras, [key, value]) => {
      if (!formBacked.has(key)) {
        extras[key] = value;
      }
      return extras;
    },
    {}
  );
};

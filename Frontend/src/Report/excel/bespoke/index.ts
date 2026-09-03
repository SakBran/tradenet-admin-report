/**
 * Bespoke Excel presentation-spec builders, keyed by `reportConfigs` key.
 *
 * A handful of report pages are not driven by `GenericReportPage` and cannot
 * describe their sheet from `config.columns` alone (composite sheets with two
 * tables + a summary line, hand-built column lists, Myanmar row labels). Those
 * pages register a builder here; everything else uses the generic
 * `buildExcelPresentation(config, applied)`.
 *
 * The registry is intentionally the ONLY seam between the bespoke pages and the
 * fixture generator, so `exportExcelSpecFixtures.test.ts` writes the same spec
 * the page posts. Empty for now — the bespoke builders land in a later phase
 * (docs/ExcelParity/Contract.md §8.6).
 */
import { ExcelPresentationSpec } from '../excelTypes';

/** Builds a spec from the page's applied (normalized) filter values. */
export type BespokeSpecBuilder = (
  applied: Record<string, unknown>
) => ExcelPresentationSpec;

export const bespokeSpecBuilders: Record<string, BespokeSpecBuilder> = {};

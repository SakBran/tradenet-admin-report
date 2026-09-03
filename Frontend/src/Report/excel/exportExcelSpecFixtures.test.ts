/**
 * Fixture generator + drift check for the Excel presentation specs.
 *
 * The backend contract test (`Backend.Tests/ExcelSpecContractTests.cs`) needs
 * the specs the frontend WOULD post, without running a browser. This test is
 * their single source: it builds one spec per report config (plus one per
 * ApplyType header variant) from fixed sample filters and writes them to
 * `Backend.Tests/Fixtures/ExcelSpecs/`.
 *
 *   verify mode (default)          — fails when the checked-in fixtures are stale
 *   write mode (EXCEL_SPEC_FIXTURES=write) — rewrites them:  npm run fixtures:excel
 *
 * Output is deterministic (fixed sample filters, sorted entries, 2-space JSON,
 * trailing newline, no timestamps) so re-running on the same tree is a no-op.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { describe, expect, it } from 'vitest';

import { reportConfigs } from '../config/reportConfigs';
import { ReportPageConfig } from '../config/reportTypes';
import { bespokeSpecBuilders } from './bespoke';
import { buildExcelPresentation } from './buildExcelPresentation';
import {
  applyTypeVariants,
  buildSampleAppliedFilters,
  EXCEL_SPEC_EXCLUDED_CONFIG_KEYS,
  EXCEL_SPEC_SAMPLE_FILTERS,
  fixtureVariantSuffix,
} from './excelSpecSamples';
import {
  EXCEL_PRESENTATION_FORMAT_VERSION,
  ExcelPresentationSpec,
} from './excelTypes';

const REGENERATE_COMMAND = 'cd Frontend && npm run fixtures:excel';

const currentDir = path.dirname(fileURLToPath(import.meta.url));
// src/Report/excel → src/Report → src → Frontend → repo root
const frontendRoot = path.resolve(currentDir, '../../..');
const repoRoot = path.resolve(frontendRoot, '..');
const pagesDir = path.join(frontendRoot, 'src', 'Report', 'Page');
const fixtureDir = path.join(
  repoRoot,
  'Backend.Tests',
  'Fixtures',
  'ExcelSpecs'
);

/** Hand-maintained files in the fixture folder that must never be pruned. */
const PRESERVED_FILES = ['allowlist.json'];

const isWriteMode = process.env.EXCEL_SPEC_FIXTURES === 'write';

interface IndexEntry {
  configKey: string;
  controllerName: string;
  file: string;
  variant: string | null;
  hasDateRange: boolean;
  hasSingleDate: boolean;
  hasCurrencyTotalsColumns: boolean;
  showRowNumber: boolean;
  rowNumberTitle: string;
  isBespokePage: boolean;
  isComposite: boolean;
  hasExcelButton: boolean;
  columnCount: number;
}

const stringify = (value: unknown) => `${JSON.stringify(value, null, 2)}\n`;

const readPageSource = (configKey: string): string | null => {
  const pageFile = path.join(pagesDir, `${configKey}.tsx`);
  return fs.existsSync(pageFile) ? fs.readFileSync(pageFile, 'utf8') : null;
};

/**
 * Whether the page shows an Excel button that reaches the export queue.
 * Mirrors `Frontend/scripts/excelParityManifest.ts` (excelPagePatternOf):
 * `GenericReportPage` always wires one; a bespoke page must post to an
 * `/Excel` route itself.
 */
const inspectPage = (configKey: string) => {
  const source = readPageSource(configKey);

  if (source === null) {
    return { isBespokePage: false, hasExcelButton: false };
  }

  if (source.includes('<GenericReportPage')) {
    return { isBespokePage: false, hasExcelButton: true };
  }

  const postsExcel =
    /['"`][\w/]*\/Excel['"`]/.test(source) ||
    /\bEXCEL_ROUTE\b/.test(source) ||
    /\.excelRoute\b/.test(source);

  return { isBespokePage: true, hasExcelButton: postsExcel };
};

const buildSpec = (
  configKey: string,
  config: ReportPageConfig,
  applyType: string | null
): ExcelPresentationSpec => {
  const applied = buildSampleAppliedFilters(
    config,
    applyType ? { ApplyType: applyType } : {}
  );
  const bespoke = bespokeSpecBuilders[configKey];

  return bespoke
    ? bespoke(applied)
    : buildExcelPresentation(config, applied, configKey);
};

interface Fixture {
  file: string;
  spec: ExcelPresentationSpec;
  entry: IndexEntry;
}

const buildFixtures = (): Fixture[] => {
  const fixtures: Fixture[] = [];

  for (const configKey of Object.keys(reportConfigs).sort()) {
    if (EXCEL_SPEC_EXCLUDED_CONFIG_KEYS.includes(configKey)) {
      continue;
    }

    const config = reportConfigs[configKey];
    const page = inspectPage(configKey);
    const hasDateRange = config.filters.some(
      (filter) => filter.type === 'dateRange'
    );
    const hasSingleDate =
      !hasDateRange && config.filters.some((filter) => filter.type === 'date');

    const variants: Array<string | null> = [null, ...applyTypeVariants(config)];

    for (const applyType of variants) {
      const spec = buildSpec(configKey, config, applyType);
      const file = applyType
        ? `${configKey}.${fixtureVariantSuffix(applyType)}.json`
        : `${configKey}.json`;

      fixtures.push({
        file,
        spec,
        entry: {
          configKey,
          controllerName: config.controllerName,
          file,
          variant: applyType,
          hasDateRange,
          hasSingleDate,
          hasCurrencyTotalsColumns: Boolean(spec.currencyTotalsColumns),
          showRowNumber: spec.showRowNumber,
          rowNumberTitle: spec.rowNumberTitle,
          isBespokePage: page.isBespokePage,
          isComposite: Boolean(spec.sections?.length),
          hasExcelButton: page.hasExcelButton,
          columnCount: spec.columns.length,
        },
      });
    }
  }

  return fixtures.sort((a, b) => (a.file < b.file ? -1 : a.file > b.file ? 1 : 0));
};

const fixtures = buildFixtures();

const indexContent = stringify({
  formatVersion: EXCEL_PRESENTATION_FORMAT_VERSION,
  source: 'Frontend/src/Report/excel/exportExcelSpecFixtures.test.ts',
  sampleFilters: EXCEL_SPEC_SAMPLE_FILTERS,
  entries: fixtures.map((fixture) => fixture.entry),
});

const expectedFiles = new Map<string, string>([
  ...fixtures.map(
    (fixture) => [fixture.file, stringify(fixture.spec)] as [string, string]
  ),
  ['index.json', indexContent],
]);

/** Stale generated spec files (never touches hand-maintained ones). */
const staleFiles = () => {
  if (!fs.existsSync(fixtureDir)) {
    return [];
  }

  return fs
    .readdirSync(fixtureDir)
    .filter(
      (name) =>
        name.endsWith('.json') &&
        !expectedFiles.has(name) &&
        !PRESERVED_FILES.includes(name)
    )
    .sort();
};

const writeFixtures = () => {
  fs.mkdirSync(fixtureDir, { recursive: true });

  for (const name of staleFiles()) {
    const file = path.join(fixtureDir, name);
    let parsed: unknown = null;
    try {
      parsed = JSON.parse(fs.readFileSync(file, 'utf8'));
    } catch {
      parsed = null;
    }

    // Only remove files this generator produced (a spec or the index).
    const generated =
      parsed !== null &&
      typeof parsed === 'object' &&
      ('configKey' in (parsed as object) || 'entries' in (parsed as object));

    if (generated) {
      fs.rmSync(file);
    }
  }

  for (const [name, content] of expectedFiles) {
    fs.writeFileSync(path.join(fixtureDir, name), content, 'utf8');
  }
};

if (isWriteMode) {
  writeFixtures();
}

describe('Excel spec fixtures', () => {
  it('produces one fixture per config plus one per ApplyType variant', () => {
    const exportable = Object.keys(reportConfigs).filter(
      (key) => !EXCEL_SPEC_EXCLUDED_CONFIG_KEYS.includes(key)
    );
    const variantCount = exportable.reduce(
      (sum, key) => sum + applyTypeVariants(reportConfigs[key]).length,
      0
    );

    expect(fixtures.length).toBe(exportable.length + variantCount);
    expect(new Set(fixtures.map((fixture) => fixture.file)).size).toBe(
      fixtures.length
    );

    for (const key of exportable) {
      expect(expectedFiles.has(`${key}.json`)).toBe(true);
    }
  });

  it('describes every fixture in index.json', () => {
    const index = JSON.parse(indexContent) as { entries: IndexEntry[] };

    expect(index.entries.length).toBe(fixtures.length);
    expect(index.entries.map((entry) => entry.file)).toEqual(
      [...index.entries.map((entry) => entry.file)].sort()
    );

    for (const entry of index.entries) {
      expect(entry.hasDateRange && entry.hasSingleDate).toBe(false);
      expect(entry.rowNumberTitle.length).toBeGreaterThan(0);
    }
  });

  it(
    isWriteMode
      ? 'wrote the fixtures to Backend.Tests/Fixtures/ExcelSpecs'
      : `matches the checked-in fixtures (regenerate with \`${REGENERATE_COMMAND}\`)`,
    () => {
      const problems: string[] = [];

      if (!fs.existsSync(fixtureDir)) {
        problems.push(`missing folder ${path.relative(repoRoot, fixtureDir)}`);
      } else {
        for (const [name, content] of expectedFiles) {
          const file = path.join(fixtureDir, name);
          if (!fs.existsSync(file)) {
            problems.push(`missing ${name}`);
            continue;
          }
          if (fs.readFileSync(file, 'utf8') !== content) {
            problems.push(`stale ${name}`);
          }
        }

        for (const name of staleFiles()) {
          problems.push(`unexpected ${name}`);
        }
      }

      expect(
        problems.length
          ? `Excel spec fixtures are stale — run \`${REGENERATE_COMMAND}\`:\n  ${problems
              .slice(0, 20)
              .join('\n  ')}${problems.length > 20 ? `\n  …and ${problems.length - 20} more` : ''}`
          : ''
      ).toBe('');
    }
  );
});

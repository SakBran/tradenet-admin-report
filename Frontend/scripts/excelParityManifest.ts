/**
 * Excel parity manifest builder (docs/ExcelParity plan, Phase 1).
 *
 * Produces docs/ExcelParity/manifest.json: one deterministic item per report
 * controller under Backend/Controllers/Report whose class declaration implements
 * IStreamingExcelReport, joined with the frontend report configs.
 *
 * Run from the Frontend folder:   npx vite-node scripts/excelParityManifest.ts
 * (or from the repo root:          node tools/build-excel-parity-manifest.mjs)
 *
 * The configs are imported AT RUNTIME on purpose: newReportConfigs.ts builds its
 * configs through factory functions, so a TypeScript-AST walk (the approach of
 * tools/compare-report-columns.mjs) cannot see them.
 *
 * Output is sorted by controller name, 2-space JSON, trailing newline, and
 * carries no timestamps so re-running on the same tree is a no-op diff.
 */
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { reportConfigs } from '../src/Report/config/reportConfigs';
import type {
  ReportFilterConfig,
  ReportPageConfig,
} from '../src/Report/config/reportTypes';

// ---------------------------------------------------------------------------
// Paths
// ---------------------------------------------------------------------------

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const frontendRoot = path.resolve(scriptDir, '..');
const repoRoot = path.resolve(frontendRoot, '..');
const backendRoot = path.join(repoRoot, 'Backend');
const controllersDir = path.join(backendRoot, 'Controllers', 'Report');
const pagesDir = path.join(frontendRoot, 'src', 'Report', 'Page');
const outputPath = path.join(repoRoot, 'docs', 'ExcelParity', 'manifest.json');
const fixtureDir = 'Backend.Tests/Fixtures/ExcelSpecs';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type Group = 'A' | 'B' | 'C' | 'D' | 'E' | 'F' | 'UNKNOWN';
type DateShape = 'range' | 'single' | 'none' | 'other';
type ExcelPagePattern = 'generic' | 'bespoke-queue' | 'legacy-sync' | 'none';

interface Variant {
  applyType: string;
  titles: string[];
}

interface ManifestItem {
  controller: string;
  reportKey: string;
  controllerFile: string;
  requestType: string | null;
  group: Group;
  shape: string | null;
  configKeys: string[];
  aliasConfigKeys: string[];
  configTitle: string | null;
  excelWorksheetTitle: string | null;
  titleMismatch: boolean;
  subtitleSample: string | null;
  reportHeading: string[];
  showRowNumber: boolean;
  dateShape: DateShape;
  requestDateProps: string[];
  dateShapeDisagrees: boolean;
  hasColumnTotals: boolean;
  hasCurrencyTotals: boolean;
  currencyTotalsColumns: ReportPageConfig['currencyTotalsColumns'] | null;
  variants: Variant[];
  columnCount: number;
  bespokePage: string | null;
  excelPagePattern: ExcelPagePattern;
  hasLayoutProvider: boolean;
  formatVersion: number;
  fixtureFiles: string[];
}

interface ExcludedController {
  controller: string;
  controllerFile: string;
  reason: string;
}

interface Manifest {
  generatedFrom: { head: string; plan: string };
  counts: {
    total: number;
    byGroup: Record<string, number>;
    hasColumnTotals: number;
    hasCurrencyTotals: number;
    both: number;
    aliases: number;
    variants: number;
    titleMismatch: number;
    bespokePages: number;
  };
  excluded: ExcludedController[];
  unknown: string[];
  controllersWithoutConfig: string[];
  configsWithoutController: string[];
  controllers: ManifestItem[];
}

// ---------------------------------------------------------------------------
// Sample filter values used to render reportSubtitle / resolveColumns.
// Fixed values (no "today") keep the manifest deterministic.
// ---------------------------------------------------------------------------

const SAMPLE_FILTERS: Record<string, unknown> = {
  FromDate: '2026-02-01T00:00:00',
  ToDate: '2026-02-28T23:59:59',
  Date: '2026-02-15T00:00:00',
  ApplyType: 'New',
  Type: '',
};

// Mirrors GenericReportPage.tsx (formTypePrefixes + getDerivedFilterValues).
// Replicated here on purpose: the page is a .tsx React module and must not be
// imported by a Node script.
const formTypePrefixes: Array<[string, string]> = [
  ['BorderExportLicence', 'Border Export Licence'],
  ['BorderImportLicence', 'Border Import Licence'],
  ['BorderExportPermit', 'Border Export Permit'],
  ['BorderImportPermit', 'Border Import Permit'],
  ['ExportLicence', 'Export Licence'],
  ['ImportLicence', 'Import Licence'],
  ['ExportPermit', 'Export Permit'],
  ['ImportPermit', 'Import Permit'],
];

const getDerivedFilterValues = (
  controllerName: string,
  filters: ReportFilterConfig[]
): Record<string, unknown> => {
  const values: Record<string, unknown> = {};
  const hasFilter = (name: string) =>
    filters.some((filter) => filter.name === name);
  const formType = formTypePrefixes.find(([prefix]) =>
    controllerName.startsWith(prefix)
  )?.[1];

  if (formType && hasFilter('FormType')) {
    values.FormType = formType;
  }

  if (hasFilter('Type')) {
    if (controllerName.startsWith('Border')) {
      values.Type = 'Border';
    } else if (
      formType &&
      ['Export Licence', 'Import Licence', 'Export Permit', 'Import Permit'].includes(
        formType
      )
    ) {
      values.Type = 'Oversea';
    }
  }

  return values;
};

/**
 * The normalized filter object GenericReportPage would hand to reportSubtitle /
 * resolveColumns for this config: filter defaults, then the fixed sample values,
 * then the hidden derived FormType/Type.
 */
const buildSampleFilters = (config: ReportPageConfig): Record<string, unknown> => {
  const values: Record<string, unknown> = {};

  for (const filter of config.filters) {
    if (filter.excludeFromRequest) {
      continue;
    }

    if (filter.type === 'dateRange') {
      values[filter.fromName ?? 'FromDate'] = SAMPLE_FILTERS.FromDate;
      values[filter.toName ?? 'ToDate'] = SAMPLE_FILTERS.ToDate;
      continue;
    }

    if (filter.type === 'date') {
      values[filter.name] = SAMPLE_FILTERS.Date;
      continue;
    }

    if (filter.type === 'number') {
      values[filter.name] =
        typeof filter.defaultValue === 'number'
          ? filter.defaultValue
          : Number(filter.defaultValue ?? 0);
      continue;
    }

    values[filter.name] = filter.defaultValue ?? '';
  }

  return {
    ...values,
    ...SAMPLE_FILTERS,
    ...getDerivedFilterValues(config.controllerName, config.filters),
  };
};

// ---------------------------------------------------------------------------
// C# source helpers (regex + brace matching; no Roslyn available here)
// ---------------------------------------------------------------------------

const readText = (file: string) => fs.readFileSync(file, 'utf8');

const toPosix = (file: string) => file.split(path.sep).join('/');

const collapseWhitespace = (value: string) => value.replace(/\s+/g, ' ').trim();

const escapeRegExp = (value: string) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** Index just past the bracket that closes the one at `openIndex`, or -1. */
const findBalancedEnd = (
  text: string,
  openIndex: number,
  open: string,
  close: string
): number => {
  let depth = 0;
  for (let i = openIndex; i < text.length; i += 1) {
    const ch = text[i];
    if (ch === open) {
      depth += 1;
    } else if (ch === close) {
      depth -= 1;
      if (depth === 0) {
        return i + 1;
      }
    }
  }
  return -1;
};

/** Body (between the braces) of `class <name>`, or null. */
const extractClassBody = (text: string, className: string): string | null => {
  const match = new RegExp(`\\bclass\\s+${escapeRegExp(className)}\\b[^{]*\\{`).exec(text);
  if (!match) {
    return null;
  }
  const openIndex = match.index + match[0].length - 1;
  const end = findBalancedEnd(text, openIndex, '{', '}');
  return end === -1 ? null : text.slice(openIndex + 1, end - 1);
};

/** Body of the private WriteRowsAsync(<Request> request, ...) overload. */
const extractWriteRowsBody = (text: string): string | null => {
  const patterns = [
    /private\s+(?:static\s+)?async\s+Task\s+WriteRowsAsync\s*\(/g,
    /async\s+Task\s+WriteRowsAsync\s*\(/g,
  ];

  for (const pattern of patterns) {
    let match: RegExpExecArray | null;
    while ((match = pattern.exec(text)) !== null) {
      const parenOpen = match.index + match[0].length - 1;
      const parenEnd = findBalancedEnd(text, parenOpen, '(', ')');
      if (parenEnd === -1) {
        continue;
      }
      // Skip the expression-bodied public overload (`=> WriteRowsAsync(...)`).
      const afterParams = text.slice(parenEnd, parenEnd + 40);
      if (/^\s*=>/.test(afterParams)) {
        continue;
      }
      const braceOpen = text.indexOf('{', parenEnd);
      if (braceOpen === -1) {
        continue;
      }
      const braceEnd = findBalancedEnd(text, braceOpen, '{', '}');
      if (braceEnd === -1) {
        continue;
      }
      const body = text.slice(braceOpen + 1, braceEnd - 1);
      if (body.includes('await ')) {
        return body;
      }
    }
  }

  return null;
};

/** The first `await` statement of a method body, whitespace-collapsed. */
const firstAwaitStatement = (body: string): string | null => {
  const awaitIndex = body.indexOf('await ');
  if (awaitIndex === -1) {
    return null;
  }

  if (body.startsWith('await foreach', awaitIndex)) {
    const parenOpen = body.indexOf('(', awaitIndex);
    const parenEnd = findBalancedEnd(body, parenOpen, '(', ')');
    return collapseWhitespace(body.slice(awaitIndex, parenEnd === -1 ? undefined : parenEnd));
  }

  const lineStart = body.lastIndexOf('\n', awaitIndex) + 1;
  const semicolon = body.indexOf(';', awaitIndex);
  return collapseWhitespace(
    body.slice(lineStart, semicolon === -1 ? undefined : semicolon + 1)
  );
};

const classifyGroup = (controller: string, statement: string | null): Group => {
  if (controller.endsWith('TotalValueLicencesReportController')) {
    return 'D';
  }
  if (!statement) {
    return 'UNKNOWN';
  }
  if (/SummaryRowAsync\(/.test(statement)) {
    return 'F';
  }
  if (/GetLicenceListRowsAsync\(/.test(statement)) {
    return 'E';
  }
  if (/GetAggregateRowsAsync\(|GetSummaryRowsAsync\(|GetSectionRowsAsync\(/.test(statement)) {
    return 'C';
  }
  if (statement.startsWith('await foreach')) {
    return /ExecuteQueryable\(|\.Query\(/.test(statement) ? 'A' : 'B';
  }
  return 'UNKNOWN';
};

/**
 * Short, human-readable form of the first await: `<source>.<Method>(<key args>)`
 * with the boilerplate arguments (_context, procedureRequest, chunkSize,
 * cancellationToken) and the AsAsyncEnumerable/ChunkAsync plumbing removed.
 */
const shapeOf = (statement: string | null, body: string | null): string | null => {
  if (!statement) {
    return null;
  }

  let expression = statement;
  const foreachMatch = /^await foreach \(\s*var\s+\w+\s+in\s+(.+)\)$/.exec(statement);
  if (foreachMatch) {
    expression = foreachMatch[1];
    // `await foreach (var chunk in query.AsAsyncEnumerable()...)` — resolve `query`.
    const source = /^(\w+)\./.exec(expression)?.[1];
    if (source && body) {
      const definition = new RegExp(`\\bvar\\s+${escapeRegExp(source)}\\s*=\\s*([^;]+);`).exec(body);
      if (definition) {
        expression = expression.replace(
          new RegExp(`^${escapeRegExp(source)}\\.`),
          `${collapseWhitespace(definition[1])}.`
        );
      }
    }
  } else {
    expression = expression
      .replace(/^var\s+\w+\s*=\s*/, '')
      .replace(/^await\s+/, '')
      .replace(/;$/, '');
  }

  return collapseWhitespace(
    expression
      .replace(/\.AsAsyncEnumerable\(\)/g, '')
      .replace(/\.ChunkAsync\([^)]*\)/g, '')
      .replace(/\bReportAggregateDimension\./g, '')
      .replace(/\b_context\b/g, '')
      .replace(/\bprocedureRequest!?/g, '')
      .replace(/\bchunkSize\b/g, '')
      .replace(/\bcancellationToken\b/g, '')
      .replace(/\s*:\s*/g, ':')
      .replace(/\(\s*,\s*/g, '(')
      .replace(/,\s*,/g, ',')
      .replace(/,\s*\)/g, ')')
      .replace(/\(\s*\)/g, '()')
      .replace(/\.\s+/g, '.')
  );
};

const dateShapeOf = (dateProps: string[]): DateShape => {
  if (dateProps.some((p) => /From/.test(p)) && dateProps.some((p) => /To/.test(p))) {
    return 'range';
  }
  if (dateProps.length === 1) {
    return 'single';
  }
  if (dateProps.length === 0) {
    return 'none';
  }
  return 'other';
};

const configDateShapeOf = (config: ReportPageConfig | null): DateShape => {
  if (!config) {
    return 'none';
  }
  if (config.filters.some((f) => f.type === 'dateRange')) {
    return 'range';
  }
  if (config.filters.some((f) => f.type === 'date')) {
    return 'single';
  }
  return 'none';
};

// Lazy index of every Backend/**/*.cs (excluding bin/obj) for request DTOs that
// are not declared at the bottom of their controller file.
let backendSourceCache: Map<string, string> | null = null;
const backendSources = (): Map<string, string> => {
  if (backendSourceCache) {
    return backendSourceCache;
  }
  const cache = new Map<string, string>();
  const walk = (dir: string) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true }).sort((a, b) =>
      a.name.localeCompare(b.name)
    )) {
      if (entry.isDirectory()) {
        if (entry.name === 'bin' || entry.name === 'obj' || entry.name === 'node_modules') {
          continue;
        }
        walk(path.join(dir, entry.name));
      } else if (entry.isFile() && entry.name.endsWith('.cs')) {
        const file = path.join(dir, entry.name);
        cache.set(file, readText(file));
      }
    }
  };
  walk(backendRoot);
  backendSourceCache = cache;
  return cache;
};

const findRequestClassBody = (controllerText: string, requestType: string): string | null => {
  const local = extractClassBody(controllerText, requestType);
  if (local !== null) {
    return local;
  }
  for (const text of backendSources().values()) {
    const body = extractClassBody(text, requestType);
    if (body !== null) {
      return body;
    }
  }
  return null;
};

const collectDateProps = (classBody: string | null): string[] => {
  if (!classBody) {
    return [];
  }
  const props: string[] = [];
  const pattern = /\bDateTime\??\s+(\w+)\s*\{/g;
  let match: RegExpExecArray | null;
  while ((match = pattern.exec(classBody)) !== null) {
    props.push(match[1]);
  }
  return props;
};

// ---------------------------------------------------------------------------
// Frontend page helpers
// ---------------------------------------------------------------------------

const pageFileFor = (configKey: string) => path.join(pagesDir, `${configKey}.tsx`);

const excelPagePatternOf = (pageSource: string): ExcelPagePattern => {
  const postsExcel =
    /['"`][\w/]*\/Excel['"`]/.test(pageSource) ||
    /\bEXCEL_ROUTE\b/.test(pageSource) ||
    /\.excelRoute\b/.test(pageSource);
  if (!postsExcel) {
    return 'none';
  }
  if (/\.status\s*===\s*['"](Ready|Processing|Queued|Completed)['"]/.test(pageSource)) {
    return 'bespoke-queue';
  }
  if (/responseType:\s*['"]blob['"]/.test(pageSource)) {
    return 'legacy-sync';
  }
  return 'none';
};

const sameStrings = (a: string[], b: string[]) =>
  a.length === b.length && a.every((value, index) => value === b[index]);

const fixtureVariantSuffix = (applyType: string) =>
  `ApplyType-${applyType.replace(/\s+/g, '_')}`;

// ---------------------------------------------------------------------------
// Build
// ---------------------------------------------------------------------------

const gitHead = () =>
  execFileSync('git', ['rev-parse', 'HEAD'], { cwd: repoRoot, encoding: 'utf8' }).trim();

const configKeysByController = new Map<string, string[]>();
for (const key of Object.keys(reportConfigs).sort()) {
  const controllerName = reportConfigs[key].controllerName;
  configKeysByController.set(controllerName, [
    ...(configKeysByController.get(controllerName) ?? []),
    key,
  ]);
}

const controllerFiles = fs
  .readdirSync(controllersDir)
  .filter((name) => name.endsWith('.cs'))
  .sort();

const items: ManifestItem[] = [];
const excluded: ExcludedController[] = [];
const warnings: string[] = [];

for (const fileName of controllerFiles) {
  const absoluteFile = path.join(controllersDir, fileName);
  const controllerFile = toPosix(path.relative(repoRoot, absoluteFile));
  const text = readText(absoluteFile);

  const classMatch = /\bclass\s+(\w+Controller)\s*:\s*([^{]*)\{/.exec(text);
  const controller = classMatch?.[1] ?? fileName.replace(/\.cs$/, '');
  const implementsStreaming = classMatch
    ? /\bIStreamingExcelReport\b/.test(classMatch[2])
    : false;

  if (!implementsStreaming) {
    excluded.push({
      controller,
      controllerFile,
      reason: 'class declaration does not implement IStreamingExcelReport',
    });
    continue;
  }

  const reportKey = controller.replace(/Controller$/, '');
  const configKeys = [...(configKeysByController.get(reportKey) ?? [])].sort();
  const aliasConfigKeys = configKeys.filter((key) => key !== reportKey);
  const primaryKey = configKeys.includes(reportKey) ? reportKey : configKeys[0] ?? null;
  const config = primaryKey ? reportConfigs[primaryKey] : null;

  const excelWorksheetTitle =
    /ExcelWorksheetTitle\s*=>\s*"([^"]+)"/.exec(text)?.[1] ?? null;
  const requestType = /ExcelRequestType\s*=>\s*typeof\((\w+)\)/.exec(text)?.[1] ?? null;
  const hasLayoutProvider = /\bIExcelReportLayoutProvider\b/.test(text);
  const formatVersion = Number(/ExcelFormatVersion\((\d+)\)/.exec(text)?.[1] ?? '1');

  const writeRowsBody = extractWriteRowsBody(text);
  const statement = writeRowsBody ? firstAwaitStatement(writeRowsBody) : null;
  const group = classifyGroup(controller, statement);
  const shape = shapeOf(statement, writeRowsBody);

  const hasColumnTotals = /includeColumnTotals:\s*true|\.ColumnTotals\s*=/.test(text);
  const hasCurrencyTotals = /\.CurrencyTotals\s*=/.test(text);

  const requestClassBody = requestType ? findRequestClassBody(text, requestType) : null;
  if (requestType && requestClassBody === null) {
    warnings.push(`${controller}: request class ${requestType} not found under Backend/`);
  }
  const requestDateProps = collectDateProps(requestClassBody);
  const dateShape = dateShapeOf(requestDateProps);
  const configDateShape = configDateShapeOf(config);

  const sample = config ? buildSampleFilters(config) : null;
  let subtitleSample: string | null = null;
  if (config?.reportSubtitle && sample) {
    try {
      subtitleSample = config.reportSubtitle(sample);
    } catch (error) {
      warnings.push(`${controller}: reportSubtitle threw: ${String(error)}`);
    }
  }

  // Variants: ApplyType options whose resolved column titles differ from the
  // default view (the sample's ApplyType, i.e. 'New'). Compared against the
  // RESOLVED default rather than the raw config titles because some raw titles
  // are RDLC placeholders ("=Parameters!header2.Value") that resolveColumns
  // always replaces.
  const variants: Variant[] = [];
  if (config?.resolveColumns && sample) {
    const baseTitles = config.resolveColumns(sample, config.columns).map((c) => c.title);
    const applyTypeFilter = config.filters.find((f) => f.name === 'ApplyType');
    const options = (applyTypeFilter?.options ?? [])
      .map((option) => String(option.value))
      .filter((value) => value !== '');
    for (const applyType of options) {
      const titles = config
        .resolveColumns({ ...sample, ApplyType: applyType }, config.columns)
        .map((c) => c.title);
      if (!sameStrings(titles, baseTitles)) {
        variants.push({ applyType, titles });
      }
    }
  }

  let bespokePage: string | null = null;
  let excelPagePattern: ExcelPagePattern = 'none';
  let sawGenericPage = false;
  for (const key of configKeys) {
    const pageFile = pageFileFor(key);
    if (!fs.existsSync(pageFile)) {
      warnings.push(`${controller}: no page file for config key ${key}`);
      continue;
    }
    const pageSource = readText(pageFile);
    if (pageSource.includes('<GenericReportPage')) {
      sawGenericPage = true;
      continue;
    }
    if (bespokePage === null) {
      bespokePage = key;
      excelPagePattern = excelPagePatternOf(pageSource);
    }
  }
  if (bespokePage === null && sawGenericPage) {
    excelPagePattern = 'generic';
  }

  const fixtureFiles: string[] = [];
  for (const key of configKeys) {
    fixtureFiles.push(`${fixtureDir}/${key}.json`);
    if (key === primaryKey) {
      for (const variant of variants) {
        fixtureFiles.push(`${fixtureDir}/${key}.${fixtureVariantSuffix(variant.applyType)}.json`);
      }
    }
  }

  const configTitle = config?.title ?? null;

  items.push({
    controller,
    reportKey,
    controllerFile,
    requestType,
    group,
    shape,
    configKeys,
    aliasConfigKeys,
    configTitle,
    excelWorksheetTitle,
    titleMismatch:
      configTitle !== null &&
      excelWorksheetTitle !== null &&
      configTitle.trim() !== excelWorksheetTitle.trim(),
    subtitleSample,
    reportHeading: config?.reportHeading ?? [],
    showRowNumber: config?.showRowNumber ?? true,
    dateShape,
    requestDateProps,
    dateShapeDisagrees: config !== null && dateShape !== configDateShape,
    hasColumnTotals,
    hasCurrencyTotals,
    currencyTotalsColumns: config?.currencyTotalsColumns ?? null,
    variants,
    columnCount: config?.columns.length ?? 0,
    bespokePage,
    excelPagePattern,
    hasLayoutProvider,
    formatVersion,
    fixtureFiles,
  });
}

items.sort((a, b) => (a.controller < b.controller ? -1 : a.controller > b.controller ? 1 : 0));
excluded.sort((a, b) => (a.controller < b.controller ? -1 : a.controller > b.controller ? 1 : 0));

const streamingReportKeys = new Set(items.map((item) => item.reportKey));
const configsWithoutController = Object.keys(reportConfigs)
  .filter((key) => !streamingReportKeys.has(reportConfigs[key].controllerName))
  .sort();

const byGroup: Record<string, number> = {};
for (const group of ['A', 'B', 'C', 'D', 'E', 'F', 'UNKNOWN'] as Group[]) {
  const count = items.filter((item) => item.group === group).length;
  if (count > 0 || group !== 'UNKNOWN') {
    byGroup[group] = count;
  }
}

const manifest: Manifest = {
  generatedFrom: { head: gitHead(), plan: 'docs/ExcelParity/Contract.md' },
  counts: {
    total: items.length,
    byGroup,
    hasColumnTotals: items.filter((item) => item.hasColumnTotals).length,
    hasCurrencyTotals: items.filter((item) => item.hasCurrencyTotals).length,
    both: items.filter((item) => item.hasColumnTotals && item.hasCurrencyTotals).length,
    aliases: items.reduce((sum, item) => sum + item.aliasConfigKeys.length, 0),
    variants: items.filter((item) => item.variants.length > 0).length,
    titleMismatch: items.filter((item) => item.titleMismatch).length,
    bespokePages: new Set(items.map((item) => item.bespokePage).filter(Boolean)).size,
  },
  excluded,
  unknown: items.filter((item) => item.group === 'UNKNOWN').map((item) => item.controller),
  controllersWithoutConfig: items
    .filter((item) => item.configKeys.length === 0)
    .map((item) => item.controller),
  configsWithoutController,
  controllers: items,
};

fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');

for (const warning of warnings) {
  console.warn(`WARN ${warning}`);
}
console.log(
  `Wrote ${toPosix(path.relative(repoRoot, outputPath))}: ${manifest.counts.total} controllers ` +
    `(${Object.entries(byGroup)
      .map(([group, count]) => `${group}=${count}`)
      .join(' ')}), ${excluded.length} excluded, ${manifest.unknown.length} unknown`
);

import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import ts from '../Frontend/node_modules/typescript/lib/typescript.js';

const repoRoot = process.cwd();
const oldAdminRoot = path.resolve(repoRoot, '..', 'tradenet-2.0-admin', 'TradenetAdmin');
const oldReportRoot = path.join(oldAdminRoot, 'ReportControl');
const oldReportsControllerPath = path.join(oldAdminRoot, 'Controllers', 'ReportsController.cs');
const newConfigPath = path.join(repoRoot, 'Frontend', 'src', 'Report', 'config', 'reportConfigs.ts');
const statusPath = path.join(repoRoot, 'ReportColumnUiFixStatus.md');

const explicitAliases = new Map([
  ['CardListsByCompanyRegistrationNumber', ['CardListsByPaThaKa']],
  ['CompanyProfile', ['CompanyProfileReport']],
  ['EIRCardBindReport', ['PathakaBindReport']],
  ['ListOfCompany', ['PaThaKaAllReport']],
  ['ListOfDirectorsByCompanyRegistrationNo', ['DirectorListByCompanyRegistrationNoReport']],
  ['ListOfDirectors', ['DirectorListReport']],
  ['ListOfTopCapitalCompany', ['TopCapitalCompanyReport']],
  ['ListOfValidAndInvalidCompany', ['ValidInvalidCompanyReport']],
  ['MPUReportV3', ['MPUReport']],
  ['RegistrationByBusinessType', ['PaThaKaRegistrationByBusinessTypeReport']],
  ['RegistrationByVoucher', ['PaThaKaRegistrationByVoucherReport']],
]);

const decodeXml = (value) =>
  value
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&amp;/g, '&')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'");

const normalizeWhitespace = (value) => value.replace(/\s+/g, ' ').trim();

const quote = (value) => `'${String(value).replace(/\\/g, '\\\\').replace(/'/g, "\\'")}'`;

const toPascalCase = (value) => {
  const words = String(value ?? '').match(/[A-Za-z0-9]+/g) ?? ['Column'];
  return words
    .map((word) => `${word.charAt(0).toUpperCase()}${word.slice(1)}`)
    .join('');
};

const toCamelCase = (value) => {
  const pascal = toPascalCase(value);
  return `${pascal.charAt(0).toLowerCase()}${pascal.slice(1)}`;
};

const normalizeColumn = (value) => {
  let normalized = decodeXml(String(value ?? ''))
    .replace(/^=Parameters!([A-Za-z0-9_]+)\.Value$/i, 'dynamic$1')
    .replace(/^=.+$/i, '')
    .toLowerCase()
    .replace(/\blicence\b/g, 'license')
    .replace(/\blicences\b/g, 'licenses')
    .replace(/[^a-z0-9]+/g, '');

  const synonyms = new Map([
    ['srno', 'no'],
    ['sr', 'no'],
    ['serialno', 'no'],
    ['number', 'no'],
    ['curency', 'currency'],
    ['hscode', 'hscode'],
    ['hsdescription', 'hsdescription'],
    ['description', 'hsdescription'],
    ['decription', 'hsdescription'],
    ['au', 'unit'],
    ['qty', 'quantity'],
    ['countryoforign', 'countryoforigin'],
    ['countryoforigion', 'countryoforigin'],
    ['placeportofdischarge', 'portofdischarge'],
    ['placeofdischarge', 'portofdischarge'],
    ['placeportofexport', 'portofexport'],
    ['placeofexport', 'portofexport'],
    ['placeportofshipment', 'portofshipment'],
    ['placeofshipment', 'portofshipment'],
    ['countryofdestination', 'destinationcountry'],
    ['section', 'sectionname'],
    ['sakhan', 'sakhancode'],
    ['method', 'methodname'],
    ['value', 'amount'],
    ['totalvalue', 'amount'],
    ['validdate', 'enddate'],
    ['micpermitno', 'micpermitno'],
    ['lineofbusiness', 'lineofbusiness'],
    ['unioncitizenshipno', 'nrcno'],
    ['typeofpermit', 'permittype'],
  ]);

  return synonyms.get(normalized) ?? normalized;
};

const fieldNamesFromExpression = (value) =>
  [...String(value ?? '').matchAll(/Fields!([A-Za-z0-9_]+)\.Value/g)].map((match) => match[1]);

const uniqueBy = (values, keySelector) => {
  const seen = new Set();
  const result = [];

  for (const value of values) {
    const key = keySelector(value);
    if (!key || seen.has(key)) {
      continue;
    }

    seen.add(key);
    result.push(value);
  }

  return result;
};

const isNoColumn = (title) => normalizeColumn(title) === 'no';

const readRdlcRows = (filePath) => {
  const xml = fs.readFileSync(filePath, 'utf8');
  return [...xml.matchAll(/<TablixRow\b[\s\S]*?<\/TablixRow>/g)].map((rowMatch) =>
    [...rowMatch[0].matchAll(/<TablixCell\b[\s\S]*?<\/TablixCell>/g)].map((cellMatch) => {
      const values = [...cellMatch[0].matchAll(/<Value>([\s\S]*?)<\/Value>/g)]
        .map((match) => normalizeWhitespace(decodeXml(match[1])))
        .filter(Boolean);

      return {
        text: normalizeWhitespace(values.join(' ')),
        fields: values.flatMap(fieldNamesFromExpression),
        isAggregate: values.some((value) => /=\s*(FORMAT\()?((Sum|CountDistinct|Count)\()/i.test(value)),
      };
    }).filter((cell) => cell.text),
  );
};

const extractRdlcColumns = (filePath) => {
  const rows = readRdlcRows(filePath);
  const candidates = [];

  rows.forEach((row, rowIndex) => {
    const staticCount = row.filter(
      (cell) =>
        !cell.text.startsWith('=Fields!') &&
        !cell.text.startsWith('=RowNumber') &&
        !cell.text.startsWith('=FORMAT') &&
        !cell.text.startsWith('=IIF') &&
        !cell.text.startsWith('=Count') &&
        !cell.text.startsWith('=Sum') &&
        !cell.text.startsWith('=Variables!'),
    ).length;

    if (row.length > 1 && staticCount > 1) {
      candidates.push({ row, rowIndex, staticCount });
    }
  });

  if (!candidates.length) {
    return [];
  }

  candidates.sort((left, right) => {
    if (right.staticCount !== left.staticCount) {
      return right.staticCount - left.staticCount;
    }

    return right.row.length - left.row.length;
  });

  const candidate = candidates[0];
  const dataRow = rows[candidate.rowIndex + 1] ?? [];
  const columns = candidate.row.map((cell, index) => {
    const dataCell = dataRow[index];

    return {
      title: cell.text,
      fields: dataCell?.fields ?? [],
      isAggregate: Boolean(dataCell?.isAggregate),
    };
  });

  return uniqueBy(columns, (column) => normalizeColumn(column.title));
};

const parseOldRdlcReports = () => {
  const reports = new Map();

  for (const entry of fs.readdirSync(oldReportRoot, { withFileTypes: true })) {
    if (!entry.isFile() || !entry.name.toLowerCase().endsWith('.rdlc')) {
      continue;
    }

    const basename = path.basename(entry.name, '.rdlc').trim();
    reports.set(basename, {
      name: basename,
      fileName: entry.name,
      columns: extractRdlcColumns(path.join(oldReportRoot, entry.name)),
    });
  }

  return reports;
};

const parseOldControllerMap = () => {
  const source = fs.readFileSync(oldReportsControllerPath, 'utf8');
  const lines = source.split(/\r?\n/);
  const map = new Map();
  let currentMethod = '';

  const addMapping = (name, rdlc) => {
    if (!name || !rdlc) {
      return;
    }

    const cleanedName = name.replace(/^Bind/, '');
    const basename = path.basename(rdlc, '.rdlc').trim();
    const list = map.get(cleanedName) ?? [];

    if (!list.includes(basename)) {
      list.push(basename);
    }

    map.set(cleanedName, list);
  };

  for (const line of lines) {
    const methodMatch = line.match(/\b(?:public|private)\s+(?:static\s+)?(?:async\s+)?(?:[A-Za-z0-9_<>,\[\]\?]+\s+)+([A-Za-z0-9_]+)\s*\(/);
    if (methodMatch) {
      currentMethod = methodMatch[1];
    }

    const rdlcMatch = line.match(/ReportControl\\([^"\\]+\.rdlc)/);
    if (rdlcMatch) {
      addMapping(currentMethod, rdlcMatch[1]);
    }
  }

  return map;
};

const candidateOldNames = (newName) => {
  if (explicitAliases.has(newName)) {
    return explicitAliases.get(newName);
  }

  const candidates = [newName];

  if (newName.endsWith('DetailReportPending')) {
    candidates.push(newName.replace('DetailReportPending', 'PendingDetailReport'));
    candidates.push(newName.replace('DetailReportPending', 'DetailReport'));
  }

  if (newName.endsWith('CompanyListReport')) {
    candidates.push(newName.replace('CompanyListReport', 'ByCompanyReport'));
  }

  if (newName.endsWith('BySellerCountryReport') && newName.includes('Export')) {
    candidates.push(newName.replace('BySellerCountryReport', 'ByBuyerCountryReport'));
  }

  if (newName.endsWith('DailyReportNewLicenceReport')) {
    candidates.push(newName.replace('DailyReportNewLicenceReport', 'ByDailyReport'));
  }

  if (newName.endsWith('DailyReportNewPermitReport')) {
    candidates.push(newName.replace('DailyReportNewPermitReport', 'ByDailyReport'));
  }

  if (newName.endsWith('NewReportNewReport')) {
    candidates.push(newName.replace('NewReportNewReport', 'NewReport'));
  }

  if (newName.endsWith('TotalValueLicencesReport')) {
    candidates.push(newName.replace('TotalValueLicencesReport', 'TotalValueLicenceReport'));
    candidates.push(newName.replace('TotalValueLicencesReport', 'ByTotalValueLicenceReport'));
  }

  if (newName.endsWith('CancellationReport')) {
    candidates.push(newName.replace('CancellationReport', 'CancelReport'));
  }

  if (newName.endsWith('ActualAmendmentReport')) {
    candidates.push(newName.replace('ActualAmendmentReport', 'ActualAmendReport'));
    candidates.push(newName.startsWith('Border') ? 'BorderAmendReport' : 'AmendReport');
  }

  if (newName.endsWith('AmendmentReport')) {
    candidates.push(newName.replace('AmendmentReport', 'AmendReport'));
    candidates.push(newName.startsWith('Border') ? 'BorderAmendReport' : 'AmendReport');
  }

  if (newName.endsWith('ExtensionReport')) {
    candidates.push(newName.startsWith('Border') ? 'BorderExtensionReport' : 'ExtensionReport');
  }

  if (newName.endsWith('VoucherReport')) {
    if (newName.startsWith('Border')) {
      candidates.push('BorderVoucherReport');
    } else if (newName.startsWith('ExportLicence')) {
      candidates.push('VoucherReport_Export');
    } else {
      candidates.push('VoucherReport');
    }
  }

  if (newName.endsWith('PendingReport')) {
    candidates.push('PendingLicenceReport');
  }

  if (newName.endsWith('NewReport')) {
    candidates.push(newName.startsWith('Border') ? 'BorderNewReport' : 'NewLicenceReport');
  }

  if (newName.endsWith('ByHSCodeReport')) {
    candidates.push(newName.startsWith('Border') ? 'BorderHSCodeReport' : 'HSCodeReport');
    candidates.push('HSCodeDetailReport');
  }

  return uniqueBy(candidates, (candidate) => candidate);
};

const resolveOldSources = (newName, oldRdlcReports, oldControllerMap) => {
  const resolved = [];

  for (const candidate of candidateOldNames(newName)) {
    const mapped = oldControllerMap.get(candidate);
    const names = mapped?.length ? mapped : [candidate];

    for (const name of names) {
      if (oldRdlcReports.has(name) && !resolved.includes(name)) {
        resolved.push(name);
      }
    }
  }

  return resolved;
};

const getPropertyName = (node, sourceFile) => {
  if (!node) {
    return undefined;
  }

  if (ts.isIdentifier(node) || ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) {
    return node.text;
  }

  return node.getText(sourceFile);
};

const getLiteralValue = (node) => {
  if (!node) {
    return undefined;
  }

  if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) {
    return node.text;
  }

  if (ts.isNumericLiteral(node)) {
    return Number(node.text);
  }

  if (node.kind === ts.SyntaxKind.TrueKeyword) {
    return true;
  }

  if (node.kind === ts.SyntaxKind.FalseKeyword) {
    return false;
  }

  return undefined;
};

const getObjectProperty = (objectLiteral, propertyName, sourceFile) =>
  objectLiteral.properties.find(
    (property) =>
      ts.isPropertyAssignment(property) &&
      getPropertyName(property.name, sourceFile) === propertyName,
  )?.initializer;

const parseObjectLiteral = (objectLiteral, sourceFile) => {
  const value = {};

  for (const property of objectLiteral.properties) {
    if (!ts.isPropertyAssignment(property)) {
      continue;
    }

    value[getPropertyName(property.name, sourceFile)] = getLiteralValue(property.initializer);
  }

  return value;
};

const readBaselineNewConfig = () => {
  try {
    return execFileSync('git', ['show', 'HEAD:Frontend/src/Report/config/reportConfigs.ts'], {
      cwd: repoRoot,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore'],
    });
  } catch {
    return fs.readFileSync(newConfigPath, 'utf8');
  }
};

const parseNewConfigs = () => {
  const sourceText = readBaselineNewConfig();
  const sourceFile = ts.createSourceFile(
    newConfigPath,
    sourceText,
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind.TS,
  );
  const reports = [];

  const visit = (node) => {
    if (
      ts.isVariableDeclaration(node) &&
      ts.isIdentifier(node.name) &&
      node.name.text === 'reportConfigs' &&
      node.initializer &&
      ts.isObjectLiteralExpression(node.initializer)
    ) {
      for (const reportProperty of node.initializer.properties) {
        if (!ts.isPropertyAssignment(reportProperty) || !ts.isObjectLiteralExpression(reportProperty.initializer)) {
          continue;
        }

        const name = getPropertyName(reportProperty.name, sourceFile);
        const config = reportProperty.initializer;
        const report = {
          name,
          controllerName: getLiteralValue(getObjectProperty(config, 'controllerName', sourceFile)),
          title: getLiteralValue(getObjectProperty(config, 'title', sourceFile)),
          apiRoute: getLiteralValue(getObjectProperty(config, 'apiRoute', sourceFile)),
          excelRoute: getLiteralValue(getObjectProperty(config, 'excelRoute', sourceFile)),
          excelFileName: getLiteralValue(getObjectProperty(config, 'excelFileName', sourceFile)),
          initialSortColumn: getLiteralValue(getObjectProperty(config, 'initialSortColumn', sourceFile)),
          showRowNumber: getLiteralValue(getObjectProperty(config, 'showRowNumber', sourceFile)),
          filters: [],
          columns: [],
        };

        const filters = getObjectProperty(config, 'filters', sourceFile);
        if (filters && ts.isArrayLiteralExpression(filters)) {
          report.filters = filters.elements
            .filter(ts.isObjectLiteralExpression)
            .map((filter) => parseObjectLiteral(filter, sourceFile));
        }

        const columns = getObjectProperty(config, 'columns', sourceFile);
        if (columns && ts.isArrayLiteralExpression(columns)) {
          report.columns = columns.elements
            .filter(ts.isObjectLiteralExpression)
            .map((column) => parseObjectLiteral(column, sourceFile));
        }

        reports.push(report);
      }
    }

    ts.forEachChild(node, visit);
  };

  visit(sourceFile);
  return reports;
};

const reportDirection = (reportName) => {
  if (reportName.includes('Import')) {
    return 'Import';
  }

  if (reportName.includes('Export')) {
    return 'Export';
  }

  return '';
};

const oldFilterLabel = (reportName, filter) => {
  const direction = reportDirection(reportName);

  const labels = {
    AmendRemarkId: 'Amend Remark',
    ApplyType: 'Apply Type',
    Auto: 'Auto',
    BusinessTypeId: 'Business Type',
    BuyerCountryId: 'Buyer Country',
    ChequeNoId: 'Cheque No',
    CompanyRegistrationNo: 'Company Registration No',
    Date: 'Date',
    FilterType: 'Filter Type',
    FormType: 'Form Type',
    HSCode: 'HS Code',
    LineofBusinessId: 'Line of Business',
    Name: 'Name',
    Nationality: 'Nationality',
    NRCNo: 'NRC No',
    NRCPrefixCodeId: 'NRC Prefix Code',
    NRCPrefixId: 'NRC Prefix',
    NRCType: 'NRC Type',
    PaThaKaTypeId: 'PaThaKa Type',
    PaymentType: 'Payment Type',
    SakhanId: 'Sakhan',
    SellerCountryId: 'Seller Country',
    State: 'State',
    Status: 'Status',
    Type: 'Type',
  };

  if (filter.name === 'dateRange') {
    return 'From Date / To Date';
  }

  if (filter.name === 'ExportImportSectionId') {
    return direction ? `${direction} Section` : 'Section';
  }

  if (filter.name === 'ExportImportMethodId') {
    return direction ? `${direction} Method` : 'Method';
  }

  if (filter.name === 'ExportImportIncotermId') {
    return direction ? `${direction} Incoterms` : 'Incoterms';
  }

  return labels[filter.name] ?? filter.label;
};

const updateFilters = (report) =>
  report.filters.map((filter) => {
    const updated = {
      ...filter,
      label: oldFilterLabel(report.name, filter),
    };

    if (filter.type === 'dateRange') {
      updated.fromLabel = 'From Date';
      updated.toLabel = 'To Date';
    }

    return updated;
  });

const addColumnAliases = (columns) => {
  const aliases = new Map();

  for (const column of columns) {
    const values = [column.key, column.dataIndex, column.title].filter(Boolean);
    for (const value of values) {
      aliases.set(normalizeColumn(value), column);
    }
  }

  for (const column of columns) {
    aliases.set(String(column.key ?? '').toLowerCase(), column);
    aliases.set(String(column.dataIndex ?? '').toLowerCase(), column);
  }

  return aliases;
};

const fieldAliasCandidates = (fieldName) => {
  const direct = toPascalCase(fieldName);
  const aliases = {
    CompanyAddress: ['CompanyAddress'],
    MICPermitNo: ['MICPermitNo', 'MicpermitNo'],
    Mobile: ['Mobile1'],
    PermitNo: ['LicenceNo'],
    PermitDate: ['LicenceDate'],
    SDate: ['SDate', 'Date'],
    sDate: ['SDate', 'Date'],
    sEndDate: ['EndDate'],
    sIssuedDate: ['IssuedDate'],
    sLicenceDate: ['SLicenceDate', 'LicenceDate'],
    sVoucherDate: ['SVoucherDate', 'VoucherDate'],
    sCurrency: ['Currency'],
    sLicenceValue: ['Amount'],
    TotalValue: ['Amount', 'TotalValue'],
    TypeOfPermit: ['PermitType'],
    UnionCitizenshipNo: ['NRCNo'],
  };

  return uniqueBy([...(aliases[fieldName] ?? []), direct], (candidate) => candidate.toLowerCase());
};

const fallbackColumnFor = (oldColumn, newColumns) => {
  const normalized = normalizeColumn(oldColumn.title);
  const currentKeys = new Set(newColumns.map((column) => String(column.dataIndex ?? '').toLowerCase()));

  const make = (dataIndex, fallbackDataIndexes) => ({
    key: toPascalCase(oldColumn.title),
    dataIndex,
    title: oldColumn.title,
    fallbackDataIndexes,
  });

  const companyAddressFields = [
    'unitLevel',
    'streetNumberStreetName',
    'quarterCityTownship',
    'state',
    'country',
    'postalCode',
  ].filter((field) => currentKeys.has(field.toLowerCase()));

  if (normalized === 'companyaddress' && companyAddressFields.length) {
    return make('companyAddress', companyAddressFields);
  }

  const directorAddressFields = [
    'directorUnitLevel',
    'directorStreetNumberStreetName',
    'directorQuarterCityTownship',
    'directorState',
    'directorCountry',
    'directorPostalCode',
  ].filter((field) => currentKeys.has(field.toLowerCase()));

  if (normalized === 'directoraddress' && directorAddressFields.length) {
    return make('directorAddress', directorAddressFields);
  }

  return undefined;
};

const matchOldColumn = (oldColumn, newColumns) => {
  const aliases = addColumnAliases(newColumns);
  const normalizedTitle = normalizeColumn(oldColumn.title);

  if (aliases.has(normalizedTitle)) {
    return aliases.get(normalizedTitle);
  }

  for (const fieldName of oldColumn.fields) {
    for (const candidate of fieldAliasCandidates(fieldName)) {
      const normalizedCandidate = normalizeColumn(candidate);
      const lowerCandidate = candidate.charAt(0).toLowerCase() + candidate.slice(1);

      if (aliases.has(normalizedCandidate)) {
        return aliases.get(normalizedCandidate);
      }

      if (aliases.has(candidate.toLowerCase())) {
        return aliases.get(candidate.toLowerCase());
      }

      if (aliases.has(lowerCandidate.toLowerCase())) {
        return aliases.get(lowerCandidate.toLowerCase());
      }
    }
  }

  return fallbackColumnFor(oldColumn, newColumns);
};

const updateColumns = (report, oldSources, oldRdlcReports) => {
  const allOldColumns = uniqueBy(
    oldSources.flatMap((source) => oldRdlcReports.get(source)?.columns ?? []),
    (column) => normalizeColumn(column.title),
  );
  const showRowNumber = allOldColumns.some((column) => isNoColumn(column.title));
  const oldColumns = allOldColumns.filter((column) => !isNoColumn(column.title));
  const usedKeys = new Map();
  const missing = [];
  const aggregateNotes = [];
  const dynamicHeaders = [];
  const columns = [];

  for (const oldColumn of oldColumns) {
    const baseKey = toPascalCase(oldColumn.title);
    const keyCount = usedKeys.get(baseKey) ?? 0;
    const key = keyCount ? `${baseKey}${keyCount + 1}` : baseKey;
    usedKeys.set(baseKey, keyCount + 1);

    if (oldColumn.title.startsWith('=Parameters!')) {
      dynamicHeaders.push(oldColumn.title);
    }

    if (oldColumn.isAggregate) {
      aggregateNotes.push(oldColumn.title);
      columns.push({
        key,
        dataIndex: toCamelCase(oldColumn.title),
        title: oldColumn.title,
      });
      missing.push(oldColumn.title);
      continue;
    }

    const match = matchOldColumn(oldColumn, report.columns);

    if (match) {
      columns.push({
        key,
        dataIndex: match.dataIndex ?? match.key,
        title: oldColumn.title,
        ...(match.dataType ? { dataType: match.dataType } : {}),
        ...(match.fallbackDataIndexes ? { fallbackDataIndexes: match.fallbackDataIndexes } : {}),
      });
      continue;
    }

    const placeholderDataIndex = toCamelCase(oldColumn.title);
    columns.push({
      key,
      dataIndex: placeholderDataIndex,
      title: oldColumn.title,
    });
    missing.push(oldColumn.title);
  }

  return {
    columns,
    oldColumns,
    showRowNumber,
    missing,
    aggregateNotes,
    dynamicHeaders,
  };
};

const propertyLine = (key, value) => {
  if (value === undefined) {
    return undefined;
  }

  if (typeof value === 'string') {
    return `${key}: ${quote(value)}`;
  }

  if (typeof value === 'number' || typeof value === 'boolean') {
    return `${key}: ${value}`;
  }

  if (Array.isArray(value)) {
    return `${key}: [${value.map(quote).join(', ')}]`;
  }

  return undefined;
};

const renderObject = (object, indent, keys) => {
  const pad = ' '.repeat(indent);
  const innerPad = ' '.repeat(indent + 2);
  const lines = [`${pad}{`];

  for (const key of keys) {
    const line = propertyLine(key, object[key]);
    if (line) {
      lines.push(`${innerPad}${line},`);
    }
  }

  lines.push(`${pad}}`);
  return lines;
};

const renderConfig = (reports) => {
  const lines = [
    "import { ReportPageConfig } from './reportTypes';",
    '',
    'export const reportConfigs: Record<string, ReportPageConfig> = {',
  ];

  for (const report of reports) {
    lines.push(`  ${report.name}: {`);
    for (const key of ['controllerName', 'title', 'apiRoute', 'excelRoute', 'excelFileName', 'initialSortColumn', 'showRowNumber']) {
      const line = propertyLine(key, report[key]);
      if (line) {
        lines.push(`    ${line},`);
      }
    }

    lines.push('    filters: [');
    for (const filter of report.filters) {
      lines.push(...renderObject(filter, 6, [
        'name',
        'label',
        'type',
        'fromName',
        'toName',
        'fromLabel',
        'toLabel',
        'defaultValue',
        'required',
      ]).map((line, index, array) => (index === array.length - 1 ? `${line},` : line)));
    }
    lines.push('    ],');

    lines.push('    columns: [');
    for (const column of report.columns) {
      lines.push(...renderObject(column, 6, [
        'key',
        'dataIndex',
        'title',
        'dataType',
        'fallbackDataIndexes',
      ]).map((line, index, array) => (index === array.length - 1 ? `${line},` : line)));
    }
    lines.push('    ],');
    lines.push('  },');
  }

  lines.push('};');
  lines.push('');
  lines.push('export const reportConfigList = Object.values(reportConfigs);');
  lines.push('');
  return lines.join('\n');
};

const formatList = (values) => values.length ? values.map((value) => `\`${value}\``).join(', ') : '_None_';

const renderStatus = (rows) => {
  const finished = rows.filter((row) => row.finished);
  const pending = rows.filter((row) => !row.finished);
  const lines = [
    '# Report Column UI Fix Status',
    '',
    `Generated: ${new Date().toISOString()}`,
    '',
    `Old source: \`${oldAdminRoot}\``,
    `New source: \`Frontend/src/Report/config/reportConfigs.ts\``,
    '',
    'The frontend report table columns were reordered and relabeled from the old RDLC visible headers. Filters were relabeled to old UI wording where the current generic report screen supports the same filter.',
    '',
    'A report is marked `Finished` when every old visible table header has a frontend column mapping. `Not finished` means the label is present, but one or more columns still need backend/computed data or a runtime dynamic header to fully match the old report behavior.',
    '',
    '## Summary',
    '',
    `- Reports checked: ${rows.length}`,
    `- Finished: ${finished.length}`,
    `- Not finished: ${pending.length}`,
    '',
    '## Per-Report Status',
    '',
  ];

  for (const row of rows) {
    lines.push(`### ${row.name}`);
    lines.push('');
    lines.push(`Status: ${row.finished ? 'Finished' : 'Not finished'}`);
    lines.push(`Old source: ${formatList(row.oldSources.map((source) => `${source}.rdlc`))}`);
    lines.push(`Table labels: Finished`);
    lines.push(`Filters: Finished`);
    lines.push(`Columns needing data/computed support: ${formatList(row.missing)}`);
    lines.push(`Old aggregate labels kept on detail-backed columns: ${formatList(row.aggregateNotes)}`);
    lines.push(`Runtime dynamic headers needing manual label choice: ${formatList(row.dynamicHeaders)}`);
    lines.push('');
  }

  return `${lines.join('\n')}\n`;
};

const apply = () => {
  const reports = parseNewConfigs();
  const oldRdlcReports = parseOldRdlcReports();
  const oldControllerMap = parseOldControllerMap();
  const statusRows = [];

  for (const report of reports) {
    const oldSources = resolveOldSources(report.name, oldRdlcReports, oldControllerMap);
    const columnResult = updateColumns(report, oldSources, oldRdlcReports);
    report.filters = updateFilters(report);
    report.columns = columnResult.columns;
    report.showRowNumber = columnResult.showRowNumber;

    statusRows.push({
      name: report.name,
      oldSources,
      missing: columnResult.missing,
      aggregateNotes: columnResult.aggregateNotes,
      dynamicHeaders: columnResult.dynamicHeaders,
      finished:
        oldSources.length > 0 &&
        columnResult.missing.length === 0 &&
        columnResult.aggregateNotes.length === 0 &&
        columnResult.dynamicHeaders.length === 0,
    });
  }

  fs.writeFileSync(newConfigPath, renderConfig(reports), 'utf8');
  fs.writeFileSync(statusPath, renderStatus(statusRows), 'utf8');

  console.log(`Updated ${path.relative(repoRoot, newConfigPath)}`);
  console.log(`Wrote ${path.relative(repoRoot, statusPath)}`);
  console.log(`Reports checked: ${statusRows.length}`);
  console.log(`Finished: ${statusRows.filter((row) => row.finished).length}`);
  console.log(`Not finished: ${statusRows.filter((row) => !row.finished).length}`);
};

apply();

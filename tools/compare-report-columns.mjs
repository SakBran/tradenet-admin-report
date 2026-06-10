import fs from 'node:fs';
import path from 'node:path';
import ts from '../Frontend/node_modules/typescript/lib/typescript.js';

const repoRoot = process.cwd();
const oldAdminRoot = path.resolve(repoRoot, '..', 'tradenet-2.0-admin', 'TradenetAdmin');
const oldReportRoot = path.join(oldAdminRoot, 'ReportControl');
const oldReportsControllerPath = path.join(oldAdminRoot, 'Controllers', 'ReportsController.cs');
const newConfigPath = path.join(repoRoot, 'Frontend', 'src', 'Report', 'config', 'reportConfigs.ts');
const outputPath = path.join(repoRoot, 'docs', 'ReportColumnComparison.md');

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

const unique = (values) => {
  const seen = new Set();
  const result = [];

  for (const value of values) {
    const key = normalizeColumn(value);
    if (!key || seen.has(key)) {
      continue;
    }

    seen.add(key);
    result.push(value);
  }

  return result;
};

const normalizeColumn = (value) => {
  let normalized = decodeXml(String(value ?? ''))
    .replace(/^=Parameters!([A-Za-z0-9_]+)\.Value$/i, 'dynamic $1')
    .replace(/^=.+$/i, '')
    .toLowerCase()
    .replace(/\blicence\b/g, 'license')
    .replace(/\blicences\b/g, 'licenses')
    .replace(/\bnrc\b/g, 'nrc')
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
    ['countryoforigin', 'countryoforigin'],
    ['placeportofdischarge', 'portofdischarge'],
    ['placeofdischarge', 'portofdischarge'],
    ['portofdischarge', 'portofdischarge'],
    ['placeportofexport', 'portofexport'],
    ['placeofexport', 'portofexport'],
    ['placeportofshipment', 'portofexport'],
    ['placeofshipment', 'portofexport'],
    ['portofshipment', 'portofexport'],
    ['portofexport', 'portofexport'],
    ['countryofdestination', 'destinationcountry'],
    ['section', 'sectionname'],
    ['sakhan', 'sakhanname'],
    ['method', 'methodname'],
    ['value', 'amount'],
    ['licvalue', 'licensevalue'],
    ['licensevalue', 'licensevalue'],
    ['validdate', 'enddate'],
    ['issueddate', 'issueddate'],
  ]);

  if (synonyms.has(normalized)) {
    normalized = synonyms.get(normalized);
  }

  return normalized;
};

const formatList = (values) => {
  if (!values.length) {
    return '_None_';
  }

  return values.map((value) => `\`${value}\``).join(', ');
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

const getStringValue = (node) => {
  if (!node) {
    return undefined;
  }

  if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) {
    return node.text;
  }

  return undefined;
};

const getBooleanValue = (node) => {
  if (!node) {
    return undefined;
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

const parseNewConfigs = () => {
  const sourceText = fs.readFileSync(newConfigPath, 'utf8');
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

        const reportName = getPropertyName(reportProperty.name, sourceFile);
        const config = reportProperty.initializer;
        const title = getStringValue(getObjectProperty(config, 'title', sourceFile)) ?? reportName;
        const showRowNumber = getBooleanValue(getObjectProperty(config, 'showRowNumber', sourceFile)) ?? true;
        const columnsArray = getObjectProperty(config, 'columns', sourceFile);
        const columns = [];

        if (columnsArray && ts.isArrayLiteralExpression(columnsArray)) {
          for (const column of columnsArray.elements) {
            if (!ts.isObjectLiteralExpression(column)) {
              continue;
            }

            const key = getStringValue(getObjectProperty(column, 'key', sourceFile));
            const titleValue = getStringValue(getObjectProperty(column, 'title', sourceFile));
            columns.push(titleValue ?? key);
          }
        }

        reports.push({
          name: reportName,
          title,
          columns: [...(showRowNumber ? ['No'] : []), ...columns.filter(Boolean)],
        });
      }
    }

    ts.forEachChild(node, visit);
  };

  visit(sourceFile);
  return reports;
};

const extractRdlcHeaderColumns = (filePath) => {
  const xml = fs.readFileSync(filePath, 'utf8');
  const rowMatches = [...xml.matchAll(/<TablixRow\b[\s\S]*?<\/TablixRow>/g)];
  const candidates = [];

  for (const rowMatch of rowMatches) {
    const rowXml = rowMatch[0];
    const cellMatches = [...rowXml.matchAll(/<TablixCell\b[\s\S]*?<\/TablixCell>/g)];
    const columns = [];

    for (const cellMatch of cellMatches) {
      const values = [...cellMatch[0].matchAll(/<Value>([\s\S]*?)<\/Value>/g)]
        .map((match) => normalizeWhitespace(decodeXml(match[1])))
        .filter(Boolean);
      const columnText = normalizeWhitespace(values.join(' '));

      if (columnText) {
        columns.push(columnText);
      }
    }

    const staticCount = columns.filter((column) => !column.startsWith('=Fields!') && !column.startsWith('=RowNumber') && !column.startsWith('=FORMAT') && !column.startsWith('=IIF') && !column.startsWith('=Count') && !column.startsWith('=Sum') && !column.startsWith('=Variables!')).length;

    if (columns.length > 1 && staticCount > 1) {
      candidates.push({ columns, staticCount });
    }
  }

  if (!candidates.length) {
    return [];
  }

  candidates.sort((left, right) => {
    if (right.staticCount !== left.staticCount) {
      return right.staticCount - left.staticCount;
    }

    return right.columns.length - left.columns.length;
  });

  return unique(candidates[0].columns);
};

const parseOldRdlcReports = () => {
  const entries = fs
    .readdirSync(oldReportRoot, { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.toLowerCase().endsWith('.rdlc'));
  const reports = new Map();

  for (const entry of entries) {
    const filePath = path.join(oldReportRoot, entry.name);
    const basename = path.basename(entry.name, '.rdlc').trim();
    reports.set(basename, {
      name: basename,
      fileName: entry.name,
      columns: extractRdlcHeaderColumns(filePath),
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

  if (newName.endsWith('CancelReport')) {
    candidates.push(newName.startsWith('Border') ? 'BorderCancelReport' : 'CancelReport');
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

  return unique(candidates);
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

const diffColumns = (oldColumns, newColumns) => {
  const oldKeys = new Set(oldColumns.map(normalizeColumn).filter(Boolean));
  const newKeys = new Set(newColumns.map(normalizeColumn).filter(Boolean));

  return {
    missingInNew: oldColumns.filter((column) => {
      const key = normalizeColumn(column);
      return key && !newKeys.has(key);
    }),
    extraInNew: newColumns.filter((column) => {
      const key = normalizeColumn(column);
      return key && !oldKeys.has(key);
    }),
  };
};

const generateReport = () => {
  const newReports = parseNewConfigs();
  const oldRdlcReports = parseOldRdlcReports();
  const oldControllerMap = parseOldControllerMap();
  const rows = [];
  const usedOldSources = new Set();

  for (const report of newReports) {
    const oldSourceNames = resolveOldSources(report.name, oldRdlcReports, oldControllerMap);
    oldSourceNames.forEach((name) => usedOldSources.add(name));

    const oldColumns = unique(
      oldSourceNames.flatMap((name) => oldRdlcReports.get(name)?.columns ?? []),
    );
    const { missingInNew, extraInNew } = diffColumns(oldColumns, report.columns);

    rows.push({
      ...report,
      oldSourceNames,
      oldColumns,
      missingInNew,
      extraInNew,
    });
  }

  const unmatchedNew = rows.filter((row) => row.oldSourceNames.length === 0);
  const oldNotMapped = [...oldRdlcReports.keys()]
    .filter((name) => !usedOldSources.has(name))
    .sort((left, right) => left.localeCompare(right));
  const withMissing = rows.filter((row) => row.missingInNew.length > 0);
  const withExtra = rows.filter((row) => row.extraInNew.length > 0);

  const lines = [
    '# Report Column Comparison',
    '',
    `Generated: ${new Date().toISOString()}`,
    '',
    `Old source: \`${oldReportRoot}\\*.rdlc\` from the old Tradenet 2.0 Admin project.`,
    `New source: \`${path.relative(repoRoot, newConfigPath)}\` plus the conditional \`No\` column rendered by \`BasicTable\`.`,
    '',
    'Comparison uses visible RDLC table headers from the old report viewer and visible React table columns from the new frontend config. `Need in new` means the old report showed the column but the new table does not. `Extra in new` means the new table shows a column that was not visible in the old RDLC table.',
    '',
    '## Summary',
    '',
    `- New frontend report configs checked: ${newReports.length}`,
    `- New reports matched to an old RDLC source: ${newReports.length - unmatchedNew.length}`,
    `- New reports without an old RDLC match: ${unmatchedNew.length}`,
    `- Reports with old columns missing in new: ${withMissing.length}`,
    `- Reports with extra new columns: ${withExtra.length}`,
    `- Old RDLC files not mapped to current frontend reports: ${oldNotMapped.length}`,
    '',
  ];

  if (unmatchedNew.length) {
    lines.push('## New Reports Without Old Match', '');
    for (const row of unmatchedNew) {
      lines.push(`- \`${row.name}\``);
    }
    lines.push('');
  }

  lines.push('## Per-Report Comparison', '');

  for (const row of rows) {
    lines.push(`### ${row.name}`, '');
    lines.push(`Title: ${row.title}`);
    lines.push(`Old source: ${row.oldSourceNames.length ? row.oldSourceNames.map((name) => `\`${oldRdlcReports.get(name)?.fileName ?? `${name}.rdlc`}\``).join(', ') : '_No match found_'}`);
    lines.push(`Old columns (${row.oldColumns.length}): ${formatList(row.oldColumns)}`);
    lines.push(`New columns (${row.columns.length}): ${formatList(row.columns)}`);
    lines.push(`Need in new (${row.missingInNew.length}): ${formatList(row.missingInNew)}`);
    lines.push(`Extra in new (${row.extraInNew.length}): ${formatList(row.extraInNew)}`);
    lines.push('');
  }

  if (oldNotMapped.length) {
    lines.push('## Old RDLC Files Not Mapped To New Frontend', '');
    for (const name of oldNotMapped) {
      lines.push(`- \`${oldRdlcReports.get(name)?.fileName ?? `${name}.rdlc`}\``);
    }
    lines.push('');
  }

  fs.writeFileSync(outputPath, `${lines.join('\n')}\n`, 'utf8');
  return { outputPath, rows, unmatchedNew, oldNotMapped, withMissing, withExtra };
};

const result = generateReport();

console.log(`Wrote ${path.relative(repoRoot, result.outputPath)}`);
console.log(`Reports checked: ${result.rows.length}`);
console.log(`Without old match: ${result.unmatchedNew.length}`);
console.log(`With missing columns: ${result.withMissing.length}`);
console.log(`With extra columns: ${result.withExtra.length}`);
console.log(`Old RDLC files not mapped: ${result.oldNotMapped.length}`);

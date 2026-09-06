import { describe, expect, it } from 'vitest';
import { reportConfigs } from './config/reportConfigs';
import type { ReportFilterConfig } from './config/reportTypes';
import {
  formatDateCell,
  getDerivedFilterValues,
} from './reportPresentation';

describe('getDerivedFilterValues', () => {
  it('posts a constantValue filter without rendering it', () => {
    const filters: ReportFilterConfig[] = [
      { name: 'hsCode', label: 'HS Code', type: 'text', defaultValue: '' },
      { name: 'GroupBy', label: 'Group By', type: 'text', constantValue: 'Company' },
    ];

    expect(getDerivedFilterValues('SomeReport', filters)).toEqual({ GroupBy: 'Company' });
  });

  it('lets the identity-derived FormType win over a pinned one', () => {
    const filters: ReportFilterConfig[] = [
      { name: 'FormType', label: 'Form Type', type: 'text', constantValue: 'Wrong' },
    ];

    expect(getDerivedFilterValues('BorderImportPermitDetailReport', filters)).toEqual({
      FormType: 'Border Import Permit',
    });
  });

  it('marks only the HS Code detail drill as GroupBy Company', () => {
    // The drill shares BorderImportPermitByHSCodeReport's controller; the backend needs
    // this to pick HSCodeDetailReport.rdlc's (HS code, company) grouping over the summary's
    // (HS code, currency). The summary must NOT send it, or its rows would company-split.
    const drill = reportConfigs.BorderImportPermitHSCodeDetailReport;
    const summary = reportConfigs.BorderImportPermitByHSCodeReport;

    expect(getDerivedFilterValues(drill.controllerName, drill.filters)).toEqual({
      FormType: 'Border Import Permit',
      GroupBy: 'Company',
    });
    expect(getDerivedFilterValues(summary.controllerName, summary.filters)).toEqual({
      FormType: 'Border Import Permit',
    });
  });
});

describe('formatDateCell', () => {
  // BorderImportPermitDetailReport.rdlc prints the old model's sLicenceDate / LastDate,
  // which were built with .ToString("dd/MM/yyyy"); the grid must print the same bytes.
  it('renders an ISO date-time with the legacy dd/MM/yyyy pattern', () => {
    expect(formatDateCell('2025-01-13T12:14:20', 'DD/MM/YYYY')).toBe('13/01/2025');
    expect(formatDateCell('2025-04-12T00:00:00', 'DD/MM/YYYY')).toBe('12/04/2025');
  });

  it('treats blank and DateTime.MinValue as no value', () => {
    expect(formatDateCell(undefined, 'DD/MM/YYYY')).toBe('N/A');
    expect(formatDateCell(null, 'DD/MM/YYYY')).toBe('N/A');
    expect(formatDateCell('', 'DD/MM/YYYY')).toBe('N/A');
    expect(formatDateCell('0001-01-01T00:00:00', 'DD/MM/YYYY')).toBe('N/A');
  });

  it('shows an unparseable value as received', () => {
    expect(formatDateCell('not a date', 'DD/MM/YYYY')).toBe('not a date');
  });
});

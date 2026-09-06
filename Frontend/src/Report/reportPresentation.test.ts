import { describe, expect, it } from 'vitest';
import { reportConfigs } from './config/reportConfigs';
import type { ReportFilterConfig } from './config/reportTypes';
import { getDerivedFilterValues } from './reportPresentation';

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

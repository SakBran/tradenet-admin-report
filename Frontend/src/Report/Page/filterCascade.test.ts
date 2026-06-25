import { describe, expect, it } from 'vitest';
import { filterOptionsByParent } from './filterCascade';

// Mirrors the live OGA data: each section tagged with its OGADepartmentId.
// e.g. Ministry of Health (deptId 1) owns sections 11 & 12.
const sections = [
  { id: 11, label: 'Department of Public Health', parentId: 1 },
  { id: 12, label: 'Department of Medical Services', parentId: 1 },
  { id: 21, label: 'Forest Department', parentId: 2 },
  { id: 31, label: 'Road Transport', parentId: 3 },
];

describe('filterOptionsByParent (OGA Department -> Section cascade)', () => {
  it('shows ALL sections when no department is selected', () => {
    expect(filterOptionsByParent(sections, 0)).toHaveLength(4);
    expect(filterOptionsByParent(sections, undefined)).toHaveLength(4);
    expect(filterOptionsByParent(sections, '')).toHaveLength(4);
  });

  it('shows ONLY the selected department\'s sections', () => {
    // Selecting Ministry of Health (1) → only its two sections.
    expect(filterOptionsByParent(sections, 1).map((s) => s.id)).toEqual([
      11, 12,
    ]);
    expect(filterOptionsByParent(sections, 2).map((s) => s.id)).toEqual([21]);
    expect(filterOptionsByParent(sections, 3).map((s) => s.id)).toEqual([31]);
  });

  it('accepts the numeric-string value antd selects can emit', () => {
    expect(filterOptionsByParent(sections, '1').map((s) => s.id)).toEqual([
      11, 12,
    ]);
  });

  it('returns nothing for a department with no sections', () => {
    expect(filterOptionsByParent(sections, 99)).toEqual([]);
  });
});

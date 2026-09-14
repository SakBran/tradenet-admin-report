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

// The EICC Licence/Permit reports cascade Product Group from the CARD TYPE, which is a
// name, not an id. Each group is tagged with the side it belongs to, and the old admin
// picked the side by testing the card type's own text
// (eicc-reports.js:69-74 on origin/master).
interface ProductGroupOption {
  id: number;
  label: string;
  parentCode?: string;
}

const productGroups: ProductGroupOption[] = [
  { id: 1, label: 'Rice', parentCode: 'Export' },
  { id: 2, label: 'Beans', parentCode: 'Export' },
  { id: 3, label: 'Machinery', parentCode: 'Import' },
];

describe('filterOptionsByParent (EICC Card Type -> Product Group cascade)', () => {
  it('keeps only the Export groups for an Export card type', () => {
    expect(
      filterOptionsByParent(productGroups, 'Export Licence').map((g) => g.id)
    ).toEqual([1, 2]);
    expect(
      filterOptionsByParent(productGroups, 'Border Export Permit').map(
        (g) => g.id
      )
    ).toEqual([1, 2]);
  });

  it('keeps only the Import groups for an Import card type', () => {
    expect(
      filterOptionsByParent(productGroups, 'Import Permit').map((g) => g.id)
    ).toEqual([3]);
    expect(
      filterOptionsByParent(productGroups, 'Border Import Licence').map(
        (g) => g.id
      )
    ).toEqual([3]);
  });

  it('shows every group when the card type is "--- All ---"', () => {
    expect(filterOptionsByParent(productGroups, '')).toHaveLength(3);
  });

  it('drops an untagged option once a card type is chosen', () => {
    const withUntagged: ProductGroupOption[] = [
      ...productGroups,
      { id: 4, label: 'Unknown' },
    ];
    expect(
      filterOptionsByParent(withUntagged, 'Export Licence').map((g) => g.id)
    ).toEqual([1, 2]);
  });
});

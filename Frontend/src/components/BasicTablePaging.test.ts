import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

// BasicTable is a React component and this suite runs in the Node environment (no jsdom),
// so this is a source contract rather than a render test: it pins the one line that sends a
// re-filtered grid back to page 1.
const source = readFileSync(
  fileURLToPath(new URL('./My Components/Table/BasicTable.tsx', import.meta.url)),
  'utf8'
);

describe('BasicTable pagination', () => {
  it('returns to page 1 when the report filters change', () => {
    // `GenericReportPage` signals a filter change by bumping `refreshKey` (a prop, not React's
    // `key`, so the table is never remounted). Without this reset the grid re-requested whatever
    // page the user was on against the new, usually smaller result set and rendered nothing --
    // the "Sakhan filter shows no data" complaints (Border Import Permit Detail: 70 rows over 7
    // pages, then 20 rows over 2 once a Sakhan is picked).
    expect(source).toMatch(
      /if \(refreshKey !== lastRefreshKey\) \{\s*setLastRefreshKey\(refreshKey\);\s*setPageIndex\(0\);\s*\}/
    );
  });

  it('adjusts the page during render, not from an effect', () => {
    // An effect would let the load effect fire once for the stale page first -- a wasted request
    // and a visible empty flash. Setting state during render makes React re-render before commit.
    const resetIndex = source.indexOf('if (refreshKey !== lastRefreshKey)');
    const loadEffectIndex = source.indexOf('}, [api, enabled, fetch, fetchData, query, refreshKey]);');

    expect(resetIndex).toBeGreaterThan(-1);
    expect(loadEffectIndex).toBeGreaterThan(resetIndex);
  });
});

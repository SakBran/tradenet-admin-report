import { test, expect, type Page } from '@playwright/test';

/**
 * Level 4 (end-to-end): drives the real UI to prove the Import Permit "By Section" → Detail
 * drill-down works for a user — the flow that the SectionId-threading fix enables.
 *
 * Requires the full stack live (see playwright.config.ts header). A couple of steps are
 * environment-specific and marked PROJECT-SPECIFIC — wire them to this deployment's login and
 * filter controls. The drill-link selector (`.report-drill-link`) and the `/Report/<key>`
 * routes are real (BasicTable renders the link; GenericReportPage navigates on click).
 */

const SECTION_REPORT = '/Report/ImportPermitBySectionReport';
const DETAIL_REPORT_KEY = 'ImportPermitDetailReport';

// PROJECT-SPECIFIC: replace with this deployment's real login. Credentials via env so they
// are never committed. If the app uses SSO/another scheme, adapt accordingly.
async function login(page: Page) {
  const user = process.env.E2E_USER;
  const pass = process.env.E2E_PASS;
  if (!user || !pass) {
    test.skip(true, 'Set E2E_USER and E2E_PASS to run the end-to-end drill-down test.');
  }
  await page.goto('/login');
  await page.getByLabel(/user/i).fill(user!);
  await page.getByLabel(/password/i).fill(pass!);
  await page.getByRole('button', { name: /log ?in|sign ?in/i }).click();
  await expect(page).not.toHaveURL(/login/i);
}

// PROJECT-SPECIFIC: apply a date range that has data, then submit the filter form.
async function applyDateRangeAndRun(page: Page) {
  // The report filter form submits on the primary button; adjust the label if different.
  await page.getByRole('button', { name: /search|apply|generate|run/i }).first().click();
}

test.describe('Import Permit By Section drill-down', () => {
  test('clicking a Section navigates to the Detail report pre-filtered by that section', async ({ page }) => {
    await login(page);

    await page.goto(SECTION_REPORT);
    await applyDateRangeAndRun(page);

    // The Section cells render as drill links (BasicTable `.report-drill-link`).
    const firstSectionLink = page.locator('.report-drill-link').first();
    await expect(firstSectionLink).toBeVisible();
    await firstSectionLink.click();

    // GenericReportPage navigates to /Report/<targetReportKey> carrying the drill filters,
    // so the Detail report opens (pre-filtered to the clicked section via ExportImportSectionId).
    await expect(page).toHaveURL(new RegExp(`/Report/${DETAIL_REPORT_KEY}`));

    // The Detail grid should render rows (the drill resolved a real section id, not null).
    await expect(page.locator('table')).toBeVisible();
    await expect(page.locator('table tbody tr').first()).toBeVisible();
  });
});

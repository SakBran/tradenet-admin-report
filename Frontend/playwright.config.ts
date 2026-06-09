import { defineConfig, devices } from '@playwright/test';

// Level 4 (end-to-end) config for the Import Permit drill-down flow.
//
// Prerequisites to run (E2E needs the full stack live):
//   1. Backend API running against a reachable TradeNetDB:  dotnet run --project Backend
//   2. Frontend dev server running:                          npm run dev   (http://localhost:5173)
//   3. Browser binaries installed once:                      npx playwright install chromium
//   4. Run:                                                  npm run test:e2e
//
// Override the app URL with E2E_BASE_URL when the frontend runs elsewhere.
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: true,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});

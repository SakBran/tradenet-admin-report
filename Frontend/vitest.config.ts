import { defineConfig } from 'vitest/config';

// Minimal, self-contained Vitest config. Uses the Node environment (the current tests are
// pure config-integrity checks with no DOM) and does NOT load the app's Vite/React plugins,
// so the suite stays fast and dependency-light. Add environment: 'jsdom' + @testing-library
// here later if/when component tests are introduced.
export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.test.{ts,tsx}'],
    globals: false,
  },
});

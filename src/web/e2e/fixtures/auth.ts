import { test as base, type Page } from '@playwright/test';

/**
 * Auth fixture that abstracts authentication.
 * Currently uses the dev role switcher.
 * When real auth lands, swap the internals without changing test files.
 */

async function setRole(page: Page, role: 'golfer' | 'operator' | 'admin') {
  // The dev role switcher stores the role in localStorage.
  // Set it directly to avoid UI interaction overhead.
  await page.evaluate((r) => localStorage.setItem('shadowbrook-dev-role', r), role);
}

export const test = base.extend<{
  asGolfer: () => Promise<void>;
  asOperator: () => Promise<void>;
  asAdmin: () => Promise<void>;
}>({
  asGolfer: async ({ page }, use) => {
    await use(async () => {
      await setRole(page, 'golfer');
    });
  },
  asOperator: async ({ page }, use) => {
    await use(async () => {
      await setRole(page, 'operator');
    });
  },
  asAdmin: async ({ page }, use) => {
    await use(async () => {
      await setRole(page, 'admin');
    });
  },
});

export { expect } from '@playwright/test';

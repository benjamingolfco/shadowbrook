import { test as base, type Page } from '@playwright/test';

/**
 * Auth fixture that abstracts authentication.
 * Currently uses the dev role switcher.
 * When real auth lands, swap the internals without changing test files.
 */

async function setRole(page: Page, role: 'golfer' | 'operator' | 'admin') {
  // Must navigate to the app first so localStorage is accessible (not about:blank).
  await page.goto('/');
  await page.evaluate((r) => localStorage.setItem('shadowbrook-dev-role', r), role);
  await page.reload();
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

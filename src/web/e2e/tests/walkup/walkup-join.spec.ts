import { test, expect } from '@playwright/test';
import { WalkupPage } from '../../fixtures/walkup-page';
import { TEST_WALKUP_CODE, TEST_GOLFER } from '../../fixtures/test-data';

test.describe('Walkup Join Flow', () => {
  test('golfer can join the waitlist via walkup code', async ({ page }) => {
    const walkup = new WalkupPage(page);

    await walkup.goto();
    await walkup.enterCode(TEST_WALKUP_CODE);

    // After valid code, the join form should appear
    await expect(page.getByLabel('First Name')).toBeVisible();

    await walkup.fillJoinForm(TEST_GOLFER);

    // Should see confirmation with position
    await expect(walkup.getConfirmationHeading()).toBeVisible();
    await expect(walkup.getPositionText()).toBeVisible();
  });
});

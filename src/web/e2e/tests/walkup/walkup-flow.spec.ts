import { test, expect } from '../../fixtures/auth';
import { OperatorWaitlistPage } from '../../fixtures/operator-waitlist-page';
import { WalkupPage } from '../../fixtures/walkup-page';
import { TEST_COURSE_NAME, TEST_GOLFER } from '../../fixtures/test-data';

let walkupCode: string;

test.describe.serial('Walkup Waitlist Flow', () => {
  test('operator opens a walkup waitlist', async ({ page, asOperator }) => {
    await asOperator();
    const waitlist = new OperatorWaitlistPage(page);

    await waitlist.goto();
    await waitlist.selectCourse(TEST_COURSE_NAME);
    await waitlist.openWaitlist();

    walkupCode = await waitlist.getShortCode();
    expect(walkupCode).toBeTruthy();
    expect(walkupCode).toHaveLength(4);
  });

  test('golfer joins the waitlist via walkup code', async ({ page }) => {
    const walkup = new WalkupPage(page);

    await walkup.goto();
    await walkup.enterCode(walkupCode);

    // After valid code, the join form should appear
    await expect(page.getByLabel('First Name')).toBeVisible();

    await walkup.fillJoinForm(TEST_GOLFER);

    // Should see confirmation with position
    await expect(walkup.getConfirmationHeading()).toBeVisible();
    await expect(walkup.getPositionText()).toBeVisible();
  });
});

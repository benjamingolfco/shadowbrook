import { test, expect } from '../../fixtures/auth';
import { OperatorRegisterPage } from '../../fixtures/operator-register-page';
import { OperatorWaitlistPage } from '../../fixtures/operator-waitlist-page';
import { WalkupPage } from '../../fixtures/walkup-page';
import { TEST_TENANT_NAME, TEST_GOLFER, uniqueCourseName } from '../../fixtures/test-data';

const courseName = uniqueCourseName();
let walkupCode: string;

test.describe.serial('Walkup Waitlist Flow', () => {
  test('operator registers a new course', async ({ page, asOperator }) => {
    await asOperator();
    const register = new OperatorRegisterPage(page);

    await register.goto();
    await register.selectTenant(TEST_TENANT_NAME);
    await register.registerCourse(courseName);
  });

  test('operator opens a walkup waitlist', async ({ page, asOperator }) => {
    await asOperator();
    const waitlist = new OperatorWaitlistPage(page);

    await waitlist.goto();
    await waitlist.selectTenant(TEST_TENANT_NAME);
    await waitlist.selectCourse(courseName);
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

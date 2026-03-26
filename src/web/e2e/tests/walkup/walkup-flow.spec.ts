import { test, expect } from '../../fixtures/auth';
import { OperatorRegisterPage } from '../../fixtures/operator-register-page';
import { OperatorWaitlistPage } from '../../fixtures/operator-waitlist-page';
import { WalkupPage } from '../../fixtures/walkup-page';
import { WalkUpOfferPage } from '../../fixtures/walk-up-offer-page';
import { waitForSms, extractOfferUrl } from '../../fixtures/sms-helper';
import { TEST_TENANT_NAME, TEST_GOLFER, uniqueCourseName } from '../../fixtures/test-data';

const courseName = uniqueCourseName();
let walkupCode: string;
let offerPath: string;

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

  test('operator adds a tee time opening', async ({ page, asOperator }) => {
    await asOperator();
    const waitlist = new OperatorWaitlistPage(page);

    await waitlist.goto();
    await waitlist.selectTenant(TEST_TENANT_NAME);
    await waitlist.selectCourse(courseName);

    // Add an opening 2 hours from now to avoid past-time validation
    const futureTime = new Date(Date.now() + 2 * 60 * 60 * 1000);
    const timeStr = `${String(futureTime.getHours()).padStart(2, '0')}:${String(futureTime.getMinutes()).padStart(2, '0')}`;
    await waitlist.addTeeTimeOpening(timeStr, 1);

    // Verify it appears in the Tee Time Openings tab
    await waitlist.getOpeningsTab();
    await expect(page.getByRole('cell', { name: /Open/i })).toBeVisible();
  });

  test('golfer receives offer via SMS', async () => {
    // Poll the dev SMS API for the offer message
    const smsBody = await waitForSms(
      TEST_GOLFER.phone,
      (body) => body.includes('/book/walkup/'),
      { timeoutMs: 20_000 },
    );

    offerPath = extractOfferUrl(smsBody);
    expect(offerPath).toMatch(/^\/book\/walkup\/[\w-]+$/);
  });

  test('golfer accepts the tee time offer', async ({ page }) => {
    const offerPage = new WalkUpOfferPage(page);

    await offerPage.goto(offerPath);
    await offerPage.expectOfferDetails(courseName);
    await offerPage.claimTeeTime();
    await offerPage.expectConfirmation();
  });
});

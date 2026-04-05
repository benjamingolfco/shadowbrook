import { test, expect } from '../../fixtures/auth';
import { OperatorWaitlistPage } from '../../fixtures/operator-waitlist-page';
import { WalkupPage } from '../../fixtures/walkup-page';
import { WalkUpOfferPage } from '../../fixtures/walk-up-offer-page';
import { waitForSms, extractOfferUrl } from '../../fixtures/sms-helper';
import { TEST_GOLFER, TEST_OPERATOR_IDENTITY_ID } from '../../fixtures/test-data';
import { API_BASE_URL } from '../../playwright.config';

// Create a fresh course per run via API to avoid stale waitlist state
const courseName = `E2E Run ${Date.now()}`;
let walkupCode: string;
let offerPath: string;

test.describe.serial('Walkup Waitlist Flow', () => {
  test('setup: create a fresh course', async () => {
    const response = await fetch(`${API_BASE_URL}/courses`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${TEST_OPERATOR_IDENTITY_ID}`,
      },
      body: JSON.stringify({ name: courseName, timeZoneId: 'Etc/UTC' }),
    });

    expect(response.status).toBe(201);
  });

  test('operator opens a walkup waitlist', async ({ page, asOperator }) => {
    await asOperator();
    const waitlist = new OperatorWaitlistPage(page);

    await waitlist.goto();
    await waitlist.selectCourse(courseName);
    await waitlist.ensureFreshWaitlist();

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

    // Should see confirmation
    await expect(walkup.getConfirmationHeading()).toBeVisible();
  });

  test('operator adds a tee time opening', async ({ page, asOperator }) => {
    await asOperator();
    const waitlist = new OperatorWaitlistPage(page);

    await waitlist.goto();
    await waitlist.selectCourse(courseName);

    // Course uses UTC timezone. Compute tee time 10 minutes from now in UTC —
    // within the 30-minute walkup window and past the 5-minute grace period.
    const futureUtc = new Date(Date.now() + 10 * 60 * 1000);
    const timeStr = `${String(futureUtc.getUTCHours()).padStart(2, '0')}:${String(futureUtc.getUTCMinutes()).padStart(2, '0')}`;
    await waitlist.addTeeTimeOpening(timeStr, 1);

    // Verify the opening appears in the list
    await waitlist.verifyOpeningPosted();
  });

  test('golfer receives offer via SMS', async () => {
    // Poll the dev SMS API for the offer message
    const smsBody = await waitForSms(
      TEST_GOLFER.normalizedPhone,
      (body) => body.includes('/book/walkup/') && body.includes(courseName),
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

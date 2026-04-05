import { type Page, type Locator } from '@playwright/test';

export class OperatorWaitlistPage {
  private readonly openWaitlistButton: Locator;

  constructor(private readonly page: Page) {
    this.openWaitlistButton = page.getByRole('button', { name: 'Open Waitlist for Today' });
  }

  async goto() {
    await this.page.goto('/operator');
  }

  async selectCourse(courseName: string) {
    await this.page
      .getByRole('button', { name: new RegExp(`Manage ${courseName}`) })
      .click();
    await this.page.getByRole('heading', { name: 'Walk-Up Waitlist' }).waitFor();
  }

  /**
   * Ensure a fresh waitlist is open for today.
   * Handles three possible states:
   * - No waitlist → click "Open Waitlist for Today"
   * - Already open → close it, then reopen
   * - Closed → reopen it
   */
  async ensureFreshWaitlist() {
    const openButton = this.openWaitlistButton;
    const shortCode = this.page.getByTestId('short-code');
    const closedBadge = this.page.getByText('Closed', { exact: true });

    // Wait for the page to settle into one of three states
    const state = await Promise.race([
      openButton.waitFor().then(() => 'no-waitlist' as const),
      shortCode.waitFor().then(() => 'open' as const),
      closedBadge.waitFor().then(() => 'closed' as const),
    ]);

    if (state === 'open') {
      // Close the existing waitlist first
      await this.page.getByText('Close waitlist for today').click();
      const closeDialog = this.page.getByRole('alertdialog');
      await closeDialog.waitFor();
      await closeDialog.getByRole('button', { name: 'Close Waitlist' }).click();
      // Wait for closed state
      await closedBadge.waitFor();
    }

    if (state === 'open' || state === 'closed') {
      // Reopen from closed state
      await this.page.getByRole('button', { name: 'Reopen' }).click();
      const reopenDialog = this.page.getByRole('alertdialog');
      await reopenDialog.waitFor();
      await reopenDialog.getByRole('button', { name: 'Reopen Waitlist' }).click();
    } else {
      // No waitlist yet — open fresh
      await openButton.click();
    }

    // Wait for the short code to appear (confirms waitlist is open)
    await shortCode.waitFor();
  }

  async getShortCode(): Promise<string> {
    const codeElement = this.page.getByTestId('short-code');
    const spacedCode = await codeElement.textContent();
    return spacedCode?.replace(/\s/g, '') ?? '';
  }

  async verifyOpeningPosted() {
    // Scope to the desktop layout row — at 1280px the desktop layout is visible.
    const desktopRow = this.page.locator('[data-testid="opening-row-desktop"]');
    await desktopRow.getByText('Waiting for golfers...').waitFor();
  }

  async addTeeTimeOpening(time: string, slots: number) {
    // Inline PostTeeTimeForm — fill time input and select slots via radio group
    await this.page.getByLabel('Time').fill(time);

    if (slots !== 1) {
      await this.page.getByRole('radio', { name: String(slots) }).click();
    }

    await this.page.getByRole('button', { name: 'Post Tee Time' }).click();

    // Wait for success feedback
    await this.page.getByRole('button', { name: 'Posted!' }).waitFor();
  }
}

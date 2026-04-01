import { type Page, type Locator } from '@playwright/test';

export class OperatorWaitlistPage {
  private readonly openWaitlistButton: Locator;

  constructor(private readonly page: Page) {
    this.openWaitlistButton = page.getByRole('button', { name: 'Open Waitlist for Today' });
  }

  async goto() {
    await this.page.goto('/operator/waitlist');
  }

  async selectTenant(tenantName: string) {
    await this.page.getByRole('cell', { name: tenantName }).click();
  }

  async selectCourse(courseName: string) {
    await this.page
      .getByRole('button', { name: new RegExp(`Manage ${courseName}`) })
      .click();
    await this.page.getByRole('heading', { name: 'Walk-Up Waitlist' }).waitFor();
  }

  async openWaitlist() {
    await this.openWaitlistButton.click();

    // No confirmation dialog — button directly fires the mutation.
    // Wait for either the short code (success) or an error message (API failure).
    const shortCode = this.page.getByTestId('short-code');
    const error = this.page.getByRole('alert');

    const result = await Promise.race([
      shortCode.waitFor().then(() => 'success' as const),
      error.waitFor().then(() => 'error' as const),
    ]);

    if (result === 'error') {
      const msg = await error.textContent();
      throw new Error(`Open waitlist failed: ${msg}`);
    }
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

  /** Openings are now rendered inline — no tab to select. This is a no-op for backward compat. */
  async selectOpeningsTab() {
    // Openings list is always visible on the active waitlist page — no tab needed.
  }
}

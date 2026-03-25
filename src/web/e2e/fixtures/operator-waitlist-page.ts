import { type Page, type Locator } from '@playwright/test';

export class OperatorWaitlistPage {
  private readonly openWaitlistButton: Locator;

  constructor(private readonly page: Page) {
    this.openWaitlistButton = page.getByRole('button', { name: 'Open Waitlist' });
  }

  async goto() {
    await this.page.goto('/operator/waitlist');
  }

  async selectTenant(tenantName: string) {
    // Fresh browser context — need to select tenant first
    await this.page.getByRole('cell', { name: tenantName }).click();
  }

  async selectCourse(courseName: string) {
    // Course switcher is visible when multiple courses exist
    const switcher = this.page.getByRole('combobox', { name: 'Switch course' });
    if (await switcher.isVisible()) {
      await switcher.click();
      await this.page.getByRole('option', { name: courseName }).click();
    }
    await this.page.getByRole('heading', { name: 'Walk-Up Waitlist' }).waitFor();
  }

  async openWaitlist() {
    await this.openWaitlistButton.click();
    const dialog = this.page.getByRole('alertdialog');
    await dialog.getByRole('button', { name: 'Open Waitlist' }).click();
    // Wait for the short code to appear — confirms waitlist is open
    await this.page.locator('span.font-mono.font-bold.tracking-widest').waitFor();
  }

  async getShortCode(): Promise<string> {
    const codeElement = this.page.locator('span.font-mono.font-bold.tracking-widest');
    const spacedCode = await codeElement.textContent();
    // Code is displayed with spaces between digits (e.g., "4 8 2 7")
    return spacedCode?.replace(/\s/g, '') ?? '';
  }
}

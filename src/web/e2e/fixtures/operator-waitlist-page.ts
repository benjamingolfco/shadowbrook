import { type Page, type Locator } from '@playwright/test';

export class OperatorWaitlistPage {
  private readonly openWaitlistButton: Locator;

  constructor(private readonly page: Page) {
    this.openWaitlistButton = page.getByRole('button', { name: 'Open Waitlist' });
  }

  async goto() {
    await this.page.goto('/operator/waitlist');
  }

  async selectCourse(courseName: string) {
    // If course switcher is visible (multiple courses), select the course
    const switcher = this.page.getByRole('combobox', { name: 'Switch course' });
    if (await switcher.isVisible()) {
      await switcher.click();
      await this.page.getByRole('option', { name: courseName }).click();
    }
    // If single course, it's auto-selected — wait for the page to load
    await this.page.getByRole('heading', { name: 'Walk-Up Waitlist' }).waitFor();
  }

  async openWaitlist() {
    await this.openWaitlistButton.click();
    // Confirm in the dialog
    const dialog = this.page.getByRole('alertdialog');
    await dialog.getByRole('button', { name: 'Open Waitlist' }).click();
    // Wait for the Open badge to appear
    await this.page.getByText('Open').waitFor();
  }

  async getShortCode(): Promise<string> {
    const codeElement = this.page.locator('span.font-mono.font-bold.tracking-widest');
    const spacedCode = await codeElement.textContent();
    // Code is displayed with spaces between digits (e.g., "4 8 2 7")
    return spacedCode?.replace(/\s/g, '') ?? '';
  }
}

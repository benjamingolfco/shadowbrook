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
    const dialog = this.page.getByRole('alertdialog');
    await dialog.getByRole('button', { name: 'Open Waitlist' }).click();
    await this.page.locator('span.font-mono.font-bold.tracking-widest').waitFor();
  }

  async getShortCode(): Promise<string> {
    const codeElement = this.page.locator('span.font-mono.font-bold.tracking-widest');
    const spacedCode = await codeElement.textContent();
    return spacedCode?.replace(/\s/g, '') ?? '';
  }

  async addTeeTimeOpening(time: string, slots: number) {
    await this.page.getByRole('button', { name: 'Add Tee Time Opening' }).click();

    const dialog = this.page.getByRole('dialog');
    await dialog.getByLabel('Tee Time').fill(time);

    if (slots !== 1) {
      await dialog.getByRole('combobox').click();
      await this.page.getByRole('option', { name: String(slots) }).click();
    }

    await dialog.getByRole('button', { name: 'Add Opening' }).click();
    await dialog.waitFor({ state: 'hidden' });
  }

  async getOpeningsTab() {
    await this.page.getByRole('tab', { name: 'Tee Time Openings' }).click();
  }
}

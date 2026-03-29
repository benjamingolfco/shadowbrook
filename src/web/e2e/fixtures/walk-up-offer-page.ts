import { type Page, expect } from '@playwright/test';

export class WalkUpOfferPage {
  constructor(private readonly page: Page) {}

  async goto(offerPath: string) {
    await this.page.goto(offerPath);
  }

  async expectOfferDetails(courseName: string) {
    await expect(this.page.getByText(courseName)).toBeVisible();
    await expect(this.page.getByRole('button', { name: 'Claim This Tee Time' })).toBeVisible();
  }

  async claimTeeTime() {
    await this.page.getByRole('button', { name: 'Claim This Tee Time' }).click();

    const dialog = this.page.getByRole('alertdialog');
    await dialog.getByRole('button', { name: 'Confirm' }).click();
  }

  async expectConfirmation() {
    await expect(this.page.getByText('Tee Time Claimed')).toBeVisible();
  }
}

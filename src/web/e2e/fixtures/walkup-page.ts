import { type Page, type Locator } from '@playwright/test';

export class WalkupPage {
  private readonly codeInput: Locator;
  private readonly firstNameInput: Locator;
  private readonly lastNameInput: Locator;
  private readonly phoneInput: Locator;
  private readonly joinButton: Locator;

  constructor(private readonly page: Page) {
    this.codeInput = page.getByLabel('4-digit course code');
    this.firstNameInput = page.getByLabel('First Name');
    this.lastNameInput = page.getByLabel('Last Name');
    this.phoneInput = page.getByLabel('Phone Number');
    this.joinButton = page.getByRole('button', { name: 'Join Waitlist' });
  }

  async goto() {
    await this.page.goto('/join');
  }

  async enterCode(code: string) {
    await this.codeInput.fill(code);
  }

  async fillJoinForm(data: { firstName: string; lastName: string; phone: string }) {
    await this.firstNameInput.fill(data.firstName);
    await this.lastNameInput.fill(data.lastName);
    await this.phoneInput.fill(data.phone);
    await this.joinButton.click();
  }

  getConfirmationHeading() {
    return this.page.getByRole('heading', { name: /You're on the list/ });
  }

  getPositionText() {
    return this.page.getByText(/#\d+ in line at/);
  }
}

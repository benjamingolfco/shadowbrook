import { type Page } from '@playwright/test';

export class OperatorRegisterPage {
  constructor(private readonly page: Page) {}

  async goto() {
    await this.page.goto('/operator/register-course');
  }

  async selectTenant(tenantName: string) {
    // OrganizationSelect shows a table of tenants — click the row
    await this.page.getByRole('cell', { name: tenantName }).click();
  }

  async registerCourse(courseName: string) {
    await this.page.getByLabel('Course Name *').fill(courseName);
    // Timezone is auto-filled from browser — leave it
    await this.page.getByRole('button', { name: 'Register Course' }).click();
    // After registration, the app navigates to /operator/settings.
    // Wait for the success message first, then the navigation.
    await this.page.getByText('Course registered successfully').waitFor();
    await this.page.waitForURL(/\/operator\/settings/);
  }
}

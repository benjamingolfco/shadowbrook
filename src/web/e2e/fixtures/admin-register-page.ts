import { type Page } from '@playwright/test';

export class AdminRegisterPage {
  constructor(private readonly page: Page) {}

  async goto() {
    await this.page.goto('/admin/courses/new');
  }

  async registerCourse(courseName: string, tenantName: string) {
    // Select tenant from shadcn Select dropdown
    await this.page.getByRole('combobox').click();
    await this.page.getByRole('option', { name: tenantName }).click();

    // Fill course name
    await this.page.getByLabel('Course Name *').fill(courseName);

    // Timezone is auto-filled from browser — leave it
    await this.page.getByRole('button', { name: 'Register Course' }).click();

    // After registration, the app navigates to /admin/courses (CourseList).
    // Wait for the list heading to confirm navigation completed.
    await this.page.getByRole('heading', { name: 'All Registered Courses' }).waitFor();
  }
}

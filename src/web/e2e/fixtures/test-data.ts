/**
 * Known test data seeded in the test environment.
 * Must match E2ESeedData.cs in the backend.
 */
export const TEST_TENANT_NAME = 'E2E Test Golf Group';
export const TEST_GOLFER = {
  firstName: 'E2E',
  lastName: 'Tester',
  phone: '5551230000',
};

/**
 * Generate a unique course name per test run to avoid stale state.
 */
export function uniqueCourseName(): string {
  return `E2E Run ${Date.now()}`;
}

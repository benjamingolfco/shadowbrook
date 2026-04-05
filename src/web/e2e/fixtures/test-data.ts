/**
 * Known test data seeded in the test environment.
 * Must match E2ESeedData.cs in the backend.
 */
export const TEST_TENANT_NAME = 'E2E Test Golf Group';

/** Dedicated e2e course seeded with UTC timezone to avoid timezone mismatches. */
export const TEST_E2E_COURSE = 'E2E Walkup Course';

/** Dedicated e2e golfer — unique phone number to avoid SMS collisions. */
export const TEST_GOLFER = {
  firstName: 'E2E',
  lastName: 'Tester',
  phone: '5559990001',
  normalizedPhone: '+15559990001',
};

/** Dev auth emails matching AppUsers seeded in E2ESeedData.cs */
export const TEST_ADMIN_EMAIL = 'e2e-admin@benjamingolfco.onmicrosoft.com';
export const TEST_OPERATOR_EMAIL = 'e2e-operator@benjamingolfco.onmicrosoft.com';

import { test as base, type Page } from '@playwright/test';
import { API_BASE_URL } from '../playwright.config';

/**
 * Auth fixture that provides role switching for e2e tests.
 *
 * Uses page.route() to intercept GET /auth/me and return the real user payload
 * for the requested role. Also swaps the Authorization Bearer token on all
 * API requests so the backend resolves the correct AppUser (with correct
 * OrganizationId and permissions).
 *
 * Requires:
 * - Frontend running with VITE_USE_DEV_AUTH=true (--mode e2e)
 * - Backend running with Auth:UseDevAuth=true
 * - E2E seed data with AppUsers for e2e-admin@benjamingolfco.onmicrosoft.com and e2e-operator@shadowbrook.golf
 */

interface MePayload {
  id: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  displayName: string;
  role: string;
  organization: { id: string; name: string } | null;
  organizations: { id: string; name: string }[] | null;
  courses: { id: string; name: string }[];
  permissions: string[];
}

/**
 * Fetch the real /auth/me response for a given identity and cache it.
 * This ensures mock payloads have real IDs from the seeded database.
 */
const meCache = new Map<string, MePayload>();

async function fetchRealMe(identityId: string): Promise<MePayload> {
  const cached = meCache.get(identityId);
  if (cached) return cached;

  const response = await fetch(`${API_BASE_URL}/auth/me`, {
    headers: { Authorization: `Bearer ${identityId}` },
  });

  if (!response.ok) {
    throw new Error(
      `Failed to fetch /auth/me for identity "${identityId}": ${response.status} ${response.statusText}`,
    );
  }

  const data = (await response.json()) as MePayload;
  meCache.set(identityId, data);
  return data;
}

async function setRole(page: Page, role: 'admin' | 'operator', identityId: string) {
  // Fetch the real user data from the API (seeded in E2ESeedData)
  const mePayload = await fetchRealMe(identityId);

  // Remove any previous route handlers (from prior role switches in serial tests)
  await page.unrouteAll({ behavior: 'ignoreErrors' });

  // Intercept /auth/me to return the correct user
  await page.route('**/auth/me', (route) => {
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mePayload),
    });
  });

  // Intercept all API requests to swap the Bearer token for this role's identity.
  // The API client sends `Authorization: Bearer <VITE_DEV_IDENTITY_ID>` by default,
  // but we need the correct identity per role so the backend resolves the right
  // AppUser (with correct OrganizationId and permissions).
  await page.route('**/*', (route, request) => {
    const headers = request.headers();
    if (
      (request.resourceType() === 'fetch' || request.resourceType() === 'xhr') &&
      headers['authorization']
    ) {
      headers['authorization'] = `Bearer ${identityId}`;
      return route.continue({ headers });
    }
    return route.continue();
  });

  // Navigate to the app — DevAuthProvider will call /auth/me and get our mock
  await page.goto('/');

  // Wait for the app to finish loading and redirect based on role
  if (role === 'admin') {
    await page.waitForURL('**/admin*');
  } else {
    await page.waitForURL('**/operator*');
  }
}

export const test = base.extend<{
  asGolfer: () => Promise<void>;
  asOperator: () => Promise<void>;
  asAdmin: () => Promise<void>;
}>({
  asGolfer: async ({ page }, use) => {
    await use(async () => {
      // Golfer routes are public — no auth needed
      await page.goto('/join');
    });
  },
  asOperator: async ({ page }, use) => {
    await use(async () => {
      await setRole(page, 'operator', 'e2e-operator@shadowbrook.golf');
    });
  },
  asAdmin: async ({ page }, use) => {
    await use(async () => {
      await setRole(page, 'admin', 'e2e-admin@benjamingolfco.onmicrosoft.com');
    });
  },
});

export { expect } from '@playwright/test';

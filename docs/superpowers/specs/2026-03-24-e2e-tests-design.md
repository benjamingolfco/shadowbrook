# E2E Tests Design

## Problem

Shadowbrook has unit tests (domain, validators, handlers) and integration tests (API + Testcontainers), but no browser-level end-to-end tests that verify the full deployed stack works from a user's perspective. After deploying to the test environment, there is no automated verification that critical user flows actually work.

## Goals

- Catch regressions in deployed environments before they reach production
- Gate releases on critical user paths working end to end
- Start small (walkup waitlist flow) and expand coverage incrementally

## Non-Goals

- Running e2e tests on every PR in CI (unit + integration tests handle that)
- Replacing the agent-driven QA skill (`/qa`) — that serves a different purpose (AC validation per story)
- Multi-browser coverage initially

## Architecture

### Framework

Playwright (TypeScript), installed as a dev dependency in `src/web/`. Playwright is the industry standard for browser e2e testing with excellent TypeScript support, built-in retries, tracing, and parallel execution.

### Project Structure

```
src/web/
  e2e/
    fixtures/          # Custom test fixtures (auth, page objects)
      auth.ts          # Auth abstraction (dev switcher now, real auth later)
      operator-waitlist-page.ts  # Page object for operator waitlist
      walkup-page.ts   # Page object for golfer walkup flow
      test-data.ts     # Known test data constants (seeded tenant/course names)
    tests/
      walkup/          # Walkup waitlist flow tests
        walkup-flow.spec.ts      # Serial: operator opens → golfer joins
    playwright.config.ts
```

Tests follow the same feature-based folder structure as the application source.

### Configuration

`e2e/playwright.config.ts`:

- **Base URL:** `E2E_BASE_URL` env var, defaulting to the test environment URL
- **Browser:** Chromium only (add Firefox/WebKit later if needed)
- **Retries:** 1 on CI, 0 locally
- **Tracing:** On first retry — captures screenshots, DOM snapshots, and network logs for debugging without overhead on every run
- **No `webServer` block** — tests always target an already-deployed environment

### Auth Abstraction

A custom Playwright fixture handles authentication by abstracting the current dev role switcher behind a stable interface:

```typescript
asGolfer()     // Sets up golfer browser context
asOperator()   // Sets up operator browser context
asAdmin()      // Sets up admin browser context
```

When real auth is implemented, only the fixture internals change (e.g., API-based login, cookie injection). No test files need modification.

### Test Data Strategy

**Seeded baseline data:** An idempotent startup seed runs in non-production environments (`Program.cs`, after migration). It ensures a test tenant with 3 courses exists. The seed checks by name before creating, so it's safe to run on every startup.

- 1 tenant: "E2E Test Golf Group" with contact details
- 3 courses: each with a name, timezone, and basic settings

**Transient test data (walkup sessions, entries):** Created by the e2e tests themselves through the UI. Tests run in serial order — earlier tests produce the data later tests consume. No walkup codes or golfer records are pre-seeded.

This means the e2e suite exercises both the operator path (create walkup session) and the golfer path (join via code), and each test validates a real user flow while producing data for the next.

Constants for the seeded tenant/course names live in `e2e/fixtures/test-data.ts` so tests can navigate to the right course.

## Test Scope — Walkup Waitlist Flow

Tests use `test.describe.serial()` to run in order. Each test validates a user flow and produces data the next test needs.

### Test 1: Operator opens a walkup waitlist
1. Log in as operator, navigate to `/operator/waitlist`
2. Click "Open Waitlist", confirm in dialog
3. Capture the short code displayed on the page
4. Verify the waitlist shows as "Open"

### Test 2: Golfer joins via that code
1. Navigate to `/join`
2. Enter the code captured from Test 1
3. Fill out the join form
4. See confirmation with position

If Test 1 fails, Test 2 is skipped — which is correct (if operators can't create waitlists, there's nothing for golfers to join).

### Testing Philosophy

E2E tests verify that the deployed system works as a whole. They are not the place to test individual error states, validation rules, or edge cases. Those belong at cheaper, faster layers:

- **Form validation** (missing fields, invalid input) — React component tests via Vitest + Testing Library
- **Invalid/expired codes, already-joined golfer** — Backend unit tests (domain/handler) and integration tests (API + Testcontainers)

This keeps the e2e suite fast, stable, and focused on deployment confidence rather than duplicating coverage that already exists closer to the code.

### Page Objects

Lightweight page objects keep tests readable:

```typescript
class OperatorWaitlistPage {
  goto()
  openWaitlist()
  getShortCode()
}

class WalkupPage {
  goto()
  enterCode(code: string)
  fillJoinForm({ firstName, lastName, phone })
  getConfirmationHeading()
  getPositionText()
}
```

Not a full page object framework — just enough to avoid repeating selectors.

## Running the Tests

### Scripts

| Command | What it does |
|---------|-------------|
| `pnpm --dir src/web e2e` | Run all e2e tests headless |
| `pnpm --dir src/web e2e:ui` | Open Playwright interactive UI mode |
| `make e2e` | Makefile shorthand for headless run |

### Local Development

```bash
# Against test environment (default)
make e2e

# Against local dev server
E2E_BASE_URL=http://localhost:3000 make e2e
```

### CI Integration

A GitHub Actions workflow triggered by deployment to the test environment via `workflow_run`. It:

1. Waits for the deploy workflow to complete
2. Installs Playwright browsers
3. Runs the e2e suite against the test environment URL
4. Uploads test report and trace artifacts on failure

This runs post-deploy to the test environment, not on every PR.

## Future Expansion

- Add flows as features are built (tee time booking, operator tee sheet, etc.)
- Add Firefox/WebKit browsers if cross-browser issues emerge
- Swap auth fixture internals when real auth is implemented

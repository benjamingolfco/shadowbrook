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
      walkup-page.ts   # Page object for walkup flow
      test-data.ts     # Known test data constants
    tests/
      walkup/          # Walkup waitlist flow tests
        walkup-join.spec.ts
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

Seeded test data in the test environment:

- A known test course with a fixed slug/ID
- Pre-created walkup codes with long/no expiry
- Known test golfer records as needed

Constants live in `e2e/fixtures/test-data.ts` and reference this seeded data. The seed runs as part of the test environment's deployment/migration process (EF Core data seed or SQL script).

This approach is simple and fast (no per-run API setup). If test isolation becomes an issue as the suite grows, individual tests can be migrated to create their own data via API calls in `beforeAll`.

## Test Scope — Walkup Waitlist Flow

### E2E: Happy Path Only

One test: enter a valid walkup code, fill out the join form, see confirmation. This verifies the full deployed stack works end to end — browser, frontend, API, database.

### Testing Philosophy

E2E tests verify that the deployed system works as a whole. They are not the place to test individual error states, validation rules, or edge cases. Those belong at cheaper, faster layers:

- **Form validation** (missing fields, invalid input) — React component tests via Vitest + Testing Library
- **Invalid/expired codes, already-joined golfer** — Backend unit tests (domain/handler) and integration tests (API + Testcontainers)

This keeps the e2e suite fast, stable, and focused on deployment confidence rather than duplicating coverage that already exists closer to the code.

### Page Object

A lightweight page object keeps tests readable:

```typescript
class WalkupPage {
  enterCode(code: string)
  fillJoinForm({ name, phone, ... })
  getConfirmation()
  getErrorMessage()
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
4. Uploads trace artifacts on failure
5. Posts results as a check/comment

This runs post-deploy to the test environment, not on every PR.

## Future Expansion

- Add flows as features are built (tee time booking, operator tee sheet, etc.)
- Add Firefox/WebKit browsers if cross-browser issues emerge
- Migrate to API-created test data if shared state becomes problematic
- Swap auth fixture internals when real auth is implemented

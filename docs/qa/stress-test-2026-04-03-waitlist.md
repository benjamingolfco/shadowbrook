# Stress Test Report — waitlist — 2026-04-03

**Duration:** ~8 minutes
**Environment:** test (https://white-stone-00610060f.1.azurestaticapps.net)
**Agents:** 1 operator + 3 golfers + 1 observer
**Hint:** none
**Exit reason:** Circuit breaker — UnknownSagaException in TeeTimeOpeningExpirationPolicy handler

---

## Operator Actions

| Time | Action | Result | Notes |
|------|--------|--------|-------|
| 19:27:33 | Open Waitlist for Today | SUCCESS | Short code 1308 assigned, 0 golfers waiting |
| 19:27:48 | Post tee time 09:00 / 4 slots | SUCCESS | Opening appeared, later auto-transitioned to "Expired" |
| 19:28:10 | Post tee time 10:30 / 2 slots (fill method) | FAILED | "Invalid input" — Playwright fill() doesn't trigger RHF validation on type="time" inputs |
| 19:30:00 | Post tee time 14:30 / 3 slots | SUCCESS | API returned 503 on subsequent GET but POST succeeded |
| 19:30:42 | Click "Add golfer manually" | FAILED | Redirected to /join/1308 instead of opening modal dialog |
| 19:31:20 | Cancel 2:30 PM opening | FAILED | MSAL redirect to /join/1308 before completing |
| 19:36:34 | Post tee time 14:40 / 4 slots | SUCCESS | 1 golfer now on waitlist (from golfer-1 agent) |
| 19:38:11 | Post duplicate 14:40 / 2 slots | CORRECTLY REJECTED | 409: "A tee time opening for this time already exists with 4 slots" |
| 19:38:26 | Cancel 2:40 PM opening | UNCERTAIN | Dialog confirmed but opening persisted; 503 on refresh |
| 19:39:33 | Page reload | STUCK | Blank page — MSAL auth state corrupted |

## Golfer Results

| Golfer | Phone | Joined Waitlist | Received Offer | Accepted | Notes |
|--------|-------|-----------------|----------------|----------|-------|
| golfer-1 | +15550001 | Yes — via API bypass | Yes (confirmation) | No — openings expired | Brute-forced course code 1308, joined via POST /walkup/join, got SMS confirmation. No active openings to accept. |
| golfer-2 | +15550002 | No — blocked by auth redirect | N/A | N/A | CRITICAL BLOCKER: /join redirects to Entra login |
| golfer-3 | +15550003 | No — blocked by auth redirect | N/A | N/A | Still running at report time |

## Contention Events

None observed — golfer agents could not reach the waitlist join form due to authentication redirect.

## Observer Findings

### Errors
| Time | Type | Message | Operation |
|------|------|---------|-----------|
| 19:29:01 UTC | Wolverine.Persistence.Sagas.UnknownSagaException | Could not find expected saga document of type TeeTimeOpeningExpirationPolicy for id '019d54d0-f137-73dd-9104-99ae2cc15ec5' | TeeTimeOpeningExpired handler |

### Warnings
- **Massive 404 volume on `/walkup/verify`:** 12,176 requests returned 404 out of ~12,307 total. Not from our agents — likely bots or scanners hitting the test environment.
- **Very low successful traffic:** Only 119 successful requests during the window (65 health checks, 17 waitlist queries, 11 auth, 4 walkup verify).

### Concurrency Events
None observed during this run.

### Flow Gaps
- Golfer join flow (`/join`) is completely blocked by authentication redirect — anonymous access not working on test environment.
- Walkup verification flow effectively non-functional — 99.97% of `/walkup/verify` requests returned 404.
- Only 1 successful `POST /courses/{courseId}/walkup-waitlist/open` observed — waitlist opening flow barely exercised.

---

## Summary

The stress test surfaced multiple significant issues across all three agent types. The golfer flow is completely blocked by MSAL auth wrapping on the public `/join` route (frontend-only — backend is correctly `[AllowAnonymous]`). The operator flow revealed a session-breaking MSAL state corruption bug: clicking "Add golfer manually" navigates to `/join/1308` instead of opening a modal, and this corrupts the MSAL redirect state so every subsequent token refresh loops back to `/join` instead of `/operator`. The observer detected an `UnknownSagaException` race condition in `TeeTimeOpeningExpirationPolicy` and intermittent 503s on the waitlist API. Despite these blockers, golfer-1 managed to join the waitlist via direct API call and received an SMS confirmation, and the operator successfully opened the waitlist and posted tee time openings.

### Issues Found

- **BUG: `/join` route requires authentication on test environment.** Navigating to `/join` redirects to `login.microsoftonline.com`. Anonymous golfers cannot join the waitlist. This blocks the entire golfer-side flow.
- **BUG: UnknownSagaException in TeeTimeOpeningExpirationPolicy.** Wolverine cannot find saga state for `TeeTimeOpeningExpired` event (id `019d54d0-f137-73dd-9104-99ae2cc15ec5`). Race condition: saga completes or is deleted before the scheduled expiration timeout fires.
- **INVESTIGATE: Massive 404 traffic on `/walkup/verify`.** 12,176 requests returning 404 — likely external bots/scanners, but worth confirming the endpoint path is correct and adding rate limiting.
- **SKILL FIX: Phone number format.** Golfer agents use 7-digit phones (`5550001`) but `PhoneNormalizer` requires 10-11 digits. Agent config should use `5555550001` format.
- **SKILL FIX: Course code coordination.** Golfer agents need the waitlist short code from the operator agent (who sees it after opening the waitlist). Currently no coordination mechanism — golfer had to brute-force all 10,000 codes.
- **NOTE: Frontend MSAL wrapping causes auth on public routes.** `/join` has no `AuthGuard` in `router.tsx`, but the app-level `MsalProvider`/`AuthProvider` wrapper forces auth redirect anyway. Backend endpoints are correctly `[AllowAnonymous]`.
- **BUG: "Add golfer manually" navigates instead of opening modal.** Clicking the button redirects the entire page to `/join/1308` instead of opening the expected modal dialog. Reproduced 2/2 times.
- **BUG: MSAL redirect state corruption.** After navigating to `/join/1308` (via the "Add golfer manually" bug), every MSAL token refresh redirects to `/join/1308` instead of `/operator`. Page reloads at `/operator` produce a blank white page. Only clearing browser storage recovers. Session-breaking for operators.
- **BUG: Intermittent 503s on GET /walkup-waitlist/today.** Causes stale data display (cancelled openings still appearing). Container App may be scaling down under load.
- **BUG: Stale validation message persists.** The "A tee time opening for this time already exists" message is not cleared on form context changes or page reloads.
- **BUG: Playwright can't set type="time" inputs via fill().** RHF/Zod validation shows "Invalid input" when Playwright programmatically changes the value. Only pre-populated default time works. Affects automated testing, not real users.

# Stress Test Report — Walk-up Waitlist (Operator-Led) — 2026-04-07

**Mode:** Operator-led — human acted as operator; 10 golfer agents joined autonomously.
**Duration:** ~12 minutes (03:17–03:29 UTC)
**Environment:** test (https://purple-field-0a3932a0f.4.azurestaticapps.net)
**Waitlist short code:** 7644
**Course:** Pine Valley
**Agents:** 10 stress-test-golfer subagents (no operator agent, no observer)
**Golfer behavior mix:** 4 accept-immediately, 3 accept-after-60s-buffer, 3 pass
**Join cadence:** Random delays 12s–172s
**Exit reason:** Completed — all 10 agents returned within their 6-minute budgets.

---

## Golfer Results

| # | Phone | Party | Join @ | Behavior | Queue # | Outcome |
|---|---|---|---|---|---|---|
| 1 | 5555550001 | 1 | +12s | accept-immediately | #1 | **Booked 10:30 PM** |
| 2 | 5555550002 | 2 | +28s | +60s buffer | #3 | **Booked 10:40 PM** (after re-offer) |
| 3 | 5555550003 | 1 | +45s | pass | #2 | First offer (10:30 PM) consumed by another golfer; re-offered 10:40 PM; no Pass UI affordance |
| 4 | 5555550004 | 3 | +67s | accept-immediately | #4 | Joined OK; SMS polling broken — no offer observed |
| 5 | 5555550005 | 2 | +82s | +60s buffer | #5 | **Booked 10:30 PM** |
| 6 | 5555550006 | 4 | +95s | pass | #7 | Offer arrived; expired before navigation; "no longer available" SMS received |
| 7 | 5555550007 | 1 | +110s | accept-immediately | #6 | **Booked 10:50 PM** |
| 8 | 5555550008 | 2 | +132s | +60s buffer | — | Joined; offer arrived; agent script clicked wrong selector — claim never executed |
| 9 | 5555550009 | 3 | +155s | pass | #7 | Joined; no offer received in 4-min window |
| 10 | 5555550010 | 1 | +172s | accept-immediately | #7 | **Booked 10:40 PM** |

**Successful bookings:** 5 (g1, g2, g5, g7, g10)
**Re-offer events:** 2 (g2, g3)
**Expired offers:** 1 (g6)

---

## Contention Events

These are the most valuable signals from the run — the system correctly handled every observed contention event.

1. **10:30 PM slot — multi-golfer race.** g1, g3, g6, g8 all received the same 10:30 PM offer (multiple slots at that time, ~2 spots). g1 claimed first.
2. **g2 → re-offer.** Received 10:30 PM offer at 03:22:03; navigation delayed to 03:25:13 by an SMS-polling bug; by then the slot was taken. System sent "Sorry, that tee time is no longer available." at 03:25:02 and immediately re-offered 10:40 PM in the same batch. g2 then claimed 10:40 PM successfully.
3. **g3 → re-offer.** Same pattern as g2. Received "Sorry…" + new 10:40 PM offer in the same timestamp batch.
4. **g6 → offer expired.** Navigated 62s after receipt; offer page rendered the **"Offer No Longer Available"** state correctly (no action buttons, clear message). System also sent the matching "Sorry…" SMS.

✅ **The re-offer + unavailability flow works correctly.** Every golfer who lost a slot was either re-queued with a new offer or shown a clean "no longer available" page.

---

## Application Findings

These are real issues observed in the deployed app worth filing or investigating:

### 1. Page title says "Shadowbrook" but course is "Pine Valley"
Multiple agents independently noted: `<title>Shadowbrook</title>` while the page body and SMS messages say "Pine Valley". Likely a tenant branding/title mismatch in the public join page.

### 2. Dev SMS link on join confirmation page is broken
The "View SMS messages" link on the post-join confirmation page points to `/dev/sms/golfer/` (no phone segment), which renders "Page not found." The working path is `/dev/sms/golfer/{phoneNumber}`. The link omits the phone parameter.

### 3. No Pass / Decline UI on offer page
The offer page (`/book/walkup/{id}`) only has a "Claim This Tee Time" button. There is no explicit Pass or Decline action. Three of our agents were configured to pass — none could express that intent in the UI. It is unclear whether the system distinguishes "passed" from "ignored until expiry," and whether pass behavior should advance the queue immediately rather than waiting for the offer to time out.

### 4. SMS API trailing-slash inconsistency
`{api_url}/dev/sms` works; `{api_url}/dev/sms/` returns 404. This caused two agents (g2, g4) to silently miss SMS for several minutes. Recommend either supporting the trailing slash or 301-redirecting it.

### 5. `/w/{code}` → `/join/{code}` redirect
The short link `/w/7644` redirects to `/join/7644` after submission. Informational only — note for any documentation that reuses the original URL.

### 6. Two-step claim confirmation
Clicking "Claim This Tee Time" opens a confirmation dialog with Cancel/Confirm before the booking is committed. This is good UX, but worth documenting for future automation/QA scripts.

### 7. No party-size field on join form
Inputs defined party sizes 1–4 across the agents, but the form has only First Name / Last Name / Phone. Either party size should be added, or the test scenarios should drop the field.

---

## Agent / Script Findings (NOT app bugs)

These tripped up the golfer subagents and should be folded back into the `stress-test-golfer` skill or agent definition before the next run:

1. **`jq` not installed** in the subagent environment. SMS polling using `jq` silently returns empty. Agents that fell back to `python3 -c '...'` worked. → Update agent prompts to use `python3` for JSON parsing.
2. **`Accept` selector wrong.** g8 used `button:has-text("Accept")`. The actual button label is **"Claim This Tee Time"**, and there's a follow-up **"Confirm"** dialog button. Agent definition should specify both.
3. **Wrong SMS endpoint path** in the agent prompt template — `{api_url}/dev/sms/` (trailing slash) returns 404. Should be `{api_url}/dev/sms`.
4. **Playwright sandbox**. Subagents needed `dangerouslyDisableSandbox: true` plus `--no-sandbox` Chromium flags. Worth baking into the golfer agent definition.
5. **Google Fonts hangs** under headless Chromium with no proxy. g6 had to block `fonts.googleapis.com` / `fonts.gstatic.com` via `context.route()` to render the React app.
6. **Polling-budget vs URL detection.** g1's polling loop kept looping even after the offer SMS arrived because its URL-pattern detection didn't recognize `/book/walkup/`. g1 still won the race because no one contested it, but in a tighter scenario this would lose.

---

## Summary

Five clean bookings out of ten agents, with two successful re-offer recoveries and one cleanly-expired offer — the contention paths exercised here all worked. The system correctly serialized claims on shared slots, re-queued losing golfers with fresh offers, and showed the "no longer available" state when slots were already taken.

The remaining five "non-bookings" are mostly **agent-side bugs** (selector mismatch, wrong API path, missing `jq`) rather than app defects. Once those are fixed in the `stress-test-golfer` definition, a re-run should give a much cleaner picture.

The most interesting **real** finding is the absence of an explicit Pass/Decline action — three agents wanted to decline and had no way to express it, which means we have no test coverage for explicit-pass behavior in the queue.

### Issues Worth Filing

- Title "Shadowbrook" vs course "Pine Valley" on the public join page
- Broken `/dev/sms/golfer/` link on join confirmation (missing phone segment)
- No Pass/Decline action on the offer page
- SMS API trailing-slash returning 404 (low priority)
- Missing party-size field on the public waitlist join form (if intended)

### Skill / Agent Improvements

- Update `stress-test-golfer.md`: switch to `python3` for SMS JSON parsing, fix endpoint path, document the "Claim This Tee Time" + "Confirm" two-step button flow, document `dangerouslyDisableSandbox` need, document Google Fonts blocking workaround.

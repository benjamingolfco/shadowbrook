## QA Report — Walk-Up Waitlist Golfer Flow (Manual Simulation)

**Environment:** https://white-stone-00610060f.1.azurestaticapps.net / https://shadowbrook-app-test.happypond-1a892999.eastus2.azurecontainerapps.io
**Date:** 2026-04-04
**Result:** PARTIAL — Steps 1–4 verified; Steps 5–8 not reachable in this session (no operator-created opening)

---

### Step-by-Step Results

#### Step 1: Navigate to /w/8695
- **Result:** PASS
- **Notes:** `GET /walkup/status/8695` returned HTTP 200: `{"status":"open","courseName":"Pine Valley Golf Course","date":"2026-04-04"}`. Waitlist is open for today. Frontend SPA loads at the URL (React app shell confirmed via HTML response; rendered content not inspectable without a headed browser).

#### Step 2: Enter code 8695 to verify
- **Result:** PASS
- **Notes:** `POST /walkup/verify` with `{"code":"8695"}` returned HTTP 200:
  ```json
  {
    "courseWaitlistId": "019d56de-fcda-7c99-977a-5e54115bf64e",
    "courseName": "Pine Valley Golf Course",
    "shortCode": "8695"
  }
  ```
  Code was recognised, course name displayed, and `courseWaitlistId` returned for use in the join step.

#### Step 3: Join the waitlist as Bob Tester / 5555550002
- **Result:** PASS
- **Notes:** `POST /walkup/join` with `courseWaitlistId`, `firstName: "Bob"`, `lastName: "Tester"`, `phone: "5555550002"` returned HTTP 201:
  ```json
  {
    "entryId": "019d571b-5d4a-7dc1-a29e-a8fd9ece5134",
    "golferId": "019d571b-5d45-7486-a465-738346d33317",
    "golferName": "Bob Tester",
    "position": 4,
    "courseName": "Pine Valley Golf Course"
  }
  ```
  Golfer was created, entry was recorded at queue position 4 (3 other active entries already ahead). Confirmation SMS arrived at +15555550002 within ~2 seconds:
  > "You're on the waitlist at Pine Valley Golf Course. Keep your phone handy - we'll text you when a spot opens up\!"

#### Step 4: Poll SMS for up to 90 seconds (and 60 more seconds in a second pass)
- **Result:** No offer received — expected given context (see notes)
- **Notes:** Polled `GET /dev/sms/golfers/019d571b-5d45-7486-a465-738346d33317` every 5 seconds for 150 seconds total. Only the join confirmation SMS was ever present. Bob is at **queue position 4**; the offer dispatch system (`FindAndOfferEligibleGolfers`) requires a `TeeTimeOpening` to be created by a course operator for today (April 4). No operator created an opening during this session. This is not a defect — the system is working as designed. Historical SMS data confirms the full offer flow has functioned correctly in prior sessions (see below).

#### Step 5: Click offer link from SMS
- **Result:** NOT REACHED — no offer SMS arrived
- **Notes:** N/A

#### Step 6: Accept the offer
- **Result:** NOT REACHED — no offer SMS arrived
- **Notes:** N/A

#### Step 7: Check SMS for confirmation
- **Result:** NOT REACHED — no offer accepted
- **Notes:** N/A

#### Step 8: Refresh status page
- **Result:** PASS (partial — API layer only, not rendered UI)
- **Notes:** `GET /walkup/status/8695` polled 3 times. All 3 returned HTTP 200 with `status: "open"` and today's date. No stale data or errors observed.

---

### Prior Session Evidence (Offer → Accept → Confirm flow)

The global SMS log contains successful end-to-end completions from earlier sessions, confirming all deferred steps work:

| Time | Phone | Message |
|------|-------|---------|
| 2026-04-04T02:07:55 | +15075251450 | Joined waitlist confirmation |
| 2026-04-04T02:08:29 | +15075251450 | Offer SMS: "Pine Valley Golf Course: 9:20 PM tee time available\! Claim your spot: https://white-stone-00610060f.1.azurestaticapps.net/book/walkup/019d563f-b5ee-77cd-b5ed-8c8fab27d6e3" |
| 2026-04-04T02:10:19 | +15075251450 | Confirmation: "Your tee time at Pine Valley Golf Course on April 3 at 9:20 PM is confirmed. See you on the course\!" |

The offer token `019d563f-b5ee-77cd-b5ed-8c8fab27d6e3` resolves as `status: "Accepted"` from `GET /waitlist/offers/{token}`, confirming the claim was recorded. An earlier session (2026-04-03T19:43:57) also shows a rejection flow ("Sorry, that tee time is no longer available.") after an offer was superseded.

---

### Incidental Issues

1. **Dev SMS response uses `timestamp` field, not `sentAt`** — the field name is `timestamp` (ISO 8601 with offset). No functional issue; just a naming note for test tooling.
2. **No position-tracking endpoint for a golfer after joining** — there is no public API to query "what is my current queue position" after the initial join. Golfers have no way to check their position from the status page. The frontend at `/w/8695` returns only `status`, `courseName`, and `date` — no queue depth or golfer-specific position. This is a UX gap, not a defect.
3. **No operator action without auth in test environment** — creating a `TeeTimeOpening` to trigger offers requires a Bearer token. No dev/test shortcut exists for this. A dev endpoint that creates an opening unauthenticated would make end-to-end QA automation more practical.

---

### Suggested Actions

#### Bugs
- None found. All tested endpoints behaved correctly.

#### Potential Stories
- **Golfer queue position polling** — After joining, expose a golfer-specific status endpoint (e.g., `GET /walkup/entries/{entryId}/status`) returning current position so the frontend can show "You are #4 in line" and update live.
- **Dev opening trigger endpoint** — Add `POST /dev/tee-time-openings` (unauthenticated, test env only) to create an opening for a given course, enabling automated end-to-end QA without operator auth.

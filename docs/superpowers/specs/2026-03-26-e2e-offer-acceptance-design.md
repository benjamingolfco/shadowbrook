# E2E Offer Acceptance + Domain Naming Alignment

**Date:** 2026-03-26
**Branch:** `chore/e2e-seed-data`

## Goals

1. Rename UI elements and API endpoints to match the `TeeTimeOpening` domain aggregate
2. Extend the e2e test suite with offer acceptance flow (operator adds opening, golfer receives SMS, golfer accepts offer)

## Section 1: Naming Renames

### Frontend

| Current | Proposed |
|---|---|
| `AddTeeTimeRequestDialog.tsx` | `AddTeeTimeOpeningDialog.tsx` |
| `AddTeeTimeRequestDialog.test.tsx` | `AddTeeTimeOpeningDialog.test.tsx` |
| Dialog title "Add Tee Time" | "Add Tee Time Opening" |
| Hook posts to `/courses/{id}/walkup-waitlist/openings` | Posts to `/courses/{id}/tee-time-openings` |

The "Tee Time Openings" tab label on the operator waitlist page already matches the domain — no change needed.

### Backend

| Current | Proposed |
|---|---|
| `POST /courses/{courseId}/walkup-waitlist/openings` | `POST /courses/{courseId}/tee-time-openings` |
| `CreateOpeningRequest` | `CreateTeeTimeOpeningRequest` |

The endpoint moves from being nested under the walkup waitlist to a top-level course resource. This reflects that `TeeTimeOpening` is its own aggregate, not a child of the waitlist. The `operatorOwned` flag is set server-side based on context (API = operator-owned).

## Section 2: E2E Test — Offer Acceptance Flow

### New tests

Three new serial tests appended to `walkup-flow.spec.ts`, continuing the existing flow (register course, open waitlist, golfer joins):

**Test 4: "operator adds a tee time opening"**
- As operator, navigate to the waitlist page (already selected tenant/course from earlier tests)
- Click "Add Tee Time Opening" button
- Fill in tee time and slots in the dialog
- Verify the opening appears in the "Tee Time Openings" tab

**Test 5: "golfer receives offer via SMS"**
- Poll `GET /dev/sms/conversations/{golferPhone}` via direct API call (not browser navigation)
- Wait for an SMS body containing `/book/walkup/`
- Extract the full offer URL from the message body
- Store the URL for the next test

**Test 6: "golfer accepts the tee time offer"**
- Navigate to the extracted `/book/walkup/{token}` URL
- Verify the offer card displays course name, tee time, and available spots
- Click "Claim This Tee Time" button
- Confirm in the alert dialog
- Verify "Request Received" confirmation heading appears

### New/modified page objects

- **`OperatorWaitlistPage`** — add `addTeeTimeOpening(time: string, slots: number)` method
- **`WalkUpOfferPage`** (new fixture) — encapsulates the offer card interactions: verify details, click claim, confirm dialog, verify confirmation
- **SMS polling helper** — utility function that polls the dev SMS API with timeout/retry, returns matching message body

### SMS polling approach

The test environment uses `DatabaseTextMessageService` which persists SMS to the `DevSmsMessages` table. The `GET /dev/sms/conversations/{phone}` endpoint is available in non-production environments. The e2e test polls this endpoint (e.g., every 500ms, up to 15s timeout) waiting for a message containing the offer URL. The offer token is extracted via regex from the SMS body.

### API base URL

The dev SMS endpoint is on the backend API, not the frontend. The playwright config will expose a new `E2E_API_URL` env var (defaulting to the test environment API URL), mirroring the existing `E2E_BASE_URL` pattern. The SMS polling helper and any direct API calls in tests will use this value.

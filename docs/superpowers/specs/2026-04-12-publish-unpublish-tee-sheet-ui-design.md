# Publish/Unpublish Tee Sheet UI

Frontend UI for operators to publish and unpublish tee sheets. The backend endpoints, domain model, domain events, and downstream handlers (booking cancellation, SMS notifications) already exist. This spec covers only the frontend.

## Context

Issue [#399](https://github.com/benjamingolfco/teeforce/issues/399). Backend merged; frontend publish/unpublish controls are missing.

Operator workflow research shows:
- Publishing is a day-by-day decision made from the weekly schedule view (not the detail page)
- Operators scan the week, then publish individual days as they're ready — some days may be held back for tournament confirmation or weather uncertainty
- Unpublishing is rare (~2-3x/month) but high-stakes; operators need to see the blast radius (booking count) before committing

## Schedule List Page — Per-Card Publish Button

Each day card in **Draft** status gets a "Publish" button on the card. Clicking it:
1. Calls `POST /courses/{courseId}/tee-sheets/{date}/publish`
2. Invalidates `['tee-sheets', courseId]` queries
3. Badge updates from "Draft" to "Published"

The button shows a loading state during the mutation. No confirmation dialog — publishing is low-risk and idempotent.

The existing bulk draft flow (checkboxes on "Not Started" cards) is unchanged. Published and draft cards remain clickable links to the detail page.

## ScheduleDay Detail Page — Status Badge + Unpublish

### Status indicator
The topbar displays a status badge (Draft or Published) next to the formatted date.

### Unpublish flow
A "Unpublish" button appears in the topbar when the sheet is Published. Clicking it:

1. Fetches `GET /courses/{courseId}/tee-sheets/{date}/booking-count`
2. **If count is 0:** Calls unpublish immediately, no dialog. Sheet returns to Draft.
3. **If count > 0:** Opens `UnpublishTeeSheetDialog`:
   - Title: "Unpublish Tee Sheet?"
   - Description: "{count} booking(s) will be cancelled and affected golfers will be notified by SMS."
   - Optional textarea: "Reason (included in cancellation SMS)"
   - Footer: Cancel button, destructive "Unpublish" button
4. On confirm, calls `POST /courses/{courseId}/tee-sheets/{date}/unpublish` with `{ reason }`
5. Invalidates `['tee-sheets', courseId]` queries
6. Page updates to show Draft status

### Backend endpoints (existing)
- `POST /courses/{courseId}/tee-sheets/{date}/publish` — returns `{ teeSheetId, status }`
- `POST /courses/{courseId}/tee-sheets/{date}/unpublish` — accepts `{ reason?: string }`, returns `{ teeSheetId, status }`
- `GET /courses/{courseId}/tee-sheets/{date}/booking-count` — returns `{ count }`

## ScheduleDay Detail Page — Fix Hardcoded Player Count

Replace the hardcoded "4 players" string on line 68 of `ScheduleDay.tsx` with `{slot.playerCount} players` from the API response.

## New Frontend Files

| File | Type | Purpose |
|------|------|---------|
| `features/course/manage/hooks/usePublishTeeSheet.ts` | Mutation hook | Calls publish endpoint, invalidates tee sheet queries |
| `features/course/manage/hooks/useUnpublishTeeSheet.ts` | Mutation hook | Calls unpublish endpoint with optional reason, invalidates tee sheet queries |
| `features/course/manage/hooks/useBookingCount.ts` | Query hook | Fetches booking count for a date (called on demand, not on page load) |
| `features/course/manage/components/UnpublishTeeSheetDialog.tsx` | Component | AlertDialog with booking count warning and optional reason textarea |

## Query Invalidation

Both publish and unpublish mutations invalidate `['tee-sheets', courseId]` broadly. This refreshes the weekly status on the Schedule list page and the tee sheet data on the ScheduleDay detail page.

## Patterns to Follow

- Mutation hooks: follow `useBulkDraft` pattern (useMutation + onSuccess invalidation)
- Dialog: follow `CancelOpeningDialog` pattern (AlertDialog with destructive action styling, isPending disabled state)
- Reason textarea: follow `AddGolferDialog` pattern (Dialog with form fields, reset on close)
- Status badges: reuse `statusConfig` mapping already in Schedule.tsx

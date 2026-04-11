# Publish and Unpublish a Tee Sheet

**Issue:** #399
**Date:** 2026-04-11

Course operators control when tee times become visible to golfers by publishing and unpublishing tee sheets. Publishing is already implemented. This spec covers unpublishing and the frontend controls for both actions.

## Acceptance Criteria

### Publishing (existing)
- Given a tee sheet is in Draft status, when I publish it, then its status changes to Published
- Given a tee sheet is Published, when a golfer views available times for that date, then the times from that sheet appear

### Unpublishing
- Given a Published tee sheet has no bookings, when I unpublish it, then it returns to Draft status
- Given a Published tee sheet has existing bookings, when I unpublish it, then I see a confirmation dialog warning me that all bookings will be cancelled
- Given I confirm the unpublish with an optional reason, then all bookings are cancelled and each affected golfer receives a notification with the cancellation reason (if provided)
- Given I cancel the confirmation dialog, then nothing changes and the sheet remains Published

## Domain Changes

### TeeSheet aggregate

Add `Unpublish(string? reason, ITimeProvider timeProvider)`:
- Guard: throws `TeeSheetNotPublishedException` if status is not Published
- Sets `Status = Draft`, clears `PublishedAt`
- Raises `TeeSheetUnpublished` event

New event `TeeSheetUnpublished`:
- `TeeSheetId`, `CourseId`, `Date`, `Reason?`, `UnpublishedAt`

### Booking aggregate

Update `Cancel()` to accept an optional reason: `Cancel(string? reason = null)`.
- `BookingCancelled` event gains a `Reason?` field
- Existing callers continue to work (default null)

## API Changes

### New endpoint: Unpublish

`POST /courses/{courseId}/tee-sheets/{date}/unpublish`

Request body:
```json
{ "reason": "string | null" }
```

Response:
```json
{ "teeSheetId": "guid", "status": "Draft" }
```

Loads tee sheet by course + date, calls `sheet.Unpublish(reason, timeProvider)`.

### New query: Active booking count

The frontend needs to know whether bookings exist before showing the confirmation dialog. This can be a dedicated endpoint or a field on the existing tee sheet response. A simple count query:

`GET /courses/{courseId}/tee-sheets/{date}/booking-count`

Response:
```json
{ "count": 3 }
```

Returns count of confirmed bookings for tee times on that sheet.

## Event Handlers

### TeeSheetUnpublished → CancelBookingsHandler

**Location:** `Features/Bookings/Handlers/TeeSheetUnpublished/`

- Loads all tee times for the sheet via `ITeeTimeRepository.GetByTeeSheetIdAsync()`
- Collects booking IDs from all claims across all tee times
- Loads each booking, calls `booking.Cancel(evt.Reason)`
- The existing `BookingCancelled` cascade handles the rest:
  - `ReleaseTeeTimeClaimHandler` releases tee time claims
  - `NotificationHandler` sends cancellation notification to each golfer

### TeeSheetUnpublished → DeleteTeeTimesHandler

**Location:** `Features/TeeSheet/Handlers/TeeSheetUnpublished/`

- Loads all tee times for the sheet via `ITeeTimeRepository.GetByTeeSheetIdAsync()`
- Deletes all tee time records (claims cascade-delete via DB FK)

**Ordering:** The delete handler must run after the cancel handler finishes (cancel needs to read claims before they're cascade-deleted). Both react to `TeeSheetUnpublished`. Since Wolverine runs separated handlers independently in the same transaction, and the cancel handler only reads claims to find booking IDs (it doesn't delete them — claim release happens via `BookingCancelled` cascade), both handlers can safely read tee time data. The delete handler removes tee times and their claims at the end of the transaction.

## Notification Changes

### BookingCancellationSmsFormatter

Update the existing formatter to include the reason when present:

- Without reason: "Your tee time at {CourseName} on {Date} at {Time} has been cancelled."
- With reason: "Your tee time at {CourseName} on {Date} at {Time} has been cancelled. Reason: {reason}"

No new notification type needed.

## Frontend Changes

### Tee sheet status in API response

The `GET /tee-sheets` response needs to include the tee sheet status so the frontend knows whether to show Publish or Unpublish. Currently it returns slots but no status field. Add `status: "Draft" | "Published" | null` to `TeeSheetResponse`.

### Publish/Unpublish button

Add a button to the tee sheet topbar area:
- When status is `Draft` → show "Publish" button
- When status is `Published` → show "Unpublish" button
- When status is `null` (no sheet exists) → no button (sheet must be drafted first)

### Unpublish confirmation dialog

When the operator clicks Unpublish:
1. Fetch booking count for the date
2. If zero → call unpublish directly, no dialog
3. If bookings exist → show confirmation dialog:
   - Warning: "{count} booking(s) will be cancelled and golfers will be notified"
   - Optional reason textarea
   - Confirm / Cancel buttons
4. On confirm → `POST .../unpublish` with reason
5. On cancel → close dialog, no action

### Query invalidation

After publish or unpublish, invalidate the tee sheet query to refresh the grid and status.

## Domain Exceptions

### Existing
- `TeeSheetNotPublishedException` — reused for unpublish guard (sheet must be Published to unpublish)

### Global exception handler
No new exceptions to register — `TeeSheetNotPublishedException` is already mapped.

## Out of Scope

- Unpublish does not handle waitlist entries or pending offers for the date — those are separate concerns that can be addressed later if needed
- No audit log of who unpublished and when (beyond the standard `UpdatedBy` audit column)
- No bulk unpublish (single date at a time)

# Weekly Tee Sheet Setup & Route Restructure

**Issue:** #398 — Weekly tee sheet view with day-level management
**Date:** 2026-04-10

## Overview

Build the weekly tee sheet setup page where operators draft and manage their tee sheets, and restructure frontend routing from `/operator` to `/course/:courseId/...` with separate management and POS modes.

## Route Structure

```
/course                               → Redirect to operator's assigned course
/course/:courseId/                     → Redirect to /course/:courseId/manage/
/course/:courseId/manage/              → Dashboard (management home)
/course/:courseId/manage/schedule      → Weekly tee sheet setup
/course/:courseId/manage/schedule/:date → Day detail (interval preview)
/course/:courseId/manage/settings      → Course settings (relocated)
/course/:courseId/pos/tee-sheet        → Day-of tee sheet grid (relocated)
/course/:courseId/pos/waitlist         → Walk-up waitlist (relocated)
```

Old `/operator` routes are removed entirely (no production users).

## Layouts

Both layouts use a sidebar. They differ in nav items and top bar actions.

### ManagementLayout

- **Sidebar:** Dashboard, Schedule, Settings
- **Top bar:** Course name + "Open POS" button

### PosLayout

- **Sidebar:** Tee Sheet, Waitlist
- **Top bar:** Course name, date navigation, gear icon linking back to management

### CourseId Resolution

The `:courseId` route param replaces the current auth-context approach. `useCourseId()` is updated to read from `useParams()`. All existing components that use this hook continue to work unchanged.

When an operator hits `/course` with no courseId, auto-redirect to `/course/:courseId/manage/` using their assigned course from auth context. Multi-course selection is deferred.

## Frontend Folder Structure

```
features/
  course/
    hooks/
      useCourseId.ts              — reads :courseId from route params
    manage/
      layouts/
        ManagementLayout.tsx
      pages/
        Dashboard.tsx             — new
        Schedule.tsx              — new (weekly view)
        ScheduleDay.tsx           — new (day detail)
        Settings.tsx              — relocated from operator/
      hooks/
        useWeeklySchedule.ts      — new
      components/
    pos/
      layouts/
        PosLayout.tsx
      pages/
        TeeSheet.tsx              — relocated from operator/
        WalkUpWaitlist.tsx         — relocated from operator/
      components/                 — relocated tee sheet grid, waitlist components
      hooks/                      — relocated hooks
```

## Backend — New Endpoints

### Weekly Status

`GET /courses/{courseId}/tee-sheets/week?startDate=2026-04-13`

Returns the status of each day in a 7-day window starting from `startDate`. Days with no `TeeSheet` record are returned as `notStarted` (implicit status — no DB row).

The frontend is responsible for computing the Monday of the target week and passing it as `startDate`. The backend returns 7 days starting from `startDate` without computing week boundaries.

**Response:**
```json
{
  "weekStart": "2026-04-13",
  "weekEnd": "2026-04-19",
  "days": [
    { "date": "2026-04-13", "status": "notStarted" },
    { "date": "2026-04-14", "status": "draft", "teeSheetId": "...", "intervalCount": 72 },
    { "date": "2026-04-15", "status": "published", "teeSheetId": "...", "intervalCount": 72 }
  ]
}
```

This is a read model query — no domain logic. Queries the `DbContext` directly for `TeeSheet` records matching the course and 7-date range, maps their status, and fills in `notStarted` for missing dates. Does not use `ITeeSheetRepository` — this is a read concern, not a write-side aggregate load.

**Validation:** `startDate` is required, must be a valid date.

### Bulk Draft

`POST /courses/{courseId}/tee-sheets/draft`

Replaces the existing single-date draft endpoint. The request body changes from `{ date }` to `{ dates }` (array). The single-date case is just an array of one.

**Request:**
```json
{ "dates": ["2026-04-13", "2026-04-16", "2026-04-17"] }
```

**Response:**
```json
{
  "teeSheets": [
    { "date": "2026-04-13", "teeSheetId": "..." },
    { "date": "2026-04-16", "teeSheetId": "..." },
    { "date": "2026-04-17", "teeSheetId": "..." }
  ]
}
```

Calls `TeeSheet.Draft()` for each date within a single transaction. Fails entirely if any date already has a sheet — the error response must include the conflicting date (e.g., "A tee sheet already exists for April 14"). `TeeSheetAlreadyExistsException` should carry the date. Requires schedule defaults to be configured (`CourseScheduleNotConfiguredException`).

**Validation:** `dates` is required, non-empty, each element a valid date format, all dates must be today or in the future (no drafting past dates).

### Existing Endpoints (Unchanged)

- `GET /tee-sheets?courseId=...&date=...` — Used by day detail view to show intervals
- `POST /courses/{courseId}/tee-sheets/{date}/publish` — Exists but not wired in UI (#399)
- `GET /courses/{courseId}/tee-time-settings` — Used by dashboard to check if defaults configured
- `PUT /courses/{courseId}/tee-time-settings` — Unchanged

## Weekly Schedule Page

**Route:** `/course/:courseId/manage/schedule`

### Week Navigation

- Previous/next week arrows
- "This Week" button to jump to current week
- Weeks are Monday–Sunday

### Day Cards

A row of 7 day cards, one per day of the week. Each card shows:

- Day name + date (e.g., "Monday, Apr 13")
- Status badge: Not Started (gray), Draft (yellow), Published (green)
- Interval count when drafted/published (e.g., "72 intervals")
- Checkbox for selecting Not Started days (only shown on Not Started cards)

### Actions

- **"Draft Selected" button** — Enabled when one or more Not Started days are checked. Calls the bulk draft endpoint. Refreshes the week view on success.
- **Click a Draft or Published day** — Navigates to `/course/:courseId/manage/schedule/:date`

### Not Configured State

If schedule defaults are not configured, the page shows a message directing the operator to configure their schedule settings first, with a link to the settings page.

## Day Detail Page

**Route:** `/course/:courseId/manage/schedule/:date`

Read-only preview of generated intervals for a drafted or published day.

- Header showing date and status badge (Draft / Published)
- Back link to the weekly view
- List of intervals: time + capacity (e.g., "7:00 AM — 4 players, 7:10 AM — 4 players")
- No editing — blocking (#400) and publishing (#399) are separate stories

Reuses the existing `useTeeSheet(courseId, date)` hook and `GET /tee-sheets` endpoint.

## Dashboard Page

**Route:** `/course/:courseId/manage/`

Simple landing page with status cards:

- **Today's Tee Sheet** — Status (Not Started / Draft / Published), link to schedule. "Open POS" shortcut.
- **This Week** — Count of days by status (e.g., "3 published, 2 draft, 2 not started"). Link to schedule.
- **Schedule Defaults** — "Configured" or "Not configured" with link to settings.

Data from existing endpoints: `useWeeklySchedule` for the current week, `GET /courses/{courseId}/tee-time-settings` for defaults status.

## Testing

### Backend

- **Endpoint unit tests:** Validators for bulk draft request (dates required, non-empty, valid format) and weekly status query (startDate required, valid format).
- **Integration tests:** Bulk draft creates multiple sheets, fails if any date has existing sheet, fails if defaults not configured. Weekly status returns correct mix of statuses for a week.

### Frontend

- **Component tests:** Weekly schedule grid renders day cards with correct statuses, checkbox selection toggles, draft button enable/disable based on selection, not-configured state.
- **Hook tests:** `useWeeklySchedule` — correct query key, maps response.

## Known Behaviors

- **Schedule defaults changed after drafting:** Each draft snapshots the current schedule defaults at draft time. If an operator drafts Monday with 10-minute intervals, changes defaults to 8 minutes, then drafts Tuesday, the two days will have different intervals. This is expected — each draft captures a point-in-time configuration. Re-drafting (deleting and recreating a draft) is not in scope for this issue.

## Out of Scope

- Publishing/unpublishing tee sheets — #399
- Blocking tee times — #400 (design notes added to that issue for Google Calendar-style quick-add interaction)
- Rate schedules — #401
- Editing individual interval capacity or times
- Multi-course selection (auto-redirect for now)
- Re-drafting a day (deleting a draft and recreating with updated defaults)

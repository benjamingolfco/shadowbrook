# Implementation Plan for #246 -- Prevent Past Tee Time Requests

## Approach

Add past-tee-time validation at two layers: (1) backend endpoint validation in `CreateWaitlistRequest` before calling `TeeTimeRequestService.CreateAsync`, returning 422 with ProblemDetails when the tee time has passed, and (2) frontend Zod schema refinement in `AddTeeTimeRequestDialog` that compares the entered time against the current course-local time. A 5-minute grace period applies at both layers so that times that just barely passed are not rejected. The backend is the source of truth; the frontend provides immediate feedback.

## Files

### Backend

- **Modify:** `src/backend/Shadowbrook.Api/Infrastructure/Services/CourseTime.cs` -- Add a `Now` method that returns the current `TimeOnly` in the course's timezone, paralleling the existing `Today` method. This keeps timezone arithmetic centralized.

- **Modify:** `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/WalkUpWaitlistEndpoints.cs` -- In `CreateWaitlistRequest`, after parsing the date/teeTime and resolving the timeZoneId, add a check: if the parsed date is today (in course time) and the parsed tee time is before `CourseTime.Now(timeProvider, timeZoneId) - 5 minutes`, return a 422 `Results.Problem` with title "Tee time is in the past" and detail message "Tee time must be in the future." If the date is in the past (before today in course time), also return 422. If the date is in the future, skip the check entirely.

- **Modify:** `tests/Shadowbrook.Api.Tests/WalkUpWaitlistEndpointsTests.cs` -- Add two integration tests: (1) `CreateRequest_PastTeeTime_Returns422` that submits a tee time several hours in the past and asserts 422, (2) `CreateRequest_FutureTeeTime_Returns201` that submits a tee time well in the future (e.g., 23:59) and asserts 201 (this already exists implicitly in the `CreateRequest_ValidRequest_Returns201` test which uses 10:00, but may need adjustment depending on when CI runs).

- **Modify:** `tests/Shadowbrook.Api.Tests/WaitlistOfferEndpointsTests.cs` -- In the `CreateTeeTimeRequestAsync` helper, change the tee time from a hardcoded `"10:00"` to a time guaranteed to be in the future (e.g., `"23:50"` or compute a time 2 hours from now in the test course's timezone). Un-skip the tests whose `Skip` reason references the `UnknownSagaException` caused by past-time issues. Tests whose skip reason is "background handler timing not yet reliable" should remain skipped -- only un-skip if the root cause was specifically the past-time validation issue.

- **Modify:** `tests/Shadowbrook.Api.Tests/CourseTimeTests.cs` -- Add unit tests for the new `CourseTime.Now` method using `FakeTimeProvider`.

### Frontend

- **Modify:** `src/web/src/lib/course-time.ts` -- Add a `getCourseNow` function that returns the current time as `HH:mm` string in the given IANA timezone (using `Intl.DateTimeFormat` or `Date.toLocaleTimeString`).

- **Modify:** `src/web/src/features/operator/components/AddTeeTimeRequestDialog.tsx` -- Enhance the Zod schema with a `.refine()` that compares `teeTime` against `getCourseNow(timeZoneId)` minus 5 minutes. The refinement needs `timeZoneId` from `useCourseContext`, so the schema must be constructed inside the component (or use `superRefine` with context). Show the error message "Tee time must be in the future" on the teeTime field.

- **Create:** `src/web/src/features/operator/__tests__/AddTeeTimeRequestDialog.test.tsx` -- Test that submitting a past tee time shows the validation error message, and that a future tee time does not.

## Patterns

- **Endpoint-level business validation** (not FluentValidation): The past-time check requires async service resolution (`ICourseTimeZoneProvider`) and route parameters (`courseId`). FluentValidation validators in this codebase are constructor-only with no DI. Follow the pattern already used in the endpoint for other business checks (e.g., waitlist-not-open check in `TeeTimeRequestService`). Return `Results.Problem(...)` with `statusCode: 422` to distinguish from FluentValidation 400s (format errors) vs. business rule violations.

- **`CourseTime` utility**: Centralizes all timezone conversion. Adding `Now` here follows the existing `Today` and `ToUtc` pattern. Reference: `src/backend/Shadowbrook.Api/Infrastructure/Services/CourseTime.cs`.

- **Frontend Zod refinement**: Schema refinements that depend on runtime state (timezone) are constructed inside the component. This matches the existing pattern where `getCourseToday` is called inside the component. Reference: current `AddTeeTimeRequestDialog.tsx` line 49.

## API Design

No new endpoints. The existing `POST /courses/{courseId}/walkup-waitlist/requests` gains a new 422 response:

```
HTTP 422
Content-Type: application/problem+json

{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.23",
  "title": "Tee time is in the past",
  "status": 422,
  "detail": "Tee time must be in the future."
}
```

## Risks

1. **Test timing sensitivity**: Integration tests that submit tee times like `"10:00"` will fail if CI runs after 10:00 AM in the test course's timezone (America/Chicago). The fix: use `"23:50"` in test helpers, or compute a future time dynamically. The `WalkUpWaitlistEndpointsTests.CreateRequest_ValidRequest_Returns201` test uses `"10:00"` -- this needs to be updated to a reliably future time.

2. **Grace period boundary**: The 5-minute grace period means a tee time at 2:30 PM is valid until 2:35 PM course-local time. This is intentional per the acceptance criteria but worth documenting in the error message or a code comment.

3. **WaitlistOffer test un-skipping**: The skipped tests reference "background handler timing not yet reliable." The issue story says to un-skip them because the past-time fix resolves the `UnknownSagaException`. However, if the skip reason is purely about async handler timing (not past-time), un-skipping may cause flaky tests. Approach: update the `CreateTeeTimeRequestAsync` helper to use future times, then un-skip one representative test to verify stability before un-skipping all.

## Testing Strategy

### Unit Tests (cheap, fast)

- `CourseTime.Now` with `FakeTimeProvider` -- verify it returns correct local time for different timezones, DST boundaries
- Frontend: `AddTeeTimeRequestDialog` renders validation error for past tee times, allows future tee times

### Integration Tests (DB-dependent)

- `CreateRequest_PastTeeTime_Returns422` -- POST with a tee time in the past, assert 422 + ProblemDetails body
- `CreateRequest_PastDate_Returns422` -- POST with yesterday's date, assert 422
- `CreateRequest_FutureTeeTime_Returns201` -- POST with a tee time well in the future, assert 201 (update existing test to use a reliably future time)
- Verify existing `CreateRequest_ValidRequest_Returns201` still passes with adjusted tee time
- WaitlistOffer saga tests -- un-skip after updating helper to use future times

## Dev Tasks

### Backend Developer

- [ ] Add `CourseTime.Now(TimeProvider, string timeZoneId)` method that returns `TimeOnly` representing current local time at the course
- [ ] Add unit tests for `CourseTime.Now` in `CourseTimeTests.cs` using `FakeTimeProvider` (at least: basic conversion, DST boundary, different timezone)
- [ ] Add past-tee-time validation in `CreateWaitlistRequest` endpoint: if date is today and tee time < now - 5 minutes, or if date < today, return 422 ProblemDetails with title "Tee time is in the past" and detail "Tee time must be in the future."
- [ ] Inject `TimeProvider` into the `CreateWaitlistRequest` endpoint method signature (it is not currently injected there)
- [ ] Add integration test `CreateRequest_PastTeeTime_Returns422` in `WalkUpWaitlistEndpointsTests`
- [ ] Add integration test `CreateRequest_PastDate_Returns422` in `WalkUpWaitlistEndpointsTests`
- [ ] Update `CreateRequest_ValidRequest_Returns201` test to use a reliably future tee time (e.g., `"23:50"`) instead of `"10:00"`
- [ ] Update all other tests in `WalkUpWaitlistEndpointsTests` that use hardcoded tee times to use future times
- [ ] Update `CreateTeeTimeRequestAsync` helper in `WaitlistOfferEndpointsTests` to use a reliably future tee time
- [ ] Un-skip WaitlistOffer integration tests -- remove `Skip` attribute from tests where the past-time fix resolves the underlying issue. If a test still fails due to async handler timing, re-skip with an updated reason.
- [ ] Run full test suite to verify no regressions: `dotnet test`

### Frontend Developer

- [ ] Add `getCourseNow(timeZoneId: string): string` function to `src/web/src/lib/course-time.ts` that returns current time as `HH:mm` in the given timezone
- [ ] Update `addTeeTimeRequestSchema` in `AddTeeTimeRequestDialog.tsx` to be constructed dynamically inside the component, using a Zod `.refine()` on `teeTime` that rejects times more than 5 minutes in the past relative to `getCourseNow(course.timeZoneId)`
- [ ] Display inline error message "Tee time must be in the future" via the existing `<FormMessage />` component on the teeTime field
- [ ] Add component tests in `src/web/src/features/operator/__tests__/AddTeeTimeRequestDialog.test.tsx`:
  - Past tee time shows validation error
  - Future tee time does not show validation error
  - Error clears when user changes to a valid time
- [ ] Run frontend lint and tests: `pnpm --dir src/web lint && pnpm --dir src/web test`

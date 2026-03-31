## Implementation Plan for #322 -- teeTime field type mismatch breaks operator tee time creation

### Approach

The API expects `CreateTeeTimeOpeningRequest.TeeTime` as a `DateTime` (ISO 8601), but both frontend forms (`PostTeeTimeForm` and `AddTeeTimeOpeningDialog`) send `teeTime` as an `HH:mm` string from the HTML `<input type="time">` element. The fix belongs entirely in the frontend: convert the `HH:mm` time string to a full ISO 8601 DateTime before sending it to the API. The API contract (`DateTime TeeTime`) is correct and well-tested -- the backend already extracts `DateOnly` and `TimeOnly` from the DateTime and creates the domain `TeeTime` value object. No backend changes needed.

The conversion strategy: combine `getCourseToday(timeZoneId)` (which returns `yyyy-MM-dd`) with the `HH:mm` time string to produce `yyyy-MM-ddTHH:mm:ss`. This gives the API the date it needs (today in the course's timezone) and the time the operator selected.

### Files

- **Modify:** `src/web/src/types/waitlist.ts` -- Update `CreateTeeTimeOpeningRequest.teeTime` type comment to clarify it expects ISO 8601 DateTime (the type is already `string`, which is correct for JSON serialization)
- **Modify:** `src/web/src/features/operator/components/PostTeeTimeForm.tsx` -- Convert `HH:mm` to ISO 8601 DateTime before passing to mutation
- **Modify:** `src/web/src/features/operator/components/AddTeeTimeOpeningDialog.tsx` -- Convert `HH:mm` to ISO 8601 DateTime before passing to mutation
- **Modify:** `src/web/src/features/operator/__tests__/PostTeeTimeForm.test.tsx` -- Update test assertion to expect ISO 8601 DateTime
- **Modify:** `src/web/src/features/operator/__tests__/AddTeeTimeOpeningDialog.test.tsx` -- Add test verifying ISO 8601 DateTime is sent to mutation

### Patterns

Both forms use the same `useCreateTeeTimeOpening` hook from `useWaitlist.ts`. The hook accepts `CreateTeeTimeOpeningRequest` which has `teeTime: string`. The conversion from `HH:mm` to ISO DateTime should happen in the `onSubmit` handler of each form, right before calling `createMutation.mutate()`. This keeps the form's internal state as `HH:mm` (matching the HTML time input) and only converts at the boundary.

A shared helper function should be added to `src/web/src/lib/course-time.ts` to avoid duplicating the conversion logic:

```pseudocode
function buildTeeTimeDateTime(timeHHmm: string, timeZoneId: string): string
  // getCourseToday returns "yyyy-MM-dd"
  const date = getCourseToday(timeZoneId)
  return `${date}T${timeHHmm}:00`
```

### API Design

No API changes. The backend `CreateTeeTimeOpeningRequest(DateTime TeeTime, int SlotsAvailable)` is correct. The frontend will now send:

```json
{ "teeTime": "2026-03-31T10:40:00", "slotsAvailable": 2 }
```

instead of the current broken:

```json
{ "teeTime": "10:40", "slotsAvailable": 2 }
```

### Risks

1. **Timezone edge case at midnight:** If an operator creates a tee time just before midnight, `getCourseToday()` could flip to the next day between rendering the form and submitting. This is an existing edge case in the product, not introduced by this fix -- the date was never sent before at all, so this is strictly an improvement.
2. **Type comment accuracy:** The `CreateTeeTimeOpeningRequest` interface in `waitlist.ts` currently has `teeTime: string` with no comment. Adding a comment clarifying `// ISO 8601 DateTime, e.g. "2026-03-31T10:00:00"` improves maintainability.

### Testing Strategy

**Frontend unit tests (Vitest):**
- Update `PostTeeTimeForm.test.tsx` assertion: the `data.teeTime` in the `mockMutate` call should match `yyyy-MM-ddTHH:mm:00` format instead of bare `HH:mm`
- Add a test to `AddTeeTimeOpeningDialog.test.tsx` verifying the mutation receives ISO DateTime format
- Add a unit test for the new `buildTeeTimeDateTime` helper in `src/web/src/lib/__tests__/course-time.test.ts`

**No backend test changes needed.** The existing `CreateTeeTimeOpeningRequestValidatorTests` already test with `DateTime` values.

### Dev Tasks

#### Frontend Developer
- [ ] Add `buildTeeTimeDateTime(timeHHmm: string, timeZoneId: string): string` helper to `src/web/src/lib/course-time.ts` that combines `getCourseToday(timeZoneId)` with the time string to produce ISO 8601 DateTime
- [ ] Add unit test for `buildTeeTimeDateTime` in `src/web/src/lib/__tests__/course-time.test.ts`
- [ ] In `PostTeeTimeForm.tsx` `onSubmit`, convert `data.teeTime` using `buildTeeTimeDateTime(data.teeTime, timeZoneId)` before passing to `createMutation.mutate()`
- [ ] In `AddTeeTimeOpeningDialog.tsx` `onSubmit`, convert `data.teeTime` using `buildTeeTimeDateTime(data.teeTime, timeZoneId)` before passing to `createMutation.mutate()`
- [ ] Update `CreateTeeTimeOpeningRequest` interface comment in `src/web/src/types/waitlist.ts` to document ISO 8601 DateTime format
- [ ] Update `PostTeeTimeForm.test.tsx` line 63 assertion: `teeTime` should match the pattern `yyyy-MM-ddTHH:mm:00` (use `expect.stringMatching` or compute the expected value from `getCourseToday('UTC')`)
- [ ] Add test in `AddTeeTimeOpeningDialog.test.tsx` that submits a valid time and verifies `mockCreateMutate` receives `teeTime` in ISO 8601 DateTime format
- [ ] Run `pnpm --dir src/web test` and `pnpm --dir src/web lint` to verify all tests pass

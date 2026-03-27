# Implementation Plan for #262 -- Cancel a tee time opening

## Approach

Add a `Cancel` method to the `TeeTimeOpening` aggregate that transitions status from `Open` to `Cancelled`, raises a `TeeTimeOpeningCancelled` domain event, and records a `CancelledAt` timestamp. A new API endpoint (`POST /courses/{courseId}/tee-time-openings/{openingId}/cancel`) invokes the domain method. A downstream handler reacts to `TeeTimeOpeningCancelled` to reject all pending offers (reusing the same pattern as `TeeTimeOpeningFilled`). The frontend adds a Cancel button to the openings table for `Open` openings, with a confirmation dialog.

### Open Questions -- Decisions

- **Visibility of cancelled openings:** Cancelled openings remain visible in the list with a "Cancelled" badge. The existing query already returns all openings for today regardless of status. No filtering is needed for v1 -- operators can see the full picture. Filtering can be added later if the list gets long.
- **SMS to golfers with pending offers:** When offers are rejected due to cancellation, the existing `WaitlistOfferRejected` SMS handler will fire automatically (it already exists). The rejection reason will be "Tee time opening has been cancelled by the course." -- this flows through the existing `offer.Reject(reason)` pattern and the SMS handler picks it up.

## Files

### Domain Layer (Shadowbrook.Domain)

- **Modify:** `src/backend/Shadowbrook.Domain/TeeTimeOpeningAggregate/TeeTimeOpeningStatus.cs` -- Add `Cancelled` enum value
- **Modify:** `src/backend/Shadowbrook.Domain/TeeTimeOpeningAggregate/TeeTimeOpening.cs` -- Add `CancelledAt` property and `Cancel()` method
- **Create:** `src/backend/Shadowbrook.Domain/TeeTimeOpeningAggregate/Events/TeeTimeOpeningCancelled.cs` -- New domain event

### API Layer (Shadowbrook.Api)

- **Modify:** `src/backend/Shadowbrook.Api/Features/Waitlist/Endpoints/WalkUpWaitlistEndpoints.cs` -- Add `CancelOpening` endpoint
- **Create:** `src/backend/Shadowbrook.Api/Features/Waitlist/Handlers/TeeTimeOpeningCancelled/RejectOffersHandler.cs` -- Handler to reject pending offers when opening is cancelled
- **Modify:** `src/backend/Shadowbrook.Api/Features/Waitlist/Policies/TeeTimeOpeningExpirationPolicy.cs` -- Handle `TeeTimeOpeningCancelled` to complete the saga (opening no longer needs expiration)
- **Modify:** `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/TeeTimeOpeningConfiguration.cs` -- Update `Status` max length from 10 to 12 (length of "Cancelled" is 9, fits in 10, but verify)

### Frontend (src/web)

- **Create:** `src/web/src/features/operator/components/CancelOpeningDialog.tsx` -- Confirmation dialog
- **Modify:** `src/web/src/features/operator/hooks/useWaitlist.ts` -- Add `useCancelTeeTimeOpening` mutation hook
- **Modify:** `src/web/src/features/operator/pages/WalkUpWaitlist.tsx` -- Add Cancel button to `OpeningsTable`, wire up dialog and mutation

### Tests

- **Modify:** `tests/Shadowbrook.Domain.Tests/TeeTimeOpeningAggregate/TeeTimeOpeningTests.cs` -- Tests for `Cancel()` method
- **Create:** `tests/Shadowbrook.Api.Tests/Handlers/TeeTimeOpeningCancelledRejectOffersHandlerTests.cs` -- Handler unit tests
- **Modify:** `tests/Shadowbrook.Api.Tests/Handlers/TeeTimeOpeningFilledRejectOffersHandlerTests.cs` -- No changes needed (just noting for reference)

## Data Model

### TeeTimeOpeningStatus enum

```
Open, Filled, Expired, Cancelled
```

### TeeTimeOpening aggregate additions

```
CancelledAt: DateTimeOffset? (private set)

Cancel(ITimeProvider):
  - Guard: if Status != Open, throw OpeningNotAvailableException
  - Set Status = Cancelled
  - Set CancelledAt = timeProvider.GetCurrentTimestamp()
  - Raise TeeTimeOpeningCancelled { OpeningId }
```

Note: Unlike `Expire()` which is idempotent (silently no-ops on non-Open), `Cancel()` should throw on non-Open status because cancellation is an explicit operator action -- if they try to cancel something already filled/expired/cancelled, that is a programming error or stale UI, and should be surfaced. The `OpeningNotAvailableException` already maps to 400 in the exception handler.

### TeeTimeOpeningCancelled event

```
record TeeTimeOpeningCancelled : IDomainEvent
  EventId: Guid (default NewGuid)
  OccurredAt: DateTimeOffset (default UtcNow)
  OpeningId: Guid (required)
```

### EF Configuration

The `Status` column currently has `HasMaxLength(10)`. "Cancelled" is 9 characters, which fits within 10. No max length change is needed. However, since we are adding the `CancelledAt` property, we must verify EF picks it up. The property is a simple nullable `DateTimeOffset` -- EF will map it automatically by convention. No explicit configuration required.

### Migration

A migration is needed for:
1. The new `CancelledAt` column (nullable `DateTimeOffset`)
2. No schema change needed for `Status` -- "Cancelled" (9 chars) fits within the existing `nvarchar(10)` column

## API Design

### Cancel Opening Endpoint

```
POST /courses/{courseId}/tee-time-openings/{openingId}/cancel
```

- No request body
- Success: `200 OK` with `WalkUpWaitlistOpeningResponse` (existing DTO)
- Opening not found: `404 Not Found` (via `GetRequiredByIdAsync`)
- Opening not Open: `400 Bad Request` (via `OpeningNotAvailableException`)

Why POST and not DELETE: Cancellation is a state transition, not resource deletion. The opening record persists for audit/history purposes. POST to an action sub-resource (`/cancel`) follows the existing pattern (see `/walkup-waitlist/close`, `/walkup-waitlist/reopen`).

### Endpoint implementation (pseudocode)

```
[WolverinePost("/courses/{courseId}/tee-time-openings/{openingId}/cancel")]
static async Task<IResult> CancelOpening(
    Guid courseId,
    Guid openingId,
    [NotBody] ITeeTimeOpeningRepository openingRepo,
    [NotBody] ITimeProvider timeProvider)

    opening = await openingRepo.GetRequiredByIdAsync(openingId)
    opening.Cancel(timeProvider)
    return Results.Ok(new WalkUpWaitlistOpeningResponse(...))
```

Note: No explicit course-scoping check needed beyond the existing `CourseExistsMiddleware` (which validates `{courseId}` in the route). We should verify the opening belongs to the course -- this is a lightweight guard. Add a check: if `opening.CourseId != courseId`, return `Results.NotFound()`.

## Event Flow

```
Operator clicks Cancel
  -> POST /courses/{courseId}/tee-time-openings/{openingId}/cancel
    -> opening.Cancel(timeProvider)
      -> raises TeeTimeOpeningCancelled

TeeTimeOpeningCancelled is handled by:
  1. TeeTimeOpeningCancelledRejectOffersHandler
     -> fetches pending offers for this opening
     -> calls offer.Reject("Tee time opening has been cancelled by the course.")
     -> each rejection raises WaitlistOfferRejected
       -> existing WaitlistOfferRejected/SmsHandler sends SMS notification

  2. TeeTimeOpeningExpirationPolicy.Handle(TeeTimeOpeningCancelled)
     -> MarkCompleted() (no need to expire a cancelled opening)
```

## Patterns

- **Domain method:** Follow the `Expire()` pattern but throw instead of silently returning for non-Open status (operator action vs system process)
- **Event:** Follow `TeeTimeOpeningFilled`/`TeeTimeOpeningExpired` pattern exactly
- **Handler:** Follow `TeeTimeOpeningFilled/RejectOffersHandler` pattern -- near-identical logic with different rejection message
- **Endpoint:** Follow existing action-endpoint pattern (`/close`, `/reopen`)
- **Frontend:** Follow `RemoveGolferDialog` pattern for the confirmation dialog (AlertDialog component)
- **Mutation hook:** Follow `useRemoveGolferFromWaitlist` pattern with cache invalidation

## Risks

1. **Stale UI race condition:** An operator might try to cancel an opening that was just filled by a concurrent offer acceptance. The domain throws `OpeningNotAvailableException` which returns 400. The frontend should handle this gracefully -- show an error and refetch the openings list.

2. **Saga completion:** The `TeeTimeOpeningExpirationPolicy` needs to handle `TeeTimeOpeningCancelled` to call `MarkCompleted()`. Without this, the saga would linger and eventually fire the expiration timeout on a cancelled opening (which would no-op in `Expire()` since status is Cancelled, but it is wasteful and leaves orphaned saga state).

3. **Status column width:** "Cancelled" is 9 characters, fits in `nvarchar(10)`. Safe but tight. If any future status is longer than 10 characters, the column constraint needs widening. Not a problem for this issue.

## Testing Strategy

### Domain Unit Tests (TeeTimeOpeningTests.cs)

- `Cancel_WhenOpen_TransitionsToCancelled` -- status, CancelledAt set
- `Cancel_WhenOpen_RaisesCancelledEvent` -- event type and OpeningId
- `Cancel_WhenFilled_ThrowsOpeningNotAvailableException`
- `Cancel_WhenExpired_ThrowsOpeningNotAvailableException`
- `Cancel_WhenAlreadyCancelled_ThrowsOpeningNotAvailableException`
- `Expire_WhenCancelled_IsIdempotent` -- existing Expire should no-op on Cancelled status
- `Claim_WhenCancelled_ThrowsOpeningNotAvailableException`

### Handler Unit Tests (TeeTimeOpeningCancelledRejectOffersHandlerTests.cs)

- `Handle_NoPendingOffers_DoesNothing`
- `Handle_PendingOffers_RejectsEachWithCancellationReason`

### Frontend Tests

- `CancelOpeningDialog` renders confirmation text and calls onConfirm
- `OpeningsTable` shows Cancel button only for Open openings
- `OpeningsTable` does not show Cancel button for Filled/Expired/Cancelled openings

## Dev Tasks

### Backend Developer

- [ ] Add `Cancelled` value to `TeeTimeOpeningStatus` enum
- [ ] Add `CancelledAt` property (nullable `DateTimeOffset`, private set) to `TeeTimeOpening`
- [ ] Implement `Cancel(ITimeProvider)` method on `TeeTimeOpening` -- guard on `Status == Open`, set `Cancelled` status and `CancelledAt`, raise `TeeTimeOpeningCancelled` event
- [ ] Create `TeeTimeOpeningCancelled` domain event record in `Events/`
- [ ] Update `TeeTimeOpening.Expire()` to also no-op when `Status == Cancelled` (currently only checks `Status != Open`, which will naturally handle Cancelled -- verify this)
- [ ] Add `CancelOpening` endpoint to `WalkUpWaitlistEndpoints.cs` -- `POST /courses/{courseId}/tee-time-openings/{openingId}/cancel`, load opening via `GetRequiredByIdAsync`, verify `opening.CourseId == courseId`, call `Cancel()`, return response
- [ ] Create `TeeTimeOpeningCancelled/RejectOffersHandler.cs` -- fetch pending offers, reject each with "Tee time opening has been cancelled by the course."
- [ ] Add `TeeTimeOpeningCancelled` handler to `TeeTimeOpeningExpirationPolicy` -- call `MarkCompleted()` (follow the `TeeTimeOpeningFilled` handler pattern with `[SagaIdentityFrom("OpeningId")]`)
- [ ] Add EF migration for the new `CancelledAt` column
- [ ] Write domain unit tests for `Cancel()` (6 test cases listed above)
- [ ] Write handler unit tests for `TeeTimeOpeningCancelledRejectOffersHandler` (2 test cases)
- [ ] Verify `dotnet build` and `dotnet test` pass

### Frontend Developer

- [ ] Create `CancelOpeningDialog.tsx` component using `AlertDialog` pattern (follow `RemoveGolferDialog` exactly) -- title: "Cancel Tee Time Opening?", description: "Cancel this tee time opening? Any pending offers will be withdrawn. This cannot be undone.", confirm button: "Cancel Opening" (destructive variant), cancel button: "Keep Opening"
- [ ] Add `useCancelTeeTimeOpening` mutation hook in `useWaitlist.ts` -- `POST /courses/{courseId}/tee-time-openings/{openingId}/cancel`, invalidate `walkUpWaitlist.today` on success
- [ ] Modify `OpeningsTable` in `WalkUpWaitlist.tsx` to accept `onCancel` callback and `cancellingOpeningId` prop
- [ ] Add Cancel button column to openings table -- only visible when `opening.status === 'Open'`, disabled while cancellation is pending
- [ ] Wire up `CancelOpeningDialog` in the `WalkUpWaitlist` page component with state management (follow the `RemoveGolferDialog` wiring pattern)
- [ ] Add `Cancelled` status styling to the openings table -- use a muted/destructive badge variant
- [ ] Handle cancellation error (show error message, allow retry)
- [ ] Write component tests for `CancelOpeningDialog` and updated `OpeningsTable`

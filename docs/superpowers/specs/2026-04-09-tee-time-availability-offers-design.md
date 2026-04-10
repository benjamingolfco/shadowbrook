# Automated Cancellation → Offer Pipeline on TeeTime

**Date:** 2026-04-09
**Branch:** `chore/teetime-ddd-exploration`
**Status:** Draft design — spec 2 of the tee sheet domain series.
**Predecessor:** `2026-04-08-tee-sheet-domain-design.md` (Spec 1: TeeSheet and TeeTime aggregates)

## Why this exists

Spec 1 introduced `TeeTime` with persistent capacity tracking — `Claim()`, `ReleaseClaim()`, `Remaining`, and events like `TeeTimeClaimReleased` and `TeeTimeReopened`. When a golfer cancels a booking, the `TeeTime` knows capacity freed up. But nothing acts on that information.

Today, filling freed capacity requires the operator to manually create a `TeeTimeOpening` and let the existing walk-up waitlist offer flow take over. That manual step only works for same-day walk-up scenarios where an operator is actively watching the tee sheet. It doesn't work for:

- **Cancellations on future dates** — no operator is watching tomorrow's sheet.
- **High-volume cancellations** — operator can't keep up with posting openings one at a time.
- **Online waitlist (future)** — golfers who waitlisted through the app expect automatic notification, not operator intervention.

This spec builds the automated pipeline: when a `TeeTime` has capacity freed by a cancellation, a policy orchestrates finding eligible waitlist entries, sending offers, and tracking the fill process — without operator involvement.

## Scope

### In scope

1. New `TeeTimeOffer` aggregate — represents an offer to a waitlisted golfer to book a specific `TeeTime`. Similar shape to `WaitlistOffer`, referencing `TeeTimeId` instead of `OpeningId`. Acceptance happens through the existing book endpoint, not through the offer itself.
2. New `TeeTimeAvailabilityPolicy` (Wolverine saga) — starts when a `TeeTimeClaimReleased` fires, orchestrates finding eligible golfers, dispatching offers in batches with a grace period, and tracking pending offers vs. available slots.
3. New `TeeTimeAvailabilityChanged` domain event on `TeeTime` — raised whenever `Remaining` changes, giving the policy an authoritative slot count rather than doing arithmetic from individual events.
4. New `ITeeTimeWaitlistMatcher` interface — abstraction for finding eligible waitlist entries for a given TeeTime. Ships with an empty implementation (returns no entries) as the seam for future specs.
5. Token-based anonymous booking authentication — the offer's `Token` serves as a credential for anonymous golfers (walk-up or future online) to book through the existing book endpoint. New authorization policy on the endpoint.
6. Offer acceptance via domain event correlation — a handler reacts to `BookingConfirmed`, finds the matching pending `TeeTimeOffer` by `TeeTimeId` + `GolferId`, and marks it accepted.
7. Domain tests for `TeeTimeOffer`, policy unit tests for `TeeTimeAvailabilityPolicy`.

### Not in scope

- **Walk-up system** — `TeeTimeOpening`, `WaitlistOffer`, walk-up waitlist, all existing walk-up handlers, policies, and endpoints are untouched. The two systems are completely separate.
- **Online waitlist entry creation** — no new join flow for golfers to express interest in a tee time via the app. That's a future spec that plugs into the `ITeeTimeWaitlistMatcher` seam.
- **Changes to `TeeTime` aggregate behavior** — no new statuses, no `PostToWaitlist()` method. The only change to `TeeTime` is raising `TeeTimeAvailabilityChanged` in its existing `ApplyClaim()` and `ReleaseClaim()` methods.
- **Domain project split** — everything stays in `Teeforce.Domain`.
- **Operator UI** — no frontend work.
- **SMS templates / notification content** — handlers raise events; notification handlers that format and send SMS are a separate concern.

### Relationship to walk-up

The walk-up waitlist system (`TeeTimeOpening`, `WaitlistOffer`, `TeeTimeOpeningOfferPolicy`) continues to operate independently. Walk-up works without a published `TeeSheet` — the operator creates ad-hoc `TeeTimeOpening`s. The automated pipeline in this spec requires a published `TeeSheet` and existing `TeeTime` rows (from Spec 1's claim flow). The two systems serve different scenarios:

| | Walk-up (existing) | Automated (this spec) |
|---|---|---|
| **Trigger** | Operator manually creates `TeeTimeOpening` | `TeeTimeClaimReleased` event from cancellation |
| **Requires TeeSheet** | No | Yes (needs `BookingAuthorization` for booking) |
| **Offer aggregate** | `WaitlistOffer` → `OpeningId` | `TeeTimeOffer` → `TeeTimeId` |
| **Claim on accept** | `TeeTimeOpening.TryClaim()` | `TeeTime.Claim()` via book endpoint |
| **Policy** | `TeeTimeOpeningOfferPolicy` | `TeeTimeAvailabilityPolicy` |

Eventually, when all courses use tee sheets, walk-up can migrate to the automated system. That's a future spec.

## Design

### `TeeTimeOffer` aggregate

```
TeeTimeOffer : Entity
├── TeeTimeId               Guid — which TeeTime the offer is for
├── GolferWaitlistEntryId    Guid — which waitlist entry matched
├── GolferId                Guid
├── GroupSize               int
├── Token                   Guid — anonymous booking credential
├── CourseId                Guid — denormalized for queries
├── Date                    DateOnly
├── Time                    TimeOnly
├── Status                  TeeTimeOfferStatus (Pending/Accepted/Rejected/Expired)
├── RejectionReason         string?
├── IsStale                 bool
├── CreatedAt               DateTimeOffset
├── NotifiedAt              DateTimeOffset?
```

Lives in `Teeforce.Domain/TeeTimeOfferAggregate/`.

**Factory:** `TeeTimeOffer.Create(teeTimeId, golferWaitlistEntryId, golferId, groupSize, courseId, date, time, timeProvider)` — public static factory (same pattern as other aggregates). The matching handler is the intended caller. Generates `Token` via `Guid.CreateVersion7()`. Raises `TeeTimeOfferCreated`.

**Methods:**

- `MarkNotified(timeProvider)` — sets `NotifiedAt`. Raises `TeeTimeOfferSent`. Throws `OfferAlreadyNotifiedException` if already notified.
- `MarkAccepted(bookingId)` — records that the golfer booked. Raises `TeeTimeOfferAccepted`. Throws `OfferNotPendingException` if not Pending.
- `Reject(reason)` — idempotent if already resolved. Sets `RejectionReason`. Raises `TeeTimeOfferRejected`.
- `Expire()` — timeout hit, golfer didn't book. Raises `TeeTimeOfferExpired`. Idempotent if already resolved.
- `MarkStale()` — slot filled by someone else while offer was pending. Raises `TeeTimeOfferStale`. Idempotent if already resolved or stale.

**Key difference from `WaitlistOffer`:** No pre-allocated `BookingId`. The golfer books through the normal book endpoint, so the `BookingId` isn't known until acceptance. `MarkAccepted(bookingId)` receives it after the fact. There is no `Accept()` method that drives the booking — the offer is a notification/tracking record, not an orchestration point.

**`TeeTimeOfferStatus` enum:** `Pending`, `Accepted`, `Rejected`, `Expired`.

### `TeeTimeAvailabilityChanged` event

New domain event raised by `TeeTime` whenever `Remaining` changes:

```csharp
public record TeeTimeAvailabilityChanged : IDomainEvent
{
    public required Guid TeeTimeId { get; init; }
    public required int Remaining { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly Time { get; init; }
}
```

Raised in both `ApplyClaim()` (remaining goes down) and `ReleaseClaim()` (remaining goes up) inside the existing `TeeTime` aggregate. This gives the `TeeTimeAvailabilityPolicy` an authoritative slot count — the policy never does its own arithmetic on remaining capacity.

### `TeeTimeAvailabilityPolicy` (Wolverine saga)

Orchestrates the fill process for a TeeTime that has freed capacity.

**State:**

```
TeeTimeAvailabilityPolicy : Saga
├── Id                      Guid — the TeeTimeId
├── PendingOfferIds         HashSet<Guid> — offer IDs with pending outcomes
├── SlotsRemaining          int — last known from TeeTimeAvailabilityChanged
├── GracePeriodExpired      bool
```

**Lifecycle:**

1. **`TeeTimeClaimReleased` starts the policy.** Sets `Id = evt.TeeTimeId`. Does not initialize `SlotsRemaining` — the `TeeTimeAvailabilityChanged` event fires from the same `ReleaseClaim()` call and will arrive to set the authoritative value. Returns a `GracePeriodTimeout` to debounce rapid successive cancellations.
2. **`GracePeriodTimeout` fires.** Sets `GracePeriodExpired = true`. Dispatches `FindAndOfferEligibleGolfers(TeeTimeId, AvailableSlots)` where `AvailableSlots = SlotsRemaining - PendingOfferIds.Count`.
3. **`TeeTimeOfferSent`** → `PendingOfferIds.Add(offerId)`.
4. **`TeeTimeAvailabilityChanged`** → `SlotsRemaining = evt.Remaining`. Dispatches more offers if `SlotsRemaining - PendingOfferIds.Count > 0` and grace period has expired.
5. **`TeeTimeOfferAccepted` / `TeeTimeOfferRejected` / `TeeTimeOfferExpired` / `TeeTimeOfferStale`** → `PendingOfferIds.Remove(offerId)`. Dispatches more offers if slots available.
6. **Completes when:** `SlotsRemaining == 0` (TeeTime filled), or `PendingOfferIds` is empty and no more eligible entries (handler returned zero offers on last dispatch).

**Correlation:** Policy keyed by `TeeTimeId`. Events use `[SagaIdentityFrom("TeeTimeId")]`.

**`NotFound` handlers:** Log and discard events for completed policies (same pattern as existing `TeeTimeOpeningOfferPolicy`).

**EF mapping:** `PendingOfferIds` mapped as a JSON column. Policy entity mapped in `ApplicationDbContext` with `IEntityTypeConfiguration`, ignoring the `Version` property from `Saga` base class.

### `ITeeTimeWaitlistMatcher` interface

```csharp
public interface ITeeTimeWaitlistMatcher
{
    Task<List<GolferWaitlistEntry>> FindEligibleEntries(
        Guid teeTimeId, Guid courseId, DateOnly date, TimeOnly time,
        int availableSlots, CancellationToken ct);
}
```

Lives in `Teeforce.Domain/WaitlistServices/` alongside the existing `WaitlistMatchingService`.

**Implementation for this spec:** Returns an empty list. This is the explicit seam where a future online waitlist spec plugs in. The implementation will query waitlist entries by course + date + time window + group size fit.

The existing `WaitlistMatchingService` is not reused — it's coupled to `TeeTimeOpening`. The new interface is `TeeTime`-oriented.

### Events

**New events from `TeeTimeOffer`:**

| Event | Key fields | Raised by |
|-------|-----------|-----------|
| `TeeTimeOfferCreated` | `TeeTimeOfferId`, `TeeTimeId`, `GolferId`, `GroupSize`, `CourseId`, `Date`, `Time` | `Create()` |
| `TeeTimeOfferSent` | `TeeTimeOfferId`, `TeeTimeId`, `GolferId`, `GroupSize` | `MarkNotified()` |
| `TeeTimeOfferAccepted` | `TeeTimeOfferId`, `TeeTimeId`, `BookingId`, `GolferId`, `GroupSize`, `CourseId`, `Date`, `Time` | `MarkAccepted()` |
| `TeeTimeOfferRejected` | `TeeTimeOfferId`, `TeeTimeId`, `Reason` | `Reject()` |
| `TeeTimeOfferExpired` | `TeeTimeOfferId`, `TeeTimeId` | `Expire()` |
| `TeeTimeOfferStale` | `TeeTimeOfferId`, `TeeTimeId` | `MarkStale()` |

**New event from `TeeTime`:**

| Event | Key fields | Raised by |
|-------|-----------|-----------|
| `TeeTimeAvailabilityChanged` | `TeeTimeId`, `Remaining`, `CourseId`, `Date`, `Time` | `ApplyClaim()`, `ReleaseClaim()` |

**Policy commands (colocated in policy file):**

| Command | Fields |
|---------|--------|
| `FindAndOfferEligibleGolfers` | `TeeTimeId`, `AvailableSlots` |
| `AvailabilityGracePeriodTimeout` | `Id` (saga ID = TeeTimeId), `Delay` |

### Booking acceptance flow

1. **Golfer receives SMS** with a link containing the offer token.
2. **Golfer clicks link** → frontend booking page, token in URL.
3. **Frontend calls book endpoint** with the token.
4. **Endpoint authenticates** via `TeeTimeOfferTokenAuthorizationPolicy` — validates token against a pending `TeeTimeOffer`, extracts `GolferId`, `TeeTimeId`, `CourseId`.
5. **Book endpoint** loads `TeeSheet`, calls `AuthorizeBooking()`, calls `TeeTime.Claim()`, creates `Booking` → raises `BookingConfirmed`.
6. **Handler reacts to `BookingConfirmed`** → queries `TeeTimeOffer` where `TeeTimeId == event.TeeTimeId && GolferId == event.GolferId && Status == Pending`. If found, calls `offer.MarkAccepted(event.BookingId)`. If not found, this was a direct booking — no-op.
7. **`TeeTimeOfferAccepted`** → `TeeTimeAvailabilityPolicy` removes from `PendingOfferIds`.

**Edge cases:**

- **Slot fills before golfer books:** `TeeTime.Claim()` throws `InsufficientCapacityException`. Offer stays pending until timeout expires it.
- **Golfer books a different TeeTime:** Offer for the original TeeTime stays pending, eventually expires.
- **Two pending offers for same golfer on same TeeTime:** Prevented by the matching handler — skip golfers with pending offers for that TeeTime.

### Token authentication

The book endpoint (`POST /courses/{courseId}/tee-times/book`) supports two auth modes:

- **Authenticated golfer** — existing JWT auth policy, golfer ID from claims.
- **Anonymous golfer with offer token** — new `TeeTimeOfferTokenAuthorizationPolicy`. The token is a query parameter or header. The policy validates the token against a pending `TeeTimeOffer`, extracts `GolferId` and verifies the `TeeTimeId`/`CourseId` match the request. If valid, the request proceeds with the offer's `GolferId` as the authenticated identity.

The endpoint itself is unchanged — it takes a `golferId` and calls `TeeTime.Claim()` regardless of how the caller authenticated.

### File structure

**Domain — New files:**

| File | Responsibility |
|------|---------------|
| `Teeforce.Domain/TeeTimeOfferAggregate/TeeTimeOffer.cs` | Aggregate |
| `Teeforce.Domain/TeeTimeOfferAggregate/TeeTimeOfferStatus.cs` | Enum |
| `Teeforce.Domain/TeeTimeOfferAggregate/ITeeTimeOfferRepository.cs` | Repository interface |
| `Teeforce.Domain/TeeTimeOfferAggregate/Events/TeeTimeOfferCreated.cs` | Event |
| `Teeforce.Domain/TeeTimeOfferAggregate/Events/TeeTimeOfferSent.cs` | Event |
| `Teeforce.Domain/TeeTimeOfferAggregate/Events/TeeTimeOfferAccepted.cs` | Event |
| `Teeforce.Domain/TeeTimeOfferAggregate/Events/TeeTimeOfferRejected.cs` | Event |
| `Teeforce.Domain/TeeTimeOfferAggregate/Events/TeeTimeOfferExpired.cs` | Event |
| `Teeforce.Domain/TeeTimeOfferAggregate/Events/TeeTimeOfferStale.cs` | Event |
| `Teeforce.Domain/TeeTimeOfferAggregate/Exceptions/OfferAlreadyNotifiedException.cs` | Exception |
| `Teeforce.Domain/TeeTimeOfferAggregate/Exceptions/OfferNotPendingException.cs` | Exception |
| `Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeAvailabilityChanged.cs` | Event |
| `Teeforce.Domain/WaitlistServices/ITeeTimeWaitlistMatcher.cs` | Interface |

**Domain — Modified files:**

| File | Change |
|------|--------|
| `Teeforce.Domain/TeeTimeAggregate/TeeTime.cs` | Raise `TeeTimeAvailabilityChanged` in `ApplyClaim()` and `ReleaseClaim()` |

**API — New files:**

| File | Responsibility |
|------|---------------|
| `Teeforce.Api/Infrastructure/Repositories/TeeTimeOfferRepository.cs` | Repository impl |
| `Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeTimeOfferConfiguration.cs` | EF mapping |
| `Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeTimeAvailabilityPolicyConfiguration.cs` | EF mapping for saga |
| `Teeforce.Api/Infrastructure/Services/EmptyTeeTimeWaitlistMatcher.cs` | Empty implementation |
| `Teeforce.Api/Features/TeeSheet/Policies/TeeTimeAvailabilityPolicy.cs` | Saga |
| `Teeforce.Api/Features/TeeSheet/Handlers/FindAndOfferEligibleGolfers/Handler.cs` | Matching handler |
| `Teeforce.Api/Features/TeeSheet/Handlers/BookingConfirmed/MarkOfferAcceptedHandler.cs` | Correlates booking to offer |
| `Teeforce.Api/Features/TeeSheet/Handlers/TeeTimeOfferCreated/SendNotificationHandler.cs` | SMS notification |
| `Teeforce.Api/Auth/TeeTimeOfferTokenAuthorizationPolicy.cs` | Token auth |

**API — Modified files:**

| File | Change |
|------|--------|
| `Teeforce.Api/Infrastructure/Data/ApplicationDbContext.cs` | Add `DbSet<TeeTimeOffer>`, `DbSet<TeeTimeAvailabilityPolicy>` |
| `Teeforce.Api/Features/TeeSheet/Endpoints/BookingEndpoints.cs` | Add anonymous token auth support |
| `Teeforce.Api/Program.cs` | Register `ITeeTimeWaitlistMatcher`, new exception types in global handler |

**Tests — New files:**

| File | Responsibility |
|------|---------------|
| `Teeforce.Domain.Tests/TeeTimeOfferAggregate/TeeTimeOfferTests.cs` | Domain unit tests |
| `Teeforce.Api.Tests/Features/TeeSheet/Policies/TeeTimeAvailabilityPolicyTests.cs` | Policy unit tests |
| `Teeforce.Api.Tests/Features/TeeSheet/Handlers/MarkOfferAcceptedHandlerTests.cs` | Handler unit tests |
| `Teeforce.Api.Tests/Features/TeeSheet/Handlers/FindAndOfferEligibleGolfersHandlerTests.cs` | Handler unit tests |

## Success criteria

- All domain unit tests pass for `TeeTimeOffer` — creation, notification, acceptance, rejection, expiration, staleness, idempotent guards.
- All policy unit tests pass for `TeeTimeAvailabilityPolicy` — start on claim released, grace period, dispatch, pending offer tracking by ID, availability changed sync, completion conditions.
- `TeeTimeAvailabilityChanged` raised correctly in existing `TeeTime.ApplyClaim()` and `TeeTime.ReleaseClaim()` — verified by updating existing domain tests.
- Handler unit tests pass — `FindAndOfferEligibleGolfers` calls matcher and creates offers, `MarkOfferAccepted` correlates booking to offer.
- `dotnet build teeforce.slnx` clean. `dotnet format` clean.
- Existing walk-up tests still pass — no regressions to `TeeTimeOpening`, `WaitlistOffer`, or walk-up flows.
- EF migration applies cleanly.

## Follow-on specs (named, not designed)

- **Spec 3 — Online waitlist.** Golfers express interest in a tee time range via the app (authenticated). Creates entries that `ITeeTimeWaitlistMatcher` queries. Plugs into the pipeline built in this spec.
- **Spec 4 — Walk-up migration.** When all courses use tee sheets, migrate walk-up to use `TeeTime` + `TeeTimeOffer` instead of `TeeTimeOpening` + `WaitlistOffer`. Delete `TeeTimeOpening`.
- **Spec 5 — Operator day-level ops.** Blocks, frost delays, shotgun events, close-the-day. Builds on `TeeSheet.*` and `TeeTime.Block()`/`Unblock()`.

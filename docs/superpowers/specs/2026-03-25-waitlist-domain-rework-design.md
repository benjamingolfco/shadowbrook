# Waitlist Domain Rework — Design Spec

**Date:** 2026-03-25
**Branch:** `chore/waitlist-offer-policy-refactor`
**Approach:** Domain-first with TDD, evolve outward to handlers and policies. Fresh EF migration when complete.

## Context

The current waitlist/offer domain has naming and structural issues:
- `TeeTimeRequest` implies a golfer is requesting — it's actually an operator-announced opening
- `GolferWaitlistEntry` carries speculative fields (`IsReady`) and lacks time windowing
- `WalkUpWaitlist` name is too narrow — the waitlist concept supports both walk-up and online entries
- Eligibility/matching logic lives in application-layer handlers instead of the domain
- No separation between the TeeTime (booking surface) and the waitlist (demand pool)
- The offer policy is sequential (one at a time) when openings often have multiple slots

## Key Decisions

1. **Walk-up waitlist and TeeTime booking are separate systems.** The walk-up waitlist is a self-contained feature. The TeeTime aggregate is deferred (see `docs/plans/2026-03-25-teetime-aggregate-thoughts.md`).
2. **No TeeTime aggregate in this phase.** Operator-owned openings are the source of truth for slot availability. The TeeTimeOpening itself tracks and controls slots via a `Claim` method. When the TeeTime aggregate arrives later, non-operator openings will use the TeeTime as the authority instead.
3. **TeeTimeOpening is persistent.** Each opening cycle is its own record with a lifecycle (Open/Filled/Expired). Useful for analytics (fill rates, response times).
4. **Entries stay as separate aggregate roots.** Conceptually the waitlist owns entries, but technically they're independent for concurrency (multiple golfers joining/leaving simultaneously).
5. **Matching logic lives in a domain service** that delegates to repository queries — no in-memory filtering of large entry lists.
6. **Offer policy manages concurrent offers** — multiple golfers offered simultaneously, throttled by remaining slots.
7. **Fresh EF migration** at the end — no incremental migration gymnastics.
8. **Bookings start as Pending.** A BookingConfirmationPolicy coordinates between the offer acceptance and the opening's slot claim. Bookings are confirmed only after the opening successfully claims the slots.

## Domain Model

### CourseWaitlist Hierarchy (Waitlist Domain)

Abstract base with two concrete subtypes. The waitlist is a factory for entries but doesn't manage them after creation.

```
CourseWaitlist (abstract aggregate root)
  Id              Guid
  CourseId        Guid
  Date            DateOnly
  CreatedAt       DateTimeOffset

  Join(golfer, entryRepository, groupSize, ...) -> GolferWaitlistEntry
    -> raises GolferJoinedWaitlist { EntryId, CourseWaitlistId, GolferId }
```

```
WalkUpWaitlist extends CourseWaitlist
  ShortCode       string (4-digit)
  Status          Open | Closed
  OpenedAt        DateTimeOffset
  ClosedAt        DateTimeOffset?

  Open()          -> raises WalkUpWaitlistOpened
  Close()         -> raises WalkUpWaitlistClosed
  Reopen()        -> raises WalkUpWaitlistReopened

  Join() override
    -> creates WalkUpGolferWaitlistEntry
    -> sets WindowStart = now, WindowEnd = now + 30min
```

```
OnlineWaitlist extends CourseWaitlist

  Join(golfer, entryRepository, groupSize, windowStart, windowEnd) override
    -> creates OnlineGolferWaitlistEntry
    -> sets golfer-specified time window
```

**EF mapping:** TPH (table-per-hierarchy) with discriminator column.

### GolferWaitlistEntry Hierarchy (Waitlist Domain)

Separate aggregate root for concurrency. Inheritance with `IsWalkUp` as TPH discriminator.

```
GolferWaitlistEntry (abstract aggregate root)
  Id                  Guid
  CourseWaitlistId    Guid
  GolferId            Guid
  IsWalkUp            bool (TPH discriminator)
  GroupSize           int (1-4)
  JoinedAt            DateTimeOffset
  RemovedAt           DateTimeOffset?
  WindowStart         TimeOnly
  WindowEnd           TimeOnly
  CreatedAt           DateTimeOffset

  Remove() -> raises GolferRemovedFromWaitlist { EntryId, GolferId }
```

```
WalkUpGolferWaitlistEntry extends GolferWaitlistEntry
  ExtendWindow(newEnd)
    -> raises WalkUpEntryWindowExtended { EntryId, NewEnd }
```

```
OnlineGolferWaitlistEntry extends GolferWaitlistEntry
  (nothing extra yet)
```

**EF discriminator:**
```csharp
builder.HasDiscriminator(e => e.IsWalkUp)
    .HasValue<WalkUpGolferWaitlistEntry>(true)
    .HasValue<OnlineGolferWaitlistEntry>(false);
```

**Constraints:** Unique filtered index on `(CourseWaitlistId, GolferId)` where `RemovedAt IS NULL`.

### TeeTimeOpening (Waitlist Domain)

Persistent record of each opening cycle. Each cycle gets its own Id for analytics tracking.

For operator-owned openings, the TeeTimeOpening is the **source of truth** for slot availability. The `Claim` method is the atomic operation that reserves slots — if slots remain, it claims them; if not, it rejects.

When the TeeTime aggregate is introduced later, non-operator openings will mirror TeeTime state via events instead of managing slots directly.

```
TeeTimeOpening (aggregate root)
  Id                Guid
  CourseId          Guid
  Date              DateOnly
  TeeTime           TimeOnly
  SlotsAvailable    int (what operator announced)
  SlotsRemaining    int (decremented by claims)
  OperatorOwned     bool (true for walk-up phase; future: false for cancellation-triggered)
  Status            Open | Filled | Expired
  CreatedAt         DateTimeOffset
  FilledAt          DateTimeOffset?
  ExpiredAt         DateTimeOffset?

  Claim(bookingId, golferId, groupSize)
    -> if SlotsRemaining >= groupSize:
         decrement SlotsRemaining
         raises TeeTimeOpeningClaimed { OpeningId, BookingId, GolferId, CourseId, Date, TeeTime }
         if SlotsRemaining == 0: set Status = Filled, raises TeeTimeOpeningFilled { OpeningId }
    -> if SlotsRemaining < groupSize:
         raises TeeTimeOpeningClaimRejected { OpeningId, BookingId, GolferId }

  Expire()
    -> sets Status = Expired, ExpiredAt
    -> raises TeeTimeOpeningExpired { OpeningId }
```

**Events:**
- `TeeTimeOpeningCreated { OpeningId, CourseId, Date, TeeTime, SlotsAvailable }`
- `TeeTimeOpeningClaimed { OpeningId, BookingId, GolferId, CourseId, Date, TeeTime }`
- `TeeTimeOpeningClaimRejected { OpeningId, BookingId, GolferId }`
- `TeeTimeOpeningFilled { OpeningId }`
- `TeeTimeOpeningExpired { OpeningId }`

### WaitlistOffer (Waitlist Domain — design deferred)

The offer aggregate will be revisited when implementation reaches the offer flow. Current design assumptions:
- Separate aggregate root (one per golfer per opening)
- Created by the offer policy via a handler
- Has a public token for SMS links
- Statuses: Pending, Accepted, Rejected
- Events: WaitlistOfferSent, WaitlistOfferAccepted, WaitlistOfferRejected

The policies below reference these events. The offer's internal structure and state transitions will be finalized during implementation — the event contract is what matters for policy design.

### Booking (Booking Domain — minimal changes)

The existing Booking aggregate gains a Pending state. Bookings start as Pending and are confirmed or rejected by the BookingConfirmationPolicy based on the opening's claim result.

Booking creation is owned by the **booking domain**. A handler in the booking domain listens for `WaitlistOfferAccepted` and creates the Booking (Pending). The waitlist domain never creates bookings directly.

- `BookingCreated` event now includes a Pending status
- `BookingConfirmed` — new event, raised when claim succeeds
- `BookingRejected` — new event, raised when claim fails

## Domain Service

### WaitlistMatchingService

Finds eligible waitlist entries for an opening. Queries at the DB level for freshness.

```
WaitlistMatchingService
  Dependencies: IGolferWaitlistEntryRepository

  FindEligibleEntries(opening: TeeTimeOpening) -> List<GolferWaitlistEntry>
    -> calls repository.FindEligibleEntries(...)
    -> returns entries ordered by JoinedAt (FIFO)
```

**Repository query criteria:**
- Active entries (`RemovedAt IS NULL`)
- Entry's time window covers the opening's tee time (`WindowStart <= TeeTime <= WindowEnd`)
- `GroupSize <= opening.SlotsRemaining`
- No pending offer for this opening
- Ordered by `JoinedAt` ascending (FIFO)

**Future considerations:**
- Priority between walk-up and online entries (course-configurable)
- Additional matching rules as business needs evolve

## Policies (Wolverine Sagas)

### TeeTimeOpeningOfferPolicy (per opening)

Manages concurrent offers for a single opening. Throttles offers so pending count never exceeds remaining slots.

```
State:
  Id                  Guid (OpeningId)
  PendingOfferCount   int
  SlotsRemaining      int

Handlers:
  Start(TeeTimeOpeningCreated)
    -> set SlotsRemaining = SlotsAvailable
    -> dispatch FindAndOfferEligibleGolfers

  Handle(WaitlistOfferSent)
    -> increment PendingOfferCount

  Handle(WaitlistOfferAccepted)
    -> decrement PendingOfferCount

  Handle(TeeTimeOpeningClaimed)
    -> update SlotsRemaining
    -> if PendingOfferCount < SlotsRemaining, dispatch more offers

  Handle(WaitlistOfferRejected)
    -> decrement PendingOfferCount
    -> if PendingOfferCount < SlotsRemaining, dispatch more offers

  Handle(WaitlistOfferStale)
    -> decrement PendingOfferCount
    -> if PendingOfferCount < SlotsRemaining, dispatch more offers

  Handle(TeeTimeOpeningFilled)
    -> reject all pending offers, mark complete

  Handle(TeeTimeOpeningExpired)
    -> reject all pending offers, mark complete
```

### WaitlistOfferResponsePolicy (per offer)

Manages the response buffer for a single offer. Buffer duration depends on golfer type.

```
State:
  Id          Guid (OfferId)
  IsWalkUp    bool

Handlers:
  Start(WaitlistOfferSent)
    -> set IsWalkUp from entry type
    -> schedule buffer timeout (60s for walk-up, longer for online)

  Handle(BufferTimeout)
    -> raise WaitlistOfferStale { OfferId, OpeningId }

  Handle(WaitlistOfferAccepted)
    -> mark complete

  Handle(WaitlistOfferRejected)
    -> mark complete
```

### BookingConfirmationPolicy (per booking)

Coordinates between offer acceptance and the opening's slot claim. Ensures bookings are only confirmed when slots are actually available.

```
State:
  Id          Guid (BookingId)

Handlers:
  Start(BookingCreated) where Status = Pending
    -> dispatch ClaimOpeningSlots command (opening.Claim)

  Handle(TeeTimeOpeningClaimed) where BookingId matches
    -> confirm booking -> raises BookingConfirmed

  Handle(TeeTimeOpeningClaimRejected) where BookingId matches
    -> reject booking -> raises BookingRejected
    -> mark complete

  Handle(BookingConfirmed)
    -> mark complete
```

### TeeTimeOpeningExpirationPolicy (per opening)

Auto-expires an opening when the tee time passes. Replaces current `TeeTimeRequestExpirationPolicy`.

```
State:
  Id          Guid (OpeningId)

Handlers:
  Start(TeeTimeOpeningCreated)
    -> calculate delay until tee time
    -> schedule expiration timeout

  Handle(ExpirationTimeout)
    -> call opening.Expire()
    -> mark complete

  Handle(TeeTimeOpeningFilled)
    -> mark complete (no need to expire)
```

## Event Flow — Walk-Up Phase

### Happy Path: 3-Slot Opening

```
1. Operator opens WalkUpWaitlist for today
   -> WalkUpWaitlistOpened

2. Golfers join via QR/walk-up
   -> GolferJoinedWaitlist (x3)
   -> Each entry gets 30-min window from join time

3. Operator creates TeeTimeOpening (OperatorOwned=true, 3 slots at 10:30)
   -> TeeTimeOpeningCreated
   -> TeeTimeOpeningOfferPolicy starts (SlotsRemaining=3, Pending=0)
   -> TeeTimeOpeningExpirationPolicy starts
   -> WaitlistMatchingService finds 3 eligible entries
   -> 3 WaitlistOffers created and sent
   -> 3 WaitlistOfferResponsePolicies start (60s buffer each)
   -> TeeTimeOpeningOfferPolicy: Pending=3, SlotsRemaining=3 -- balanced

4. Golfer A accepts (group of 1)
   -> WaitlistOfferAccepted
   -> Booking domain handler creates Booking (Pending)
   -> BookingCreated (Pending)
   -> BookingConfirmationPolicy starts
   -> opening.Claim(bookingId, golferA, 1) -> TeeTimeOpeningClaimed
   -> BookingConfirmed
   -> OfferPolicy: SlotsRemaining=2, Pending=2 -- balanced

5. Golfer B's 60s buffer expires
   -> BufferTimeout -> WaitlistOfferStale
   -> OfferPolicy: Pending drops to 1, SlotsRemaining=2 -- offer 1 more

6. Golfer C accepts (group of 2)
   -> opening.Claim(bookingId, golferC, 2) -> TeeTimeOpeningClaimed { SlotsRemaining: 0 }
   -> TeeTimeOpeningFilled
   -> BookingConfirmed
   -> OfferPolicy: reject remaining pending offers, complete
   -> ExpirationPolicy: complete
```

### Edge Case: Opening Created, No Waitlist Entries

```
1. Operator creates TeeTimeOpening
   -> TeeTimeOpeningCreated
   -> OfferPolicy starts, finds no eligible entries -- sits idle
   -> ExpirationPolicy starts timer

2. Golfer joins waitlist later
   -> GolferJoinedWaitlist
   -> Handler checks for active openings matching this golfer's window
   -> If match found, dispatches WakeUpOfferPolicy { OpeningId } command
```

### Edge Case: Concurrent Acceptances Exceed Slots

```
1. Opening has 1 slot remaining, 1 pending offer
2. Golfer accepts -> Booking domain creates Booking (Pending) -> BookingCreated
   -> BookingConfirmationPolicy -> opening.Claim(bookingId, golfer, 1)
   -> TeeTimeOpeningClaimed, SlotsRemaining=0 -> TeeTimeOpeningFilled
   -> BookingConfirmed

3. Meanwhile, stale offer's golfer also accepts -> Booking domain creates Booking (Pending)
   -> BookingConfirmationPolicy -> opening.Claim(...)
   -> TeeTimeOpeningClaimRejected (no slots left)
   -> BookingRejected
   -> SMS: "Sorry, that tee time is no longer available."
```

## What's Removed

- `TeeTimeRequest` aggregate -- replaced by `TeeTimeOpening`
- `TeeTimeSlotFill` entity -- replaced by `Claim` on opening + existing Booking
- `TeeTimeRequestService` domain service -- replaced by `WaitlistMatchingService`
- `IsReady` field on entries -- speculative, always true
- `TeeTimeRequestAdded`, `TeeTimeRequestFulfilled`, `TeeTimeRequestClosed` events -- replaced by opening events
- `TeeTimeOfferPolicy` -- replaced by `TeeTimeOpeningOfferPolicy` + `WaitlistOfferResponsePolicy`
- `TeeTimeRequestExpirationPolicy` -- replaced by `TeeTimeOpeningExpirationPolicy`
- `NotifyNextEligibleGolferHandler` -- replaced by handler using `WaitlistMatchingService`
- All existing EF migrations -- fresh migration created at the end

## What's New

- `CourseWaitlist` abstract base with `WalkUpWaitlist` and `OnlineWaitlist` subtypes
- `GolferWaitlistEntry` hierarchy with `WalkUpGolferWaitlistEntry` and `OnlineGolferWaitlistEntry`
- Time window on all entries (`WindowStart`, `WindowEnd`)
- `TeeTimeOpening` aggregate with `Claim` method (source of truth for operator-owned openings)
- `OperatorOwned` flag on openings (all true for now; future: false for cancellation-triggered)
- `WaitlistMatchingService` domain service
- `TeeTimeOpeningOfferPolicy` -- concurrent offer management
- `WaitlistOfferResponsePolicy` -- per-offer buffer management
- `BookingConfirmationPolicy` -- coordinates offer acceptance with slot claiming
- `TeeTimeOpeningExpirationPolicy` -- auto-expire openings
- Pending state on Bookings with `BookingConfirmed` / `BookingRejected` events
- `WakeUpOfferPolicy` command -- triggered when a golfer joins and an active opening exists

## Implementation Approach

TDD, domain-first, evolve outward:

1. Build and test each aggregate with unit tests
2. Build and test the domain service
3. Rewire/create event handlers
4. Rewire/create policies
5. Update API endpoints
6. Update frontend (if needed)
7. Delete old code, create fresh EF migration

Stop incrementally at each aggregate to verify the model makes sense. The WaitlistOffer aggregate will be revisited when we reach the offer flow -- it may change based on what we learn building the other aggregates.

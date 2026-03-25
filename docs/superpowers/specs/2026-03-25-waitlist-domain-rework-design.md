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

1. **Walk-up waitlist and TeeTime booking are separate systems.** The walk-up waitlist is a self-contained feature. The TeeTime aggregate is minimal for now and grows later.
2. **No cross-domain queries.** Domains communicate through events only. The waitlist domain maintains its own view of tee time state via the `TeeTimeOpening` entity.
3. **TeeTimeOpening is persistent.** Each opening cycle is its own record with a lifecycle (Open/Filled/Expired). Useful for analytics (fill rates, response times, filled by waitlist vs other).
4. **Entries stay as separate aggregate roots.** Conceptually the waitlist owns entries, but technically they're independent for concurrency (multiple golfers joining/leaving simultaneously).
5. **Matching logic lives in a domain service** that delegates to repository queries — no in-memory filtering of large entry lists.
6. **Offer policy manages concurrent offers** — multiple golfers offered simultaneously, throttled by remaining slots.
7. **Fresh EF migration** at the end — no incremental migration gymnastics.

## Domain Model

### TeeTime Aggregate (TeeTime Domain)

The TeeTime is a logical concept — it always exists for every interval on the tee sheet. A DB record is materialized on first booking. No creation event.

Composite key: `(CourseId, Date, Time)`

```
TeeTime (aggregate root)
  CourseId        Guid
  Date            DateOnly
  Time            TimeOnly
  Bookings        List<TeeTimeBooking>  (child collection)
  SlotsRemaining  calculated: 4 - active bookings (hardcoded foursome for now)

  Book(golferId, groupSize)
    → creates TeeTimeBooking
    → raises TeeTimeBooked { CourseId, Date, Time, GolferId, SlotsRemaining }
    → if SlotsRemaining == 0, raises TeeTimeFull { CourseId, Date, Time }

  CancelBooking(bookingId)
    → marks booking cancelled
    → raises TeeTimeBookingCancelled { CourseId, Date, Time, SlotsRemaining }
```

```
TeeTimeBooking (child entity)
  Id              Guid
  GolferId        Guid
  GroupSize       int (1-4)
  BookedAt        DateTimeOffset
  CancelledAt     DateTimeOffset?
```

**Events:**
- `TeeTimeBooked { CourseId, Date, Time, GolferId, SlotsRemaining }`
- `TeeTimeFull { CourseId, Date, Time }`
- `TeeTimeBookingCancelled { CourseId, Date, Time, SlotsRemaining }`

### CourseWaitlist Hierarchy (Waitlist Domain)

Abstract base with two concrete subtypes. The waitlist is a factory for entries but doesn't manage them after creation.

```
CourseWaitlist (abstract aggregate root)
  Id              Guid
  CourseId        Guid
  Date            DateOnly
  CreatedAt       DateTimeOffset

  Join(golfer, entryRepository, groupSize, ...) → GolferWaitlistEntry
    → raises GolferJoinedWaitlist { EntryId, CourseWaitlistId, GolferId }
```

```
WalkUpWaitlist extends CourseWaitlist
  ShortCode       string (4-digit)
  Status          Open | Closed
  OpenedAt        DateTimeOffset
  ClosedAt        DateTimeOffset?

  Open()          → raises WalkUpWaitlistOpened
  Close()         → raises WalkUpWaitlistClosed
  Reopen()        → raises WalkUpWaitlistReopened

  Join() override
    → creates WalkUpGolferWaitlistEntry
    → sets WindowStart = now, WindowEnd = now + 30min
```

```
OnlineWaitlist extends CourseWaitlist

  Join(golfer, entryRepository, groupSize, windowStart, windowEnd) override
    → creates OnlineGolferWaitlistEntry
    → sets golfer-specified time window
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

  Remove() → raises GolferRemovedFromWaitlist { EntryId, GolferId }
```

```
WalkUpGolferWaitlistEntry extends GolferWaitlistEntry
  ExtendWindow(newEnd)
    → raises WalkUpEntryWindowExtended { EntryId, NewEnd }
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

Persistent record of each opening cycle. One per tee time per opening event (not composite keyed — each cycle gets its own Id for analytics).

```
TeeTimeOpening (aggregate root)
  Id                Guid
  CourseId          Guid
  Date              DateOnly
  TeeTime           TimeOnly
  SlotsAvailable    int (what operator announced — informational)
  SlotsRemaining    int (updated from TeeTime events)
  Status            Open | Filled | Expired
  CreatedAt         DateTimeOffset
  FilledAt          DateTimeOffset?
  ExpiredAt         DateTimeOffset?

  UpdateSlots(remaining)
    → sets SlotsRemaining
    → raises TeeTimeOpeningSlotsUpdated { OpeningId, SlotsRemaining }

  Fill()
    → sets Status = Filled, FilledAt
    → raises TeeTimeOpeningFilled { OpeningId }

  Expire()
    → sets Status = Expired, ExpiredAt
    → raises TeeTimeOpeningExpired { OpeningId }
```

**Events:**
- `TeeTimeOpeningCreated { OpeningId, CourseId, Date, TeeTime, SlotsAvailable }`
- `TeeTimeOpeningSlotsUpdated { OpeningId, SlotsRemaining }`
- `TeeTimeOpeningFilled { OpeningId }`
- `TeeTimeOpeningExpired { OpeningId }`

### WaitlistOffer (Waitlist Domain — deferred)

The offer aggregate will be revisited when implementation reaches the offer flow. Current design assumptions:
- Separate aggregate root (one per golfer per opening)
- Created by the offer policy via a handler
- Has a public token for SMS links
- Statuses: Pending, Accepted, Rejected
- Events: WaitlistOfferSent, WaitlistOfferAccepted, WaitlistOfferRejected

Details TBD during implementation.

## Domain Service

### WaitlistMatchingService

Finds eligible waitlist entries for an opening. Queries at the DB level for freshness.

```
WaitlistMatchingService
  Dependencies: IGolferWaitlistEntryRepository

  FindEligibleEntries(opening: TeeTimeOpening) → List<GolferWaitlistEntry>
    → calls repository.FindEligibleEntries(opening.CourseId, opening.Date, opening.TeeTime, ...)
    → returns entries ordered by JoinedAt (FIFO)
```

**Repository query criteria:**
- Active entries (`RemovedAt IS NULL`)
- Entry's time window covers the opening's tee time (`WindowStart <= TeeTime <= WindowEnd`)
- `GroupSize <= SlotsRemaining` on the opening
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
    → set SlotsRemaining = SlotsAvailable
    → dispatch FindAndOfferEligibleGolfers

  Handle(WaitlistOfferSent)
    → increment PendingOfferCount

  Handle(WaitlistOfferAccepted)
    → decrement PendingOfferCount

  Handle(TeeTimeOpeningSlotsUpdated)
    → update SlotsRemaining
    → if PendingOfferCount < SlotsRemaining, dispatch more offers

  Handle(WaitlistOfferRejected)
    → decrement PendingOfferCount
    → if PendingOfferCount < SlotsRemaining, dispatch more offers

  Handle(WaitlistOfferStale)
    → decrement PendingOfferCount
    → if PendingOfferCount < SlotsRemaining, dispatch more offers

  Handle(TeeTimeOpeningFilled)
    → reject all pending offers, mark complete

  Handle(TeeTimeOpeningExpired)
    → reject all pending offers, mark complete
```

### WaitlistOfferResponsePolicy (per offer)

Manages the response buffer for a single offer. Buffer duration depends on golfer type.

```
State:
  Id          Guid (OfferId)
  IsWalkUp    bool

Handlers:
  Start(WaitlistOfferSent)
    → set IsWalkUp from entry type
    → schedule buffer timeout (60s for walk-up, longer for online)

  Handle(BufferTimeout)
    → raise WaitlistOfferStale { OfferId, OpeningId }

  Handle(WaitlistOfferAccepted)
    → mark complete

  Handle(WaitlistOfferRejected)
    → mark complete
```

### TeeTimeOpeningExpirationPolicy (per opening)

Auto-expires an opening when the tee time passes. Replaces current `TeeTimeRequestExpirationPolicy`.

```
State:
  Id          Guid (OpeningId)

Handlers:
  Start(TeeTimeOpeningCreated)
    → calculate delay until tee time
    → schedule expiration timeout

  Handle(ExpirationTimeout)
    → call opening.Expire()
    → mark complete

  Handle(TeeTimeOpeningFilled)
    → mark complete (no need to expire)
```

## Event Flow — Walk-Up Phase

### Happy Path: 3-Slot Opening

```
1. Operator opens WalkUpWaitlist for today
   → WalkUpWaitlistOpened

2. Golfers join via QR/walk-up
   → GolferJoinedWaitlist (x3)
   → Each entry gets 30-min window from join time

3. Operator creates TeeTimeOpening (3 slots at 10:30)
   → TeeTimeOpeningCreated
   → TeeTimeOpeningOfferPolicy starts (SlotsRemaining=3, Pending=0)
   → TeeTimeOpeningExpirationPolicy starts
   → WaitlistMatchingService finds 3 eligible entries
   → 3 WaitlistOffers created and sent
   → 3 WaitlistOfferResponsePolicies start (60s buffer each)
   → TeeTimeOpeningOfferPolicy: Pending=3, SlotsRemaining=3 — balanced

4. Golfer A accepts (group of 1)
   → WaitlistOfferAccepted
   → Handler: find-or-create TeeTime(CourseId, Date, 10:30)
   → TeeTime.Book(golferA, 1) → TeeTimeBooked { SlotsRemaining: 3 }
   → Handler: find active opening, call UpdateSlots(3)
   → TeeTimeOpeningSlotsUpdated
   → OfferPolicy: SlotsRemaining=3, Pending=2 — offer 1 more

5. Golfer B's 60s buffer expires
   → BufferTimeout → WaitlistOfferStale
   → OfferPolicy: Pending drops to 2, SlotsRemaining=3 — offer 1 more

6. Golfer C accepts (group of 2)
   → TeeTime.Book(golferC, 2) → TeeTimeBooked { SlotsRemaining: 1 }
   → Opening.UpdateSlots(1)
   → OfferPolicy adjusts: SlotsRemaining=1

7. Golfer D accepts (group of 1)
   → TeeTime.Book(golferD, 1) → TeeTimeBooked { SlotsRemaining: 0 }
   → TeeTimeFull
   → Handler: opening.Fill() → TeeTimeOpeningFilled
   → OfferPolicy: reject remaining pending offers, complete
   → ExpirationPolicy: complete
```

### Edge Case: Opening Created, No Waitlist Entries

```
1. Operator creates TeeTimeOpening
   → TeeTimeOpeningCreated
   → OfferPolicy starts, finds no eligible entries — sits idle
   → ExpirationPolicy starts timer

2. Golfer joins waitlist later
   → GolferJoinedWaitlist
   → Handler checks for active openings matching this golfer's window
   → If match found, notifies OfferPolicy to try again
```

### Edge Case: Golfer Joins, Opening Already Exists

```
1. TeeTimeOpening exists, OfferPolicy is idle (no eligible entries)
2. Golfer joins waitlist
   → GolferJoinedWaitlist
   → Handler: find active openings for this course+date where tee time is in golfer's window
   → Dispatch event/command to wake up the OfferPolicy
```

## Cross-Domain Event Flow

```
TeeTime Domain                    Waitlist Domain
──────────────                    ───────────────

TeeTimeBooked ──────────────────→ Handler: find active opening by (CourseId, Date, Time)
                                  → opening.UpdateSlots(slotsRemaining)
                                  → raises TeeTimeOpeningSlotsUpdated

TeeTimeFull ────────────────────→ Handler: find active opening by (CourseId, Date, Time)
                                  → opening.Fill()
                                  → raises TeeTimeOpeningFilled

TeeTimeBookingCancelled ────────→ Handler: find active opening or create new one
                                  → opening.UpdateSlots(slotsRemaining)
                                  → raises TeeTimeOpeningSlotsUpdated
                                  (future: may create new TeeTimeOpening)
```

## What's Removed

- `TeeTimeRequest` aggregate — replaced by `TeeTimeOpening`
- `TeeTimeSlotFill` entity — bookings now live on `TeeTime` aggregate
- `TeeTimeRequestService` domain service — replaced by `WaitlistMatchingService`
- `IsReady` field on entries — speculative, always true
- `TeeTimeRequestAdded`, `TeeTimeRequestFulfilled`, `TeeTimeRequestClosed` events — replaced by opening events
- `TeeTimeOfferPolicy` — replaced by `TeeTimeOpeningOfferPolicy` + `WaitlistOfferResponsePolicy`
- `TeeTimeRequestExpirationPolicy` — replaced by `TeeTimeOpeningExpirationPolicy`
- `NotifyNextEligibleGolferHandler` — replaced by handler using `WaitlistMatchingService`
- All existing EF migrations — fresh migration created at the end

## What's New

- `TeeTime` aggregate with `TeeTimeBooking` children (TeeTime domain)
- `CourseWaitlist` abstract base with `WalkUpWaitlist` and `OnlineWaitlist` subtypes
- `GolferWaitlistEntry` hierarchy with `WalkUpGolferWaitlistEntry` and `OnlineGolferWaitlistEntry`
- Time window on all entries (`WindowStart`, `WindowEnd`)
- `TeeTimeOpening` aggregate in the waitlist domain
- `WaitlistMatchingService` domain service
- `TeeTimeOpeningOfferPolicy` — concurrent offer management
- `WaitlistOfferResponsePolicy` — per-offer buffer management
- `TeeTimeOpeningExpirationPolicy` — auto-expire openings
- Cross-domain event handlers (TeeTime events → Opening updates)

## Implementation Approach

TDD, domain-first, evolve outward:

1. Build and test each aggregate with unit tests
2. Build and test the domain service
3. Rewire/create event handlers
4. Rewire/create policies
5. Update API endpoints
6. Update frontend (if needed)
7. Delete old code, create fresh EF migration

Stop incrementally at each aggregate to verify the model makes sense. The WaitlistOffer aggregate will be revisited when we reach the offer flow — it may change based on what we learn building the other aggregates.

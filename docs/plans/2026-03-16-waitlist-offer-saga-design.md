# Waitlist Offer Saga Design

**Goal:** Redesign the waitlist offer acceptance flow using decoupled event-driven saga pattern. Remove duplicated data from WaitlistOffer, make GolferWaitlistEntry its own aggregate, promote Booking to a domain aggregate, and add a child collection to TeeTimeRequest for tracking slot fills.

**Architecture:** Each step in the acceptance flow is a single-aggregate transaction that raises an event. Handlers react to events and perform the next step. Compensation flows handle failures by walking backward. Events carry identifiers only — handlers look up the data they need.

---

## Aggregate Changes

### WaitlistOffer (lean)

**Keeps:** `Token`, `TeeTimeRequestId`, `GolferWaitlistEntryId`, `BookingId` (pre-allocated), `Status`, `CreatedAt`

**Removes:** `CourseId`, `CourseName`, `Date`, `TeeTime`, `GolfersNeeded`, `GolferName`, `GolferPhone`, `ExpiresAt`

**No expiration.** Offers stay `Pending` until `Accepted` or `Rejected`.

**Status enum:** `Pending`, `Accepted`, `Rejected`

**Methods:**
- `Create(...)` — generates `Token`, `BookingId` (both `Guid.CreateVersion7()`)
- `Accept(golfer)` — validates offer is Pending, validates golfer matches the offer. Sets status to `Accepted`. Raises `WaitlistOfferAccepted`.
- `Reject(reason)` — sets status to `Rejected`. Raises `WaitlistOfferRejected`.

**Rejection is a successful domain outcome**, not an exception. No exception leaves `Accept()` or `Reject()`.

### TeeTimeRequest (gains child collection)

**New child entity: `TeeTimeSlotFill`**
- `GolferId`, `BookingId`, `GroupSize`, `FilledAt`
- Tracks who filled each slot and which booking it corresponds to

**New internal method: `Fill(golferId, groupSize, bookingId)`**
- Returns `FillResult(bool Success, string? RejectionReason)`
- Validates:
  - Request is still `Pending` (not `Fulfilled`)
  - `groupSize <= remainingSlots`
- Adds `TeeTimeSlotFill` child record
- Marks `Fulfilled` when `slotsFilled >= GolfersNeeded`
- Raises `TeeTimeRequestFulfilled` event when fulfilled (expires remaining pending offers)

**New internal method: `Unfill(bookingId)`**
- Removes the `TeeTimeSlotFill` by `BookingId`
- Sets status back to `Pending` if it was `Fulfilled`
- Used for compensation when booking is cancelled

### GolferWaitlistEntry (becomes its own aggregate root)

- Owns its own lifecycle (active -> removed)
- Referenced by ID from WaitlistOffer
- `WalkUpWaitlist.AddGolfer(golfer, groupSize)` acts as factory — validates (waitlist open, no duplicate golfer) and returns a new `GolferWaitlistEntry`
- `WalkUpWaitlist` drops its `List<GolferWaitlistEntry>` collection
- `Remove()` stays public

### WalkUpWaitlist (drops child collection)

- No longer owns `List<GolferWaitlistEntry>`
- `AddGolfer(golfer, groupSize)` validates creation rules and returns new `GolferWaitlistEntry` (factory pattern)
- Duplicate detection moves to repository check (since entries aren't in the collection anymore)

### Booking (new domain aggregate)

- Promoted from anemic `Api/Models/Booking` to proper domain aggregate
- `Create(bookingId, golferId, courseId, date, teeTime, groupSize)` factory method
- `BookingId` is passed in (pre-allocated by WaitlistOffer), not generated
- Raises `BookingCreated` event
- Future: `Cancel()` method raises `BookingCancelled` event

---

## Event Chain (Identifiers Only)

All events carry identifiers only. Handlers look up the data they need.

### Happy Path

```
Golfer claims offer
  |
  v
offer.Accept(golfer)
  Status = Accepted
  Raises: WaitlistOfferAccepted { OfferId, BookingId, TeeTimeRequestId, GolferWaitlistEntryId, GolferId }
  SMS: "We're processing your request - you'll receive a confirmation shortly."
  |
  v
Handler: Fill TeeTimeRequest
  request.Fill(golferId, groupSize, bookingId)
  Raises: TeeTimeSlotFilled { TeeTimeRequestId, BookingId, GolferId }
  |
  v
Handler: Create Booking
  Booking.Create(bookingId, golferId, courseId, date, teeTime, groupSize)
  Raises: BookingCreated { BookingId, GolferId, CourseId }
  |
  +---> Handler: Remove from waitlist
  |      entry.Remove()
  |      Raises: GolferRemovedFromWaitlist { GolferWaitlistEntryId, GolferId }
  |
  +---> Handler: Send confirmation SMS
         Looks up golfer phone, course name, tee time from DB
         SMS: "You're booked! [Course] at [Time] on [Date]. See you on the course!"
```

### Compensation: Fill Fails

```
Handler: Fill TeeTimeRequest
  request.Fill(...) returns failure (already fulfilled, group too large)
  Raises: TeeTimeSlotFillFailed { TeeTimeRequestId, OfferId, Reason }
  |
  v
Handler: Reject offer
  offer.Reject(reason)
  Raises: WaitlistOfferRejected { OfferId, GolferWaitlistEntryId, Reason }
  |
  +---> Handler: Send rejection SMS (only if golfer still on waitlist)
  |      SMS: "Sorry, that tee time is no longer available."
  |
  +---> Handler: Send next offer after 60s buffer
         Find next eligible golfer on waitlist, create new WaitlistOffer
```

### Compensation: Booking Cancelled (future)

```
BookingCancelled { BookingId }
  |
  v
Handler: Unfill TeeTimeRequest
  Find request by BookingId -> request.Unfill(bookingId)
  Request goes back to Pending with available slots
  |
  v
Handler: Send next offer for newly available slots
```

### TeeTimeRequest Fulfilled

```
TeeTimeRequestFulfilled { TeeTimeRequestId }
  |
  v
Handler: Expire remaining pending offers for this request
  Load all pending offers for TeeTimeRequestId
  For each: offer.Reject("Tee time has been filled.")
  (Each rejection triggers its own WaitlistOfferRejected chain)
```

---

## Initial Offer Creation

When operator creates a TeeTimeRequest:

1. `TeeTimeRequestAdded` event fires
2. Handler finds eligible golfers on the waitlist:
   - Active (`RemovedAt is null`), walk-up, ready
   - **Smart matching**: skip entries whose `GroupSize > remainingSlots`
   - Ordered by `JoinedAt`
3. Creates **one offer per slot needed** — enough offers to fill the request if everyone accepts
4. Each offer sent via SMS with claim link

On rejection of any offer:
- 60-second system default buffer
- Then offer to next eligible golfer (smart matching still applies)

---

## Read Models (CQRS-lite)

Domain models are pure write/behavior. Read concerns use separate classes.

The `GET /waitlist/offers/{token}` endpoint needs course name, tee time, golfer name for display. This data is NOT on the WaitlistOffer domain entity. A read model (e.g., `WaitlistOfferReadModel`) joins WaitlistOffer + TeeTimeRequest + Golfer + Course at query time.

Read models live in the API layer (e.g., `Infrastructure/ReadModels/` or inline in endpoint files), not in the Domain.

---

## Summary of Domain Events

| Event | Raised By | Handlers |
|---|---|---|
| `WaitlistOfferAccepted` | `WaitlistOffer.Accept()` | Fill TeeTimeRequest, Send "processing" SMS |
| `TeeTimeSlotFilled` | Fill handler | Create Booking |
| `TeeTimeSlotFillFailed` | Fill handler | Reject offer |
| `TeeTimeRequestFulfilled` | `TeeTimeRequest.Fill()` | Expire remaining offers |
| `BookingCreated` | `Booking.Create()` | Remove from waitlist, Send confirmation SMS |
| `WaitlistOfferRejected` | `WaitlistOffer.Reject()` | Send rejection SMS, Next offer after 60s |
| `BookingCancelled` | `Booking.Cancel()` (future) | Unfill TeeTimeRequest, Re-offer |
| `GolferRemovedFromWaitlist` | `GolferWaitlistEntry.Remove()` | (none currently) |

---

## Design Decisions

1. **No expiration on offers** — offers stay Pending until Accepted or Rejected
2. **Events carry identifiers only** — handlers look up data they need
3. **Rejection is a domain outcome** — not an exception, sets status and raises event
4. **One offer per slot** — smart matching skips groups that don't fit
5. **Sequential on rejection** — 60s buffer before next offer
6. **BookingId pre-allocated on offer** — flows through the entire saga for correlation
7. **WalkUpWaitlist as factory** — validates creation rules, returns independent GolferWaitlistEntry aggregate
8. **TeeTimeRequest tracks fills** — child collection with BookingId for cancellation support
9. **Read models separate from domain** — CQRS-lite, no display data on domain entities
10. **Fully decoupled steps** — each handler does one thing, raises one event

# Waitlist System Architecture

**Issue:** #180 (operator walk-up waitlist), parent #29 (walk-up waitlist family), sibling #3 (remote/SMS waitlist)
**Date:** 2026-02-25
**Status:** Draft — pending owner review

---

## 1. Overview

The waitlist system enables two core scenarios:

1. **Walk-up waitlist** (#29) — An operator sees an open tee time and broadcasts an offer to golfers who are physically on-site (e.g., at the driving range). Golfers who previously joined the waitlist and indicated they are walk-up and ready get notified via SMS. The first to accept claims the slot.

2. **Remote/SMS waitlist** (#3) — A golfer sets a waitlist alert for a time window at one or more courses. When a cancellation opens a slot, the system offers it to waitlisted golfers in FIFO order via SMS with a 15-minute acceptance window.

Both scenarios share infrastructure: a per-course daily waitlist, golfer entries on that waitlist, and a mechanism to match open slots with waiting golfers. The data model is designed to be agnostic — the same `GolferWaitlistEntry` serves both walk-up and remote golfers, distinguished by flags.

---

## 2. Data Model

This section describes the **end-goal data model** for the complete waitlist system. Not all entities are created in the same story — see Section 6 for the phased implementation scope.

### 2.1 Entity Definitions

#### CourseWaitlist

The daily waitlist container for a course. One per course per day. Created lazily when the first entry or request is added for that course-date pair.

```
CourseWaitlist
  Id              Guid, PK
  CourseId         Guid, FK -> Course, required
  Date            DateOnly, required
  CreatedAt       DateTimeOffset
  UpdatedAt       DateTimeOffset

  Unique constraint: (CourseId, Date)
  Index: (CourseId, Date)
```

**Rationale:** The owner proposed this entity and it serves an important structural role. Rather than every waitlist entry and every request independently carrying CourseId + Date, those values are normalized into a single parent row. This makes daily aggregation queries clean (count all entries for a course-day by joining to one CourseWaitlist) and provides a natural grouping for the "end of day cleanup" lifecycle. It also gives future features a place to hang daily waitlist configuration (e.g., per-day discount percentages, daily caps).

#### WaitlistRequest

An operator-initiated request to fill a specific tee time. When an operator adds a tee time to the walk-up waitlist, this is the record created. In the future, the system could also auto-create requests when cancellations occur (for the remote waitlist flow).

```
WaitlistRequest
  Id                    Guid, PK
  CourseWaitlistId      Guid, FK -> CourseWaitlist, required
  TeeTime               TimeOnly, required
  GolfersNeeded         int, required (1-4)
  Status                string, required (default: "Pending")
  CreatedAt             DateTimeOffset
  UpdatedAt             DateTimeOffset

  Index: (CourseWaitlistId, TeeTime)
  Index: (CourseWaitlistId, Status)
```

**Status values:** `Pending`, `Filled`, `Expired`, `Cancelled`

**Naming decision:** The owner proposed `CourseWalkupWaitlistRequest`. We shortened to `WaitlistRequest` because:
- The request entity is not inherently walk-up-only. A cancellation-triggered remote waitlist offer is also a "request to fill a tee time."
- The `CourseWaitlist` parent already scopes it to a course and date.
- Shorter names reduce cognitive load and are easier to work with in code.

**On the `TeeTime` field:** The owner noted this could be a `TeeTimeId` in the future if tee times become persisted entities. For now, tee times are computed from course configuration (see `TeeSheetEndpoints.cs` lines 43-58). Using `TimeOnly` matches the current `Booking.Time` convention. When/if a `TeeTime` entity is introduced, a migration can add the FK. The field name `TeeTime` (rather than `Time`) is intentionally more descriptive to distinguish from generic time fields.

**On uniqueness:** We do NOT enforce a unique constraint on `(CourseWaitlistId, TeeTime)` because an operator might cancel a request and create a new one for the same time, or there could be partial-fill scenarios where a second request is created for remaining slots. Status-based filtering handles this.

#### GolferWaitlistEntry

A golfer's presence on a course's daily waitlist. Represents "I am available to play at this course today." This is the owner's proposed entity with refinements.

```
GolferWaitlistEntry
  Id                  Guid, PK
  CourseWaitlistId    Guid, FK -> CourseWaitlist, required
  GolferId            Guid, FK -> Golfer, required (non-nullable from day one — introduced in story #31)
  GolferName          string, required (denormalized: "FirstName LastName" — survives golfer record changes)
  GolferPhone         string, required (denormalized: E.164 — used for SMS delivery without joins)
  IsWalkUp            bool, required (default: true for walk-up golfers)
  IsReady             bool, required (default: true)
  JoinedAt            DateTimeOffset, required
  RemovedAt           DateTimeOffset, nullable
  CreatedAt           DateTimeOffset
  UpdatedAt           DateTimeOffset

  Index: (CourseWaitlistId, IsWalkUp, IsReady)
  Index: (CourseWaitlistId, GolferPhone)
  Index: (CourseWaitlistId, GolferId)
```

**Key design decisions:**

1. **GolferId FK is required from day one.** The `Golfer` entity was introduced in story #31 (golfer joins walk-up waitlist) alongside `GolferWaitlistEntry`. `GolferId` is non-nullable — every waitlist entry belongs to a `Golfer` record, created via the lookup-or-create pattern (normalize phone to E.164, query by phone, create if not found). `GolferName` and `GolferPhone` remain as **denormalized copies** — they ensure SMS delivery and operator display never require a join to the `Golfer` table, and they provide resilience if a golfer record is modified.

3. **Composite key vs surrogate key.** The owner proposed `(golferId, courseWaitlistId)` as a composite primary key. Because a golfer could theoretically leave and rejoin the waitlist on the same day (with different time windows), we use a surrogate `Id` (Guid) as PK. A unique constraint on `(CourseWaitlistId, GolferPhone)` prevents duplicate active entries, enforced at the application level by checking `RemovedAt IS NULL`. Once `GolferId` is available, the uniqueness check shifts to `(CourseWaitlistId, GolferId)`.

4. **WaitingFrom / WaitingUntil vs time window.** These fields define the golfer's availability window. For walk-up golfers, `WaitingFrom` is "now" and `WaitingUntil` might be null (they will stay as long as needed). For remote waitlist golfers, these define their preferred time window (e.g., "I want to play between 8am and 10am"). The query to match golfers to a request filters: `WaitingFrom <= request.TeeTime AND (WaitingUntil IS NULL OR WaitingUntil >= request.TeeTime)`.

5. **IsReady flag.** The owner included this for the walk-up scenario — a golfer might join the waitlist but then step away (eating lunch, in the middle of a lesson). `IsReady = false` means "don't notify me right now." Defaults to `true`.

6. **RemovedAt instead of deletion.** Soft-delete via `RemovedAt` timestamp allows audit trail and prevents re-notification of golfers who already left the waitlist. Active entries are those where `RemovedAt IS NULL`.

7. **Renamed from owner's proposal.** `waitingTill` becomes `WaitingUntil` for consistency with .NET naming conventions (PascalCase) and clarity (`Until` is more standard than `Till`).

#### WaitlistRequestAcceptance

Tracks which golfer accepted a specific waitlist request. Replaces the owner's proposed `CourseWalkupWaitlistRequestGolfer`.

```
WaitlistRequestAcceptance
  Id                      Guid, PK
  WaitlistRequestId       Guid, FK -> WaitlistRequest, required
  GolferWaitlistEntryId   Guid, FK -> GolferWaitlistEntry, required
  AcceptedAt              DateTimeOffset, required
  CreatedAt               DateTimeOffset

  Unique constraint: (WaitlistRequestId, GolferWaitlistEntryId)
```

**Why a separate entity instead of a counter?** The previous architect review proposed `GolfersAccepted` as an integer counter on the request. The owner's model correctly identifies that we need to track *which* golfers accepted, not just a count. This is essential for:
- Preventing duplicate acceptances
- Creating the booking record for the correct golfer
- Removing the golfer from other waitlist entries after acceptance
- Audit trail

**Naming decision:** `WaitlistRequestAcceptance` rather than `CourseWalkupWaitlistRequestGolfer` because:
- It describes the *action* (an acceptance), not just a junction
- It is not walk-up-specific
- It is clearer about what the record represents

### 2.2 Modified Entities

#### Course

Add a waitlist feature flag:

```
Course (existing)
  + WaitlistEnabled    bool?, nullable (null = not configured, same pattern as TeeTimeIntervalMinutes)
```

This follows the established pattern where nullable means "not yet configured" (see `TeeTimeIntervalMinutes`, `FlatRatePrice`).

### 2.3 Entity Relationship Diagram

```
Tenant
  └── Course
        ├── Booking (existing)
        └── CourseWaitlist (one per date)
              ├── GolferWaitlistEntry (many per waitlist)
              │     ├── Golfer (FK — required, introduced in #31)
              │     └── WaitlistRequestAcceptance (many — one per accepted request)
              └── WaitlistRequest (many per waitlist, one per tee time request)
                    └── WaitlistRequestAcceptance (many — one per accepting golfer)
```

`WaitlistRequestAcceptance` is a junction between `WaitlistRequest` and `GolferWaitlistEntry`.

### 2.4 Future Entities and Migrations

- **Golfer entity** — Created in story #31 (golfer joins walk-up waitlist). `GolferWaitlistEntry.GolferId` FK is required from day one. `Booking` will gain a `GolferId` FK in a future migration. Phone numbers are normalized to E.164 via `PhoneNormalizer`.
- **TeeTime entity** — When tee times are persisted (not computed), `WaitlistRequest.TeeTime` (TimeOnly) gains a parallel `TeeTimeId` FK.
- **Waitlist configuration** — Per-course settings like acceptance window duration (default 15 min), max waitlist size, walk-up discount percentage. These belong on a `WaitlistSettings` entity or on `Course` directly. Deferred to #175.
- **WaitlistOffer** — Tracks individual SMS offers sent to golfers (offered at, expires at, response). Needed for the 15-minute rolling offer flow in #3. Not needed for #180.

### 2.5 Phased Table Creation

The data model above is the end-goal. Tables are created incrementally as stories require them:

| Story | Tables Created | Fields Added |
|-------|---------------|--------------|
| #180 (operator waitlist trigger) | `CourseWaitlists`, `WaitlistRequests` | `Course.WaitlistEnabled` |
| #31 (golfer joins walk-up waitlist) | `Golfers`, `GolferWaitlistEntries` | — |
| Golfer accepts offer (future) | `WaitlistRequestAcceptances` | — |

---

## 3. Event System Abstraction

The owner correctly identified that this system needs to be "highly resilient, event based, and atomic." The waitlist is a natural fit for event-driven architecture per the project's core principles.

### 3.1 Domain Events

Domain events represent things that happened in the system. They are past-tense facts.

**Proposed events for the waitlist domain:**

| Event | Published When | Subscribers |
|-------|---------------|-------------|
| `WaitlistRequestCreated` | Operator adds a tee time to the waitlist | Notification service (match & notify eligible golfers) |
| `GolferJoinedWaitlist` | A golfer joins the course waitlist | Analytics; potentially auto-match against open requests |
| `GolferLeftWaitlist` | A golfer leaves or is removed from the waitlist | Analytics; update request match counts |
| `WaitlistOfferSent` | SMS offer sent to a golfer (future, #3) | Offer timeout scheduler |
| `WaitlistOfferAccepted` | A golfer accepts a waitlist offer | Booking creation; remove golfer from other waitlists; update request status |
| `WaitlistOfferDeclined` | A golfer declines or times out | Roll to next golfer in queue |
| `WaitlistRequestFilled` | All needed golfers have accepted | Update tee sheet; analytics |
| `WaitlistRequestExpired` | Request expired without being filled | Analytics; operator notification |
| `BookingCancelled` | A booking is cancelled (future, #3) | Auto-create WaitlistRequest for the freed slot |

### 3.2 Event Infrastructure Abstraction

The owner asked for "an abstraction over the event system, that we can decide how to implement later." Here is the proposed design.

#### Interface

```csharp
// src/api/Events/IDomainEventPublisher.cs
public interface IDomainEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent;
}

// src/api/Events/IDomainEvent.cs
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}

// src/api/Events/IDomainEventHandler.cs
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}
```

#### Initial Implementation: In-Process Dispatcher

For v1, events are dispatched in-process synchronously (within the same request/transaction). This is the simplest implementation that still enforces the abstraction boundary.

```csharp
// src/api/Events/InProcessDomainEventPublisher.cs
// Resolves all registered IDomainEventHandler<TEvent> from DI
// Calls each handler sequentially
// If a handler fails, logs the error but does not fail the parent operation
//   (resilience: downstream failures don't break core flow)
```

**Registration pattern:**

```csharp
// In Program.cs or a service registration extension:
builder.Services.AddScoped<IDomainEventPublisher, InProcessDomainEventPublisher>();
builder.Services.AddScoped<IDomainEventHandler<WaitlistRequestCreated>, NotifyEligibleGolfersHandler>();
```

#### Future Evolution Path

1. **In-process (v1)** — synchronous, same transaction, good enough for MVP
2. **Background queue (v1.5)** — `Channel<IDomainEvent>` with a hosted service consumer, decouples handler execution from the request
3. **Message broker (v2+)** — Azure Service Bus or similar, enables cross-service communication and guaranteed delivery

The abstraction (`IDomainEventPublisher` / `IDomainEventHandler<T>`) remains identical across all three. Only the publisher implementation changes.

### 3.3 Atomicity Concerns

The owner flagged atomicity as critical. Key scenarios:

1. **Creating a WaitlistRequest** — Must create the `CourseWaitlist` (if not exists) and the `WaitlistRequest` in a single transaction, then publish `WaitlistRequestCreated`. With the in-process dispatcher, this is naturally atomic — `SaveChangesAsync()` commits both entities, then the event fires.

2. **Accepting an offer** — Must create `WaitlistRequestAcceptance`, update `WaitlistRequest.Status` (if all slots filled), and soft-delete the golfer's other waitlist entries. This requires a transaction scope. The in-process event model handles this naturally. With a message broker, this becomes an eventual consistency concern that needs idempotent handlers.

3. **Golfer accepts a tee time anywhere** — The owner noted: "when a golfer accepts a teetime anywhere, all their waitlist entries for that day need to be removed." This is a cross-aggregate operation. The `WaitlistOfferAccepted` event handler queries all `GolferWaitlistEntry` rows for that golfer (by `GolferId` or by phone if `GolferId` is not yet available) on that date across all courses and soft-deletes them. With in-process events, this can be within the same transaction. With a broker, this is eventually consistent (acceptable — the worst case is a golfer gets an SMS for a slot they already filled, which they can ignore).

---

## 4. API Design Direction

### 4.1 Endpoints for Issue #180 (Operator Walk-Up Waitlist)

These are the endpoints needed for the operator-facing story:

```
GET  /courses/{courseId}/waitlist?date=yyyy-MM-dd
  -> Returns waitlist summary and all requests for the given date
  -> Response: { courseWaitlistId, date, totalGolfersOnWaitlist, requests: [...] }
  -> 200 OK, 404 if course not found, 400 if waitlist not enabled

POST /courses/{courseId}/waitlist/requests
  -> Operator adds a tee time to the waitlist
  -> Body: { teeTime: "HH:mm", golfersNeeded: 1-4 }
  -> Creates CourseWaitlist if not exists, then WaitlistRequest
  -> Publishes WaitlistRequestCreated event
  -> 201 Created, 400 validation errors, 404 course not found, 409 if active request exists for that time

GET  /courses/{courseId}/waitlist-settings
  -> Returns { waitlistEnabled: bool }
  -> 200 OK

PUT  /courses/{courseId}/waitlist-settings
  -> Body: { waitlistEnabled: bool }
  -> 200 OK
```

### 4.2 Future Endpoints (Not in #180 Scope)

```
POST /courses/{courseId}/waitlist/join
  -> Golfer joins the waitlist (golfer-facing, future story)
  -> Body: { golferName, golferPhone, waitingFrom, waitingUntil?, isWalkUp }
  -> Once Golfer entity exists: authenticated, GolferId derived from session

DELETE /courses/{courseId}/waitlist/entries/{entryId}
  -> Golfer or operator removes an entry

POST /courses/{courseId}/waitlist/requests/{requestId}/accept
  -> Golfer accepts a waitlist offer (SMS-triggered, future story)

GET  /courses/{courseId}/waitlist/entries
  -> Operator views all golfers on the waitlist for a date
```

---

## 5. Scenario Walkthroughs

### 5.1 Walk-Up Waitlist (Issue #180 + Future Golfer Story)

1. **Operator enables waitlist** — `PUT /courses/{id}/waitlist-settings { waitlistEnabled: true }`
2. **Golfer arrives at course, joins walk-up waitlist** — (future story) Creates `GolferWaitlistEntry` with `IsWalkUp = true`, `IsReady = true`, `GolferId` set if golfer has an account
3. **Operator sees open 9:20 AM slot** — `POST /courses/{id}/waitlist/requests { teeTime: "09:20", golfersNeeded: 2 }`
4. **System creates** `CourseWaitlist` (if needed) + `WaitlistRequest` (status: Pending)
5. **`WaitlistRequestCreated` event fires** — Handler queries `GolferWaitlistEntry` where `IsWalkUp = true AND IsReady = true AND WaitingFrom <= 09:20 AND (WaitingUntil IS NULL OR WaitingUntil >= 09:20)`, ordered by `JoinedAt` ASC (FIFO)
6. **SMS sent to eligible golfers** — (future story) "9:20 AM at Pine Valley just opened up. Reply Y to claim. 1 other waiting - you have 15 minutes."
7. **Golfer replies Y** — Creates `WaitlistRequestAcceptance`, publishes `WaitlistOfferAccepted`
8. **Handler for WaitlistOfferAccepted** — If `GolfersNeeded` acceptances reached, update `WaitlistRequest.Status = "Filled"`, soft-delete golfer's other entries for the day

### 5.2 Remote/SMS Waitlist (Issue #3 — Future)

1. **Golfer browses and sees a full tee sheet for Saturday 8-10 AM**
2. **Golfer joins waitlist** — Creates `GolferWaitlistEntry` with `IsWalkUp = false`, `WaitingFrom = 08:00`, `WaitingUntil = 10:00`, `GolferId` set from authenticated session
3. **Another golfer cancels their 8:40 AM booking** — `BookingCancelled` event fires
4. **Handler creates** `WaitlistRequest` for the freed 8:40 slot, `GolfersNeeded` based on cancelled booking's player count
5. **`WaitlistRequestCreated` event fires** — Same handler as walk-up, but now matches remote golfers (the `IsWalkUp` flag is not used for filtering which golfers to notify — both walk-up and remote golfers in the matching time window are eligible)
6. **SMS offer cascade** — First in FIFO order, 15-minute window, rolls to next on timeout/decline

### 5.3 How IsWalkUp is Used

The `IsWalkUp` flag on `GolferWaitlistEntry` is primarily for:
- **Operator visibility** — "How many walk-up golfers do I have vs remote?"
- **Analytics** — Track walk-up conversion rates separately from remote
- **Potential future differentiation** — Walk-up golfers might get different discount pricing or priority for same-day slots

It is NOT used to restrict which golfers are offered a slot. Both walk-up and remote golfers in the matching time window are eligible for any request.

---

## 6. Scope for Issue #180

Issue #180 is scoped to the **operator view only**. Based on the acceptance criteria:

### What Gets Built

- `CourseWaitlist` entity and EF configuration
- `WaitlistRequest` entity and EF configuration
- `Course.WaitlistEnabled` field + migration
- `GET /courses/{courseId}/waitlist?date=` endpoint
- `POST /courses/{courseId}/waitlist/requests` endpoint
- `GET /courses/{courseId}/waitlist-settings` endpoint
- `PUT /courses/{courseId}/waitlist-settings` endpoint
- `IDomainEvent`, `IDomainEventPublisher`, `IDomainEventHandler<T>` interfaces
- `InProcessDomainEventPublisher` implementation
- `WaitlistRequestCreated` event (published but no handler yet — no golfer entries exist to notify)
- Frontend: Waitlist page, nav item, add-to-waitlist form, entries table, summary stat
- Tests for all endpoints

### What Does NOT Get Built (Future Stories)

- `GolferWaitlistEntry` table (created when the golfer join-waitlist story is built)
- `WaitlistRequestAcceptance` table (created when the golfer acceptance story is built)
- `Golfer` table (created when the golfer account/sign-up story is built)
- SMS notification handlers
- Golfer-facing endpoints
- Offer timeout/cascade logic
- Cross-waitlist cleanup on acceptance

This is intentional — the operator can create waitlist requests and see them in the UI. The "N pending" status reflects that no golfer has accepted yet (because the golfer-side is not built). When the golfer stories are implemented, the same data model supports the full flow without schema changes to the tables created in #180.

---

## 7. Risks and Concerns

### 7.1 Golfer Entity Timing

The `Golfer` entity was introduced in story #31 (golfer joins walk-up waitlist). `GolferWaitlistEntry.GolferId` is required (non-nullable) from the first migration — no interim nullable period was needed because both entities were created together. Phone numbers are normalized to E.164 using the `PhoneNormalizer` utility. The lookup-or-create pattern handles concurrent join requests via a try/catch on the unique index violation. When a future `Booking.GolferId` FK is needed, a migration can add it to the existing `Booking` entity using phone-based matching against the `Golfers` table.

### 7.2 Tee Time Validation

Tee times are computed, not persisted. When an operator creates a `WaitlistRequest` for "09:20", the API must validate that 09:20 is a valid slot given the course's `FirstTeeTime`, `LastTeeTime`, and `TeeTimeIntervalMinutes`. The slot-generation logic in `TeeSheetEndpoints.cs` (lines 43-58) should be extracted into a shared utility (`TeeTimeSlotGenerator` or similar) to avoid duplication.

### 7.3 Concurrency

Two operators adding a request for the same tee time simultaneously: the lack of a unique constraint on `(CourseWaitlistId, TeeTime)` means both could succeed. This is by design (see section 2.1), but the `POST` endpoint should check for existing active (non-cancelled, non-expired) requests and return 409 Conflict if found. This is an application-level check, not a DB constraint, so there is a race condition window. For v1, this is acceptable — walk-up waitlist operations are low-frequency and typically done by a single operator at a time. If needed later, an optimistic concurrency check with `rowversion`/`xmin` can be added.

### 7.4 CourseWaitlist Lazy Creation

The `CourseWaitlist` row is created on-demand when the first request or entry is added for a course-date. This means the `POST /waitlist/requests` endpoint must do an upsert: find-or-create the `CourseWaitlist`, then create the `WaitlistRequest`. EF Core handles this cleanly in a single `SaveChangesAsync()` call.

### 7.5 Event Handler Failure Isolation

Per project principles, "If a downstream system is slow or down, the core flow still completes." The in-process event publisher must catch and log handler exceptions without failing the parent operation. This means: `SaveChangesAsync()` succeeds (data is persisted), then events fire, and if a handler fails, the request still returns success. The handler failure is logged for investigation.

### 7.6 Multi-Tenancy

`CourseWaitlist` has a `CourseId` FK. The existing global query filter on `Course` (scoped by `TenantId`) means that any query joining through `Course` is automatically tenant-scoped. Direct queries on `CourseWaitlist` should join to `Course` or include a `CourseId` filter derived from a validated course lookup to maintain isolation.

---

## 8. Open Questions

1. **Should `WaitlistRequest` track who created it?** For audit purposes, should it have a `CreatedByOperator` field? The current system has no operator identity beyond the tenant claim. Deferring until operator authentication is built.

2. **Can an operator cancel a waitlist request?** The acceptance criteria for #180 don't mention cancellation, but it seems like a natural need. If so, `DELETE` or `PATCH` on `/waitlist/requests/{id}` setting status to `Cancelled`. Recommend deferring to a follow-up story.

3. **What happens at end of day?** Should `WaitlistRequest` entries with status `Pending` be automatically expired? This implies a background job or a status check on read (lazy expiration). Recommend lazy expiration: when querying, any request for a past date is treated as expired.

4. **Walk-up discount percentage** — The parent issue #29 mentions "course-set discounts." Where does the discount percentage live? Options: on `WaitlistRequest` (per-request), on `CourseWaitlist` (per-day), or on a `WaitlistSettings` entity (course-wide default). This is out of scope for #180 but worth noting.

5. **Should `GolferWaitlistEntry` allow the same phone number across multiple courses on the same day?** Yes — a golfer might join the waitlist at multiple nearby courses. The unique constraint is per-course-waitlist, not global.

---

## 9. Migration Plan

### Migration for #180: `AddWaitlistEntities`

- `CourseWaitlists` table
- `WaitlistRequests` table
- `WaitlistEnabled` column on `Courses`

### Future Migrations (Not in #180)

- `AddGolferAndWaitlistEntries` — Creates `Golfers` table and `GolferWaitlistEntries` table with required `GolferId` FK. Shipped with story #31.
- `AddWaitlistRequestAcceptances` — Creates `WaitlistRequestAcceptances` table. Ships with the golfer acceptance story.
- Add `GolferId` FK to `Booking` — Future migration when golfer-authenticated booking is built.

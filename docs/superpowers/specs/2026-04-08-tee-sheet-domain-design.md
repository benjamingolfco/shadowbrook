# Tee Sheet as a First-Class Domain — Introducing `TeeSheet` and `TeeTime` Aggregates

**Date:** 2026-04-08
**Branch:** `chore/teetime-ddd-exploration`
**Status:** Draft design — spec 1 of an anticipated multi-spec rollout.

## Why this exists

Today, "a tee time" has no single owner in the domain.

- **`Course`** holds the schedule shape (`TeeTimeIntervalMinutes`, `FirstTeeTime`, `LastTeeTime`) as nullable fields. This is the only place interval knowledge lives.
- **`Booking`** is a golfer + a `TeeTime` value object (date + time) + a player count. It has **no reference to a slot**. Two bookings for the same course, date, and time can coexist; nothing in the aggregate prevents it. Capacity for direct bookings is enforced nowhere.
- **`TeeTimeOpening`** is the only thing in the domain today that enforces slot capacity (`SlotsAvailable` / `SlotsRemaining` + `ClaimedSlots`), but it only exists for the walk-up/waitlist flow. The "normal" tee sheet never creates one.
- **`TeeSheetEndpoints.GetTeeSheet`** generates slots on the fly by iterating `Course.FirstTeeTime` in `Interval` steps and left-joining bookings by wall-clock time match. No persistent slot entity is ever touched.

The consequences of this pile up as we try to grow the operator app:

1. **No home for per-slot operator control.** Blocking a slot for maintenance, frost delay, a shotgun event, or a per-day schedule override is impossible because the slot has no persistent identity.
2. **Historical inaccuracy.** Changing `Course.TeeTimeIntervalMinutes` rewrites every past tee sheet's rendering. Yesterday's 10-minute interval becomes today's 8-minute interval retroactively.
3. **Two capacity universes.** Walk-up bookings enforce capacity via `TeeTimeOpening`. Direct bookings enforce none. `Booking` and `TeeTimeOpening` reference each other only loosely via a `BookingId` correlation and can drift.
4. **Walk-up and direct flows share nothing.** The ubiquitous language pretends they are the same thing ("a tee time at 9am") but the code treats them as unrelated.

This spec introduces persistent slot identity as the structural foundation for everything else — operator block/override features, richer slot semantics (9/18, front/back, variable capacity), consolidated capacity accounting, and eventual retirement of `TeeTimeOpening`.

## Scope

### In scope

1. New `TeeSheet` aggregate (per course per day), owning a schedule snapshot (via a `ScheduleSettings` value object) and a collection of `TeeSheetInterval` child entities with stable IDs. `TeeSheet.Draft()` is the factory; `TeeSheet.Publish()` transitions from Draft to Published.
2. New `TeeTime` aggregate — per-interval, lazily materialized. Holds capacity state, claims, and status. Carries denormalized `Date` and `Time`.
3. New `TeeTimeClaim` child entity (inside `TeeTime`). Replaces `ClaimedSlot`'s responsibilities.
4. **Rename** the existing `TeeTime` value object to `BookingDateTime` (or similar) as a prep step, so the `TeeTime` name can belong to the new aggregate. The VO is deleted in the follow-on spec, not this one.
5. New `BookingAuthorization` capability token value object. `TeeSheet.AuthorizeBooking()` mints one after validating the sheet is Published. `TeeTime.Claim(...)` requires one and validates it references the same sheet. This pushes the "sheet is claimable" invariant from convention into the type system.
6. New `ScheduleSettings` value object (`FirstTeeTime`, `LastTeeTime`, `IntervalMinutes`, `DefaultCapacity`). `TeeSheet.Draft()` takes it; `Course` grows a `CurrentScheduleDefaults()` method that produces it from its stored fields. `Course` gains `DefaultCapacity` (new field, default 4).
7. Migrate the **direct-booking flow** to go through the new claim path. `Booking` gains a `TeeTimeId` reference.
8. Keep `Booking` minimal. Add `TeeTimeId`. No ancillary fields (carts, range, etc.) — those are future bounded contexts correlated by `BookingId`.
9. Rewrite `TeeSheetEndpoints.GetTeeSheet` as a left join of `TeeSheet.Intervals` against the sparse `TeeTime` rows (and their claims' booking projections).
10. New HTTP surface: `POST /courses/{courseId}/tee-sheets/draft`, `POST /courses/{courseId}/tee-sheets/{date}/publish`, `POST /courses/{courseId}/tee-times/book`.

### Explicitly out of scope (deferred to follow-on specs)

- **Walk-up/waitlist migration.** `TeeTimeOpening`, `WaitlistOffer.TeeTimeOpeningId`, all `Features/Waitlist/Handlers/TeeTimeOpening*/` handlers, and the offer/expiration policies continue to work as-is against the old aggregate. A follow-on spec will fold these into `TeeTime`.
- **Operator-facing UI for blocks, frost delays, shotgun events, per-day schedule overrides, day close.** The domain will support them; the UI stories come later as separate issues.
- **Ancillaries** — carts, range bucket pre-buy, food, pro-shop items. Not on `Booking`, not on `TeeTimeClaim`. Future bounded contexts correlate by `BookingId`.
- **Richer slot semantics** — 9/18 hole, front/back, crossovers, per-slot pricing, variable capacity per interval. `TeeTime.Capacity` is a single `int` for this spec; extending it is a future design exercise.

### A note on `TeeTimeOpening`

The manual "operator posts an opening" flow is not load-bearing long term. In the full operator app, walk-up waitlist fills become automated (driven by cancellation events, no-show detection, auto-release of held capacity). The surviving shape of that concept is closer to **"fill a no-show"** than **"manually post a time for grabs."** The follow-on spec should design from that end state, not from the current `TeeTimeOpening` shape. This spec does not need to preserve any particular `TeeTimeOpening` affordance because of that.

## Domain model

### `TeeSheet` (new aggregate root)

Represents a single day's schedule at a single course. One `TeeSheet` per (`CourseId`, `Date`).

**State:**

```csharp
public class TeeSheet : Entity
{
    public Guid CourseId { get; private set; }
    public DateOnly Date { get; private set; }
    public TeeSheetStatus Status { get; private set; }   // Draft | Published

    // Schedule snapshot — copied from Course at draft time, then owned by the sheet.
    // Future per-day overrides (frost delay, shotgun events) mutate this VO, not Course.
    public ScheduleSettings Settings { get; private set; } = null!;

    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<TeeSheetInterval> intervals = [];
    public IReadOnlyList<TeeSheetInterval> Intervals => this.intervals.AsReadOnly();
}
```

`ScheduleSettings` is a value object:

```csharp
public class ScheduleSettings : IEquatable<ScheduleSettings>
{
    public TimeOnly FirstTeeTime { get; }
    public TimeOnly LastTeeTime { get; }
    public int IntervalMinutes { get; }
    public int DefaultCapacity { get; }      // usually 4 (foursome)

    public ScheduleSettings(TimeOnly firstTeeTime, TimeOnly lastTeeTime, int intervalMinutes, int defaultCapacity)
    {
        // guards: first < last, interval > 0, defaultCapacity > 0
        ...
    }
    private ScheduleSettings() { } // EF
}
```

Mapped via EF Core `ComplexProperty` per VO conventions. Future per-day operations (frost delay, shotgun events) produce a new `ScheduleSettings` and replace the property — the VO itself is immutable.

**Behavior (this spec):**

- `TeeSheet.Draft(courseId, date, settings, timeProvider)` — static factory. Produces a `Draft` sheet with a fully enumerated `Intervals` collection. Each interval gets a `Guid.CreateVersion7()` id. Raises `TeeSheetDrafted`.
- `TeeSheet.Publish(timeProvider)` — transitions `Draft → Published`. Sets `PublishedAt`. Raises `TeeSheetPublished`. Idempotent (re-calling on an already-published sheet is a no-op, per the existing codebase pattern for terminal states).
- `TeeSheet.AuthorizeBooking()` — mints a `BookingAuthorization` capability token. Throws `TeeSheetNotPublishedException` if the sheet is still a Draft. Does **not** mutate the sheet; the token is a proof-of-check, not a reservation. See the "Capability token" subsection below.

`Course.CurrentScheduleDefaults()` is a new domain method on `Course` that returns a `ScheduleSettings` VO by reading the course's current `TeeTimeIntervalMinutes`, `FirstTeeTime`, `LastTeeTime`, and `DefaultCapacity` fields. Throws `CourseScheduleNotConfiguredException` if any of them are unset. `Course.DefaultCapacity` is a new column (int, default 4) added in the same migration.

**Behavior (deferred to future specs, but the aggregate is the right home):**

- `FrostDelay(minutes)` — shifts remaining intervals forward by `minutes`, cascades denormalized times to affected `TeeTime` rows.
- `ShotgunEvent(...)` — collapses a range of intervals into a shotgun start.
- `BlockRange(start, end, reason)` — blocks a contiguous range.
- `CloseDay(reason)` — blocks the whole day.

None of these are implemented in this spec, but the aggregate boundary is chosen so they have an obvious home.

**Why a day aggregate (not a per-slot aggregate)?** Day-level operations (frost delay, shotgun events, close the day) need atomic consistency across many intervals. Keeping those operations on the sheet lets them be single-transaction, single-aggregate mutations. At the same time, the sheet does **not** own the per-interval claim state — `TeeTime` does. So the concurrency-hot path (individual bookings) never contends on the sheet.

### `TeeSheetInterval` (child entity of `TeeSheet`)

```csharp
public class TeeSheetInterval : Entity
{
    public Guid TeeSheetId { get; private set; }
    public TimeOnly Time { get; private set; }
    public int Capacity { get; private set; }           // denormalized from TeeSheet.Settings.DefaultCapacity at creation
}
```

Intervals are created with the sheet and live inside it. They are **not** a separate aggregate; they cannot be modified without going through `TeeSheet`. Their `Id` is referenced by `TeeTime` rows and is the stable handle for "the 9:00 slot on this day at this course." Ordering within a sheet is always `ORDER BY Time` — no separate ordinal column is needed.

`Capacity` on the interval overrides `ScheduleSettings.DefaultCapacity` in the future when we introduce per-slot capacity (twosome-only slots, etc.). For this spec, it is seeded from the default and never changed.

### `TeeTime` (new aggregate root)

Lazily materialized per interval. A row exists for an interval only when something non-default has happened to it: a claim was made, a block was placed, or (future) a non-default capacity was set. If nothing has happened, no row exists, and the interval shows empty on the tee sheet view.

**State:**

```csharp
public class TeeTime : Entity
{
    public Guid TeeSheetId { get; private set; }           // denormalized from the interval — for BookingAuthorization comparison
    public Guid TeeSheetIntervalId { get; private set; }   // the stable interval id from TeeSheet
    public Guid CourseId { get; private set; }              // denormalized for tenant scoping
    public DateOnly Date { get; private set; }              // denormalized from the interval
    public TimeOnly Time { get; private set; }              // denormalized from the interval
    public int Capacity { get; private set; }               // snapshot at materialization (= interval.Capacity)
    public int Remaining { get; private set; }
    public TeeTimeStatus Status { get; private set; }       // Open | Filled | Blocked
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<TeeTimeClaim> claims = [];
    public IReadOnlyList<TeeTimeClaim> Claims => this.claims.AsReadOnly();
}
```

**Status values (mutually exclusive):**

| Status   | Meaning                                                    | Claims allowed? |
|----------|------------------------------------------------------------|-----------------|
| `Open`   | Has remaining capacity, can accept claims.                 | Yes             |
| `Filled` | Remaining capacity is zero. All claims accounted for.      | No              |
| `Blocked`| Operator-closed (maintenance / event). Cannot hold claims. | No              |

**Transitions:**

```
           ┌─────────────┐
           │ (no row)    │
           └──────┬──────┘
                  │ Claim() / Block() factory
                  ▼
          ┌──▶┌─────────────┐
          │   │    Open     │───┐
          │   └──┬──┬───────┘   │ claim fills capacity
          │      │  │           ▼
          │      │  │     ┌─────────────┐
          │      │  └────▶│   Filled    │
          │      │        └──────┬──────┘
          │      │               │ release claim
          │      │  ◀─────────────┘
          │      ▼
          │   ┌─────────────┐
          └───│   Blocked   │
              └─────────────┘
              (reachable only from Open with zero claims)
```

**Invariants (enforced in the aggregate, each with a dedicated `DomainException` subclass):**

- A `TeeTime` cannot be materialized with a non-positive capacity.
- `Claim()` on a `Blocked` TeeTime throws.
- `Claim()` on a `Filled` TeeTime throws (`Remaining == 0`).
- `Claim()` with `groupSize > Remaining` throws.
- `Claim()` with `groupSize <= 0` throws.
- `Block()` on a TeeTime with any claims throws (caller must release first or force-cancel bookings).
- `ReleaseClaim()` with an unknown `BookingId` is a silent no-op (idempotent — the event may arrive twice).

**Behavior:**

```csharp
public static TeeTime Claim(
    TeeSheetInterval interval,       // passed in as parameter (cross-aggregate read, not held)
    BookingAuthorization auth,       // capability token — proof the sheet is claimable
    Guid bookingId,
    Guid golferId,
    int groupSize,
    ITimeProvider timeProvider)
{
    // Factory: validates the auth token targets the interval's sheet, then
    // constructs a new TeeTime for this interval in Open status and applies
    // the first claim in a single atomic operation.
    // Called when no TeeTime row yet exists for the interval.
}

public void Claim(
    BookingAuthorization auth,
    Guid bookingId,
    Guid golferId,
    int groupSize,
    ITimeProvider timeProvider)
{
    // Validates the auth token targets this TeeTime's sheet.
    // Validates status, capacity, group size. Adds a TeeTimeClaim child.
    // Decrements Remaining. If Remaining == 0, transitions to Filled.
    // Raises TeeTimeClaimed (and TeeTimeFilled if applicable).
}

public void ReleaseClaim(Guid bookingId, ITimeProvider timeProvider)
{
    // Finds the claim by BookingId. If not found, return (idempotent).
    // Removes the claim, increments Remaining.
    // If status was Filled, transitions back to Open.
    // Raises TeeTimeClaimReleased (and TeeTimeReopened if applicable).
    // No auth token required — releasing a claim is not gated by sheet state.
}

public static TeeTime Block(
    TeeSheetInterval interval,
    string reason,
    ITimeProvider timeProvider)
{
    // Factory: creates a TeeTime row in Blocked status with zero claims.
    // Used when an operator blocks an interval that had no prior activity.
    // No auth token — blocking is an operator action independent of claimability.
}

public void Block(string reason, ITimeProvider timeProvider)
{
    // Transitions an existing Open TeeTime to Blocked.
    // Throws if there are any claims (caller must release first).
    // Throws if already Filled.
}

public void Unblock(ITimeProvider timeProvider)
{
    // Blocked → Open. Idempotent if already Open.
}
```

**Guarding sheet state: the `BookingAuthorization` capability token**

The invariant "you can't claim a TeeTime against an unpublished or closed sheet" needs a home. Three options:

1. Endpoint checks sheet status, then calls `TeeTime.Claim(...)`. Relies on convention.
2. `TeeTime` stores a denormalized "sheet published" flag. Extra state; drifts if the sheet un-publishes.
3. Pass the sheet as an argument to `Claim`. Reasonable, but the contract is "caller promises they loaded a real sheet" — nothing prevents a bug where the wrong sheet (or a stale one) is passed.

Instead, the design uses a lightweight capability pattern:

```csharp
public sealed class BookingAuthorization
{
    public Guid SheetId { get; }
    internal BookingAuthorization(Guid sheetId) { SheetId = sheetId; }
}

public class TeeSheet
{
    public BookingAuthorization AuthorizeBooking()
    {
        if (Status != TeeSheetStatus.Published)
            throw new TeeSheetNotPublishedException(Id);
        return new BookingAuthorization(Id);
    }
}

public class TeeTime
{
    public void Claim(BookingAuthorization auth, Guid bookingId, Guid golferId, int groupSize, ITimeProvider timeProvider)
    {
        if (auth.SheetId != this.TeeSheetId)
            throw new BookingAuthorizationMismatchException(this.Id, auth.SheetId, this.TeeSheetId);
        // ... proceed with claim
    }
}
```

A caller cannot fabricate a `BookingAuthorization` — the constructor is `internal` (same assembly as `TeeSheet`) and the only way to obtain one is `sheet.AuthorizeBooking()`, which throws on drafts. The only way to get a valid token is to actually ask a real, published sheet. The invariant moves from "caller promises" to "type system enforces."

**How the TeeTime knows its sheet id:** The factory path receives a `TeeSheetInterval`, which carries `TeeSheetId`. The new `TeeTime` aggregate denormalizes `TeeSheetId` at materialization time so `Claim()` can compare the incoming token against its own sheet. A call site looks like:

```csharp
var sheet = await sheetRepo.GetRequiredByIntervalIdAsync(request.TeeSheetIntervalId);
var auth = sheet.AuthorizeBooking();  // throws if Draft
var interval = sheet.Intervals.Single(i => i.Id == request.TeeSheetIntervalId);
var existing = await teeTimeRepo.GetOrNullByIntervalIdAsync(interval.Id);

if (existing is null)
{
    var teeTime = TeeTime.Claim(interval, auth, request.BookingId, request.GolferId, request.GroupSize, timeProvider);
    await teeTimeRepo.AddAsync(teeTime);
}
else
{
    existing.Claim(auth, request.BookingId, request.GolferId, request.GroupSize, timeProvider);
}
```

**Constructor visibility:** `BookingAuthorization`'s constructor is `internal` within `Teeforce.Domain`. No code outside the domain assembly can fabricate one. Test code in `Teeforce.Domain.Tests` gets at it the normal way — by calling `sheet.AuthorizeBooking()` on a real `TeeSheet` — not via `InternalsVisibleTo`. This matches the backend convention: "Never use `InternalsVisibleTo` to expose domain internals for testing."

**Why this is worth ~15 lines:** It makes the bypass path literally uncompilable. A developer adding a new endpoint that claims a TeeTime cannot forget the sheet check — the method signature requires a token, and the token can only come from a real published sheet. Convention becomes compile-time enforcement.

**Why TeeTime is its own aggregate (not a child of TeeSheet):** Concurrency. Bookings happen in parallel at different times on the same day. If `TeeTime` lived inside `TeeSheet`, every booking on Saturday would contend on a single row. Making `TeeTime` its own write unit scales concurrency per-interval. The tradeoff — cross-slot invariants become sagas — is acceptable because the cross-slot operations that need atomicity (frost delay, shotgun, day close) live on `TeeSheet` and operate on intervals, not on the claim state.

**Why `TeeTime` carries denormalized `Date` and `Time`:** Every query against tee times wants the wall-clock time. Joining to `TeeSheetInterval` on every read is wasteful and couples read paths to the sheet aggregate. The denormalization is cheap — `Date`/`Time` change only under frost-delay-style operations, and those are rare and handled as a cascade from the sheet.

**Why no "Expired" status:** Under the current model, `TeeTimeOpening.Expired` means "the waitlist offer round timed out." Under the new model, `TeeTime` has no opinion about waitlist offer rounds — only `WaitlistOffer` does. When all offers for a TeeTime expire, the TeeTime stays `Open` with its remaining capacity intact. The operator (or the automated walk-up system in the follow-on spec) can do something else with it.

**Why no "Passed" status (past tee times):** Deliberately deferred. A future spec may introduce a `TeeTimePassedPolicy` that reacts to a `TeeTimeMaterialized` event and transitions passed slots to a terminal state (or emits a `TeeTimePassed` event for downstream handlers like analytics/scoring). For this spec, "you can't claim a past tee time" is enforced at the endpoint via a wall-clock guard — which has to live there anyway to handle the "5 minutes before the clock" edge case.

**Why no "being offered to waitlist" flag:** That state lives on `WaitlistOffer`, not on `TeeTime`. `TeeTime.Claim()` is the single ingress point for capacity changes; whether the claim comes from a direct booking or an accepted walk-up offer is irrelevant to the aggregate. A read-model join produces "this slot has outstanding offers" for the UI.

### `TeeTimeClaim` (child entity of `TeeTime`)

```csharp
public class TeeTimeClaim : Entity
{
    public Guid TeeTimeId { get; private set; }
    public Guid BookingId { get; private set; }          // correlation to Booking aggregate
    public Guid GolferId { get; private set; }           // denormalized for fast operator-facing queries
    public int GroupSize { get; private set; }
    public DateTimeOffset ClaimedAt { get; private set; }
}
```

`Id` follows the normal `Guid.CreateVersion7()` pattern and is **independent** of `BookingId`. Uniqueness of `(TeeTimeId, BookingId)` is enforced via a unique index. Rationale: pattern consistency with every other `Entity` in the codebase, and room for additive future changes (see next paragraph) without schema migration pain.

Deliberately thin. Ancillaries (carts, range, food, pro-shop purchases) do **not** live here — they live in future bounded contexts that correlate by `BookingId`. Keeping the claim minimal preserves the mental model "TeeTime is the capacity ledger; Booking is the commercial reservation."

**Future group support (not in scope, but designed not to block it):** A future feature where a single claim represents multiple named golfers (a foursome with four identified players, each with their own account) would add a `ClaimMember` child collection to `TeeTimeClaim` — purely additive. `GolferId` would remain as the "primary/booker" and `GroupSize` as a denormalized count. Because `TeeTimeClaim.Id` is independent of `BookingId`, this extension does not require breaking schema changes.

### `Booking` (modified existing aggregate)

Changes:

- **Add:** `TeeTimeId` — **nullable** for the duration of this spec. Direct-booked rows set it; legacy rows get it set by the data migration; new walk-up rows (which keep going through the untouched `TeeTimeOpening` path during the compatibility seam) leave it null. The follow-on spec makes it NOT NULL after the walk-up migration lands.
- **Keep:** `CourseId`, `GolferId`, `PlayerCount`, `Status`, `CreatedAt`.
- **Rename, don't delete (yet):** The existing `TeeTime` value object is renamed to `BookingDateTime` (or another name chosen during implementation) as a prep step, and stays on `Booking`. The walk-up flow still creates `Booking` rows with no `TeeTimeId` during the compatibility seam, so the booking must still carry its own date/time. The VO's normalization and comparison behavior is preserved under the new name.

The rename is a namespace/type rename only — no behavioral change. It clears the `TeeTime` name for the new aggregate so the two don't collide. The follow-on spec deletes the renamed VO along with the old `Bookings.TeeTime` columns once walk-up moves onto `TeeTime`.

`Booking.Status` keeps its current enum (`Pending | Confirmed | Rejected | Cancelled`). Lifecycle methods (`Confirm`, `Reject`, `Cancel`) are unchanged in shape. `CreateConfirmed` gains a `teeTimeId` parameter.

## Domain events

### New events

- `TeeSheetDrafted { TeeSheetId, CourseId, Date, IntervalCount }`
- `TeeSheetPublished { TeeSheetId, CourseId, Date, PublishedAt }`
- `TeeTimeClaimed { TeeTimeId, BookingId, GolferId, GroupSize, CourseId, Date, Time }`
- `TeeTimeFilled { TeeTimeId, CourseId, Date, Time }`
- `TeeTimeClaimReleased { TeeTimeId, BookingId, GolferId, GroupSize, CourseId, Date, Time }`
- `TeeTimeReopened { TeeTimeId, CourseId, Date, Time }`  (emitted when Filled → Open via release)
- `TeeTimeBlocked { TeeTimeId, CourseId, Date, Time, Reason }`
- `TeeTimeUnblocked { TeeTimeId, CourseId, Date, Time }`

`GroupSize` is deliberately included on `TeeTimeClaimReleased` because a future waitlist offer policy (outside this spec's scope) will correlate `BookingCancelled` and `TeeTimeClaimReleased` to decide how many slots just freed up and kick off offers. The policy needs the number of slots to act on, and we don't want to force it to round-trip to the DB to look up the original claim's group size.

All events carry denormalized `Date`/`Time` because downstream handlers (SMS, analytics, waitlist eligibility checks) currently lift these from `TeeTimeOpening` events and converting every handler to look them up again would be churn for no benefit. Events are wire formats, not write models — denormalization is expected there.

### Retired events (after full rollout — not in this spec)

None in this spec. All existing `TeeTimeOpening*` events keep working against the untouched walk-up flow. They are retired in the follow-on spec.

### Event handlers

- New: `Features/Bookings/Handlers/TeeTimeClaimed/CreateConfirmedBookingHandler.cs`
  - Replaces the current `BookingCreated` → confirm flow for direct bookings. Loads the pre-allocated `bookingId`, creates a `Booking.CreateConfirmed(bookingId, courseId, golferId, teeTimeId, playerCount)`.
- New: `Features/Bookings/Handlers/BookingCancelled/ReleaseTeeTimeClaimHandler.cs`
  - On `BookingCancelled`, loads the `TeeTime`, calls `ReleaseClaim(bookingId)`. Idempotent — safe if the release already happened from the other side.
- Existing waitlist handlers (`TeeTimeOpeningSlotsClaimed/...`, `TeeTimeOpeningFilled/...`, etc.) are **not touched** by this spec.

## Endpoint changes

### `TeeSheetEndpoints.GetTeeSheet` — rewritten

**Before:** Generates intervals on the fly from `Course.FirstTeeTime + Interval`, left-joins `Bookings` by time-match.

**After:**

1. Load the `TeeSheet` for (`courseId`, `date`). If none exists and the day is in the near future, return empty-but-scheduled (or auto-create a draft sheet — decision deferred; see open question 1).
2. Load all `TeeTime` rows for that `TeeSheetId` with their `Claims` eager-loaded.
3. Load all `Booking` rows referenced by the claims (for golfer name, player count — though player count is now on the claim).
4. Project: for each `TeeSheetInterval` in ordinal order, emit a response slot. If a `TeeTime` exists for that interval, fold in its status, claims, and bookings. Otherwise, emit an empty slot.

The read-model shape is a new record type scoped to the endpoint file, per the feature-folder conventions.


### New endpoint: `POST /courses/{courseId}/tee-sheets/draft`

Creates a `Draft` `TeeSheet` for the given date using the course's current schedule defaults. Body: `{ date: DateOnly }`. Returns 409 if a sheet (draft or published) already exists for that course and date. Under the hood: calls `Course.CurrentScheduleDefaults()` to produce a `ScheduleSettings`, then `TeeSheet.Draft(courseId, date, settings, timeProvider)`.

### New endpoint: `POST /courses/{courseId}/tee-sheets/{date}/publish`

Publishes an existing draft sheet. 404 if no sheet exists; 409 if already published (or idempotent — implementation plan picks; I lean idempotent per the existing "already in terminal state is a no-op" convention). Used to seed data in dev/test and (eventually) by an automated "publish the next N days" job.

### New endpoint: `POST /courses/{courseId}/tee-times/book`

Direct booking action. `courseId` stays in the path so tenant scoping goes through the existing `CourseExistsMiddleware`. The specific target interval is an input in the body, not a URL segment — which avoids the "path id type doesn't match body id type" mismatch that a `.../{intervalId}/book` shape would imply.

Body:

```
{ bookingId: Guid, teeSheetIntervalId: Guid, golferId: Guid, groupSize: int }
```

`bookingId` is supplied by the caller — pre-allocated client-side or (for waitlist acceptance) lifted from the existing `WaitlistOffer.BookingId` per the existing pre-allocation convention. FluentValidation requires it to be a non-empty `Guid`.

The Wolverine endpoint:

1. Loads the `TeeSheet` for the interval (via `TeeSheetRepository.GetByIntervalIdAsync` or similar).
2. Calls `sheet.AuthorizeBooking()` to mint a `BookingAuthorization` token (throws if sheet is Draft).
3. Finds the `TeeSheetInterval` within the sheet by id.
4. Looks up the existing `TeeTime` for the interval via `teeTimeRepo.GetOrNullByIntervalIdAsync(intervalId)`.
5. If null → calls `TeeTime.Claim(interval, auth, bookingId, golferId, groupSize, timeProvider)` factory.
6. If non-null → calls the existing `teeTime.Claim(auth, bookingId, golferId, groupSize, timeProvider)` instance method.
7. `repo.AddAsync` (for the factory path) or no-op (the existing TeeTime is already tracked by EF).

Transactional middleware saves, events flow, the `Booking` row lands via the `TeeTimeClaimed` handler.

### Existing booking endpoints

Any existing direct-booking endpoints (operator drag-drop, golfer-facing booking) are retargeted at the new flow. An inventory of those endpoints and their call sites will be part of the implementation plan.

## Persistence and migration

### Schema changes (EF migration)

- New table `TeeSheets` (Id, CourseId, Date, Status, Settings_FirstTeeTime, Settings_LastTeeTime, Settings_IntervalMinutes, Settings_DefaultCapacity, PublishedAt, CreatedAt). Unique index on (`CourseId`, `Date`). The `Settings_*` columns come from the `ScheduleSettings` VO via EF Core `ComplexProperty` mapping.
- New table `TeeSheetIntervals` (Id, TeeSheetId, Time, Capacity). Unique index on (`TeeSheetId`, `Time`).
- New table `TeeTimes` (Id, TeeSheetId, TeeSheetIntervalId, CourseId, Date, Time, Capacity, Remaining, Status, CreatedAt). Unique index on `TeeSheetIntervalId`. `TeeSheetId` is denormalized for the `BookingAuthorization` token comparison.
- New table `TeeTimeClaims` (Id, TeeTimeId, BookingId, GolferId, GroupSize, ClaimedAt). Unique index on (`TeeTimeId`, `BookingId`).
- `Courses` table: add `DefaultCapacity` column (int, default 4, not null).
- `Bookings` table: add `TeeTimeId` column, **nullable** (stays nullable through this spec — see the compatibility seam section and the `Booking` model change).

### No data migration — fresh DB

The DB is being reset as part of this change. A fresh initial EF migration captures the new schema and the old `Bookings.TeeTime` columns are preserved (still used by the walk-up flow during the seam). Test and dev data is reseeded via the existing seeding paths. No row-level backfill logic is specced or implemented.

### The compatibility seam

During the rollout and indefinitely until the follow-on spec ships:

- **`TeeTimeOpening` continues to exist** and is the sole owner of walk-up claim state.
- **New direct-booked `Booking` rows** always set `TeeTimeId` (non-null).
- **New walk-up-created `Booking` rows** leave `TeeTimeId` **null**. The walk-up flow is untouched; it continues to correlate via `TeeTimeOpeningId` on `WaitlistOffer` and via `BookingId` on `Booking`. These rows will be backfilled in the follow-on spec once walk-up migrates.
- **Capacity for walk-ups is tracked on `TeeTimeOpening`.** Capacity for direct bookings is tracked on `TeeTime`. This means capacity is tracked in two disjoint universes during the seam — a slot booked via the direct flow does not reserve any `TeeTimeOpening` capacity, and vice versa. In practice the two flows do not compete for the same slots in phase-1 deployments (walk-up flow runs separately from the direct tee sheet), so the drift is containable.

This is the known ugliness that the follow-on spec resolves. The implementation plan must call it out in PR descriptions so reviewers understand why the two flows look inconsistent.

## Testing

### Unit tests (domain)

Location: `tests/Teeforce.Domain.Tests/TeeSheetAggregate/` and `tests/Teeforce.Domain.Tests/TeeTimeAggregate/`.

`ScheduleSettings` (VO):
- Valid combinations construct successfully.
- Invalid combinations (first >= last, interval <= 0, defaultCapacity <= 0) throw domain exceptions.

`Course.CurrentScheduleDefaults()`:
- Returns a `ScheduleSettings` when all schedule fields are configured.
- Throws `CourseScheduleNotConfiguredException` when any field is unset.

`TeeSheet`:
- `Draft` enumerates intervals correctly for various (first, last, interval) combinations.
- `Draft` raises `TeeSheetDrafted`.
- `Publish` transitions `Draft → Published`, raises `TeeSheetPublished`, is idempotent.
- `Publish` on a `Draft` is the only way to reach `Published` (no shortcut).
- `AuthorizeBooking` on a `Published` sheet returns a token carrying the sheet id.
- `AuthorizeBooking` on a `Draft` sheet throws `TeeSheetNotPublishedException`.

`TeeTime` (the factory path passes a token from `sheet.AuthorizeBooking()`):
- `Claim` factory creates a new row in `Open` with the first claim applied.
- `Claim` on `Open` adds a claim, decrements `Remaining`, raises `TeeTimeClaimed`.
- `Claim` that exhausts capacity transitions to `Filled` and raises `TeeTimeFilled`.
- `Claim` on `Filled` throws `TeeTimeFilledException`.
- `Claim` on `Blocked` throws `TeeTimeBlockedException`.
- `Claim` with `groupSize > Remaining` throws `InsufficientCapacityException`.
- `Claim` with non-positive `groupSize` throws `InvalidGroupSizeException`.
- `Claim` with a token from a different sheet throws `BookingAuthorizationMismatchException`.
- `ReleaseClaim` removes the claim, increments `Remaining`, raises `TeeTimeClaimReleased` (with correct `GroupSize`).
- `ReleaseClaim` from `Filled` transitions to `Open` and raises `TeeTimeReopened`.
- `ReleaseClaim` with an unknown `BookingId` is a silent no-op.
- `Block` factory creates a row directly in `Blocked`.
- `Block` on `Open` with zero claims transitions, raises `TeeTimeBlocked`.
- `Block` on `Open` with any claims throws.
- `Unblock` transitions `Blocked → Open`, is idempotent.

`Block`/`Unblock` are domain-tested only — no HTTP endpoints ship in this spec for blocking. The implementation plan picks whether to expose them later.

### Unit tests (handlers)

Location: `tests/Teeforce.Api.Tests/Features/Bookings/Handlers/`.

- `CreateConfirmedBookingHandler` on `TeeTimeClaimed` creates a `Booking` in `Confirmed` status with the correct `TeeTimeId`.
- `ReleaseTeeTimeClaimHandler` on `BookingCancelled` loads the `TeeTime`, calls `ReleaseClaim`.

### Integration tests

Location: `tests/Teeforce.Api.IntegrationTests/`.

- End-to-end direct booking scenario: `POST /courses/{courseId}/tee-sheets/draft` → `POST /courses/{courseId}/tee-sheets/{date}/publish` → `POST /courses/{courseId}/tee-times/book` → assert `Booking` row, `TeeTime` row, `TeeTimeClaim` row, statuses.
- Booking against a draft sheet returns the `TeeSheetNotPublishedException` mapping (likely 409 or 422 — global handler decides).
- Cancellation scenario: book → `POST /bookings/{id}/cancel` → assert claim released, `TeeTime` back to `Open`, `TeeTimeClaimReleased` event carries the original `GroupSize`.
- Fill scenario: book up to capacity → next booking returns the appropriate domain-exception-mapped status.
- Concurrency test (smoke): two parallel booking requests at the same interval, last slot — one succeeds, one fails cleanly.
- Tee sheet view: publish a sheet, make a few bookings, `GET /tee-sheets` returns the sparse-joined projection in the right order with the right statuses.

### Tests not in this spec

No frontend tests. The web tee sheet view is updated only to consume the new response shape, which is backward-compatible (the response fields the UI reads today — `teeTime`, `status`, `golferName`, `playerCount` — remain, just sourced differently). A separate issue covers any UI work that surfaces per-slot identity (booking from the grid by intervalId instead of by wall-clock time).

## Open questions

These do not block the spec being approved, but they need answers in the implementation plan.

1. **When is a `TeeSheet` drafted and published for a given day?** This spec ships an explicit `/draft` + `/publish` endpoint pair; no auto-creation. A seed-during-dev-startup helper creates a small window of published sheets so local dev and integration tests are frictionless. The near-future feature (anticipated, not specced) is an operator-facing flow that auto-drafts the next week's sheets, lets the operator fill in blockages on each day, then publishes a whole week (or selected days) at once. The Draft/Publish separation in this spec is deliberately chosen to support that workflow — drafts are safe to edit, publish is a deliberate operator action. Implementation plan picks the exact seeding helper.
2. **Cross-slot invariants the domain does not yet enforce** — e.g., "a golfer can't have two bookings on the same day at the same time," or "minimum spacing between a single golfer's bookings." Today these are enforced, if at all, via repository queries or not at all. This spec does not add any new cross-aggregate rules, but it does identify the likely home for them: a new domain service (probably `BookingService` or `IBookingConflictChecker`) per the backend conventions ("Domain services for cross-aggregate logic that doesn't belong to any single aggregate"). Implementation plan documents what exists and what doesn't; no new rules added in this spec.
3. **Denormalized `Date`/`Time` drift.** If a future frost delay cascades updates from `TeeSheet` to `TeeTime`, is there a risk the cascade partially fails and leaves rows out of sync? The implementation plan should specify that the cascade is a single transaction on `TeeSheet` and its child intervals, with a separate saga (or Wolverine message chain) updating each affected `TeeTime`. Eventual consistency, with a reconciliation check. Not implemented in this spec.

## Success criteria

- All domain unit tests pass. No `DomainException` subclass is missing for any aggregate invariant (including the new `TeeSheetNotPublishedException`, `BookingAuthorizationMismatchException`, and per-`TeeTime` guards).
- All new integration tests pass, including the end-to-end draft → publish → book flow and the "book against draft sheet is rejected" path.
- `dotnet build teeforce.slnx` clean. `dotnet format` clean.
- The fresh initial migration applies against an empty database. Test data reseeds through normal seeding paths; the seeded environment has at least one published `TeeSheet` so a manual smoke test of booking works immediately.
- `GET /tee-sheets?courseId=X&date=Y` returns the same information the UI renders today (booking golfer, group size, status), sourced from the new aggregates.
- Walk-up/waitlist flow continues to work untouched — all existing `TeeTimeOpening*` tests still pass.
- `make dev` brings up the app; a manual smoke test of the operator tee sheet view and a direct booking works end-to-end.

## Follow-on specs (named, not designed)

- **Spec 2 — Walk-up / waitlist on TeeTime.** Migrate `TeeTimeOpening`, `WaitlistOffer`, offer policies, and walk-up handlers onto `TeeTime`. Design from the "automated fill-a-no-show" end state, not from the current manual-post shape. Delete `TeeTimeOpening`. Introduces the future `TeeTimeOfferPolicy` that correlates `BookingCancelled` and `TeeTimeClaimReleased` (both events needed, both carrying `GroupSize`) to trigger waitlist offers.
- **Spec 3 — Operator day-level ops.** UX and endpoints for blocks, frost delays, shotgun events, close-the-day. Builds on `TeeSheet.*` methods that this spec leaves as placeholders. Likely also introduces the auto-draft-next-week operator workflow (draft N days out automatically, operator fills in blockages, publishes a week or selected days at a time).
- **Spec 4 — Past tee time lifecycle.** Introduces a `TeeTimeMaterialized` event (raised on first `Claim`) and a `TeeTimePassedPolicy` that fires on a timeout at the tee time's datetime. The policy either emits a `TeeTimePassed` event for downstream handlers (analytics, scoring) or transitions the TeeTime to a new terminal state — design decision deferred to that spec.
- **Spec 5 — Richer slot semantics.** 9/18, front/back, per-slot capacity, per-slot pricing. Extends `TeeSheetInterval` and `TeeTime` schemas.
- **Spec 6 — Multi-golfer claims.** Adds `ClaimMember` child collection to `TeeTimeClaim` so a single claim can represent multiple named golfers (a foursome with four identified players). Purely additive — no schema breakage. Enables leaderboards, handicaps, repeat-play tracking per golfer.

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

1. New `TeeSheet` aggregate (per course per day), owning a schedule snapshot and a collection of `TeeSheetInterval` child entities with stable IDs.
2. New `TeeTime` aggregate — per-interval, lazily materialized. Holds capacity state, claims, and status. Carries denormalized `Date` and `Time`.
3. New `TeeTimeClaim` child entity (inside `TeeTime`). Replaces `ClaimedSlot`'s responsibilities.
4. **Rename** the existing `TeeTime` value object to `BookingDateTime` (or similar) as a prep step, so the `TeeTime` name can belong to the new aggregate. The VO is deleted in the follow-on spec, not this one.
5. Migrate the **direct-booking flow** to go through `TeeTime.Claim(bookingId, golferId, groupSize)`. `Booking` gains a `TeeTimeId` reference.
6. Keep `Booking` minimal. Add `TeeTimeId`. No ancillary fields (carts, range, etc.) — those are future bounded contexts correlated by `BookingId`.
7. Rewrite `TeeSheetEndpoints.GetTeeSheet` as a left join of `TeeSheet.Intervals` against the sparse `TeeTime` rows (and their claims' booking projections).
8. Minimal day-level operation support: `TeeSheet.Publish`. Enough publish machinery to make tests meaningful and to demonstrate that day-level ops have a real home.
9. Data migration: every existing `Booking` row gets paired with a materialized `TeeTime` + `TeeTimeClaim` under a published `TeeSheet` for its (course, date).

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

    // Schedule snapshot — copied from Course at publish time, then owned by the sheet.
    // Future per-day overrides (frost delay, shotgun events) mutate these, not Course.
    public TimeOnly FirstTeeTime { get; private set; }
    public TimeOnly LastTeeTime { get; private set; }
    public int IntervalMinutes { get; private set; }
    public int DefaultCapacity { get; private set; }     // usually 4 (foursome)

    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<TeeSheetInterval> intervals = [];
    public IReadOnlyList<TeeSheetInterval> Intervals => this.intervals.AsReadOnly();
}
```

**Behavior (this spec):**

- `TeeSheet.Create(courseId, date, firstTeeTime, lastTeeTime, intervalMinutes, defaultCapacity, timeProvider)` — static factory. Produces a `Draft` sheet with a fully enumerated `Intervals` collection. Each interval gets a `Guid.CreateVersion7()` id. Raises `TeeSheetCreated`.
- `TeeSheet.Publish(timeProvider)` — transitions `Draft → Published`. Sets `PublishedAt`. Raises `TeeSheetPublished`. Idempotent (re-calling on an already-published sheet is a no-op, per the existing codebase pattern for terminal states).

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
    public int Capacity { get; private set; }           // denormalized from TeeSheet.DefaultCapacity at creation
    public int OrdinalPosition { get; private set; }    // 0-indexed position within the day
}
```

Intervals are created with the sheet and live inside it. They are **not** a separate aggregate; they cannot be modified without going through `TeeSheet`. Their `Id` is referenced by `TeeTime` rows and is the stable handle for "the 9:00 slot on this day at this course."

`Capacity` on the interval overrides `TeeSheet.DefaultCapacity` in the future when we introduce per-slot capacity (twosome-only slots, etc.). For this spec, it is seeded from the default and never changed.

### `TeeTime` (new aggregate root)

Lazily materialized per interval. A row exists for an interval only when something non-default has happened to it: a claim was made, a block was placed, or (future) a non-default capacity was set. If nothing has happened, no row exists, and the interval shows empty on the tee sheet view.

**State:**

```csharp
public class TeeTime : Entity
{
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
    Guid bookingId,
    Guid golferId,
    int groupSize,
    ITimeProvider timeProvider)
{
    // Factory: constructs a new TeeTime for this interval in Open status
    // and applies the first claim in a single atomic operation.
    // Called when no TeeTime row yet exists for the interval.
}

public void Claim(Guid bookingId, Guid golferId, int groupSize, ITimeProvider timeProvider)
{
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
}

public static TeeTime Block(
    TeeSheetInterval interval,
    string reason,
    ITimeProvider timeProvider)
{
    // Factory: creates a TeeTime row in Blocked status with zero claims.
    // Used when an operator blocks an interval that had no prior activity.
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

**Why TeeTime is its own aggregate (not a child of TeeSheet):** Concurrency. Bookings happen in parallel at different times on the same day. If `TeeTime` lived inside `TeeSheet`, every booking on Saturday would contend on a single row. Making `TeeTime` its own write unit scales concurrency per-interval. The tradeoff — cross-slot invariants become sagas — is acceptable because the cross-slot operations that need atomicity (frost delay, shotgun, day close) live on `TeeSheet` and operate on intervals, not on the claim state.

**Why `TeeTime` carries denormalized `Date` and `Time`:** Every query against tee times wants the wall-clock time. Joining to `TeeSheetInterval` on every read is wasteful and couples read paths to the sheet aggregate. The denormalization is cheap — `Date`/`Time` change only under frost-delay-style operations, and those are rare and handled as a cascade from the sheet.

**Why no "Expired" status:** Under the current model, `TeeTimeOpening.Expired` means "the waitlist offer round timed out." Under the new model, `TeeTime` has no opinion about waitlist offer rounds — only `WaitlistOffer` does. When all offers for a TeeTime expire, the TeeTime stays `Open` with its remaining capacity intact. The operator (or the automated walk-up system in the follow-on spec) can do something else with it.

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

Deliberately thin. Ancillaries (carts, range, food, pro-shop purchases) do **not** live here — they live in future bounded contexts that correlate by `BookingId`. Keeping the claim minimal preserves the mental model "TeeTime is the capacity ledger; Booking is the commercial reservation."

### `Booking` (modified existing aggregate)

Changes:

- **Add:** `TeeTimeId` — **nullable** for the duration of this spec. Direct-booked rows set it; legacy rows get it set by the data migration; new walk-up rows (which keep going through the untouched `TeeTimeOpening` path during the compatibility seam) leave it null. The follow-on spec makes it NOT NULL after the walk-up migration lands.
- **Keep:** `CourseId`, `GolferId`, `PlayerCount`, `Status`, `CreatedAt`.
- **Rename, don't delete (yet):** The existing `TeeTime` value object is renamed to `BookingDateTime` (or another name chosen during implementation) as a prep step, and stays on `Booking`. The walk-up flow still creates `Booking` rows with no `TeeTimeId` during the compatibility seam, so the booking must still carry its own date/time. The VO's normalization and comparison behavior is preserved under the new name.

The rename is a namespace/type rename only — no behavioral change. It clears the `TeeTime` name for the new aggregate so the two don't collide. The follow-on spec deletes the renamed VO along with the old `Bookings.TeeTime` columns once walk-up moves onto `TeeTime`.

`Booking.Status` keeps its current enum (`Pending | Confirmed | Rejected | Cancelled`). Lifecycle methods (`Confirm`, `Reject`, `Cancel`) are unchanged in shape. `CreateConfirmed` gains a `teeTimeId` parameter.

## Domain events

### New events

- `TeeSheetCreated { TeeSheetId, CourseId, Date, IntervalCount }`
- `TeeSheetPublished { TeeSheetId, CourseId, Date, PublishedAt }`
- `TeeTimeClaimed { TeeTimeId, BookingId, GolferId, GroupSize, CourseId, Date, Time }`
- `TeeTimeFilled { TeeTimeId, CourseId, Date, Time }`
- `TeeTimeClaimReleased { TeeTimeId, BookingId, GolferId, CourseId, Date, Time }`
- `TeeTimeReopened { TeeTimeId, CourseId, Date, Time }`  (emitted when Filled → Open via release)
- `TeeTimeBlocked { TeeTimeId, CourseId, Date, Time, Reason }`
- `TeeTimeUnblocked { TeeTimeId, CourseId, Date, Time }`

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

### New endpoint: `POST /courses/{courseId}/tee-sheets/{date}/publish`

Creates and publishes the `TeeSheet` for the given date if it doesn't exist; publishes an existing draft if it does. Used to seed data in dev/test and (eventually) by an automated job that publishes days N days in advance.

### New endpoint: `POST /courses/{courseId}/tee-sheet-intervals/{intervalId}/book`

Direct booking against a `TeeSheetInterval` (note: the route uses the interval id, not the TeeTime id, because the TeeTime may not yet exist at the time of the call). Pre-allocates `bookingId` on the server side. Body:

```
{ golferId: Guid, groupSize: int }
```

Wolverine endpoint calls `TeeTime.Claim(...)` via the repository (or materializes a new TeeTime via the factory if none exists for the interval). Transactional middleware saves, events flow, the `Booking` row lands via the `TeeTimeClaimed` handler.

### Existing booking endpoints

Any existing direct-booking endpoints (operator drag-drop, golfer-facing booking) are retargeted at the new flow. An inventory of those endpoints and their call sites will be part of the implementation plan.

## Persistence and migration

### Schema changes (EF migration)

- New table `TeeSheets` (Id, CourseId, Date, Status, FirstTeeTime, LastTeeTime, IntervalMinutes, DefaultCapacity, PublishedAt, CreatedAt). Unique index on (`CourseId`, `Date`).
- New table `TeeSheetIntervals` (Id, TeeSheetId, Time, Capacity, OrdinalPosition). Unique index on (`TeeSheetId`, `Time`).
- New table `TeeTimes` (Id, TeeSheetIntervalId, CourseId, Date, Time, Capacity, Remaining, Status, CreatedAt). Unique index on `TeeSheetIntervalId`.
- New table `TeeTimeClaims` (Id, TeeTimeId, BookingId, GolferId, GroupSize, ClaimedAt). Unique index on (`TeeTimeId`, `BookingId`).
- `Bookings` table: add `TeeTimeId` column, **nullable** (stays nullable through this spec — see the compatibility seam section and the `Booking` model change).
- `Bookings` table: drop the old `TeeTime` (date+time composite) columns once backfill is complete. Direct-booked rows reach their date/time via the `TeeTime` row; walk-up rows during the seam still need date/time stored on the booking because they have no `TeeTimeId` — so during this spec, the old columns stay **and** a new nullable `TeeTimeId` is added alongside them. The follow-on spec drops the old columns.

### Data migration (one-shot, in the same PR chain)

For every existing `Booking`:

1. Look up (or create) a `TeeSheet` for (`Booking.CourseId`, `Booking.TeeTime.Date`). Use the current `Course.TeeTimeIntervalMinutes`/`FirstTeeTime`/`LastTeeTime` at migration time as the schedule snapshot. Set `DefaultCapacity = 4`. Mark as `Published` with `PublishedAt = Booking.CreatedAt` (or `now`).
2. Find the matching `TeeSheetInterval` by `Time` within that sheet. If the booking's time doesn't align to an interval boundary, log and flag — this should not happen with existing data, but we want to know if it does.
3. Materialize a `TeeTime` row for that interval (if not already created by a sibling booking on the same interval). Set `Capacity = 4`, `Remaining = 4 - sum(existing group sizes)`, `Status = Open | Filled`.
4. Create a `TeeTimeClaim` for the booking.
5. Set `Booking.TeeTimeId` to the new row's id.

The migration is idempotent (safe to rerun) and logged. Any orphan row encountered (booking without a matchable interval) is logged loudly and **does not fail the migration** — it gets skipped and surfaced in a follow-up report.

### The compatibility seam

During the rollout and indefinitely until the follow-on spec ships:

- **`TeeTimeOpening` continues to exist** and is the sole owner of walk-up claim state.
- **Pre-existing `Booking` rows** (at the time of migration) get a `TeeTimeId` populated by the data migration regardless of whether they originated from the walk-up or direct-booking flow.
- **New walk-up-created `Booking` rows** (after the migration runs) leave `TeeTimeId` **null**. The walk-up flow is untouched; it continues to correlate via `TeeTimeOpeningId` on `WaitlistOffer` and via `BookingId` on `Booking`. These rows will be backfilled in the follow-on spec once walk-up migrates.
- **Capacity for walk-ups is tracked on `TeeTimeOpening`.** Capacity for direct bookings is tracked on `TeeTime`. This means capacity is tracked in two disjoint universes during the seam — a slot booked via the direct flow does not reserve any `TeeTimeOpening` capacity, and vice versa. In practice the two flows do not compete for the same slots in phase-1 deployments (walk-up flow runs separately from the direct tee sheet), so the drift is containable.

This is the known ugliness that the follow-on spec resolves. The implementation plan must call it out in PR descriptions so reviewers understand why the two flows look inconsistent.

## Testing

### Unit tests (domain)

Location: `tests/Teeforce.Domain.Tests/TeeSheetAggregate/` and `tests/Teeforce.Domain.Tests/TeeTimeAggregate/`.

`TeeSheet`:
- `Create` enumerates intervals correctly for various (first, last, interval) combinations.
- `Create` with invalid config (first >= last, interval <= 0, capacity <= 0) throws the appropriate domain exceptions.
- `Publish` transitions `Draft → Published`, raises event, is idempotent.
- `Publish` on a `Draft` is the only way to reach `Published` (no shortcut).

`TeeTime`:
- `Claim` factory creates a new row in `Open` with the first claim applied.
- `Claim` on `Open` adds a claim, decrements `Remaining`, raises `TeeTimeClaimed`.
- `Claim` that exhausts capacity transitions to `Filled` and raises `TeeTimeFilled`.
- `Claim` on `Filled` throws `TeeTimeFilledException`.
- `Claim` on `Blocked` throws `TeeTimeBlockedException`.
- `Claim` with `groupSize > Remaining` throws `InsufficientCapacityException`.
- `Claim` with non-positive `groupSize` throws `InvalidGroupSizeException`.
- `ReleaseClaim` removes the claim, increments `Remaining`, raises `TeeTimeClaimReleased`.
- `ReleaseClaim` from `Filled` transitions to `Open` and raises `TeeTimeReopened`.
- `ReleaseClaim` with an unknown `BookingId` is a silent no-op.
- `Block` factory creates a row directly in `Blocked`.
- `Block` on `Open` with zero claims transitions, raises `TeeTimeBlocked`.
- `Block` on `Open` with any claims throws.
- `Unblock` transitions `Blocked → Open`, is idempotent.

### Unit tests (handlers)

Location: `tests/Teeforce.Api.Tests/Features/Bookings/Handlers/`.

- `CreateConfirmedBookingHandler` on `TeeTimeClaimed` creates a `Booking` in `Confirmed` status with the correct `TeeTimeId`.
- `ReleaseTeeTimeClaimHandler` on `BookingCancelled` loads the `TeeTime`, calls `ReleaseClaim`.

### Integration tests

Location: `tests/Teeforce.Api.IntegrationTests/`.

- End-to-end direct booking scenario: `POST /tee-sheets/.../publish` → `POST /tee-times/.../book` → assert `Booking` row, `TeeTime` row, `TeeTimeClaim` row, statuses.
- Cancellation scenario: book → `POST /bookings/{id}/cancel` → assert claim released, `TeeTime` back to `Open`.
- Fill scenario: book up to capacity → next booking returns 409 or the appropriate domain error.
- Concurrency test (smoke): two parallel booking requests at the same interval, last slot — one succeeds, one fails cleanly.
- Tee sheet view: publish a sheet, make a few bookings, `GET /tee-sheets` returns the sparse-joined projection in the right order with the right statuses.
- Migration scenario: seed legacy bookings via the old schema path (if feasible) and run the migration. Assert pairings.

### Tests not in this spec

No frontend tests. The web tee sheet view is updated only to consume the new response shape, which is backward-compatible (the response fields the UI reads today — `teeTime`, `status`, `golferName`, `playerCount` — remain, just sourced differently). A separate issue covers any UI work that surfaces per-slot identity (booking from the grid by intervalId instead of by wall-clock time).

## Open questions

These do not block the spec being approved, but they need answers in the implementation plan.

1. **When is a `TeeSheet` created for a given day?** Options: (a) auto-create on first read if none exists; (b) require explicit publish via a seed job running N days ahead; (c) create on demand the first time a booking is attempted at that date. For the spec, I lean (b) with a seed-during-dev-startup helper so local development and tests are frictionless. Implementation plan should confirm.
2. **How does the new `POST /tee-times/{intervalId}/book` endpoint handle the "no TeeTime row yet exists" case?** The factory path is clean (`TeeTime.Claim(interval, ...)`), but the endpoint needs to know whether to load + call the instance method or call the factory. Clean way: repository method `GetOrNullAsync(intervalId)` + branch. Plan should specify.
3. **Do operator-facing endpoints for block/unblock ship in this spec, or a follow-on?** The domain supports them; a minimal `POST /tee-times/{intervalId}/block` would prove the block path works. Lean yes — include it, skip the UI. Plan should confirm.
4. **Cross-slot invariants the domain does not yet enforce** — e.g., "a golfer can't have two bookings on the same day at the same time." Today these are enforced, if at all, via repository queries or not at all. Document what exists and what doesn't; do not add new cross-aggregate rules in this spec.
5. **Denormalized `Date`/`Time` drift.** If a future frost delay cascades updates from `TeeSheet` to `TeeTime`, is there a risk the cascade partially fails and leaves rows out of sync? The implementation plan should specify that the cascade is a single transaction on `TeeSheet` and its child intervals, with a separate saga (or Wolverine message chain) updating each affected `TeeTime`. Eventual consistency, with a reconciliation check.

## Success criteria

- All domain unit tests pass. No `DomainException` subclass is missing for any aggregate invariant.
- All new integration tests pass.
- `dotnet build teeforce.slnx` clean. `dotnet format` clean.
- Data migration runs end-to-end on a seeded test database and produces a consistent state (every pre-existing `Booking` has a matching `TeeTime` + `TeeTimeClaim`, and `Booking.TeeTimeId` is populated for every pre-existing row). New walk-up bookings created after the migration ships may still have null `TeeTimeId` — that's expected and handled in the follow-on spec.
- `GET /tee-sheets?courseId=X&date=Y` returns the same information the UI renders today (booking golfer, group size, status), sourced from the new aggregates.
- Walk-up/waitlist flow continues to work untouched — all existing `TeeTimeOpening*` tests still pass.
- `make dev` brings up the app; a manual smoke test of the operator tee sheet view and a direct booking works end-to-end.

## Follow-on specs (named, not designed)

- **Spec 2 — Walk-up / waitlist on TeeTime.** Migrate `TeeTimeOpening`, `WaitlistOffer`, offer policies, and walk-up handlers onto `TeeTime`. Design from the "automated fill-a-no-show" end state, not from the current manual-post shape. Delete `TeeTimeOpening`.
- **Spec 3 — Operator day-level ops.** UX and endpoints for blocks, frost delays, shotgun events, close-the-day. Builds on `TeeSheet.*` methods that this spec leaves as placeholders.
- **Spec 4 — Richer slot semantics.** 9/18, front/back, per-slot capacity, per-slot pricing. Extends `TeeSheetInterval` and `TeeTime` schemas.

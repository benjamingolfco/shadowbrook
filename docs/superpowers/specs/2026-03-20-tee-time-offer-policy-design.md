# Tee Time Offer Policy Design

## Problem

When a tee time slot opens (currently via walk-up waitlist tee time requests), all eligible golfers are notified simultaneously in a single transaction. This creates a race condition where multiple golfers compete for the same slot, and couples SMS delivery with offer creation.

The system needs sequential, buffered notifications — offer to one golfer at a time, give them a window to respond, then move to the next.

## Design Overview

Two Wolverine saga-based policies replace the monolithic `TeeTimeRequestAddedNotifyHandler`:

- **`TeeTimeOfferPolicy`** — sequentially notifies eligible golfers with a buffer between notifications
- **`TeeTimeRequestExpirationPolicy`** — closes tee time requests when the tee time passes

The term "policy" is used instead of "saga" to tie the concept to business language. The underlying implementation uses Wolverine's `Saga` base class.

## TeeTimeOfferPolicy

### State

```csharp
public class TeeTimeOfferPolicy : Saga
{
    public Guid Id { get; set; }              // = TeeTimeRequestId
    public Guid? CurrentOfferId { get; set; } // the offer we're waiting on
    public bool IsBuffering { get; set; }     // true = buffer timeout is active
}
```

Persisted via EF Core — mapped in `ApplicationDbContext`. Wolverine handles load/save/delete automatically.

### Lifecycle

| Event | Condition | Action |
|-------|-----------|--------|
| `TeeTimeRequestAdded` | -- | `Start` — send `NotifyNextEligibleGolfer` command |
| `GolferNotifiedOfOffer` | -- | Set `CurrentOfferId`, `IsBuffering = true`, schedule `TeeTimeOfferTimeout` |
| `TeeTimeOfferTimeout` | `OfferId == CurrentOfferId` | Set `IsBuffering = false`, send `NotifyNextEligibleGolfer` |
| `TeeTimeOfferTimeout` | `OfferId != CurrentOfferId` | Ignore (stale timeout) |
| `WaitlistOfferRejected` | `OfferId == CurrentOfferId` | Set `IsBuffering = false`, send `NotifyNextEligibleGolfer` |
| `WaitlistOfferRejected` | `OfferId != CurrentOfferId` | Ignore |
| `TeeTimeRequestFulfilled` | -- | `MarkCompleted()` |
| `TeeTimeRequestClosed` | -- | `MarkCompleted()` |

### Flow Diagram

```
TeeTimeRequestAdded
  |
  v
[Start] --> NotifyNextEligibleGolfer { TeeTimeRequestId }
                |
                v
         Handler finds next eligible golfer
                |
        +-------+-------+
        |               |
   Found golfer    No golfer found
        |               |
        v               v
  Create offer      Do nothing
  Send SMS          (policy sits idle
  Call offer.        until expiration)
  MarkNotified()
        |
        v
  GolferNotifiedOfOffer (domain event)
        |
        v
  [Policy] Set CurrentOfferId, IsBuffering=true
           Schedule TeeTimeOfferTimeout(5 min)
        |
        +---------------------------+
        |                           |
  Timeout fires              Golfer rejects
  (OfferId matches)          (OfferId matches)
        |                           |
        v                           v
  IsBuffering=false           IsBuffering=false
  NotifyNextEligibleGolfer    NotifyNextEligibleGolfer
        |                           |
        +------------+--------------+
                     |
                     v
              (cycle repeats)
```

### Buffer Behavior

- The buffer (60 seconds, hardcoded for now) ensures minimum spacing between notifications
- Each golfer gets a full buffer window from when *they* were notified
- If a golfer rejects during the buffer, the buffer is canceled and the next golfer is notified immediately with a fresh buffer
- If the buffer expires without a response, the next golfer is notified but the previous offer stays valid — it only becomes invalid when the tee time request is fulfilled or closed
- Stale timeouts (where `OfferId` doesn't match `CurrentOfferId`) are ignored
- **Idle state:** If the handler finds no eligible golfers, it does nothing. The policy sits idle with `IsBuffering = false` until the tee time request is closed by the expiration policy. In this initial implementation, there is no reactivation path — a golfer joining the waitlist after all eligible golfers have been exhausted will not trigger new offers. This is a known gap deferred to a future iteration.

### File Placement

`Features/WaitlistOffers/TeeTimeOfferPolicy.cs` — the policy's primary output is creating offers (consumer rule).

## TeeTimeRequestExpirationPolicy

### State

```csharp
public class TeeTimeRequestExpirationPolicy : Saga
{
    public Guid Id { get; set; } // = TeeTimeRequestId
}
```

### Lifecycle

| Event | Action |
|-------|--------|
| `TeeTimeRequestAdded` | `Start` — schedule `TeeTimeRequestExpirationTimeout` for the tee time. Note: `TeeTimeRequestAdded` carries `Date` and `TeeTime` fields (a pre-existing deviation from the IDs-only event convention). The policy combines these into a `DateTimeOffset` using UTC for timeout scheduling. |
| `TeeTimeRequestExpirationTimeout` | Send `CloseTeeTimeRequest` command |
| `TeeTimeRequestFulfilled` | `MarkCompleted()` |
| `TeeTimeRequestClosed` | `MarkCompleted()` (idempotent — prevents orphaned saga rows after expiration fires) |

### File Placement

`Features/WalkUpWaitlist/TeeTimeRequestExpirationPolicy.cs` — it modifies `TeeTimeRequest` state (consumer rule), and `TeeTimeRequest` lives with walk-up waitlist.

## NotifyNextEligibleGolferHandler

Stateless command handler that replaces the bulk logic in `TeeTimeRequestAddedNotifyHandler`.

### Behavior

1. Load `TeeTimeRequest` (for CourseId, Date, TeeTime, GolfersNeeded)
2. Find the `WalkUpWaitlist` for the course+date
3. Query eligible `GolferWaitlistEntry` records: active, walk-up, ready, group size fits remaining slots
4. Exclude entries that already have a `WaitlistOffer` for this `TeeTimeRequestId`
5. Order by `JoinedAt` (FIFO), take first
6. If found:
   - Create `WaitlistOffer` (raises `WaitlistOfferCreated`)
   - Send SMS via `ITextMessageService`
   - Call `offer.MarkNotified()` (sets `NotifiedAt`, raises `GolferNotifiedOfOffer`)
   - If SMS fails, the handler throws before `MarkNotified()` is called — no event fires, no buffer starts. Wolverine retries the handler.
7. If not found: do nothing — policy sits idle

### File Placement

`Features/WaitlistOffers/NotifyNextEligibleGolferHandler.cs` — creates offers (consumer rule).

## CloseTeeTimeRequestHandler

Stateless command handler that loads a `TeeTimeRequest` and calls `.Close()`.

### File Placement

`Features/WalkUpWaitlist/CloseTeeTimeRequestHandler.cs` — modifies `TeeTimeRequest` state.

## Domain Changes

### WaitlistOffer Aggregate

**New method — `MarkNotified`:**

```csharp
public void MarkNotified()
{
    this.NotifiedAt = DateTimeOffset.UtcNow;
    AddDomainEvent(new GolferNotifiedOfOffer(this.Id, this.TeeTimeRequestId));
}
```

The handler is responsible for sending the SMS before calling this method. This keeps the domain pure (no infrastructure dependencies in aggregate methods) while ensuring the buffer only starts after successful SMS delivery.

**New property:** `NotifiedAt` (`DateTimeOffset?`) — records when the golfer was notified.

**New event on `Create()`:** `WaitlistOfferCreated` — raised when an offer is created. No consumer defined in this spec; available for future handlers (e.g., analytics, audit).

### WaitlistOfferRejected Event Change

`WaitlistOfferRejected` must add `TeeTimeRequestId` to its payload. Without it, Wolverine cannot correlate the event to the correct `TeeTimeOfferPolicy` saga instance.

Updated shape:

```csharp
public record WaitlistOfferRejected : IDomainEvent
{
    public required Guid WaitlistOfferId { get; init; }
    public required Guid TeeTimeRequestId { get; init; }  // NEW — required for saga correlation
    public required Guid GolferWaitlistEntryId { get; init; }
    public required string Reason { get; init; }
}
```

### TeeTimeRequest Aggregate

**New method — `Close()`:**

```csharp
public void Close()
{
    // Guard: only close if not already closed/fulfilled
    this.Status = TeeTimeRequestStatus.Closed;
    AddDomainEvent(new TeeTimeRequestClosed(this.Id));
}
```

### New Domain Events

| Event | Raised By | Carries |
|-------|-----------|---------|
| `WaitlistOfferCreated` | `WaitlistOffer.Create()` | `OfferId`, `TeeTimeRequestId`, `GolferWaitlistEntryId` |
| `GolferNotifiedOfOffer` | `WaitlistOffer.MarkNotified()` | `OfferId`, `TeeTimeRequestId` |
| `TeeTimeRequestClosed` | `TeeTimeRequest.Close()` | `TeeTimeRequestId` |

### Modified Domain Events

| Event | Change |
|-------|--------|
| `WaitlistOfferRejected` | Add `TeeTimeRequestId` field (required for saga correlation) |

### New Commands

| Command | Carries | Handled By |
|---------|---------|------------|
| `NotifyNextEligibleGolfer` | `TeeTimeRequestId` | `NotifyNextEligibleGolferHandler` |
| `CloseTeeTimeRequest` | `TeeTimeRequestId` | `CloseTeeTimeRequestHandler` |

### New Timeout Messages

| Timeout | Duration | Carries |
|---------|----------|---------|
| `TeeTimeOfferTimeout` | 5 minutes | `TeeTimeRequestId`, `OfferId` |
| `TeeTimeRequestExpirationTimeout` | Until tee time | `TeeTimeRequestId` |

## EF Core Changes

- Map `TeeTimeOfferPolicy` in `ApplicationDbContext` (Wolverine saga persistence)
- Map `TeeTimeRequestExpirationPolicy` in `ApplicationDbContext`
- Add `NotifiedAt` column to `WaitlistOffer` table (migration required)

## Removed

- `TeeTimeRequestAddedNotifyHandler` — responsibilities fully replaced by `TeeTimeOfferPolicy` + `NotifyNextEligibleGolferHandler`

## Future Considerations

- **Rename `TeeTimeRequest` to `TeeTimeOpening`:** The current name implies a golfer is requesting something, but the concept is really "a tee time slot opened up." Tracked as a separate chore — all references in this spec will be updated after the rename lands.
- **Tee time cancellations:** Add a second `Start` handler on `TeeTimeOfferPolicy` for a cancellation event — the policy behavior is identical regardless of how the slot opened.
- **Configurable buffer timeout:** Per-course configuration, not needed for v1.
- **`GolferJoinedWaitlist` reactivation:** If a golfer joins the waitlist while the policy is idle (no eligible golfers), the policy could be reactivated. Deferred — requires fan-out correlation since `GolferJoinedWaitlist` maps to multiple policies by `CourseWaitlistId`, not `TeeTimeRequestId`.
- **Cancellation reopening:** If a booking is cancelled after `TeeTimeRequestFulfilled`, the request may need to be reopened and the offer policy restarted. Separate design needed.

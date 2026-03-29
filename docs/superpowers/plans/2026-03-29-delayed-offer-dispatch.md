# Delayed Offer Dispatch (Grace Period)

## Problem

When an operator posts a tee time opening, `TeeTimeOpeningCreated` immediately triggers `TeeTimeOpeningOfferPolicy.Start()`, which dispatches `FindAndOfferEligibleGolfers` synchronously. Offers are created and SMS sent within seconds. If the operator made a mistake, golfers have already been notified.

## Current Flow

```
Operator posts opening
  -> TeeTimeOpening.Create() raises TeeTimeOpeningCreated
  -> TeeTimeOpeningOfferPolicy.Start(TeeTimeOpeningCreated)
       returns (policy, FindAndOfferEligibleGolfers)     <-- immediate
  -> FindAndOfferEligibleGolfersHandler.Handle()
       creates WaitlistOffer entities (raises WaitlistOfferCreated)
  -> WaitlistOfferCreatedSendSmsHandler.Handle()
       sends SMS to golfer
```

The key trigger is `TeeTimeOpeningOfferPolicy.Start()` -- it returns `FindAndOfferEligibleGolfers` as a cascading message, which Wolverine dispatches immediately.

## Proposed Change

Replace the immediate `FindAndOfferEligibleGolfers` cascading message with a `TimeoutMessage` that fires after a 15-second grace period. The policy already has the exact pattern we need -- `TeeTimeOpeningExpirationPolicy` and `WaitlistOfferResponsePolicy` both use `TimeoutMessage` for delayed actions.

### Modified flow

```
Operator posts opening
  -> TeeTimeOpening.Create() raises TeeTimeOpeningCreated
  -> TeeTimeOpeningOfferPolicy.Start(TeeTimeOpeningCreated)
       returns (policy, OfferDispatchGracePeriodTimeout)     <-- 15s delay
  -> [15 seconds pass]
  -> TeeTimeOpeningOfferPolicy.Handle(OfferDispatchGracePeriodTimeout)
       returns FindAndOfferEligibleGolfers                   <-- now dispatches
  -> (rest of flow unchanged)
```

### Cancellation during grace period

The policy already handles `TeeTimeOpeningCancelled`:

```
Currently: TeeTimeOpeningCancelled is NOT handled by TeeTimeOpeningOfferPolicy
```

Wait -- let me verify. Looking at the policy code, it does NOT have a handler for `TeeTimeOpeningCancelled`. It handles `TeeTimeOpeningFilled` and `TeeTimeOpeningExpired` (both call `MarkCompleted()`), but NOT cancelled.

This is fine today because the policy starts and immediately dispatches offers -- there's no window where cancellation matters to the policy itself. The separate `TeeTimeOpeningCancelledRejectOffersHandler` handles rejecting any already-created offers.

With the grace period, we need the policy to handle `TeeTimeOpeningCancelled` by marking itself completed. When the timeout fires on a completed saga, Wolverine simply discards it -- the saga no longer exists.

## Files to Modify

### 1. `src/backend/Shadowbrook.Api/Features/Waitlist/Policies/TeeTimeOpeningOfferPolicy.cs`

Changes:
- Add a `GracePeriodExpired` boolean property (persisted) to track whether offers have been dispatched
- Modify `Start()` to return `OfferDispatchGracePeriodTimeout` instead of `FindAndOfferEligibleGolfers`
- Add `Handle(OfferDispatchGracePeriodTimeout)` that sets `GracePeriodExpired = true` and returns `FindAndOfferEligibleGolfers`
- Add `Handle(TeeTimeOpeningCancelled)` that calls `MarkCompleted()`
- Add the `OfferDispatchGracePeriodTimeout` record as a `TimeoutMessage`
- `DispatchMoreOffersIfNeeded()` should remain unchanged -- it is called by subsequent events (rejection, stale, etc.) that happen after the grace period

Pseudocode:

```csharp
public static (TeeTimeOpeningOfferPolicy, OfferDispatchGracePeriodTimeout) Start(TeeTimeOpeningCreated evt)
{
    var policy = new TeeTimeOpeningOfferPolicy
    {
        Id = evt.OpeningId,
        SlotsRemaining = evt.SlotsAvailable
    };
    return (policy, new OfferDispatchGracePeriodTimeout(GracePeriod));
}

public FindAndOfferEligibleGolfers Handle(OfferDispatchGracePeriodTimeout timeout)
{
    GracePeriodExpired = true;
    return new FindAndOfferEligibleGolfers(Id, SlotsRemaining);
}

public void Handle([SagaIdentityFrom("OpeningId")] TeeTimeOpeningCancelled evt)
{
    MarkCompleted();
}

// Existing DispatchMoreOffersIfNeeded also needs a guard:
private FindAndOfferEligibleGolfers? DispatchMoreOffersIfNeeded()
{
    if (!GracePeriodExpired) return null;  // <-- new guard
    // ... rest unchanged
}

public record OfferDispatchGracePeriodTimeout(TimeSpan Delay) : TimeoutMessage(Delay);
private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(15);
```

The `GracePeriodExpired` guard on `DispatchMoreOffersIfNeeded` is important: if a `WakeUpOfferPolicy` command arrives during the grace period (because a golfer joins the waitlist), we should NOT dispatch offers yet.

### 2. `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/TeeTimeOpeningOfferPolicyConfiguration.cs`

Add property mapping for `GracePeriodExpired`:

```csharp
builder.Property(p => p.GracePeriodExpired);
```

### 3. EF Migration

A new migration is needed to add the `GracePeriodExpired` column to `TeeTimeOpeningOfferPolicies`:

```
dotnet ef migrations add AddGracePeriodToOfferPolicy --project src/backend/Shadowbrook.Api
```

The column is a `bit NOT NULL DEFAULT 0`. Existing rows (if any active policies exist) will default to `false`, which is correct -- they were created before the grace period feature and should behave as if the grace period already expired. However, we should set the default to `true` for existing rows since those policies have already dispatched their initial offers. Actually, `false` is safe: the timeout has already fired for existing policies (or will never fire), and the `DispatchMoreOffersIfNeeded` guard just means they won't re-dispatch on subsequent events until... wait, that's a problem.

**Migration consideration:** For any in-flight policies that already dispatched their initial offers (pre-migration), `GracePeriodExpired` will be `false`, and the new guard will prevent `DispatchMoreOffersIfNeeded` from working. Fix: set the default to `true` in the migration for existing rows, and only new policies start with `false` (set in `Start()`).

Actually, simpler: initialize `GracePeriodExpired = false` only in `Start()`. In the migration, use `DEFAULT 1` so existing rows get `true`. The property default in C# is `false` (bool default), but EF will set it explicitly on new inserts because `Start()` creates the policy with `GracePeriodExpired = false` explicitly... wait, no. The `Start()` method doesn't set it, so it defaults to `false`.

Cleanest approach: the migration adds the column with `DEFAULT 1` (so existing rows are treated as grace-period-already-expired). New policies created by `Start()` will have `GracePeriodExpired = false` (C# bool default). When the timeout fires, `Handle(OfferDispatchGracePeriodTimeout)` sets it to `true`.

## Edge Cases

### Opening cancelled during grace period
- `TeeTimeOpeningCancelled` arrives at the policy -> `MarkCompleted()` -> saga deleted
- When `OfferDispatchGracePeriodTimeout` fires, Wolverine can't load the saga -> message is discarded
- No offers are ever created or sent
- This is the happy path for the "oops" scenario

### Opening cancelled after grace period (offers already sent)
- Same as today -- `TeeTimeOpeningCancelledRejectOffersHandler` rejects pending offers
- Policy may or may not still be alive (it completes on `TeeTimeOpeningFilled` or `TeeTimeOpeningExpired`)
- New `Handle(TeeTimeOpeningCancelled)` also marks the policy as completed -- no harm if it's already completed

### WakeUpOfferPolicy arrives during grace period
- A golfer joins the waitlist, which sends `WakeUpOfferPolicy` to the saga
- The existing handler calls `DispatchMoreOffersIfNeeded()`
- The new `GracePeriodExpired` guard returns `null` -- no offers dispatched yet
- When the grace period expires, `FindAndOfferEligibleGolfers` will pick up this golfer

### Two openings posted rapidly
- Each opening creates its own independent policy instance (keyed by `OpeningId`)
- Each has its own 15-second timeout
- No interference between them

### Opening filled during grace period (unlikely but possible)
- `TeeTimeOpeningFilled` arrives -> `MarkCompleted()` -> saga deleted
- Timeout fires on deleted saga -> discarded
- No offers sent (which is correct -- the opening is already full)

## Testing Approach

### Unit tests for the policy

The policy is a pure state machine -- test it directly without DB or messaging.

1. **Start returns timeout, not immediate dispatch** -- verify `Start()` returns `OfferDispatchGracePeriodTimeout` (not `FindAndOfferEligibleGolfers`)
2. **Timeout handler dispatches offers** -- call `Handle(OfferDispatchGracePeriodTimeout)`, verify it returns `FindAndOfferEligibleGolfers` and sets `GracePeriodExpired = true`
3. **Cancellation before timeout marks completed** -- call `Handle(TeeTimeOpeningCancelled)`, verify saga is completed
4. **WakeUpOfferPolicy during grace period returns null** -- start policy, call `Handle(WakeUpOfferPolicy)` without first handling the timeout, verify null return
5. **WakeUpOfferPolicy after grace period dispatches** -- start policy, handle timeout, then handle `WakeUpOfferPolicy`, verify `FindAndOfferEligibleGolfers` returned
6. **Rejection/stale during grace period returns null** -- same pattern as #4

### Existing tests

Check and update any existing `TeeTimeOpeningOfferPolicy` tests to account for the new timeout step. Tests that previously expected `FindAndOfferEligibleGolfers` from `Start()` need to expect `OfferDispatchGracePeriodTimeout` instead.

## Notes

- The 15-second value should be a constant in the policy class (like `WalkUpBuffer` and `OnlineBuffer` in `WaitlistOfferResponsePolicy`). It can be made configurable per-course later if needed.
- No frontend changes needed -- this is purely backend messaging timing.
- No new endpoints needed.
- The `TeeTimeOpeningCancelledRejectOffersHandler` continues to work as-is for offers that were already created post-grace-period.

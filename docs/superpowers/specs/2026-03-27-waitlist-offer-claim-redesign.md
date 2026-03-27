# Waitlist Offer Accept: Claim-First via Domain Service

## Problem

The current waitlist offer accept flow creates a Pending booking, then asynchronously attempts to claim slots on the opening, then confirms or rejects the booking via a saga. This introduces:

- 4 transactions and 3 handlers before the golfer knows the outcome
- A `Pending` booking state that exists only as an infrastructure artifact (not a business concept in the waitlist flow)
- A `BookingConfirmationPolicy` saga to bridge the gap between booking creation and claim result
- A window between offer acceptance and claim attempt where slots can be stolen
- A "Processing..." response to the golfer instead of immediate confirmation

## Solution

Move the slot claim into the offer acceptance action via a domain service. The golfer's action atomically claims slots on the opening and accepts/rejects the offer in a single transaction, with immediate HTTP feedback.

## Domain Service: WaitlistOfferClaimService

A new domain service in `Shadowbrook.Domain` coordinates the offer-accept-claim operation:

```csharp
public class WaitlistOfferClaimService(ITimeProvider timeProvider)
{
    public ClaimResult AcceptOffer(WaitlistOffer offer, TeeTimeOpening opening)
    {
        var result = opening.TryClaim(offer.BookingId, offer.GolferId, offer.GroupSize, timeProvider);

        if (result.Success)
            offer.Accept();
        else
            offer.Reject(result.Reason);

        return result;
    }
}
```

- Lives in Domain layer (zero infrastructure dependencies)
- Receives already-loaded aggregates as parameters (the endpoint/handler loads them)
- Pure domain logic: "if claim succeeds, accept offer; if claim fails, reject offer"

## WaitlistOffer Changes

- Add `BookingId` property (UUIDv7, generated at offer creation time in `GolferWaitlistEntry.CreateOffer()`)
- `Accept()` becomes `internal` — only callable within the Domain assembly via the domain service
- `Reject()` stays public — called from timeout handlers, opening filled/cancelled handlers
- `WaitlistOfferCreated` event includes `BookingId`
- `WaitlistOfferAccepted` event includes `BookingId`

## Booking Aggregate Changes

- Add `Booking.CreateConfirmed(...)` factory method — creates a booking in Confirmed status, raises `BookingConfirmed`
- `Booking.Create()` stays unchanged for future use (direct bookings that need Pending → Confirmed flow with payment validation, etc.)
- Remove `GolferName` property — read concerns join to the Golfer aggregate instead

## Endpoint Changes

The accept endpoint becomes synchronous:

```csharp
[WolverinePost("/waitlist/offers/{token}/accept")]
public static async Task<IResult> Accept(string token, ...)
{
    var offer = await offerRepo.GetByTokenAsync(token);
    var opening = await openingRepo.GetRequiredByIdAsync(offer.OpeningId);

    var result = claimService.AcceptOffer(offer, opening);

    return result.Success
        ? Results.Ok(new { status = "Confirmed" })
        : Results.Conflict(new { reason = result.Reason });
}
```

- 200 means confirmed, 409 means slot taken
- Wolverine's transactional middleware saves both aggregates and publishes all events in one transaction

## Event Renames

- `TeeTimeOpeningClaimed` → `TeeTimeOpeningSlotsClaimed`
- `TeeTimeOpeningClaimRejected` → `TeeTimeOpeningSlotsClaimRejected`

Note: `BookingCreated` event type remains in the codebase for future direct booking flows — it is simply not raised in the waitlist acceptance path.

## Downstream Handler Changes

### Eliminated

- `BookingConfirmationPolicy` — no Pending → Confirmed saga needed
- `Bookings/Handlers/WaitlistOfferAccepted/CreateBookingHandler` — booking no longer created from offer acceptance
- `Waitlist/Handlers/BookingCreated/ClaimHandler` — claim happens in the domain service
- `Waitlist/Handlers/WaitlistOfferAccepted/SmsHandler` — replaced by confirmation SMS on claim

### New Handlers for TeeTimeOpeningSlotsClaimed

- **CreateConfirmedBookingHandler** — calls `Booking.CreateConfirmed(...)`, raises `BookingConfirmed`
- **SendConfirmationSmsHandler** — sends golfer a confirmation SMS

### Modified Handlers for WaitlistOfferAccepted

- **RemoveFromWaitlistHandler** — stays as-is, removes the waitlist entry

### Unchanged

- `TeeTimeOpeningOfferPolicy` — still listens for `WaitlistOfferAccepted` (decrement PendingOfferCount) and `TeeTimeOpeningSlotsClaimed` (decrement SlotsRemaining). Both events fire from the same transaction now but the saga handles them independently.
- `WaitlistOfferResponsePolicy` — still manages offer timeouts. `WaitlistOfferAccepted` or `WaitlistOfferRejected` completes the policy.
- `TeeTimeOpeningFilled/RejectOffersHandler` — unchanged
- `TeeTimeOpeningCancelled/RejectOffersHandler` — unchanged

## Event Flow (After)

```
POST /waitlist/offers/{token}/accept
  → Endpoint loads offer + opening
  → claimService.AcceptOffer(offer, opening)
    → opening.TryClaim()  [raises TeeTimeOpeningSlotsClaimed or SlotsClaimRejected]
    → offer.Accept() or offer.Reject()  [raises WaitlistOfferAccepted or Rejected]
  → Return 200 (Confirmed) or 409 (Conflict)

If successful, single transaction publishes:
  TeeTimeOpeningSlotsClaimed
    → CreateConfirmedBookingHandler → Booking.CreateConfirmed() [raises BookingConfirmed]
    → SendConfirmationSmsHandler → SMS to golfer
  WaitlistOfferAccepted
    → RemoveFromWaitlistHandler → entry.Remove()
  TeeTimeOpeningFilled (if last slot)
    → RejectOffersHandler → reject remaining pending offers
```

## DDD Justification

This design modifies two aggregates (WaitlistOffer and TeeTimeOpening) in one transaction, which deviates from the strict one-aggregate-per-transaction rule. This is justified because:

- Both aggregates are in the same bounded context and same database
- The operation is logically atomic — you cannot "accept" an offer without claiming the slot
- Eventual consistency creates a worse problem: telling the golfer "processing" when we could tell them "confirmed" or "taken"
- Vernon, Bogard, and Microsoft's eShop reference architecture all endorse this pragmatic compromise for same-context, same-DB operations
- The domain service encapsulates the coordination, keeping it in the domain layer rather than leaking into application code
- `WaitlistOffer.Accept()` is `internal`, enforcing that the coordinated path through the domain service is the only way to accept an offer

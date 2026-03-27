# Late Claim on Stale Offers

**Issue:** #198 — Walk-up golfer can still claim a slot after the response window if no one else has taken it
**Date:** 2026-03-27

## Problem

When the offer response window expires (60s for walk-up, 10m for online), the system hard-rejects the offer via `RejectStaleOffer`. If the golfer clicks the claim link after this point, the offer is already in `Rejected` status and cannot be claimed — even if the slot is still available.

## Design

Replace hard-reject on timeout with a "mark stale" flow. The offer stays in `Pending` status but is flagged as stale. This lets the cascade continue (next golfer gets notified) while keeping the original offer claimable.

### Domain: `WaitlistOffer`

- Add `IsStale` boolean property (default `false`).
- Add `MarkStale()` method: sets `IsStale = true`, raises `WaitlistOfferStale` event.
- Offer remains `Pending` — `Accept()` and `Reject()` transitions still work on stale offers.

### Command: `MarkOfferStale` (replaces `RejectStaleOffer`)

- Rename `RejectStaleOffer` command to `MarkOfferStale`.
- Handler calls `offer.MarkStale()` instead of `offer.Reject()`.
- If offer is no longer `Pending` (already accepted/rejected), no-op with warning log.

### Saga: `WaitlistOfferResponsePolicy`

- Timeout handler returns `MarkOfferStale` command instead of `RejectStaleOffer`.
- Must handle `WaitlistOfferAccepted` for late claims (stale offers accepted after timeout) to mark saga complete.

### Saga: `TeeTimeOpeningOfferPolicy`

- Already handles `WaitlistOfferStale` to decrement `PendingOfferCount` and cascade to next golfer. No changes needed.

### Claim Flow

- `WaitlistOfferClaimService.AcceptOffer` — no changes. Works on any `Pending` offer regardless of `IsStale` flag. `TryClaim` on the opening handles concurrency (slots remaining check).
- Accept endpoint — no changes. Looks up offer by token, delegates to claim service.

### SMS

- Success: same confirmation as normal claim.
- Failure (slot taken): same rejection SMS. The `WaitlistOfferRejected` SMS handler already covers this.

### What stays the same

- `TryClaim` on `TeeTimeOpening` — natural concurrency handling via slots remaining.
- `FindAndOfferEligibleGolfers` cascade — already triggered by `WaitlistOfferStale` event.
- Waitlist entry lifecycle — entry only removed on successful accept, so failed late claims leave the golfer on the queue.
- All SMS message content — no distinction between late and on-time claims from the golfer's perspective.

## Acceptance Criteria Mapping

**Late Claim — Slot Still Available:** Golfer clicks link after window expires. Offer is `Pending` + `IsStale`. `AcceptOffer` calls `TryClaim`, slots remain, claim succeeds. Golfer gets confirmation SMS.

**Late Claim — Slot Already Taken:** Golfer clicks link after window expires. Offer is `Pending` + `IsStale`. `AcceptOffer` calls `TryClaim`, no slots remain, claim fails. Offer rejected. Golfer gets "slot no longer available" SMS. Entry stays on waitlist.

## Non-goals

- Recalling in-flight offers to other golfers when a late claim succeeds — `TryClaim` handles concurrency naturally.
- Different SMS messaging for late vs on-time claims — golfer doesn't need to know.
- New offer status — `IsStale` is a flag, not a status.

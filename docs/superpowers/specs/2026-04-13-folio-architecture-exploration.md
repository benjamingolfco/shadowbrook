# Folio Architecture Exploration

**Date:** 2026-04-13
**Status:** Exploration (not a committed design — captures architectural thinking for future reference)
**Related:** #401 (rate schedules), future billing/POS work

## Context

While designing rate schedules (#401), we explored how pricing data flows from the scheduling domain through to payment at the POS. The rate schedules spec handles price resolution and booking-time lock-in. This document captures the architectural thinking about what happens after that — how charges from multiple domains accumulate into a payable total.

## The Problem

A golfer's visit produces charges from multiple bounded contexts:

- **Scheduling** — green fees (price per player x group size)
- **Operations** (future) — cart rentals, range balls
- **Food & Beverage** (future) — pro shop, restaurant

These charges need to accumulate under a single BookingId and be settled together at the POS. The system uses Wolverine with durable local queues and SQL Server transport — domain events are processed asynchronously in separate transactions, not in-band with the originating request.

## The Folio Pattern

A folio is an open ledger that accumulates line items over time. It does not coordinate or wait for charges — it just collects them. Settlement is an explicit operator action.

### Lifecycle

```
Open → (charges accumulate) → Settled → (late charges) → Adjusted
```

- **Open** — created when the first charge arrives. Line items append freely.
- **Settled** — operator triggers payment at POS. Folio freezes. Payment recorded.
- **Adjusted** — charges arriving after settlement. Handled via stored payment method, member balance, or manual resolution.

### Why Not a Saga

A scatter-gather saga requires knowing how many events to expect. A golfer's visit is open-ended — they might rent a cart 30 minutes after booking. The folio pattern accepts charges at any time without a completeness gate.

## The Async Gap

Because Wolverine handlers run asynchronously (durable local queues), there is a real timing gap between a command completing and its domain event being processed:

```
T+0ms      POST /book → booking created, TeeTimeClaimed written to outbox → 200
T+0ms      UI gets response. Folio has nothing yet.
T+50-500ms Wolverine worker processes TeeTimeClaimed → folio line item created
```

### Mitigation: Pending Line Items

The endpoint that creates the charge writes a pending folio line item synchronously, in the same transaction as the domain write. The async event handler later confirms it.

```
T+0ms      POST /book:
             - Creates booking
             - Writes FolioLineItem { status: Pending, amount: $135 }
             - TeeTimeClaimed written to outbox
           → 200 returned, UI shows pending line item immediately

T+200ms    Wolverine handler processes TeeTimeClaimed
             - Confirms pending line item → status: Confirmed
```

The UI always has something to show. The dollar amount is correct from T+0 because the endpoint knows the price (it read it from the interval). "Pending" means "async handlers haven't finished downstream work," not "price unknown."

### Settlement Doesn't Wait

Settlement can proceed with pending items. The amounts are accurate — only the confirmation status differs. The payment terminal receives the total regardless of line item status.

## Cross-Domain Charge Composition

### Interface Pattern (Modular Monolith — Current Architecture)

Each domain module implements a shared interface:

```csharp
public interface IProvideFolioCharges
{
    Task<FolioLineItem?> GetCharge(Guid bookingId);
}
```

In the monolith, providers query the shared database. DI discovers all implementations via `IEnumerable<IProvideFolioCharges>`.

### NuGet Package Pattern (Separate Services — Future)

Each bounded context ships a class library (NuGet package) containing its `IProvideFolioCharges` implementation. The implementation contains an HTTP client that calls back to its own service's API — it does not connect to another service's database.

```
Scheduling.FolioComposition (NuGet package)
  └── SchedulingFolioChargeProvider : IProvideFolioCharges
        └── HttpClient → calls Scheduling API → /api/bookings/{id}/charge
```

The folio service installs all provider packages. DI discovers them. At assembly time, all providers run in parallel via `Task.WhenAll`.

### Transition Path

The interface is the seam. In the monolith, the implementation uses DbContext. In separate services, the implementation uses HttpClient. The folio code doesn't change.

```
Monolith:   IProvideFolioCharges → DbContext → shared DB
Split:      IProvideFolioCharges → HttpClient → service API → separate DB
```

## Service Unavailability

When a provider fails (service down, timeout), the folio returns partial results with a completeness flag:

- UI shows available charges with a warning for unavailable services
- Operator chooses: retry, or settle without the missing charges
- Late charges after settlement become adjustments

This matches how hotel PMS systems handle the "minibar charge after checkout" problem — charge the card on file or add to the member's running balance.

## Duplicate Charge Prevention

Risk: if the POS writes a pending line item synchronously AND the async event handler also writes a line item, the folio gets a double charge.

Mitigation: each line item has a deterministic idempotency key (`{BookingId}:{ChargeType}:{SourceId}`). The async handler uses upsert semantics — if a pending item with that key exists, it confirms it rather than creating a duplicate.

## POS Flows

### Walk-up (Synchronous)

Operator rings up everything in one session. Each action is a separate HTTP request, each writes a pending line item synchronously. By the time the operator clicks "Pay," all line items are present.

### Call-ahead (Asynchronous)

Golfer books online. Charges accumulate over hours via domain events. When the golfer arrives, the POS shows the assembled folio. Additional charges (cart, food) are added at the counter.

### Either Flow

The folio data model is the same. Only the timing of charge arrival differs.

## Relationship to Rate Schedules (#401)

The rate schedules spec defines how `TeeTimeClaim.Price` is resolved and captured. This is the "green fee" line item source. The folio pattern describes where that price ends up:

```
CoursePricingSettings → resolves price
  → TeeSheetInterval.Price (live, mutable)
    → TeeTimeClaim.Price (snapshot, immutable)
      → Booking.PricePerPlayer + TotalPrice (current spec, interim)
        → FolioLineItem (future, when folio is introduced)
```

For #401, `Booking.PricePerPlayer` and `TotalPrice` are the right interim step. When the folio is introduced, these fields are replaced by folio line items and the booking becomes purely a reservation.

## Key Sources

- Mauro Servienti — ViewModel Composition (ServiceComposer.AspNetCore)
- Martin Fowler — Accounting Entry pattern, Event Sourcing
- Particular Software — IT/Ops pattern, saga vs process manager
- Hotel PMS systems (Mews, OPERA) — folio lifecycle, late charges
- Lightspeed Golf — member accounts, tee sheet to POS charge flow
- Airbnb Engineering — idempotency in distributed payments
- Mercari Engineering — reconciliation in microservices

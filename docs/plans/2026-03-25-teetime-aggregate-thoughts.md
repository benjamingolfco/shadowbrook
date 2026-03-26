# TeeTime Aggregate — Future Design Thoughts

**Status:** Brainstorm notes — not a spec, not ready for implementation.

**Date:** 2026-03-25

## Context

During the walk-up waitlist rework, we identified that the current domain conflates two separate concerns:

1. **Walk-up waitlist** — Operator manually announces openings, offer policy finds golfers from the waitlist. No tee sheet, no booking lifecycle. This is the phase-one, "I need a golfer" mechanism.

2. **TeeTime booking system** — Full tee sheet with TeeTime as an aggregate root, bookings as children, cancellations that re-open slots, and the complete lifecycle.

These are different systems. The walk-up waitlist should be cleaned up on its own terms, not forced into a TeeTime aggregate model that doesn't exist yet.

## TeeTime Aggregate — How It Might Work

### Core Idea

The TeeTime is the source of truth for a specific slot on the tee sheet. It knows its capacity, who's booked, and how many slots remain. Everything else reacts to its state.

### Aggregate Shape

- **TeeTime** (aggregate root)
  - Id, CourseId, Date, Time, Capacity (typically 4)
  - Child collection of **TeeTimeBooking** (GolferId, GroupSize, BookedAt, CancelledAt)
  - `RemainingSlots` — calculated from capacity minus active bookings
  - `Book(golfer, groupSize)` — creates a booking, raises `TeeTimeBookingCreated`; if full, raises `TeeTimeFilled`
  - `CancelBooking(bookingId)` — cancels a booking, raises `TeeTimeBookingCancelled`; if slots opened, raises `TeeTimeOpened`

### Key Events

| Event | When |
|-------|------|
| `TeeTimeBookingCreated` | Golfer books slot(s) |
| `TeeTimeFilled` | All slots occupied |
| `TeeTimeOpened` | Cancellation frees slot(s) — triggers offer flow |
| `TeeTimeBookingCancelled` | A booking is cancelled |

### Cancellation Model

You don't cancel a TeeTime — you cancel a **booking on a TeeTime**. The TeeTime itself persists. When a cancellation opens slots, `TeeTimeOpened` fires and downstream systems (waitlist offer policy, notifications) react.

### Offer Policy Integration

A `TeeTimeOpeningOfferPolicy` (Wolverine saga) would:
- Start on `TeeTimeOpened` (slots became available)
- Cycle through eligible waitlist golfers with offers
- Handle `TeeTimeFilled` to stop offering
- Handle `TeeTimeOpened` again if more cancellations happen while the policy is active
- The TeeTime aggregate is the single source of truth, so the policy can always check real availability before sending an SMS

### Sources of TeeTime Creation

Multiple sources can create or populate TeeTime aggregates:
- **Tee sheet configuration** — Course defines intervals, TeeTime entities generated per day
- **Operator manual entry** — Same as today's walk-up flow, but creating a real TeeTime
- **External integrations** — Sync from existing tee sheet software

### Open Questions

- How does the tee sheet define TeeTime intervals? (Course-level config? Per-day override?)
- How do TeeTime entities get created for a given day? (Lazy on first access? Batch generation?)
- How does the TeeTime aggregate relate to pricing, player types, restrictions?
- Should TeeTimeBooking be its own aggregate or always a child of TeeTime?

## Separation Decision

The walk-up waitlist rework proceeds independently of this design. The walk-up waitlist is a self-contained feature with its own domain model. When the TeeTime aggregate is built later, the walk-up waitlist may integrate with it or be superseded — but that's a future concern.

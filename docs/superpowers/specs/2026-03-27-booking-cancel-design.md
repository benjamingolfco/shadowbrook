# Booking.Cancel() — Design Spec

## Context

When a tee time opening is cancelled by a course operator, all associated bookings (Pending and Confirmed) need to be voided. The current implementation calls `Booking.Reject()`, but rejection and cancellation are semantically different:

- **Reject** — "we reviewed your booking and said no" (confirmation timeout, explicit rejection)
- **Cancel** — "the tee time no longer exists, so the booking is void"

This distinction matters for golfer-facing messaging (SMS) and for domain clarity.

## Domain Model Changes

### BookingStatus enum

Add `Cancelled` value:

```
Pending, Confirmed, Rejected, Cancelled
```

### Booking.Cancel() method

- Transitions from `Pending` or `Confirmed` to `Cancelled`
- Idempotent on `Cancelled` (silent return)
- Throws `BookingNotCancellableException` if `Rejected` (terminal state — should not happen in correct flows)
- Raises `BookingCancelled` domain event

### BookingCancelled event

New domain event record following the existing pattern:

```
record BookingCancelled : IDomainEvent
  EventId: Guid (default NewGuid)
  OccurredAt: DateTimeOffset (default UtcNow)
  BookingId: Guid (required)
```

### BookingNotCancellableException

New domain exception for attempting to cancel a Rejected booking. Maps to 400 in the global exception handler.

## Handler Changes

### CancelBookingsHandler (reacts to TeeTimeOpeningCancelled)

Currently filters to Pending bookings and calls `Reject()`. Updated behavior:

- Fetch all bookings for the course/date/tee time
- Filter out terminal states (Rejected, Cancelled)
- Call `Cancel()` on remaining bookings (Pending and Confirmed)
- Log the count of cancelled bookings

### What stays the same

- `Booking.Reject()` — still used by `BookingConfirmationPolicy` for timeout/explicit rejection
- `RejectBookingCommand` / `RejectBookingHandler` — untouched
- `BookingRejected` event — still raised by `Reject()`

## Downstream Enablement

`BookingCancelled` is a new event that enables future handlers:

- SMS notification: "Your tee time has been cancelled by the course"
- Analytics: distinguish cancellations from rejections

No SMS handler is included in this scope — just the event.

## Testing Strategy

### Domain Unit Tests (BookingTests.cs)

- `Cancel_WhenPending_TransitionsToCancelled` — status change, event raised
- `Cancel_WhenConfirmed_TransitionsToCancelled` — status change, event raised
- `Cancel_WhenAlreadyCancelled_IsIdempotent` — no exception, no duplicate event
- `Cancel_WhenRejected_ThrowsBookingNotCancellableException`

### Handler Unit Tests (TeeTimeOpeningCancelledCancelBookingsHandlerTests.cs)

- Update existing tests to use `Cancel()` instead of `Reject()`
- `Handle_WhenPendingAndConfirmedBookings_CancelsAll`
- `Handle_WhenOnlyTerminalBookings_DoesNothing`
- `Handle_WhenMixOfStates_CancelsOnlyNonTerminal`

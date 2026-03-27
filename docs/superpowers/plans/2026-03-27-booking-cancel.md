# Booking.Cancel() Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `Cancel()` method to the Booking aggregate that transitions Pending or Confirmed bookings to Cancelled, distinct from Reject which is for denied booking requests.

**Architecture:** New `Cancelled` status, `BookingCancelled` event, and `BookingNotCancellableException`. The existing `CancelBookingsHandler` (reacts to `TeeTimeOpeningCancelled`) is updated to filter out terminal states and call `Cancel()` instead of `Reject()`.

**Tech Stack:** .NET 10, xUnit, NSubstitute

**Spec:** `docs/superpowers/specs/2026-03-27-booking-cancel-design.md`

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `src/backend/Shadowbrook.Domain/BookingAggregate/BookingStatus.cs` | Add `Cancelled` enum value |
| Create | `src/backend/Shadowbrook.Domain/BookingAggregate/Events/BookingCancelled.cs` | New domain event |
| Create | `src/backend/Shadowbrook.Domain/BookingAggregate/Exceptions/BookingNotCancellableException.cs` | Exception for cancelling a Rejected booking |
| Modify | `src/backend/Shadowbrook.Domain/BookingAggregate/Booking.cs` | Add `Cancel()` method |
| Modify | `src/backend/Shadowbrook.Api/Infrastructure/Middleware/DomainExceptionHandler.cs` | Map new exception to 409 |
| Modify | `src/backend/Shadowbrook.Api/Features/Bookings/Handlers/TeeTimeOpeningCancelled/CancelBookingsHandler.cs` | Use `Cancel()`, filter non-terminal bookings |
| Modify | `tests/Shadowbrook.Domain.Tests/BookingAggregate/BookingTests.cs` | Cancel() domain tests |
| Modify | `tests/Shadowbrook.Api.Tests/Handlers/TeeTimeOpeningCancelledCancelBookingsHandlerTests.cs` | Updated handler tests |

---

### Task 1: Domain — BookingCancelled event and BookingNotCancellableException

**Files:**
- Modify: `src/backend/Shadowbrook.Domain/BookingAggregate/BookingStatus.cs`
- Create: `src/backend/Shadowbrook.Domain/BookingAggregate/Events/BookingCancelled.cs`
- Create: `src/backend/Shadowbrook.Domain/BookingAggregate/Exceptions/BookingNotCancellableException.cs`

- [ ] **Step 1: Add `Cancelled` to BookingStatus**

```csharp
// src/backend/Shadowbrook.Domain/BookingAggregate/BookingStatus.cs
namespace Shadowbrook.Domain.BookingAggregate;

public enum BookingStatus
{
    Pending,
    Confirmed,
    Rejected,
    Cancelled
}
```

- [ ] **Step 2: Create BookingCancelled event**

```csharp
// src/backend/Shadowbrook.Domain/BookingAggregate/Events/BookingCancelled.cs
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.BookingAggregate.Events;

public record BookingCancelled : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid BookingId { get; init; }
}
```

- [ ] **Step 3: Create BookingNotCancellableException**

```csharp
// src/backend/Shadowbrook.Domain/BookingAggregate/Exceptions/BookingNotCancellableException.cs
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.BookingAggregate.Exceptions;

public class BookingNotCancellableException(Guid bookingId, BookingStatus currentStatus)
    : DomainException($"Cannot cancel booking {bookingId} — status is {currentStatus}, only Pending or Confirmed bookings can be cancelled.");
```

- [ ] **Step 4: Verify build**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded, 0 errors

- [ ] **Step 5: Commit**

```bash
git add src/backend/Shadowbrook.Domain/BookingAggregate/BookingStatus.cs \
  src/backend/Shadowbrook.Domain/BookingAggregate/Events/BookingCancelled.cs \
  src/backend/Shadowbrook.Domain/BookingAggregate/Exceptions/BookingNotCancellableException.cs
git commit -m "feat(domain): add Cancelled status, BookingCancelled event, and BookingNotCancellableException"
```

---

### Task 2: Domain — Booking.Cancel() method with tests (TDD)

**Files:**
- Modify: `tests/Shadowbrook.Domain.Tests/BookingAggregate/BookingTests.cs`
- Modify: `src/backend/Shadowbrook.Domain/BookingAggregate/Booking.cs`

- [ ] **Step 1: Write failing tests for Cancel()**

Add these tests to `BookingTests.cs`:

```csharp
[Fact]
public void Cancel_FromPending_TransitionsToCancelledAndRaisesEvent()
{
    var bookingId = Guid.CreateVersion7();
    var booking = Booking.Create(
        bookingId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 6, 15),
        new TimeOnly(9, 0),
        "Jane Smith",
        1);

    booking.Cancel();

    Assert.Equal(BookingStatus.Cancelled, booking.Status);
    var cancelledEvent = Assert.IsType<BookingCancelled>(booking.DomainEvents.Last());
    Assert.Equal(bookingId, cancelledEvent.BookingId);
}

[Fact]
public void Cancel_FromConfirmed_TransitionsToCancelledAndRaisesEvent()
{
    var bookingId = Guid.CreateVersion7();
    var booking = Booking.Create(
        bookingId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 6, 15),
        new TimeOnly(9, 0),
        "Jane Smith",
        1);

    booking.Confirm();
    booking.Cancel();

    Assert.Equal(BookingStatus.Cancelled, booking.Status);
    var cancelledEvent = Assert.IsType<BookingCancelled>(booking.DomainEvents.Last());
    Assert.Equal(bookingId, cancelledEvent.BookingId);
}

[Fact]
public void Cancel_WhenAlreadyCancelled_IsIdempotent()
{
    var booking = Booking.Create(
        Guid.CreateVersion7(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 6, 15),
        new TimeOnly(9, 0),
        "Jane Smith",
        1);

    booking.Cancel();
    var eventsAfterFirst = booking.DomainEvents.Count;

    booking.Cancel();

    Assert.Equal(BookingStatus.Cancelled, booking.Status);
    Assert.Equal(eventsAfterFirst, booking.DomainEvents.Count);
}

[Fact]
public void Cancel_WhenRejected_ThrowsBookingNotCancellableException()
{
    var booking = Booking.Create(
        Guid.CreateVersion7(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 6, 15),
        new TimeOnly(9, 0),
        "Jane Smith",
        1);

    booking.Reject();

    Assert.Throws<BookingNotCancellableException>(() => booking.Cancel());
}
```

Add `using Shadowbrook.Domain.BookingAggregate.Events;` to the test file if not already present (it is — `BookingCancelled` is in the same namespace as `BookingCreated`).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Shadowbrook.Domain.Tests --filter "BookingTests.Cancel" --no-build 2>&1 || true`
Expected: Build error — `Booking` does not contain a definition for `Cancel`

- [ ] **Step 3: Implement Cancel() on Booking**

Add this method to `src/backend/Shadowbrook.Domain/BookingAggregate/Booking.cs` after the `Reject()` method:

```csharp
public void Cancel()
{
    if (Status == BookingStatus.Cancelled)
    {
        return;
    }

    if (Status == BookingStatus.Rejected)
    {
        throw new BookingNotCancellableException(Id, Status);
    }

    Status = BookingStatus.Cancelled;
    AddDomainEvent(new BookingCancelled { BookingId = Id });
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Shadowbrook.Domain.Tests --filter "BookingTests" -v minimal`
Expected: All tests pass (existing + 4 new Cancel tests)

- [ ] **Step 5: Commit**

```bash
git add src/backend/Shadowbrook.Domain/BookingAggregate/Booking.cs \
  tests/Shadowbrook.Domain.Tests/BookingAggregate/BookingTests.cs
git commit -m "feat(domain): add Booking.Cancel() for Pending and Confirmed bookings"
```

---

### Task 3: API — Wire up exception handler and update CancelBookingsHandler

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Middleware/DomainExceptionHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/Features/Bookings/Handlers/TeeTimeOpeningCancelled/CancelBookingsHandler.cs`
- Modify: `tests/Shadowbrook.Api.Tests/Handlers/TeeTimeOpeningCancelledCancelBookingsHandlerTests.cs`

- [ ] **Step 1: Add BookingNotCancellableException to DomainExceptionHandler**

In `src/backend/Shadowbrook.Api/Infrastructure/Middleware/DomainExceptionHandler.cs`, add to the switch expression (after the `BookingNotPendingException` line):

```csharp
BookingNotCancellableException => StatusCodes.Status409Conflict,
```

Add the using if not already covered by the existing `Shadowbrook.Domain.BookingAggregate.Exceptions` import (it is — same namespace).

- [ ] **Step 2: Rewrite handler tests**

Replace the contents of `tests/Shadowbrook.Api.Tests/Handlers/TeeTimeOpeningCancelledCancelBookingsHandlerTests.cs`:

```csharp
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shadowbrook.Api.Features.Bookings.Handlers;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class TeeTimeOpeningCancelledCancelBookingsHandlerTests
{
    private readonly IBookingRepository bookingRepository = Substitute.For<IBookingRepository>();
    private readonly ILogger<TeeTimeOpeningCancelledCancelBookingsHandler> logger = Substitute.For<ILogger<TeeTimeOpeningCancelledCancelBookingsHandler>>();
    private readonly TeeTimeOpeningCancelledCancelBookingsHandler handler;

    public TeeTimeOpeningCancelledCancelBookingsHandlerTests()
    {
        this.handler = new TeeTimeOpeningCancelledCancelBookingsHandler(this.bookingRepository, this.logger);
    }

    private static TeeTimeOpeningCancelled CreateEvent() => new()
    {
        OpeningId = Guid.NewGuid(),
        CourseId = Guid.NewGuid(),
        Date = new DateOnly(2026, 6, 1),
        TeeTime = new TimeOnly(9, 0),
    };

    private static Booking CreateBooking(TeeTimeOpeningCancelled evt) =>
        Booking.Create(Guid.NewGuid(), evt.CourseId, Guid.NewGuid(), evt.Date, evt.TeeTime, "Jane Doe", 2);

    [Fact]
    public async Task Handle_WhenPendingAndConfirmedBookings_CancelsAll()
    {
        var evt = CreateEvent();
        var pending = CreateBooking(evt);
        var confirmed = CreateBooking(evt);
        confirmed.Confirm();

        this.bookingRepository.GetByCourseAndTeeTimeAsync(evt.CourseId, evt.Date, evt.TeeTime, Arg.Any<CancellationToken>())
            .Returns([pending, confirmed]);

        await this.handler.Handle(evt, CancellationToken.None);

        Assert.Equal(BookingStatus.Cancelled, pending.Status);
        Assert.Equal(BookingStatus.Cancelled, confirmed.Status);
    }

    [Fact]
    public async Task Handle_WhenNoBookingsExist_LogsWarningAndReturns()
    {
        var evt = CreateEvent();

        this.bookingRepository.GetByCourseAndTeeTimeAsync(evt.CourseId, evt.Date, evt.TeeTime, Arg.Any<CancellationToken>())
            .Returns([]);

        await this.handler.Handle(evt, CancellationToken.None);

        await this.bookingRepository.Received(1).GetByCourseAndTeeTimeAsync(evt.CourseId, evt.Date, evt.TeeTime, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOnlyTerminalBookings_DoesNothing()
    {
        var evt = CreateEvent();
        var rejected = CreateBooking(evt);
        rejected.Reject();
        var cancelled = CreateBooking(evt);
        cancelled.Cancel();

        this.bookingRepository.GetByCourseAndTeeTimeAsync(evt.CourseId, evt.Date, evt.TeeTime, Arg.Any<CancellationToken>())
            .Returns([rejected, cancelled]);

        await this.handler.Handle(evt, CancellationToken.None);

        Assert.Equal(BookingStatus.Rejected, rejected.Status);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task Handle_WhenMixOfStates_CancelsOnlyNonTerminal()
    {
        var evt = CreateEvent();
        var pending = CreateBooking(evt);
        var confirmed = CreateBooking(evt);
        confirmed.Confirm();
        var rejected = CreateBooking(evt);
        rejected.Reject();

        this.bookingRepository.GetByCourseAndTeeTimeAsync(evt.CourseId, evt.Date, evt.TeeTime, Arg.Any<CancellationToken>())
            .Returns([pending, confirmed, rejected]);

        await this.handler.Handle(evt, CancellationToken.None);

        Assert.Equal(BookingStatus.Cancelled, pending.Status);
        Assert.Equal(BookingStatus.Cancelled, confirmed.Status);
        Assert.Equal(BookingStatus.Rejected, rejected.Status);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "TeeTimeOpeningCancelledCancelBookings" 2>&1 || true`
Expected: Tests fail because handler still calls `Reject()` and filters to Pending only

- [ ] **Step 4: Update CancelBookingsHandler**

Replace the contents of `src/backend/Shadowbrook.Api/Features/Bookings/Handlers/TeeTimeOpeningCancelled/CancelBookingsHandler.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public class TeeTimeOpeningCancelledCancelBookingsHandler(IBookingRepository bookingRepository, ILogger<TeeTimeOpeningCancelledCancelBookingsHandler> logger)
{
    private static readonly BookingStatus[] TerminalStatuses = [BookingStatus.Rejected, BookingStatus.Cancelled];

    public async Task Handle(TeeTimeOpeningCancelled domainEvent, CancellationToken ct)
    {
        var bookings = await bookingRepository.GetByCourseAndTeeTimeAsync(
            domainEvent.CourseId,
            domainEvent.Date,
            domainEvent.TeeTime,
            ct);

        if (bookings.Count == 0)
        {
            logger.LogWarning(
                "No bookings found for cancelled opening {OpeningId} (Course: {CourseId}, Date: {Date}, TeeTime: {TeeTime})",
                domainEvent.OpeningId,
                domainEvent.CourseId,
                domainEvent.Date,
                domainEvent.TeeTime);
            return;
        }

        var activeBookings = bookings.Where(b => !TerminalStatuses.Contains(b.Status)).ToList();

        if (activeBookings.Count == 0)
        {
            logger.LogInformation(
                "No active bookings to cancel for cancelled opening {OpeningId} (found {TotalCount} bookings, all terminal)",
                domainEvent.OpeningId,
                bookings.Count);
            return;
        }

        foreach (var booking in activeBookings)
        {
            booking.Cancel();
        }

        logger.LogInformation(
            "Cancelled {CancelledCount} booking(s) for cancelled opening {OpeningId}",
            activeBookings.Count,
            domainEvent.OpeningId);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "TeeTimeOpeningCancelledCancelBookings" -v minimal`
Expected: All 4 tests pass

- [ ] **Step 6: Run full build and test suite**

Run: `dotnet build shadowbrook.slnx && dotnet test shadowbrook.slnx -v minimal`
Expected: Build succeeded, all tests pass

- [ ] **Step 7: Commit**

```bash
git add src/backend/Shadowbrook.Api/Infrastructure/Middleware/DomainExceptionHandler.cs \
  src/backend/Shadowbrook.Api/Features/Bookings/Handlers/TeeTimeOpeningCancelled/CancelBookingsHandler.cs \
  tests/Shadowbrook.Api.Tests/Handlers/TeeTimeOpeningCancelledCancelBookingsHandlerTests.cs
git commit -m "feat: update CancelBookingsHandler to use Booking.Cancel() instead of Reject()"
```

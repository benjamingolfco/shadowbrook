# Waitlist Offer Saga Refactoring — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Refactor the waitlist offer acceptance flow from coupled single-transaction to a decoupled event-driven saga. Remove duplicated data from WaitlistOffer, promote GolferWaitlistEntry and Booking to independent aggregates, add Fill/Unfill to TeeTimeRequest, and implement sequential event chain with compensation.

**Architecture:** Each saga step is a single-aggregate transaction raising an event. Events carry identifiers only. Handlers look up data they need. Compensation flows walk backward on failure. Read models are separate from domain models (CQRS-lite).

**Tech Stack:** .NET 10, EF Core 10, xUnit

**Design doc:** `docs/plans/2026-03-16-waitlist-offer-saga-design.md`

**Conventions:** `.claude/rules/backend/backend-conventions.md`

---

## Important Patterns

Before implementing, read these files for established patterns:
- `src/backend/Shadowbrook.Domain/WalkUpWaitlistAggregate/WalkUpWaitlist.cs` — aggregate root pattern
- `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/TeeTimeRequest.cs` — factory method, domain events
- `src/backend/Shadowbrook.Domain/Common/Entity.cs` — base class
- `src/backend/Shadowbrook.Domain/Common/DomainException.cs` — exception base
- `src/backend/Shadowbrook.Api/Infrastructure/Repositories/WalkUpWaitlistRepository.cs` — repository pattern
- `src/backend/Shadowbrook.Api/EventHandlers/` — event handler pattern
- `.claude/rules/backend/backend-conventions.md` — DDD conventions (aggregate boundaries, saga pattern, result objects, CQRS-lite)

Key rules:
- All aggregate properties use `private set`
- Private parameterless constructor for EF
- Static factory methods for construction
- `Guid.CreateVersion7()` for all new IDs
- Events carry identifiers only — handlers look up data
- `internal` methods for cross-aggregate operations within Domain assembly
- Result objects (not exceptions) for cross-aggregate internal methods
- Read models separate from domain models
- Domain event handlers in `EventHandlers/` folder (sibling to `Infrastructure/`)
- Run `dotnet build shadowbrook.slnx` after every change set
- Run `dotnet test shadowbrook.slnx -v minimal` before every commit

---

### Task 1: Promote Booking to Domain Aggregate

Booking is currently an anemic model in `Api/Models/Booking.cs`. Promote it to a proper domain aggregate.

**Files:**
- Create: `src/backend/Shadowbrook.Domain/BookingAggregate/Booking.cs`
- Create: `src/backend/Shadowbrook.Domain/BookingAggregate/IBookingRepository.cs`
- Create: `src/backend/Shadowbrook.Domain/BookingAggregate/Events/BookingCreated.cs`
- Create: `tests/Shadowbrook.Domain.Tests/BookingAggregate/BookingTests.cs`
- Create: `src/backend/Shadowbrook.Api/Infrastructure/Repositories/BookingRepository.cs`
- Create: `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/BookingConfiguration.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs` — update DbSet to use domain type
- Modify: `src/backend/Shadowbrook.Api/Program.cs` — register repository
- Delete: `src/backend/Shadowbrook.Api/Models/Booking.cs`

**Booking aggregate:**

```csharp
// src/backend/Shadowbrook.Domain/BookingAggregate/Booking.cs
public class Booking : Entity
{
    public Guid CourseId { get; private set; }
    public Guid GolferId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly Time { get; private set; }
    public string GolferName { get; private set; } = string.Empty;
    public int PlayerCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Booking() { } // EF

    public static Booking Create(
        Guid bookingId,
        Guid courseId,
        Guid golferId,
        DateOnly date,
        TimeOnly time,
        string golferName,
        int playerCount)
    {
        var now = DateTimeOffset.UtcNow;
        var booking = new Booking
        {
            Id = bookingId, // Pre-allocated by WaitlistOffer
            CourseId = courseId,
            GolferId = golferId,
            Date = date,
            Time = time,
            GolferName = golferName,
            PlayerCount = playerCount,
            CreatedAt = now,
            UpdatedAt = now
        };

        booking.AddDomainEvent(new BookingCreated
        {
            BookingId = bookingId,
            GolferId = golferId,
            CourseId = courseId
        });

        return booking;
    }
}
```

**BookingCreated event (identifiers only):**

```csharp
public record BookingCreated : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid BookingId { get; init; }
    public required Guid GolferId { get; init; }
    public required Guid CourseId { get; init; }
}
```

**Repository interface:**

```csharp
public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id);
    void Add(Booking booking);
    Task SaveAsync();
}
```

**Tests:** Test `Create()` sets all properties, generates `BookingCreated` event with correct IDs, uses pre-allocated `bookingId`.

**EF Configuration:** Move the existing Booking configuration from inline in `ApplicationDbContext.OnModelCreating()` to a proper `BookingConfiguration` class. Add `ValueGeneratedNever()` on Id. Keep existing table name and indexes.

**Migration:** Do NOT generate a migration yet — we'll do one migration at the end after all schema changes.

**Commit message:** `feat: promote Booking to domain aggregate with Create factory and BookingCreated event`

---

### Task 2: Add TeeTimeSlotFill Child Entity + Fill/Unfill to TeeTimeRequest

TeeTimeRequest needs a child collection tracking which golfers filled which slots.

**Files:**
- Create: `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/TeeTimeSlotFill.cs`
- Create: `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/FillResult.cs`
- Create: `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/Events/TeeTimeRequestFulfilled.cs`
- Modify: `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/TeeTimeRequest.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/TeeTimeRequestConfiguration.cs`
- Modify: `tests/Shadowbrook.Domain.Tests/TeeTimeRequestAggregate/TeeTimeRequestTests.cs`

**TeeTimeSlotFill child entity:**

```csharp
// src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/TeeTimeSlotFill.cs
public class TeeTimeSlotFill : Entity
{
    public Guid TeeTimeRequestId { get; private set; }
    public Guid GolferId { get; private set; }
    public Guid BookingId { get; private set; }
    public int GroupSize { get; private set; }
    public DateTimeOffset FilledAt { get; private set; }

    private TeeTimeSlotFill() { } // EF

    internal TeeTimeSlotFill(Guid teeTimeRequestId, Guid golferId, Guid bookingId, int groupSize)
    {
        var now = DateTimeOffset.UtcNow;
        Id = Guid.CreateVersion7();
        TeeTimeRequestId = teeTimeRequestId;
        GolferId = golferId;
        BookingId = bookingId;
        GroupSize = groupSize;
        FilledAt = now;
    }
}
```

**FillResult record:**

```csharp
// src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/FillResult.cs
namespace Shadowbrook.Domain.TeeTimeRequestAggregate;

public record FillResult(bool Success, string? RejectionReason = null);
```

**TeeTimeRequestFulfilled event:**

```csharp
public record TeeTimeRequestFulfilled : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid TeeTimeRequestId { get; init; }
}
```

**Add to TeeTimeRequest:**

```csharp
// Add child collection
private readonly List<TeeTimeSlotFill> slotFills = [];
public IReadOnlyCollection<TeeTimeSlotFill> SlotFills => this.slotFills.AsReadOnly();

public int RemainingSlots => GolfersNeeded - this.slotFills.Sum(f => f.GroupSize);

internal FillResult Fill(Guid golferId, int groupSize, Guid bookingId)
{
    if (Status == TeeTimeRequestStatus.Fulfilled)
    {
        return new FillResult(false, "This tee time has already been filled.");
    }

    if (groupSize > RemainingSlots)
    {
        return new FillResult(false, "Your group is too large for the remaining slots.");
    }

    var fill = new TeeTimeSlotFill(Id, golferId, bookingId, groupSize);
    this.slotFills.Add(fill);
    UpdatedAt = DateTimeOffset.UtcNow;

    if (RemainingSlots <= 0)
    {
        Status = TeeTimeRequestStatus.Fulfilled;
        AddDomainEvent(new TeeTimeRequestFulfilled
        {
            TeeTimeRequestId = Id
        });
    }

    return new FillResult(true);
}

internal void Unfill(Guid bookingId)
{
    var fill = this.slotFills.FirstOrDefault(f => f.BookingId == bookingId);
    if (fill is not null)
    {
        this.slotFills.Remove(fill);
        if (Status == TeeTimeRequestStatus.Fulfilled)
        {
            Status = TeeTimeRequestStatus.Pending;
        }
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

**Update ITeeTimeRequestRepository:** Add `Task<TeeTimeRequest?> GetByIdAsync(Guid id)` — include SlotFills.

**Update TeeTimeRequestRepository:** Implement `GetByIdAsync` with `.Include(r => r.SlotFills)`.

**Update TeeTimeRequestConfiguration:** Add child collection mapping:
```csharp
builder.HasMany(r => r.SlotFills)
    .WithOne()
    .HasForeignKey(f => f.TeeTimeRequestId)
    .OnDelete(DeleteBehavior.Cascade);

builder.Navigation(r => r.SlotFills)
    .UsePropertyAccessMode(PropertyAccessMode.Field);
```

**Tests:**
- `Fill_Success_AddsSlotFill` — fills a slot, returns success
- `Fill_GroupTooLarge_ReturnsFailure` — group of 3 with 2 remaining slots
- `Fill_AlreadyFulfilled_ReturnsFailure` — fill after all slots taken
- `Fill_ExactFit_MarksFulfilled_RaisesEvent` — filling last slot triggers fulfillment
- `Unfill_RemovesSlotFill_ResetsTosPending` — unfill reverts fulfilled status
- `RemainingSlots_CalculatesCorrectly` — 4 needed, group of 2 filled, 2 remaining

**Commit message:** `feat: add TeeTimeSlotFill child entity with Fill/Unfill methods on TeeTimeRequest`

---

### Task 3: Promote GolferWaitlistEntry to Independent Aggregate

GolferWaitlistEntry is currently a child entity of WalkUpWaitlist. Promote it to its own aggregate root.

**Files:**
- Move: `src/backend/Shadowbrook.Domain/WalkUpWaitlistAggregate/GolferWaitlistEntry.cs` → `src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/GolferWaitlistEntry.cs`
- Create: `src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/IGolferWaitlistEntryRepository.cs`
- Create: `src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/Events/GolferRemovedFromWaitlist.cs`
- Create: `src/backend/Shadowbrook.Api/Infrastructure/Repositories/GolferWaitlistEntryRepository.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/GolferWaitlistEntryConfiguration.cs`
- Modify: `src/backend/Shadowbrook.Api/Program.cs` — register repository
- Update: all files referencing old namespace `Shadowbrook.Domain.WalkUpWaitlistAggregate.GolferWaitlistEntry`

**Key changes to GolferWaitlistEntry:**
- New namespace: `Shadowbrook.Domain.GolferWaitlistEntryAggregate`
- Constructor changes from `internal` to allow creation from WalkUpWaitlist factory (keep `internal` since both are in same assembly)
- `Remove()` stays public, now raises `GolferRemovedFromWaitlist` event

```csharp
public void Remove()
{
    var now = DateTimeOffset.UtcNow;
    RemovedAt = now;
    UpdatedAt = now;

    AddDomainEvent(new GolferRemovedFromWaitlist
    {
        GolferWaitlistEntryId = Id,
        GolferId = GolferId
    });
}
```

**GolferRemovedFromWaitlist event:**

```csharp
public record GolferRemovedFromWaitlist : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid GolferWaitlistEntryId { get; init; }
    public required Guid GolferId { get; init; }
}
```

**Repository interface:**

```csharp
public interface IGolferWaitlistEntryRepository
{
    Task<GolferWaitlistEntry?> GetByIdAsync(Guid id);
    Task<GolferWaitlistEntry?> GetActiveByWaitlistAndGolferAsync(Guid courseWaitlistId, Guid golferId);
    Task<List<GolferWaitlistEntry>> GetActiveByWaitlistAsync(Guid courseWaitlistId);
    void Add(GolferWaitlistEntry entry);
    Task SaveAsync();
}
```

**EF Configuration update:** Remove the parent navigation to WalkUpWaitlist (the `WithOne()` on the parent side). Keep `CourseWaitlistId` as a property (it's still useful for queries) but the relationship is now just a foreign key, not a navigation. The GolferWaitlistEntryConfiguration should map this entity independently.

**Update ApplicationDbContext:** The `DbSet<GolferWaitlistEntry>` already exists. Update the `using` to the new namespace.

**Commit message:** `refactor: promote GolferWaitlistEntry to independent aggregate with repository`

---

### Task 4: Update WalkUpWaitlist to Factory Pattern

WalkUpWaitlist drops its child collection and becomes a factory for GolferWaitlistEntry.

**Files:**
- Modify: `src/backend/Shadowbrook.Domain/WalkUpWaitlistAggregate/WalkUpWaitlist.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WalkUpWaitlistConfiguration.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Repositories/WalkUpWaitlistRepository.cs`
- Modify: `tests/Shadowbrook.Domain.Tests/WalkUpWaitlistAggregate/WalkUpWaitlistTests.cs`

**WalkUpWaitlist changes:**

Remove:
```csharp
private readonly List<GolferWaitlistEntry> entries = [];
public IReadOnlyCollection<GolferWaitlistEntry> Entries => this.entries.AsReadOnly();
```

Change `Join()` to factory pattern — it validates rules and returns a new independent `GolferWaitlistEntry`, but no longer adds to internal collection. Duplicate detection is now the caller's responsibility (via repository check before calling `Join`).

```csharp
public GolferWaitlistEntry AddGolfer(Golfer golfer, int groupSize = 1)
{
    if (Status != WaitlistStatus.Open)
    {
        throw new WaitlistNotOpenException();
    }

    var entry = new GolferWaitlistEntry(Id, golfer.Id, groupSize);

    AddDomainEvent(new GolferJoinedWaitlist
    {
        GolferWaitlistEntryId = entry.Id,
        CourseWaitlistId = Id,
        GolferId = golfer.Id,
        GolferName = golfer.FullName,
        GolferPhone = golfer.Phone,
        CourseId = CourseId,
        Position = 0 // Position now determined by query, not collection count
    });

    UpdatedAt = DateTimeOffset.UtcNow;
    return entry;
}
```

Note: The `Position` field on the event will need to be set by the caller (endpoint/service) that knows the count from the repository. Or we can drop it from the event and have the SMS handler compute it. Simplest: pass position as a parameter to `AddGolfer()`.

**WalkUpWaitlistConfiguration:** Remove the `HasMany(w => w.Entries)` and `Navigation(w => w.Entries)` lines.

**WalkUpWaitlistRepository:** Remove all `.Include(w => w.Entries)` calls.

**Tests:** Update `Join()` tests:
- No longer assert on `waitlist.Entries.Count`
- Assert returned `GolferWaitlistEntry` has correct properties
- Remove duplicate detection tests (now at caller level)
- Keep `WaitlistNotOpenException` test

**Commit message:** `refactor: update WalkUpWaitlist to factory pattern, drop child collection`

---

### Task 5: Lean Down WaitlistOffer + New Accept/Reject

Remove duplicated data from WaitlistOffer, add BookingId, change Accept() to take Golfer, add Reject(), change OfferStatus.

**Files:**
- Modify: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/WaitlistOffer.cs`
- Modify: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/OfferStatus.cs`
- Modify: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/WaitlistOfferAccepted.cs`
- Create: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/WaitlistOfferRejected.cs`
- Delete: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Exceptions/OfferExpiredException.cs`
- Delete: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Exceptions/OfferSlotsFilledException.cs`
- Modify: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/IWaitlistOfferRepository.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistOfferConfiguration.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Repositories/WaitlistOfferRepository.cs`
- Modify: `tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs`

**OfferStatus change:**

```csharp
public enum OfferStatus
{
    Pending,
    Accepted,
    Rejected
}
```

**Lean WaitlistOffer:**

```csharp
public class WaitlistOffer : Entity
{
    public Guid Token { get; private set; }
    public Guid TeeTimeRequestId { get; private set; }
    public Guid GolferWaitlistEntryId { get; private set; }
    public Guid BookingId { get; private set; }
    public OfferStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private WaitlistOffer() { } // EF

    public static WaitlistOffer Create(
        Guid teeTimeRequestId,
        Guid golferWaitlistEntryId)
    {
        return new WaitlistOffer
        {
            Id = Guid.CreateVersion7(),
            Token = Guid.CreateVersion7(),
            BookingId = Guid.CreateVersion7(),
            TeeTimeRequestId = teeTimeRequestId,
            GolferWaitlistEntryId = golferWaitlistEntryId,
            Status = OfferStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Accept(Golfer golfer)
    {
        if (Status != OfferStatus.Pending)
        {
            throw new OfferNotPendingException();
        }

        // Validate golfer matches (via GolferWaitlistEntry — the entry references the golfer)
        // The endpoint/handler is responsible for loading the correct golfer

        Status = OfferStatus.Accepted;

        AddDomainEvent(new WaitlistOfferAccepted
        {
            WaitlistOfferId = Id,
            BookingId = BookingId,
            TeeTimeRequestId = TeeTimeRequestId,
            GolferWaitlistEntryId = GolferWaitlistEntryId,
            GolferId = golfer.Id
        });
    }

    public void Reject(string reason)
    {
        if (Status != OfferStatus.Pending)
        {
            return; // Idempotent — already resolved
        }

        Status = OfferStatus.Rejected;
        RejectionReason = reason;

        AddDomainEvent(new WaitlistOfferRejected
        {
            WaitlistOfferId = Id,
            GolferWaitlistEntryId = GolferWaitlistEntryId,
            Reason = reason
        });
    }
}
```

**WaitlistOfferAccepted (identifiers only):**

```csharp
public record WaitlistOfferAccepted : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid WaitlistOfferId { get; init; }
    public required Guid BookingId { get; init; }
    public required Guid TeeTimeRequestId { get; init; }
    public required Guid GolferWaitlistEntryId { get; init; }
    public required Guid GolferId { get; init; }
}
```

**WaitlistOfferRejected (new):**

```csharp
public record WaitlistOfferRejected : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid WaitlistOfferId { get; init; }
    public required Guid GolferWaitlistEntryId { get; init; }
    public required string Reason { get; init; }
}
```

**Delete exceptions:** `OfferExpiredException.cs`, `OfferSlotsFilledException.cs` (no longer needed — rejection is a domain outcome, not an exception). Keep `OfferNotPendingException`.

**Update IWaitlistOfferRepository:**

```csharp
public interface IWaitlistOfferRepository
{
    Task<WaitlistOffer?> GetByTokenAsync(Guid token);
    Task<WaitlistOffer?> GetByBookingIdAsync(Guid bookingId);
    Task<List<WaitlistOffer>> GetPendingByRequestAsync(Guid teeTimeRequestId);
    void Add(WaitlistOffer offer);
    void AddRange(IEnumerable<WaitlistOffer> offers);
    Task SaveAsync();
}
```

Remove `GetAcceptanceCountAsync()` (no longer needed).

**Update WaitlistOfferConfiguration:** Remove all dropped property mappings (`CourseName`, `Date`, `TeeTime`, etc.). Add `BookingId` mapping with index. Add `RejectionReason` with `HasMaxLength(500)`.

**Update Program.cs:** Remove `OfferExpiredException` and `OfferSlotsFilledException` from the global exception handler.

**Tests:** Rewrite WaitlistOfferTests:
- `Create_SetsPropertiesAndGeneratesBookingId`
- `Accept_PendingOffer_SetsAcceptedAndRaisesEvent`
- `Accept_AlreadyAccepted_ThrowsOfferNotPending`
- `Accept_AlreadyRejected_ThrowsOfferNotPending`
- `Reject_PendingOffer_SetsRejectedWithReason`
- `Reject_AlreadyAccepted_NoChange` (idempotent)
- Remove all expiration tests

**Commit message:** `refactor: lean down WaitlistOffer, add Accept(golfer)/Reject(reason), remove expiration`

---

### Task 6: Create New Domain Events

Create the remaining events needed for the saga chain that weren't created in earlier tasks.

**Files:**
- Create: `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/Events/TeeTimeSlotFilled.cs`
- Create: `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/Events/TeeTimeSlotFillFailed.cs`

These events are raised by handlers (not by aggregates directly), but they are domain events that flow through the system.

**TeeTimeSlotFilled:**

```csharp
public record TeeTimeSlotFilled : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid TeeTimeRequestId { get; init; }
    public required Guid BookingId { get; init; }
    public required Guid GolferId { get; init; }
}
```

**TeeTimeSlotFillFailed:**

```csharp
public record TeeTimeSlotFillFailed : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid TeeTimeRequestId { get; init; }
    public required Guid OfferId { get; init; }
    public required string Reason { get; init; }
}
```

Note: These events are published by handlers (not via `AddDomainEvent` on an aggregate) because the handler is orchestrating the saga step. The handler will call `eventPublisher.PublishAsync()` directly.

**Commit message:** `feat: add TeeTimeSlotFilled and TeeTimeSlotFillFailed domain events`

---

### Task 7: EF Migration

Generate a single migration that captures all schema changes from Tasks 1-6.

**Files:**
- Create: new migration file via `dotnet ef migrations add`
- Modify: `ApplicationDbContextModelSnapshot.cs` (auto-generated)

**Schema changes:**
- WaitlistOffers table: drop columns (`CourseId`, `CourseName`, `Date`, `TeeTime`, `GolfersNeeded`, `GolferName`, `GolferPhone`, `ExpiresAt`), add columns (`BookingId`, `RejectionReason`), update indexes
- New table: `TeeTimeSlotFills` (child of TeeTimeRequests)
- Booking table: may need `GolferId` column if not already present
- GolferWaitlistEntries: may need relationship changes (remove parent FK constraint or keep as simple column)

**Steps:**

1. Verify build passes: `dotnet build shadowbrook.slnx`
2. Check pending changes: `export PATH="$PATH:/home/aaron/.dotnet/tools" && dotnet ef migrations has-pending-model-changes --project src/backend/Shadowbrook.Api`
3. Generate migration: `dotnet ef migrations add WaitlistOfferSagaRefactor --project src/backend/Shadowbrook.Api`
4. Review the generated migration file
5. Verify build still passes

**Commit message:** `feat: add migration for waitlist offer saga schema changes`

---

### Task 8: Implement Saga Event Handlers

Rewrite existing handlers and create new ones for the sequential event chain.

**Files:**
- Rewrite: `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeRequestAddedNotifyHandler.cs`
- Rewrite: `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferAcceptedHandler.cs`
- Delete: Old handler logic that creates bookings, marks fulfilled, etc.
- Create: `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferAcceptedFillHandler.cs`
- Create: `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferAcceptedSmsHandler.cs`
- Create: `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeSlotFilledBookingHandler.cs`
- Create: `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeSlotFillFailedHandler.cs`
- Create: `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeRequestFulfilledHandler.cs`
- Create: `src/backend/Shadowbrook.Api/EventHandlers/BookingCreatedRemoveFromWaitlistHandler.cs`
- Create: `src/backend/Shadowbrook.Api/EventHandlers/BookingCreatedConfirmationSmsHandler.cs`
- Create: `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferRejectedSmsHandler.cs`
- Create: `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferRejectedNextOfferHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/Program.cs` — register all new handlers

**The saga chain (each handler does ONE thing):**

**1. WaitlistOfferAcceptedFillHandler** — reacts to `WaitlistOfferAccepted`
```
Load TeeTimeRequest (with SlotFills) by Id
Load GolferWaitlistEntry by Id (to get GroupSize)
Call request.Fill(golferId, groupSize, bookingId)
If success → publish TeeTimeSlotFilled
If failure → publish TeeTimeSlotFillFailed
Save request
```

**2. WaitlistOfferAcceptedSmsHandler** — reacts to `WaitlistOfferAccepted`
```
Look up golfer phone from GolferWaitlistEntry → Golfer
SMS: "We're processing your request — you'll receive a confirmation shortly."
```

**3. TeeTimeSlotFilledBookingHandler** — reacts to `TeeTimeSlotFilled`
```
Look up TeeTimeRequest for course/date/time details
Look up Golfer for name
Create Booking.Create(bookingId, golferId, courseId, date, time, golferName, groupSize)
Save booking (triggers BookingCreated event)
```

**4. TeeTimeSlotFillFailedHandler** — reacts to `TeeTimeSlotFillFailed`
```
Load WaitlistOffer by OfferId
Call offer.Reject(reason)
Save offer (triggers WaitlistOfferRejected event)
```

**5. TeeTimeRequestFulfilledHandler** — reacts to `TeeTimeRequestFulfilled`
```
Load all pending WaitlistOffers for this TeeTimeRequestId
For each: call offer.Reject("Tee time has been filled.")
Save all (each triggers WaitlistOfferRejected event)
```

**6. BookingCreatedRemoveFromWaitlistHandler** — reacts to `BookingCreated`
```
Look up WaitlistOffer by BookingId → get GolferWaitlistEntryId
Load GolferWaitlistEntry by Id
Call entry.Remove()
Save entry (triggers GolferRemovedFromWaitlist event)
```

**7. BookingCreatedConfirmationSmsHandler** — reacts to `BookingCreated`
```
Look up Golfer by GolferId → phone
Look up Course by CourseId → name
Look up Booking by BookingId → date, time
SMS: "You're booked! {CourseName} at {Time} on {Date}. See you on the course!"
```

**8. WaitlistOfferRejectedSmsHandler** — reacts to `WaitlistOfferRejected`
```
Load GolferWaitlistEntry by Id → check if still active (RemovedAt is null)
If active: look up Golfer → phone
SMS: "Sorry, that tee time is no longer available."
```

**9. WaitlistOfferRejectedNextOfferHandler** — reacts to `WaitlistOfferRejected`
```
After 60s buffer (for now, use Task.Delay or just do it immediately — we can add proper scheduling later)
Load TeeTimeRequest → check if still Pending
Find next eligible golfer on waitlist (smart matching: GroupSize <= RemainingSlots, not already offered)
Create new WaitlistOffer.Create(teeTimeRequestId, entryId)
Send SMS with claim link
Save offer
```

**10. TeeTimeRequestAddedNotifyHandler (rewrite)**
```
Find open walk-up waitlist for course + date
Find eligible golfers (smart matching: GroupSize fits, active, walk-up, ready)
Order by JoinedAt
Create one offer per slot needed (not one per golfer — match group sizes to fill slots)
Save offers
Send SMS to each with claim link
```

**Handler registration note:** For events with multiple handlers (e.g., `BookingCreated` has two handlers), the current `InProcessDomainEventPublisher` resolves handlers via `IServiceProvider.GetServices<IDomainEventHandler<T>>()`. Multiple handlers for the same event type need to be registered with `AddScoped` — each as a separate registration. Check how `InProcessDomainEventPublisher` resolves handlers.

Read `src/backend/Shadowbrook.Api/Infrastructure/Events/InProcessDomainEventPublisher.cs` to verify it supports multiple handlers per event. If it uses `GetService` (singular), it will only resolve one. If it uses `GetServices` (plural), it resolves all. Update if needed.

**Commit message:** `feat: implement saga event handler chain for waitlist offer acceptance`

---

### Task 9: Update Endpoints + Read Models

Update endpoints to work with the new lean domain models. Add read models for display data.

**Files:**
- Rewrite: `src/backend/Shadowbrook.Api/Endpoints/WaitlistOfferEndpoints.cs`
- Modify: `src/backend/Shadowbrook.Api/Endpoints/WalkUpWaitlistEndpoints.cs`
- Modify: `src/backend/Shadowbrook.Api/Endpoints/WalkUpJoinEndpoints.cs`
- Modify: `src/backend/Shadowbrook.Api/Program.cs`

**WaitlistOfferEndpoints rewrite:**

`GET /{token}` — needs a read model to join offer + request + golfer + course:

```csharp
private static async Task<IResult> GetOffer(Guid token, ApplicationDbContext db)
{
    var offer = await db.WaitlistOffers
        .IgnoreQueryFilters()
        .Where(o => o.Token == token)
        .Join(db.TeeTimeRequests, o => o.TeeTimeRequestId, r => r.Id,
            (o, r) => new { Offer = o, Request = r })
        .Join(db.GolferWaitlistEntries, x => x.Offer.GolferWaitlistEntryId, e => e.Id,
            (x, e) => new { x.Offer, x.Request, Entry = e })
        .Join(db.Golfers.IgnoreQueryFilters(), x => x.Entry.GolferId, g => g.Id,
            (x, g) => new { x.Offer, x.Request, x.Entry, Golfer = g })
        .Join(db.Courses.IgnoreQueryFilters(), x => x.Request.CourseId, c => c.Id,
            (x, c) => new WaitlistOfferReadModel(
                x.Offer.Token,
                c.Name,
                x.Request.Date.ToString("yyyy-MM-dd"),
                x.Request.TeeTime.ToString("HH:mm"),
                x.Request.GolfersNeeded,
                x.Golfer.FullName,
                x.Offer.Status.ToString()))
        .FirstOrDefaultAsync();

    if (offer is null)
    {
        return Results.NotFound(new { error = "Offer not found." });
    }

    return Results.Ok(offer);
}
```

Or use a simpler approach with a raw SQL query or multiple lookups. The exact implementation can be adjusted — the key point is the domain model doesn't carry display data.

`POST /{token}/accept` — much simpler now:

```csharp
private static async Task<IResult> AcceptOffer(
    Guid token,
    IWaitlistOfferRepository offerRepository,
    IGolferWaitlistEntryRepository entryRepository,
    IGolferRepository golferRepository)
{
    var offer = await offerRepository.GetByTokenAsync(token);
    if (offer is null)
    {
        return Results.NotFound(new { error = "Offer not found." });
    }

    var entry = await entryRepository.GetByIdAsync(offer.GolferWaitlistEntryId);
    if (entry is null)
    {
        return Results.NotFound(new { error = "Waitlist entry not found." });
    }

    var golfer = await golferRepository.GetByIdAsync(entry.GolferId);
    if (golfer is null)
    {
        return Results.NotFound(new { error = "Golfer not found." });
    }

    offer.Accept(golfer);
    await offerRepository.SaveAsync();

    return Results.Ok(new { status = "Processing", message = "We're processing your request — you'll receive a confirmation shortly." });
}
```

No transaction needed. No acceptance records. Just accept the offer and let the saga handle everything else.

**WalkUpWaitlistEndpoints + WalkUpJoinEndpoints:**
- `AddGolferToWaitlist` / `JoinWaitlist`: Call `waitlist.AddGolfer(golfer, groupSize)` which returns the entry. Save both the waitlist (for the event) and the entry (as a new aggregate) via their respective repositories.
- `GetToday`: Query entries separately via `IGolferWaitlistEntryRepository.GetActiveByWaitlistAsync(waitlistId)` instead of using `waitlist.Entries`.
- Duplicate detection: Check via `entryRepository.GetActiveByWaitlistAndGolferAsync()` before calling `AddGolfer()`.

**Delete:** `src/backend/Shadowbrook.Api/Models/WaitlistRequestAcceptance.cs` and its EF configuration — no longer needed.

**Commit message:** `refactor: update endpoints for saga pattern with read models`

---

### Task 10: Update All Tests

Update domain unit tests and integration tests to match the new design.

**Files:**
- Rewrite: `tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs`
- Modify: `tests/Shadowbrook.Domain.Tests/TeeTimeRequestAggregate/TeeTimeRequestTests.cs`
- Modify: `tests/Shadowbrook.Domain.Tests/WalkUpWaitlistAggregate/WalkUpWaitlistTests.cs`
- Rewrite: `tests/Shadowbrook.Api.Tests/WaitlistOfferEndpointsTests.cs`
- Modify: `tests/Shadowbrook.Api.Tests/WalkUpWaitlistEndpointsTests.cs`
- Modify: `tests/Shadowbrook.Api.Tests/WalkUpJoinEndpointsTests.cs`
- Create: `tests/Shadowbrook.Domain.Tests/BookingAggregate/BookingTests.cs` (if not created in Task 1)

**Domain test updates:**

WaitlistOfferTests — rewrite for new Accept(golfer)/Reject(reason) signatures. Remove expiration tests.

TeeTimeRequestTests — add Fill/Unfill tests (if not created in Task 2).

WalkUpWaitlistTests — update for factory pattern (AddGolfer returns entry, no collection).

**Integration test updates:**

WaitlistOfferEndpointsTests — major rewrite:
- `GET /waitlist/offers/{token}` — verify read model returns joined data
- `POST /waitlist/offers/{token}/accept` — verify offer status changes, saga chain fires (fill + booking + removal + SMS)
- Remove expiration tests
- Add rejection scenario tests (group too large, request already fulfilled)
- Update helper methods (no more direct WaitlistOffer field access for display data)

WalkUpWaitlistEndpointsTests / WalkUpJoinEndpointsTests:
- Update for new entry creation pattern (saved via entry repository)
- Update position calculation
- Update GetToday response (entries queried separately)

**Commit message:** `test: update all tests for saga pattern and new aggregate boundaries`

---

### Task 11: Cleanup and Final Verification

Remove unused code, verify everything works end-to-end.

**Steps:**

1. Remove unused files:
   - `src/backend/Shadowbrook.Api/Models/WaitlistRequestAcceptance.cs` (if still present)
   - `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistRequestAcceptanceConfiguration.cs`
   - `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Exceptions/OfferExpiredException.cs`
   - `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Exceptions/OfferSlotsFilledException.cs`
   - Old `src/backend/Shadowbrook.Api/Models/Booking.cs` (if still present)

2. Remove unused references from `ApplicationDbContext`:
   - `WaitlistRequestAcceptances` DbSet

3. Remove unused exception mappings from `Program.cs` global handler

4. Run full test suite: `dotnet test shadowbrook.slnx -v minimal`

5. Verify no pending migration issues: `dotnet ef migrations has-pending-model-changes --project src/backend/Shadowbrook.Api`

6. Run frontend lint: `pnpm --dir src/web lint`

7. Verify the frontend claim page still compiles (route: `/book/walkup/:token`). The API contract changed — the response no longer has `ExpiresAt`. Update the frontend types and remove the countdown timer component.

**Frontend updates:**
- `src/web/src/types/waitlist.ts` — remove `ExpiresAt` from `WaitlistOfferResponse`
- `src/web/src/features/walk-up/components/CountdownTimer.tsx` — delete
- `src/web/src/features/walk-up/components/OfferCard.tsx` — remove countdown, update for new response shape
- `src/web/src/features/walk-up/pages/WalkUpOfferPage.tsx` — update success message to "We're processing your request..."
- `src/web/src/features/walk-up/components/AcceptConfirmation.tsx` — update to "processing" message instead of immediate "booked" confirmation

**Commit message:** `chore: cleanup unused code, update frontend for saga pattern`

---

## Task Dependency Order

```
Task 1 (Booking aggregate)     ─┐
Task 2 (TeeTimeRequest Fill)   ─┤
Task 3 (GolferWaitlistEntry)   ─┼─→ Task 7 (Migration) ─→ Task 8 (Handlers) ─→ Task 9 (Endpoints) ─→ Task 10 (Tests) ─→ Task 11 (Cleanup)
Task 4 (WalkUpWaitlist factory) ─┤
Task 5 (WaitlistOffer lean)    ─┤
Task 6 (New events)            ─┘
```

Tasks 1-6 can be done in any order (they're all domain changes). Task 7 (migration) must come after all domain + EF config changes. Tasks 8-11 are sequential.

---

## Summary

| Task | What | Key Files |
|------|------|-----------|
| 1 | Booking → domain aggregate | Domain/BookingAggregate/, Models/Booking.cs |
| 2 | TeeTimeRequest + Fill/Unfill | Domain/TeeTimeRequestAggregate/ |
| 3 | GolferWaitlistEntry → own aggregate | Domain/GolferWaitlistEntryAggregate/ |
| 4 | WalkUpWaitlist → factory | Domain/WalkUpWaitlistAggregate/ |
| 5 | WaitlistOffer lean + Accept/Reject | Domain/WaitlistOfferAggregate/ |
| 6 | New saga events | Domain/TeeTimeRequestAggregate/Events/ |
| 7 | EF migration | Migrations/ |
| 8 | Saga event handlers | EventHandlers/ |
| 9 | Endpoints + read models | Endpoints/ |
| 10 | Update all tests | tests/ |
| 11 | Cleanup + frontend | Everything |

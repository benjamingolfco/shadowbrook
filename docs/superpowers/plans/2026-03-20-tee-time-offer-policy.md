# Tee Time Offer Policy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the monolithic `TeeTimeRequestAddedNotifyHandler` with two Wolverine saga-based policies that sequentially notify eligible golfers with a buffer between notifications.

**Architecture:** Two policies (`TeeTimeOfferPolicy` for sequential offer notifications, `TeeTimeRequestExpirationPolicy` for closing expired requests) coordinate via domain events and commands. A stateless `NotifyNextEligibleGolferHandler` handles the actual golfer lookup, offer creation, and SMS delivery. The policies can be built in parallel after shared domain changes land.

**Tech Stack:** .NET 10, WolverineFx (Saga base class, TimeoutMessage), EF Core 10, SQL Server, xUnit, NSubstitute

**Spec:** `docs/superpowers/specs/2026-03-20-tee-time-offer-policy-design.md`

**Wolverine skill:** `.claude/skills/wolverine/SKILL.md` — reference for endpoint patterns, handler conventions, `[NotBody]` rules

---

## File Structure

### New Files — Domain

| File | Purpose |
|------|---------|
| `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/WaitlistOfferCreated.cs` | Domain event raised on offer creation |
| `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/GolferNotifiedOfOffer.cs` | Domain event raised when golfer is notified |
| `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/Events/TeeTimeRequestClosed.cs` | Domain event raised when request is closed |

### New Files — API

| File | Purpose |
|------|---------|
| `src/backend/Shadowbrook.Api/Features/WaitlistOffers/TeeTimeOfferPolicy.cs` | Saga policy + timeout message + command records |
| `src/backend/Shadowbrook.Api/Features/WaitlistOffers/NotifyNextEligibleGolferHandler.cs` | Stateless handler for finding and notifying next golfer |
| `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/TeeTimeRequestExpirationPolicy.cs` | Saga policy + timeout message + command records |
| `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/CloseTeeTimeRequestHandler.cs` | Handler to close expired tee time requests |
| `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/TeeTimeOfferPolicyConfiguration.cs` | EF Core mapping for saga state |
| `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/TeeTimeRequestExpirationPolicyConfiguration.cs` | EF Core mapping for saga state |

### New Files — Tests

| File | Purpose |
|------|---------|
| `tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs` | Tests for MarkNotified, Create raising event |
| `tests/Shadowbrook.Domain.Tests/TeeTimeRequestAggregate/TeeTimeRequestCloseTests.cs` | Tests for Close method |
| ~~`tests/Shadowbrook.Api.Tests/Handlers/NotifyNextEligibleGolferHandlerTests.cs`~~ | Deferred — handler uses `ApplicationDbContext` directly, needs integration tests (Task 11) |
| `tests/Shadowbrook.Api.Tests/Handlers/CloseTeeTimeRequestHandlerTests.cs` | Handler unit tests |
| `tests/Shadowbrook.Api.Tests/Policies/TeeTimeOfferPolicyTests.cs` | Policy saga unit tests |
| `tests/Shadowbrook.Api.Tests/Policies/TeeTimeRequestExpirationPolicyTests.cs` | Policy saga unit tests |

### Modified Files

| File | Change |
|------|--------|
| `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/WaitlistOffer.cs` | Add `NotifiedAt`, `MarkNotified()`, `WaitlistOfferCreated` event in `Create()` |
| `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/WaitlistOfferRejected.cs` | Add `TeeTimeRequestId` field |
| `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/TeeTimeRequest.cs` | Add `Close()` method |
| `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/TeeTimeRequestStatus.cs` | Add `Closed` enum value |
| `src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs` | Add DbSets for policies |
| `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistOfferConfiguration.cs` | Add `NotifiedAt` column |
| `tests/Shadowbrook.Domain.Tests/TeeTimeRequestAggregate/TeeTimeRequestTests.cs` | Update `Reject` event assertions for new `TeeTimeRequestId` field |
| `tests/Shadowbrook.Api.Tests/Handlers/TeeTimeSlotFillFailedHandlerTests.cs` | Update `WaitlistOfferRejected` assertions for new field |
| `tests/Shadowbrook.Api.Tests/Handlers/TeeTimeRequestFulfilledHandlerTests.cs` | Update `WaitlistOfferRejected` assertions for new field |

### Removed Files

| File | Reason |
|------|--------|
| `src/backend/Shadowbrook.Api/Features/WaitlistOffers/TeeTimeRequestAddedNotifyHandler.cs` | Replaced by policy + handler |
| `src/backend/Shadowbrook.Api/Features/WaitlistOffers/WaitlistOfferRejectedNextOfferHandler.cs` | Replaced by policy |

---

## Task 1: Domain Events — WaitlistOfferCreated & GolferNotifiedOfOffer

**Files:**
- Create: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/WaitlistOfferCreated.cs`
- Create: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/GolferNotifiedOfOffer.cs`

- [ ] **Step 1: Create WaitlistOfferCreated event**

```csharp
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WaitlistOfferAggregate.Events;

public record WaitlistOfferCreated : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid WaitlistOfferId { get; init; }
    public required Guid TeeTimeRequestId { get; init; }
    public required Guid GolferWaitlistEntryId { get; init; }
}
```

- [ ] **Step 2: Create GolferNotifiedOfOffer event**

```csharp
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WaitlistOfferAggregate.Events;

public record GolferNotifiedOfOffer : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid WaitlistOfferId { get; init; }
    public required Guid TeeTimeRequestId { get; init; }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build shadowbrook.slnx --verbosity quiet`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/WaitlistOfferCreated.cs src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/GolferNotifiedOfOffer.cs
git commit -m "feat: add WaitlistOfferCreated and GolferNotifiedOfOffer domain events"
```

---

## Task 2: Domain Event — TeeTimeRequestClosed

**Files:**
- Create: `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/Events/TeeTimeRequestClosed.cs`
- Modify: `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/TeeTimeRequestStatus.cs`

- [ ] **Step 1: Create TeeTimeRequestClosed event**

```csharp
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TeeTimeRequestAggregate.Events;

public record TeeTimeRequestClosed : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid TeeTimeRequestId { get; init; }
}
```

- [ ] **Step 2: Add Closed status to TeeTimeRequestStatus enum**

Add `Closed` after `Cancelled` in `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/TeeTimeRequestStatus.cs`:

```csharp
public enum TeeTimeRequestStatus
{
    Pending,
    Fulfilled,
    Cancelled,
    Closed
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build shadowbrook.slnx --verbosity quiet`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/Events/TeeTimeRequestClosed.cs src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/TeeTimeRequestStatus.cs
git commit -m "feat: add TeeTimeRequestClosed event and Closed status"
```

---

## Task 3: WaitlistOffer — Add MarkNotified and WaitlistOfferCreated

**Files:**
- Modify: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/WaitlistOffer.cs`
- Create: `tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistOfferConfiguration.cs`

- [ ] **Step 1: Write failing tests for MarkNotified and Create raising WaitlistOfferCreated**

Create `tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs`:

```csharp
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Domain.Tests.WaitlistOfferAggregate;

public class WaitlistOfferTests
{
    [Fact]
    public void Create_RaisesWaitlistOfferCreatedEvent()
    {
        var requestId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var offer = WaitlistOffer.Create(requestId, entryId);

        var domainEvent = Assert.Single(offer.DomainEvents);
        var created = Assert.IsType<WaitlistOfferCreated>(domainEvent);
        Assert.Equal(offer.Id, created.WaitlistOfferId);
        Assert.Equal(requestId, created.TeeTimeRequestId);
        Assert.Equal(entryId, created.GolferWaitlistEntryId);
    }

    [Fact]
    public void MarkNotified_SetsNotifiedAtAndRaisesEvent()
    {
        var requestId = Guid.NewGuid();
        var offer = WaitlistOffer.Create(requestId, Guid.NewGuid());
        offer.ClearDomainEvents();

        offer.MarkNotified();

        Assert.NotNull(offer.NotifiedAt);
        var domainEvent = Assert.Single(offer.DomainEvents);
        var notified = Assert.IsType<GolferNotifiedOfOffer>(domainEvent);
        Assert.Equal(offer.Id, notified.WaitlistOfferId);
        Assert.Equal(requestId, notified.TeeTimeRequestId);
    }

    [Fact]
    public void MarkNotified_AlreadyNotified_Throws()
    {
        var offer = WaitlistOffer.Create(Guid.NewGuid(), Guid.NewGuid());
        offer.MarkNotified();

        Assert.Throws<InvalidOperationException>(() => offer.MarkNotified());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Shadowbrook.Domain.Tests --filter "FullyQualifiedName~WaitlistOfferTests" --verbosity quiet`
Expected: FAIL — `MarkNotified` method and `NotifiedAt` property don't exist, `Create` doesn't raise event

- [ ] **Step 3: Implement MarkNotified and update Create**

In `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/WaitlistOffer.cs`:

Add `NotifiedAt` property alongside existing properties:

```csharp
public DateTimeOffset? NotifiedAt { get; private set; }
```

Add `WaitlistOfferCreated` event at end of `Create()` method, before the return:

```csharp
var offer = new WaitlistOffer
{
    Id = Guid.CreateVersion7(),
    Token = Guid.CreateVersion7(),
    BookingId = Guid.CreateVersion7(),
    TeeTimeRequestId = teeTimeRequestId,
    GolferWaitlistEntryId = golferWaitlistEntryId,
    Status = OfferStatus.Pending,
    CreatedAt = DateTimeOffset.UtcNow
};

offer.AddDomainEvent(new WaitlistOfferCreated
{
    WaitlistOfferId = offer.Id,
    TeeTimeRequestId = teeTimeRequestId,
    GolferWaitlistEntryId = golferWaitlistEntryId
});

return offer;
```

Add `MarkNotified()` method after `Reject()`:

```csharp
public void MarkNotified()
{
    if (NotifiedAt is not null)
    {
        throw new InvalidOperationException("Offer has already been marked as notified.");
    }

    NotifiedAt = DateTimeOffset.UtcNow;

    AddDomainEvent(new GolferNotifiedOfOffer
    {
        WaitlistOfferId = Id,
        TeeTimeRequestId = TeeTimeRequestId
    });
}
```

Add the using for the new events (already imported via `using Shadowbrook.Domain.WaitlistOfferAggregate.Events;`).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Shadowbrook.Domain.Tests --filter "FullyQualifiedName~WaitlistOfferTests" --verbosity quiet`
Expected: 3 passed

- [ ] **Step 5: Add NotifiedAt to EF configuration**

In `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistOfferConfiguration.cs`, add inside `Configure()`:

```csharp
builder.Property(o => o.NotifiedAt);
```

- [ ] **Step 6: Run full test suite to check for regressions**

Run: `dotnet test shadowbrook.slnx --verbosity quiet`
Expected: All pass. Note: existing tests that assert on `WaitlistOffer.Create()` domain events will now see `WaitlistOfferCreated` — check if any assert `Assert.Empty(offer.DomainEvents)` after `Create()`.

- [ ] **Step 7: Commit**

```bash
git add src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/WaitlistOffer.cs tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistOfferConfiguration.cs
git commit -m "feat: add MarkNotified method and WaitlistOfferCreated event to WaitlistOffer"
```

---

## Task 4: WaitlistOfferRejected — Add TeeTimeRequestId

**Files:**
- Modify: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/WaitlistOfferRejected.cs`
- Modify: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/WaitlistOffer.cs` (Reject method)
- Modify: `tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs`

- [ ] **Step 1: Write test for Reject raising event with TeeTimeRequestId**

Add to `tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs`:

```csharp
[Fact]
public void Reject_RaisesEventWithTeeTimeRequestId()
{
    var requestId = Guid.NewGuid();
    var offer = WaitlistOffer.Create(requestId, Guid.NewGuid());
    offer.ClearDomainEvents();

    offer.Reject("No longer available");

    var domainEvent = Assert.Single(offer.DomainEvents);
    var rejected = Assert.IsType<WaitlistOfferRejected>(domainEvent);
    Assert.Equal(offer.Id, rejected.WaitlistOfferId);
    Assert.Equal(requestId, rejected.TeeTimeRequestId);
    Assert.Equal("No longer available", rejected.Reason);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Shadowbrook.Domain.Tests --filter "FullyQualifiedName~Reject_RaisesEventWithTeeTimeRequestId" --verbosity quiet`
Expected: FAIL — `WaitlistOfferRejected` has no `TeeTimeRequestId` property

- [ ] **Step 3: Add TeeTimeRequestId to WaitlistOfferRejected**

In `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/WaitlistOfferRejected.cs`:

```csharp
public record WaitlistOfferRejected : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid WaitlistOfferId { get; init; }
    public required Guid TeeTimeRequestId { get; init; }
    public required Guid GolferWaitlistEntryId { get; init; }
    public required string Reason { get; init; }
}
```

- [ ] **Step 4: Update Reject method to include TeeTimeRequestId**

In `WaitlistOffer.Reject()`, update the event construction:

```csharp
AddDomainEvent(new WaitlistOfferRejected
{
    WaitlistOfferId = Id,
    TeeTimeRequestId = TeeTimeRequestId,
    GolferWaitlistEntryId = GolferWaitlistEntryId,
    Reason = reason
});
```

- [ ] **Step 5: Run all tests**

Run: `dotnet test shadowbrook.slnx --verbosity quiet`
Expected: All pass. Existing tests that construct `WaitlistOfferRejected` manually may need updating if they use object initializer syntax without `TeeTimeRequestId`.

- [ ] **Step 6: Fix any broken tests**

Check `tests/Shadowbrook.Api.Tests/Handlers/TeeTimeSlotFillFailedHandlerTests.cs` and `tests/Shadowbrook.Api.Tests/Handlers/TeeTimeRequestFulfilledHandlerTests.cs` — these tests call `offer.Reject()` which now produces an event with `TeeTimeRequestId`. The tests assert on `offer.Status` and `offer.RejectionReason`, not on the event shape, so they should still pass. Verify and fix if needed.

- [ ] **Step 7: Commit**

```bash
git add src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/WaitlistOfferRejected.cs src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/WaitlistOffer.cs tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs
git commit -m "feat: add TeeTimeRequestId to WaitlistOfferRejected for saga correlation"
```

---

## Task 5: TeeTimeRequest.Close()

**Files:**
- Modify: `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/TeeTimeRequest.cs`
- Create: `tests/Shadowbrook.Domain.Tests/TeeTimeRequestAggregate/TeeTimeRequestCloseTests.cs`

- [ ] **Step 1: Write failing tests for Close**

Create `tests/Shadowbrook.Domain.Tests/TeeTimeRequestAggregate/TeeTimeRequestCloseTests.cs`:

```csharp
using NSubstitute;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;

namespace Shadowbrook.Domain.Tests.TeeTimeRequestAggregate;

public class TeeTimeRequestCloseTests
{
    private readonly ITeeTimeRequestRepository repository = Substitute.For<ITeeTimeRequestRepository>();

    [Fact]
    public async Task Close_PendingRequest_SetsClosedStatusAndRaisesEvent()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 20), new TimeOnly(10, 0), 2, this.repository);
        request.ClearDomainEvents();

        request.Close();

        Assert.Equal(TeeTimeRequestStatus.Closed, request.Status);
        var domainEvent = Assert.Single(request.DomainEvents);
        var closed = Assert.IsType<TeeTimeRequestClosed>(domainEvent);
        Assert.Equal(request.Id, closed.TeeTimeRequestId);
    }

    [Fact]
    public async Task Close_AlreadyClosed_DoesNothing()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 20), new TimeOnly(10, 0), 2, this.repository);
        request.Close();
        request.ClearDomainEvents();

        request.Close();

        Assert.Empty(request.DomainEvents);
        Assert.Equal(TeeTimeRequestStatus.Closed, request.Status);
    }

    [Fact]
    public async Task Close_FulfilledRequest_DoesNothing()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 20), new TimeOnly(10, 0), 1, this.repository);
        request.Fill(Guid.NewGuid(), groupSize: 1, Guid.NewGuid(), Guid.NewGuid());
        request.ClearDomainEvents();

        request.Close();

        Assert.Empty(request.DomainEvents);
        Assert.Equal(TeeTimeRequestStatus.Fulfilled, request.Status);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Shadowbrook.Domain.Tests --filter "FullyQualifiedName~TeeTimeRequestCloseTests" --verbosity quiet`
Expected: FAIL — `Close()` method doesn't exist

- [ ] **Step 3: Implement Close method**

Add to `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/TeeTimeRequest.cs` after `Unfill()`:

```csharp
public void Close()
{
    if (Status is TeeTimeRequestStatus.Closed or TeeTimeRequestStatus.Fulfilled)
    {
        return;
    }

    Status = TeeTimeRequestStatus.Closed;
    UpdatedAt = DateTimeOffset.UtcNow;

    AddDomainEvent(new TeeTimeRequestClosed
    {
        TeeTimeRequestId = Id
    });
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Shadowbrook.Domain.Tests --filter "FullyQualifiedName~TeeTimeRequestCloseTests" --verbosity quiet`
Expected: 3 passed

- [ ] **Step 5: Commit**

```bash
git add src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/TeeTimeRequest.cs tests/Shadowbrook.Domain.Tests/TeeTimeRequestAggregate/TeeTimeRequestCloseTests.cs
git commit -m "feat: add Close method to TeeTimeRequest"
```

---

## Task 6: EF Core — Policy Mappings and Migration

**Files:**
- Create: `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/TeeTimeOfferPolicyConfiguration.cs`
- Create: `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/TeeTimeRequestExpirationPolicyConfiguration.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs`

This task sets up the EF Core infrastructure for both saga policies. The actual policy classes are defined here as simple POCOs for the configuration to reference — the full saga behavior is added in later tasks.

- [ ] **Step 1: Create TeeTimeOfferPolicy entity class**

This will be defined in the policy file in Task 8, but we need a minimal version here for EF mapping. Create a temporary class in the configuration file, or create the policy file early with just the state:

Create `src/backend/Shadowbrook.Api/Features/WaitlistOffers/TeeTimeOfferPolicy.cs`:

```csharp
using Wolverine;
using Wolverine.Attributes;

namespace Shadowbrook.Api.Features.WaitlistOffers;

public class TeeTimeOfferPolicy : Saga
{
    [SagaIdentity("TeeTimeRequestId")]
    public Guid Id { get; set; }
    public Guid? CurrentOfferId { get; set; }
    public bool IsBuffering { get; set; }
}
```

**Important:** `[SagaIdentity("TeeTimeRequestId")]` tells Wolverine to correlate incoming messages by matching their `TeeTimeRequestId` property to this saga's `Id`. Without this, Wolverine cannot route domain events (which carry `TeeTimeRequestId`, not `TeeTimeOfferPolicyId`) to the correct saga instance.

- [ ] **Step 2: Create TeeTimeRequestExpirationPolicy entity class**

Create `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/TeeTimeRequestExpirationPolicy.cs`:

```csharp
using Wolverine;
using Wolverine.Attributes;

namespace Shadowbrook.Api.Features.WalkUpWaitlist;

public class TeeTimeRequestExpirationPolicy : Saga
{
    [SagaIdentity("TeeTimeRequestId")]
    public Guid Id { get; set; }
}
```

- [ ] **Step 3: Create TeeTimeOfferPolicyConfiguration**

Create `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/TeeTimeOfferPolicyConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Features.WaitlistOffers;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeOfferPolicyConfiguration : IEntityTypeConfiguration<TeeTimeOfferPolicy>
{
    public void Configure(EntityTypeBuilder<TeeTimeOfferPolicy> builder)
    {
        builder.ToTable("TeeTimeOfferPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
    }
}
```

- [ ] **Step 4: Create TeeTimeRequestExpirationPolicyConfiguration**

Create `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/TeeTimeRequestExpirationPolicyConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Features.WalkUpWaitlist;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeRequestExpirationPolicyConfiguration : IEntityTypeConfiguration<TeeTimeRequestExpirationPolicy>
{
    public void Configure(EntityTypeBuilder<TeeTimeRequestExpirationPolicy> builder)
    {
        builder.ToTable("TeeTimeRequestExpirationPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
    }
}
```

- [ ] **Step 5: Add DbSets and configurations to ApplicationDbContext**

In `src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs`:

Add DbSet properties:

```csharp
public DbSet<TeeTimeOfferPolicy> TeeTimeOfferPolicies => Set<TeeTimeOfferPolicy>();
public DbSet<TeeTimeRequestExpirationPolicy> TeeTimeRequestExpirationPolicies => Set<TeeTimeRequestExpirationPolicy>();
```

Add configuration lines in `OnModelCreating`:

```csharp
modelBuilder.ApplyConfiguration(new TeeTimeOfferPolicyConfiguration());
modelBuilder.ApplyConfiguration(new TeeTimeRequestExpirationPolicyConfiguration());
```

Add usings:

```csharp
using Shadowbrook.Api.Features.WaitlistOffers;
using Shadowbrook.Api.Features.WalkUpWaitlist;
```

- [ ] **Step 6: Add EF Core migration**

Run:
```bash
export PATH="$PATH:/home/aaron/.dotnet/tools"
dotnet ef migrations add AddTeeTimeOfferPoliciesAndNotifiedAt --project src/backend/Shadowbrook.Api
```

This migration covers both policy tables and the `NotifiedAt` column on `WaitlistOffers` (from Task 3).

- [ ] **Step 7: Verify build**

Run: `dotnet build shadowbrook.slnx --verbosity quiet`
Expected: 0 errors

- [ ] **Step 8: Commit**

```bash
git add src/backend/Shadowbrook.Api/Features/WaitlistOffers/TeeTimeOfferPolicy.cs src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/TeeTimeRequestExpirationPolicy.cs src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/TeeTimeOfferPolicyConfiguration.cs src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/TeeTimeRequestExpirationPolicyConfiguration.cs src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs src/backend/Shadowbrook.Api/Migrations/
git commit -m "feat: add EF Core mappings and migration for offer policies"
```

---

## Task 7: NotifyNextEligibleGolferHandler

**Files:**
- Create: `src/backend/Shadowbrook.Api/Features/WaitlistOffers/NotifyNextEligibleGolferHandler.cs`
- Create: `tests/Shadowbrook.Api.Tests/Handlers/NotifyNextEligibleGolferHandlerTests.cs`

The command record `NotifyNextEligibleGolfer` lives in the `TeeTimeOfferPolicy.cs` file (already created in Task 6). Add it now.

- [ ] **Step 1: Add the command record to TeeTimeOfferPolicy.cs**

Add at the bottom of `src/backend/Shadowbrook.Api/Features/WaitlistOffers/TeeTimeOfferPolicy.cs`:

```csharp
public record NotifyNextEligibleGolfer(Guid TeeTimeRequestId);
```

- [ ] **Step 2: Implement NotifyNextEligibleGolferHandler**

This handler uses `ApplicationDbContext` directly for cross-aggregate queries (same pattern as the existing `TeeTimeRequestAddedNotifyHandler` and `WaitlistOfferRejectedNextOfferHandler`). Because of this, it cannot be unit tested with NSubstitute — it needs integration tests with a real DB. Integration tests are deferred to Task 11.



Create `src/backend/Shadowbrook.Api/Features/WaitlistOffers/NotifyNextEligibleGolferHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Features.WaitlistOffers;

public static class NotifyNextEligibleGolferHandler
{
    public static async Task Handle(
        NotifyNextEligibleGolfer command,
        ITeeTimeRequestRepository requestRepository,
        IWaitlistOfferRepository offerRepository,
        ApplicationDbContext db,
        ITextMessageService textMessageService,
        IConfiguration configuration,
        CancellationToken ct)
    {
        var request = await requestRepository.GetByIdAsync(command.TeeTimeRequestId);
        if (request is null || request.Status != TeeTimeRequestStatus.Pending)
        {
            return;
        }

        if (request.RemainingSlots <= 0)
        {
            return;
        }

        // Find the waitlist for this course + date
        var waitlist = await db.WalkUpWaitlists
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.CourseId == request.CourseId && w.Date == request.Date, ct);

        if (waitlist is null)
        {
            return;
        }

        // Find GolferWaitlistEntryIds that already have an offer for this request
        var alreadyOfferedEntryIds = await db.WaitlistOffers
            .Where(o => o.TeeTimeRequestId == request.Id)
            .Select(o => o.GolferWaitlistEntryId)
            .ToListAsync(ct);

        // Find the next eligible golfer — active, walk-up, ready, not already offered, group fits
        var nextEntry = (await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == waitlist.Id
                && e.IsWalkUp == true
                && e.IsReady == true
                && e.RemovedAt == null
                && !alreadyOfferedEntryIds.Contains(e.Id)
                && e.GroupSize <= request.RemainingSlots)
            .Join(db.Golfers.IgnoreQueryFilters(),
                e => e.GolferId, g => g.Id,
                (e, g) => new { Entry = e, Golfer = g })
            .ToListAsync(ct))
            .OrderBy(eg => eg.Entry.JoinedAt)
            .ThenBy(eg => eg.Entry.Id.ToString())
            .FirstOrDefault();

        if (nextEntry is null)
        {
            return;
        }

        // Create offer
        var offer = WaitlistOffer.Create(
            teeTimeRequestId: request.Id,
            golferWaitlistEntryId: nextEntry.Entry.Id);

        offerRepository.Add(offer);

        // Send SMS — if this fails, handler throws before MarkNotified, no event fires, no buffer starts
        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == request.CourseId)
            .Select(c => c.Name)
            .FirstAsync(ct);

        var baseUrl = configuration["App:BaseUrl"] ?? "http://localhost:3000";
        var message = $"{courseName}: {request.TeeTime:h:mm tt} tee time available! Claim your spot: {baseUrl}/book/walkup/{offer.Token}";
        await textMessageService.SendAsync(nextEntry.Golfer.Phone, message, ct);

        // Mark as notified — raises GolferNotifiedOfOffer which triggers the policy's buffer
        offer.MarkNotified();
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build shadowbrook.slnx --verbosity quiet`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add src/backend/Shadowbrook.Api/Features/WaitlistOffers/NotifyNextEligibleGolferHandler.cs src/backend/Shadowbrook.Api/Features/WaitlistOffers/TeeTimeOfferPolicy.cs
git commit -m "feat: add NotifyNextEligibleGolferHandler for sequential offer notifications"
```

---

## Task 8: TeeTimeOfferPolicy — Saga Implementation

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Features/WaitlistOffers/TeeTimeOfferPolicy.cs`
- Create: `tests/Shadowbrook.Api.Tests/Policies/TeeTimeOfferPolicyTests.cs`

- [ ] **Step 1: Write failing tests for the policy**

Create `tests/Shadowbrook.Api.Tests/Policies/TeeTimeOfferPolicyTests.cs`:

```csharp
using Shadowbrook.Api.Features.WaitlistOffers;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Policies;

public class TeeTimeOfferPolicyTests
{
    [Fact]
    public void Start_TeeTimeRequestAdded_ReturnsNotifyCommand()
    {
        var requestId = Guid.NewGuid();
        var evt = new TeeTimeRequestAdded
        {
            TeeTimeRequestId = requestId,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 20),
            TeeTime = new TimeOnly(10, 0),
            GolfersNeeded = 2
        };

        var (policy, command) = TeeTimeOfferPolicy.Start(evt);

        Assert.Equal(requestId, policy.Id);
        Assert.False(policy.IsBuffering);
        Assert.Null(policy.CurrentOfferId);
        Assert.Equal(requestId, command.TeeTimeRequestId);
    }

    [Fact]
    public void Handle_GolferNotifiedOfOffer_SetsStateAndReturnsTimeout()
    {
        var policy = new TeeTimeOfferPolicy { Id = Guid.NewGuid() };
        var offerId = Guid.NewGuid();
        var evt = new GolferNotifiedOfOffer
        {
            WaitlistOfferId = offerId,
            TeeTimeRequestId = policy.Id
        };

        var timeout = policy.Handle(evt);

        Assert.Equal(offerId, policy.CurrentOfferId);
        Assert.True(policy.IsBuffering);
        Assert.Equal(policy.Id, timeout.TeeTimeRequestId);
        Assert.Equal(offerId, timeout.OfferId);
    }

    [Fact]
    public void Handle_TeeTimeOfferTimeout_CurrentOffer_SendsNextCommand()
    {
        var offerId = Guid.NewGuid();
        var policy = new TeeTimeOfferPolicy
        {
            Id = Guid.NewGuid(),
            CurrentOfferId = offerId,
            IsBuffering = true
        };
        var timeout = new TeeTimeOfferTimeout(policy.Id, offerId);

        var command = policy.Handle(timeout);

        Assert.NotNull(command);
        Assert.False(policy.IsBuffering);
        Assert.Equal(policy.Id, command!.TeeTimeRequestId);
    }

    [Fact]
    public void Handle_TeeTimeOfferTimeout_StaleOffer_ReturnsNull()
    {
        var policy = new TeeTimeOfferPolicy
        {
            Id = Guid.NewGuid(),
            CurrentOfferId = Guid.NewGuid(),
            IsBuffering = true
        };
        var timeout = new TeeTimeOfferTimeout(policy.Id, Guid.NewGuid()); // different OfferId

        var command = policy.Handle(timeout);

        Assert.Null(command);
        Assert.True(policy.IsBuffering); // unchanged
    }

    [Fact]
    public void Handle_WaitlistOfferRejected_CurrentOffer_SendsNextCommand()
    {
        var offerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeOfferPolicy
        {
            Id = requestId,
            CurrentOfferId = offerId,
            IsBuffering = true
        };
        var evt = new WaitlistOfferRejected
        {
            WaitlistOfferId = offerId,
            TeeTimeRequestId = requestId,
            GolferWaitlistEntryId = Guid.NewGuid(),
            Reason = "Declined"
        };

        var command = policy.Handle(evt);

        Assert.NotNull(command);
        Assert.False(policy.IsBuffering);
        Assert.Equal(requestId, command!.TeeTimeRequestId);
    }

    [Fact]
    public void Handle_WaitlistOfferRejected_DifferentOffer_ReturnsNull()
    {
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeOfferPolicy
        {
            Id = requestId,
            CurrentOfferId = Guid.NewGuid(),
            IsBuffering = true
        };
        var evt = new WaitlistOfferRejected
        {
            WaitlistOfferId = Guid.NewGuid(), // different
            TeeTimeRequestId = requestId,
            GolferWaitlistEntryId = Guid.NewGuid(),
            Reason = "Declined"
        };

        var command = policy.Handle(evt);

        Assert.Null(command);
        Assert.True(policy.IsBuffering);
    }

    [Fact]
    public void Handle_TeeTimeRequestFulfilled_MarksCompleted()
    {
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeOfferPolicy
        {
            Id = requestId,
            CurrentOfferId = Guid.NewGuid(),
            IsBuffering = true
        };
        var evt = new TeeTimeRequestFulfilled { TeeTimeRequestId = requestId };

        policy.Handle(evt);

        Assert.True(policy.IsCompleted);
    }

    [Fact]
    public void Handle_TeeTimeRequestClosed_MarksCompleted()
    {
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeOfferPolicy
        {
            Id = requestId,
            IsBuffering = false
        };
        var evt = new TeeTimeRequestClosed { TeeTimeRequestId = requestId };

        policy.Handle(evt);

        Assert.True(policy.IsCompleted);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~TeeTimeOfferPolicyTests" --verbosity quiet`
Expected: FAIL — policy has no Start/Handle methods, no timeout/command types

- [ ] **Step 3: Implement the full policy**

Update `src/backend/Shadowbrook.Api/Features/WaitlistOffers/TeeTimeOfferPolicy.cs`:

```csharp
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Wolverine;
using Wolverine.Attributes;

namespace Shadowbrook.Api.Features.WaitlistOffers;

public class TeeTimeOfferPolicy : Saga
{
    [SagaIdentity("TeeTimeRequestId")]
    public Guid Id { get; set; }
    public Guid? CurrentOfferId { get; set; }
    public bool IsBuffering { get; set; }

    public static (TeeTimeOfferPolicy, NotifyNextEligibleGolfer) Start(TeeTimeRequestAdded evt)
    {
        var policy = new TeeTimeOfferPolicy
        {
            Id = evt.TeeTimeRequestId
        };

        return (policy, new NotifyNextEligibleGolfer(evt.TeeTimeRequestId));
    }

    public TeeTimeOfferTimeout Handle(GolferNotifiedOfOffer evt)
    {
        CurrentOfferId = evt.WaitlistOfferId;
        IsBuffering = true;

        return new TeeTimeOfferTimeout(Id, evt.WaitlistOfferId);
    }

    public NotifyNextEligibleGolfer? Handle(TeeTimeOfferTimeout timeout)
    {
        if (timeout.OfferId != CurrentOfferId)
        {
            return null;
        }

        IsBuffering = false;
        return new NotifyNextEligibleGolfer(Id);
    }

    public NotifyNextEligibleGolfer? Handle(WaitlistOfferRejected evt)
    {
        if (evt.WaitlistOfferId != CurrentOfferId)
        {
            return null;
        }

        IsBuffering = false;
        return new NotifyNextEligibleGolfer(Id);
    }

    public void Handle(TeeTimeRequestFulfilled evt)
    {
        MarkCompleted();
    }

    public void Handle(TeeTimeRequestClosed evt)
    {
        MarkCompleted();
    }
}

public record NotifyNextEligibleGolfer(Guid TeeTimeRequestId);

public record TeeTimeOfferTimeout(Guid TeeTimeRequestId, Guid OfferId)
    : TimeoutMessage(TimeSpan.FromSeconds(60));
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~TeeTimeOfferPolicyTests" --verbosity quiet`
Expected: 8 passed

- [ ] **Step 5: Commit**

```bash
git add src/backend/Shadowbrook.Api/Features/WaitlistOffers/TeeTimeOfferPolicy.cs tests/Shadowbrook.Api.Tests/Policies/TeeTimeOfferPolicyTests.cs
git commit -m "feat: implement TeeTimeOfferPolicy saga for sequential offer notifications"
```

---

## Task 9: TeeTimeRequestExpirationPolicy & CloseTeeTimeRequestHandler

This task can be done in parallel with Tasks 7-8 (after Tasks 1-6 are complete).

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/TeeTimeRequestExpirationPolicy.cs`
- Create: `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/CloseTeeTimeRequestHandler.cs`
- Create: `tests/Shadowbrook.Api.Tests/Policies/TeeTimeRequestExpirationPolicyTests.cs`
- Create: `tests/Shadowbrook.Api.Tests/Handlers/CloseTeeTimeRequestHandlerTests.cs`

- [ ] **Step 1: Write failing tests for the expiration policy**

Create `tests/Shadowbrook.Api.Tests/Policies/TeeTimeRequestExpirationPolicyTests.cs`:

```csharp
using Shadowbrook.Api.Features.WalkUpWaitlist;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;

namespace Shadowbrook.Api.Tests.Policies;

public class TeeTimeRequestExpirationPolicyTests
{
    [Fact]
    public void Start_TeeTimeRequestAdded_SchedulesTimeout()
    {
        var requestId = Guid.NewGuid();
        var evt = new TeeTimeRequestAdded
        {
            TeeTimeRequestId = requestId,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(14, 30),
            GolfersNeeded = 2
        };

        var (policy, timeout) = TeeTimeRequestExpirationPolicy.Start(evt);

        Assert.Equal(requestId, policy.Id);
        Assert.Equal(requestId, timeout.TeeTimeRequestId);
    }

    [Fact]
    public void Handle_ExpirationTimeout_ReturnsCloseCommandAndMarksCompleted()
    {
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeRequestExpirationPolicy { Id = requestId };
        var timeout = new TeeTimeRequestExpirationTimeout(requestId);

        var command = policy.Handle(timeout);

        Assert.IsType<CloseTeeTimeRequest>(command);
        Assert.Equal(requestId, command.TeeTimeRequestId);
        Assert.True(policy.IsCompleted);
    }

    [Fact]
    public void Handle_TeeTimeRequestFulfilled_MarksCompleted()
    {
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeRequestExpirationPolicy { Id = requestId };
        var evt = new TeeTimeRequestFulfilled { TeeTimeRequestId = requestId };

        policy.Handle(evt);

        Assert.True(policy.IsCompleted);
    }

    [Fact]
    public void Handle_TeeTimeRequestClosed_MarksCompleted()
    {
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeRequestExpirationPolicy { Id = requestId };
        var evt = new TeeTimeRequestClosed { TeeTimeRequestId = requestId };

        policy.Handle(evt);

        Assert.True(policy.IsCompleted);
    }
}
```

- [ ] **Step 2: Write failing tests for CloseTeeTimeRequestHandler**

Create `tests/Shadowbrook.Api.Tests/Handlers/CloseTeeTimeRequestHandlerTests.cs`:

```csharp
using NSubstitute;
using Shadowbrook.Api.Features.WalkUpWaitlist;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class CloseTeeTimeRequestHandlerTests
{
    private readonly ITeeTimeRequestRepository requestRepo = Substitute.For<ITeeTimeRequestRepository>();

    [Fact]
    public async Task Handle_RequestNotFound_DoesNothing()
    {
        this.requestRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((TeeTimeRequest?)null);

        var command = new CloseTeeTimeRequest(Guid.NewGuid());
        await CloseTeeTimeRequestHandler.Handle(command, this.requestRepo);

        // No exception thrown, no side effects
    }

    [Fact]
    public async Task Handle_PendingRequest_ClosesIt()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 20), new TimeOnly(10, 0), 2,
            Substitute.For<ITeeTimeRequestRepository>());
        request.ClearDomainEvents();

        this.requestRepo.GetByIdAsync(request.Id).Returns(request);

        var command = new CloseTeeTimeRequest(request.Id);
        await CloseTeeTimeRequestHandler.Handle(command, this.requestRepo);

        Assert.Equal(TeeTimeRequestStatus.Closed, request.Status);
        var domainEvent = Assert.Single(request.DomainEvents);
        Assert.IsType<TeeTimeRequestClosed>(domainEvent);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~TeeTimeRequestExpirationPolicyTests|FullyQualifiedName~CloseTeeTimeRequestHandlerTests" --verbosity quiet`
Expected: FAIL — classes don't exist

- [ ] **Step 4: Implement TeeTimeRequestExpirationPolicy**

Update `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/TeeTimeRequestExpirationPolicy.cs`:

```csharp
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Wolverine;
using Wolverine.Attributes;

namespace Shadowbrook.Api.Features.WalkUpWaitlist;

public class TeeTimeRequestExpirationPolicy : Saga
{
    [SagaIdentity("TeeTimeRequestId")]
    public Guid Id { get; set; }

    public static (TeeTimeRequestExpirationPolicy, TeeTimeRequestExpirationTimeout) Start(TeeTimeRequestAdded evt)
    {
        var policy = new TeeTimeRequestExpirationPolicy
        {
            Id = evt.TeeTimeRequestId
        };

        // Schedule timeout for tee time in UTC
        var teeTimeUtc = evt.Date.ToDateTime(evt.TeeTime, DateTimeKind.Utc);
        var delay = teeTimeUtc - DateTimeOffset.UtcNow;
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        var timeout = new TeeTimeRequestExpirationTimeout(evt.TeeTimeRequestId)
        {
            ScheduleDelay = delay
        };

        return (policy, timeout);
    }

    public CloseTeeTimeRequest Handle(TeeTimeRequestExpirationTimeout timeout)
    {
        MarkCompleted();
        return new CloseTeeTimeRequest(timeout.TeeTimeRequestId);
    }

    public void Handle(TeeTimeRequestFulfilled evt)
    {
        MarkCompleted();
    }

    public void Handle(TeeTimeRequestClosed evt)
    {
        MarkCompleted();
    }
}

public record TeeTimeRequestExpirationTimeout(Guid TeeTimeRequestId) : TimeoutMessage(TimeSpan.Zero);

public record CloseTeeTimeRequest(Guid TeeTimeRequestId);
```

Note: `TeeTimeRequestExpirationTimeout` uses `TimeSpan.Zero` as the base constructor value — the actual delay is always set dynamically via `ScheduleDelay` in the `Start` method, calculated from the tee time.

- [ ] **Step 5: Implement CloseTeeTimeRequestHandler**

Create `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/CloseTeeTimeRequestHandler.cs`:

```csharp
using Shadowbrook.Domain.TeeTimeRequestAggregate;

namespace Shadowbrook.Api.Features.WalkUpWaitlist;

public static class CloseTeeTimeRequestHandler
{
    public static async Task Handle(
        CloseTeeTimeRequest command,
        ITeeTimeRequestRepository requestRepository)
    {
        var request = await requestRepository.GetByIdAsync(command.TeeTimeRequestId);
        if (request is null)
        {
            return;
        }

        request.Close();
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~TeeTimeRequestExpirationPolicyTests|FullyQualifiedName~CloseTeeTimeRequestHandlerTests" --verbosity quiet`
Expected: All pass

- [ ] **Step 7: Commit**

```bash
git add src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/TeeTimeRequestExpirationPolicy.cs src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/CloseTeeTimeRequestHandler.cs tests/Shadowbrook.Api.Tests/Policies/TeeTimeRequestExpirationPolicyTests.cs tests/Shadowbrook.Api.Tests/Handlers/CloseTeeTimeRequestHandlerTests.cs
git commit -m "feat: add TeeTimeRequestExpirationPolicy and CloseTeeTimeRequestHandler"
```

---

## Task 10: Remove Old Handlers

**Files:**
- Delete: `src/backend/Shadowbrook.Api/Features/WaitlistOffers/TeeTimeRequestAddedNotifyHandler.cs`
- Delete: `src/backend/Shadowbrook.Api/Features/WaitlistOffers/WaitlistOfferRejectedNextOfferHandler.cs`

- [ ] **Step 1: Delete the old handlers**

```bash
git rm src/backend/Shadowbrook.Api/Features/WaitlistOffers/TeeTimeRequestAddedNotifyHandler.cs
git rm src/backend/Shadowbrook.Api/Features/WaitlistOffers/WaitlistOfferRejectedNextOfferHandler.cs
```

- [ ] **Step 2: Verify build**

Run: `dotnet build shadowbrook.slnx --verbosity quiet`
Expected: 0 errors. If any files reference the deleted handler classes, fix them.

- [ ] **Step 3: Run full test suite**

Run: `dotnet test shadowbrook.slnx --verbosity quiet`
Expected: All pass

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor: remove TeeTimeRequestAddedNotifyHandler and WaitlistOfferRejectedNextOfferHandler

Replaced by TeeTimeOfferPolicy + NotifyNextEligibleGolferHandler for sequential offer notifications with buffer timing."
```

---

## Task 11: Integration Tests & Final Verification

**Files:**
- Create: `tests/Shadowbrook.Api.Tests/Policies/TeeTimeOfferPolicyIntegrationTests.cs` (if time permits)

- [ ] **Step 1: Run all tests**

Run: `dotnet test shadowbrook.slnx --verbosity quiet`
Expected: All pass

- [ ] **Step 2: Verify build**

Run: `dotnet build shadowbrook.slnx --verbosity quiet`
Expected: 0 errors

- [ ] **Step 3: Check for pending model changes**

Run:
```bash
export PATH="$PATH:/home/aaron/.dotnet/tools"
dotnet ef migrations has-pending-model-changes --project src/backend/Shadowbrook.Api
```
Expected: No pending changes

- [ ] **Step 4: Consider integration test for saga lifecycle (optional)**

The unit tests in Tasks 8-9 verify policy method behavior in isolation. An integration test that publishes `TeeTimeRequestAdded` through the full Wolverine pipeline and verifies `NotifyNextEligibleGolfer` is dispatched would catch saga correlation failures. This is the highest-risk piece — if `[SagaIdentity("TeeTimeRequestId")]` doesn't work as expected, only an integration test would catch it. Defer if time is tight, but flag as a follow-up.

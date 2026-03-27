# Late Claim on Stale Offers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow golfers to claim waitlist offer slots after the response window expires, as long as the slot hasn't been taken by someone else.

**Architecture:** Add `IsStale` flag to `WaitlistOffer`. Replace the hard-reject on timeout with `MarkStale()` that keeps the offer in `Pending` status but raises `WaitlistOfferStale` to continue the cascade. The existing claim flow works unchanged since stale offers remain `Pending`.

**Tech Stack:** .NET 10, EF Core 10, Wolverine sagas, xUnit, NSubstitute

---

### File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/WaitlistOffer.cs` | Add `IsStale` property and `MarkStale()` method |
| Modify | `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistOfferConfiguration.cs` | Map `IsStale` column |
| Create | EF migration (auto-generated) | Add `IsStale` column to `WaitlistOffers` table |
| Rename | `src/backend/Shadowbrook.Api/Features/Waitlist/Handlers/RejectStaleOffer/` → `MarkOfferStale/` | Rename folder to match new command |
| Modify | `src/backend/Shadowbrook.Api/Features/Waitlist/Handlers/RejectStaleOffer/Handler.cs` → `MarkOfferStale/Handler.cs` | Call `MarkStale()` instead of `Reject()` |
| Modify | `src/backend/Shadowbrook.Api/Features/Waitlist/Policies/WaitlistOfferResponsePolicy.cs` | Return `MarkOfferStale` command, rename record |
| Modify | `tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs` | Tests for `MarkStale()` |
| Modify | `tests/Shadowbrook.Api.Tests/Handlers/RejectStaleOfferHandlerTests.cs` → `MarkOfferStaleHandlerTests.cs` | Update tests for renamed handler |
| Modify | `tests/Shadowbrook.Api.Tests/Policies/WaitlistOfferResponsePolicyTests.cs` | Update timeout test for `MarkOfferStale` |

---

### Task 1: Add `IsStale` property and `MarkStale()` to `WaitlistOffer`

**Files:**
- Modify: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/WaitlistOffer.cs`
- Test: `tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs`

- [ ] **Step 1: Write failing tests for `MarkStale()`**

Add these tests to `tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs`:

```csharp
[Fact]
public void MarkStale_PendingOffer_SetsIsStaleAndRaisesEvent()
{
    var offer = CreateOffer();
    offer.ClearDomainEvents();

    offer.MarkStale();

    Assert.True(offer.IsStale);
    Assert.Equal(OfferStatus.Pending, offer.Status);
    var domainEvent = Assert.Single(offer.DomainEvents);
    var stale = Assert.IsType<WaitlistOfferStale>(domainEvent);
    Assert.Equal(offer.Id, stale.WaitlistOfferId);
    Assert.Equal(offer.OpeningId, stale.OpeningId);
}

[Fact]
public void MarkStale_AlreadyStale_IsIdempotent()
{
    var offer = CreateOffer();
    offer.MarkStale();
    offer.ClearDomainEvents();

    offer.MarkStale();

    Assert.True(offer.IsStale);
    Assert.Empty(offer.DomainEvents);
}

[Fact]
public void MarkStale_AcceptedOffer_IsIdempotent()
{
    var offer = CreateOffer();
    // Accept requires internal access via claim service — use Reject to move out of Pending
    offer.Reject("taken");
    offer.ClearDomainEvents();

    offer.MarkStale();

    Assert.Equal(OfferStatus.Rejected, offer.Status);
    Assert.Empty(offer.DomainEvents);
}

[Fact]
public void Accept_StaleOffer_TransitionsToAccepted()
{
    var offer = CreateOffer();
    offer.MarkStale();
    offer.ClearDomainEvents();

    // Accept is internal — test via WaitlistOfferClaimService in Task 4
    // This test verifies IsStale doesn't block status transitions
    Assert.Equal(OfferStatus.Pending, offer.Status);
    Assert.True(offer.IsStale);
}

[Fact]
public void Create_SetsIsStaleToFalse()
{
    var offer = CreateOffer();

    Assert.False(offer.IsStale);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~WaitlistOfferTests" --no-restore -v minimal`
Expected: Compilation errors — `IsStale` and `MarkStale()` don't exist yet.

- [ ] **Step 3: Implement `IsStale` and `MarkStale()` on `WaitlistOffer`**

In `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/WaitlistOffer.cs`, add the property after `NotifiedAt`:

```csharp
public bool IsStale { get; private set; }
```

Add the `MarkStale()` method after the `Reject()` method:

```csharp
public void MarkStale()
{
    if (Status != OfferStatus.Pending || IsStale)
    {
        return; // Idempotent — already resolved or already stale
    }

    IsStale = true;

    AddDomainEvent(new WaitlistOfferStale
    {
        WaitlistOfferId = Id,
        OpeningId = OpeningId
    });
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~WaitlistOfferTests" --no-restore -v minimal`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/WaitlistOffer.cs tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs
git commit -m "feat: add IsStale flag and MarkStale() to WaitlistOffer aggregate"
```

---

### Task 2: Add EF configuration and migration for `IsStale`

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistOfferConfiguration.cs`
- Create: EF migration (auto-generated)

- [ ] **Step 1: Add `IsStale` column mapping to EF configuration**

In `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistOfferConfiguration.cs`, add after the `NotifiedAt` property line (line 19):

```csharp
builder.Property(o => o.IsStale).HasDefaultValue(false);
```

- [ ] **Step 2: Generate EF migration**

Run:
```bash
export PATH="$PATH:/home/aaron/.dotnet/tools"
dotnet ef migrations add AddWaitlistOfferIsStale --project src/backend/Shadowbrook.Api
```
Expected: Migration files created in `src/backend/Shadowbrook.Api/Infrastructure/Data/Migrations/`.

- [ ] **Step 3: Verify no pending model changes**

Run:
```bash
dotnet ef migrations has-pending-model-changes --project src/backend/Shadowbrook.Api
```
Expected: No pending changes.

- [ ] **Step 4: Commit**

```bash
git add src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistOfferConfiguration.cs src/backend/Shadowbrook.Api/Infrastructure/Data/Migrations/
git commit -m "feat: add IsStale column to WaitlistOffers table"
```

---

### Task 3: Rename `RejectStaleOffer` to `MarkOfferStale` and update handler

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Features/Waitlist/Policies/WaitlistOfferResponsePolicy.cs` (rename record)
- Rename: `src/backend/Shadowbrook.Api/Features/Waitlist/Handlers/RejectStaleOffer/` → `MarkOfferStale/`
- Modify: handler file inside renamed folder
- Modify: `tests/Shadowbrook.Api.Tests/Handlers/RejectStaleOfferHandlerTests.cs` (rename + update)
- Modify: `tests/Shadowbrook.Api.Tests/Policies/WaitlistOfferResponsePolicyTests.cs`

- [ ] **Step 1: Rename the command record**

In `src/backend/Shadowbrook.Api/Features/Waitlist/Policies/WaitlistOfferResponsePolicy.cs`, change the record at the bottom of the file:

Old:
```csharp
public record RejectStaleOffer(Guid WaitlistOfferId, Guid OpeningId);
```

New:
```csharp
public record MarkOfferStale(Guid WaitlistOfferId, Guid OpeningId);
```

- [ ] **Step 2: Update the policy timeout handler return type**

In the same file, update the `Handle(OfferResponseBufferTimeout)` method:

Old:
```csharp
public RejectStaleOffer Handle(OfferResponseBufferTimeout timeout)
{
    MarkCompleted();
    return new RejectStaleOffer(Id, OpeningId);
}
```

New:
```csharp
public MarkOfferStale Handle(OfferResponseBufferTimeout timeout)
{
    MarkCompleted();
    return new MarkOfferStale(Id, OpeningId);
}
```

- [ ] **Step 3: Rename the handler folder and file**

```bash
mv src/backend/Shadowbrook.Api/Features/Waitlist/Handlers/RejectStaleOffer src/backend/Shadowbrook.Api/Features/Waitlist/Handlers/MarkOfferStale
```

- [ ] **Step 4: Update the handler to call `MarkStale()` instead of `Reject()`**

Replace the entire contents of `src/backend/Shadowbrook.Api/Features/Waitlist/Handlers/MarkOfferStale/Handler.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class MarkOfferStaleHandler
{
    public static async Task Handle(
        MarkOfferStale command,
        IWaitlistOfferRepository offerRepository,
        ILogger logger)
    {
        var offer = await offerRepository.GetRequiredByIdAsync(command.WaitlistOfferId);

        if (offer.Status != OfferStatus.Pending)
        {
            logger.LogWarning(
                "WaitlistOffer {OfferId} is {Status}, not pending — skipping stale marking",
                command.WaitlistOfferId,
                offer.Status);
            return;
        }

        offer.MarkStale();
    }
}
```

Note: The handler no longer returns `WaitlistOfferStale` directly — it's now raised as a domain event by `MarkStale()`, which gets automatically published by the EF transactional middleware when the transaction commits.

- [ ] **Step 5: Build to verify compilation**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeds (test project may have warnings about renamed types, but should compile since we haven't renamed test files yet).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: rename RejectStaleOffer to MarkOfferStale, call MarkStale() instead of Reject()"
```

---

### Task 4: Update tests for renamed handler and policy

**Files:**
- Rename + Modify: `tests/Shadowbrook.Api.Tests/Handlers/RejectStaleOfferHandlerTests.cs` → `MarkOfferStaleHandlerTests.cs`
- Modify: `tests/Shadowbrook.Api.Tests/Policies/WaitlistOfferResponsePolicyTests.cs`

- [ ] **Step 1: Rename the handler test file**

```bash
mv tests/Shadowbrook.Api.Tests/Handlers/RejectStaleOfferHandlerTests.cs tests/Shadowbrook.Api.Tests/Handlers/MarkOfferStaleHandlerTests.cs
```

- [ ] **Step 2: Rewrite handler tests for `MarkOfferStaleHandler`**

Replace the entire contents of `tests/Shadowbrook.Api.Tests/Handlers/MarkOfferStaleHandlerTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class MarkOfferStaleHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public MarkOfferStaleHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    private WaitlistOffer CreatePendingOffer(Guid openingId)
    {
        var offer = WaitlistOffer.Create(openingId, Guid.NewGuid(), Guid.NewGuid(), 1, true, Guid.NewGuid(), new DateOnly(2026, 3, 25), new TimeOnly(10, 0), this.timeProvider);
        offer.ClearDomainEvents();
        return offer;
    }

    [Fact]
    public async Task Handle_PendingOffer_MarksStaleAndRaisesDomainEvent()
    {
        var openingId = Guid.NewGuid();
        var offer = CreatePendingOffer(openingId);
        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var command = new MarkOfferStale(offer.Id, openingId);

        await MarkOfferStaleHandler.Handle(command, this.offerRepo, NullLogger.Instance);

        Assert.True(offer.IsStale);
        Assert.Equal(OfferStatus.Pending, offer.Status);
        var domainEvent = Assert.Single(offer.DomainEvents);
        var stale = Assert.IsType<WaitlistOfferStale>(domainEvent);
        Assert.Equal(offer.Id, stale.WaitlistOfferId);
        Assert.Equal(openingId, stale.OpeningId);
    }

    [Fact]
    public async Task Handle_AlreadyRejectedOffer_LogsAndSkips()
    {
        var openingId = Guid.NewGuid();
        var offer = CreatePendingOffer(openingId);
        offer.Reject("already handled");
        offer.ClearDomainEvents();
        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var command = new MarkOfferStale(offer.Id, openingId);

        await MarkOfferStaleHandler.Handle(command, this.offerRepo, NullLogger.Instance);

        Assert.False(offer.IsStale);
        Assert.Empty(offer.DomainEvents);
    }

    [Fact]
    public async Task Handle_OfferNotFound_Throws()
    {
        var offerId = Guid.NewGuid();
        this.offerRepo.GetByIdAsync(offerId).Returns((WaitlistOffer?)null);

        var command = new MarkOfferStale(offerId, Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => MarkOfferStaleHandler.Handle(command, this.offerRepo, NullLogger.Instance));
    }
}
```

- [ ] **Step 3: Update policy test for `MarkOfferStale` return type**

In `tests/Shadowbrook.Api.Tests/Policies/WaitlistOfferResponsePolicyTests.cs`, update the timeout test:

Old:
```csharp
[Fact]
public void Handle_BufferTimeout_ReturnsRejectCommandAndMarksCompleted()
{
    var offerId = Guid.NewGuid();
    var openingId = Guid.NewGuid();
    var policy = new WaitlistOfferResponsePolicy { Id = offerId, OpeningId = openingId };

    var command = policy.Handle(new OfferResponseBufferTimeout(TimeSpan.FromSeconds(60)));

    Assert.IsType<RejectStaleOffer>(command);
    Assert.Equal(offerId, command.WaitlistOfferId);
    Assert.Equal(openingId, command.OpeningId);
    Assert.True(policy.IsCompleted());
}
```

New:
```csharp
[Fact]
public void Handle_BufferTimeout_ReturnsMarkStaleCommandAndMarksCompleted()
{
    var offerId = Guid.NewGuid();
    var openingId = Guid.NewGuid();
    var policy = new WaitlistOfferResponsePolicy { Id = offerId, OpeningId = openingId };

    var command = policy.Handle(new OfferResponseBufferTimeout(TimeSpan.FromSeconds(60)));

    Assert.IsType<MarkOfferStale>(command);
    Assert.Equal(offerId, command.WaitlistOfferId);
    Assert.Equal(openingId, command.OpeningId);
    Assert.True(policy.IsCompleted());
}
```

- [ ] **Step 4: Run all affected tests**

Run: `dotnet test tests/Shadowbrook.Api.Tests/ --filter "FullyQualifiedName~MarkOfferStale|FullyQualifiedName~WaitlistOfferResponsePolicy" --no-restore -v minimal`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "test: update handler and policy tests for MarkOfferStale rename"
```

---

### Task 5: Add claim service test for stale offer acceptance

**Files:**
- Modify: `tests/Shadowbrook.Domain.Tests/WaitlistServices/WaitlistOfferClaimServiceTests.cs`

- [ ] **Step 1: Write test for accepting a stale offer when slot is available**

Add to `tests/Shadowbrook.Domain.Tests/WaitlistServices/WaitlistOfferClaimServiceTests.cs`:

```csharp
[Fact]
public void AcceptOffer_StaleOffer_WhenClaimSucceeds_ReturnsSuccessAndAccepts()
{
    var opening = CreateOpening(slotsAvailable: 4);
    var offer = CreateOffer(opening.Id, groupSize: 2);
    offer.MarkStale();
    opening.ClearDomainEvents();
    offer.ClearDomainEvents();

    var result = this.sut.AcceptOffer(offer, opening);

    Assert.True(result.Success);
    Assert.Equal(OfferStatus.Accepted, offer.Status);
    Assert.Contains(offer.DomainEvents, e => e is WaitlistOfferAccepted);
}

[Fact]
public void AcceptOffer_StaleOffer_WhenClaimFails_ReturnsFailureAndRejects()
{
    var opening = CreateOpening(slotsAvailable: 1);
    var offer = CreateOffer(opening.Id, groupSize: 2);
    offer.MarkStale();
    opening.ClearDomainEvents();
    offer.ClearDomainEvents();

    var result = this.sut.AcceptOffer(offer, opening);

    Assert.False(result.Success);
    Assert.Equal(OfferStatus.Rejected, offer.Status);
}
```

- [ ] **Step 2: Run claim service tests**

Run: `dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~WaitlistOfferClaimServiceTests" --no-restore -v minimal`
Expected: All tests pass (no code changes needed — stale offers are still `Pending`, so `Accept()` and `Reject()` work).

- [ ] **Step 3: Commit**

```bash
git add tests/Shadowbrook.Domain.Tests/WaitlistServices/WaitlistOfferClaimServiceTests.cs
git commit -m "test: add claim service tests for stale offer late claim scenarios"
```

---

### Task 6: Run full test suite and format

- [ ] **Step 1: Format code**

Run: `dotnet format shadowbrook.slnx`

- [ ] **Step 2: Run full test suite**

Run: `dotnet test shadowbrook.slnx --no-restore -v minimal`
Expected: All tests pass.

- [ ] **Step 3: Run `make dev` to verify runtime**

Run: `make dev`
Expected: API starts on :5221, migrations apply, no errors.

- [ ] **Step 4: Commit any formatting changes**

```bash
git add -A
git commit -m "chore: format code"
```

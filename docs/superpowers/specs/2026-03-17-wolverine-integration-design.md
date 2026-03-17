# Wolverine Integration Design

**Goal:** Replace the custom `InProcessDomainEventPublisher` with Wolverine, gaining automatic handler discovery, retry policies, separated handler execution, and a migration path to Azure Service Bus. Transactional outbox is deferred to a follow-up — this migration is a 1:1 behavioral replacement first.

**Scope:** The issue/37-claim-tee-time-slot branch. All 11 existing event handlers are migrated. Domain aggregates and event contracts are unchanged.

---

## What Changes

### 1. NuGet Packages

Add to `Shadowbrook.Api.csproj`:

- `WolverineFx` — core framework
- `WolverineFx.SqlServer` — SQL Server transport + durable messaging
- `WolverineFx.EntityFrameworkCore` — EF Core integration (needed for Wolverine's scoped DbContext handling; also enables future transactional outbox via `IDbContextOutbox`)

Add to `Shadowbrook.Api.Tests.csproj`:

- `WolverineFx` — needed for test host configuration (provides `DisableAllExternalWolverineTransports` and `RunWolverineInSoloMode`)

### 2. Program.cs — Wolverine Registration

Replace the manual event handler registrations and `IDomainEventPublisher` with Wolverine:

```csharp
// Remove these:
builder.Services.AddScoped<IDomainEventPublisher, InProcessDomainEventPublisher>();
builder.Services.AddScoped<IDomainEventHandler<GolferJoinedWaitlist>, ...>();
// ... all 11 handler registrations

// Add this:
builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

    opts.UseSqlServerPersistenceAndTransport(connectionString, "wolverine")
        .AutoProvision();

    // Each handler for the same event runs independently — a failure in one
    // does not block or roll back others (matches current InProcessDomainEventPublisher behavior)
    opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;

    // Retry concurrency conflicts automatically (replaces manual retry in FillHandler)
    opts.Handlers.OnException<DbUpdateConcurrencyException>()
        .RetryTimes(3);
});
```

Key points:
- `UseSqlServerPersistenceAndTransport` configures SQL Server as both the message transport and durable storage, using a dedicated `wolverine` schema. `.AutoProvision()` creates the tables automatically.
- `MultipleHandlerBehavior.Separated` ensures that when two handlers subscribe to the same event (e.g., `WaitlistOfferAccepted` has both `FillHandler` and `SmsHandler`), they execute independently with isolated failure domains. This matches the current `InProcessDomainEventPublisher` behavior where each handler is wrapped in its own try/catch.
- Handler discovery scans the application assembly — no manual registration needed.
- Later, swap `UseSqlServerPersistenceAndTransport` for `UseAzureServiceBus` with minimal changes.

### 3. ApplicationDbContext.SaveChangesAsync — Bridge to Wolverine

The override stays but pushes events through Wolverine's `IMessageBus` instead of `IDomainEventPublisher`:

```csharp
public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUser? currentUser = null,
    IMessageBus? bus = null) : DbContext(options)
{
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);

        var domainEvents = ChangeTracker.Entries<Entity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        foreach (var entity in ChangeTracker.Entries<Entity>())
        {
            entity.Entity.ClearDomainEvents();
        }

        if (bus is not null)
        {
            foreach (var domainEvent in domainEvents)
            {
                await bus.PublishAsync(domainEvent);
            }
        }

        return result;
    }
}
```

**Reliability note:** This pattern publishes events *after* `base.SaveChangesAsync()` returns, outside the EF transaction. If the process crashes between save and publish, events are lost. This is the **same reliability as the current `InProcessDomainEventPublisher`** — not a regression. True transactional outbox (where events are written to the outbox table within the same DB transaction as the entity save) requires Wolverine's `IDbContextOutbox` integration, which is deferred to a follow-up.

### 4. Delete Custom Event Infrastructure

Remove these files entirely:

- `Infrastructure/Events/IDomainEventPublisher.cs`
- `Infrastructure/Events/InProcessDomainEventPublisher.cs`
- `Infrastructure/Events/IDomainEventHandler.cs`

These are fully replaced by Wolverine's `IMessageBus` and handler conventions.

### 5. Migrate Event Handlers

Each handler drops the `IDomainEventHandler<T>` interface and renames `HandleAsync` to `Handle`. The handler body stays the same. Dependencies stay as constructor injection (instance style).

**Convention:** Use instance style with primary constructors (closest to current pattern, minimal diff).

**Before:**

```csharp
public class TeeTimeSlotFillFailedHandler(
    IWaitlistOfferRepository offerRepository)
    : IDomainEventHandler<TeeTimeSlotFillFailed>
{
    public async Task HandleAsync(TeeTimeSlotFillFailed domainEvent, CancellationToken ct = default)
    {
        var offer = await offerRepository.GetByIdAsync(domainEvent.OfferId);
        if (offer is null) return;
        offer.Reject(domainEvent.Reason);
        await offerRepository.SaveAsync();
    }
}
```

**After:**

```csharp
public class TeeTimeSlotFillFailedHandler(
    IWaitlistOfferRepository offerRepository)
{
    public async Task Handle(TeeTimeSlotFillFailed domainEvent, CancellationToken ct)
    {
        var offer = await offerRepository.GetByIdAsync(domainEvent.OfferId);
        if (offer is null) return;
        offer.Reject(domainEvent.Reason);
        await offerRepository.SaveAsync();
    }
}
```

Changes per handler:
- Remove `: IDomainEventHandler<T>` interface
- Rename `HandleAsync` to `Handle`
- Remove `= default` from CancellationToken (Wolverine passes it)
- Replace any `IDomainEventPublisher` dependency with `IMessageBus`

**Handlers that publish follow-on events** (currently injecting `IDomainEventPublisher`):
- `WaitlistOfferAcceptedFillHandler` — uses `IDomainEventPublisher` to publish `TeeTimeSlotFilled` / `TeeTimeSlotFillFailed`. Change to `IMessageBus`.

All other handlers publish events implicitly through domain aggregates + `SaveChangesAsync`, so they need no change beyond the interface/method rename.

### 6. Domain Events — Keep IDomainEvent Interface

Wolverine does not require messages to implement any interface. However, the `IDomainEvent` interface (`EventId`, `OccurredAt`) is used by the `SaveChangesAsync` harvesting logic and is useful for the domain model. **Keep it.** Wolverine handles these records fine — it just needs them to be public types.

No changes to any event records.

### 7. Handler-Raised Events vs Aggregate-Raised Events

Two patterns coexist in the current codebase:

1. **Aggregate-raised:** Domain aggregate calls `AddDomainEvent()`, events are harvested by `SaveChangesAsync` and published via `IMessageBus`. Example: `WaitlistOffer.Accept()` raises `WaitlistOfferAccepted`.

2. **Handler-raised:** A handler explicitly calls `eventPublisher.PublishAsync()` to emit a follow-on event that isn't associated with an aggregate save. Example: `WaitlistOfferAcceptedFillHandler` publishes `TeeTimeSlotFilled` or `TeeTimeSlotFillFailed`.

With Wolverine, pattern (1) stays the same. Pattern (2) changes from `eventPublisher.PublishAsync(event)` to `bus.PublishAsync(event)`.

**Decision:** Keep explicit `IMessageBus.PublishAsync()` for handler-raised events. Wolverine supports return-based cascading (returning a message from `Handle` to auto-publish), but that's awkward when a handler might return different event types depending on success/failure (like the fill handler). We can migrate individual handlers to return style later if desired.

### 8. Concurrency Retry

`WaitlistOfferAcceptedFillHandler` currently has manual concurrency retry logic (catch `DbUpdateConcurrencyException`, clear change tracker, retry once). The global Wolverine retry policy (Section 2) replaces this.

Wolverine retries create a **new handler invocation with a fresh DI scope** — scoped services like `ApplicationDbContext` get a clean instance, so the change tracker is naturally empty. This is why the manual `db.ChangeTracker.Clear()` and `TryFillAsync` pattern can be removed entirely.

The handler simplifies to a single fill attempt:

```csharp
public async Task Handle(WaitlistOfferAccepted domainEvent, ...)
{
    var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId);
    if (entry is null) return;

    var request = await requestRepository.GetByIdAsync(domainEvent.TeeTimeRequestId);
    if (request is null)
    {
        await bus.PublishAsync(new TeeTimeSlotFillFailed { ... });
        return;
    }

    var result = request.Fill(domainEvent.GolferId, entry.GroupSize, domainEvent.BookingId);

    if (!result.Success)
    {
        await bus.PublishAsync(new TeeTimeSlotFillFailed { ... });
        return;
    }

    await requestRepository.SaveAsync();
    await bus.PublishAsync(new TeeTimeSlotFilled { ... });
}
```

If `SaveAsync` throws `DbUpdateConcurrencyException`, Wolverine retries the entire handler up to 3 times with a fresh scope.

**Note:** The retry policy is global — all handlers retry on `DbUpdateConcurrencyException`. This is acceptable because only handlers that call `SaveAsync` on concurrency-protected entities can throw this exception, and retrying is the correct response in all cases.

### 9. Integration Tests

The `TestWebApplicationFactory` uses SQLite in-memory. Wolverine's SQL Server transport won't work with SQLite.

**Solution:** Disable external transports and run in solo mode:

```csharp
// In TestWebApplicationFactory:
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.ConfigureServices(services =>
    {
        // ... existing SQLite setup ...

        services.DisableAllExternalWolverineTransports();
        services.RunWolverineInSoloMode();
    });
}
```

- `DisableAllExternalWolverineTransports()` stubs out the SQL Server transport so Wolverine runs purely in-process
- `RunWolverineInSoloMode()` disables durability agent and leader election (single-node test optimization)
- Handlers still execute when events are published — business logic is fully tested
- Transport durability is not tested, but that's infrastructure, not business logic

### 10. Wolverine Schema

Wolverine with SQL Server transport creates its own tables (`wolverine.incoming_envelopes`, `wolverine.outgoing_envelopes`, `wolverine.dead_letters`, etc.) in the `wolverine` schema. `.AutoProvision()` creates these automatically on startup — no EF migration needed.

The existing event infrastructure is purely in-memory (no outbox tables), so no migration is needed to remove anything either.

---

## What Does NOT Change

- **Domain aggregates** — `WaitlistOffer`, `TeeTimeRequest`, `Booking`, `GolferWaitlistEntry`, `WalkUpWaitlist` — zero changes
- **Domain events** — all event records stay exactly as-is
- **Handler business logic** — the 5-30 lines inside each handler that do the actual work
- **Endpoints** — no changes to any endpoint
- **Frontend** — no changes
- **Domain tests** — no changes (they don't touch event infrastructure)
- **Singleton services** (e.g., `ITextMessageService`) — work fine in Wolverine handlers since Wolverine creates scoped containers per message invocation

---

## Multiple Handlers for Same Event

The current codebase has four events with multiple handlers:

| Event | Handlers |
|-------|----------|
| `WaitlistOfferAccepted` | `FillHandler`, `SmsHandler` |
| `BookingCreated` | `RemoveFromWaitlistHandler`, `ConfirmationSmsHandler` |
| `WaitlistOfferRejected` | `NextOfferHandler`, `SmsHandler` |

With `MultipleHandlerBehavior.Separated` (configured in Section 2), each handler gets its own independent message subscription and local queue. This means:
- A failure in `FillHandler` does not prevent `SmsHandler` from running
- Each handler retries independently
- Handlers may execute in any order (but this is already true with the current publisher)

---

## File Inventory

### Files to Delete

| File | Reason |
|------|--------|
| `Infrastructure/Events/IDomainEventPublisher.cs` | Replaced by `IMessageBus` |
| `Infrastructure/Events/InProcessDomainEventPublisher.cs` | Replaced by Wolverine |
| `Infrastructure/Events/IDomainEventHandler.cs` | Replaced by Wolverine conventions |

### Files to Modify

| File | Change |
|------|--------|
| `Shadowbrook.Api.csproj` | Add Wolverine NuGet packages |
| `Program.cs` | Replace handler registrations with `UseWolverine()` |
| `Infrastructure/Data/ApplicationDbContext.cs` | `IDomainEventPublisher` -> `IMessageBus` |
| `EventHandlers/GolferJoinedWaitlistSmsHandler.cs` | Drop interface, rename method |
| `EventHandlers/TeeTimeRequestAddedNotifyHandler.cs` | Drop interface, rename method |
| `EventHandlers/WaitlistOfferAcceptedFillHandler.cs` | Drop interface, rename method, `IDomainEventPublisher` -> `IMessageBus`, remove manual retry |
| `EventHandlers/WaitlistOfferAcceptedSmsHandler.cs` | Drop interface, rename method |
| `EventHandlers/TeeTimeSlotFilledBookingHandler.cs` | Drop interface, rename method |
| `EventHandlers/TeeTimeSlotFillFailedHandler.cs` | Drop interface, rename method |
| `EventHandlers/TeeTimeRequestFulfilledHandler.cs` | Drop interface, rename method |
| `EventHandlers/BookingCreatedRemoveFromWaitlistHandler.cs` | Drop interface, rename method |
| `EventHandlers/BookingCreatedConfirmationSmsHandler.cs` | Drop interface, rename method |
| `EventHandlers/WaitlistOfferRejectedSmsHandler.cs` | Drop interface, rename method |
| `EventHandlers/WaitlistOfferRejectedNextOfferHandler.cs` | Drop interface, rename method |
| `TestWebApplicationFactory.cs` | Disable Wolverine transport, run in solo mode |

### Files Unchanged

All domain model files, event records, endpoints, frontend, domain tests.

---

## Implementation Order

1. Add NuGet packages
2. Configure Wolverine in `Program.cs` (remove old registrations)
3. Update `ApplicationDbContext` to use `IMessageBus`
4. Migrate all 11 handlers (mechanical: drop interface, rename method, swap publisher)
5. Simplify `WaitlistOfferAcceptedFillHandler` — remove manual retry logic
6. Update `TestWebApplicationFactory` — disable transports, solo mode
7. Delete old event infrastructure files (`IDomainEventPublisher`, `InProcessDomainEventPublisher`, `IDomainEventHandler`)
8. Build and run all tests
9. Verify Wolverine schema creation with local SQL Server (`docker compose up db`)

---

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| SQLite test incompatibility with Wolverine transport | `DisableAllExternalWolverineTransports()` + `RunWolverineInSoloMode()` |
| Handlers now execute asynchronously (not in HTTP request) | Tests validate end-state, not execution timing. `Separated` mode ensures all handlers fire. |
| Wolverine auto-discovery picks up unintended classes | Namespace convention (`EventHandlers/`) and `Handle` method naming keeps handlers explicit |
| SQL Server schema creation on first run | `.AutoProvision()` handles it; `docker compose up db` provides SQL Server locally |
| Global retry policy affects unintended handlers | Only handlers that `SaveAsync` on concurrency-protected entities can throw `DbUpdateConcurrencyException`; retrying is correct in all cases |

---

## Rollback Strategy

This work happens on a feature branch. If Wolverine causes unexpected issues:
1. `git revert` the Wolverine commits
2. The custom `InProcessDomainEventPublisher` infrastructure is restored
3. No database migration to reverse (Wolverine manages its own schema separately)

---

## Future Opportunities (Not in Scope)

- **Transactional outbox** via `IDbContextOutbox` — write events to outbox within the same EF transaction as entity save, eliminating the crash-window between save and publish
- **Scheduled messages** for the 60-second re-offer buffer (`bus.ScheduleAsync(msg, 60.Seconds())`)
- **Offer expiration timeouts** via `TimeoutMessage` if the design adds time-limited offers later
- **Azure Service Bus transport** — swap `UseSqlServerPersistenceAndTransport` for `UseAzureServiceBus`
- **Return-based cascading** for simpler handlers
- **Dead letter queue monitoring** via Wolverine's built-in DLQ support

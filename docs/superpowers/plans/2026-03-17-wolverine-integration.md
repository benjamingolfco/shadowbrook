# Wolverine Integration Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the custom `InProcessDomainEventPublisher` with Wolverine for handler discovery, retry, separated execution, and a future migration path to Azure Service Bus.

**Architecture:** 1:1 behavioral replacement — same handler logic, different plumbing. Domain events are harvested from EF entities in `SaveChangesAsync` and published via Wolverine's `IMessageBus`. Wolverine auto-discovers handlers by convention (public class with a `Handle` method taking the event type). SQL Server is used as both transport and persistence.

**Tech Stack:** WolverineFx, WolverineFx.SqlServer, WolverineFx.EntityFrameworkCore, .NET 10, EF Core 10

**Spec:** `docs/superpowers/specs/2026-03-17-wolverine-integration-design.md`

---

## File Map

### Files to Create

None.

### Files to Delete

| File | Reason |
|------|--------|
| `src/backend/Shadowbrook.Api/Infrastructure/Events/IDomainEventPublisher.cs` | Replaced by `IMessageBus` |
| `src/backend/Shadowbrook.Api/Infrastructure/Events/InProcessDomainEventPublisher.cs` | Replaced by Wolverine |
| `src/backend/Shadowbrook.Api/Infrastructure/Events/IDomainEventHandler.cs` | Replaced by Wolverine conventions |

### Files to Modify

| File | Change |
|------|--------|
| `src/backend/Shadowbrook.Api/Shadowbrook.Api.csproj` | Add 3 Wolverine NuGet packages |
| `tests/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj` | Add `WolverineFx` package |
| `src/backend/Shadowbrook.Api/Program.cs` | Replace 13 DI lines with `UseWolverine()` |
| `src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs` | `IDomainEventPublisher` → `IMessageBus` |
| `tests/Shadowbrook.Api.Tests/TestWebApplicationFactory.cs` | Add Wolverine test config |
| `src/backend/Shadowbrook.Api/EventHandlers/GolferJoinedWaitlistSmsHandler.cs` | Drop interface, rename method |
| `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeRequestAddedNotifyHandler.cs` | Drop interface, rename method |
| `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferAcceptedFillHandler.cs` | Drop interface, rename method, swap publisher, remove manual retry |
| `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferAcceptedSmsHandler.cs` | Drop interface, rename method |
| `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeSlotFilledBookingHandler.cs` | Drop interface, rename method |
| `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeSlotFillFailedHandler.cs` | Drop interface, rename method |
| `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeRequestFulfilledHandler.cs` | Drop interface, rename method |
| `src/backend/Shadowbrook.Api/EventHandlers/BookingCreatedRemoveFromWaitlistHandler.cs` | Drop interface, rename method |
| `src/backend/Shadowbrook.Api/EventHandlers/BookingCreatedConfirmationSmsHandler.cs` | Drop interface, rename method |
| `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferRejectedSmsHandler.cs` | Drop interface, rename method |
| `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferRejectedNextOfferHandler.cs` | Drop interface, rename method |

---

## Task 1: Add NuGet Packages

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Shadowbrook.Api.csproj`
- Modify: `tests/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj`

- [ ] **Step 1: Add Wolverine packages to API project**

```bash
dotnet add src/backend/Shadowbrook.Api package WolverineFx
dotnet add src/backend/Shadowbrook.Api package WolverineFx.SqlServer
dotnet add src/backend/Shadowbrook.Api package WolverineFx.EntityFrameworkCore
```

- [ ] **Step 2: Add Wolverine package to test project**

```bash
dotnet add tests/Shadowbrook.Api.Tests package WolverineFx
```

- [ ] **Step 3: Verify restore succeeds**

```bash
dotnet restore shadowbrook.slnx
```

Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add src/backend/Shadowbrook.Api/Shadowbrook.Api.csproj tests/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj
git commit -m "chore: add WolverineFx NuGet packages"
```

---

## Task 2: Configure Wolverine in Program.cs

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Program.cs`

- [ ] **Step 1: Add Wolverine using directives**

Add to the top of `Program.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.SqlServer;
```

- [ ] **Step 2: Add Wolverine host configuration**

Add after the `AddDbContext` call (around line 51) and before the repository registrations:

```csharp
builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

    opts.UseSqlServerPersistenceAndTransport(connectionString!, "wolverine")
        .AutoProvision();

    opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;

    opts.Handlers.OnException<DbUpdateConcurrencyException>()
        .RetryTimes(3);
});
```

- [ ] **Step 3: Remove old event infrastructure registrations**

Remove these lines from `Program.cs` (lines 55, 66-88):

```csharp
// Remove this:
builder.Services.AddScoped<IDomainEventPublisher, InProcessDomainEventPublisher>();

// Remove all of these (lines 66-88):
builder.Services.AddScoped<IDomainEventHandler<Shadowbrook.Domain.WalkUpWaitlistAggregate.Events.GolferJoinedWaitlist>, Shadowbrook.Api.EventHandlers.GolferJoinedWaitlistSmsHandler>();
builder.Services.AddScoped<IDomainEventHandler<TeeTimeRequestAdded>, Shadowbrook.Api.EventHandlers.TeeTimeRequestAddedNotifyHandler>();
builder.Services.AddScoped<IDomainEventHandler<WaitlistOfferAccepted>, Shadowbrook.Api.EventHandlers.WaitlistOfferAcceptedFillHandler>();
builder.Services.AddScoped<IDomainEventHandler<WaitlistOfferAccepted>, Shadowbrook.Api.EventHandlers.WaitlistOfferAcceptedSmsHandler>();
builder.Services.AddScoped<IDomainEventHandler<TeeTimeSlotFilled>, Shadowbrook.Api.EventHandlers.TeeTimeSlotFilledBookingHandler>();
builder.Services.AddScoped<IDomainEventHandler<TeeTimeSlotFillFailed>, Shadowbrook.Api.EventHandlers.TeeTimeSlotFillFailedHandler>();
builder.Services.AddScoped<IDomainEventHandler<TeeTimeRequestFulfilled>, Shadowbrook.Api.EventHandlers.TeeTimeRequestFulfilledHandler>();
builder.Services.AddScoped<IDomainEventHandler<BookingCreated>, Shadowbrook.Api.EventHandlers.BookingCreatedRemoveFromWaitlistHandler>();
builder.Services.AddScoped<IDomainEventHandler<BookingCreated>, Shadowbrook.Api.EventHandlers.BookingCreatedConfirmationSmsHandler>();
builder.Services.AddScoped<IDomainEventHandler<WaitlistOfferRejected>, Shadowbrook.Api.EventHandlers.WaitlistOfferRejectedSmsHandler>();
builder.Services.AddScoped<IDomainEventHandler<WaitlistOfferRejected>, Shadowbrook.Api.EventHandlers.WaitlistOfferRejectedNextOfferHandler>();
```

- [ ] **Step 4: Remove unused using directives**

Remove any `using` statements at the top of `Program.cs` that referenced `IDomainEventHandler`, `IDomainEventPublisher`, or event types that are no longer directly referenced (they'll be discovered by Wolverine). Keep `using` statements that are still needed for repository registrations, validators, etc.

- [ ] **Step 5: Verify the file compiles (will have errors — other files still reference old interfaces)**

```bash
dotnet build shadowbrook.slnx 2>&1 | head -30
```

Expected: compilation errors in handler files and `ApplicationDbContext` referencing `IDomainEventPublisher` and `IDomainEventHandler`. This is expected — we fix these in the next tasks.

- [ ] **Step 6: Commit (build is intentionally broken at this point — fixed in Tasks 3-6)**

```bash
git add src/backend/Shadowbrook.Api/Program.cs
git commit -m "refactor: configure Wolverine, remove manual handler registrations"
```

---

## Task 3: Update ApplicationDbContext

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Replace IDomainEventPublisher with IMessageBus**

Change the primary constructor parameter from `IDomainEventPublisher? eventPublisher = null` to `IMessageBus? bus = null`. Update the `using` directives: remove `Shadowbrook.Api.Infrastructure.Events`, add `Wolverine`.

Replace the `SaveChangesAsync` method — change `eventPublisher` references to `bus` and `eventPublisher.PublishAsync(domainEvent, cancellationToken)` to `bus.PublishAsync(domainEvent)`:

```csharp
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;
using Shadowbrook.Api.Models;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;
using Wolverine;

namespace Shadowbrook.Api.Infrastructure.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUser? currentUser = null,
    IMessageBus? bus = null) : DbContext(options)
{
    // DbSet properties unchanged...

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

    // OnModelCreating unchanged...
}
```

- [ ] **Step 2: Commit**

```bash
git add src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs
git commit -m "refactor: replace IDomainEventPublisher with IMessageBus in ApplicationDbContext"
```

---

## Task 4: Migrate Simple Handlers (9 handlers)

These 9 handlers need the same mechanical change: drop `IDomainEventHandler<T>` interface, rename `HandleAsync` → `Handle`, remove `= default` from CancellationToken. No logic changes.

**Files:**
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/GolferJoinedWaitlistSmsHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeRequestAddedNotifyHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferAcceptedSmsHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeSlotFilledBookingHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeSlotFillFailedHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeRequestFulfilledHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/BookingCreatedRemoveFromWaitlistHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/BookingCreatedConfirmationSmsHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferRejectedSmsHandler.cs`

For each handler, apply these changes:

1. Remove the `using Shadowbrook.Api.Infrastructure.Events;` import
2. Remove `: IDomainEventHandler<EventType>` from the class declaration
3. Rename `public async Task HandleAsync(` to `public async Task Handle(`
4. Change `CancellationToken ct = default` to `CancellationToken ct`
5. **Keep or remove `using Shadowbrook.Domain.Common;`** — see per-handler notes below. `ITextMessageService` lives in this namespace, so handlers that use SMS must keep it.

- [ ] **Step 1: Migrate GolferJoinedWaitlistSmsHandler**

Remove `using Shadowbow.Api.Infrastructure.Events;`. **Keep** `using Shadowbrook.Domain.Common;` (uses `ITextMessageService`). Drop interface, rename method.

- [ ] **Step 2: Migrate TeeTimeRequestAddedNotifyHandler**

Remove `using Shadowbrook.Api.Infrastructure.Events;`. **Keep** `using Shadowbrook.Domain.Common;` (uses `ITextMessageService`). Drop interface, rename method.

- [ ] **Step 3: Migrate WaitlistOfferAcceptedSmsHandler**

Remove `using Shadowbrook.Api.Infrastructure.Events;`. **Keep** `using Shadowbrook.Domain.Common;` (uses `ITextMessageService`). Drop interface, rename method.

- [ ] **Step 4: Migrate TeeTimeSlotFilledBookingHandler**

Remove `using Shadowbrook.Api.Infrastructure.Events;`. **Remove** `using Shadowbrook.Domain.Common;` (no longer needed — was only for `IDomainEventHandler`). Drop interface, rename method.

- [ ] **Step 5: Migrate TeeTimeSlotFillFailedHandler**

Remove `using Shadowbrook.Api.Infrastructure.Events;`. **Remove** `using Shadowbrook.Domain.Common;` (no longer needed). Drop interface, rename method.

- [ ] **Step 6: Migrate TeeTimeRequestFulfilledHandler**

Remove `using Shadowbrook.Api.Infrastructure.Events;`. **Remove** `using Shadowbrook.Domain.Common;` (no longer needed). Drop interface, rename method.

- [ ] **Step 7: Migrate BookingCreatedRemoveFromWaitlistHandler**

Remove `using Shadowbrook.Api.Infrastructure.Events;`. **Remove** `using Shadowbrook.Domain.Common;` (no longer needed). Drop interface, rename method.

- [ ] **Step 8: Migrate BookingCreatedConfirmationSmsHandler**

Remove `using Shadowbrook.Api.Infrastructure.Events;`. **Keep** `using Shadowbrook.Domain.Common;` (uses `ITextMessageService`). Drop interface, rename method.

- [ ] **Step 9: Migrate WaitlistOfferRejectedSmsHandler**

Remove `using Shadowbrook.Api.Infrastructure.Events;`. **Keep** `using Shadowbrook.Domain.Common;` (uses `ITextMessageService`). Drop interface, rename method.

- [ ] **Step 10: Commit**

```bash
git add src/backend/Shadowbrook.Api/EventHandlers/GolferJoinedWaitlistSmsHandler.cs \
  src/backend/Shadowbrook.Api/EventHandlers/TeeTimeRequestAddedNotifyHandler.cs \
  src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferAcceptedSmsHandler.cs \
  src/backend/Shadowbrook.Api/EventHandlers/TeeTimeSlotFilledBookingHandler.cs \
  src/backend/Shadowbrook.Api/EventHandlers/TeeTimeSlotFillFailedHandler.cs \
  src/backend/Shadowbrook.Api/EventHandlers/TeeTimeRequestFulfilledHandler.cs \
  src/backend/Shadowbrook.Api/EventHandlers/BookingCreatedRemoveFromWaitlistHandler.cs \
  src/backend/Shadowbrook.Api/EventHandlers/BookingCreatedConfirmationSmsHandler.cs \
  src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferRejectedSmsHandler.cs
git commit -m "refactor: migrate 9 simple handlers to Wolverine conventions"
```

---

## Task 5: Migrate Complex Handlers (2 handlers)

These handlers need additional changes beyond the mechanical migration.

**Files:**
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferAcceptedFillHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferRejectedNextOfferHandler.cs`

- [ ] **Step 1: Migrate WaitlistOfferAcceptedFillHandler**

This handler has three extra changes beyond the interface/method rename:
1. Replace `IDomainEventPublisher eventPublisher` with `IMessageBus bus` in the constructor
2. Replace `eventPublisher.PublishAsync(...)` calls with `bus.PublishAsync(...)`
3. Remove the manual concurrency retry logic (`TryFillAsync`, `db.ChangeTracker.Clear()`, the `ApplicationDbContext db` dependency)

The handler simplifies from 76 lines to ~30. Replace the entire file:

```csharp
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Wolverine;

namespace Shadowbrook.Api.EventHandlers;

public class WaitlistOfferAcceptedFillHandler(
    ITeeTimeRequestRepository requestRepository,
    IGolferWaitlistEntryRepository entryRepository,
    IMessageBus bus)
{
    public async Task Handle(WaitlistOfferAccepted domainEvent, CancellationToken ct)
    {
        var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId);
        if (entry is null)
        {
            return;
        }

        var request = await requestRepository.GetByIdAsync(domainEvent.TeeTimeRequestId);
        if (request is null)
        {
            await bus.PublishAsync(new TeeTimeSlotFillFailed
            {
                TeeTimeRequestId = domainEvent.TeeTimeRequestId,
                OfferId = domainEvent.WaitlistOfferId,
                Reason = "Tee time request not found."
            });
            return;
        }

        var result = request.Fill(domainEvent.GolferId, entry.GroupSize, domainEvent.BookingId);

        if (!result.Success)
        {
            await bus.PublishAsync(new TeeTimeSlotFillFailed
            {
                TeeTimeRequestId = domainEvent.TeeTimeRequestId,
                OfferId = domainEvent.WaitlistOfferId,
                Reason = result.RejectionReason ?? "Unable to fill slot."
            });
            return;
        }

        await requestRepository.SaveAsync();

        await bus.PublishAsync(new TeeTimeSlotFilled
        {
            TeeTimeRequestId = domainEvent.TeeTimeRequestId,
            BookingId = domainEvent.BookingId,
            GolferId = domainEvent.GolferId
        });
    }
}
```

Wolverine's global `OnException<DbUpdateConcurrencyException>().RetryTimes(3)` policy handles the concurrency case — if `requestRepository.SaveAsync()` throws, Wolverine retries the entire handler with a fresh DI scope.

- [ ] **Step 2: Migrate WaitlistOfferRejectedNextOfferHandler**

This handler needs the interface/method rename plus: remove `using Shadowbrook.Api.Infrastructure.Events;`. **Keep** `using Shadowbrook.Domain.Common;` (uses `ITextMessageService`).

```csharp
// Remove: using Shadowbrook.Api.Infrastructure.Events;
// Remove: using Shadowbrook.Domain.Common;
// Remove ": IDomainEventHandler<WaitlistOfferRejected>"
// Rename HandleAsync → Handle, remove "= default"

public class WaitlistOfferRejectedNextOfferHandler(
    IWaitlistOfferRepository offerRepository,
    ITeeTimeRequestRepository requestRepository,
    ApplicationDbContext db,
    ITextMessageService textMessageService,
    IConfiguration configuration)
{
    public async Task Handle(WaitlistOfferRejected domainEvent, CancellationToken ct)
    {
        // ... body unchanged ...
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferAcceptedFillHandler.cs \
  src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferRejectedNextOfferHandler.cs
git commit -m "refactor: migrate FillHandler and NextOfferHandler to Wolverine, simplify retry"
```

---

## Task 6: Delete Old Event Infrastructure

**Files:**
- Delete: `src/backend/Shadowbrook.Api/Infrastructure/Events/IDomainEventPublisher.cs`
- Delete: `src/backend/Shadowbrook.Api/Infrastructure/Events/InProcessDomainEventPublisher.cs`
- Delete: `src/backend/Shadowbrook.Api/Infrastructure/Events/IDomainEventHandler.cs`

- [ ] **Step 1: Delete the three files**

```bash
rm src/backend/Shadowbrook.Api/Infrastructure/Events/IDomainEventPublisher.cs
rm src/backend/Shadowbrook.Api/Infrastructure/Events/InProcessDomainEventPublisher.cs
rm src/backend/Shadowbrook.Api/Infrastructure/Events/IDomainEventHandler.cs
```

- [ ] **Step 2: Check if the Infrastructure/Events directory is now empty**

```bash
ls src/backend/Shadowbrook.Api/Infrastructure/Events/
```

If empty, delete the directory:

```bash
rmdir src/backend/Shadowbrook.Api/Infrastructure/Events/
```

- [ ] **Step 3: Verify build compiles**

```bash
dotnet build shadowbrook.slnx
```

Expected: no errors. All references to `IDomainEventPublisher`, `IDomainEventHandler`, and `InProcessDomainEventPublisher` have been removed in previous tasks.

- [ ] **Step 4: Commit**

```bash
git add -A src/backend/Shadowbrook.Api/Infrastructure/Events/
git commit -m "chore: delete old InProcessDomainEventPublisher infrastructure"
```

---

## Task 7: Update Test Infrastructure

**Files:**
- Modify: `tests/Shadowbrook.Api.Tests/TestWebApplicationFactory.cs`

- [ ] **Step 1: Add Wolverine test configuration**

Add `using Wolverine;` to the imports. Add two lines inside `ConfigureServices` in `TestWebApplicationFactory`, after the SQLite DbContext setup:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shadowbrook.Api.Infrastructure.Data;
using Wolverine;

namespace Shadowbrook.Api.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all EF Core registrations for ApplicationDbContext
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Share a single connection so the in-memory database persists across scopes
            var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            connection.Open();

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));

            // Disable Wolverine's SQL Server transport for SQLite-based tests
            services.DisableAllExternalWolverineTransports();
            services.RunWolverineInSoloMode();
        });

        builder.UseEnvironment("Testing");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        return host;
    }
}
```

- [ ] **Step 2: Run all tests**

```bash
dotnet test shadowbrook.slnx
```

Expected: all tests pass. If any fail, investigate — the most likely cause is Wolverine handler discovery issues or transport configuration leaking into tests.

- [ ] **Step 3: Commit**

```bash
git add tests/Shadowbrook.Api.Tests/TestWebApplicationFactory.cs
git commit -m "test: configure Wolverine for SQLite-based integration tests"
```

---

## Task 8: Verify Full Build and Tests

- [ ] **Step 1: Clean build**

```bash
dotnet clean shadowbrook.slnx && dotnet build shadowbrook.slnx
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 2: Run all tests**

```bash
dotnet test shadowbrook.slnx --verbosity normal
```

Expected: all tests pass.

- [ ] **Step 3: Run the API locally to verify Wolverine schema creation**

Make sure the local SQL Server is running:

```bash
docker compose up db -d
```

Then start the API:

```bash
dotnet run --project src/backend/Shadowbrook.Api
```

Expected: API starts without errors. Check the SQL Server database — a `wolverine` schema should exist with tables like `wolverine.incoming_envelopes`, `wolverine.outgoing_envelopes`, `wolverine.dead_letters`.

- [ ] **Step 4: Stop the API (Ctrl+C) and commit if any fixups were needed**

If any changes were required to get the build/tests/runtime working, commit them:

```bash
git add -A
git commit -m "fix: resolve Wolverine integration issues found during verification"
```

---

## Task 9: Final Cleanup

- [ ] **Step 1: Verify no remaining references to old infrastructure**

Search for any lingering references:

```bash
grep -r "IDomainEventPublisher\|IDomainEventHandler\|InProcessDomainEventPublisher" src/ tests/ --include="*.cs"
```

Expected: no matches.

- [ ] **Step 2: Verify no unused using directives in modified files**

```bash
dotnet build shadowbrook.slnx -warnaserror
```

If there are warnings about unused usings, fix them.

- [ ] **Step 3: Commit any cleanup**

```bash
git add -A
git commit -m "chore: remove remaining references to old event infrastructure"
```

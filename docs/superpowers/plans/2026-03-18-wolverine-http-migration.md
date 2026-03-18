# Wolverine HTTP & Domain Event Scraping Migration

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate HTTP endpoints to Wolverine HTTP, enable automatic domain event scraping via EF Core transactional middleware, and remove manual event publishing infrastructure.

**Architecture:** Replace minimal API endpoints with Wolverine HTTP endpoints (attribute-routed static methods). Enable `UseEntityFrameworkCoreTransactions()` and `PublishDomainEventsFromEntityFrameworkCore<Entity, IDomainEvent>()` so that both HTTP endpoints and message handlers get automatic domain event scraping through the correct `MessageContext`. Remove `IMessageBus` injection from `ApplicationDbContext` and all manual `bus.PublishAsync()` / `repo.SaveAsync()` calls from message handlers (transactional middleware owns the save). Remove `SaveAsync()` from all repository interfaces and implementations.

**Tech Stack:** WolverineFx.Http 5.20.1, WolverineFx.FluentValidation 5.20.1, WolverineFx.EntityFrameworkCore 5.20.1

**Key docs:**
- Wolverine HTTP endpoints: https://wolverinefx.net/guide/http/endpoints.html
- EF Core domain events: https://wolverinefx.net/guide/durability/efcore/domain-events.html
- FluentValidation: https://wolverinefx.net/guide/handlers/fluent-validation.html
- Transactional middleware: https://wolverinefx.net/guide/durability/efcore.html

**Important context for implementer:**
- Wolverine HTTP endpoints must be `public static` methods on `public` classes
- `[WolverineGet]`, `[WolverinePost]`, `[WolverinePut]`, `[WolverineDelete]` replace route registration
- Parameter binding: route params from URL, first complex type = JSON body, other types = DI services
- Return `IResult` for full control over status codes (same as minimal APIs)
- `app.MapWolverineEndpoints()` discovers all decorated methods automatically
- Wolverine HTTP coexists with regular minimal API endpoints — migration can be incremental
- The transactional middleware calls `SaveChangesAsync()` automatically at end of handler/endpoint — DO NOT call it manually
- Domain event scraping happens during the transactional middleware's save — events on tracked entities are collected and published through the correct `MessageContext`
- `[Authorize]` and `[AllowAnonymous]` work on Wolverine HTTP endpoints
- FluentValidation middleware auto-discovers validators and returns RFC 7807 Problem Details on failure — this is a DIFFERENT format than the custom `ValidationFilter` which returned `{ "error": "message" }`. Endpoints with FluentValidation validators (`AddGolferToWaitlistRequest`, `CreateWalkUpWaitlistRequestRequest`, `VerifyCodeRequest`, `JoinWaitlistRequest`) will have their validation handled by Wolverine middleware with Problem Details format. Endpoints without validators (Tenant, Course, TeeSheet) keep inline validation with `{ "error": "..." }` format. Test assertions on validation responses will need updating.
- `WolverineFx.EntityFrameworkCore` is already in the project — only `WolverineFx.Http` and `WolverineFx.FluentValidation` need to be added
- `Golfer.Create()` does NOT raise domain events — the mid-flow SaveChangesAsync in the golfer upsert pattern is safe

---

### Task 1: Add NuGet packages

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Shadowbrook.Api.csproj`
- Modify: `tests/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj`

- [ ] **Step 1: Add WolverineFx.Http and WolverineFx.FluentValidation packages**

```bash
dotnet add src/backend/Shadowbrook.Api package WolverineFx.Http --version 5.20.1
dotnet add src/backend/Shadowbrook.Api package WolverineFx.FluentValidation --version 5.20.1
dotnet add tests/Shadowbrook.Api.Tests package WolverineFx.Http --version 5.20.1
```

- [ ] **Step 2: Verify build**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/backend/Shadowbrook.Api/Shadowbrook.Api.csproj tests/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj
git commit -m "chore: add WolverineFx.Http and WolverineFx.FluentValidation packages"
```

---

### Task 2: Configure Wolverine EF Core transactional middleware and domain event scraping

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Program.cs`

This task wires up the core infrastructure. After this, both Wolverine message handlers and (once migrated) HTTP endpoints will automatically have their `SaveChangesAsync` called by middleware, and domain events will be scraped from tracked entities and published through the correct `MessageContext`.

- [ ] **Step 1: Run existing tests to establish baseline**

Run: `dotnet test tests/Shadowbrook.Api.Tests --no-build -v q`
Expected: All tests pass

- [ ] **Step 2: Add Wolverine HTTP services, EF Core transactions, and domain event scraping to Program.cs**

In `Program.cs`, inside the `builder.Host.UseWolverine(opts => { ... })` block, add:

```csharp
opts.UseEntityFrameworkCoreTransactions();
opts.UseFluentValidation();
opts.PublishDomainEventsFromEntityFrameworkCore<Entity, IDomainEvent>(e => e.DomainEvents);
```

Add required usings at the top of Program.cs:

```csharp
using Wolverine.EntityFrameworkCore;
using Wolverine.FluentValidation;
```

After `builder.Services.AddValidatorsFromAssemblyContaining<Program>();`, add:

```csharp
builder.Services.AddWolverineHttp();
```

Before `app.Run();`, add:

```csharp
app.MapWolverineEndpoints();
```

- [ ] **Step 3: Verify build**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded

- [ ] **Step 4: Run tests to verify no regressions**

Run: `dotnet test tests/Shadowbrook.Api.Tests -v q`
Expected: All tests pass (existing endpoints still work as minimal APIs)

- [ ] **Step 5: Commit**

```bash
git add src/backend/Shadowbrook.Api/Program.cs
git commit -m "feat: configure Wolverine EF Core transactions, domain event scraping, and HTTP"
```

---

### Task 3: Update Entity base class for domain event scraping compatibility

**Files:**
- Modify: `src/backend/Shadowbrook.Domain/Common/Entity.cs`

Wolverine's domain event scraper reads events via the lambda `e => e.DomainEvents` and calls `Clear()` on the collection after scraping. The current `IReadOnlyCollection<IDomainEvent>` does not expose `Clear()`. Change `DomainEvents` to return the mutable list.

- [ ] **Step 1: Update Entity to expose mutable DomainEvents**

```csharp
namespace Shadowbrook.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; init; }

    private readonly List<IDomainEvent> domainEvents = [];
    public List<IDomainEvent> DomainEvents => this.domainEvents;
    public void ClearDomainEvents() => this.domainEvents.Clear();
    protected void AddDomainEvent(IDomainEvent domainEvent) => this.domainEvents.Add(domainEvent);
}
```

Note: `ClearDomainEvents()` is kept — domain unit tests call it to isolate event assertions. `IReadOnlyCollection` accessor changed to `List<IDomainEvent>` — the scraper needs a mutable collection to call `Clear()` after scraping.

- [ ] **Step 2: Verify build**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/backend/Shadowbrook.Domain/Common/Entity.cs
git commit -m "refactor: expose mutable DomainEvents list for Wolverine event scraping"
```

---

### Task 4: Clean up ApplicationDbContext — remove IMessageBus and SaveChangesAsync override

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs`

The transactional middleware now owns saving and event publishing. The DbContext no longer needs to do either.

- [ ] **Step 1: Remove IMessageBus from constructor and remove SaveChangesAsync override**

The constructor should become:

```csharp
public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUser? currentUser = null) : DbContext(options)
```

Remove the entire `SaveChangesAsync` override (lines 31-53). Remove the `using Wolverine;` import.

- [ ] **Step 2: Verify build**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Shadowbrook.Api.Tests -v q`
Expected: All tests pass

- [ ] **Step 4: Commit**

```bash
git add src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs
git commit -m "refactor: remove IMessageBus and SaveChangesAsync override from DbContext"
```

---

### Task 5: Remove SaveAsync from repository interfaces and implementations

**Files:**
- Modify: `src/backend/Shadowbrook.Domain/BookingAggregate/IBookingRepository.cs`
- Modify: `src/backend/Shadowbrook.Domain/GolferAggregate/IGolferRepository.cs`
- Modify: `src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/IGolferWaitlistEntryRepository.cs`
- Modify: `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/IWaitlistOfferRepository.cs`
- Modify: `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/ITeeTimeRequestRepository.cs`
- Modify: `src/backend/Shadowbrook.Domain/WalkUpWaitlistAggregate/IWalkUpWaitlistRepository.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Repositories/BookingRepository.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Repositories/GolferRepository.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Repositories/GolferWaitlistEntryRepository.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Repositories/WaitlistOfferRepository.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Repositories/TeeTimeRequestRepository.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Repositories/WalkUpWaitlistRepository.cs`

The transactional middleware calls `SaveChangesAsync()` automatically. Repositories become pure query/add — they no longer own persistence.

- [ ] **Step 1: Remove `Task SaveAsync()` from all 6 repository interfaces**

Remove the `Task SaveAsync();` line from each interface:
- `IBookingRepository.cs`
- `IGolferRepository.cs`
- `IGolferWaitlistEntryRepository.cs`
- `IWaitlistOfferRepository.cs`
- `ITeeTimeRequestRepository.cs`
- `IWalkUpWaitlistRepository.cs`

- [ ] **Step 2: Remove `SaveAsync` implementations from all 6 repository classes**

Remove the `public async Task SaveAsync() => await db.SaveChangesAsync();` line from each implementation:
- `BookingRepository.cs`
- `GolferRepository.cs`
- `GolferWaitlistEntryRepository.cs`
- `WaitlistOfferRepository.cs`
- `TeeTimeRequestRepository.cs`
- `WalkUpWaitlistRepository.cs`

The transactional middleware calls `SaveChangesAsync()` at the end of every handler. Domain events raised by entities (via `AddDomainEvent`) are scraped and published automatically. Handlers no longer need to call `SaveAsync()` or `bus.PublishAsync()`.

Now update the message handlers:

**Files:**
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferAcceptedFillHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeSlotFilledBookingHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeSlotFillFailedHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeRequestFulfilledHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/TeeTimeRequestAddedNotifyHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/BookingCreatedRemoveFromWaitlistHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferRejectedNextOfferHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/BookingCreatedConfirmationSmsHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/GolferJoinedWaitlistSmsHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferAcceptedSmsHandler.cs`
- Modify: `src/backend/Shadowbrook.Api/EventHandlers/WaitlistOfferRejectedSmsHandler.cs`

The transactional middleware calls `SaveChangesAsync()` at the end of every handler. Domain events raised by entities (via `AddDomainEvent`) are scraped and published automatically. Handlers no longer need to call `SaveAsync()` or `bus.PublishAsync()`.

- [ ] **Step 3: Update WaitlistOfferAcceptedFillHandler — remove SaveAsync and bus.PublishAsync**

This handler currently calls `requestRepository.SaveAsync()` and `bus.PublishAsync()` for `TeeTimeSlotFilled` / `TeeTimeSlotFillFailed`. The `request.Fill()` domain method already raises `TeeTimeRequestFulfilled` via `AddDomainEvent`. However, the `TeeTimeSlotFilled` and `TeeTimeSlotFillFailed` events are currently published manually by this handler — they are NOT raised by domain entities.

**Decision point:** `TeeTimeSlotFilled` and `TeeTimeSlotFillFailed` are handler-level orchestration events, not domain events. For these, use Wolverine's **cascading messages** pattern — return them from the handler instead of calling `bus.PublishAsync()`.

The handler should:
1. Remove `IMessageBus bus` parameter
2. Remove all `await bus.PublishAsync(...)` calls
3. Remove `await requestRepository.SaveAsync()` call
4. Return cascading messages instead

```csharp
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.EventHandlers;

public static class WaitlistOfferAcceptedFillHandler
{
    public static async Task<object?> Handle(
        WaitlistOfferAccepted domainEvent,
        ITeeTimeRequestRepository requestRepository,
        IGolferWaitlistEntryRepository entryRepository)
    {
        var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId);
        if (entry is null)
        {
            return null;
        }

        var request = await requestRepository.GetByIdAsync(domainEvent.TeeTimeRequestId);
        if (request is null)
        {
            return new TeeTimeSlotFillFailed
            {
                TeeTimeRequestId = domainEvent.TeeTimeRequestId,
                OfferId = domainEvent.WaitlistOfferId,
                Reason = "Tee time request not found."
            };
        }

        var result = request.Fill(domainEvent.GolferId, entry.GroupSize, domainEvent.BookingId);

        if (!result.Success)
        {
            return new TeeTimeSlotFillFailed
            {
                TeeTimeRequestId = domainEvent.TeeTimeRequestId,
                OfferId = domainEvent.WaitlistOfferId,
                Reason = result.RejectionReason ?? "Unable to fill slot."
            };
        }

        return new TeeTimeSlotFilled
        {
            TeeTimeRequestId = domainEvent.TeeTimeRequestId,
            BookingId = domainEvent.BookingId,
            GolferId = domainEvent.GolferId
        };
    }
}
```

Note: Returning `object?` lets Wolverine treat non-null return values as cascading messages. Returning `null` means no cascading message.

- [ ] **Step 4: Update remaining handlers that call SaveAsync — remove the call**

For each handler below, remove the `await <repo>.SaveAsync();` line. The transactional middleware handles it.

**TeeTimeSlotFilledBookingHandler.cs** — remove `await bookingRepository.SaveAsync();`

**TeeTimeSlotFillFailedHandler.cs** — remove `await offerRepository.SaveAsync();`

**TeeTimeRequestFulfilledHandler.cs** — remove `await offerRepository.SaveAsync();`

**TeeTimeRequestAddedNotifyHandler.cs** — remove `await repository.SaveAsync();`

**BookingCreatedRemoveFromWaitlistHandler.cs** — remove `await entryRepository.SaveAsync();`

**WaitlistOfferRejectedNextOfferHandler.cs** — remove `await offerRepository.SaveAsync();`

- [ ] **Step 5: Verify read-only handlers need no changes**

These handlers only read data and send SMS — no `SaveAsync` or `PublishAsync` calls:
- `BookingCreatedConfirmationSmsHandler.cs`
- `GolferJoinedWaitlistSmsHandler.cs`
- `WaitlistOfferAcceptedSmsHandler.cs`
- `WaitlistOfferRejectedSmsHandler.cs`

Confirm they have no `SaveAsync` or `PublishAsync` calls. No changes needed.

- [ ] **Step 6: Verify build**

Run: `dotnet build shadowbrook.slnx`
Expected: Build FAILS — endpoint files still reference `SaveAsync()`. This is expected and will be fixed in Task 6.

- [ ] **Step 7: Commit**

```bash
git add src/backend/Shadowbrook.Domain/ src/backend/Shadowbrook.Api/Infrastructure/Repositories/ src/backend/Shadowbrook.Api/EventHandlers/
git commit -m "refactor: remove SaveAsync from repos and manual PublishAsync from handlers"
```

---

### Task 6: Migrate HTTP endpoints to Wolverine HTTP

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Endpoints/TenantEndpoints.cs`
- Modify: `src/backend/Shadowbrook.Api/Endpoints/CourseEndpoints.cs`
- Modify: `src/backend/Shadowbrook.Api/Endpoints/TeeSheetEndpoints.cs`
- Modify: `src/backend/Shadowbrook.Api/Endpoints/WalkUpWaitlistEndpoints.cs`
- Modify: `src/backend/Shadowbrook.Api/Endpoints/WalkUpJoinEndpoints.cs`
- Modify: `src/backend/Shadowbrook.Api/Endpoints/WaitlistOfferEndpoints.cs`
- Modify: `src/backend/Shadowbrook.Api/Endpoints/DevSmsEndpoints.cs`
- Modify: `src/backend/Shadowbrook.Api/Program.cs`

Migration pattern for each endpoint:
1. `private static` → `public static`
2. Add `[WolverineGet("/route")]` / `[WolverinePost("/route")]` / etc. attribute
3. Remove `await db.SaveChangesAsync()` and `await repo.SaveAsync()` calls (transactional middleware handles it)
4. Remove `Map*Endpoints` extension method and manual route registration from Program.cs

**Important exception:** The `AddGolferToWaitlist` and `JoinWaitlist` endpoints have an intentional mid-flow `SaveAsync` for the golfer upsert race condition pattern. This needs special handling — see step for that endpoint.

- [ ] **Step 1: Migrate TenantEndpoints**

Replace the file contents. Key changes:
- Remove `MapTenantEndpoints` extension method
- Add Wolverine HTTP attributes to each method
- Make methods `public static`
- Remove `await db.SaveChangesAsync()` from `CreateTenant`

```csharp
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Models;
using System.Text.RegularExpressions;
using Wolverine.Http;

namespace Shadowbrook.Api.Endpoints;

public static partial class TenantEndpoints
{
    [WolverinePost("/tenants")]
    public static async Task<IResult> CreateTenant(
        CreateTenantRequest request,
        ApplicationDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationName))
        {
            return Results.BadRequest(new { error = "OrganizationName is required." });
        }

        if (string.IsNullOrWhiteSpace(request.ContactName))
        {
            return Results.BadRequest(new { error = "ContactName is required." });
        }

        if (string.IsNullOrWhiteSpace(request.ContactEmail))
        {
            return Results.BadRequest(new { error = "ContactEmail is required." });
        }

        if (!IsValidEmail(request.ContactEmail))
        {
            return Results.BadRequest(new { error = "ContactEmail must be a valid email address." });
        }

        if (string.IsNullOrWhiteSpace(request.ContactPhone))
        {
            return Results.BadRequest(new { error = "ContactPhone is required." });
        }

        var normalizedName = request.OrganizationName.ToLower();
        var existingTenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.OrganizationName.ToLower() == normalizedName);

        if (existingTenant is not null)
        {
            return Results.Conflict(new { error = "A tenant with this organization name already exists." });
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            OrganizationName = request.OrganizationName,
            ContactName = request.ContactName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Tenants.Add(tenant);

        var response = new TenantResponse(
            tenant.Id,
            tenant.OrganizationName,
            tenant.ContactName,
            tenant.ContactEmail,
            tenant.ContactPhone,
            tenant.CreatedAt,
            tenant.UpdatedAt);

        return Results.Created($"/tenants/{tenant.Id}", response);
    }

    [WolverineGet("/tenants")]
    public static async Task<IResult> GetAllTenants(ApplicationDbContext db)
    {
        var tenants = await db.Tenants
            .Select(t => new TenantListResponse(
                t.Id,
                t.OrganizationName,
                t.ContactName,
                t.ContactEmail,
                t.ContactPhone,
                t.Courses.Count,
                t.CreatedAt,
                t.UpdatedAt))
            .ToListAsync();

        return Results.Ok(tenants);
    }

    [WolverineGet("/tenants/{id}")]
    public static async Task<IResult> GetTenantById(Guid id, ApplicationDbContext db)
    {
        var tenant = await db.Tenants
            .Include(t => t.Courses)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant is null)
        {
            return Results.NotFound();
        }

        var response = new TenantDetailResponse(
            tenant.Id,
            tenant.OrganizationName,
            tenant.ContactName,
            tenant.ContactEmail,
            tenant.ContactPhone,
            tenant.Courses.Select(c => new CourseInfo(c.Id, c.Name, c.City, c.State)).ToList(),
            tenant.CreatedAt,
            tenant.UpdatedAt);

        return Results.Ok(response);
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    private static bool IsValidEmail(string email) => EmailRegex().IsMatch(email);
}
```

Keep the record types at the bottom of the file unchanged.

- [ ] **Step 2: Migrate CourseEndpoints**

Key changes:
- Remove `MapCourseEndpoints` extension method
- Add Wolverine HTTP attributes
- Make methods `public static`
- Remove `await db.SaveChangesAsync()` from `CreateCourse`, `UpdateTeeTimeSettings`, `UpdatePricing`
- The `CourseExistsFilter` was applied to a sub-group for `{id}` routes. For Wolverine HTTP, create a Wolverine middleware instead (see note below).

**CourseExistsFilter migration:** The existing `CourseExistsFilter` is an ASP.NET endpoint filter. For Wolverine HTTP endpoints, convert the course-exists check into inline validation within each endpoint that needs it, OR create a Wolverine `Before` middleware. Since only a few endpoints use it and the check is simple, inline it for now to keep things simple.

```csharp
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Models;
using Wolverine.Http;

namespace Shadowbrook.Api.Endpoints;

public static class CourseEndpoints
{
    [WolverinePost("/courses")]
    public static async Task<IResult> CreateCourse(
        CreateCourseRequest request,
        ApplicationDbContext db,
        ICurrentUser currentUser)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { error = "Name is required." });
        }

        var tenantId = currentUser.TenantId ?? request.TenantId;
        if (tenantId is null)
        {
            return Results.BadRequest(new { error = "TenantId is required (via X-Tenant-Id header or request body)." });
        }

        var tenant = await db.Tenants.FindAsync(tenantId.Value);
        if (tenant is null)
        {
            return Results.BadRequest(new { error = "Tenant does not exist." });
        }

        var normalizedName = request.Name.ToLower();
        var duplicateExists = await db.Courses
            .IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId.Value && c.Name.ToLower() == normalizedName);
        if (duplicateExists)
        {
            return Results.Conflict(new { error = "A course with this name already exists for this tenant." });
        }

        var course = new Course
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Name = request.Name,
            StreetAddress = request.StreetAddress,
            City = request.City,
            State = request.State,
            ZipCode = request.ZipCode,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Courses.Add(course);

        var response = new CourseResponse(
            course.Id,
            course.Name,
            course.StreetAddress,
            course.City,
            course.State,
            course.ZipCode,
            course.ContactEmail,
            course.ContactPhone,
            course.CreatedAt,
            course.UpdatedAt,
            new TenantInfo(tenant.Id, tenant.OrganizationName));

        return Results.Created($"/courses/{course.Id}", response);
    }

    [WolverineGet("/courses")]
    public static async Task<IResult> GetAllCourses(ApplicationDbContext db, ICurrentUser currentUser)
    {
        var courses = await db.Courses
            .Include(c => c.Tenant)
            .Select(c => new CourseResponse(
                c.Id,
                c.Name,
                c.StreetAddress,
                c.City,
                c.State,
                c.ZipCode,
                c.ContactEmail,
                c.ContactPhone,
                c.CreatedAt,
                c.UpdatedAt,
                new TenantInfo(c.Tenant!.Id, c.Tenant.OrganizationName)))
            .ToListAsync();
        return Results.Ok(courses);
    }

    [WolverineGet("/courses/{id}")]
    public static async Task<IResult> GetCourseById(Guid id, ApplicationDbContext db)
    {
        var course = await db.Courses
            .Include(c => c.Tenant)
            .Where(c => c.Id == id)
            .Select(c => new CourseResponse(
                c.Id,
                c.Name,
                c.StreetAddress,
                c.City,
                c.State,
                c.ZipCode,
                c.ContactEmail,
                c.ContactPhone,
                c.CreatedAt,
                c.UpdatedAt,
                new TenantInfo(c.Tenant!.Id, c.Tenant.OrganizationName)))
            .FirstOrDefaultAsync();
        return course is null ? Results.NotFound() : Results.Ok(course);
    }

    [WolverinePut("/courses/{id}/tee-time-settings")]
    public static async Task<IResult> UpdateTeeTimeSettings(
        Guid id,
        TeeTimeSettingsRequest request,
        ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        if (!AllowedIntervals.Contains(request.TeeTimeIntervalMinutes))
        {
            return Results.BadRequest(new { error = "Interval must be 8, 10, or 12 minutes." });
        }

        if (request.FirstTeeTime >= request.LastTeeTime)
        {
            return Results.BadRequest(new { error = "First tee time must be before last tee time." });
        }

        course.TeeTimeIntervalMinutes = request.TeeTimeIntervalMinutes;
        course.FirstTeeTime = request.FirstTeeTime;
        course.LastTeeTime = request.LastTeeTime;
        course.UpdatedAt = DateTimeOffset.UtcNow;

        return Results.Ok(new TeeTimeSettingsResponse(
            course.TeeTimeIntervalMinutes.Value,
            course.FirstTeeTime.Value,
            course.LastTeeTime.Value));
    }

    [WolverineGet("/courses/{id}/tee-time-settings")]
    public static async Task<IResult> GetTeeTimeSettings(Guid id, ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        if (course.TeeTimeIntervalMinutes is null || course.FirstTeeTime is null || course.LastTeeTime is null)
        {
            return Results.Ok(new { });
        }

        return Results.Ok(new TeeTimeSettingsResponse(
            course.TeeTimeIntervalMinutes.Value,
            course.FirstTeeTime.Value,
            course.LastTeeTime.Value));
    }

    [WolverinePut("/courses/{id}/pricing")]
    public static async Task<IResult> UpdatePricing(
        Guid id,
        PricingRequest request,
        ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        if (request.FlatRatePrice < 0)
        {
            return Results.BadRequest(new { error = "Price must be greater than or equal to 0." });
        }

        if (request.FlatRatePrice > 10000)
        {
            return Results.BadRequest(new { error = "Price must be less than or equal to 10000." });
        }

        course.FlatRatePrice = request.FlatRatePrice;
        course.UpdatedAt = DateTimeOffset.UtcNow;

        return Results.Ok(new PricingResponse(course.FlatRatePrice.Value));
    }

    [WolverineGet("/courses/{id}/pricing")]
    public static async Task<IResult> GetPricing(Guid id, ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        if (course.FlatRatePrice is null)
        {
            return Results.Ok(new { });
        }

        return Results.Ok(new PricingResponse(course.FlatRatePrice.Value));
    }

    private static readonly int[] AllowedIntervals = [8, 10, 12];
}
```

Note: The `CourseExistsFilter` was previously applied to PUT/GET endpoints under `{id}`. Now each endpoint that needs a course does its own lookup and returns 404. This is slightly more explicit but removes the filter dependency.

- [ ] **Step 3: Migrate TeeSheetEndpoints**

```csharp
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Wolverine.Http;

namespace Shadowbrook.Api.Endpoints;

public static class TeeSheetEndpoints
{
    [WolverineGet("/tee-sheets")]
    public static async Task<IResult> GetTeeSheet(
        Guid? courseId,
        string? date,
        ApplicationDbContext db)
    {
        // Body unchanged from current implementation — no SaveAsync to remove
        if (courseId is null)
        {
            return Results.BadRequest(new { error = "courseId query parameter is required." });
        }

        if (string.IsNullOrWhiteSpace(date))
        {
            return Results.BadRequest(new { error = "date query parameter is required." });
        }

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var dateOnly))
        {
            return Results.BadRequest(new { error = "date must be in yyyy-MM-dd format." });
        }

        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId.Value);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        if (course.TeeTimeIntervalMinutes is null || course.FirstTeeTime is null || course.LastTeeTime is null)
        {
            return Results.NotFound(new { error = "Tee time settings not configured for this course." });
        }

        var bookings = await db.Bookings
            .Where(b => b.CourseId == courseId.Value && b.Date == dateOnly)
            .ToListAsync();

        var slots = new List<TeeSheetSlot>();
        var currentTime = course.FirstTeeTime.Value;
        var interval = TimeSpan.FromMinutes(course.TeeTimeIntervalMinutes.Value);

        while (currentTime < course.LastTeeTime.Value)
        {
            var booking = bookings.FirstOrDefault(b => b.Time == currentTime);

            slots.Add(new TeeSheetSlot(
                currentTime.ToString("HH:mm"),
                booking is not null ? "booked" : "open",
                booking?.GolferName,
                booking?.PlayerCount ?? 0));

            currentTime = currentTime.Add(interval);
        }

        return Results.Ok(new TeeSheetResponse(
            course.Id,
            course.Name,
            dateOnly.ToString("yyyy-MM-dd"),
            slots));
    }
}
```

- [ ] **Step 4: Migrate WalkUpWaitlistEndpoints**

Key change: Remove `await repo.SaveAsync()` from `OpenWaitlist`, `CloseWaitlist`, `CreateWaitlistRequest`. For `AddGolferToWaitlist`, see the special handling note below.

**Special handling for golfer upsert:** The `AddGolferToWaitlist` endpoint has a mid-flow `await golferRepo.SaveAsync()` for a race-condition upsert pattern. Since the transactional middleware commits at the end, we need `ApplicationDbContext` injected directly so we can call `db.SaveChangesAsync()` for just the golfer insert, then continue. This is the one place where an explicit mid-flow save is intentional. Keep the `db.SaveChangesAsync()` call for the golfer creation only (not via repo — call it directly on the injected `ApplicationDbContext`).

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate.Exceptions;
using Wolverine.Http;

namespace Shadowbrook.Api.Endpoints;

public static class WalkUpWaitlistEndpoints
{
    [WolverinePost("/courses/{courseId}/walkup-waitlist/open")]
    public static async Task<IResult> OpenWaitlist(
        Guid courseId,
        IWalkUpWaitlistRepository repo,
        IShortCodeGenerator shortCodeGenerator,
        ApplicationDbContext db)
    {
        var courseExists = await db.Courses.AnyAsync(c => c.Id == courseId);
        if (!courseExists)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await WalkUpWaitlist.OpenAsync(courseId, today, shortCodeGenerator, repo);

        repo.Add(waitlist);

        var response = ToResponse(waitlist);
        return Results.Created($"/courses/{courseId}/walkup-waitlist/today", response);
    }

    [WolverinePost("/courses/{courseId}/walkup-waitlist/close")]
    public static async Task<IResult> CloseWaitlist(
        Guid courseId,
        IWalkUpWaitlistRepository repo,
        ApplicationDbContext db)
    {
        var courseExists = await db.Courses.AnyAsync(c => c.Id == courseId);
        if (!courseExists)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await repo.GetOpenByCourseDateAsync(courseId, today);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "No open walk-up waitlist found for today." });
        }

        waitlist.Close();

        return Results.Ok(ToResponse(waitlist));
    }

    [WolverineGet("/courses/{courseId}/walkup-waitlist/today")]
    public static async Task<IResult> GetToday(
        Guid courseId,
        IWalkUpWaitlistRepository repo,
        ApplicationDbContext db)
    {
        var courseExists = await db.Courses.AnyAsync(c => c.Id == courseId);
        if (!courseExists)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await repo.GetByCourseDateAsync(courseId, today);

        var waitlistResponse = waitlist is not null ? ToResponse(waitlist) : null;

        var entries = waitlist is not null
            ? (await db.GolferWaitlistEntries
                .Where(e => e.CourseWaitlistId == waitlist.Id && e.RemovedAt == null)
                .Join(db.Golfers.IgnoreQueryFilters(),
                    e => e.GolferId, g => g.Id,
                    (e, g) => new WalkUpWaitlistEntryResponse(e.Id, g.FirstName + " " + g.LastName, e.GroupSize, e.JoinedAt))
                .ToListAsync())
                .OrderBy(e => e.JoinedAt)
                .ToList()
            : new List<WalkUpWaitlistEntryResponse>();

        var requests = (await db.TeeTimeRequests
                .Where(r => r.CourseId == courseId && r.Date == today)
                .Select(r => new { r.Id, r.TeeTime, r.GolfersNeeded, r.Status })
                .ToListAsync())
                .Select(r => new WalkUpWaitlistRequestResponse(r.Id, r.TeeTime.ToString("HH:mm"), r.GolfersNeeded, r.Status.ToString()))
                .ToList();

        return Results.Ok(new WalkUpWaitlistTodayResponse(waitlistResponse, entries, requests));
    }

    [WolverinePost("/courses/{courseId}/walkup-waitlist/requests")]
    public static async Task<IResult> CreateWaitlistRequest(
        Guid courseId,
        CreateWalkUpWaitlistRequestRequest request,
        TeeTimeRequestService teeTimeRequestService,
        ITeeTimeRequestRepository teeTimeRequestRepo,
        ApplicationDbContext db)
    {
        var courseExists = await db.Courses.AnyAsync(c => c.Id == courseId);
        if (!courseExists)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var parsedDate = DateOnly.ParseExact(request.Date, "yyyy-MM-dd");
        var parsedTeeTime = TimeOnly.ParseExact(request.TeeTime, ["HH:mm", "HH:mm:ss"]);

        var teeTimeRequest = await teeTimeRequestService.CreateAsync(
            courseId, parsedDate, parsedTeeTime, request.GolfersNeeded);

        teeTimeRequestRepo.Add(teeTimeRequest);

        var response = new WalkUpWaitlistRequestResponse(
            teeTimeRequest.Id,
            teeTimeRequest.TeeTime.ToString("HH:mm"),
            teeTimeRequest.GolfersNeeded,
            teeTimeRequest.Status.ToString());

        return Results.Created($"/courses/{courseId}/walkup-waitlist/requests/{teeTimeRequest.Id}", response);
    }

    [WolverinePost("/courses/{courseId}/walkup-waitlist/entries")]
    public static async Task<IResult> AddGolferToWaitlist(
        Guid courseId,
        AddGolferToWaitlistRequest request,
        IWalkUpWaitlistRepository repo,
        IGolferWaitlistEntryRepository entryRepo,
        IGolferRepository golferRepo,
        ApplicationDbContext db)
    {
        var courseExists = await db.Courses.AnyAsync(c => c.Id == courseId);
        if (!courseExists)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var normalizedPhone = PhoneNormalizer.Normalize(request.Phone);

        var waitlist = await repo.GetOpenByCourseDateAsync(courseId, today);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "No open walk-up waitlist found for today." });
        }

        var courseName = await db.Courses
            .Where(c => c.Id == courseId)
            .Select(c => c.Name)
            .FirstAsync();

        // Golfer lookup-or-create — intentional mid-flow save for race condition handling
        var golfer = await golferRepo.GetByPhoneAsync(normalizedPhone!);

        if (golfer is null)
        {
            golfer = Golfer.Create(normalizedPhone!, request.FirstName, request.LastName);
            golferRepo.Add(golfer);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                db.Entry(golfer).State = EntityState.Detached;
                golfer = await golferRepo.GetByPhoneAsync(normalizedPhone!);

                if (golfer is null)
                {
                    return Results.Problem("Unable to create or retrieve golfer record.", statusCode: 500);
                }
            }
        }

        var duplicate = await entryRepo.GetActiveByWaitlistAndGolferAsync(waitlist.Id, golfer.Id);
        if (duplicate is not null)
        {
            throw new GolferAlreadyOnWaitlistException(golfer.Phone);
        }

        var entry = waitlist.AddGolfer(golfer, request.GroupSize ?? 1);
        entryRepo.Add(entry);

        var joinedAt = entry.JoinedAt;
        var activeEntries = await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == waitlist.Id && e.RemovedAt == null)
            .Select(e => e.JoinedAt)
            .ToListAsync();
        var position = activeEntries.Count(t => t <= joinedAt);

        return Results.Created(
            $"/courses/{courseId}/walkup-waitlist/entries/{entry.Id}",
            new AddGolferToWaitlistResponse(
                entry.Id,
                golfer.FullName,
                golfer.Phone,
                entry.GroupSize,
                position,
                courseName));
    }

    private static WalkUpWaitlistResponse ToResponse(WalkUpWaitlist w) =>
        new(w.Id, w.CourseId, w.ShortCode, w.Date.ToString("yyyy-MM-dd"), w.Status.ToString(), w.OpenedAt, w.ClosedAt);
}
```

- [ ] **Step 5: Migrate WalkUpJoinEndpoints**

Same golfer upsert pattern applies here.

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate.Exceptions;
using Wolverine.Http;

namespace Shadowbrook.Api.Endpoints;

public static class WalkUpJoinEndpoints
{
    [WolverinePost("/walkup/verify")]
    public static async Task<IResult> VerifyShortCode(VerifyCodeRequest request, ApplicationDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await db.WalkUpWaitlists
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.ShortCode == request.Code && w.Date == today && w.Status == WaitlistStatus.Open);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "Code not found or waitlist is not active." });
        }

        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == waitlist.CourseId)
            .Select(c => c.Name)
            .FirstAsync();

        return Results.Ok(new VerifyCodeResponse(
            waitlist.Id,
            courseName,
            waitlist.ShortCode));
    }

    [WolverinePost("/walkup/join")]
    public static async Task<IResult> JoinWaitlist(
        JoinWaitlistRequest request,
        IWalkUpWaitlistRepository waitlistRepo,
        IGolferWaitlistEntryRepository entryRepo,
        IGolferRepository golferRepo,
        ApplicationDbContext db)
    {
        var normalizedPhone = PhoneNormalizer.Normalize(request.Phone);

        var waitlist = await waitlistRepo.GetByIdAsync(request.CourseWaitlistId);

        if (waitlist is null || waitlist.Status != WaitlistStatus.Open)
        {
            return Results.NotFound(new { error = "This waitlist is no longer accepting new entries." });
        }

        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == waitlist.CourseId)
            .Select(c => c.Name)
            .FirstAsync();

        // Golfer lookup-or-create — intentional mid-flow save
        var golfer = await golferRepo.GetByPhoneAsync(normalizedPhone!);

        if (golfer is null)
        {
            golfer = Golfer.Create(normalizedPhone!, request.FirstName, request.LastName);
            golferRepo.Add(golfer);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                db.Entry(golfer).State = EntityState.Detached;
                golfer = await golferRepo.GetByPhoneAsync(normalizedPhone!);

                if (golfer is null)
                {
                    return Results.Problem("Unable to create or retrieve golfer record.", statusCode: 500);
                }
            }
        }

        var duplicate = await entryRepo.GetActiveByWaitlistAndGolferAsync(waitlist.Id, golfer.Id);
        if (duplicate is not null)
        {
            throw new GolferAlreadyOnWaitlistException(golfer.Phone);
        }

        var entry = waitlist.AddGolfer(golfer);
        entryRepo.Add(entry);

        var joinedAt = entry.JoinedAt;
        var activeEntries = await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == waitlist.Id && e.RemovedAt == null)
            .Select(e => e.JoinedAt)
            .ToListAsync();
        var position = activeEntries.Count(t => t <= joinedAt);

        return Results.Created($"/walkup/join", new JoinWaitlistResponse(
            entry.Id,
            golfer.FullName,
            position,
            courseName));
    }
}
```

Keep all record types and validators at bottom unchanged.

- [ ] **Step 6: Migrate WaitlistOfferEndpoints**

```csharp
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Wolverine.Http;

namespace Shadowbrook.Api.Endpoints;

public static class WaitlistOfferEndpoints
{
    [WolverineGet("/waitlist/offers/{token}")]
    public static async Task<IResult> GetOffer(Guid token, ApplicationDbContext db)
    {
        // Body unchanged — read-only, no SaveAsync
        var raw = await db.WaitlistOffers
            .IgnoreQueryFilters()
            .Where(o => o.Token == token)
            .Join(db.TeeTimeRequests, o => o.TeeTimeRequestId, r => r.Id, (o, r) => new { Offer = o, Request = r })
            .Join(db.GolferWaitlistEntries, or => or.Offer.GolferWaitlistEntryId, e => e.Id, (or, e) => new { or.Offer, or.Request, Entry = e })
            .Join(db.Golfers.IgnoreQueryFilters(), ore => ore.Entry.GolferId, g => g.Id, (ore, g) => new { ore.Offer, ore.Request, ore.Entry, Golfer = g })
            .Join(db.Courses.IgnoreQueryFilters(), oreg => oreg.Request.CourseId, c => c.Id, (oreg, c) => new
            {
                oreg.Offer.Token,
                CourseName = c.Name,
                oreg.Request.Date,
                oreg.Request.TeeTime,
                oreg.Request.GolfersNeeded,
                oreg.Golfer.FirstName,
                oreg.Golfer.LastName,
                oreg.Offer.Status
            })
            .FirstOrDefaultAsync();

        if (raw is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        return Results.Ok(new WaitlistOfferResponse(
            raw.Token,
            raw.CourseName,
            raw.Date.ToString("yyyy-MM-dd"),
            raw.TeeTime.ToString("HH:mm"),
            raw.GolfersNeeded,
            $"{raw.FirstName} {raw.LastName}",
            raw.Status.ToString()));
    }

    [WolverinePost("/waitlist/offers/{token}/accept")]
    public static async Task<IResult> AcceptOffer(
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
        // No SaveAsync — transactional middleware handles it
        // Domain events from offer.Accept() are scraped automatically

        return Results.Ok(new WaitlistOfferAcceptResponse(
            "Processing",
            "We're processing your request — you'll receive a confirmation shortly."));
    }
}
```

Keep record types unchanged.

- [ ] **Step 7: Keep DevSmsEndpoints as minimal APIs**

`DevSmsEndpoints` is a dev-only utility that doesn't touch the database or raise domain events. Keep it as a minimal API endpoint — Wolverine HTTP and minimal APIs coexist. No changes needed to this file.

- [ ] **Step 8: Update Program.cs — remove manual endpoint registration, keep dev/test endpoints**

Remove the `Map*Endpoints` extension method calls and the validation filter group. Keep `MapDevSmsEndpoints` and the testing debug endpoint as minimal APIs.

Replace lines 127-133:
```csharp
var api = app.MapGroup("").AddValidationFilter();
api.MapTenantEndpoints();
api.MapCourseEndpoints();
api.MapTeeSheetEndpoints();
api.MapWalkUpWaitlistEndpoints();
api.MapWalkUpJoinEndpoints();
api.MapWaitlistOfferEndpoints();
```

With nothing — `app.MapWolverineEndpoints()` (already added in Task 2) discovers all Wolverine HTTP endpoints automatically.

Also remove the `using Shadowbrook.Api.Endpoints.Filters;` import if no longer needed, and the `using Shadowbrook.Api.Endpoints;` import if no Map extension methods remain.

- [ ] **Step 9: Delete ValidationFilter and CourseExistsFilter**

Delete these files — they are ASP.NET endpoint filters no longer used:
- `src/backend/Shadowbrook.Api/Endpoints/Filters/ValidationFilter.cs`
- `src/backend/Shadowbrook.Api/Endpoints/Filters/CourseExistsFilter.cs`

Wolverine's FluentValidation middleware replaces the ValidationFilter. Course existence checks are now inline in each endpoint.

- [ ] **Step 10: Verify build**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "feat: migrate all HTTP endpoints to Wolverine HTTP"
```

---

### Task 7: Fix integration tests

**Files:**
- Modify: `tests/Shadowbrook.Api.Tests/TestWebApplicationFactory.cs`
- Possibly modify: individual test files that call endpoints

The test factory already runs Wolverine in solo mode with external transports disabled. However:
1. Wolverine HTTP endpoints are discovered automatically — tests hitting HTTP routes should still work
2. The transactional middleware runs in solo mode — domain events are processed in-memory
3. FluentValidation middleware is active in tests

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test tests/Shadowbrook.Api.Tests -v n`
Expected: Note which tests pass and which fail

- [ ] **Step 2: Fix any test failures**

Common issues to watch for:
- **Route changes**: Wolverine HTTP routes should match the attribute routes exactly. If tests use specific URLs, verify they match.
- **Validation response format**: Wolverine's FluentValidation middleware may return a different error format than the custom `ValidationFilter`. The custom filter returned `{ "error": "message" }`. Wolverine's FluentValidation returns RFC 7807 Problem Details. Update test assertions if needed.
- **SaveChangesAsync timing**: The transactional middleware commits after the endpoint returns. If tests check database state immediately after an HTTP call, this should still work because the middleware runs before the response is sent to the client.

- [ ] **Step 3: Run tests again to verify all pass**

Run: `dotnet test tests/Shadowbrook.Api.Tests -v n`
Expected: All tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "fix: update integration tests for Wolverine HTTP migration"
```

---

### Task 8: Clean up unused code

**Files:**
- Delete: `src/backend/Shadowbrook.Api/Endpoints/Filters/` directory (if not already deleted)
- Modify: `src/backend/Shadowbrook.Api/Program.cs` — remove unused usings

- [ ] **Step 1: Remove unused imports and dead code**

In `Program.cs`, remove any unused `using` statements related to the old endpoint filters or manual endpoint registration.

- [ ] **Step 2: Final build and test**

Run: `dotnet build shadowbrook.slnx && dotnet test tests/Shadowbrook.Api.Tests -v q`
Expected: Build succeeded, all tests pass

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "chore: clean up unused imports and endpoint filter code"
```

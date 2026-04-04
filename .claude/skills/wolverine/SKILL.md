---
name: wolverine
description: Use when working with Wolverine HTTP endpoints, message handlers, EF Core transactional middleware, domain event scraping, or FluentValidation in this project. Also use when debugging test failures related to messaging, event publishing, or parameter binding.
user-invocable: false
---

# Wolverine Patterns & Gotchas

Canonical reference: https://wolverinefx.net/llms-full.txt — fetch this when you need details beyond what's here.

## Quick Reference

| What | How |
|------|-----|
| HTTP endpoint | `[WolverinePost("/route")] public static async Task<IResult> Method(...)` |
| Route param | `Guid id` matches `{id}` in route |
| Request body | First complex non-service type parameter |
| DI service | Interfaces and registered types resolve automatically |
| DbContext param | Use `[NotBody]` only on POST/PUT endpoints where no request body type exists (e.g., `OpenWaitlist(Guid courseId, [NotBody] ApplicationDbContext db)`). Not needed on GET endpoints or when another type is already the body. |
| Save changes | **Don't** — transactional middleware calls `SaveChangesAsync()` automatically |
| Domain events | Scraped from `Entity.DomainEvents` via `PublishDomainEventsFromEntityFrameworkCore` |
| Cascading messages | Return `object?` from handler — non-null values published as messages |
| Before middleware | `public static async Task<IResult?> Before(...)` on same class, or standalone class via policy |

## Critical Configuration (all required)

```csharp
builder.Host.UseWolverine(opts =>
{
    // These three are ALL required for full functionality:
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.AutoApplyTransactions();  // NOT on by default!
    opts.PublishDomainEventsFromEntityFrameworkCore<Entity, IDomainEvent>(e => e.DomainEvents);

    opts.UseFluentValidation();  // For message handlers only
});

builder.Services.AddWolverineHttp();

app.MapWolverineEndpoints(opts =>
{
    opts.UseFluentValidationProblemDetailMiddleware();  // For HTTP endpoints — separate package!

    // Policy-based middleware (e.g., course-exists check on all {courseId} routes):
    opts.AddMiddleware(typeof(CourseExistsMiddleware),
        chain => chain.RoutePattern?.RawText?.Contains("{courseId}") == true);
});
```

## Packages

| Package | Purpose |
|---------|---------|
| `WolverineFx` | Core messaging |
| `WolverineFx.SqlServer` | SQL Server transport + message persistence |
| `WolverineFx.EntityFrameworkCore` | EF Core transactional middleware + domain event scraping |
| `WolverineFx.Http` | HTTP endpoint support |
| `WolverineFx.FluentValidation` | Validation for **message handlers** |
| `WolverineFx.Http.FluentValidation` | Validation for **HTTP endpoints** (separate package!) |

## Common Mistakes

### `AutoApplyTransactions()` not called
**Symptom:** Data not persisting between HTTP requests. Endpoints return success but nothing saved.
**Fix:** Add `opts.Policies.AutoApplyTransactions()` inside `UseWolverine`. This is NOT enabled by default.

### `[NotBody]` missing on `ApplicationDbContext`
**Symptom:** `Error trying to deserialize JSON from incoming HTTP body to type ApplicationDbContext`
**When:** POST/PUT endpoints where `ApplicationDbContext` is the first concrete non-service type and there's no request body parameter before it (e.g., `OpenWaitlist(Guid courseId, ApplicationDbContext db)`).
**Not needed:** On GET endpoints (no body), or when another type already serves as the body (e.g., `CreateTenant(CreateTenantRequest request, ApplicationDbContext db)` — `request` is the body).
**Fix:** Add `[NotBody]` to `ApplicationDbContext` on the affected POST/PUT endpoints only.

### Wrong FluentValidation package for HTTP
**Symptom:** FluentValidation validators not running on HTTP endpoints (invalid requests get through).
**Fix:** Add `WolverineFx.Http.FluentValidation` package AND call `opts.UseFluentValidationProblemDetailMiddleware()` on `MapWolverineEndpoints`. The `UseFluentValidation()` on `WolverineOptions` is only for message handlers.

### Domain event scraping not firing
**Symptom:** Domain events on entities not published, downstream handlers never execute.
**Cause:** Domain event scraping requires `EfCoreEnvelopeTransaction` which **hard-requires** `IMessageDatabase` (SQL Server or PostgreSQL). Will NOT work with SQLite.
**Fix:** Use SQL Server for tests via Testcontainers. See `TestWebApplicationFactory.cs`.

### Class mistakenly discovered as a Wolverine handler
**Symptom:** `UnResolvableVariableException` during codegen for a type Wolverine shouldn't be handling (e.g., `PolicyAuthorizationResult`).
**Cause:** Wolverine discovers any class whose name ends with "Handler" and has a `Handle`/`HandleAsync` method. ASP.NET Core's `IAuthorizationMiddlewareResultHandler` matches both conventions, so Wolverine tries to generate code for it and fails on types it can't resolve.
**Fix:** Add `[WolverineIgnore]` (from `Wolverine.Attributes`) to the class. See `AppUserAuthorizationResultHandler.cs`.

### Calling `SaveChangesAsync()` or `SaveAsync()` manually
**Symptom:** Double saves, unexpected behavior, or events published outside Wolverine's pipeline.
**Fix:** Don't call save — the transactional middleware handles it. Only exception: intentional mid-flow saves (like golfer upsert race condition pattern).

## HTTP Endpoint Pattern

```csharp
[WolverinePost("/courses/{courseId}/walkup-waitlist/open")]
public static async Task<IResult> OpenWaitlist(
    Guid courseId,                           // Route param
    [NotBody] ApplicationDbContext db,       // DI — MUST have [NotBody]
    IWalkUpWaitlistRepository repo,          // DI (interface)
    IShortCodeGenerator shortCodeGenerator)  // DI (interface)
{
    // Business logic — NO SaveChangesAsync call
    repo.Add(waitlist);
    return Results.Created($"/...", response);
    // Transactional middleware saves + scrapes domain events automatically
}
```

## Before Middleware Pattern

```csharp
// Standalone middleware class
public static class CourseExistsMiddleware
{
    public static async Task<IResult?> Before(Guid courseId, [NotBody] ApplicationDbContext db)
    {
        var exists = await db.Courses.AnyAsync(c => c.Id == courseId);
        return exists ? null : Results.NotFound(new { error = "Course not found." });
    }
}

// Apply via policy in MapWolverineEndpoints:
opts.AddMiddleware(typeof(CourseExistsMiddleware),
    chain => chain.RoutePattern?.RawText?.Contains("{courseId}") == true);
```

Return `null` to continue, return `IResult` to short-circuit.

## Cascading Messages Pattern

For handler-level orchestration events (not raised by domain entities):

```csharp
public static async Task<object?> Handle(
    WaitlistOfferAccepted domainEvent,
    ITeeTimeRequestRepository requestRepository)
{
    // Return value is published as a cascading message
    // Return null for no message
    return new TeeTimeSlotFilled { ... };
}
```

## Testing

### Unit Tests — Handlers (NSubstitute)

Wolverine handlers are static methods with injected dependencies — test them directly by calling `Handle()` with NSubstitute stubs for repository interfaces and real domain objects for aggregates.

```csharp
using NSubstitute;

public class MyHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly ITextMessageService sms = Substitute.For<ITextMessageService>();

    [Fact]
    public async Task Handle_GolferNotFound_NoSms()
    {
        // Stub: return null for any golfer lookup
        // (NSubstitute returns null by default, but be explicit when it matters)

        var evt = new SomeEvent { GolferId = Guid.NewGuid() };
        await MyHandler.Handle(evt, golferRepo, sms, CancellationToken.None);

        // Verify SMS was NOT sent
        await sms.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_SendsSms()
    {
        // Use real domain objects — they have behavior worth exercising
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        var evt = new SomeEvent { GolferId = golfer.Id };
        await MyHandler.Handle(evt, golferRepo, sms, CancellationToken.None);

        // Verify SMS was sent with correct phone and message content
        await sms.Received(1).SendAsync(
            "+15551234567",
            Arg.Is<string>(m => m.Contains("expected text")),
            Arg.Any<CancellationToken>());
    }
}
```

**Key patterns:**
- `Substitute.For<IRepository>()` — stub repository interfaces
- `.Returns(entity)` — control what the handler sees
- `.Received(1)` / `.DidNotReceive()` — verify side effects (SMS, repository writes)
- `Arg.Is<T>(predicate)` — match arguments with conditions
- `Arg.Any<T>()` — match any argument of type T
- Use **real** domain objects (`Golfer.Create(...)`, `new GolferWaitlistEntry(...)`, `WaitlistOffer.Create(...)`) — don't substitute aggregates
- Handlers that return cascading messages (`Task<object?>`) — assert the return type: `Assert.IsType<TeeTimeSlotFilled>(result)`

**When to unit test vs integration test handlers:**
- **Unit test:** Handlers with only repository interface dependencies — stub with NSubstitute
- **Integration test:** Handlers with direct `ApplicationDbContext` queries (joins, query filters) — need real DB

### Unit Tests — Validators

Test FluentValidation validators by calling `Validate()` directly — no HTTP needed:

```csharp
public class MyRequestValidatorTests
{
    private readonly MyRequestValidator validator = new();

    [Fact]
    public void ValidRequest_Passes() =>
        Assert.True(validator.Validate(new MyRequest("valid")).IsValid);

    [Fact]
    public void EmptyField_Fails()
    {
        var result = validator.Validate(new MyRequest(""));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "FieldName");
    }
}
```

### Integration Tests — Full Stack

Integration tests use Testcontainers SQL Server (required for Wolverine's transactional middleware):

```csharp
public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly MsSqlContainer SqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    // Override ConnectionStrings:DefaultConnection so Wolverine's
    // UseSqlServerPersistenceAndTransport picks up the test container
    builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
}
```

**Key:** The connection string override must happen via `UseSetting` so that `Program.cs`'s `GetConnectionString("DefaultConnection")` returns the test container's connection string. This enables Wolverine's SQL Server persistence in tests.

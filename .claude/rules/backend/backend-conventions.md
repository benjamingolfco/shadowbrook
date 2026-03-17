---
paths:
  - "src/backend/**/*.cs"
  - "tests/**/*.cs"
---

# Backend Conventions

## Code Style

`.editorconfig` at repo root defines all C# style rules (suggestion severity). Key conventions:

- Prefer `var` over explicit types
- Always use braces on control flow statements
- Private fields use `camelCase` (no underscore prefix), qualified with `this.`
- Prefer target-typed `new()` and primary constructors for DI classes
- File-scoped namespaces, nullable reference types, implicit usings
- `is null` / `is not null` for null checks (not `== null`)
- Always explicit accessibility modifiers
- String interpolation over concatenation

## API Patterns

- Minimal API endpoints in `src/backend/Shadowbrook.Api/Endpoints/`, extension method pattern (`MapXxxEndpoints`)
- Inline DTOs as records within endpoint files
- `Results.*` return pattern (`Results.Ok()`, `Results.BadRequest()`, `Results.NotFound()`)
- Multi-tenant scoping via `ICurrentUser.TenantId` and EF query filters
- Endpoint filters for cross-cutting concerns on route groups (e.g., `CourseExistsFilter` validates course existence for all endpoints under `/courses/{courseId:guid}/...`). Add filters via `.AddEndpointFilter<T>()` on `MapGroup()`. Filters live in `Endpoints/Filters/`.

## Request Validation

- Use FluentValidation (`AbstractValidator<T>`) for request object validation — not manual `if` checks in handlers
- Validators are auto-registered via `AddValidatorsFromAssemblyContaining<Program>()` in `Program.cs`
- A generic `ValidationFilter` (`Endpoints/Filters/ValidationFilter.cs`) runs validation automatically before handlers execute — add it to route groups via `.AddValidationFilter()`
- The filter discovers validators at startup (no per-request reflection) and short-circuits with `Results.BadRequest(new { error = "..." })` on failure
- Validators live in the same file as their request record DTOs (inline pattern), or in a separate file if complex
- Endpoints with `.AddValidationFilter()` can trust that the request body is valid — no need for manual validation of fields that have validator rules
- For endpoints without the filter, inject `IValidator<T>` directly if needed
- The filter is a no-op for request types without a registered validator, so it's safe to apply broadly

## Identifiers

- Use `Guid.CreateVersion7()` when generating new GUIDs for database identifiers — it produces time-ordered UUIDs (UUIDv7) that sort chronologically, avoiding index fragmentation in SQL Server

## Domain-Driven Design

### Core Principles

- Domain model lives in `Shadowbrook.Domain` (zero dependencies — no EF, no ASP.NET)
- Domain models are pure write/behavior — never add properties or methods to serve read/display concerns
- If the domain model makes it hard to query data for display, create a separate read model (CQRS-lite)
- EF entity type configurations in `Infrastructure/EntityTypeConfigurations/`

### Aggregates

- Inherit from `Entity` base class
- All properties use `private set` — no public setters
- Private parameterless constructor for EF (`private MyAggregate() { }`)
- Static factory methods for construction (e.g., `MyAggregate.Create(...)` or `MyAggregate.CreateAsync(...)`)
- Use `Guid.CreateVersion7()` for all new IDs (time-ordered UUIDs)
- Guard their own invariants — validate state in domain methods before making changes
- Raise domain events via `AddDomainEvent()` inside state-changing methods

### Aggregate Boundaries

- Each aggregate defines a consistency boundary — one transaction should ideally modify one aggregate
- Reference other aggregates by ID only, not by holding direct navigation properties
- Passing another aggregate as a **method parameter** for validation is acceptable (e.g., `offer.Accept(golfer)`)
- An aggregate can serve as a **factory for another aggregate** — validate creation rules and return a new independent aggregate (e.g., `WalkUpWaitlist.AddGolfer()` validates waitlist rules and returns a new `GolferWaitlistEntry` aggregate). The returned aggregate is independent and not owned as a child.
- Use `internal` methods for cross-aggregate operations within the Domain assembly (e.g., `TeeTimeRequest.Fill()` called by `WaitlistOffer.Accept()`)

### Domain Events

- Events carry **identifiers only** — handlers look up the data they need at handling time
- Events are immutable `record` types implementing `IDomainEvent`
- Events are dispatched via Wolverine's `IMessageBus` — `ApplicationDbContext.SaveChangesAsync()` harvests events from tracked entities and publishes them
- Event handlers live in `EventHandlers/` (top-level Api project folder, sibling to `Infrastructure/`)
- Each handler does **one thing** and raises **one event** — chain handlers for multi-step flows

### Wolverine (Message Bus)

The project uses [WolverineFx](https://wolverinefx.net) for message handling, replacing a custom in-process event dispatcher.

**Handler conventions:**
- Handlers are plain classes with a `Handle` method — no interface to implement
- Wolverine discovers handlers automatically by convention (scans the assembly)
- Dependencies are constructor-injected via primary constructors (instance style)
- Method signature: `public async Task Handle(EventType domainEvent, CancellationToken ct)`
- Handlers that need to publish follow-on events inject `IMessageBus` and call `bus.PublishAsync()`

**Configuration (Program.cs):**
- `UseWolverine()` on the host builder with SQL Server persistence and transport
- `MultipleHandlerBehavior.Separated` — multiple handlers for the same event type run independently (isolated failure domains)
- `OnException<DbUpdateConcurrencyException>().RetryTimes(3)` — automatic retry for optimistic concurrency conflicts

**Testing:**
- `TestWebApplicationFactory` disables external transports via `services.DisableAllExternalWolverineTransports()` and `services.RunWolverineInSoloMode()` for SQLite-based tests
- Handlers still fire in tests — only the durable transport is disabled

**Handler example:**
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

### Saga Pattern (Event-Driven Choreography)

For operations spanning multiple aggregates, use sequential event chains instead of locking multiple aggregates in one transaction:

- Each step is a single-aggregate transaction that raises an event
- The next handler reacts to that event and performs the next step
- Compensation flows handle failures by walking backward (undo previous steps)
- Domain outcomes that represent business failures (e.g., rejection, slot full) are **not exceptions** — they are successful state transitions that raise events
- Pre-allocate IDs (e.g., `BookingId` on `WaitlistOffer`) when downstream steps need correlation
- Wolverine's `MultipleHandlerBehavior.Separated` ensures that if one handler for an event fails, the others still run independently

### Result Objects

- For `internal` cross-aggregate methods, prefer returning result objects over throwing exceptions
- Use `record` types: `record FillResult(bool Success, string? RejectionReason = null)`
- The calling aggregate inspects the result and decides what to do (e.g., reject the offer with the reason)
- Reserve exceptions for true invariant violations that should never happen in correct code

### Domain Exceptions

- `DomainException` subclasses break control flow for true invariant violations
- The global exception handler in `Program.cs` maps them to HTTP status codes
- Do NOT catch `DomainException` in endpoints — let them propagate

### Repositories

- Repository interfaces defined in domain, implemented in `Infrastructure/Repositories/`
- One repository per aggregate root
- Return fully loaded aggregates (with child collections via `.Include()`)
- `SaveAsync()` triggers event publishing via `ApplicationDbContext`

### Domain Services

- Domain service interfaces defined in domain (e.g., `IShortCodeGenerator`)
- Implementations in `Infrastructure/Services/`
- Use for cross-aggregate logic that doesn't belong to any single aggregate

### Read Models (CQRS-lite)

- Domain models serve write operations only — no display/query concerns
- Read models are separate classes optimized for queries
- Read models live in the API layer (e.g., inline in endpoint files or `Infrastructure/ReadModels/`)
- Read models can join across aggregates freely (they don't need to respect aggregate boundaries)
- Use for endpoints that need denormalized/combined data from multiple aggregates

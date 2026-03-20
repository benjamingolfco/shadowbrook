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

## Project Structure (Feature-Based)

The API project uses **feature folders** — endpoints, event handlers, validators, and DTOs for a feature live together. Shared infrastructure stays in horizontal layers.

```
Shadowbrook.Api/
  Features/
    Tenants/                              ← feature folder
      TenantEndpoints.cs                  ← endpoints + inline DTOs
    Courses/
      CourseEndpoints.cs
    WalkUpWaitlist/
      WalkUpWaitlistEndpoints.cs          ← endpoints
      WalkUpJoinEndpoints.cs
      GolferJoinedWaitlistSmsHandler.cs   ← event handler co-located with consumer
      BookingCreatedRemoveFromWaitlistHandler.cs  ← reacts to BookingCreated, modifies waitlist
    WaitlistOffers/
      WaitlistOfferEndpoints.cs
      WaitlistOfferAcceptedSmsHandler.cs
      WaitlistOfferRejectedNextOfferHandler.cs
      WaitlistOfferRejectedSmsHandler.cs
      TeeTimeSlotFillFailedHandler.cs     ← reacts to TeeTimeSlotFillFailed, modifies offers
      TeeTimeRequestFulfilledHandler.cs   ← reacts to TeeTimeRequestFulfilled, rejects offers
      TeeTimeRequestAddedNotifyHandler.cs ← reacts to TeeTimeRequestAdded, creates offers
    TeeSheet/
      TeeSheetEndpoints.cs
      WaitlistOfferAcceptedFillHandler.cs ← reacts to WaitlistOfferAccepted, fills tee time
    Bookings/
      TeeTimeSlotFilledBookingHandler.cs
      BookingCreatedConfirmationSmsHandler.cs
  Infrastructure/                         ← shared horizontal concerns
    Data/ApplicationDbContext.cs
    Middleware/                            ← shared Wolverine Before middleware
    Repositories/
    EntityTypeConfigurations/
    Services/
  Auth/
  Models/
```

**Rules:**
- New endpoints and handlers go in `Features/{FeatureName}/`
- Place a handler in the feature that **consumes** the event — the feature whose state or concern the handler modifies (e.g., `BookingCreatedRemoveFromWaitlistHandler` lives in `WalkUpWaitlist/` because it modifies waitlist state, even though it reacts to `BookingCreated`)
- Shared infrastructure (DbContext, repositories, EF configs, services) stays in `Infrastructure/`
- Domain model stays in `Shadowbrook.Domain/` — feature folders are API-layer only

## API Patterns

- Wolverine HTTP endpoints in `src/backend/Shadowbrook.Api/Features/` using `[WolverineGet]`, `[WolverinePost]`, etc.
- Endpoints are `public static` methods on classes, discovered by convention — no manual route registration
- Inline DTOs as records within endpoint files
- `Results.*` return pattern (`Results.Ok()`, `Results.BadRequest()`, `Results.NotFound()`) with `IResult` return type
- Multi-tenant scoping via `ICurrentUser.TenantId` and EF query filters
- Cross-cutting concerns via Wolverine `Before` middleware applied by policy in `MapWolverineEndpoints` (e.g., `CourseExistsMiddleware` validates course existence for all `{courseId}` routes). Shared middleware lives in `Infrastructure/Middleware/`.
- Transactional middleware auto-saves via `UseEntityFrameworkCoreTransactions()` + `AutoApplyTransactions()` — do NOT call `SaveChangesAsync()` in endpoints
- Domain events scraped automatically from tracked entities via `PublishDomainEventsFromEntityFrameworkCore`

## Request Validation

- Use FluentValidation (`AbstractValidator<T>`) for request object validation — not manual `if` checks in handlers
- Validators are auto-registered via `AddValidatorsFromAssemblyContaining<Program>()` in `Program.cs`
- Wolverine HTTP validates automatically via `WolverineFx.Http.FluentValidation` — call `opts.UseFluentValidationProblemDetailMiddleware()` in `MapWolverineEndpoints`
- Wolverine message handlers validate via `WolverineFx.FluentValidation` — call `opts.UseFluentValidation()` in `UseWolverine` (separate package!)
- Validators live in the same file as their request record DTOs (inline pattern), or in a separate file if complex
- Endpoints can trust that the request body is valid — no need for manual validation of fields that have validator rules

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
- An aggregate can serve as a **factory for another aggregate** — validate creation rules and return a new independent aggregate (e.g., `WalkUpWaitlist.Join()` validates waitlist rules and returns a new `GolferWaitlistEntry` aggregate). The returned aggregate is independent and not owned as a child.
- Use `internal` methods for cross-aggregate operations within the Domain assembly (e.g., `TeeTimeRequest.Fill()` called by `WaitlistOffer.Accept()`)

### Domain Events

- Events carry **identifiers only** — handlers look up the data they need at handling time
- Events are immutable `record` types implementing `IDomainEvent`
- Events are dispatched via Wolverine's `IMessageBus` — `ApplicationDbContext.SaveChangesAsync()` harvests events from tracked entities and publishes them
- Event handlers live in `Features/{FeatureName}/` alongside related endpoints
- Each handler does **one thing** and raises **one event** — chain handlers for multi-step flows

### Wolverine (Message Bus)

The project uses [WolverineFx](https://wolverinefx.net) for message handling. See the **wolverine skill** for detailed configuration, endpoint patterns, `[NotBody]` rules, and troubleshooting.

**Handler conventions:**
- Handlers are plain classes with a `Handle` method — no interface to implement
- Wolverine discovers handlers automatically by convention (scans the assembly)
- Dependencies are constructor-injected via primary constructors (instance style)
- Method signature: `public async Task Handle(EventType domainEvent, CancellationToken ct)`
- Handlers that need to publish follow-on events inject `IMessageBus` and call `bus.PublishAsync()`
- `MultipleHandlerBehavior.Separated` — multiple handlers for the same event type run independently

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
- Do NOT call `SaveAsync()` in endpoints or handlers — Wolverine's transactional middleware handles save + domain event publishing automatically

### Domain Services

- Domain service interfaces defined in domain (e.g., `IShortCodeGenerator`)
- Implementations in `Infrastructure/Services/`
- Use for cross-aggregate logic that doesn't belong to any single aggregate

## Testing

### Testing Pyramid

Unit tests first, integration tests second. Test at the cheapest layer that can prove the behavior.

**Unit tests** (no DB, no HTTP, no container):
- Domain aggregates and services — pure behavior, state transitions, event raising
- FluentValidation validators — call `validator.Validate()` directly
- Wolverine message handlers — call `Handle()` with fake repositories (see `tests/Shadowbrook.Api.Tests/Fakes/`)
- Infrastructure utilities (e.g., `PhoneNormalizer`)

**Integration tests** (TestWebApplicationFactory + SQL Server container) — only for what genuinely needs the real stack. Always apply both `[Collection("Integration")]` (fixture sharing) and `[IntegrationTest]` (category trait for `dotnet test --filter Category=Integration`):
- Happy-path E2E flows (tenant → course → waitlist → join)
- DB-dependent behavior (unique constraints, query filters, tenant isolation)
- Middleware behavior (tenant claim, course-exists)
- Smoke tests (health, OpenAPI)

**Do not** use integration tests to verify validation rules, null guards, or handler branching logic. Those belong in unit tests.

### Test Organization

```
tests/Shadowbrook.Domain.Tests/
  {Aggregate}Aggregate/              ← domain unit tests
tests/Shadowbrook.Api.Tests/
  Validators/                        ← FluentValidation unit tests
  Handlers/                          ← Wolverine handler unit tests
  *.cs                               ← integration tests (use TestWebApplicationFactory)
```

### NSubstitute for Stubs

Use NSubstitute (`Substitute.For<IProvideAServiceInterface>()`) to stub interfaces in handler unit tests. Use real domain objects (aggregates, entities) — don't substitute those, they have behavior worth exercising. Use `Received()` / `DidNotReceive()` to verify side effects like SMS sends or repository writes.

### Read Models (CQRS-lite)

- Domain models serve write operations only — no display/query concerns
- Read models are separate classes optimized for queries
- Read models live in the API layer (e.g., inline in endpoint files or `Infrastructure/ReadModels/`)
- Read models can join across aggregates freely (they don't need to respect aggregate boundaries)
- Use for endpoints that need denormalized/combined data from multiple aggregates

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
- Never use `Async` suffix on method names — the `Task<T>` return type already signals async behavior
- When an interface and its implementation live in the same file, name the file after the implementation — never after the interface. (e.g., `DeliverSmsHandler.cs` containing both `DeliverSms` and `DeliverSmsHandler`, not `IDeliverSms.cs`.) If they live in separate files, the interface file is named after the interface as usual.

## Project Structure (Feature-Based)

The API project uses **feature folders** — endpoints, event handlers, validators, and DTOs for a feature live together. Shared infrastructure stays in horizontal layers.

```
Teeforce.Api/
  Features/
    Tenants/                              ← feature folder
      Endpoints/
        TenantEndpoints.cs
    Courses/
      Endpoints/
        CourseEndpoints.cs
    Waitlist/                             ← all waitlist concerns in one feature
      Endpoints/
        WalkUpWaitlistEndpoints.cs
        WalkUpJoinEndpoints.cs
        WalkUpQrEndpoints.cs
        WaitlistOfferEndpoints.cs
      Handlers/                           ← grouped by triggering event/command
        GolferJoinedWaitlist/
          NotificationHandler.cs
          WakeUpOfferPolicyHandler.cs
        TeeTimeOpeningFilled/
          RejectOffersHandler.cs
        TeeTimeOpeningCancelled/
          RejectOffersHandler.cs
        TeeTimeOpeningSlotsClaimed/
          NotificationHandler.cs          ← confirmation notification to golfer
        WaitlistOfferAccepted/
          RemoveFromWaitlistHandler.cs
        WaitlistOfferRejected/
          NotificationHandler.cs
      Policies/                           ← Wolverine sagas
        TeeTimeOpeningExpirationPolicy.cs
        TeeTimeOpeningOfferPolicy.cs
        WaitlistOfferResponsePolicy.cs
    Bookings/
      Handlers/
        TeeTimeOpeningSlotsClaimed/
          CreateConfirmedBookingHandler.cs
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
- Each feature folder has three subfolders: `Endpoints/`, `Handlers/`, `Policies/`
- Handlers are grouped by the event/command they handle (subfolder per event)
- Place a handler in the feature that **consumes** the event — the feature whose state or concern the handler modifies (e.g., `BookingCreated/ClaimHandler` lives in `Waitlist/` because it modifies opening state, even though it reacts to `BookingCreated`)
- Shared infrastructure (DbContext, repositories, EF configs, services) stays in `Infrastructure/`
- Domain model stays in `Teeforce.Domain/` — feature folders are API-layer only

## API Patterns

- Wolverine HTTP endpoints in `src/backend/Teeforce.Api/Features/` using `[WolverineGet]`, `[WolverinePost]`, etc.
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

## Configuration (IOptions Pattern)

- Always use strongly-typed options classes — never read `IConfiguration["key"]` directly in application code
- Options classes live in `Infrastructure/Configuration/`
- Register with `builder.Services.Configure<T>(builder.Configuration.GetSection("SectionName"))` near the top of service registrations in `Program.cs`
- Inject `IOptions<T>` (singleton-lifetime config) — not `IOptionsSnapshot` or `IOptionsMonitor` unless reload-on-change is explicitly needed
- Access the value via the `.Value` property
- Keep options classes simple POCOs with `{ get; init; }` properties and sensible defaults

```csharp
// Infrastructure/Configuration/AppSettings.cs
public class AppSettings
{
    public string FrontendUrl { get; init; } = string.Empty;
}

// Program.cs
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));

// Handler
public static async Task Handle(IOptions<AppSettings> appSettings, ...)
{
    var baseUrl = appSettings.Value.FrontendUrl;
}
```

## Identifiers

- Use `Guid.CreateVersion7()` when generating new GUIDs for database identifiers — it produces time-ordered UUIDs (UUIDv7) that sort chronologically, avoiding index fragmentation in SQL Server

## Domain-Driven Design

### Core Principles

- Domain model lives in `Teeforce.Domain` (zero dependencies — no EF, no ASP.NET)
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
- Never use `InternalsVisibleTo` to expose domain internals for testing or handler consumption. If you need to test or call an `internal` method from outside the domain, rethink the design — either make it part of the public API or restructure so the behavior is exercised through public methods. Tests should always test the public interface.

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

### Command Colocation

Command and message records MUST be defined in the same file as their handler. This keeps the
message contract and its processing logic together — if you're reading a handler, you can see
exactly what shape of message it expects without navigating elsewhere.

- Commands handled by a standalone handler class → define the record in the handler file
- Timeout messages consumed by a policy → define the record in the policy file
- Wake-up/trigger commands consumed by a policy → define the record in the policy file

The rule is: **the record lives with the code that has the `Handle` method for it.**

**No silent returns — hard rule:**
- Every early return in a handler MUST either throw or log a warning before returning. Silent failures in event handlers are invisible in production.
- **ID lookups throw:** When looking up an entity by an ID from an event/command, use `GetRequiredByIdAsync(id)` — it throws `EntityNotFoundException` if not found. Extension methods for all repositories are in `Teeforce.Domain.Common.RepositoryExtensions`. Never write `GetByIdAsync(...) ?? throw` inline — always use `GetRequiredByIdAsync`.
- **Query-based lookups log + return:** When searching by criteria (e.g., "find active opening for this course/date"), null means "no match" — a valid business case. Log a warning and return.
- Use `ILogger logger` as a parameter (Wolverine injects it). Use `LogWarning` with `{PropertyName}` placeholders.
- Domain aggregates that are intentionally idempotent (e.g., `Expire()`, `Remove()`, `Reject()`) may use silent returns — but domain methods that should never be called in an invalid state must throw `DomainException` subclasses.

```csharp
// GOOD — handler logs before early return
var opening = await repo.GetByIdAsync(evt.OpeningId);
if (opening is null)
{
    logger.LogWarning("Opening {OpeningId} not found, skipping", evt.OpeningId);
    return;
}

// BAD — silent swallow
if (opening is null) { return; }
```

### Policies (Wolverine Sagas)

Use Wolverine's `Saga` base class for stateful, long-running processes — but call them **policies**, not sagas. "Policy" communicates business intent (e.g., `TeeTimeOfferPolicy` governs how offers are sequenced). Name classes `*Policy` and place them in feature folders using the consumer rule.

**Correlation:** Domain events carry `TeeTimeRequestId`, not `{SagaTypeName}Id`. Use `[SagaIdentityFrom("TeeTimeRequestId")]` from `Wolverine.Persistence.Sagas` on each `Handle` method parameter. `Start` methods set the `Id` directly — no attribute needed.

**Timeout messages need the saga ID as a property:** Wolverine resolves saga identity from message properties, not from the envelope. `TimeoutMessage` records must include the saga ID so Wolverine can load the correct saga instance when the timeout fires. Use a property named `Id` (matches the saga's `Id` property by convention). `[SagaIdentityFrom]` is only needed on **external** messages (domain events) where the property name differs from `Id`.

```csharp
// GOOD — timeout carries saga ID
public record MyTimeout(Guid Id, TimeSpan Delay) : TimeoutMessage(Delay);

// In Start():
return (policy, new MyTimeout(policy.Id, GracePeriod));

// BAD — Wolverine can't correlate this back to the saga
public record MyTimeout(TimeSpan Delay) : TimeoutMessage(Delay);
```

**Persistence:** Map policy types in `ApplicationDbContext` with `IEntityTypeConfiguration`. Ignore the `Version` property from the `Saga` base class. Wolverine handles load/save/delete automatically.

**Cascading messages:** Return commands and timeout messages from `Handle` methods. Wolverine persists them to the outbox as part of the same transaction.

### Event-Driven Choreography

For operations spanning multiple aggregates, use sequential event chains instead of locking multiple aggregates in one transaction:

- Each step is a single-aggregate transaction that raises an event
- The next handler reacts to that event and performs the next step
- Compensation flows handle failures by walking backward (undo previous steps)
- Domain outcomes that represent business failures (e.g., rejection, slot full) are **not exceptions** — they are successful state transitions that raise events
- Pre-allocate IDs (e.g., `BookingId` on `WaitlistOffer`) when downstream steps need correlation
- Wolverine's `MultipleHandlerBehavior.Separated` ensures that if one handler for an event fails, the others still run independently

### Value Objects

- Value objects are defined by their attributes, not by identity — no `Id`, no inheritance from `Entity`
- Immutable: all properties are `get`-only, set through the constructor
- Implement `IEquatable<T>` with `Equals`, `GetHashCode`, and a private parameterless constructor for EF
- Value objects live in `Teeforce.Domain/Common/` (shared) or in an aggregate folder if aggregate-specific
- Map in EF Core using `ComplexProperty` (not `OwnsOne`) — this is the modern EF Core 10 approach
- Use `HasColumnName` inside `ComplexProperty` to control column names (default is `{Nav}_{Prop}`)
- Events carry flat fields, not value objects — events are data contracts across boundaries
- Use value objects for comparisons when they normalize data — e.g., `TeeTime` normalizes seconds to zero in its constructor, so comparing two `TeeTime` instances gives minute-granularity comparison without manual truncation. Prefer `new TeeTime(date, time).Value < new TeeTime(otherDate, otherTime).Value` over hand-rolling `new TimeOnly(hour, minute)`.

```csharp
// Domain: Value object
public class TeeTime : IEquatable<TeeTime>
{
    public DateOnly Date { get; }
    public TimeOnly Time { get; }
    public TeeTime(DateOnly date, TimeOnly time) { Date = date; Time = time; }
    private TeeTime() { } // EF
    // ... Equals, GetHashCode, ToString
}

// EF Config: ComplexProperty mapping
builder.ComplexProperty(o => o.TeeTime, t =>
{
    t.Property(x => x.Date).HasColumnName("Date");
    t.Property(x => x.Time).HasColumnName("TeeTime").HasColumnType("time");
});
```

### Result Objects

- For `internal` cross-aggregate methods, prefer returning result objects over throwing exceptions
- Use `record` types: `record FillResult(bool Success, string? RejectionReason = null)`
- The calling aggregate inspects the result and decides what to do (e.g., reject the offer with the reason)
- Reserve exceptions for true invariant violations that should never happen in correct code

### Domain Exceptions

- `DomainException` subclasses break control flow for true invariant violations
- **Always use `DomainException` subclasses** — never throw `InvalidOperationException`, `ArgumentException`, or any other .NET framework exception from domain code. Every domain invariant violation gets its own exception class in `{Aggregate}/Exceptions/`. This includes factory method guards (e.g., invalid slots → `InvalidSlotsAvailableException`), state guards (e.g., already notified → `OfferAlreadyNotifiedException`), and cross-aggregate lookups (e.g., not found → `EntityNotFoundException`).
- The global exception handler in `Program.cs` maps them to HTTP status codes — new exception types must be added there
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

**Unit tests** (`Teeforce.Api.Tests` — no DB, no HTTP, no container):
- Domain aggregates and services — pure behavior, state transitions, event raising
- FluentValidation validators — call `validator.Validate()` directly
- Wolverine message handlers — call `Handle()` with NSubstitute stubs
- Wolverine policies (sagas) — call `Start()` / `Handle()` directly with constructed events
- Infrastructure utilities (e.g., `PhoneNormalizer`, `CourseTime`)

**Integration tests** (`Teeforce.Api.IntegrationTests` — TestWebApplicationFactory + SQL Server container):
- Flow-based scenario tests (dependent steps telling a user story)
- DB-dependent behavior (unique constraints, query filters, tenant isolation)
- Middleware behavior (tenant claim, course-exists)
- Repository queries that need real SQL Server (eligibility, filtering)
- Smoke tests (health, OpenAPI)

See `.claude/rules/backend/integration-test-conventions.md` for the scenario test pattern.

**Do not** use integration tests to verify validation rules, null guards, or handler branching logic. Those belong in unit tests.

### Test Organization

```
tests/Teeforce.Domain.Tests/
  {Aggregate}Aggregate/              ← domain unit tests

tests/Teeforce.Api.Tests/
  Features/
    Waitlist/
      Handlers/                      ← Wolverine handler unit tests
      Policies/                      ← Wolverine policy unit tests
      Validators/                    ← FluentValidation unit tests
    Courses/
      Validators/
    Tenants/
      Validators/
    TeeSheet/
      Validators/
    Bookings/
      Handlers/
  Services/                          ← utility unit tests

tests/Teeforce.Api.IntegrationTests/
  *Tests.cs                          ← scenario-based integration tests
  TestWebApplicationFactory.cs       ← shared test server + SQL container
  TestSetup.cs                       ← shared setup helpers
  ResponseDtos.cs                    ← shared response records
  StepOrderer.cs                     ← test execution ordering
```

### NSubstitute for Stubs

Use NSubstitute (`Substitute.For<IProvideAServiceInterface>()`) to stub interfaces in handler unit tests. Use real domain objects (aggregates, entities) — don't substitute those, they have behavior worth exercising. Use `Received()` / `DidNotReceive()` to verify side effects like SMS sends or repository writes.

### Read Models (CQRS-lite)

- Domain models serve write operations only — no display/query concerns
- Read models are separate classes optimized for queries
- Read models live in the API layer (e.g., inline in endpoint files or `Infrastructure/ReadModels/`)
- Read models can join across aggregates freely (they don't need to respect aggregate boundaries)
- Use for endpoints that need denormalized/combined data from multiple aggregates

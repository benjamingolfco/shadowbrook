---
paths:
  - "src/backend/Teeforce.Domain/**/*.cs"
  - "tests/Teeforce.Domain.Tests/**/*.cs"
---

# Domain Conventions

Rules for the `Teeforce.Domain` project. The domain has **zero dependencies** — no EF, no ASP.NET, no Wolverine, no logging. Anything that requires those concerns belongs in the API project; see `backend-conventions.md` for handler/endpoint/middleware patterns.

## Core Principles

- Domain models are pure write/behavior — never add properties or methods to serve read/display concerns
- If the domain model makes it hard to query data for display, create a separate read model in the API layer (CQRS-lite)
- All construction goes through static factory methods that enforce invariants — never expose public constructors
- Use `Guid.CreateVersion7()` for all new IDs (time-ordered UUIDs avoid SQL Server index fragmentation)

## Aggregates

- Inherit from `Entity` base class
- All properties use `private set` — no public setters
- Private parameterless constructor for EF (`private MyAggregate() { }`)
- Static factory methods for construction (e.g., `MyAggregate.Create(...)`)
- Guard their own invariants — validate state in domain methods before making changes
- Raise domain events via `AddDomainEvent()` inside state-changing methods

### Aggregate Boundaries

- Each aggregate defines a consistency boundary — one transaction should ideally modify one aggregate
- Reference other aggregates by ID only, not by holding direct navigation properties
- Passing another aggregate as a **method parameter** for validation is acceptable (e.g., `offer.Accept(golfer)`)
- An aggregate can serve as a **factory for another aggregate** — validate creation rules and return a new independent aggregate (e.g., `WalkUpWaitlist.Join()` validates waitlist rules and returns a new `GolferWaitlistEntry` aggregate). The returned aggregate is independent and not owned as a child.
- Never use `InternalsVisibleTo` to expose domain internals for testing or handler consumption. If you need to test or call an `internal` method from outside the domain, rethink the design — either make it part of the public API or restructure so the behavior is exercised through public methods. Tests should always test the public interface.

## Domain Events

- Events are immutable `record` types implementing `IDomainEvent`
- Events carry **identifiers only** — handlers look up the data they need at handling time
- Events carry flat fields, not value objects — events are data contracts across boundaries
- Raise events from inside state-changing methods via `AddDomainEvent()`; never from outside the aggregate
- Domain outcomes that represent business failures (e.g., rejection, slot full) are **not exceptions** — they are successful state transitions that raise events. Reserve exceptions for true invariant violations.

## Value Objects

- Defined by their attributes, not by identity — no `Id`, no inheritance from `Entity`
- Immutable: all properties are `get`-only, set through the constructor
- Implement `IEquatable<T>` with `Equals`, `GetHashCode`, and a private parameterless constructor for EF
- Live in `Teeforce.Domain/Common/` (shared) or in an aggregate folder if aggregate-specific
- Use value objects for comparisons when they normalize data — e.g., `TeeTime` normalizes seconds to zero in its constructor, so comparing two `TeeTime` instances gives minute-granularity comparison without manual truncation. Prefer `new TeeTime(date, time).Value < new TeeTime(otherDate, otherTime).Value` over hand-rolling `new TimeOnly(hour, minute)`.

```csharp
public class TeeTime : IEquatable<TeeTime>
{
    public DateOnly Date { get; }
    public TimeOnly Time { get; }
    public TeeTime(DateOnly date, TimeOnly time) { Date = date; Time = time; }
    private TeeTime() { } // EF
    // ... Equals, GetHashCode, ToString
}
```

## Capability Tokens

When an invariant spans aggregates — "you can only do X to aggregate B if aggregate A is in state Y" — encode the check as a **capability token** value object that the source aggregate mints and the target aggregate requires.

**Shape:**
- The token is a value object with an `internal` constructor — only the issuing aggregate (same assembly, same namespace folder) can mint it
- The source aggregate exposes an `Authorize{Action}()` method that validates its state and returns the token, or throws a `DomainException` subclass if the state is wrong
- The target aggregate's factory/method takes the token as a parameter and validates it correlates back to the right source (e.g., `auth.TeeSheetId == interval.TeeSheetId`)
- The token is **proof-of-check, not a reservation** — minting it does NOT mutate the source aggregate, raises no events, and creates no obligation to follow through

**Why:** This pushes the invariant from "convention every caller must remember" into the type system. A handler cannot call `TeeTime.Claim(...)` without first calling `TeeSheet.AuthorizeBooking()`, because there's no other way to obtain a `BookingAuthorization` instance. The check becomes structurally unskippable rather than relying on code review.

**Example:**

```csharp
// In TeeSheetAggregate/BookingAuthorization.cs
public sealed class BookingAuthorization
{
    public Guid TeeSheetId { get; }
    internal BookingAuthorization(Guid teeSheetId) { TeeSheetId = teeSheetId; }
    private BookingAuthorization() { } // EF (if ever persisted; usually transient)
}

// On TeeSheet
public BookingAuthorization AuthorizeBooking()
{
    if (Status != TeeSheetStatus.Published)
    {
        throw new TeeSheetNotPublishedException(Id);
    }
    return new BookingAuthorization(Id);
}

// On TeeTime
public static TeeTime Claim(
    TeeSheetInterval interval,
    BookingAuthorization auth,   // proof the sheet is claimable
    Guid bookingId,
    /* ... */)
{
    if (auth.TeeSheetId != interval.TeeSheetId)
    {
        throw new BookingAuthorizationMismatchException(auth.TeeSheetId, interval.TeeSheetId);
    }
    // ... safe to proceed
}
```

**When to reach for this pattern:**
- The invariant cannot live inside a single aggregate (it requires reading state from another aggregate)
- You want a compile-time signal — a parameter type — rather than a runtime check that the handler must remember to perform
- The check is read-only on the source side; if minting the token must reserve or mutate state, model it as a real domain operation that raises an event instead

## Result Objects

- For `internal` cross-aggregate methods, prefer returning result objects over throwing exceptions
- Use `record` types: `record FillResult(bool Success, string? RejectionReason = null)`
- The calling aggregate inspects the result and decides what to do (e.g., reject the offer with the reason)
- Reserve exceptions for true invariant violations that should never happen in correct code

## Domain Exceptions

- `DomainException` subclasses break control flow for true invariant violations
- **Always use `DomainException` subclasses** — never throw `InvalidOperationException`, `ArgumentException`, or any other .NET framework exception from domain code. Every domain invariant violation gets its own exception class in `{Aggregate}/Exceptions/`. This includes factory method guards (e.g., invalid slots → `InvalidSlotsAvailableException`), state guards (e.g., already notified → `OfferAlreadyNotifiedException`), and cross-aggregate lookups (e.g., not found → `EntityNotFoundException`).
- The global exception handler in `Program.cs` maps them to HTTP status codes — new exception types must be added there

## Repository Interfaces

- Repository interfaces are defined in the domain (one per aggregate root), implementations live in `Teeforce.Api/Infrastructure/Repositories/`
- Methods return fully loaded aggregates (the implementation handles `.Include()` of child collections)

## Domain Service Interfaces

- Domain service interfaces are defined in the domain (e.g., `IShortCodeGenerator`), implementations live in `Teeforce.Api/Infrastructure/Services/`
- Use for cross-aggregate logic that doesn't belong to any single aggregate

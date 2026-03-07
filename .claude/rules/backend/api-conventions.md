---
paths:
  - "src/backend/Shadowbrook.Api/**/*.cs"
---

# Backend API Conventions

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

- Domain model lives in `Shadowbrook.Domain` (zero dependencies — no EF, no ASP.NET)
- Aggregates guard their own invariants and raise domain events via `AddDomainEvent()`
- Repository interfaces defined in domain, implemented in `Infrastructure/Repositories/`
- Domain service interfaces defined in domain (e.g., `IShortCodeGenerator`), implemented in `Infrastructure/Services/`
- Domain exceptions (`DomainException` subclasses) break control flow; the global exception handler in `Program.cs` maps them to HTTP status codes — do NOT catch `DomainException` in endpoints, let them propagate
- EF entity type configurations in `Infrastructure/EntityTypeConfigurations/`
- Domain events dispatched automatically via `ApplicationDbContext.SaveChangesAsync()` override

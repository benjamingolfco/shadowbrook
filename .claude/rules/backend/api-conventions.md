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

## Domain-Driven Design

- Domain model lives in `Shadowbrook.Domain` (zero dependencies — no EF, no ASP.NET)
- Aggregates guard their own invariants and raise domain events via `AddDomainEvent()`
- Repository interfaces defined in domain, implemented in `Infrastructure/Repositories/`
- Domain service interfaces defined in domain (e.g., `IShortCodeGenerator`), implemented in `Infrastructure/Services/`
- Domain exceptions (`DomainException` subclasses) break control flow; endpoints catch specific exceptions and map to HTTP status codes
- EF entity type configurations in `Infrastructure/EntityTypeConfigurations/`
- Domain events dispatched automatically via `ApplicationDbContext.SaveChangesAsync()` override

# Backend Developer Memory

## Project Structure

- Domain: `src/backend/Shadowbrook.Domain/`
- Domain Tests: `tests/Shadowbrook.Domain.Tests/`
- API: `src/backend/Shadowbrook.Api/` (references Domain)
- API Tests: `tests/Shadowbrook.Api.Tests/`
- Solution file: `shadowbrook.slnx` at repo root

## Key Commands

- Build: `dotnet build shadowbrook.slnx`
- Test (all): `dotnet test tests/Shadowbrook.Domain.Tests && dotnet test tests/Shadowbrook.Api.Tests`
- Test (targeted API): `dotnet test tests/Shadowbrook.Api.Tests/ --filter "FullyQualifiedName~{TestClass}"`
- Add migration: `export PATH="$PATH:/home/aaron/.dotnet/tools" && dotnet ef migrations add <Name> --project src/backend/Shadowbrook.Api`
- Check pending: `dotnet ef migrations has-pending-model-changes --project src/backend/Shadowbrook.Api`

## Key Files

- `src/backend/Shadowbrook.Api/Program.cs` — app bootstrap, DI registration, Wolverine config, Serilog + OTel setup
- `src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs` — EF context with CurrentTenantId property for query filter
- `src/backend/Shadowbrook.Api/Features/` — Wolverine HTTP endpoints and handlers (feature-based folders)
- `src/backend/Shadowbrook.Api/Infrastructure/Middleware/CourseExistsMiddleware.cs` — Wolverine Before middleware for course existence check
- `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/` — IEntityTypeConfiguration classes for domain entities
- `src/backend/Shadowbrook.Api/Infrastructure/Observability/TenantIdEnricher.cs` — Serilog enricher that adds TenantId from tenant_id claim
- `tests/Shadowbrook.Api.Tests/TestWebApplicationFactory.cs` — Testcontainers SQL Server test host

## Domain Project Conventions

- Base types in `Shadowbrook.Domain/Common/`: `Entity`, `IDomainEvent`, `DomainException`
- Domain aggregates in feature folders: e.g., `Shadowbrook.Domain/WalkUpWaitlist/`
- Exceptions derive from `DomainException` and live in `{Feature}/Exceptions/`
- Domain events implement `IDomainEvent` and live in `{Feature}/Events/`
- Enums live directly in the feature folder
- Interfaces (e.g., `IShortCodeGenerator`) live directly in the feature folder
- Domain test project is empty by default — add unit tests for aggregate behavior
- Domain entities use factory methods (e.g., `Course.Create(...)`, `Tenant.Create(...)`) — do NOT use object initializers
- Domain entities use private setters; mutation via domain methods

## Domain Entities

- `Shadowbrook.Domain.CourseAggregate.Course` — factory: `Course.Create(tenantId, name, timeZoneId, ...)`; mutation: `UpdateTeeTimeSettings(...)`, `UpdatePricing(...)`
- `Shadowbrook.Domain.TenantAggregate.Tenant` — factory: `Tenant.Create(orgName, contactName, contactEmail, contactPhone)`
- Both have `ICourseRepository` / `ITenantRepository` with `GetByIdAsync`, `GetByTenantIdAsync`/`GetAllAsync`, `ExistsByNameAsync`, `Add`

## Patterns

- Endpoints: Wolverine HTTP `[WolverineGet]`/`[WolverinePost]` static methods in `Features/` folders, discovered by convention
- DTOs: inline records within endpoint files
- Multi-tenancy: `ICurrentUser.TenantId` injected via `TenantClaimMiddleware` (reads `X-Tenant-Id` header → adds `tenant_id` claim)
- EF query filter on Course: defined in `ApplicationDbContext.OnModelCreating` as `c => CurrentTenantId == null || c.TenantId == CurrentTenantId` — BUT this filter does NOT reliably enforce isolation per request (see Known Issues). Read/write course endpoints explicitly inject `ICurrentUser` and apply `IgnoreQueryFilters()` + manual `WHERE` clause.
- Domain events: Wolverine `IMessageBus` + `PublishDomainEventsFromEntityFrameworkCore` (auto-scraped on save)
- Transactional middleware: `UseEntityFrameworkCoreTransactions()` + `AutoApplyTransactions()` — do NOT call `SaveChangesAsync()` in endpoints or handlers, EXCEPT for intentional mid-flow saves (position queries that need the new row flushed before reading)
- Return types: `Results.Ok()`, `Results.BadRequest()`, `Results.NotFound()` — `IResult` return type
- Validation: FluentValidation via `WolverineFx.Http.FluentValidation` (automatic for HTTP endpoints)
- Cross-cutting: Wolverine `Before` middleware via policy (e.g., `CourseExistsMiddleware` in `Infrastructure/Middleware/`)
- Non-body parameters in Wolverine POST/PUT endpoints require `[NotBody]` attribute (repositories, services, currentUser)
- Shadow properties (`UpdatedAt`, `UpdatedBy`, `RowVersion`) set in `SaveChangesAsync` — NOT on domain entity classes; use `HasShadowAuditProperties()` and `HasShadowRowVersion()` extension methods in entity configurations
- Response DTOs do NOT include `UpdatedAt` (shadow-only)

## Observability Stack

- Serilog: `Serilog.AspNetCore` 9.0.0, `Serilog.Sinks.Console` 6.0.0, `Serilog.Enrichers.Environment` 3.0.1
- OTel: `OpenTelemetry.Instrumentation.AspNetCore` 1.14.0, `OpenTelemetry.Instrumentation.Http` 1.14.0, `OpenTelemetry.Instrumentation.EntityFrameworkCore` 1.15.0-beta.1 (no stable release exists), `Azure.Monitor.OpenTelemetry.AspNetCore` 1.4.0
- Wolverine OTel: `WolverineFx.OpenTelemetry` package does NOT exist — use `AddSource("Wolverine")` in tracing builder (activity source is in core WolverineFx). No `opts.OpenTelemetryInstrumentation()` method in 5.20.1.
- Azure Monitor is conditional on `APPLICATIONINSIGHTS_CONNECTION_STRING` env var being set
- OTel extension methods require explicit usings: `OpenTelemetry.Trace`, `OpenTelemetry.Metrics`, `Azure.Monitor.OpenTelemetry.AspNetCore`
- `EntityFrameworkInstrumentationOptions` in 1.15.0-beta.1 only has `EnrichWithIDbCommand` and `Filter` — no statement capture options

## Test Conventions

- Integration tests use Testcontainers SQL Server via `TestWebApplicationFactory`
- Each test class calls `factory.ResetDatabaseAsync()` in `InitializeAsync` via Respawn
- Full Wolverine pipeline active in tests (transactional middleware, domain event scraping)
- Handler unit tests in `tests/Shadowbrook.Api.Tests/Handlers/` — use NSubstitute, no factory, call `Handle()` directly
- Validator unit tests in `tests/Shadowbrook.Api.Tests/Validators/` — no factory, call `Validate()` directly
- 223 passing API tests + 49 domain tests as of 2026-03-23 (PR #245)
- 11 WaitlistOfferEndpointsTests known-skipped: async domain event chain tests (Wolverine background handlers) need work
- Domain test folder structure mirrors domain: `Shadowbrook.Domain.Tests/WalkUpWaitlist/`

## Known Issues

- **EF Core query filter not enforcing tenant isolation reliably**: The `HasQueryFilter` on `Course` in `ApplicationDbContext.OnModelCreating` uses `CurrentTenantId` (snapshotted at DbContext construction). This should work in theory (EF Core re-evaluates `this.Property` per DbContext instance) but in practice does not enforce isolation. Root cause unclear — may be EF Core model caching with primary constructor syntax, or DbContext construction timing relative to middleware. Course endpoints work around this by using `IgnoreQueryFilters()` + explicit `WHERE tenantId == null || c.TenantId == tenantId` with injected `ICurrentUser`.
- `CourseExistsMiddleware` still uses `db.Courses.AnyAsync(c => c.Id == courseId)` which depends on the query filter — tenant isolation for middleware is unverified.
- `WaitlistOfferEndpointsTests` (11 tests): Skipped — depend on Wolverine processing `TeeTimeRequestAdded` domain event asynchronously after HTTP response.

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

- `src/backend/Shadowbrook.Api/Program.cs` — app bootstrap, DI registration, Wolverine config
- `src/backend/Shadowbrook.Api/Data/ApplicationDbContext.cs` — EF context with global query filters for multi-tenancy
- `src/backend/Shadowbrook.Api/Features/` — Wolverine HTTP endpoints and handlers (feature-based folders)
- `src/backend/Shadowbrook.Api/Infrastructure/Middleware/CourseExistsMiddleware.cs` — Wolverine Before middleware for course existence check
- `tests/Shadowbrook.Api.Tests/TestWebApplicationFactory.cs` — Testcontainers SQL Server test host

## Domain Project Conventions

- Base types in `Shadowbrook.Domain/Common/`: `Entity`, `IDomainEvent`, `DomainException`
- Domain aggregates in feature folders: e.g., `Shadowbrook.Domain/WalkUpWaitlist/`
- Exceptions derive from `DomainException` and live in `{Feature}/Exceptions/`
- Domain events implement `IDomainEvent` and live in `{Feature}/Events/`
- Enums live directly in the feature folder
- Interfaces (e.g., `IShortCodeGenerator`) live directly in the feature folder
- Domain test project is empty by default — add unit tests for aggregate behavior

## Patterns

- Endpoints: Wolverine HTTP `[WolverineGet]`/`[WolverinePost]` static methods in `Features/` folders, discovered by convention
- DTOs: inline records within endpoint files
- Multi-tenancy: `ICurrentUser.TenantId` injected via `TenantClaimMiddleware`, applied via EF global query filters
- Domain events: Wolverine `IMessageBus` + `PublishDomainEventsFromEntityFrameworkCore` (auto-scraped on save)
- Transactional middleware: `UseEntityFrameworkCoreTransactions()` + `AutoApplyTransactions()` — do NOT call `SaveChangesAsync()` in endpoints or handlers, EXCEPT for intentional mid-flow saves (position queries that need the new row flushed before reading)
- Return types: `Results.Ok()`, `Results.BadRequest()`, `Results.NotFound()` — `IResult` return type
- Validation: FluentValidation via `WolverineFx.Http.FluentValidation` (automatic for HTTP endpoints)
- Cross-cutting: Wolverine `Before` middleware via policy (e.g., `CourseExistsMiddleware` in `Infrastructure/Middleware/`)

## Test Conventions

- Integration tests use Testcontainers SQL Server via `TestWebApplicationFactory`
- Each test class gets its own database via unique name (`test_{Guid:N}[..30]`) in a shared container
- Full Wolverine pipeline active in tests (transactional middleware, domain event scraping)
- Handler unit tests in `tests/Shadowbrook.Api.Tests/Handlers/` — use NSubstitute, no factory, call `Handle()` directly
- Validator unit tests in `tests/Shadowbrook.Api.Tests/Validators/` — no factory, call `Validate()` directly
- 200 passing API tests + 39 domain tests as of 2026-03-19
- 13 WaitlistOfferEndpointsTests known-failing: async domain event chain tests (Wolverine background handlers) need work
- Domain test folder structure mirrors domain: `Shadowbrook.Domain.Tests/WalkUpWaitlist/`

## Known Issues on chore/wolverine-http-migration branch

- `WaitlistOfferEndpointsTests` (13 tests): Depend on Wolverine processing `TeeTimeRequestAdded` domain event asynchronously after HTTP response. Tests assert state immediately after the HTTP call — the background handler hasn't run yet. Fix requires either polling/await or a test helper that waits for Wolverine to process messages.

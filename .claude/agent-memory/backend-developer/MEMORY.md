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

- `src/backend/Shadowbrook.Api/Program.cs` — app bootstrap, DI registration, middleware
- `src/backend/Shadowbrook.Api/Data/ApplicationDbContext.cs` — EF context with global query filters for multi-tenancy
- `src/backend/Shadowbrook.Api/Endpoints/` — minimal API endpoint groups (extension method pattern)
- `tests/Shadowbrook.Api.Tests/TestWebApplicationFactory.cs` — SQLite in-memory test host

## Domain Project Conventions

- Base types in `Shadowbrook.Domain/Common/`: `Entity`, `IDomainEvent`, `DomainException`
- Domain aggregates in feature folders: e.g., `Shadowbrook.Domain/WalkUpWaitlist/`
- Exceptions derive from `DomainException` and live in `{Feature}/Exceptions/`
- Domain events implement `IDomainEvent` and live in `{Feature}/Events/`
- Enums live directly in the feature folder
- Interfaces (e.g., `IShortCodeGenerator`) live directly in the feature folder
- Domain test project is empty by default — add unit tests for aggregate behavior

## Patterns

- Endpoints: `MapXxxEndpoints()` extension methods registered in `Program.cs`
- DTOs: inline records within endpoint files
- Multi-tenancy: `ICurrentUser.TenantId` injected via `TenantClaimMiddleware`, applied via EF global query filters
- Domain events: `IDomainEventPublisher` with in-process synchronous handlers
- Return types: `Results.Ok()`, `Results.BadRequest()`, `Results.NotFound()` — never return raw objects
- Webhook endpoints (e.g., `/webhooks/sms/inbound`) registered outside the tenant-scoped `api` group — no query filters, no `ICurrentUser`
- `TimeProvider` registered as `AddSingleton(TimeProvider.System)` — inject `FakeTimeProvider` in tests for expiry/time-sensitive behavior
- EF models that raise domain events extend `Entity` (e.g., `WaitlistOffer`) — simple data models that don't raise events don't need to extend `Entity`
- Static endpoint classes cannot be used as `ILogger<T>` type parameter — use `ILoggerFactory` + `CreateLogger("name")` instead

## Test Conventions

- Integration tests use SQLite in-memory via `TestWebApplicationFactory`
- Tests use `EnsureCreated()` (not migrations) — validates schema independent of migration history
- 154 API tests + 13 domain tests as of 2026-03-11
- Domain test folder structure mirrors domain: `Shadowbrook.Domain.Tests/WalkUpWaitlist/`
- WaitlistOffer model tests live in the API test project (not domain) — model is in the API layer
- Use `factory.Services.GetRequiredService<InMemoryTextMessageService>()` to inspect SMS in tests — do NOT call `/dev/sms` endpoints (not registered in Testing environment)
- Inject `FakeTimeProvider` (from `Microsoft.Extensions.TimeProvider.Testing`) via `factory.WithWebHostBuilder` for time-sensitive tests
- SQLite does not support `DateTimeOffset` in EF ORDER BY — always sort in memory after `.ToListAsync()`

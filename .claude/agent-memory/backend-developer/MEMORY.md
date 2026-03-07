# Backend Developer Memory

## Project Structure

- Domain: `src/backend/Shadowbrook.Domain/`
- Domain Tests: `src/backend/Shadowbrook.Domain.Tests/`
- API: `src/backend/Shadowbrook.Api/` (references Domain)
- API Tests: `src/backend/Shadowbrook.Api.Tests/`
- Solution file: `shadowbrook.slnx` at repo root

## Key Commands

- Build: `dotnet build shadowbrook.slnx`
- Test (all): `dotnet test src/backend/Shadowbrook.Domain.Tests && dotnet test src/backend/Shadowbrook.Api.Tests`
- Test (targeted API): `dotnet test src/backend/Shadowbrook.Api.Tests/ --filter "FullyQualifiedName~{TestClass}"`
- Add migration: `export PATH="$PATH:/home/aaron/.dotnet/tools" && dotnet ef migrations add <Name> --project src/backend/Shadowbrook.Api`
- Check pending: `dotnet ef migrations has-pending-model-changes --project src/backend/Shadowbrook.Api`

## Key Files

- `src/backend/Shadowbrook.Api/Program.cs` — app bootstrap, DI registration, middleware
- `src/backend/Shadowbrook.Api/Data/ApplicationDbContext.cs` — EF context with global query filters for multi-tenancy
- `src/backend/Shadowbrook.Api/Endpoints/` — minimal API endpoint groups (extension method pattern)
- `src/backend/Shadowbrook.Api.Tests/TestWebApplicationFactory.cs` — SQLite in-memory test host

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

## Test Conventions

- Integration tests use SQLite in-memory via `TestWebApplicationFactory`
- Tests use `EnsureCreated()` (not migrations) — validates schema independent of migration history
- 85 API tests + 8 domain tests (1 TeeTimeRequest + 7 WalkUpWaitlist) as of 2026-03-06
- Domain test folder structure mirrors domain: `Shadowbrook.Domain.Tests/WalkUpWaitlist/`

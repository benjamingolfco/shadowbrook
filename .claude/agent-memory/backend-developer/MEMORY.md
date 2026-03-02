# Backend Developer Agent Memory

## Project Structure
- API: `src/api/` — .NET 10 minimal API, `Microsoft.NET.Sdk.Web`
- Tests: `tests/api/` — xUnit + `WebApplicationFactory<Program>`, `Microsoft.NET.Sdk` (NOT Web SDK)
- Build: `dotnet build shadowbrook.slnx`
- Run tests: `dotnet test tests/api/ --filter "FullyQualifiedName~{TestClass}"`

## Key Patterns

### Endpoint convention
- Endpoints live in `src/api/Endpoints/`, one file per domain area
- Each file has a static class with `Map{Domain}Endpoints(this WebApplication app)` extension
- Called in `Program.cs` after middleware setup

### WebApplicationFactory config overrides
- **Use `builder.UseSetting(key, value)` in `ConfigureWebHost`** to override config values read at Program.cs startup
- `ConfigureAppConfiguration` does NOT work for values read as top-level variables during `WebApplication.CreateBuilder()` — those are captured before `ConfigureWebHost` hooks run
- `UseSetting` updates the host configuration which IS available before `GetValue()` calls in Program.cs

### Rate limiting (see `src/api/Program.cs`)
- Uses built-in `Microsoft.AspNetCore.RateLimiting` (no extra NuGet package needed)
- Policy name is a `const` on the endpoint class to keep registration/endpoint in sync
- `UseRateLimiter()` middleware placed after `UseCors()`, before custom middleware
- `RequireRateLimiting(policyName)` on individual endpoint route registrations
- Config-driven limits (`RateLimiting:WalkUpVerify:PermitLimit`, `WindowSeconds`) for testability

### Rate limiting tests
- Test project uses `Microsoft.NET.Sdk` — `AddRateLimiter` extension is NOT available there
- Use `TightRateLimitFactory` with `UseSetting` to inject tight limit (e.g., 2) instead
- Each test sends a unique `X-Forwarded-For` header to isolate rate limit buckets per test
- Rate limiter prefers `X-Forwarded-For` over `RemoteIpAddress` (null in test server)

### SQLite in tests
- `TestWebApplicationFactory` replaces SQL Server with SQLite in-memory
- Share a single open `SqliteConnection` per factory so the in-memory DB persists across scopes
- `db.Database.EnsureCreated()` in `CreateHost` override

## Entity Notes
- `WalkUpCode` — 4-digit per-course per-day code, `IsActive` flag
- `CourseWaitlist` — per-course per-day waitlist, created lazily on first verify
- `GolferWaitlistEntry` — per golfer per waitlist, soft-deleted via `RemovedAt`
- `Golfer` — found-or-created by normalized phone (E.164 via `PhoneNormalizer`)

## Debugging Notes
- See `debugging.md` for detailed notes on config override and rate limit test isolation

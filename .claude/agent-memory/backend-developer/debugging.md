# Debugging Notes

## WebApplicationFactory Config Overrides for Minimal APIs

**Problem:** `builder.ConfigureAppConfiguration()` in `ConfigureWebHost` does NOT override
values that are read as top-level variables in `Program.cs` via `builder.Configuration.GetValue()`.

**Why:** `WebApplication.CreateBuilder()` initializes its own `ConfigurationManager`. The
`ConfigureWebHost` callback is applied to the `GenericWebHostBuilder`, and `ConfigureAppConfiguration`
adds providers to the chain — but those providers are appended AFTER the initial builder config.
When `Program.cs` calls `GetValue()` at the top level (before `builder.Build()`), the in-memory
providers from `ConfigureAppConfiguration` haven't been applied yet.

**Fix:** Use `builder.UseSetting(key, value)` instead. This updates the underlying host
configuration before Program.cs reads values. Example in `TightRateLimitFactory`:

```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.UseSetting("RateLimiting:WalkUpVerify:PermitLimit", "2");
    builder.UseSetting("RateLimiting:WalkUpVerify:WindowSeconds", "300");
    // ...
}
```

## Rate Limiting Test Isolation

**Problem:** `IClassFixture<TFactory>` shares a single factory (and thus the rate limiter's
in-memory state) across all tests in a class. If all requests use the same partition key
(`"unknown"` when `RemoteIpAddress` is null), tests deplete each other's buckets.

**Fix:** Add a unique `X-Forwarded-For` header per test so each test gets its own rate limit bucket:

```csharp
var client = _factory.CreateClient();
client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1"); // unique per test
```

The rate limiter must be configured to prefer `X-Forwarded-For` over `RemoteIpAddress`:

```csharp
var ip = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
         ?? httpContext.Connection.RemoteIpAddress?.ToString()
         ?? "unknown";
```

## ASP.NET Core Rate Limiting in Test Projects

**Problem:** `AddRateLimiter` (from `Microsoft.AspNetCore.RateLimiting`) is not available
in test projects that use `Microsoft.NET.Sdk` (non-web SDK).

**Workaround:** Don't call `AddRateLimiter` in the test project. Instead:
1. Make `PermitLimit` config-driven in `Program.cs` via `GetValue()`
2. Override config in test factories via `UseSetting()`
3. The tight limit applies without needing to reference rate limiting APIs in the test project

# Plan: Make SeedAdminEmails Safe for Production

## Problem

`AppUserEnrichmentMiddleware` reads `Auth:SeedAdminEmails` from config and auto-promotes matching emails to `AppUserRole.Admin` on first login. This is useful for dev bootstrapping but dangerous in production -- if the config key leaks or is accidentally set, any matching email gets admin access.

## Approach

**Guard with `IHostEnvironment.IsDevelopment()`** -- the simplest, most explicit approach. The codebase already uses `!app.Environment.IsProduction()` guards in `Program.cs` for Swagger, migrations, etc. Inject `IHostEnvironment` into the middleware and only read `SeedAdminEmails` when `env.IsDevelopment()` is true. In all other environments, the seed list is always empty.

This is preferred over a config toggle (`Auth:EnableSeedAdmins`) because a config toggle can be accidentally set in production, defeating the purpose.

## Changes

### Modify: `src/backend/Teeforce.Api/Infrastructure/Auth/AppUserEnrichmentMiddleware.cs`

1. Add `IHostEnvironment env` parameter to `InvokeAsync`
2. Only read `Auth:SeedAdminEmails` when `env.IsDevelopment()` -- otherwise pass an empty array
3. Log a warning (once per email) when auto-promotion happens:
   ```
   logger.LogWarning("Auto-promoting user {Email} to Admin via SeedAdminEmails (dev only)", email);
   ```

Pseudocode for the guard:

```csharp
var seedAdminEmails = env.IsDevelopment()
    ? configuration.GetValue<string>("Auth:SeedAdminEmails")
        ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? []
    : [];
```

### Add/update tests: `tests/Teeforce.Api.Tests/Auth/AppUserEnrichmentMiddlewareTests.cs`

Add `IHostEnvironment` parameter to all existing `InvokeAsync` calls (create a simple stub that returns `Development`). Then add two new tests:

1. **SeedAdminEmail_InDevelopment_AutoPromotesToAdmin** -- new user with a seed email gets `AppUserRole.Admin`
2. **SeedAdminEmail_InProduction_AutoProvisionedAsStaff** -- same email, but `IHostEnvironment.EnvironmentName = "Production"` -- user gets `Staff`, not `Admin`

## Dev Tasks

### Backend Developer

- [ ] Add `IHostEnvironment` to `InvokeAsync` and guard `SeedAdminEmails` behind `IsDevelopment()`
- [ ] Add `LogWarning` when auto-promotion occurs
- [ ] Update existing test helper to pass `IHostEnvironment` (Development by default)
- [ ] Add test: seed admin email in Development promotes to Admin
- [ ] Add test: seed admin email in Production is provisioned as Staff

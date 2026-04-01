# Implementation Plan: Auth File Reorganization (#240)

## Approach

Move all auth infrastructure files from `Shadowbrook.Api/Auth/` to `Shadowbrook.Api/Infrastructure/Auth/`, updating the namespace from `Shadowbrook.Api.Auth` to `Shadowbrook.Api.Infrastructure.Auth`. Extract auth service registration from `Program.cs` into a new `AddShadowbrookAuth()` extension method in `AuthServiceCollectionExtensions.cs`. The auth feature endpoint (`Features/Auth/AuthEndpoints.cs`) stays where it is.

## Step 1: Move Files and Update Namespaces

### 1a. Create the destination directory

Create `src/backend/Shadowbrook.Api/Infrastructure/Auth/`.

### 1b. Move these 9 files

Move each file from `Auth/` to `Infrastructure/Auth/` and change the namespace declaration from `Shadowbrook.Api.Auth` to `Shadowbrook.Api.Infrastructure.Auth`:

| File | Namespace change |
|------|-----------------|
| `AppUserEnrichmentMiddleware.cs` | `Shadowbrook.Api.Auth` -> `Shadowbrook.Api.Infrastructure.Auth` |
| `CourseAccessAuthorizationHandler.cs` | `Shadowbrook.Api.Auth` -> `Shadowbrook.Api.Infrastructure.Auth` |
| `CourseAccessRequirement.cs` | `Shadowbrook.Api.Auth` -> `Shadowbrook.Api.Infrastructure.Auth` |
| `CurrentUser.cs` | `Shadowbrook.Api.Auth` -> `Shadowbrook.Api.Infrastructure.Auth` |
| `DevAuthHandler.cs` | `Shadowbrook.Api.Auth` -> `Shadowbrook.Api.Infrastructure.Auth` |
| `ICurrentUser.cs` | `Shadowbrook.Api.Auth` -> `Shadowbrook.Api.Infrastructure.Auth` |
| `PermissionAuthorizationHandler.cs` | `Shadowbrook.Api.Auth` -> `Shadowbrook.Api.Infrastructure.Auth` |
| `PermissionRequirement.cs` | `Shadowbrook.Api.Auth` -> `Shadowbrook.Api.Infrastructure.Auth` |
| `Permissions.cs` | `Shadowbrook.Api.Auth` -> `Shadowbrook.Api.Infrastructure.Auth` |

No internal `using` changes are needed within these files since they all share the same namespace, and their non-auth usings remain valid.

### 1c. Delete the old `Auth/` directory

After moving all files, delete `src/backend/Shadowbrook.Api/Auth/` (it should be empty).

## Step 2: Create `AuthServiceCollectionExtensions.cs`

Create `src/backend/Shadowbrook.Api/Infrastructure/Auth/AuthServiceCollectionExtensions.cs` with the following content:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace Shadowbrook.Api.Infrastructure.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddShadowbrookAuth(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Authentication
        var useDevAuth = configuration.GetValue<bool>("Auth:UseDevAuth");
        if (useDevAuth)
        {
            services.AddAuthentication("DevAuth")
                .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>("DevAuth", null);
        }
        else
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));
        }

        // Authorization policies
        services.AddAuthorizationBuilder()
            .AddPolicy("RequireAppAccess", policy =>
                policy.AddRequirements(new PermissionRequirement(Permissions.AppAccess)))
            .AddPolicy("RequireUsersManage", policy =>
                policy.AddRequirements(new PermissionRequirement(Permissions.UsersManage)))
            .AddPolicy("RequireCourseAccess", policy =>
                policy.AddRequirements(new CourseAccessRequirement()));

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, CourseAccessAuthorizationHandler>();

        return services;
    }
}
```

## Step 3: Update `Program.cs`

### 3a. Replace the `using` statement

Change:
```csharp
using Shadowbrook.Api.Auth;
```
to:
```csharp
using Shadowbrook.Api.Infrastructure.Auth;
```

### 3b. Remove the inline auth registration block

Remove the following lines (currently around lines 143-166):

```csharp
// Authentication
var useDevAuth = builder.Configuration.GetValue<bool>("Auth:UseDevAuth");
if (useDevAuth)
{
    builder.Services.AddAuthentication("DevAuth")
        .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>("DevAuth", null);
}
else
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
}

// Authorization
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAppAccess", policy =>
        policy.AddRequirements(new PermissionRequirement(Permissions.AppAccess)))
    .AddPolicy("RequireUsersManage", policy =>
        policy.AddRequirements(new PermissionRequirement(Permissions.UsersManage)))
    .AddPolicy("RequireCourseAccess", policy =>
        policy.AddRequirements(new CourseAccessRequirement()));

builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, CourseAccessAuthorizationHandler>();
```

### 3c. Insert the extension method call

In place of the removed block, add:

```csharp
builder.Services.AddShadowbrookAuth(builder.Configuration);
```

### 3d. Remove unused `using` statements from `Program.cs`

After the extraction, these `using` statements are no longer needed in `Program.cs` (they are consumed only by the auth registration code and are now internal to the extension method):

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
```

**Verify before removing:** Check that nothing else in `Program.cs` references types from these namespaces. Currently:
- `Microsoft.AspNetCore.Authentication` -- only used by `AddScheme<AuthenticationSchemeOptions, ...>` (moved)
- `Microsoft.AspNetCore.Authentication.JwtBearer` -- only used by `JwtBearerDefaults` (moved)
- `Microsoft.AspNetCore.Authorization` -- only used by `IAuthorizationHandler` (moved)
- `Microsoft.Identity.Web` -- only used by `AddMicrosoftIdentityWebApi` (moved)

All four are safe to remove.

### 3e. Keep the `ICurrentUser` registration in `Program.cs`

This line stays in `Program.cs` because it registers the DI abstraction, which is a general service registration concern alongside the other `AddScoped` calls:

```csharp
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
```

However, since `ICurrentUser` and `CurrentUser` have moved namespaces, the `using Shadowbrook.Api.Infrastructure.Auth;` added in step 3a covers this.

## Step 4: Update `using` Statements Across the Codebase

The following files reference `Shadowbrook.Api.Auth` and need the `using` updated to `Shadowbrook.Api.Infrastructure.Auth`:

### API project (src/backend/Shadowbrook.Api/)

| File | Current using | New using |
|------|--------------|-----------|
| `Program.cs` | `using Shadowbrook.Api.Auth;` | `using Shadowbrook.Api.Infrastructure.Auth;` |
| `Infrastructure/Data/ApplicationDbContext.cs` | `using Shadowbrook.Api.Auth;` | `using Shadowbrook.Api.Infrastructure.Auth;` |
| `Features/Courses/CourseEndpoints.cs` | `using Shadowbrook.Api.Auth;` | `using Shadowbrook.Api.Infrastructure.Auth;` |
| `Features/Auth/AuthEndpoints.cs` | `using Shadowbrook.Api.Auth;` | `using Shadowbrook.Api.Infrastructure.Auth;` |
| `Features/FeatureFlags/FeatureEndpoints.cs` | `using Shadowbrook.Api.Auth;` | `using Shadowbrook.Api.Infrastructure.Auth;` |

### Test project (tests/Shadowbrook.Api.Tests/)

| File | Current using | New using |
|------|--------------|-----------|
| `Auth/AppUserEnrichmentMiddlewareTests.cs` | `using Shadowbrook.Api.Auth;` | `using Shadowbrook.Api.Infrastructure.Auth;` |
| `Auth/PermissionsTests.cs` | `using Shadowbrook.Api.Auth;` | `using Shadowbrook.Api.Infrastructure.Auth;` |
| `Auth/CourseAccessAuthorizationHandlerTests.cs` | `using Shadowbrook.Api.Auth;` | `using Shadowbrook.Api.Infrastructure.Auth;` |
| `Auth/PermissionAuthorizationHandlerTests.cs` | `using Shadowbrook.Api.Auth;` | `using Shadowbrook.Api.Infrastructure.Auth;` |

## Step 5: Verify

Run these commands from the repo root in order:

```bash
# 1. Build to verify compilation
dotnet build shadowbrook.slnx

# 2. Fix any style warnings
dotnet format shadowbrook.slnx

# 3. Run auth-specific tests
dotnet test tests/Shadowbrook.Api.Tests/ --filter "FullyQualifiedName~Auth"

# 4. Run full test suite
make test
```

## Checklist Summary

- [ ] Create `src/backend/Shadowbrook.Api/Infrastructure/Auth/` directory
- [ ] Move 9 files from `Auth/` to `Infrastructure/Auth/`, updating namespace to `Shadowbrook.Api.Infrastructure.Auth`
- [ ] Create `AuthServiceCollectionExtensions.cs` with `AddShadowbrookAuth()` extension method
- [ ] Delete empty `src/backend/Shadowbrook.Api/Auth/` directory
- [ ] Update `Program.cs`: replace inline auth block with `builder.Services.AddShadowbrookAuth(builder.Configuration);`
- [ ] Remove 4 now-unused `using` statements from `Program.cs`
- [ ] Update `using` in 5 API project files (see table above)
- [ ] Update `using` in 4 test files (see table above)
- [ ] Build, format, and run tests

## Risks

- **Low risk.** This is a pure structural refactor -- no behavioral changes. The namespace rename is mechanical and the extension method extracts code verbatim.
- The `ICurrentUser` registration (`AddScoped<ICurrentUser, CurrentUser>()`) is intentionally left in `Program.cs` alongside other DI registrations. If you prefer it inside `AddShadowbrookAuth()`, it can be moved there -- but that couples a general service abstraction to the auth setup method.

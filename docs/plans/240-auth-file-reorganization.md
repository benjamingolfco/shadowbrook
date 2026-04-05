# Implementation Plan: Auth File Reorganization (#240)

## Approach

Move all auth infrastructure files from `Teeforce.Api/Auth/` to `Teeforce.Api/Infrastructure/Auth/`, updating the namespace from `Teeforce.Api.Auth` to `Teeforce.Api.Infrastructure.Auth`. Extract auth service registration from `Program.cs` into a new `AddTeeforceAuth()` extension method in `AuthServiceCollectionExtensions.cs`. The auth feature endpoint (`Features/Auth/AuthEndpoints.cs`) stays where it is.

## Step 1: Move Files and Update Namespaces

### 1a. Create the destination directory

Create `src/backend/Teeforce.Api/Infrastructure/Auth/`.

### 1b. Move these 9 files

Move each file from `Auth/` to `Infrastructure/Auth/` and change the namespace declaration from `Teeforce.Api.Auth` to `Teeforce.Api.Infrastructure.Auth`:

| File | Namespace change |
|------|-----------------|
| `AppUserEnrichmentMiddleware.cs` | `Teeforce.Api.Auth` -> `Teeforce.Api.Infrastructure.Auth` |
| `CourseAccessAuthorizationHandler.cs` | `Teeforce.Api.Auth` -> `Teeforce.Api.Infrastructure.Auth` |
| `CourseAccessRequirement.cs` | `Teeforce.Api.Auth` -> `Teeforce.Api.Infrastructure.Auth` |
| `CurrentUser.cs` | `Teeforce.Api.Auth` -> `Teeforce.Api.Infrastructure.Auth` |
| `DevAuthHandler.cs` | `Teeforce.Api.Auth` -> `Teeforce.Api.Infrastructure.Auth` |
| `ICurrentUser.cs` | `Teeforce.Api.Auth` -> `Teeforce.Api.Infrastructure.Auth` |
| `PermissionAuthorizationHandler.cs` | `Teeforce.Api.Auth` -> `Teeforce.Api.Infrastructure.Auth` |
| `PermissionRequirement.cs` | `Teeforce.Api.Auth` -> `Teeforce.Api.Infrastructure.Auth` |
| `Permissions.cs` | `Teeforce.Api.Auth` -> `Teeforce.Api.Infrastructure.Auth` |

No internal `using` changes are needed within these files since they all share the same namespace, and their non-auth usings remain valid.

### 1c. Delete the old `Auth/` directory

After moving all files, delete `src/backend/Teeforce.Api/Auth/` (it should be empty).

## Step 2: Create `AuthServiceCollectionExtensions.cs`

Create `src/backend/Teeforce.Api/Infrastructure/Auth/AuthServiceCollectionExtensions.cs` with the following content:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace Teeforce.Api.Infrastructure.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddTeeforceAuth(
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
using Teeforce.Api.Auth;
```
to:
```csharp
using Teeforce.Api.Infrastructure.Auth;
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
builder.Services.AddTeeforceAuth(builder.Configuration);
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

However, since `ICurrentUser` and `CurrentUser` have moved namespaces, the `using Teeforce.Api.Infrastructure.Auth;` added in step 3a covers this.

## Step 4: Update `using` Statements Across the Codebase

The following files reference `Teeforce.Api.Auth` and need the `using` updated to `Teeforce.Api.Infrastructure.Auth`:

### API project (src/backend/Teeforce.Api/)

| File | Current using | New using |
|------|--------------|-----------|
| `Program.cs` | `using Teeforce.Api.Auth;` | `using Teeforce.Api.Infrastructure.Auth;` |
| `Infrastructure/Data/ApplicationDbContext.cs` | `using Teeforce.Api.Auth;` | `using Teeforce.Api.Infrastructure.Auth;` |
| `Features/Courses/CourseEndpoints.cs` | `using Teeforce.Api.Auth;` | `using Teeforce.Api.Infrastructure.Auth;` |
| `Features/Auth/AuthEndpoints.cs` | `using Teeforce.Api.Auth;` | `using Teeforce.Api.Infrastructure.Auth;` |
| `Features/FeatureFlags/FeatureEndpoints.cs` | `using Teeforce.Api.Auth;` | `using Teeforce.Api.Infrastructure.Auth;` |

### Test project (tests/Teeforce.Api.Tests/)

| File | Current using | New using |
|------|--------------|-----------|
| `Auth/AppUserEnrichmentMiddlewareTests.cs` | `using Teeforce.Api.Auth;` | `using Teeforce.Api.Infrastructure.Auth;` |
| `Auth/PermissionsTests.cs` | `using Teeforce.Api.Auth;` | `using Teeforce.Api.Infrastructure.Auth;` |
| `Auth/CourseAccessAuthorizationHandlerTests.cs` | `using Teeforce.Api.Auth;` | `using Teeforce.Api.Infrastructure.Auth;` |
| `Auth/PermissionAuthorizationHandlerTests.cs` | `using Teeforce.Api.Auth;` | `using Teeforce.Api.Infrastructure.Auth;` |

## Step 5: Verify

Run these commands from the repo root in order:

```bash
# 1. Build to verify compilation
dotnet build teeforce.slnx

# 2. Fix any style warnings
dotnet format teeforce.slnx

# 3. Run auth-specific tests
dotnet test tests/Teeforce.Api.Tests/ --filter "FullyQualifiedName~Auth"

# 4. Run full test suite
make test
```

## Checklist Summary

- [ ] Create `src/backend/Teeforce.Api/Infrastructure/Auth/` directory
- [ ] Move 9 files from `Auth/` to `Infrastructure/Auth/`, updating namespace to `Teeforce.Api.Infrastructure.Auth`
- [ ] Create `AuthServiceCollectionExtensions.cs` with `AddTeeforceAuth()` extension method
- [ ] Delete empty `src/backend/Teeforce.Api/Auth/` directory
- [ ] Update `Program.cs`: replace inline auth block with `builder.Services.AddTeeforceAuth(builder.Configuration);`
- [ ] Remove 4 now-unused `using` statements from `Program.cs`
- [ ] Update `using` in 5 API project files (see table above)
- [ ] Update `using` in 4 test files (see table above)
- [ ] Build, format, and run tests

## Risks

- **Low risk.** This is a pure structural refactor -- no behavioral changes. The namespace rename is mechanical and the extension method extracts code verbatim.
- The `ICurrentUser` registration (`AddScoped<ICurrentUser, CurrentUser>()`) is intentionally left in `Program.cs` alongside other DI registrations. If you prefer it inside `AddTeeforceAuth()`, it can be moved there -- but that couples a general service abstraction to the auth setup method.

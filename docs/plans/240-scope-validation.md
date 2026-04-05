# 240 — Scope and Audience Validation

## Problem

The API validates JWT tokens via `Microsoft.Identity.Web` but does not validate that tokens carry the expected delegated scope (`access_as_user`). Any application that obtains a token for the API's client ID can call any endpoint. We need to restrict access to tokens that were granted the correct delegated permission by the SPA.

Additionally, the `AzureAd` config section is missing an explicit `Audience` property, which means `Microsoft.Identity.Web` falls back to using `ClientId` as the audience. While this works for the default `api://{ClientId}` App ID URI, it is fragile and should be explicit.

## Approach

Use the `AddMicrosoftIdentityWebApi` configuration callback to add scope validation at the JwtBearer token validation level. This is the right layer because:

1. All endpoints are already behind `[Authorize(Policy = "...")]` policies, so scope validation should happen globally during token validation, not per-endpoint.
2. The `[RequiredScope]` attribute is designed for MVC controllers. While it can work on Wolverine endpoint classes, applying it globally via JwtBearer options is cleaner and requires zero changes to any endpoint file.
3. Dev mode uses `DevAuthHandler` which bypasses JwtBearer entirely, so scope validation is naturally skipped.

## Steps

### 1. Add `Audience` and `Scopes` to `appsettings.json`

**File:** `src/backend/Teeforce.Api/appsettings.json`

Add `Audience` and `Scopes` to the `AzureAd` section:

```jsonc
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "f74e8993-5f31-49cb-8772-a20a7f0cf2b6",
  "ClientId": "e3eea6af-dfac-49f5-a3ea-45dc1cf42873",
  "Audience": "api://e3eea6af-dfac-49f5-a3ea-45dc1cf42873",
  "Scopes": "access_as_user"
}
```

**Why `Audience`:** Makes the expected audience explicit. `Microsoft.Identity.Web` uses this to validate the `aud` claim. Without it, the library falls back to `ClientId`, which works only if the App ID URI is the default `api://{ClientId}`. Being explicit prevents breakage if the App ID URI changes.

**Why `Scopes`:** Configuration-driven scope list. The `RequiredScope` attribute and `ScopeAuthorizationRequirement` both support reading from config keys. Using config rather than hardcoded values allows different scopes per environment if needed.

### 2. Add scope validation to `AuthServiceCollectionExtensions.cs`

**File:** `src/backend/Teeforce.Api/Infrastructure/Auth/AuthServiceCollectionExtensions.cs`

In the non-dev-auth branch, after `AddMicrosoftIdentityWebApi`, configure the JwtBearer options to validate scopes. There are two viable approaches:

#### Recommended approach: Authorization policy with `RequiredScopeOrAppPermission`

Microsoft.Identity.Web provides `AddRequiredScopeOrAppPermissionAuthorization()` which adds a global authorization policy. However, since our endpoints already use custom authorization policies, the cleanest approach is to add scope validation to the JwtBearer `OnTokenValidated` event.

#### Actual approach: JwtBearer `OnTokenValidated` event

Add scope checking logic in the `OnTokenValidated` event of JwtBearer. This runs after the token is validated but before the request reaches any endpoint. When the token lacks the required scope, the event handler marks authentication as failed, resulting in a 401.

```csharp
// Pseudocode for the non-dev branch
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(jwtOptions =>
    {
        // Audience is set from config automatically by Microsoft.Identity.Web
        // when AzureAd:Audience is present
    },
    msIdentityOptions =>
    {
        configuration.GetSection("AzureAd").Bind(msIdentityOptions);
    });

// Add scope authorization requirement globally
services.Configure<JwtBearerOptions>(
    JwtBearerDefaults.AuthenticationScheme,
    options =>
    {
        var requiredScopes = configuration
            .GetValue<string>("AzureAd:Scopes")?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            ?? [];

        var existingOnTokenValidated = options.Events?.OnTokenValidated;

        options.Events ??= new JwtBearerEvents();
        options.Events.OnTokenValidated = async context =>
        {
            // Call any existing handler first
            if (existingOnTokenValidated is not null)
                await existingOnTokenValidated(context);

            // Validate scope claim
            var scopeClaim = context.Principal?.FindFirst("scp")
                          ?? context.Principal?.FindFirst(
                              "http://schemas.microsoft.com/identity/claims/scope");

            if (scopeClaim is null || requiredScopes.Length == 0)
            {
                context.Fail("Token does not contain required scope.");
                return;
            }

            var tokenScopes = scopeClaim.Value.Split(' ');
            if (!requiredScopes.Any(s => tokenScopes.Contains(s)))
            {
                context.Fail($"Token scope '{scopeClaim.Value}' does not contain any of: {string.Join(", ", requiredScopes)}");
            }
        };
    });
```

**Wait -- simpler alternative:** Microsoft.Identity.Web natively supports scope validation via the `[RequiredScope]` attribute and `AddMicrosoftIdentityWebApiAuthentication`. But since we use Wolverine HTTP (static methods with `[WolverineGet]` etc.), the cleanest approach is actually to use the overload of `AddMicrosoftIdentityWebApi` that accepts an `Action<JwtBearerOptions>` and configure `TokenValidationParameters.ValidAudiences`, combined with a scope-checking authorization requirement added to all existing policies.

#### Final recommended approach: Add scope requirement to the default authorization policy

The simplest, most maintainable approach:

1. Use the `AddMicrosoftIdentityWebApi` overload that takes both `Action<JwtBearerOptions>` and `Action<MicrosoftIdentityOptions>` -- this lets us configure audience explicitly.
2. Add a `ScopeAuthorizationRequirement` to the default authorization policy (or a base policy). Since **every** endpoint already uses `[Authorize(Policy = "...")]`, we can add scope validation as a requirement on the **default policy** or as a shared requirement across all named policies.

Actually, the cleanest path: since every endpoint uses a named policy, we should add a scope authorization handler that runs for all authenticated requests. Microsoft.Identity.Web ships `ScopeAuthorizationHandler` which checks for `scp` claims. We can register it and add `ScopeAuthorizationRequirement` to each policy.

**But this adds boilerplate to every policy.** Instead, use the **fallback policy** or add scope checking to the `RequireAppAccess` base, since `RequireCourseAccess` also requires `RequireAppAccess` implicitly via the `AppUserEnrichmentMiddleware`.

### Revised, concrete approach

After reviewing the options and the codebase patterns, here is the simplest correct implementation:

#### A. Change `AddMicrosoftIdentityWebApi` to use the two-lambda overload

This allows configuring `JwtBearerOptions.Audience` explicitly and hooking into token validation:

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(
        jwtBearerOptions =>
        {
            configuration.Bind("AzureAd", jwtBearerOptions);
            // Audience is set automatically from AzureAd:Audience by the
            // MicrosoftIdentityOptions binder below, but we can also set it
            // explicitly on TokenValidationParameters if needed.
        },
        microsoftIdentityOptions =>
        {
            configuration.GetSection("AzureAd").Bind(microsoftIdentityOptions);
        });
```

#### B. Add scope validation via a custom authorization requirement

Create a `ScopeRequirement` and `ScopeAuthorizationHandler` that reads the required scopes from configuration and validates the `scp` claim on the token. Add this requirement to all three existing policies.

Alternatively, since `RequireAppAccess` is the base-level policy and is used on every authenticated endpoint (either directly or indirectly via `RequireCourseAccess` which also passes through `AppUserEnrichmentMiddleware`), we can add scope validation into the `PermissionAuthorizationHandler` itself -- but that conflates concerns.

**Best option: add `ScopeAuthorizationRequirement` to each policy definition.** This is explicit, easy to test, and follows the existing pattern.

```csharp
// New files
public class ScopeRequirement(params string[] acceptedScopes) : IAuthorizationRequirement
{
    public string[] AcceptedScopes { get; } = acceptedScopes;
}

public class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var scopeClaim = context.User.FindFirst("scp")
                      ?? context.User.FindFirst("http://schemas.microsoft.com/identity/claims/scope");

        if (scopeClaim is not null)
        {
            var scopes = scopeClaim.Value.Split(' ');
            if (requirement.AcceptedScopes.Any(s => scopes.Contains(s)))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
```

Then in the policy registration:

```csharp
var requiredScopes = configuration
    .GetValue<string>("AzureAd:Scopes")?
    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
    ?? [];

services.AddAuthorizationBuilder()
    .AddPolicy("RequireAppAccess", policy =>
    {
        policy.AddRequirements(new PermissionRequirement(Permissions.AppAccess));
        if (requiredScopes.Length > 0)
            policy.AddRequirements(new ScopeRequirement(requiredScopes));
    })
    .AddPolicy("RequireUsersManage", policy =>
    {
        policy.AddRequirements(new PermissionRequirement(Permissions.UsersManage));
        if (requiredScopes.Length > 0)
            policy.AddRequirements(new ScopeRequirement(requiredScopes));
    })
    .AddPolicy("RequireCourseAccess", policy =>
    {
        policy.AddRequirements(new CourseAccessRequirement());
        if (requiredScopes.Length > 0)
            policy.AddRequirements(new ScopeRequirement(requiredScopes));
    });

services.AddScoped<IAuthorizationHandler, ScopeAuthorizationHandler>();
```

#### C. Dev mode handling

No changes needed. When `Auth:UseDevAuth` is true:
- `DevAuthHandler` is used instead of JwtBearer -- no audience or scope validation occurs
- The `AzureAd:Scopes` config key is not set in dev/test environments, so `requiredScopes` will be empty and `ScopeRequirement` won't be added to policies
- If `Scopes` IS set in dev mode, `DevAuthHandler` doesn't produce `scp` claims, so we need to ensure the `ScopeRequirement` is only added when NOT using dev auth

The conditional should be:

```csharp
if (!useDevAuth && requiredScopes.Length > 0)
    policy.AddRequirements(new ScopeRequirement(requiredScopes));
```

This ensures dev mode and integration tests are completely unaffected.

### 3. Summary of file changes

#### Create

| File | Purpose |
|------|---------|
| `src/backend/Teeforce.Api/Infrastructure/Auth/ScopeRequirement.cs` | `IAuthorizationRequirement` that holds accepted scopes |
| `src/backend/Teeforce.Api/Infrastructure/Auth/ScopeAuthorizationHandler.cs` | `AuthorizationHandler<ScopeRequirement>` that validates `scp` claim |
| `tests/Teeforce.Api.Tests/Features/Auth/ScopeAuthorizationHandlerTests.cs` | Unit tests for the handler |

#### Modify

| File | Change |
|------|--------|
| `src/backend/Teeforce.Api/appsettings.json` | Add `Audience` and `Scopes` to `AzureAd` section |
| `src/backend/Teeforce.Api/Infrastructure/Auth/AuthServiceCollectionExtensions.cs` | Switch to two-lambda `AddMicrosoftIdentityWebApi` overload; add `ScopeRequirement` to all policies; register `ScopeAuthorizationHandler` |

### 4. Testing strategy

#### Unit tests (new file: `ScopeAuthorizationHandlerTests.cs`)

- Token with matching `scp` claim succeeds
- Token with `scp` claim containing multiple scopes, one matching, succeeds
- Token with `scp` claim not matching any required scope does NOT succeed (handler doesn't call `context.Succeed`)
- Token with no `scp` claim does NOT succeed
- Token with `http://schemas.microsoft.com/identity/claims/scope` claim (v1.0 token format) succeeds
- Empty `AcceptedScopes` array -- handler does not succeed (no scopes to match)

#### Integration tests (existing tests)

No changes needed. Integration tests use `DevAuthHandler` via `Auth:UseDevAuth = true` (set in `TestWebApplicationFactory.ConfigureWebHost`). The `AzureAd:Scopes` config is not set in test environments, and even if it were, the `!useDevAuth` guard prevents `ScopeRequirement` from being added.

Verify: run existing auth integration tests to confirm no regression.

### 5. Risks and edge cases

- **v1.0 vs v2.0 tokens:** v1.0 tokens use `http://schemas.microsoft.com/identity/claims/scope` while v2.0 use `scp`. The handler checks both.
- **Daemon apps:** Currently no daemon/service-to-service calls. If added later, they use `roles` claim, not `scp`. The `ScopeAuthorizationHandler` will not block them -- it simply won't succeed the requirement. A separate `AppRoleRequirement` would be needed. This is out of scope for now.
- **Multiple scopes:** The `Scopes` config value is space-delimited (matching the Entra ID token format). The handler checks if the token contains ANY of the accepted scopes.
- **Config-only deployment:** Adding `Audience` and `Scopes` to `appsettings.json` is sufficient for dev. For production, these values should also be set in Azure Container App environment variables or Key Vault (via `AzureAd__Audience` and `AzureAd__Scopes` env vars).

### 6. Dev tasks

#### Backend Developer

- [ ] Add `Audience` and `Scopes` to `AzureAd` section in `appsettings.json`
- [ ] Create `ScopeRequirement.cs` implementing `IAuthorizationRequirement`
- [ ] Create `ScopeAuthorizationHandler.cs` implementing `AuthorizationHandler<ScopeRequirement>`
- [ ] Update `AuthServiceCollectionExtensions.cs`: switch to two-lambda `AddMicrosoftIdentityWebApi` overload
- [ ] Update `AuthServiceCollectionExtensions.cs`: add `ScopeRequirement` to all three policies (guarded by `!useDevAuth && requiredScopes.Length > 0`)
- [ ] Register `ScopeAuthorizationHandler` in DI
- [ ] Write unit tests for `ScopeAuthorizationHandler` covering all scenarios listed above
- [ ] Run existing integration tests to verify no regression
- [ ] Run `dotnet build` and `dotnet format` to verify compilation and style

# Plan: Remove AppUserEnrichmentMiddleware

## Problem

`AppUserEnrichmentMiddleware` is a custom middleware that runs on every authenticated request to:
1. Look up the AppUser by Entra `oid` claim (with 5-min memory cache)
2. Handle first-login identity linking (match by email, dispatch `CompleteIdentitySetupCommand`)
3. Compute permissions and add application claims (`app_user_id`, `organization_id`, `role`, `permission`)
4. Return 403 `{ "reason": "no_account" }` for authenticated users with no AppUser
5. Call `RecordLogin()` + `SaveChangesAsync()` on every request

Problems with this approach:
- Custom middleware bypasses ASP.NET Core's built-in authentication/authorization pipeline
- `SaveChangesAsync()` on every request bypasses Wolverine's transactional middleware
- `RecordLogin()` writes to the DB on every request (Entra ID tracks logins already)
- `DevAuthHandler` duplicates the AppUser lookup and claim enrichment (lines 33-52)
- The 403 short-circuit for missing accounts belongs in authorization, not middleware

## Approach

Replace the middleware with two ASP.NET Core extension points, then decouple the claims transformation from EF/DbContext:

1. **`IClaimsTransformation`** -- handles enrichment (steps 1-3 above) in the authentication pipeline
2. **`IAuthorizationHandler` for a new `RequireAppUserRequirement`** -- handles the 403 rejection (step 4)
3. **Remove `RecordLogin()`** entirely -- Entra ID tracks logins, we don't need to duplicate
4. **Simplify `DevAuthHandler`** -- only authenticate and set `oid` claim; enrichment flows through `IClaimsTransformation` uniformly
5. **Introduce `IAppUserClaimsProvider`** -- a lightweight read-model interface that decouples the claims transformation from `ApplicationDbContext` and the `AppUser` entity
6. **Make `CompleteIdentitySetup` fire-and-forget** -- use `bus.PublishAsync()` instead of `bus.InvokeAsync()`, eliminating the `ReloadAsync` hack and cross-DbContext scoping concern

---

## Phase 1: Replace Middleware with ASP.NET Core Extension Points

### Step 1: Create `AppUserClaimsTransformation`

**Create:** `src/backend/Shadowbrook.Api/Infrastructure/Auth/AppUserClaimsTransformation.cs`

This implements `IClaimsTransformation` (from `Microsoft.AspNetCore.Authentication`). ASP.NET Core calls `TransformAsync` on every authenticated request after the authentication handler runs. The cache makes repeated calls cheap.

**Important: `IClaimsTransformation.TransformAsync` can be called multiple times per request** (e.g., if `HttpContext.AuthenticateAsync()` is invoked more than once). The implementation must guard against adding duplicate claims. Before adding any claims, check if the principal already has an `app_user_id` claim and return early if so. The memory cache alone does not protect against this -- it returns `EnrichmentData` but `AddIdentity()` would still append duplicate claims on each call.

```csharp
public class AppUserClaimsTransformation : IClaimsTransformation
{
    // Constructor-injected: ApplicationDbContext, IMemoryCache, IMessageBus, ILogger

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // 1. Extract oid from principal (check both "oid" and the long-form MS claim)
        // 2. If not authenticated or no oid, return principal unchanged
        // 3. DUPLICATE GUARD: If principal already has "app_user_id" claim, return early.
        //    This handles the case where TransformAsync is called multiple times per request.
        // 4. Check cache for EnrichmentData (same CacheKey pattern: "appuser:{oid}")
        // 5. On cache miss:
        //    a. Look up AppUser by IdentityId
        //    b. If not found, try email match (extract email from claims)
        //       - If email match found with null IdentityId, dispatch CompleteIdentitySetupCommand
        //       - Reload entity after command completes (see note below on DbContext scoping)
        //    c. If still not found, return principal unchanged (no claims added)
        //       Authorization layer handles rejection.
        //    d. Compute permissions via Permissions.GetForRole()
        //    e. Cache the EnrichmentData (5-min TTL)
        // 6. Add claims to principal: app_user_id, organization_id, role, permission(s)
        // 7. Return enriched principal
    }
}
```

**DbContext scoping note (Phase 1 only -- eliminated in Phase 2):** The `CompleteIdentitySetupHandler` runs in a Wolverine-managed scope with a *different* `DbContext` instance than the one injected into `AppUserClaimsTransformation`. This is why `db.Entry(appUser).ReloadAsync()` is needed after `bus.InvokeAsync()` returns -- the local DbContext's tracked entity is stale. Add a code comment at the `ReloadAsync()` call site explaining this.

Key differences from the middleware:
- **No 403 writing** -- if no AppUser is found, the principal is returned without `app_user_id`. The authorization handler below will reject the request.
- **No `RecordLogin()`** -- removed entirely (see Step 4).
- **No `SaveChangesAsync()`** -- the only DB write was `RecordLogin()`, which is gone. The `CompleteIdentitySetupCommand` is dispatched via `IMessageBus.InvokeAsync`, which has its own transaction.
- **Idempotent on repeated calls** -- the duplicate claims guard ensures `AddIdentity()` is only called once per request lifetime.

The `EnrichmentData` record and `CacheKey` static method move into this class. `CacheKey` remains `public static` because `AuthEndpoints.UpdateUser` uses it for cache invalidation.

The `ExtractEmail` helper also moves here (private static method, same logic).

### Step 2: Create `RequireAppUserRequirement` and `RequireAppUserHandler`

**Create:** `src/backend/Shadowbrook.Api/Infrastructure/Auth/RequireAppUserRequirement.cs`

```csharp
public class RequireAppUserRequirement : IAuthorizationRequirement { }
```

**Create:** `src/backend/Shadowbrook.Api/Infrastructure/Auth/RequireAppUserHandler.cs`

```csharp
public class RequireAppUserHandler : AuthorizationHandler<RequireAppUserRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RequireAppUserRequirement requirement)
    {
        // If the user has an "app_user_id" claim, succeed:
        //   context.Succeed(requirement);
        // Otherwise, explicitly call context.Fail():
        //   context.Fail(new AuthorizationFailureReason(this, "No linked AppUser account"));
        //
        // IMPORTANT: Fail() must be called explicitly. Without it, FailedRequirements
        // will be empty and AppUserAuthorizationResultHandler cannot detect which
        // requirement failed. These two components are coupled -- the result handler
        // uses OfType<RequireAppUserRequirement>() on FailedRequirements.
    }
}
```

**Important:** This handler does NOT write the `{ "reason": "no_account" }` response body directly. Instead, a custom `IAuthorizationMiddlewareResultHandler` inspects `FailedRequirements` to produce the JSON response.

**Create:** `src/backend/Shadowbrook.Api/Infrastructure/Auth/AppUserAuthorizationResultHandler.cs`

```csharp
public class AppUserAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly IAuthorizationMiddlewareResultHandler _defaultHandler;

    // Constructor accepts IAuthorizationMiddlewareResultHandler for delegation.
    // In production, this is AuthorizationMiddlewareResultHandler.
    // In tests, this can be a mock to verify delegation behavior.

    public async Task HandleAsync(RequestDelegate next, HttpContext context,
        AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        // Detection logic -- all four conditions must be true:
        //   1. authorizeResult.Forbidden
        //   2. context.User.Identity?.IsAuthenticated == true
        //   3. !context.User.HasClaim(c => c.Type == "app_user_id")
        //   4. authorizeResult.AuthorizationFailure?.FailedRequirements
        //        .OfType<RequireAppUserRequirement>().Any() == true
        //
        // If all conditions met:
        //   Write 403 with { "reason": "no_account" }
        // Otherwise:
        //   Delegate to _defaultHandler.HandleAsync(...)
    }
}
```

### Step 3: Register new services in Program.cs

**Modify:** `src/backend/Shadowbrook.Api/Program.cs`

#### 3a. Register `IClaimsTransformation`

Add after the existing service registrations (near the `AddScoped<ICurrentUser>` line):

```csharp
builder.Services.AddScoped<IClaimsTransformation, AppUserClaimsTransformation>();
```

#### 3b. Register authorization requirement and handler

Update the authorization section to add the `RequireAppUser` policy. All existing policies (`RequireAppAccess`, `RequireUsersManage`) should include `RequireAppUserRequirement` as a base requirement so that every protected endpoint gets the no-account check:

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAppUser", policy =>
        policy.AddRequirements(new RequireAppUserRequirement()))
    .AddPolicy("RequireAppAccess", policy =>
        policy.AddRequirements(new RequireAppUserRequirement(), new PermissionRequirement(Permissions.AppAccess)))
    .AddPolicy("RequireUsersManage", policy =>
        policy.AddRequirements(new RequireAppUserRequirement(), new PermissionRequirement(Permissions.UsersManage)));

builder.Services.AddScoped<IAuthorizationHandler, RequireAppUserHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AppUserAuthorizationResultHandler>();
```

#### 3c. Remove middleware registration

Delete this line from the middleware pipeline:

```csharp
app.UseMiddleware<AppUserEnrichmentMiddleware>();
```

The pipeline becomes: `UseCors() -> UseAuthentication() -> UseAuthorization()`. Claims transformation runs automatically as part of `UseAuthentication()`.

### Step 4: Remove `RecordLogin()` and `LastLoginAt`

#### 4a. Modify domain entity

**Modify:** `src/backend/Shadowbrook.Domain/AppUserAggregate/AppUser.cs`

- Delete the `LastLoginAt` property
- Delete the `RecordLogin()` method

#### 4b. Modify EF configuration

**Modify:** `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/AppUserConfiguration.cs`

- Delete the line: `builder.Property(u => u.LastLoginAt);`

#### 4c. Add EF migration

```bash
dotnet ef migrations add RemoveLastLoginAt --project src/backend/Shadowbrook.Api
```

This generates a migration that drops the `LastLoginAt` column from the `AppUsers` table.

#### 4d. Remove domain tests

**Modify:** `tests/Shadowbrook.Domain.Tests/AppUserAggregate/AppUserTests.cs`

- Delete the `RecordLogin_UpdatesLastLoginAt` test entirely (behavior is removed, not changed)
- Remove `Assert.Null(user.LastLoginAt)` from `CreateAdmin_SetsAdminRoleAndNullOrganizationId`
- Remove `Assert.Null(user.LastLoginAt)` from `CreateOperator_SetsOperatorRoleAndOrganizationId`

**PR justification required:** These test assertion removals must be accompanied by a PR comment explaining that `RecordLogin()` and `LastLoginAt` are intentionally removed behaviors (not regressions). The comment should state: "Removed `RecordLogin` tests and `LastLoginAt` assertions because the feature is being deleted -- Entra ID tracks login timestamps, making our custom tracking redundant. This is an intentional behavior removal per the acceptance criteria." Per CLAUDE.md test integrity rules, this justification is mandatory.

### Step 5: Simplify `DevAuthHandler`

**Modify:** `src/backend/Shadowbrook.Api/Infrastructure/Auth/DevAuthHandler.cs`

Remove the entire AppUser lookup and claim enrichment block (lines 33-52). After this change, `DevAuthHandler` only:

1. Extracts the identity ID from `Bearer {id}` header
2. Creates a `ClaimsPrincipal` with a single `oid` claim
3. Returns `AuthenticateResult.Success`

The `IClaimsTransformation` runs after authentication and adds all the application claims uniformly, regardless of whether the request came through DevAuth or Entra ID JWT.

The simplified handler no longer needs `IServiceProvider` injection. Remove it from the primary constructor. The constructor becomes:

```csharp
public class DevAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
```

Remove the `using` statements that are no longer needed:
- `Microsoft.EntityFrameworkCore`
- `Shadowbrook.Api.Infrastructure.Data`

**`role` claim consistency note:** After this change, DevAuth will produce `role` claims via `IClaimsTransformation` instead of the custom middleware. Previously, only the middleware added `role` claims for DevAuth users. This is a correctness improvement (DevAuth now matches the production auth path exactly), not a regression. Flag this for smoke testing -- verify that DevAuth users get the correct role claims through the new pipeline.

### Step 6: Update `AuthEndpoints` cache invalidation

**Modify:** `src/backend/Shadowbrook.Api/Features/Auth/AuthEndpoints.cs`

Line 174 references `AppUserEnrichmentMiddleware.CacheKey(appUser.IdentityId)`. Update to reference the new class:

```csharp
cache.Remove(AppUserClaimsTransformation.CacheKey(appUser.IdentityId));
```

Also update the `using` statement if the namespace changes (it stays in `Shadowbrook.Api.Infrastructure.Auth`, so no change needed).

### Step 7: Delete `AppUserEnrichmentMiddleware`

**Delete:** `src/backend/Shadowbrook.Api/Infrastructure/Auth/AppUserEnrichmentMiddleware.cs`

### Step 8: Rewrite middleware tests as transformation + authorization tests

**Delete or rename:** `tests/Shadowbrook.Api.Tests/Auth/AppUserEnrichmentMiddlewareTests.cs`

**Create:** `tests/Shadowbrook.Api.Tests/Auth/AppUserClaimsTransformationTests.cs`

Port the existing test scenarios, adapted for the `IClaimsTransformation` interface:

| Old test | New test | Changes |
|----------|----------|---------|
| `UnauthenticatedRequest_PassesThroughWithoutEnrichment` | `UnauthenticatedPrincipal_ReturnsUnchanged` | Call `TransformAsync` with unauthenticated principal, assert no claims added |
| `AuthenticatedUser_ExistingLinkedAppUser_GetsEnrichedWithClaims` | `AuthenticatedUser_LinkedAppUser_AddsClaims` | Call `TransformAsync`, assert `app_user_id`, `organization_id`, `role`, `permission` claims |
| `AuthenticatedUser_UnlinkedAppUser_MatchesByEmail_DispatchesSetupCommand` | `AuthenticatedUser_UnlinkedAppUser_MatchesByEmail_DispatchesSetupCommand` | Same -- verify `IMessageBus.InvokeAsync` called with `CompleteIdentitySetupCommand` |
| `AuthenticatedUser_NoAppUser_Returns403WithNoAccountReason` | `AuthenticatedUser_NoAppUser_ReturnsWithoutAppUserClaim` | No 403 check -- just verify no `app_user_id` claim is added. The 403 is now authorization's job. |
| `InactiveLinkedUser_IsEnrichedWithoutPermissions` | `InactiveUser_EnrichedWithoutPermissions` | Same behavior -- claims added but no permission claims |
| `InactiveLinkedUser_DoesNotRecordLogin` | **Delete** | `RecordLogin` is removed entirely |
| *(new)* | `TransformAsync_CalledTwice_DoesNotDuplicateClaims` | Call `TransformAsync` twice with the same principal. Assert that `app_user_id` claim appears exactly once. Verifies the duplicate claims guard. |

Test setup changes:
- Instantiate `AppUserClaimsTransformation` directly with dependencies (InMemory DbContext, MemoryCache, NSubstitute `IMessageBus`, NullLogger)
- Call `TransformAsync(principal)` and inspect the returned `ClaimsPrincipal`
- No `HttpContext`, no `RequestDelegate`, no middleware pipeline

**Create:** `tests/Shadowbrook.Api.Tests/Auth/RequireAppUserHandlerTests.cs`

| Test | Behavior |
|------|----------|
| `UserWithAppUserIdClaim_Succeeds` | Handler calls `context.Succeed(requirement)` |
| `UserWithoutAppUserIdClaim_Fails` | Handler calls `context.Fail()` -- verify requirement appears in `FailedRequirements` |

**Create:** `tests/Shadowbrook.Api.Tests/Auth/AppUserAuthorizationResultHandlerTests.cs`

| Test | Behavior |
|------|----------|
| `FailedAppUserRequirement_AuthenticatedUser_Returns403WithNoAccountReason` | Writes 403 + `{ "reason": "no_account" }`. Test `HttpContext` must set `Response.Body = new MemoryStream()` (same pattern as existing middleware tests). |
| `FailedOtherRequirement_DelegatesToDefaultHandler` | Inject a mock `IAuthorizationMiddlewareResultHandler` as the default handler. Verify delegation via the mock, not against the real `AuthorizationMiddlewareResultHandler`. |

---

## Phase 2: Decouple Claims Transformation from DbContext

Phase 2 introduces a read-model interface so the claims transformation never touches `ApplicationDbContext` or materializes `AppUser` entities. It also makes `CompleteIdentitySetup` fire-and-forget, eliminating the `ReloadAsync` hack.

### Step 9: Create `IAppUserClaimsProvider` and `AppUserClaimsData`

**Create:** `src/backend/Shadowbrook.Api/Infrastructure/Auth/IAppUserClaimsProvider.cs`

This is a lightweight read-model interface. It lives in the API layer's `Infrastructure/Auth/` because it serves the authentication/authorization concern specifically -- it is not a domain interface.

```csharp
public interface IAppUserClaimsProvider
{
    Task<AppUserClaimsData?> GetByIdentityIdAsync(string identityId);
    Task<AppUserClaimsData?> GetByEmailUnlinkedAsync(string email);
}

public record AppUserClaimsData(
    Guid AppUserId,
    Guid? OrganizationId,
    AppUserRole Role,
    bool IsActive);
```

**Design rationale:** This is intentionally not a domain repository interface. It is a read-model projection that serves only the claims transformation. It returns a flat record with just the four fields needed for enrichment, never the full `AppUser` entity. This keeps the transformation decoupled from the domain's write model.

### Step 10: Create `AppUserClaimsProvider` implementation

**Create:** `src/backend/Shadowbrook.Api/Infrastructure/Auth/AppUserClaimsProvider.cs`

```csharp
public class AppUserClaimsProvider(ApplicationDbContext db) : IAppUserClaimsProvider
{
    public async Task<AppUserClaimsData?> GetByIdentityIdAsync(string identityId)
    {
        // Use EF projection (Select) -- never materializes the AppUser entity.
        // SELECT AppUserId, OrganizationId, Role, IsActive
        // FROM AppUsers WHERE IdentityId = @identityId
        return await db.AppUsers
            .Where(u => u.IdentityId == identityId)
            .Select(u => new AppUserClaimsData(u.Id, u.OrganizationId, u.Role, u.IsActive))
            .FirstOrDefaultAsync();
    }

    public async Task<AppUserClaimsData?> GetByEmailUnlinkedAsync(string email)
    {
        // SELECT AppUserId, OrganizationId, Role, IsActive
        // FROM AppUsers WHERE Email = @email AND IdentityId IS NULL
        return await db.AppUsers
            .Where(u => u.Email == email && u.IdentityId == null)
            .Select(u => new AppUserClaimsData(u.Id, u.OrganizationId, u.Role, u.IsActive))
            .FirstOrDefaultAsync();
    }
}
```

### Step 11: Register `IAppUserClaimsProvider`

**Modify:** `src/backend/Shadowbrook.Api/Program.cs`

Add near the other scoped service registrations:

```csharp
builder.Services.AddScoped<IAppUserClaimsProvider, AppUserClaimsProvider>();
```

This step compiles independently -- the provider is registered but not yet consumed.

### Step 12: Refactor `AppUserClaimsTransformation` to use `IAppUserClaimsProvider`

**Modify:** `src/backend/Shadowbrook.Api/Infrastructure/Auth/AppUserClaimsTransformation.cs`

Replace `ApplicationDbContext` dependency with `IAppUserClaimsProvider`. The constructor becomes:

```csharp
public class AppUserClaimsTransformation(
    IAppUserClaimsProvider claimsProvider,
    IMemoryCache cache,
    IMessageBus bus,
    ILogger<AppUserClaimsTransformation> logger) : IClaimsTransformation
```

The transformation flow changes:

```csharp
public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
{
    // 1. Extract oid, check authenticated, duplicate guard (unchanged)

    // 2. Check cache (unchanged)

    // 3. On cache miss:
    //    a. var data = await claimsProvider.GetByIdentityIdAsync(oid);
    //    b. If not found, try email match:
    //       - var email = ExtractEmail(principal);
    //       - data = await claimsProvider.GetByEmailUnlinkedAsync(email);
    //       - If found:
    //           * Build enrichment immediately from this data (no reload needed)
    //           * Fire-and-forget: await bus.PublishAsync(new CompleteIdentitySetupCommand(...))
    //    c. If data is still null, return principal unchanged
    //    d. Compute permissions via Permissions.GetForRole(data.Role) -- pure static, unchanged
    //    e. Build EnrichmentData from AppUserClaimsData + permissions, cache it

    // 4. Add claims to principal (unchanged)
}
```

**Key changes from Phase 1 version:**
- **No `ApplicationDbContext` dependency** -- replaced by `IAppUserClaimsProvider`
- **No entity materialization** -- works with `AppUserClaimsData` record throughout
- **`bus.PublishAsync()` instead of `bus.InvokeAsync()`** -- fire-and-forget. The `CompleteIdentitySetupCommand` runs asynchronously via Wolverine's outbox. We already have the `AppUserClaimsData` from the `GetByEmailUnlinkedAsync` call, so we can build enrichment claims immediately without waiting for the handler.
- **No `ReloadAsync()` hack** -- eliminated entirely. We never hold an entity reference and never need to refresh stale state.
- **No cross-DbContext scoping concern** -- the transformation never touches DbContext.

**Wolverine messaging note:** `bus.PublishAsync()` is Wolverine's fire-and-forget local dispatch. It enqueues the message to the durable outbox for eventual processing. Unlike `InvokeAsync()` (which executes the handler inline and blocks), `PublishAsync()` returns immediately. The `CompleteIdentitySetupHandler` will run in its own scope/transaction on the next processing cycle. This is correct because we already have the data we need for enrichment -- the identity linking is a side effect that just needs to happen eventually.

**`EnrichmentData` remains unchanged** -- it is built from `AppUserClaimsData` fields:

```csharp
var permissions = data.IsActive ? Permissions.GetForRole(data.Role) : [];
enrichmentData = new EnrichmentData(data.AppUserId, data.OrganizationId, data.Role, permissions);
```

**Cache stays in the transformation.** The `IMemoryCache` sits above the data interface and stores computed `EnrichmentData` including permissions. The provider is a raw data fetch; the transformation owns the caching and permission computation.

**`Permissions.GetForRole()` stays in the transformation.** It is a pure static mapping that takes an `AppUserRole` and returns permission strings. No reason to push it behind the interface.

Remove the following `using` statements from the file:
- `Microsoft.EntityFrameworkCore`
- `Shadowbrook.Api.Infrastructure.Data`

### Step 13: Update `AppUserClaimsTransformationTests`

**Modify:** `tests/Shadowbrook.Api.Tests/Auth/AppUserClaimsTransformationTests.cs`

Replace `InMemoryDbContext` setup with `NSubstitute` mock of `IAppUserClaimsProvider`. This is a significant simplification.

**Test setup changes:**

```csharp
// Before (Phase 1):
private static ApplicationDbContext CreateInMemoryDbContext() { ... }
private AppUserClaimsTransformation CreateTransformation(
    ApplicationDbContext db, IMemoryCache cache, IMessageBus bus) =>
    new(db, cache, bus, NullLogger<AppUserClaimsTransformation>.Instance);

// After (Phase 2):
private readonly IAppUserClaimsProvider claimsProvider = Substitute.For<IAppUserClaimsProvider>();
private AppUserClaimsTransformation CreateTransformation(
    IMemoryCache cache, IMessageBus bus) =>
    new(claimsProvider, cache, bus, NullLogger<AppUserClaimsTransformation>.Instance);
```

**Test-by-test changes:**

| Test | Changes |
|------|---------|
| `UnauthenticatedPrincipal_ReturnsUnchanged` | Remove `CreateInMemoryDbContext()`. Pass mock provider. No setup needed -- provider is never called for unauthenticated principals. |
| `AuthenticatedUser_LinkedAppUser_AddsClaims` | Replace DB seed with `claimsProvider.GetByIdentityIdAsync(oid).Returns(new AppUserClaimsData(...))`. Assert claims as before. |
| `AuthenticatedUser_UnlinkedAppUser_MatchesByEmail_DispatchesSetupCommand` | Replace DB seed with: `claimsProvider.GetByIdentityIdAsync(oid).Returns((AppUserClaimsData?)null)` and `claimsProvider.GetByEmailUnlinkedAsync("admin@example.com").Returns(new AppUserClaimsData(...))`. **Remove the `bus.When(...).Do(...)` callback simulation entirely** -- no longer needed since we don't reload. Change assertion from `bus.Received(1).InvokeAsync(...)` to `bus.Received(1).PublishAsync(Arg.Is<CompleteIdentitySetupCommand>(...))`. Verify claims are built from the `AppUserClaimsData` returned by `GetByEmailUnlinkedAsync`. |
| `AuthenticatedUser_NoAppUser_ReturnsWithoutAppUserClaim` | Replace DB setup with both provider methods returning null. Assert unchanged. |
| `InactiveUser_EnrichedWithoutPermissions` | Replace DB seed with `claimsProvider.GetByIdentityIdAsync(oid).Returns(new AppUserClaimsData(..., IsActive: false))`. Assert unchanged. |
| `TransformAsync_CalledTwice_DoesNotDuplicateClaims` | Same mock setup as linked user test. Assert unchanged. |

**Remove these imports from test file:**
- `Microsoft.EntityFrameworkCore`
- `Shadowbrook.Api.Infrastructure.Data`
- `Shadowbrook.Domain.AppUserAggregate` (no longer creating `AppUser` entities)
- `Shadowbrook.Domain.OrganizationAggregate` (no longer creating `Organization` entities)

**Add import:**
- `Shadowbrook.Api.Infrastructure.Auth` (for `AppUserClaimsData`)

### Step 14: `CompleteIdentitySetupHandler` stays as-is

**No changes** to `src/backend/Shadowbrook.Api/Features/Auth/Handlers/CompleteIdentitySetup/Handler.cs`.

The handler still uses `IAppUserRepository` to load the full `AppUser` aggregate, call `CompleteIdentitySetup()`, and let Wolverine's transactional middleware save. It runs in its own scope and transaction, decoupled from the claims transformation.

**No changes** to `tests/Shadowbrook.Api.Tests/Features/Auth/Handlers/CompleteIdentitySetupHandlerTests.cs`. The handler tests exercise the handler in isolation -- they are unaffected by how the command is dispatched.

---

## Verification

Run in order after each step:

```bash
# 1. Build
dotnet build shadowbrook.slnx

# 2. Format
dotnet format shadowbrook.slnx

# 3. Run auth tests
dotnet test tests/Shadowbrook.Api.Tests/ --filter "FullyQualifiedName~Auth"
dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~AppUser"

# 4. Full test suite
make test

# 5. Pre-merge check: grep for bare [Authorize] without a named policy
# Any endpoint using bare [Authorize] will bypass RequireAppUserRequirement.
# These must be updated to use a named policy (e.g., "RequireAppUser") or
# explicitly documented as intentionally exempt.
grep -rn '\[Authorize\]' src/backend/ --include='*.cs' | grep -v 'Authorize(' | grep -v 'AuthorizeAttribute'

# 6. Smoke test (requires DB running)
make dev
# Verify: unauthenticated request to /auth/me returns 401
# Verify: DevAuth Bearer {known-oid} to /auth/me returns enriched user (including role claims)
# Verify: DevAuth Bearer {unknown-oid} returns 403 { "reason": "no_account" }
# Verify: DevAuth role claims match expected values (consistency fix from DevAuthHandler simplification)
```

## Risks

1. **`IClaimsTransformation` runs on every authenticated request.** This is identical to the middleware behavior. The 5-min memory cache ensures the DB is only hit once per user per TTL window. No regression.

2. **`IClaimsTransformation` can be called multiple times per request.** ASP.NET Core may invoke `TransformAsync` more than once (e.g., explicit `HttpContext.AuthenticateAsync()` calls). The duplicate claims guard (check for existing `app_user_id` claim before adding) prevents `AddIdentity()` from appending duplicate claims. The cache handles the performance aspect, but the guard handles the correctness aspect. Both are required.

3. **`CompleteIdentitySetup` as fire-and-forget (Phase 2).** After Phase 2, `bus.PublishAsync()` enqueues the command for eventual processing. There is a brief window where the user has claims-based enrichment but the `AppUser.IdentityId` is not yet persisted in the database. This is acceptable because: (a) the enrichment data comes from `GetByEmailUnlinkedAsync`, which already returned the correct `AppUserId`, `OrganizationId`, `Role`, and `IsActive`; (b) on the next request (cache hit), enrichment comes from cache; (c) on the next cache miss (after TTL), `GetByIdentityIdAsync(oid)` will find the now-linked user. The only edge case is if the fire-and-forget fails entirely -- Wolverine's durable outbox with retries handles this.

4. **Race condition: two requests for the same unlinked user.** If two requests arrive simultaneously for a user matching by email, both may call `GetByEmailUnlinkedAsync` and both may publish `CompleteIdentitySetupCommand`. The handler is idempotent (`CompleteIdentitySetup()` returns silently if already linked to the same OID), so duplicate commands are harmless.

5. **Authorization result handler ordering.** `IAuthorizationMiddlewareResultHandler` is registered as a singleton. Only one can be active. Our custom handler must delegate to the default `AuthorizationMiddlewareResultHandler` for all non-AppUser failures to preserve standard behavior (401 for unauthenticated, default 403 for other policy failures).

6. **Cache key migration.** The `CacheKey` static method moves from `AppUserEnrichmentMiddleware` to `AppUserClaimsTransformation`. `AuthEndpoints.UpdateUser` references it for cache invalidation. This is a compile-time break that the developer must fix in Step 6 before deleting the middleware in Step 7.

7. **DevAuthHandler losing claim enrichment.** After simplification, `DevAuthHandler` only sets the `oid` claim. All other claims come from `IClaimsTransformation`. This means DevAuth now goes through the same enrichment path as production auth -- a correctness improvement. However, the developer must ensure the `IClaimsTransformation` is registered and the DevAuth user exists in the DB for dev/test scenarios to work.

8. **`RequireAppUserHandler.Fail()` and result handler detection are coupled.** The handler must explicitly call `context.Fail()` so that `RequireAppUserRequirement` appears in `AuthorizationFailure.FailedRequirements`. Without `Fail()`, the collection is empty and `AppUserAuthorizationResultHandler` cannot detect which requirement failed -- it would fall through to the default handler and produce a generic 403 instead of `{ "reason": "no_account" }`. If either side changes, both must be updated together.

9. **Bare `[Authorize]` endpoints bypass `RequireAppUserRequirement`.** Any endpoint using `[Authorize]` without a named policy will not include `RequireAppUserRequirement`. The pre-merge grep check (Step 5 in Verification) catches these. They must be updated to use a named policy or documented as intentionally exempt.

## Dev Tasks

### Backend Developer

#### Phase 1: Replace Middleware with ASP.NET Core Extension Points

- [ ] Create `AppUserClaimsTransformation` implementing `IClaimsTransformation` with cached AppUser lookup, email-match identity linking, claim enrichment, and **duplicate claims guard** (check for existing `app_user_id` claim before adding, to handle multiple `TransformAsync` calls per request)
- [ ] Add code comment at the `ReloadAsync()` call in `AppUserClaimsTransformation` explaining that `CompleteIdentitySetupHandler` runs in a Wolverine-managed scope with a different `DbContext` instance
- [ ] Create `RequireAppUserRequirement` (empty `IAuthorizationRequirement`)
- [ ] Create `RequireAppUserHandler` (`AuthorizationHandler<RequireAppUserRequirement>`) that succeeds when `app_user_id` claim exists, and **explicitly calls `context.Fail()`** when the claim is missing (required for `FailedRequirements` detection)
- [ ] Create `AppUserAuthorizationResultHandler` (`IAuthorizationMiddlewareResultHandler`) with detection logic: `authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true && !context.User.HasClaim(c => c.Type == "app_user_id") && authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<RequireAppUserRequirement>().Any() == true`
- [ ] Register all new services in `Program.cs` and update authorization policies to include `RequireAppUserRequirement`
- [ ] Remove `app.UseMiddleware<AppUserEnrichmentMiddleware>()` from `Program.cs`
- [ ] Simplify `DevAuthHandler` to only set `oid` claim (remove DB lookup, remove `IServiceProvider`)
- [ ] Remove `RecordLogin()` method and `LastLoginAt` property from `AppUser` entity
- [ ] Remove `LastLoginAt` from `AppUserConfiguration` EF type config
- [ ] Add EF migration `RemoveLastLoginAt`
- [ ] Update `AuthEndpoints.UpdateUser` cache invalidation to use `AppUserClaimsTransformation.CacheKey`
- [ ] Delete `AppUserEnrichmentMiddleware.cs`
- [ ] Delete `RecordLogin_UpdatesLastLoginAt` test and `LastLoginAt` assertions from `AppUserTests`
- [ ] Delete `InactiveLinkedUser_DoesNotRecordLogin` test from middleware tests
- [ ] **Post PR justification comment** for removed/modified test assertions (`RecordLogin` tests and `LastLoginAt` assertions) per CLAUDE.md test integrity rules
- [ ] Create `AppUserClaimsTransformationTests` (6 tests: unauthenticated, linked user, email match, no account, inactive user, **double-call idempotency**)
- [ ] Create `RequireAppUserHandlerTests` (2 tests: with claim succeeds, without claim calls `Fail()`)
- [ ] Create `AppUserAuthorizationResultHandlerTests` (2 tests: no-account 403 response with `Response.Body = new MemoryStream()`, delegation to **mock** `IAuthorizationMiddlewareResultHandler`)
- [ ] Verify build, format, and tests pass

#### Phase 2: Decouple Claims Transformation from DbContext

- [ ] Create `IAppUserClaimsProvider` interface and `AppUserClaimsData` record in `Infrastructure/Auth/IAppUserClaimsProvider.cs`
- [ ] Create `AppUserClaimsProvider` implementation in `Infrastructure/Auth/AppUserClaimsProvider.cs` using EF projections (`Select`) -- never materialize `AppUser` entity
- [ ] Register `IAppUserClaimsProvider` as scoped in `Program.cs`
- [ ] Refactor `AppUserClaimsTransformation` constructor: replace `ApplicationDbContext` with `IAppUserClaimsProvider`
- [ ] Refactor `TransformAsync` to use `claimsProvider.GetByIdentityIdAsync()` and `claimsProvider.GetByEmailUnlinkedAsync()` instead of direct DbContext queries
- [ ] Change `bus.InvokeAsync(new CompleteIdentitySetupCommand(...))` to `bus.PublishAsync(new CompleteIdentitySetupCommand(...))` (fire-and-forget)
- [ ] Remove `db.Entry(appUser).ReloadAsync()` call and its explanatory comment (no longer needed)
- [ ] Remove `ApplicationDbContext` and `Microsoft.EntityFrameworkCore` using statements from `AppUserClaimsTransformation.cs`
- [ ] Refactor `AppUserClaimsTransformationTests`: replace `CreateInMemoryDbContext()` with `Substitute.For<IAppUserClaimsProvider>()` mock
- [ ] Update email-match test: remove `bus.When(...).Do(...)` callback, change assertion to `bus.Received(1).PublishAsync(...)`, verify claims from `AppUserClaimsData`
- [ ] Update all other tests to use mock provider instead of seeded InMemory DB
- [ ] Remove unused imports from test file (`Microsoft.EntityFrameworkCore`, `Shadowbrook.Api.Infrastructure.Data`, `Shadowbrook.Domain.AppUserAggregate`, `Shadowbrook.Domain.OrganizationAggregate`)
- [ ] Verify `CompleteIdentitySetupHandler` and its tests are unchanged (handler still uses `IAppUserRepository`)
- [ ] Verify build: `dotnet build shadowbrook.slnx`
- [ ] Verify format: `dotnet format shadowbrook.slnx`
- [ ] Run auth tests: `dotnet test tests/Shadowbrook.Api.Tests/ --filter "FullyQualifiedName~Auth"`
- [ ] Run full suite: `make test`
- [ ] **Pre-merge check:** grep for bare `[Authorize]` endpoints without a named policy and remediate or document
- [ ] Smoke test with `make dev` (including DevAuth role claim verification)

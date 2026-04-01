# Plan: Inactive User Handling in AppUserEnrichmentMiddleware

## Problem

When an authenticated user has `IsActive = false`, `EnrichFromAppUserAsync` skips enrichment
but `InvokeAsync` still calls `next(context)`. The request reaches endpoints unauthenticated
(no permission claims), which fails authorization with a generic 403. The user gets no
indication that their account is deactivated.

Secondary issue: inactive status is never cached, so every request from a deactivated user
hits the database.

## Approach

Short-circuit the request pipeline in `InvokeAsync` when the user is inactive. Return HTTP 403
with a JSON error body matching the existing error response pattern (`{ "error": "..." }`).
Log a warning. Cache the inactive status so repeated requests don't hit the DB.

## Changes

### 1. Modify `AppUserEnrichmentMiddleware.cs`

**File:** `src/backend/Shadowbrook.Api/Infrastructure/Auth/AppUserEnrichmentMiddleware.cs`

#### a. Add `ILogger<AppUserEnrichmentMiddleware>` parameter

Add `ILogger<AppUserEnrichmentMiddleware> logger` to the `InvokeAsync` method signature
(Wolverine/ASP.NET DI resolves method-injected parameters for middleware).

#### b. Change `EnrichFromAppUserAsync` return type to signal inactive status

Change the return type from `Task` to `Task<bool>` where `true` means the user is active
(or unauthenticated/new) and `false` means the user is inactive.

Current inactive handling (lines 58-62):
```csharp
if (!appUser.IsActive)
{
    await db.SaveChangesAsync();
    return;
}
```

Change to:
```csharp
if (!appUser.IsActive)
{
    // Cache inactive status so we don't hit DB on every request
    cache.Set(cacheKey, (EnrichmentData?)null, CacheTtl);
    await db.SaveChangesAsync();
    return false;
}
```

Also handle the cached-null case. Currently (lines 77-79):
```csharp
if (enrichmentData is null)
{
    return;
}
```

This path is hit when the cache contains a null value (inactive user, cached). Change to:
```csharp
if (enrichmentData is null)
{
    return false;
}
```

All other return paths return `true`. The method's final line (after adding claims) returns `true`.

**Important cache concern:** The current cache uses `TryGetValue` which returns `false` for
missing keys but `true` with a null value for explicitly-cached nulls. This is exactly the
behavior we want: cache miss = go to DB, cache hit with null = inactive user.

However, the current code `cache.TryGetValue(cacheKey, out EnrichmentData? enrichmentData)`
will return `true` with `enrichmentData = null` when we cache null. The existing
`if (enrichmentData is null) { return; }` check on line 77 already handles this path --
we just need to change its return value to `false`.

#### c. Short-circuit in `InvokeAsync`

In `InvokeAsync`, capture the return value and short-circuit:

```csharp
if (context.User?.Identity?.IsAuthenticated == true && oid is not null)
{
    var seedAdminEmails = /* ... same as before ... */;
    var isActive = await EnrichFromAppUserAsync(context, db, cache, oid, seedAdminEmails);

    if (!isActive)
    {
        logger.LogWarning("Deactivated user attempted access. IdentityId: {IdentityId}", oid);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = "Your account has been deactivated." });
        return; // Do NOT call next(context)
    }
}

await this.next(context);
```

This matches the existing error response pattern from `DomainExceptionHandler`
(`WriteAsJsonAsync(new { error = message })`).

### 2. Update existing test

**File:** `tests/Shadowbrook.Api.Tests/Auth/AppUserEnrichmentMiddlewareTests.cs`

#### a. Update `InactiveUser_IsNotEnriched` test

The existing test verifies that claims are not added but still expects `next` to be called
(implicitly -- it doesn't check). Update it to verify:

- `next` delegate is **not** called
- Response status code is 403
- Response body contains the deactivation message
- Claims are still not enriched

The test needs a `MemoryStream` assigned to `context.Response.Body` to capture the JSON
response (the default `Stream.Null` on `DefaultHttpContext` silently discards writes).

Add `ILogger<AppUserEnrichmentMiddleware>` to the middleware constructor call -- use
`Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory` to create it, or use
`NullLogger<AppUserEnrichmentMiddleware>.Instance` from the same package (already available
in test projects via the Microsoft.Extensions hosting packages).

Since `InvokeAsync` now takes an `ILogger` parameter, all test calls need updating. Use
`NullLogger<AppUserEnrichmentMiddleware>.Instance` for all existing tests.

### 3. Add new tests

#### b. `InactiveUser_Returns403WithMessage`

Dedicated test that reads the response body JSON and asserts:
- `context.Response.StatusCode == 403`
- Deserialized body contains `{ "error": "Your account has been deactivated." }`

#### c. `InactiveUser_DoesNotCallNext`

Verify the `next` delegate is never invoked (use a boolean flag like the existing
`UnauthenticatedRequest_PassesThroughWithoutEnrichment` test).

These can be combined into the updated `InactiveUser_IsNotEnriched` test if preferred --
one test that checks all three aspects (no claims, 403 status, next not called). The exact
test structure is at the implementer's discretion, but all three assertions must be covered.

#### d. `InactiveUser_CachedStatus_Returns403WithoutDbHit`

Test that a second request for the same inactive user still returns 403 without querying the
DB again. Approach:
- Create inactive user, call middleware once (populates cache)
- Call middleware again with the same OID and a **fresh** DbContext (or clear the user from
  the first DbContext's change tracker)
- Assert 403 on second call
- Optionally verify no new DB query was made (the cache hit means `EnrichFromAppUserAsync`
  returns `false` from the `enrichmentData is null` branch without touching `db`)

## Response Format

```json
{
  "error": "Your account has been deactivated."
}
```

HTTP 403 Forbidden. Matches the existing `{ "error": "..." }` pattern used by
`DomainExceptionHandler`.

## Logging

Log at `Warning` level with the identity ID. This is a security-relevant event (deactivated
user attempting access) that operators should be able to monitor.

```
LogWarning("Deactivated user attempted access. IdentityId: {IdentityId}", oid)
```

## Edge Cases

1. **Newly auto-provisioned user who is somehow inactive:** The `AppUser.Create` factory sets
   `IsActive = true` by default, so a freshly-created user will never hit the inactive path
   in the same request. This is safe.

2. **Race condition with reactivation:** If a user is reactivated while a cached inactive
   status exists, they'll get 403 until the cache TTL (5 minutes) expires. This is acceptable
   -- the TTL is short and the admin can communicate the delay.

3. **Cache invalidation on reactivation:** Not addressed here. A future enhancement could
   evict the cache entry when a user is reactivated, but the 5-minute TTL makes this low
   priority.

## Dev Tasks

### Backend Developer

- [ ] Modify `EnrichFromAppUserAsync` to return `Task<bool>` (active status)
- [ ] Cache inactive status as null in the cache
- [ ] Short-circuit `InvokeAsync` with 403 + JSON body for inactive users
- [ ] Add `ILogger` parameter to `InvokeAsync` and log deactivated access attempts
- [ ] Update existing `InactiveUser_IsNotEnriched` test for new behavior (403, next not called)
- [ ] Add test for cached inactive status returning 403 without DB hit
- [ ] Update all other test methods to pass logger parameter
- [ ] Verify compilation with `dotnet build shadowbrook.slnx`
- [ ] Run tests with `dotnet test tests/Shadowbrook.Api.Tests --filter AppUserEnrichmentMiddleware`

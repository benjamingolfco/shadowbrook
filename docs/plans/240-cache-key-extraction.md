# Cache Key Extraction

## Problem

The enrichment cache key `$"appuser:{identityId}"` is duplicated in 3 places across 2 files. If the key format changes, all locations must be updated manually.

## Occurrences

1. `AppUserEnrichmentMiddleware.cs:48` — `var cacheKey = $"appuser:{oid}";`
2. `AuthEndpoints.cs:169` — `cache.Remove($"appuser:{appUser.IdentityId}");` (UpdateUser)
3. `AuthEndpoints.cs:219` — `cache.Remove($"appuser:{appUser.IdentityId}");` (UpdateUserCourses)

## Approach

Add a static helper method to `AppUserEnrichmentMiddleware` (or a small companion class in the same `Infrastructure/Auth/` folder) that constructs the cache key. The middleware already owns the cache format and TTL, so co-locating the key builder there keeps the concern in one place.

Simplest option: a `public static` method on the middleware class itself:

```csharp
// In AppUserEnrichmentMiddleware.cs
public static string CacheKey(string identityId) => $"appuser:{identityId}";
```

Then update both endpoint call sites to use `AppUserEnrichmentMiddleware.CacheKey(appUser.IdentityId)`.

If the class name feels too long at call sites, an alternative is a small `AppUserCache` static class in the same namespace. Either works — the key constraint is single definition.

## Files to Modify

- **Modify:** `src/backend/Teeforce.Api/Infrastructure/Auth/AppUserEnrichmentMiddleware.cs` — add `CacheKey()` static method, replace inline string on line 48
- **Modify:** `src/backend/Teeforce.Api/Features/Auth/AuthEndpoints.cs` — replace both `cache.Remove(...)` calls (lines 169 and 219) to use the shared method

## Testing

No new tests needed. This is a mechanical refactor with no behavior change. Existing integration tests covering auth endpoints and middleware will validate correctness.

## Dev Tasks

### Backend Developer

- [ ] Add `public static string CacheKey(string identityId)` to `AppUserEnrichmentMiddleware`
- [ ] Replace the 3 inline cache key constructions with calls to the new method
- [ ] Run `dotnet build teeforce.slnx` to verify compilation

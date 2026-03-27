# Feature Flags Design

## Problem

Shadowbrook needs a way to hide incomplete features from production while keeping them testable in non-production environments. In the future, the same system should support tenant-level feature entitlements (controlling what a tenant has access to based on their business contract) without changing the frontend contract or the service interface.

## Design Principles

- **Unified consumption** — the frontend and backend check one service, regardless of how many sources feed into the flag resolution.
- **Enabled by default** — features are on unless explicitly disabled. You only configure what you're gating.
- **Additive sources** — today the only source is app configuration. Tenant entitlements can be added later as a second gate without changing the interface or the endpoint contract.

## Architecture

### Feature Keys

Feature keys are string constants defined in a `FeatureKeys` static class. This serves as the registry of all known features in the system.

```csharp
public static class FeatureKeys
{
    public const string SmsNotifications = "sms-notifications";
    public const string DynamicPricing = "dynamic-pricing";
    // Add new feature keys here as features are developed

    public static readonly string[] All = [SmsNotifications, DynamicPricing];
}
```

Using a static registry means the `GET /api/features` endpoint can return the resolved state of every known feature, not just the ones explicitly configured.

### App-Level Configuration

Feature flags are configured in `appsettings.json` under a `Features` section:

```json
{
  "Features": {
    "sms-notifications": false
  }
}
```

- A feature listed as `false` is disabled in that environment.
- A feature not listed, or listed as `true`, is enabled.
- Non-production environments override via `appsettings.Development.json` or environment variables (`Features__sms-notifications=true`).

This follows standard .NET configuration layering — no custom config providers needed.

### IFeatureService

```csharp
public interface IFeatureService
{
    bool IsEnabled(string featureKey);
}
```

The initial implementation reads only from `IConfiguration`:

```csharp
public class FeatureService(IConfiguration configuration) : IFeatureService
{
    public bool IsEnabled(string featureKey)
    {
        var value = configuration.GetValue<bool?>($"Features:{featureKey}");
        return value ?? true; // enabled by default
    }
}
```

Register as singleton: `builder.Services.AddSingleton<IFeatureService, FeatureService>()`. Singleton is correct since `IConfiguration` is already singleton and there is no per-request state. When tenant entitlements are added (Phase 2), the lifetime must change to scoped since `ICurrentUser` is scoped.

When tenant entitlements are added later, the implementation gains an `ICurrentUser` dependency and checks both gates: `appEnabled && tenantEntitled`. The interface stays the same.

### API Endpoint

`GET /api/features` — Wolverine HTTP endpoint in `Features/FeatureFlags/FeatureEndpoints.cs`, returns the resolved state of all known features for the current request context.

```json
{
  "sms-notifications": false,
  "dynamic-pricing": true
}
```

The endpoint iterates `FeatureKeys.All` and calls `IFeatureService.IsEnabled()` for each. Response is a flat `Dictionary<string, bool>`.

This endpoint is unauthenticated today (no auth system yet). When auth is added, it should be tenant-scoped so the response reflects that tenant's entitlements.

### Frontend

A `useFeatures()` hook in `src/web/src/hooks/use-features.ts` (shared, since feature flags are cross-cutting). Backed by TanStack Query with a key registered in `query-keys.ts`:

```typescript
// query-keys.ts
export const queryKeys = {
  // ...existing keys
  features: ['features'] as const,
};

// hooks/use-features.ts
export function useFeatures() {
  return useQuery({
    queryKey: queryKeys.features,
    queryFn: () => api.get<Record<string, boolean>>('/features'),
    staleTime: Infinity, // features don't change during a session
  });
}
```

Components use it to conditionally render:

```typescript
const { data: features } = useFeatures();

if (features?.['sms-notifications']) {
  // render SMS settings
}
```

A helper can wrap the lookup for cleaner usage:

```typescript
export function useFeature(key: string): boolean {
  const { data: features } = useFeatures();
  return features?.[key] ?? false;
}
```

Note the frontend defaults to `false` (don't show) when features haven't loaded yet, which is the safe default — the opposite of the backend's "enabled by default" since the frontend is gating UI visibility. If the endpoint fails, all gated features are hidden — this is intentionally safe for Phase 1 where flags only gate incomplete work.

### Backend Usage

Endpoints or handlers that need to check a feature flag inject `IFeatureService`:

```csharp
// Example — actual endpoints use Wolverine HTTP conventions
if (!features.IsEnabled(FeatureKeys.Waitlist))
    return Results.NotFound();
```

This keeps incomplete endpoints from being callable in production even if someone discovers the URL.

## Future: Tenant Entitlements

When tenant-level gating is needed, the changes are:

1. Add a `TenantFeature` table (`TenantId`, `FeatureKey`, `Enabled`, `CreatedAt`, `UpdatedAt`).
2. Update `FeatureService` to check both app config and tenant entitlements — a feature must pass both gates.
3. The `GET /api/features` endpoint automatically returns tenant-aware results because it calls the same `IFeatureService`.
4. Build an admin UI for managing tenant entitlements (CRUD on the join table).

No changes to the frontend hook, the endpoint contract, or any backend feature checks. Note: the frontend `staleTime: Infinity` will need to change to support tenant switching or entitlement updates.

## File Placement

| Component | Location |
|-----------|----------|
| `FeatureKeys` | `Features/FeatureFlags/FeatureKeys.cs` |
| `IFeatureService` | `Infrastructure/Services/IFeatureService.cs` |
| `FeatureService` | `Infrastructure/Services/FeatureService.cs` |
| `FeatureEndpoints` | `Features/FeatureFlags/FeatureEndpoints.cs` |
| `useFeatures` / `useFeature` | `src/web/src/hooks/use-features.ts` |
| Query key | `src/web/src/lib/query-keys.ts` (add `features` entry) |

## Scope (Phase 1)

- `FeatureKeys` static class
- `IFeatureService` / `FeatureService` (config-only implementation, singleton)
- `GET /api/features` Wolverine HTTP endpoint
- `useFeatures()` / `useFeature()` hooks
- Unit tests for `FeatureService` (configured vs. default behavior)
- Unit test verifying `FeatureKeys.All` contains all defined constants (reflection)
- Integration test for the endpoint

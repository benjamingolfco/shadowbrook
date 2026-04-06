# Dual-Tenant Authentication Architecture

## Context

Teeforce is a multi-tenant tee time booking platform with two distinct user populations:

- **Customers** (golfers and course operators) — authenticate via an Azure Entra External ID (CIAM) tenant
- **Internal team** (developers, platform admins) — authenticate via the Benjamin Golf Co workforce tenant

The application is deployed on Azure Container Apps. The workforce tenant also handles Azure RBAC for infrastructure access. This spec defines how both populations authenticate to the same API and how the frontend is structured to support this.

## Architecture

```
+-------------------+     +--------------------+
|   Customer App    |     |   Admin Portal     |
|   (React SPA)     |     |   (React SPA)      |
|   CIAM authority  |     |  Workforce auth    |
|   :3000 (local)   |     |  :3001 (local)     |
+--------+----------+     +--------+-----------+
         |                          |
         |   Bearer tokens          |
         v                          v
+-------------------------------------------------+
|              Shared API (.NET)                   |
|  AddPolicyScheme -> ForwardDefaultSelector       |
|  +-- "Ciam" scheme (ciamlogin.com issuer)        |
|  +-- "Workforce" scheme (microsoftonline.com)    |
|                                                  |
|  AppUserClaimsTransformation (both paths)        |
|  Authorization policies (role + permission)      |
+-------------------------------------------------+
```

**Three deployables, one API:**

- **Customer App** — Azure Static Web App, CIAM tenant authority, used by golfers and operators
- **Admin Portal** — separate Azure Static Web App, workforce tenant authority, used by internal team
- **API** — Azure Container App, dual-scheme JWT validation, shared by both SPAs

**Entra tenants:**

| Tenant | Type | Purpose |
|--------|------|---------|
| Benjamin Golf Co | Workforce | Internal team auth, Azure RBAC, admin portal |
| Production CIAM | External ID | Customer auth (golfers + operators) |
| Dev/Test CIAM | External ID | Mirrors production CIAM for non-prod (deferred) |

## API Authentication — Dual-Scheme Setup

The API registers two JWT Bearer handlers behind a policy scheme that inspects the token's issuer before routing to the correct handler.

### Policy Scheme with ForwardDefaultSelector

The `ForwardDefaultSelector` reads the raw JWT and checks the `iss` claim. Tokens from `*.ciamlogin.com` route to the "Ciam" handler; tokens from `login.microsoftonline.com` route to the "Workforce" handler. Only one handler runs per request — no double-validation overhead.

### Configuration

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<workforce-tenant-id>",
    "ClientId": "<workforce-api-app-registration>"
  },
  "ExternalId": {
    "Authority": "https://<tenant>.ciamlogin.com/",
    "ClientId": "<ciam-api-app-registration>"
  }
}
```

Two separate app registrations in two separate tenants, each with their own client ID and audience. The API validates the audience matches the scheme that was selected.

### App Registrations

| App | Tenant | Type | Purpose |
|-----|--------|------|---------|
| Teeforce API (Workforce) | Benjamin Golf Co | API | Validates tokens from admin portal |
| Teeforce API (CIAM) | Production CIAM | API | Validates tokens from customer app |
| Teeforce SPA | Production CIAM | SPA | Customer app client |
| Teeforce Admin Portal | Benjamin Golf Co | SPA | Admin portal client |

### AppUserClaimsTransformation

Works the same for both token issuers. Extracts the `oid` claim, looks up `AppUser` by `IdentityId`, and enriches claims with `app_user_id`, `organization_id`, `role`, and `permissions`. Workforce admins get `AppUser` records just like CIAM users. The `CompleteIdentitySetup` flow works identically for both populations.

The `oid` claim is used as the sole lookup key. While Microsoft recommends `oid` + `tid` for global uniqueness, a GUID collision across two known tenants is not a practical risk. The `ForwardDefaultSelector` already ensures only tokens from the two configured authorities are accepted, preventing rogue-tenant scenarios.

### Future Optimization: Custom Claims

Not built now. Two approaches documented for when per-request DB lookup becomes a performance concern:

**Token issuance time:** CIAM supports a `tokenIssuanceStart` custom authentication extension — an Azure Function called when the token is being built. It queries the Teeforce database for the `AppUserId` and injects it as a claim. The workforce tenant equivalent uses optional claims or Graph API custom attributes.

**User creation time:** Store the `AppUserId` as a custom directory attribute on the Entra user via Graph API when the `AppUser` is created. Map it as a token claim. No external call at login time, but requires Graph API write access to both directories and sync discipline.

Either approach eliminates the per-request database lookup in `AppUserClaimsTransformation`.

## CIAM Tenant — Identity Providers and User Flows

### Identity Providers

| Provider | Type | Users | Setup |
|----------|------|-------|-------|
| Email + password | Local account | All users | Built-in |
| Google | Social | Golfers primarily | OAuth app in Google Developer Console |
| Apple | Social | Golfers primarily | Apple Developer federation |
| Microsoft personal | Social / custom OIDC | Golfers | Custom OIDC provider config |
| Corporate Entra ID (per org) | Custom OIDC | Operators at orgs with their own tenant | One config per federated org |
| Corporate non-Entra (Okta, etc.) | Custom OIDC or SAML | Operators at orgs with other IdPs | One config per federated org |

### Federated Organization Support

Each golf course organization with its own Entra ID (or other OIDC/SAML-compliant IdP) gets a custom identity provider entry in the CIAM tenant:

1. The external org registers an app in their tenant representing the Teeforce CIAM tenant
2. A custom OIDC identity provider is configured in the CIAM tenant with their discovery endpoint
3. The provider is attached to the relevant user flow

There is no documented limit on the number of federated identity providers per CIAM tenant. The practical constraints are operational — each federation is manually configured (admin task, not self-service).

Key limitations:
- **MFA:** Enforced by the federated org's own tenant. CIAM-side MFA registration breaks for Entra-federated users.
- **IdP routing:** No automatic email-domain routing for OIDC providers. Users click their org's IdP button, or the app passes `domain_hint`.
- **Workforce federation not supported:** Microsoft blocks `microsoftonline.com` issuer URIs in CIAM custom OIDC federation. Internal team cannot federate their workforce credentials into the CIAM tenant — they use the admin portal with direct workforce auth instead.

### User Flow Strategy

Since both operators and golfers use the same customer SPA, they share one user flow. The sign-in page shows all configured identity providers. The app determines roles post-authentication.

Limit: 10 user flows per CIAM tenant.

If the single sign-in page becomes cluttered with IdP buttons, a future option: two app registrations in the CIAM tenant pointing to the same SPA — a "golfer" flow (social + email) and an "operator" flow (corporate federation + email), routed by entry point (e.g., `/login` vs `/manage/login`).

### Operator Onboarding

- **Operators with corporate IdPs:** Self-register via their federated credentials. App assigns roles after first login.
- **Operators without corporate IdPs:** Admin creates `AppUser` via the admin portal. Operator self-registers with email+password in CIAM. `CompleteIdentitySetup` links them on first sign-in (existing flow).
- **Self-service sign-up** can be disabled per user flow via Graph API if needed.

### Golfer Onboarding

Standard self-service sign-up via the user flow. Email+password, Google, Apple, or Microsoft personal account.

## Admin Portal

Separate React SPA deployed as its own Azure Static Web App (e.g., `admin.benjamingolf.com`). Authenticates against the workforce tenant via MSAL.js.

### App Registration

New app registration in the Benjamin Golf Co workforce tenant — "Teeforce Admin Portal". SPA type, redirect URI to the admin domain. Requests `access_as_user` scope on the API's workforce app registration.

### Capabilities

Built incrementally:

- **Organization management** — create, edit, deactivate courses; configure tenant settings
- **Cross-tenant visibility** — view tee sheets, bookings, waitlists across all organizations
- **User management** — invite operators, manage roles, activate/deactivate users across orgs
- **Platform operations** — health dashboard, feature flags, configuration overrides

### Authorization Model

Workforce users get `AppUser` records with a new role — `PlatformAdmin`. This role is above `Admin` in the hierarchy:

| Role | Scope | Source |
|------|-------|--------|
| PlatformAdmin | All organizations, platform settings | Workforce tenant users only |
| Admin | Single organization, user management | CIAM tenant users (course owners) |
| Operator | Single organization, tee sheet operations | CIAM tenant users (course staff) |

`AppUserClaimsTransformation` enriches `PlatformAdmin` users with a superset of permissions (e.g., `platform:manage`, `orgs:manage`, plus all existing permissions). The admin portal checks these permissions the same way the customer app does — `hasPermission()`.

**Guard rail:** `PlatformAdmin` role can only be assigned to users authenticating from the workforce tenant. The API enforces this by checking the token's issuer claim during role assignment.

## CSP and Redirect URIs

### Content Security Policy

Each SPA configures CSP headers scoped to its auth provider:

- **Customer App:** `connect-src` and `frame-src` allow `*.ciamlogin.com`
- **Admin Portal:** `connect-src` and `frame-src` allow `login.microsoftonline.com`
- **API:** No CSP changes. The API doesn't serve HTML or initiate browser auth flows.

### Redirect URIs

| App Registration | Tenant | Redirect URIs |
|------------------|--------|---------------|
| Teeforce SPA (Non-Prod) | Dev/Test CIAM | `http://localhost:3000`, `https://<nonprod-customer-domain>` |
| Teeforce SPA (Prod) | Production CIAM | `https://<prod-customer-domain>` |
| Teeforce Admin Portal (Non-Prod) | Workforce | `http://localhost:3001`, `https://<nonprod-admin-domain>` |
| Teeforce Admin Portal (Prod) | Workforce | `https://<prod-admin-domain>` |

## Dev Auth Mode

The existing dev auth bypass (`UseDevAuth=true`) bypasses real token validation entirely, so the dual-scheme setup is irrelevant in dev mode.

Changes needed:

- **Seed data:** Add a `PlatformAdmin` user to seed config (new `Auth:SeedPlatformAdminEmails` key or extend existing seed mechanism)
- **E2E fixtures:** Add a `PlatformAdmin` persona alongside existing admin and operator personas
- **Local ports:** Customer app on `:3000`, admin portal on `:3001`, each with its own Vite config

When `UseDevAuth=false`, local dev uses real MSAL configs — customer app points at the CIAM tenant (or workforce tenant until CIAM exists), admin portal points at the workforce tenant.

## Migration Path

Phased approach — no big bang required:

### Phase 1: Admin Portal (can start now)

Build the admin portal SPA authenticating against the workforce tenant. API continues single-scheme validation against the workforce tenant. Both apps use the same auth infrastructure as today.

### Phase 2: Production CIAM Tenant (before launch)

1. Create the production CIAM tenant
2. Create app registrations (API + SPA) in the CIAM tenant
3. Configure identity providers (email+password, Google, Apple, Microsoft personal)
4. Create sign-up/sign-in user flow
5. Add dual-scheme validation to the API (`AddPolicyScheme` + `ForwardDefaultSelector`)
6. Switch the customer app's MSAL config to the CIAM authority
7. Admin portal continues authenticating against the workforce tenant unchanged

### Phase 3: Dev/Test CIAM Tenant (when building CIAM features)

1. Create the dev/test CIAM tenant mirroring production config
2. Update local dev and test environment configs
3. Full auth topology parity between environments

### Phase 4: Federated Organization Onboarding (as needed)

Configure custom OIDC identity providers in the CIAM tenant for golf course organizations that want federated sign-in. Per-org manual setup.

## Deferred Items

| Item | Trigger to revisit |
|------|-------------------|
| Custom claims at token issuance (eliminate per-request DB lookup) | Performance concern with `AppUserClaimsTransformation` |
| Separate user flows for golfer vs operator sign-in UX | Sign-in page cluttered with IdP buttons |
| Dev/Test CIAM tenant creation | Building CIAM-specific features (self-service sign-up, social logins) |
| `domain_hint` / email-domain discovery for automatic IdP routing | Multiple orgs federated, users confused by IdP selection |
| Self-service IdP onboarding portal for orgs | Scale beyond manual federation config |

## Key Decisions

- **CIAM-only for customers, workforce-only for internal team** — clean separation, no cross-tenant federation needed (which Microsoft blocks anyway)
- **Shared API with dual-scheme validation** — `ForwardDefaultSelector` routes to the correct JWT handler based on issuer, no double-validation
- **`oid` as sole identity lookup key** — collision risk is theoretical; both tenants are known and validated by the policy scheme
- **PlatformAdmin role guarded by issuer** — only workforce-authenticated users can be platform admins
- **Admin portal as separate SPA** — independent deployment, clean MSAL config, scoped CSP headers
- **Phased migration** — admin portal can start today; CIAM adoption doesn't require a big bang

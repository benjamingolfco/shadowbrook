# Azure Entra ID Tenant Architecture

## Tenants

| Tenant | Domain | Tenant ID | Type | Purpose | Status |
|--------|--------|-----------|------|---------|--------|
| Default | `aarongbenjamingmail.onmicrosoft.com` | `1e46c3de-74bc-4844-a66f-b9dda2f55dcf` | Workforce | Holds Azure subscription. Auto-created when Aaron signed up. Will be retired after subscription migration. | Legacy — do not use for apps |
| Benjamin Golf Co | `benjamingolfco.onmicrosoft.com` | `f74e8993-5f31-49cb-8772-a20a7f0cf2b6` | Workforce | Company directory. Team members, Azure RBAC, internal tooling. Shared across all environments. | Active |
| Non-prod CIAM | `benjamingolfcononprod` | `4b11d3bc-d72b-4328-8602-b1cb1b16660c` | External ID (CIAM) | Originally created for non-prod auth. No longer used — replaced by workforce tenant. | Retired |
| Dev/Test CIAM | *(not yet created)* | — | External ID (CIAM) | Non-prod customer-facing auth. Mirrors production CIAM config. Only needed when building/testing CIAM-specific features (self-service sign-up, social logins, user flows). | Planned — defer until needed |
| Production CIAM | *(not yet created)* | — | External ID (CIAM) | Customer-facing auth for production. Golfer self-service sign-up, operator invitations. | Planned |

## Architecture

```
Benjamin Golf Co (workforce tenant) — shared across all environments
├── Users: Aaron, developers (members)
├── Azure RBAC: subscription access, resource management
├── No app registrations for customer-facing auth
└── Access: invite-only, company employees only

Dev/Test CIAM tenant (deferred — create when building self-service sign-up)
├── Users: Test operators (invited), test golfers (self-service)
├── App registrations: Teeforce API + SPA (Non-Prod)
├── Auth: <tenant>.ciamlogin.com
├── Identity providers: Microsoft, Google, email/password
└── Access: mirrors production config for realistic testing

Production CIAM tenant (planned)
├── Users: Course operators (invited), golfers (self-service)
├── App registrations: Teeforce API + SPA (Prod)
├── Auth: <tenant>.ciamlogin.com
├── Identity providers: Microsoft, Google, email/password
└── Access: operators invited manually, golfers self-service
```

### Why separate CIAM tenants per environment?

Workforce and CIAM tenants have fundamentally different auth behavior:
- **Different authority URLs**: `login.microsoftonline.com` (workforce) vs `<tenant>.ciamlogin.com` (CIAM)
- **Different token claims**: issuer, optional claims, and group claims differ between tenant types
- **Different features**: CIAM has user flows (sign-up/sign-in), attribute collection, and branding that workforce tenants don't
- **Different API permission models**: CIAM only supports `offline_access`, `openid`, `User.Read`, and custom API delegated permissions

Testing against a workforce tenant means you're not testing the real auth path. A dev CIAM tenant ensures parity with production. The CIAM free tier covers 50,000 MAU/month, so cost is not a concern.

### Why two tenant types?

| Audience | Tenant type | Access model |
|----------|------------|--------------|
| **Internal team** (devs) | Workforce | Members only. Azure RBAC for infrastructure. |
| **Customers** (operators + golfers) | CIAM | Operators invited manually. Golfers self-service sign-up (future). Social identity providers (Microsoft, Google). |

Operators and golfers are both customers — they belong in the CIAM tenant, not the workforce tenant. The distinction between operators and golfers is an application-layer concern (roles, permissions), not an identity-layer concern. CIAM supports Entra ID federation, so operators with organizational accounts can still use them.

## Current Configuration (Non-Prod)

**Note:** The current non-prod setup uses the workforce tenant for auth. This is fine for invite-only operator testing. Migrate to a dev CIAM tenant when building CIAM-specific features (self-service sign-up, social logins, user flows).

### Workforce Tenant: Benjamin Golf Co

**Users:**

| User | UPN | Type | Role | Object ID |
|------|-----|------|------|-----------|
| Aaron Benjamin | `aarongbenjamin_gmail.com#EXT#@benjamingolfco.onmicrosoft.com` | Member | Global Administrator | `09492d28-89a0-4a6e-a4f7-9a78872e473d` |

The `#EXT#` in the UPN means the identity is backed by a personal Microsoft account — this is normal and expected. UserType is Member with full admin rights.

**App Registrations (temporary — will move to dev CIAM tenant):**

| App | Client ID | Type | Supported Accounts |
|-----|-----------|------|--------------------|
| teeforce-api-test | `c601e2a8-c9fe-4361-8627-2e5634a55040` | API | This directory only |
| teeforce-spa-test | `035f0285-de18-4df7-8d41-19393ecfc8d8` | SPA | This directory only |

- API exposes scope: `api://c601e2a8-c9fe-4361-8627-2e5634a55040/access_as_user`
- SPA has delegated permission to call the API scope (admin consent granted)
- SPA redirect URI: `http://localhost:3000`

### App Configuration

**Backend** (`appsettings.json`):
```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "f74e8993-5f31-49cb-8772-a20a7f0cf2b6",
  "ClientId": "c601e2a8-c9fe-4361-8627-2e5634a55040"
}
```

**Frontend** (`.env.development`):
```
VITE_ENTRA_AUTHORITY=https://login.microsoftonline.com/f74e8993-5f31-49cb-8772-a20a7f0cf2b6
VITE_ENTRA_CLIENT_ID=035f0285-de18-4df7-8d41-19393ecfc8d8
VITE_API_SCOPE=api://c601e2a8-c9fe-4361-8627-2e5634a55040/access_as_user
```

**Note:** When migrated to CIAM, the authority URL will change to `https://<tenant>.ciamlogin.com` and the client IDs will be new app registrations in the CIAM tenant.

## Inviting Users (Current — Workforce Tenant)

To give a developer or test operator access:

1. Entra admin center -> switch to **Benjamin Golf Co** directory
2. **Users** -> **Invite external user** -> enter their email
3. They accept the invitation via email
4. They can now sign in to the app
5. The `AppUserEnrichmentMiddleware` auto-provisions an `AppUser` on first login (Staff role by default)
6. Promote to Admin if needed: update the `Role` column in `AppUsers` or add their email to `Auth:SeedAdminEmails`

### Lockdown (optional, recommended)

For maximum control, enable **user assignment** on the enterprise app:

1. **Enterprise apps** -> find the SPA app -> **Properties** -> **Assignment required? = Yes**
2. **Users and groups** -> assign specific users
3. Only assigned users can sign in

## Remaining Work

### Phase 1: Migrate Azure Subscription (Priority: High)
Move the Azure subscription from the default directory (`1e46c3de`) to the workforce tenant (`f74e8993`):
1. Transfer subscription ownership to the new tenant
2. Re-assign Azure RBAC roles
3. Update managed identity — new one will live in Benjamin Golf Co, so Graph API calls (e.g., user invitations) target the correct tenant
4. Verify all deployed resources still work (Container Apps, SQL, Static Web Apps, etc.)
5. Retire the default directory

### Phase 2: Production CIAM Tenant (Priority: Before launch)
1. Create a new External ID (CIAM) tenant for production
2. Create app registrations (API + SPA) mirroring dev/test
3. Configure identity providers: Microsoft Account, Google, email/password
4. Create sign-up/sign-in user flow:
   - Operators: invited manually (disable self-service sign-up initially)
   - Golfers: self-service sign-up when ready
5. Configure token claims: `oid`, `email`, `displayName`
6. Update production deployment config with new tenant/client IDs
7. Add production redirect URI to SPA app registration

### Phase 3: Dev/Test CIAM Tenant (Priority: When building self-service sign-up)
1. Create a new External ID (CIAM) tenant for dev/test
2. Create app registrations (API + SPA) mirroring production config
3. Configure identity providers: Microsoft Account, Google, email/password
4. Create sign-up/sign-in user flows (operators invited, golfers self-service)
5. Configure token claims: `oid`, `email`, `displayName`
6. Update local/test deployment config with new CIAM tenant/client IDs
7. Migrate non-prod auth from workforce tenant to CIAM tenant

Until then, the workforce tenant with invite-only access is sufficient for non-prod testing.

### Phase 4: Developer Onboarding (Priority: When hiring)
1. Create accounts in the workforce tenant for new developers
2. Assign Azure RBAC roles for resource access
3. Document the onboarding process

## Key Decisions

- **One workforce tenant for all environments** — Benjamin Golf Co is the company directory. It manages developers and Azure RBAC, not customer auth. No need for per-environment workforce tenants.
- **Separate CIAM tenants per environment** — auth behavior differs significantly between workforce and CIAM tenants (authority URLs, token claims, user flows). Testing against a workforce tenant doesn't test the real auth path. Dev CIAM ensures parity with production.
- **Operators and golfers in CIAM, not workforce** — both are customers. The operator vs golfer distinction is an application-layer concern (roles/permissions), not an identity concern. CIAM supports Entra ID federation for operators with organizational accounts.
- **User lookup by `oid`, not email** — the `AppUserEnrichmentMiddleware` identifies users by their Entra Object ID (`oid` claim), which is immutable. Email is stored for display/contact only.
- **`email` claim requires optional claim config** — for guest users in a workforce tenant, add `email` as an optional claim in the API app registration's Token Configuration. Without this, the email may not appear in the token.
- **`mail` property must be set manually** — personal Microsoft accounts don't get the `mail` property set automatically. Use Graph API or the portal to set it.

# Azure Entra ID Tenant Architecture

## Tenants

| Tenant | Domain | Tenant ID | Type | Purpose | Status |
|--------|--------|-----------|------|---------|--------|
| Default | `aarongbenjamingmail.onmicrosoft.com` | `1e46c3de-74bc-4844-a66f-b9dda2f55dcf` | Workforce | Holds Azure subscription. Auto-created when Aaron signed up. Will be retired after subscription migration. | Legacy — do not use for apps |
| Benjamin Golf Co | `benjamingolfco.onmicrosoft.com` | `f74e8993-5f31-49cb-8772-a20a7f0cf2b6` | Workforce | Company directory. Team members, non-prod app auth, internal tooling. | Active |
| Non-prod CIAM | `benjamingolfcononprod` | `4b11d3bc-d72b-4328-8602-b1cb1b16660c` | External ID (CIAM) | Originally created for non-prod auth. No longer used — replaced by workforce tenant. | Retired |
| Production CIAM | *(not yet created)* | — | External ID (CIAM) | Customer-facing auth for production. Golfer self-service sign-up, operator invitations. | Planned |

## Architecture

```
Benjamin Golf Co (workforce tenant)
├── Users: Aaron, developers (members)
├── Invited operators for testing (guests)
├── App registrations: Shadowbrook API + SPA (Non-Prod)
├── Auth: login.microsoftonline.com
└── Access: invite-only, no self-service sign-up

Production CIAM tenant (future)
├── Users: Course operators (invited), golfers (self-service)
├── App registrations: Shadowbrook API + SPA (Prod)
├── Auth: <tenant>.ciamlogin.com
├── Identity providers: Microsoft, Google, email/password
└── Access: operators invited manually, golfers self-service
```

### Why two tenant types?

| Audience | Tenant type | Access model |
|----------|------------|--------------|
| **Internal team** (devs, testing) | Workforce | Members + invited guests. Locked down. |
| **Customers** (operators, golfers) | CIAM | Operators invited manually. Golfers self-service sign-up (future). Social identity providers (Microsoft, Google). |

Non-prod environments use the workforce tenant because only the team needs access. Production uses CIAM because customers need flexible sign-up options and shouldn't be in the company directory.

## Current Configuration (Non-Prod)

### Workforce Tenant: Benjamin Golf Co

**Users:**

| User | UPN | Type | Role | Object ID |
|------|-----|------|------|-----------|
| Aaron Benjamin | `aarongbenjamin_gmail.com#EXT#@benjamingolfco.onmicrosoft.com` | Member | Global Administrator | `09492d28-89a0-4a6e-a4f7-9a78872e473d` |

The `#EXT#` in the UPN means the identity is backed by a personal Microsoft account — this is normal and expected. UserType is Member with full admin rights.

**App Registrations:**

| App | Client ID | Type | Supported Accounts |
|-----|-----------|------|--------------------|
| Shadowbrook API (Non-Prod) | `e3eea6af-dfac-49f5-a3ea-45dc1cf42873` | API | This directory only |
| Shadowbrook SPA (Non-Prod) | `0dc88653-3550-44f0-a0c7-db8ec622e5a9` | SPA | This directory only |

- API exposes scope: `api://e3eea6af-dfac-49f5-a3ea-45dc1cf42873/access_as_user`
- SPA has delegated permission to call the API scope (admin consent granted)
- SPA redirect URI: `http://localhost:3000`

### App Configuration

**Backend** (`appsettings.json`):
```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "f74e8993-5f31-49cb-8772-a20a7f0cf2b6",
  "ClientId": "e3eea6af-dfac-49f5-a3ea-45dc1cf42873"
}
```

**Frontend** (`.env.development`):
```
VITE_ENTRA_AUTHORITY=https://login.microsoftonline.com/f74e8993-5f31-49cb-8772-a20a7f0cf2b6
VITE_ENTRA_CLIENT_ID=0dc88653-3550-44f0-a0c7-db8ec622e5a9
VITE_API_SCOPE=api://e3eea6af-dfac-49f5-a3ea-45dc1cf42873/access_as_user
```

## Inviting Users (Non-Prod)

To give a developer or test operator access:

1. Entra admin center → switch to **Benjamin Golf Co** directory
2. **Users** → **Invite external user** → enter their email
3. They accept the invitation via email
4. They can now sign in to the app
5. The `AppUserEnrichmentMiddleware` auto-provisions an `AppUser` on first login (Staff role by default)
6. Promote to Admin if needed: update the `Role` column in `AppUsers` or add their email to `Auth:SeedAdminEmails`

### Lockdown (optional, recommended)

For maximum control, enable **user assignment** on the enterprise app:

1. **Enterprise apps** → find the SPA app → **Properties** → **Assignment required? = Yes**
2. **Users and groups** → assign specific users
3. Only assigned users can sign in

## Remaining Work

### Phase 1: Migrate Azure Subscription (Priority: Low)
Move the Azure subscription from the default directory (`1e46c3de`) to the workforce tenant (`f74e8993`):
1. Transfer subscription ownership to the new tenant
2. Re-assign Azure RBAC roles
3. Verify all deployed resources still work (Container Apps, SQL, Static Web Apps, etc.)
4. Retire the default directory

### Phase 2: Production CIAM Tenant (Priority: Before launch)
1. Create a new External ID (CIAM) tenant for production
2. Create app registrations (API + SPA) mirroring non-prod
3. Configure identity providers: Microsoft Account, Google, email/password
4. Create sign-up/sign-in user flow:
   - Operators: invited manually (disable self-service sign-up initially)
   - Golfers: self-service sign-up when ready
5. Configure token claims: `oid`, `email`, `displayName`
6. Update production deployment config with new tenant/client IDs
7. Add production redirect URI to SPA app registration

### Phase 3: Developer Onboarding (Priority: When hiring)
1. Create accounts in the workforce tenant for new developers
2. Assign Azure RBAC roles for resource access
3. Document the onboarding process

## Key Decisions

- **Non-prod uses workforce tenant, not CIAM** — CIAM is for customer self-service sign-up, which is the opposite of what we need for dev/test environments.
- **User lookup by `oid`, not email** — the `AppUserEnrichmentMiddleware` identifies users by their Entra Object ID (`oid` claim), which is immutable. Email is stored for display/contact only.
- **`email` claim requires optional claim config** — for guest users in a workforce tenant, add `email` as an optional claim in the API app registration's Token Configuration. Without this, the email may not appear in the token.
- **`mail` property must be set manually** — personal Microsoft accounts don't get the `mail` property set automatically. Use Graph API or the portal to set it.

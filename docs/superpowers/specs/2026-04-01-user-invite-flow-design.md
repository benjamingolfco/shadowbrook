# User Invite Flow Design

**Issue:** #333 — User invite flow: create AppUser records via admin UI and Entra invitation
**Date:** 2026-04-01

## Overview

Replace auto-provisioning of AppUser records with an explicit invite flow. Admins create AppUser records (email + role + org), and users complete their account on first login when the middleware links their Entra identity.

## User Lifecycle

1. **Admin creates AppUser** — enters email, role, and organization (if Operator). No identity ID, no name. `IsActive = false`. Raises `AppUserCreated`.
2. **User receives Entra invitation** — manual via Entra portal for now; future automation via Graph API (#334). Entra sign-up form collects first name and last name.
3. **User authenticates for the first time** — middleware matches by email, dispatches `CompleteIdentitySetupCommand` via `IMessageBus.InvokeAsync`. Handler calls `CompleteIdentitySetup(identityId, firstName, lastName)` which sets `IdentityId`, `FirstName`, `LastName`, `IsActive = true`, and raises `AppUserSetupCompleted`.
4. **Subsequent logins** — middleware matches by `IdentityId` (cached fast path).

## Domain Model Changes

### AppUser Entity

**Remove:**
- `DisplayName` property and column

**Add:**
- `FirstName` (string, nullable) — set during `CompleteIdentitySetup` from `given_name` JWT claim
- `LastName` (string, nullable) — set during `CompleteIdentitySetup` from `family_name` JWT claim

**Modify:**
- `IdentityId` becomes nullable (null at creation, populated on first login)
- `CreateAdmin(email)` — remove `identityId` and `displayName` params, set `IsActive = false`
- `CreateOperator(email, organizationId)` — remove `identityId` and `displayName` params, set `IsActive = false`

**New method — `CompleteIdentitySetup(string identityId, string firstName, string lastName)`:**
- If `IdentityId` is null: sets `IdentityId`, `FirstName`, `LastName`, `IsActive = true`, raises `AppUserSetupCompleted`
- If `IdentityId` matches the provided value: no-op (idempotent)
- If `IdentityId` is set but differs: throws `IdentityAlreadyLinkedException`

### New Domain Events

- `AppUserCreated` — raised by factory methods, carries `AppUserId`, `Email`, `Role`
- `AppUserSetupCompleted` — raised by `CompleteIdentitySetup`, carries `AppUserId`, `Email`

### New Domain Exception

- `IdentityAlreadyLinkedException` — thrown when `CompleteIdentitySetup` is called with a different OID than the one already linked

## Middleware Changes

### AppUserEnrichmentMiddleware — Updated Flow

1. Extract `oid` and `email` from token claims (existing behavior)
2. Try lookup by `IdentityId` (cached fast path) — if found, enrich claims as today
3. Cache miss → try DB lookup by `IdentityId` — if found, cache and enrich
4. No `IdentityId` match → try DB lookup by `Email` where `IdentityId IS NULL` (never cached)
   - If found → dispatch `CompleteIdentitySetupCommand` via `bus.InvokeAsync()`, enrich claims, cache by new `IdentityId`
   - If not found → return 403 with `{ reason: "no_account" }` response body
5. Inactive users with identity already linked: enriched with claims but no permissions (existing behavior preserved)

### CompleteIdentitySetupCommand Handler

A Wolverine handler in `Features/Auth/Handlers/` that:
1. Loads the AppUser by ID via repository
2. Calls `CompleteIdentitySetup(identityId, firstName, lastName)`
3. EF transactional middleware saves the entity and domain events are scraped automatically

Command record: `CompleteIdentitySetupCommand(Guid AppUserId, string IdentityId, string FirstName, string LastName)` — defined in the handler file per colocation convention.

### Why InvokeAsync?

The middleware saves outside the Wolverine pipeline. Using `IMessageBus.InvokeAsync` to dispatch a command ensures the handler runs inside the full Wolverine pipeline — EF transactional middleware saves the entity and domain events get scraped and published automatically.

### New Repository Method

`IAppUserRepository.GetByEmailWithoutIdentityAsync(string email)` — returns an AppUser where `Email` matches and `IdentityId IS NULL`.

## API Endpoint Changes

### POST /auth/users (Create User)

**Request DTO (revised):**
- `Email` (required)
- `Role` (required — Admin or Operator)
- `OrganizationId` (required for Operator, must be null for Admin)

**Removed from request:** `IdentityId`, `DisplayName`

**Behavior:** Creates AppUser via factory method. Returns the created user with `IdentityId: null`, `IsActive: false`.

### PUT /auth/users/{id} (Update User)

**Removed from request:** `IdentityId`

**Behavior:** Activate/deactivate, change role, reassign organization. `IdentityId` is system-managed.

### Validators

Updated to remove `IdentityId` and `DisplayName` validation rules from create and update validators.

## Seed Admin Changes

Move seed admin creation from middleware to app startup:

- On startup, read `Auth:SeedAdminEmails` config
- For each email not already in the database, create an AppUser via `CreateAdmin(email)`
- Seed admins go through the same first-login flow as invited users (`IsActive = false` until `CompleteIdentitySetup`)

Remove seed-admin-specific logic from the middleware.

## Database Changes

**Schema changes (no migration in this issue — handled on issue/240 branch):**
- Drop `DisplayName` column
- Add `FirstName` column (nvarchar(100), nullable)
- Add `LastName` column (nvarchar(100), nullable)
- Make `IdentityId` nullable
- Change unique index on `IdentityId` to filtered index (`WHERE IdentityId IS NOT NULL`)

**Data:** Destructive migration — drop and recreate AppUsers table.

## Unknown/Unauthenticated Users

When an authenticated user has no matching AppUser record:
- Middleware returns HTTP 403 with body `{ reason: "no_account" }`
- Frontend can use this to show a "You haven't been invited yet" message (frontend implementation out of scope)

## Out of Scope

- **Graph API invitation handler** — tracked in #334, subscribes to `AppUserCreated` event
- **Frontend user management pages** — being built on `issue/240-authentication-authorization` branch
- **DevAuthHandler refactor** — works as-is for linked users; simplification is a separate concern
- **Migration** — schema changes applied on issue/240 branch

# Optional Invite & Manual Resend Design

**Date:** 2026-04-05

## Overview

Make Entra invitation sending optional at user creation time and add a manual send/resend action to the users list and user detail pages.

Currently, `AppUserCreated` always triggers `SendEntraInvitationHandler`, which calls `AppUser.Invite()`. This change makes that conditional via a `ShouldSendInvite` flag on the event, and adds a dedicated endpoint for manual (re)invitations.

## Domain Changes

### AppUser.Invite()

Remove the idempotency guard (`if InviteSentAt is not null, return`). The method should always call the invitation service and update `InviteSentAt`, allowing resends of expired or lost invitations.

### AppUserCreated Event

Add `bool ShouldSendInvite` property. The `SendEntraInvitationHandler` checks this flag and skips invitation if `false`.

### Factory Methods

Update `AppUser.CreateAdmin()` and `AppUser.CreateOperator()` to accept a `bool sendInvite` parameter, passed through to the `AppUserCreated` event.

## API Changes

### POST /auth/users — Add sendInvite field

Add optional `bool SendInvite` to `CreateUserRequest`, defaulting to `false`. Passed to the factory method.

### POST /organizations — Add sendInvite field

Add optional `bool SendInvite` to `CreateOrganizationRequest`, defaulting to `false`. Passed to `AppUser.CreateOperator()` for the first operator.

### POST /auth/users/{id}/invite — New endpoint

Loads the user, calls `Invite()`, saves, returns updated `UserListResponse`. Same `RequireUsersManage` authorization. Returns 404 if user not found.

## Frontend Changes

### UserCreate.tsx

Add a "Send Invite" checkbox below the existing form fields, unchecked by default. Include `sendInvite` in the POST body.

### OrgCreate.tsx

Add a "Send Invite" checkbox (labeled to clarify it's for the first operator), unchecked by default. Include `sendInvite` in the POST body.

### UserList.tsx

Add a row action menu (three-dot dropdown) to each row with a single action:
- **"Send Invite"** if `inviteSentAt` is null
- **"Resend Invite"** if `inviteSentAt` is set

Calls `POST /auth/users/{id}/invite`. On success, invalidate the users query to refresh the list.

### UserDetail.tsx

Add a button:
- **"Send Invite"** if `inviteSentAt` is null
- **"Resend Invite"** if `inviteSentAt` is set

Same endpoint. On success, refetch the user data.

## Testing

### Domain unit tests
- `Invite()` sends invitation and sets `InviteSentAt` on first call
- `Invite()` sends invitation and updates `InviteSentAt` on subsequent calls (resend)
- `AppUserCreated` event carries `ShouldSendInvite` flag from factory methods

### Handler unit tests
- `SendEntraInvitationHandler` skips when `ShouldSendInvite` is false
- `SendEntraInvitationHandler` invites when `ShouldSendInvite` is true

### Validator unit tests
- `CreateUserRequest` accepts `sendInvite` as optional boolean
- `CreateOrganizationRequest` accepts `sendInvite` as optional boolean

### Integration tests
- `POST /auth/users` with `sendInvite: false` creates user without invitation
- `POST /auth/users` with `sendInvite: true` creates user with invitation
- `POST /auth/users/{id}/invite` sends invitation and returns updated user
- `POST /auth/users/{id}/invite` on already-invited user resends and updates timestamp

## Out of Scope

- Email notification content customization
- Invitation expiry tracking
- Bulk invite operations

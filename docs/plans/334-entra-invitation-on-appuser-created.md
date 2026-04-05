# Issue #334 — Send Entra External ID Invitation on AppUserCreated

## Summary

When an admin creates an AppUser, the system sends an Entra External ID invitation via Microsoft Graph API, captures the returned Entra object ID as the user's `IdentityId`, and records that the invitation was sent.

## Domain Changes

### `IAppUserInvitationService` (new, in `Domain/Services/`)

```csharp
public interface IAppUserInvitationService
{
    Task<string> SendInvitationAsync(string email, CancellationToken ct = default);
    // Returns the Entra object ID of the invited user
}
```

### `AppUser` Aggregate Changes

**New property:**

- `InviteSentAt` (`DateTimeOffset?`, null = not yet invited)

**New method:**

- `async Task Invite(IAppUserInvitationService invitationService, CancellationToken ct)`
  - Guard: no-op if `IdentityId` is already set or `InviteSentAt` is not null (idempotency)
  - Calls `invitationService.SendInvitationAsync(Email, ct)`
  - Sets `IdentityId` from the returned Entra object ID
  - Sets `InviteSentAt = DateTimeOffset.UtcNow`

**EF migration** required for the new `InviteSentAt` column.

## Infrastructure

### `GraphAppUserInvitationService` (new, in `Api/Infrastructure/Services/`)

- Implements `IAppUserInvitationService`
- Injects `GraphServiceClient` and `IOptions<AppSettings>` (for `FrontendUrl` as redirect URL)
- Calls `POST /invitations` with:
  - `invitedUserEmailAddress` = email
  - `inviteRedirectUrl` = `AppSettings.FrontendUrl`
  - `sendInvitationMessage` = true
- Returns `invitation.InvitedUser.Id` (the Entra object ID — created immediately, even before the user redeems)

### `NoOpAppUserInvitationService` (new, in `Api/Infrastructure/Services/`)

- Dev implementation — logs the invitation details and returns a generated GUID string
- Allows the full flow to complete locally without calling Graph

### `GraphSettings` (new, in `Api/Infrastructure/Configuration/`)

```csharp
public class GraphSettings
{
    public string ClientId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string? ClientSecret { get; init; }  // Optional — managed identity used if absent
}
```

### Authentication

- Uses `DefaultAzureCredential` from `Azure.Identity`
- In production (Container Apps): picks up managed identity automatically
- In local dev with Graph config: uses client secret or `az login` credentials
- If `Graph:ClientId` is not configured: `NoOpAppUserInvitationService` is registered instead

### DI Registration (in `Program.cs`)

- Bind `GraphSettings` from `Graph` config section
- If `Graph:ClientId` is configured → register `GraphAppUserInvitationService` with `GraphServiceClient` using `DefaultAzureCredential`
- If not → register `NoOpAppUserInvitationService`

### NuGet Packages (added to `Teeforce.Api`)

- `Microsoft.Graph`
- `Azure.Identity`

## Wolverine Handler

### `SendEntraInvitationHandler` (new, in `Api/Features/AppUsers/Handlers/AppUserCreated/`)

```csharp
public static class SendEntraInvitationHandler
{
    public static async Task Handle(
        AppUserCreated evt,
        IAppUserRepository appUserRepository,
        IAppUserInvitationService invitationService,
        CancellationToken ct)
    {
        var appUser = await appUserRepository.GetRequiredByIdAsync(evt.AppUserId);
        await appUser.Invite(invitationService, ct);
    }
}
```

- Wolverine auto-discovers via convention
- EF transactional middleware saves the aggregate (with updated `IdentityId` and `InviteSentAt`) after handler completes
- If Graph call fails, Wolverine retry policies handle it

## Azure App Registration

Requires an app registration with `User.Invite.All` application permission (admin-consented). This is a deployment/infra concern, not a code change.

## Testing Strategy

### Domain Unit Tests

- `Invite` sets `IdentityId` and `InviteSentAt` when called with a mock service
- `Invite` is idempotent — second call is a no-op when `InviteSentAt` is already set
- `Invite` is a no-op when `IdentityId` is already set

### Handler Unit Tests

- Handler loads AppUser and calls `Invite`
- Verify `invitationService.SendInvitationAsync` is called with correct email

### Infrastructure Unit Tests

- `NoOpAppUserInvitationService` returns a non-empty string and logs
- `GraphAppUserInvitationService` — test with mocked `GraphServiceClient` if feasible, otherwise cover via integration test

## Config Examples

**appsettings.json (production):**

```json
{
  "Graph": {
    "ClientId": "<app-registration-client-id>",
    "TenantId": "<tenant-id>"
  }
}
```

**appsettings.Development.json (no Graph section = NoOp):**

No `Graph` section needed — `NoOpAppUserInvitationService` is used automatically.

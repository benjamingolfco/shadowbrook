# Entra External ID Invitation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When an AppUser is created, automatically send an Entra External ID invitation via Microsoft Graph API, capturing the returned Entra object ID and recording that the invitation was sent.

**Architecture:** A Wolverine handler subscribes to `AppUserCreated`, loads the aggregate, and calls `AppUser.Invite(IAppUserInvitationService)`. The domain method calls the service to send the invitation, then sets `IdentityId` and `InviteSentAt`. Two implementations of the service exist: `GraphAppUserInvitationService` (calls Microsoft Graph) and `NoOpAppUserInvitationService` (logs and returns a fake ID for local dev). DI registration selects the implementation based on whether Graph config is present.

**Tech Stack:** .NET 10, Microsoft.Graph SDK, Azure.Identity (DefaultAzureCredential), Wolverine event handlers, EF Core migrations, NSubstitute for tests

**Spec:** `docs/plans/334-entra-invitation-on-appuser-created.md`

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `src/backend/Teeforce.Domain/Services/IAppUserInvitationService.cs` | Domain service interface |
| Modify | `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs` | Add `InviteSentAt` property and `Invite()` method |
| Create | `src/backend/Teeforce.Api/Infrastructure/Configuration/GraphSettings.cs` | Strongly-typed Graph config |
| Create | `src/backend/Teeforce.Api/Infrastructure/Services/GraphAppUserInvitationService.cs` | Graph API implementation |
| Create | `src/backend/Teeforce.Api/Infrastructure/Services/NoOpAppUserInvitationService.cs` | Dev no-op implementation |
| Modify | `src/backend/Teeforce.Api/Program.cs` | Register GraphSettings, invitation service, NuGet usings |
| Modify | `src/backend/Teeforce.Api/Teeforce.Api.csproj` | Add Microsoft.Graph and Azure.Identity packages |
| Create | `src/backend/Teeforce.Api/Features/AppUsers/Handlers/AppUserCreated/SendEntraInvitationHandler.cs` | Wolverine event handler |
| Modify | `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/AppUserConfiguration.cs` | Map `InviteSentAt` column |
| Create | EF migration (auto-generated) | Add `InviteSentAt` column to AppUsers table |
| Create | `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserInviteTests.cs` | Domain unit tests for `Invite()` |
| Create | `tests/Teeforce.Api.Tests/Features/AppUsers/Handlers/SendEntraInvitationHandlerTests.cs` | Handler unit tests |

---

### Task 1: Domain Service Interface

**Files:**
- Create: `src/backend/Teeforce.Domain/Services/IAppUserInvitationService.cs`

- [ ] **Step 1: Create the interface**

```csharp
namespace Teeforce.Domain.Services;

public interface IAppUserInvitationService
{
    Task<string> SendInvitationAsync(string email, CancellationToken ct = default);
}
```

- [ ] **Step 2: Verify it compiles**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet build src/backend/Teeforce.Domain/Teeforce.Domain.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/backend/Teeforce.Domain/Services/IAppUserInvitationService.cs
git commit -m "feat(domain): add IAppUserInvitationService interface

Returns the Entra object ID of the invited user.

Closes #334"
```

---

### Task 2: AppUser Aggregate — Add `InviteSentAt` and `Invite()` Method

**Files:**
- Modify: `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs`
- Create: `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserInviteTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserInviteTests.cs`:

```csharp
using NSubstitute;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.Services;

namespace Teeforce.Domain.Tests.AppUserAggregate;

public class AppUserInviteTests
{
    private readonly IAppUserInvitationService invitationService = Substitute.For<IAppUserInvitationService>();

    [Fact]
    public async Task Invite_SetsIdentityIdAndInviteSentAt()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        this.invitationService.SendInvitationAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns("entra-object-id-123");

        await user.Invite(this.invitationService, CancellationToken.None);

        Assert.Equal("entra-object-id-123", user.IdentityId);
        Assert.NotNull(user.InviteSentAt);
        Assert.True(user.InviteSentAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
    }

    [Fact]
    public async Task Invite_CallsServiceWithCorrectEmail()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        this.invitationService.SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("entra-object-id-123");

        await user.Invite(this.invitationService, CancellationToken.None);

        await this.invitationService.Received(1).SendInvitationAsync("admin@example.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invite_WhenAlreadyInvited_IsNoOp()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        this.invitationService.SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("entra-object-id-123");
        await user.Invite(this.invitationService, CancellationToken.None);

        await user.Invite(this.invitationService, CancellationToken.None);

        await this.invitationService.Received(1).SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invite_WhenIdentityAlreadySet_IsNoOp()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        user.CompleteIdentitySetup("existing-oid", "Jane", "Smith");
        this.invitationService.SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("different-oid");

        await user.Invite(this.invitationService, CancellationToken.None);

        await this.invitationService.DidNotReceive().SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet test tests/Teeforce.Domain.Tests/Teeforce.Domain.Tests.csproj --filter "FullyQualifiedName~AppUserInviteTests"`
Expected: Compilation error — `AppUser` has no `Invite` method or `InviteSentAt` property

- [ ] **Step 3: Implement the domain changes**

Modify `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs`:

Add using at top:
```csharp
using Teeforce.Domain.Services;
```

Add property after `CreatedAt`:
```csharp
    public DateTimeOffset? InviteSentAt { get; private set; }
```

Add method after `CompleteIdentitySetup`:
```csharp
    public async Task Invite(IAppUserInvitationService invitationService, CancellationToken ct)
    {
        if (IdentityId is not null || InviteSentAt is not null)
        {
            return;
        }

        var identityId = await invitationService.SendInvitationAsync(Email, ct);
        IdentityId = identityId;
        InviteSentAt = DateTimeOffset.UtcNow;
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet test tests/Teeforce.Domain.Tests/Teeforce.Domain.Tests.csproj --filter "FullyQualifiedName~AppUserInviteTests"`
Expected: 4 passed, 0 failed

- [ ] **Step 5: Run all domain tests to verify no regressions**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet test tests/Teeforce.Domain.Tests/Teeforce.Domain.Tests.csproj`
Expected: All 122+ tests pass

- [ ] **Step 6: Run dotnet format**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet format teeforce.slnx`

- [ ] **Step 7: Commit**

```bash
git add src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserInviteTests.cs
git commit -m "feat(domain): add Invite method and InviteSentAt to AppUser

Invite calls IAppUserInvitationService, captures the returned Entra
object ID as IdentityId, and records InviteSentAt. Idempotent — no-ops
if already invited or identity already set."
```

---

### Task 3: NuGet Packages and GraphSettings Config

**Files:**
- Modify: `src/backend/Teeforce.Api/Teeforce.Api.csproj`
- Create: `src/backend/Teeforce.Api/Infrastructure/Configuration/GraphSettings.cs`

- [ ] **Step 1: Add NuGet packages**

Run:
```bash
cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation
dotnet add src/backend/Teeforce.Api/Teeforce.Api.csproj package Microsoft.Graph
dotnet add src/backend/Teeforce.Api/Teeforce.Api.csproj package Azure.Identity
```

- [ ] **Step 2: Create GraphSettings config class**

Create `src/backend/Teeforce.Api/Infrastructure/Configuration/GraphSettings.cs`:

```csharp
namespace Teeforce.Api.Infrastructure.Configuration;

public class GraphSettings
{
    public const string SectionName = "Graph";
    public string ClientId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string? ClientSecret { get; init; }
}
```

- [ ] **Step 3: Verify it compiles**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet build src/backend/Teeforce.Api/Teeforce.Api.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/backend/Teeforce.Api/Teeforce.Api.csproj src/backend/Teeforce.Api/Infrastructure/Configuration/GraphSettings.cs
git commit -m "chore: add Microsoft.Graph and Azure.Identity packages, GraphSettings config"
```

---

### Task 4: Infrastructure Service Implementations

**Files:**
- Create: `src/backend/Teeforce.Api/Infrastructure/Services/NoOpAppUserInvitationService.cs`
- Create: `src/backend/Teeforce.Api/Infrastructure/Services/GraphAppUserInvitationService.cs`

- [ ] **Step 1: Create the NoOp implementation**

Create `src/backend/Teeforce.Api/Infrastructure/Services/NoOpAppUserInvitationService.cs`:

```csharp
using Teeforce.Domain.Services;

namespace Teeforce.Api.Infrastructure.Services;

public class NoOpAppUserInvitationService(ILogger<NoOpAppUserInvitationService> logger) : IAppUserInvitationService
{
    public Task<string> SendInvitationAsync(string email, CancellationToken ct = default)
    {
        var fakeIdentityId = Guid.CreateVersion7().ToString();
        logger.LogInformation("NoOp invitation for {Email} — assigned fake IdentityId {IdentityId}. Configure Graph settings to send real invitations.", email, fakeIdentityId);
        return Task.FromResult(fakeIdentityId);
    }
}
```

- [ ] **Step 2: Create the Graph implementation**

Create `src/backend/Teeforce.Api/Infrastructure/Services/GraphAppUserInvitationService.cs`:

```csharp
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Teeforce.Api.Infrastructure.Configuration;
using Teeforce.Domain.Services;

namespace Teeforce.Api.Infrastructure.Services;

public class GraphAppUserInvitationService(
    GraphServiceClient graphClient,
    IOptions<AppSettings> appSettings,
    ILogger<GraphAppUserInvitationService> logger) : IAppUserInvitationService
{
    public async Task<string> SendInvitationAsync(string email, CancellationToken ct = default)
    {
        var invitation = new Invitation
        {
            InvitedUserEmailAddress = email,
            InviteRedirectUrl = appSettings.Value.FrontendUrl,
            SendInvitationMessage = true,
        };

        var result = await graphClient.Invitations.PostAsync(invitation, cancellationToken: ct);

        var identityId = result?.InvitedUser?.Id;
        if (identityId is null)
        {
            throw new InvalidOperationException($"Graph invitation for {email} returned no InvitedUser.Id");
        }

        logger.LogInformation("Sent Entra invitation to {Email}, IdentityId: {IdentityId}", email, identityId);
        return identityId;
    }
}
```

- [ ] **Step 3: Verify it compiles**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet build src/backend/Teeforce.Api/Teeforce.Api.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/backend/Teeforce.Api/Infrastructure/Services/NoOpAppUserInvitationService.cs src/backend/Teeforce.Api/Infrastructure/Services/GraphAppUserInvitationService.cs
git commit -m "feat: add Graph and NoOp implementations of IAppUserInvitationService

GraphAppUserInvitationService calls POST /invitations via Microsoft Graph SDK.
NoOpAppUserInvitationService logs and returns a fake ID for local dev."
```

---

### Task 5: DI Registration in Program.cs

**Files:**
- Modify: `src/backend/Teeforce.Api/Program.cs`

- [ ] **Step 1: Add usings and register services**

Add usings at the top of `Program.cs`:
```csharp
using Azure.Identity;
using Microsoft.Graph;
using Teeforce.Domain.Services;
```

Add config binding after the existing `Configure<AppSettings>` line (line 72):
```csharp
builder.Services.Configure<GraphSettings>(builder.Configuration.GetSection(GraphSettings.SectionName));
```

Add invitation service registration after the `IAppUserRepository` registration (after line 139):
```csharp
var graphSettings = builder.Configuration.GetSection(GraphSettings.SectionName).Get<GraphSettings>();
if (!string.IsNullOrEmpty(graphSettings?.ClientId))
{
    var credential = string.IsNullOrEmpty(graphSettings.ClientSecret)
        ? new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = graphSettings.TenantId })
        : new ClientSecretCredential(graphSettings.TenantId, graphSettings.ClientId, graphSettings.ClientSecret);

    builder.Services.AddSingleton(_ => new GraphServiceClient(credential));
    builder.Services.AddScoped<IAppUserInvitationService, GraphAppUserInvitationService>();
}
else
{
    builder.Services.AddScoped<IAppUserInvitationService, NoOpAppUserInvitationService>();
}
```

- [ ] **Step 2: Verify it compiles**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet build src/backend/Teeforce.Api/Teeforce.Api.csproj`
Expected: Build succeeded

- [ ] **Step 3: Run dotnet format**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet format teeforce.slnx`

- [ ] **Step 4: Commit**

```bash
git add src/backend/Teeforce.Api/Program.cs
git commit -m "feat: register IAppUserInvitationService with Graph/NoOp selection

Selects GraphAppUserInvitationService when Graph:ClientId is configured,
NoOpAppUserInvitationService otherwise. Uses DefaultAzureCredential for
managed identity in prod, ClientSecretCredential as fallback."
```

---

### Task 6: Wolverine Event Handler

**Files:**
- Create: `src/backend/Teeforce.Api/Features/AppUsers/Handlers/AppUserCreated/SendEntraInvitationHandler.cs`
- Create: `tests/Teeforce.Api.Tests/Features/AppUsers/Handlers/SendEntraInvitationHandlerTests.cs`

- [ ] **Step 1: Write the failing handler tests**

Create `tests/Teeforce.Api.Tests/Features/AppUsers/Handlers/SendEntraInvitationHandlerTests.cs`:

```csharp
using NSubstitute;
using Teeforce.Api.Features.AppUsers.Handlers.AppUserCreated;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.AppUserAggregate.Events;
using Teeforce.Domain.Services;

namespace Teeforce.Api.Tests.Features.AppUsers.Handlers;

public class SendEntraInvitationHandlerTests
{
    private readonly IAppUserRepository appUserRepo = Substitute.For<IAppUserRepository>();
    private readonly IAppUserInvitationService invitationService = Substitute.For<IAppUserInvitationService>();

    [Fact]
    public async Task Handle_LoadsAppUserAndCallsInvite()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        this.appUserRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        this.invitationService.SendInvitationAsync("admin@example.com", Arg.Any<CancellationToken>())
            .Returns("entra-oid-456");
        var evt = new AppUserCreatedEvent { AppUserId = user.Id, Email = user.Email, Role = user.Role };

        await SendEntraInvitationHandler.Handle(evt, this.appUserRepo, this.invitationService, CancellationToken.None);

        await this.invitationService.Received(1).SendInvitationAsync("admin@example.com", Arg.Any<CancellationToken>());
        Assert.Equal("entra-oid-456", user.IdentityId);
        Assert.NotNull(user.InviteSentAt);
    }
}
```

Note: The event variable name `evt` uses `AppUserCreatedEvent` as a placeholder — the actual type is `Teeforce.Domain.AppUserAggregate.Events.AppUserCreated`. Adjust the using/type name when implementing to match the real event record. The test uses `GetByIdAsync` because `GetRequiredByIdAsync` is an extension method on `IRepository<T>` — stub the underlying `GetByIdAsync` to return the user so the extension works.

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet test tests/Teeforce.Api.Tests/Teeforce.Api.Tests.csproj --filter "FullyQualifiedName~SendEntraInvitationHandlerTests"`
Expected: Compilation error — `SendEntraInvitationHandler` does not exist

- [ ] **Step 3: Create the handler**

Create `src/backend/Teeforce.Api/Features/AppUsers/Handlers/AppUserCreated/SendEntraInvitationHandler.cs`:

```csharp
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.AppUserAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.Services;

namespace Teeforce.Api.Features.AppUsers.Handlers.AppUserCreated;

public static class SendEntraInvitationHandler
{
    public static async Task Handle(
        Events.AppUserCreated evt,
        IAppUserRepository appUserRepository,
        IAppUserInvitationService invitationService,
        CancellationToken ct)
    {
        var appUser = await appUserRepository.GetRequiredByIdAsync(evt.AppUserId, ct);
        await appUser.Invite(invitationService, ct);
    }
}
```

Note: The `Events.AppUserCreated` qualified name avoids ambiguity with the `AppUserCreated` folder name. Adjust the using/namespace as needed to resolve correctly. The handler uses `GetRequiredByIdAsync` (extension from `RepositoryExtensions`) which throws `EntityNotFoundException` if the AppUser doesn't exist — this is the correct pattern per backend conventions.

- [ ] **Step 4: Fix the test to use correct types and verify it compiles**

Update the test to use the actual `AppUserCreated` event type from `Teeforce.Domain.AppUserAggregate.Events`:

```csharp
using NSubstitute;
using Teeforce.Api.Features.AppUsers.Handlers.AppUserCreated;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.AppUserAggregate.Events;
using Teeforce.Domain.Services;
using AppUserCreatedEvent = Teeforce.Domain.AppUserAggregate.Events.AppUserCreated;

namespace Teeforce.Api.Tests.Features.AppUsers.Handlers;

public class SendEntraInvitationHandlerTests
{
    private readonly IAppUserRepository appUserRepo = Substitute.For<IAppUserRepository>();
    private readonly IAppUserInvitationService invitationService = Substitute.For<IAppUserInvitationService>();

    [Fact]
    public async Task Handle_LoadsAppUserAndCallsInvite()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        this.appUserRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        this.invitationService.SendInvitationAsync("admin@example.com", Arg.Any<CancellationToken>())
            .Returns("entra-oid-456");
        var evt = new AppUserCreatedEvent { AppUserId = user.Id, Email = user.Email, Role = user.Role };

        await SendEntraInvitationHandler.Handle(evt, this.appUserRepo, this.invitationService, CancellationToken.None);

        await this.invitationService.Received(1).SendInvitationAsync("admin@example.com", Arg.Any<CancellationToken>());
        Assert.Equal("entra-oid-456", user.IdentityId);
        Assert.NotNull(user.InviteSentAt);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet test tests/Teeforce.Api.Tests/Teeforce.Api.Tests.csproj --filter "FullyQualifiedName~SendEntraInvitationHandlerTests"`
Expected: 1 passed, 0 failed

- [ ] **Step 6: Run dotnet format**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet format teeforce.slnx`

- [ ] **Step 7: Commit**

```bash
git add src/backend/Teeforce.Api/Features/AppUsers/Handlers/AppUserCreated/SendEntraInvitationHandler.cs tests/Teeforce.Api.Tests/Features/AppUsers/Handlers/SendEntraInvitationHandlerTests.cs
git commit -m "feat: add SendEntraInvitationHandler for AppUserCreated event

Wolverine handler loads AppUser and calls Invite() which sends the
Entra invitation and captures the identity ID."
```

---

### Task 7: EF Configuration and Migration

**Files:**
- Modify: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/AppUserConfiguration.cs`
- Auto-generated: EF migration files

- [ ] **Step 1: Add InviteSentAt to EF configuration**

In `AppUserConfiguration.cs`, add after the `builder.Property(u => u.CreatedAt);` line (line 23):

```csharp
        builder.Property(u => u.InviteSentAt).IsRequired(false);
```

- [ ] **Step 2: Verify it compiles**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet build src/backend/Teeforce.Api/Teeforce.Api.csproj`
Expected: Build succeeded

- [ ] **Step 3: Generate the EF migration**

Run:
```bash
export PATH="$PATH:/home/aaron/.dotnet/tools"
cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation
dotnet ef migrations add AddAppUserInviteSentAt --project src/backend/Teeforce.Api
```
Expected: Migration files created in `src/backend/Teeforce.Api/Infrastructure/Data/Migrations/`

- [ ] **Step 4: Verify no pending model changes**

Run:
```bash
cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation
dotnet ef migrations has-pending-model-changes --project src/backend/Teeforce.Api
```
Expected: No pending model changes

- [ ] **Step 5: Commit**

```bash
git add src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/AppUserConfiguration.cs src/backend/Teeforce.Api/Infrastructure/Data/Migrations/
git commit -m "feat: add InviteSentAt column to AppUsers table

EF Core migration adds nullable DateTimeOffset column for tracking
when the Entra invitation was sent."
```

---

### Task 8: Smoke Test — Verify Full Build and All Tests Pass

- [ ] **Step 1: Build the full solution**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet build src/backend/Teeforce.Api/Teeforce.Api.csproj && dotnet build tests/Teeforce.Domain.Tests/Teeforce.Domain.Tests.csproj && dotnet build tests/Teeforce.Api.Tests/Teeforce.Api.Tests.csproj`
Expected: All projects build successfully

- [ ] **Step 2: Run all domain tests**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet test tests/Teeforce.Domain.Tests/Teeforce.Domain.Tests.csproj`
Expected: All tests pass (122+ existing + 4 new)

- [ ] **Step 3: Run all API unit tests**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet test tests/Teeforce.Api.Tests/Teeforce.Api.Tests.csproj`
Expected: All tests pass

- [ ] **Step 4: Run make dev to verify runtime startup**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && make dev`
Expected: API starts on :5221 without errors, logs show "NoOp invitation" messages if seed admin users are created

- [ ] **Step 5: Run dotnet format one final time**

Run: `cd /home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/issue-334-graph-invitation && dotnet format teeforce.slnx`

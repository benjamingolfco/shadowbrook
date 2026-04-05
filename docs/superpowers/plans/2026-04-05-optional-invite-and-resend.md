# Optional Invite & Manual Resend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Entra invitation sending optional at user creation and add manual send/resend invite actions on the users list and user detail pages.

**Architecture:** Add `ShouldSendInvite` flag to `AppUserCreated` event, remove idempotency guard from `AppUser.Invite()`, add `POST /auth/users/{id}/invite` endpoint, and add invite UI controls to both create forms and user management pages.

**Tech Stack:** .NET 10 (Wolverine HTTP, FluentValidation, EF Core), React 19 (TanStack Query, React Hook Form, Zod, shadcn/ui)

---

### Task 1: Remove idempotency guard from AppUser.Invite()

**Files:**
- Modify: `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs:129-146`
- Modify: `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserInviteTests.cs`

- [ ] **Step 1: Update the existing "already invited" test to expect resend behavior**

In `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserInviteTests.cs`, replace the `Invite_WhenAlreadyInvited_IsNoOp` test:

```csharp
[Fact]
public async Task Invite_WhenAlreadyInvited_ResendsInvitation()
{
    var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
    this.invitationService.SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns("entra-object-id-123");
    await user.Invite(this.invitationService, CancellationToken.None);
    var firstInviteSentAt = user.InviteSentAt;
    user.ClearDomainEvents();

    this.invitationService.SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns("entra-object-id-456");
    await user.Invite(this.invitationService, CancellationToken.None);

    await this.invitationService.Received(2).SendInvitationAsync("admin@example.com", Arg.Any<CancellationToken>());
    Assert.Equal("entra-object-id-456", user.IdentityId);
    Assert.NotNull(user.InviteSentAt);
    Assert.True(user.InviteSentAt >= firstInviteSentAt);
    var evt = Assert.Single(user.DomainEvents);
    Assert.IsType<AppUserInvited>(evt);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter "FullyQualifiedName~Invite_WhenAlreadyInvited_ResendsInvitation" -v m`

Expected: FAIL — current `Invite()` returns early when already invited.

- [ ] **Step 3: Remove the idempotency guard from Invite()**

In `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs`, replace the `Invite` method:

```csharp
public async Task Invite(IAppUserInvitationService invitationService, CancellationToken ct)
{
    var identityId = await invitationService.SendInvitationAsync(Email, ct);
    IdentityId = identityId;
    InviteSentAt = DateTimeOffset.UtcNow;

    AddDomainEvent(new AppUserInvited
    {
        AppUserId = Id,
        Email = Email,
        EntraObjectId = identityId,
    });
}
```

- [ ] **Step 4: Remove the Invite_WhenIdentityAlreadySet_IsNoOp test**

This test validates the old guard behavior where `Invite()` was a no-op if `IdentityId` was already set. Since `Invite()` now always sends, this test no longer reflects intended behavior. The user explicitly requested that `Invite()` allow multiple calls.

Delete the `Invite_WhenIdentityAlreadySet_IsNoOp` test from `AppUserInviteTests.cs`.

- [ ] **Step 5: Run all AppUser tests to verify they pass**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter "FullyQualifiedName~AppUser" -v m`

Expected: All pass.

- [ ] **Step 6: Commit**

```bash
git add src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserInviteTests.cs
git commit -m "feat: remove idempotency guard from AppUser.Invite() to allow resends"
```

---

### Task 2: Add ShouldSendInvite flag to AppUserCreated event and factory methods

**Files:**
- Modify: `src/backend/Teeforce.Domain/AppUserAggregate/Events/AppUserCreated.cs`
- Modify: `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs:22-86`
- Modify: `src/backend/Teeforce.Api/Features/AppUsers/Handlers/AppUserCreated/SendEntraInvitationHandler.cs`
- Modify: `tests/Teeforce.Api.Tests/Features/AppUsers/Handlers/SendEntraInvitationHandlerTests.cs`
- Modify: `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserTests.cs`

- [ ] **Step 1: Write handler test for ShouldSendInvite=false**

Add to `tests/Teeforce.Api.Tests/Features/AppUsers/Handlers/SendEntraInvitationHandlerTests.cs`:

```csharp
[Fact]
public async Task Handle_WhenShouldSendInviteIsFalse_SkipsInvitation()
{
    var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
    this.appUserRepo.GetByIdAsync(user.Id).Returns(user);
    var evt = new AppUserCreatedEvent
    {
        AppUserId = user.Id, Email = user.Email, Role = user.Role, ShouldSendInvite = false,
    };

    await SendEntraInvitationHandler.Handle(evt, this.appUserRepo, this.invitationService, CancellationToken.None);

    await this.invitationService.DidNotReceive().SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    Assert.Null(user.InviteSentAt);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~Handle_WhenShouldSendInviteIsFalse" -v m`

Expected: FAIL — `ShouldSendInvite` property doesn't exist on `AppUserCreated` yet.

- [ ] **Step 3: Add ShouldSendInvite to AppUserCreated event**

In `src/backend/Teeforce.Domain/AppUserAggregate/Events/AppUserCreated.cs`:

```csharp
using Teeforce.Domain.Common;

namespace Teeforce.Domain.AppUserAggregate.Events;

public record AppUserCreated : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid AppUserId { get; init; }
    public required string Email { get; init; }
    public required AppUserRole Role { get; init; }
    public bool ShouldSendInvite { get; init; }
}
```

- [ ] **Step 4: Update factory methods to accept sendInvite parameter**

In `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs`, update `CreateAdmin`:

```csharp
public static async Task<AppUser> CreateAdmin(
    string email,
    IAppUserEmailUniquenessChecker emailChecker,
    bool sendInvite = false,
    CancellationToken ct = default)
{
    if (await emailChecker.IsEmailInUse(email.Trim(), ct))
    {
        throw new DuplicateEmailException(email);
    }

    var user = new AppUser
    {
        Id = Guid.CreateVersion7(),
        Email = email.Trim(),
        Role = AppUserRole.Admin,
        OrganizationId = null,
        IsActive = false,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    user.AddDomainEvent(new AppUserCreated
    {
        AppUserId = user.Id,
        Email = user.Email,
        Role = user.Role,
        ShouldSendInvite = sendInvite,
    });

    return user;
}
```

Update `CreateOperator`:

```csharp
public static async Task<AppUser> CreateOperator(
    string email,
    Guid organizationId,
    IAppUserEmailUniquenessChecker emailChecker,
    bool sendInvite = false,
    CancellationToken ct = default)
{
    if (organizationId == Guid.Empty)
    {
        throw new EmptyOrganizationIdException();
    }

    if (await emailChecker.IsEmailInUse(email.Trim(), ct))
    {
        throw new DuplicateEmailException(email);
    }

    var user = new AppUser
    {
        Id = Guid.CreateVersion7(),
        Email = email.Trim(),
        Role = AppUserRole.Operator,
        OrganizationId = organizationId,
        IsActive = false,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    user.AddDomainEvent(new AppUserCreated
    {
        AppUserId = user.Id,
        Email = user.Email,
        Role = user.Role,
        ShouldSendInvite = sendInvite,
    });

    return user;
}
```

- [ ] **Step 5: Update handler to check ShouldSendInvite**

In `src/backend/Teeforce.Api/Features/AppUsers/Handlers/AppUserCreated/SendEntraInvitationHandler.cs`:

```csharp
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.Common;
using Teeforce.Domain.Services;
using AppUserCreatedEvent = Teeforce.Domain.AppUserAggregate.Events.AppUserCreated;

namespace Teeforce.Api.Features.AppUsers.Handlers.AppUserCreated;

public static class SendEntraInvitationHandler
{
    public static async Task Handle(
        AppUserCreatedEvent evt,
        IAppUserRepository appUserRepository,
        IAppUserInvitationService invitationService,
        CancellationToken ct)
    {
        if (!evt.ShouldSendInvite)
        {
            return;
        }

        var appUser = await appUserRepository.GetRequiredByIdAsync(evt.AppUserId);
        await appUser.Invite(invitationService, ct);
    }
}
```

- [ ] **Step 6: Update existing handler test to set ShouldSendInvite = true**

In `tests/Teeforce.Api.Tests/Features/AppUsers/Handlers/SendEntraInvitationHandlerTests.cs`, update the existing `Handle_LoadsAppUserAndCallsInvite` test event construction:

```csharp
var evt = new AppUserCreatedEvent
{
    AppUserId = user.Id, Email = user.Email, Role = user.Role, ShouldSendInvite = true,
};
```

- [ ] **Step 7: Update domain tests that assert on AppUserCreated event**

In `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserTests.cs`, update the `CreateAdmin_SetsAdminRoleAndNullOrganizationId` test — add assertion for `ShouldSendInvite`:

After the existing `Assert.Contains(user.DomainEvents, e => e is AppUserCreated);` line, add:

```csharp
var createdEvent = user.DomainEvents.OfType<AppUserCreated>().Single();
Assert.False(createdEvent.ShouldSendInvite);
```

Update `CreateOperator_SetsOperatorRoleAndOrganizationId` similarly:

```csharp
var createdEvent = user.DomainEvents.OfType<AppUserCreated>().Single();
Assert.False(createdEvent.ShouldSendInvite);
```

- [ ] **Step 8: Add domain tests for sendInvite=true**

Add to `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserTests.cs`:

```csharp
[Fact]
public async Task CreateAdmin_WithSendInvite_SetsEventFlag()
{
    var user = await AppUser.CreateAdmin("admin@example.com", NewChecker(), sendInvite: true);

    var createdEvent = user.DomainEvents.OfType<AppUserCreated>().Single();
    Assert.True(createdEvent.ShouldSendInvite);
}

[Fact]
public async Task CreateOperator_WithSendInvite_SetsEventFlag()
{
    var user = await AppUser.CreateOperator("op@example.com", Guid.CreateVersion7(), NewChecker(), sendInvite: true);

    var createdEvent = user.DomainEvents.OfType<AppUserCreated>().Single();
    Assert.True(createdEvent.ShouldSendInvite);
}
```

- [ ] **Step 9: Run all tests**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter "FullyQualifiedName~AppUser" -v m && dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~SendEntraInvitation" -v m`

Expected: All pass.

- [ ] **Step 10: Run dotnet build and dotnet format**

Run: `dotnet build teeforce.slnx && dotnet format teeforce.slnx`

Expected: Build succeeds, no format issues.

- [ ] **Step 11: Commit**

```bash
git add src/backend/Teeforce.Domain/AppUserAggregate/Events/AppUserCreated.cs src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs src/backend/Teeforce.Api/Features/AppUsers/Handlers/AppUserCreated/SendEntraInvitationHandler.cs tests/Teeforce.Api.Tests/Features/AppUsers/Handlers/SendEntraInvitationHandlerTests.cs tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserTests.cs
git commit -m "feat: add ShouldSendInvite flag to AppUserCreated event and factory methods"
```

---

### Task 3: Add sendInvite to API create endpoints

**Files:**
- Modify: `src/backend/Teeforce.Api/Features/Auth/AuthEndpoints.cs:115-141,224-252`
- Modify: `src/backend/Teeforce.Api/Features/Organizations/OrganizationEndpoints.cs:55-69,91,94-101`

- [ ] **Step 1: Update CreateUserRequest record**

In `src/backend/Teeforce.Api/Features/Auth/AuthEndpoints.cs`, replace the record:

```csharp
public sealed record CreateUserRequest(
    string Email,
    string Role,
    Guid? OrganizationId,
    bool SendInvite = false);
```

- [ ] **Step 2: Pass sendInvite to factory methods in CreateUser endpoint**

In the `CreateUser` method, update the factory calls:

```csharp
var appUser = role == AppUserRole.Admin
    ? await AppUser.CreateAdmin(request.Email, emailChecker, request.SendInvite)
    : await AppUser.CreateOperator(request.Email, request.OrganizationId!.Value, emailChecker, request.SendInvite);
```

- [ ] **Step 3: Update CreateOrganizationRequest record**

In `src/backend/Teeforce.Api/Features/Organizations/OrganizationEndpoints.cs`, replace the record:

```csharp
public sealed record CreateOrganizationRequest(string Name, string OperatorEmail, bool SendInvite = false);
```

- [ ] **Step 4: Pass sendInvite in CreateOrganization endpoint**

In the `CreateOrganization` method, update the `CreateOperator` call:

```csharp
var appUser = await AppUser.CreateOperator(request.OperatorEmail, org.Id, emailChecker, request.SendInvite);
```

- [ ] **Step 5: Run dotnet build and dotnet format**

Run: `dotnet build teeforce.slnx && dotnet format teeforce.slnx`

Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/backend/Teeforce.Api/Features/Auth/AuthEndpoints.cs src/backend/Teeforce.Api/Features/Organizations/OrganizationEndpoints.cs
git commit -m "feat: add sendInvite field to create user and create organization endpoints"
```

---

### Task 4: Add POST /auth/users/{id}/invite endpoint

**Files:**
- Modify: `src/backend/Teeforce.Api/Features/Auth/AuthEndpoints.cs`

- [ ] **Step 1: Add the invite endpoint**

Add the following method to the `AuthEndpoints` class in `src/backend/Teeforce.Api/Features/Auth/AuthEndpoints.cs`, after the `UpdateUser` method:

```csharp
[WolverinePost("/auth/users/{id}/invite")]
[Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
public static async Task<IResult> InviteUser(
    Guid id,
    [NotBody] ApplicationDbContext db,
    [NotBody] IAppUserInvitationService invitationService,
    CancellationToken ct)
{
    var appUser = await db.AppUsers
        .FirstOrDefaultAsync(u => u.Id == id, ct);

    if (appUser is null)
    {
        return Results.NotFound();
    }

    await appUser.Invite(invitationService, ct);

    var response = new UserListResponse(
        appUser.Id,
        appUser.Email,
        appUser.FirstName,
        appUser.LastName,
        appUser.Role.ToString(),
        appUser.OrganizationId,
        appUser.IsActive,
        appUser.InviteSentAt);

    return Results.Ok(response);
}
```

- [ ] **Step 2: Add IAppUserInvitationService using**

The file already imports `Teeforce.Domain.Services` (line 6), so `IAppUserInvitationService` is available. No new using needed.

- [ ] **Step 3: Run dotnet build and dotnet format**

Run: `dotnet build teeforce.slnx && dotnet format teeforce.slnx`

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/backend/Teeforce.Api/Features/Auth/AuthEndpoints.cs
git commit -m "feat: add POST /auth/users/{id}/invite endpoint for manual invite/resend"
```

---

### Task 5: Add Send Invite checkbox to UserCreate form

**Files:**
- Modify: `src/web/src/features/admin/pages/UserCreate.tsx`
- Modify: `src/web/src/features/admin/hooks/useUsers.ts`

- [ ] **Step 1: Update useCreateUser mutation to include sendInvite**

In `src/web/src/features/admin/hooks/useUsers.ts`, update the `useCreateUser` mutation:

```typescript
export function useCreateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: {
      email: string;
      role: string;
      organizationId: string | null;
      sendInvite: boolean;
    }) => api.post<UserListItem>('/auth/users', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
    },
  });
}
```

- [ ] **Step 2: Add sendInvite to the form schema and UI**

In `src/web/src/features/admin/pages/UserCreate.tsx`:

Add `Checkbox` to imports:

```typescript
import { Checkbox } from '@/components/ui/checkbox';
```

Update the Zod schema to include `sendInvite`:

```typescript
const userSchema = z
  .object({
    email: z.string().email('Invalid email address'),
    role: z.enum(['Admin', 'Operator']),
    organizationId: z.string().optional(),
    sendInvite: z.boolean(),
  })
  .check((ctx) => {
    if (ctx.value.role === 'Operator' && !ctx.value.organizationId) {
      ctx.issues.push({
        code: 'custom',
        input: ctx.value,
        message: 'Organization is required for Operator role',
        path: ['organizationId'],
      });
    }
  });
```

Update `defaultValues` to include `sendInvite: false`:

```typescript
defaultValues: {
  email: '',
  role: 'Operator',
  organizationId: '',
  sendInvite: false,
},
```

Update `onSubmit` to pass `sendInvite`:

```typescript
function onSubmit(data: UserFormData) {
  createUser.mutate(
    {
      email: data.email,
      role: data.role,
      organizationId: data.organizationId ?? null,
      sendInvite: data.sendInvite,
    },
    {
      onSuccess: () => {
        void navigate('/admin/users');
      },
    },
  );
}
```

Add the checkbox field after the Organization field (before the error message block). Place it after the closing `)}` of the conditional organization field and before `{createUser.isError && (`:

```tsx
<FormField
  control={form.control}
  name="sendInvite"
  render={({ field }) => (
    <FormItem className="flex items-center gap-3 space-y-0">
      <FormControl>
        <Checkbox
          checked={field.value}
          onCheckedChange={field.onChange}
        />
      </FormControl>
      <FormLabel className="font-normal">
        Send Invite
      </FormLabel>
    </FormItem>
  )}
/>
```

- [ ] **Step 3: Run lint**

Run: `pnpm --dir src/web lint`

Expected: No errors.

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/admin/pages/UserCreate.tsx src/web/src/features/admin/hooks/useUsers.ts
git commit -m "feat: add Send Invite checkbox to create user form"
```

---

### Task 6: Add Send Invite checkbox to OrgCreate form

**Files:**
- Modify: `src/web/src/features/admin/pages/OrgCreate.tsx`
- Modify: `src/web/src/features/admin/hooks/useOrganizations.ts`

- [ ] **Step 1: Update useCreateOrganization mutation to include sendInvite**

In `src/web/src/features/admin/hooks/useOrganizations.ts`, update the `useCreateOrganization` mutation type:

```typescript
export function useCreateOrganization() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { name: string; operatorEmail: string; sendInvite: boolean }) =>
      api.post<{ id: string; name: string }>('/organizations', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.organizations.all });
    },
  });
}
```

- [ ] **Step 3: Add sendInvite to the form schema and UI**

In `src/web/src/features/admin/pages/OrgCreate.tsx`:

Add `Checkbox` to imports:

```typescript
import { Checkbox } from '@/components/ui/checkbox';
```

Update the schema:

```typescript
const schema = z.object({
  name: z.string().min(1, 'Organization name is required'),
  operatorEmail: z.string().email('Invalid email address'),
  sendInvite: z.boolean(),
});
```

Update `defaultValues`:

```typescript
defaultValues: { name: '', operatorEmail: '', sendInvite: false },
```

Update `onSubmit`:

```typescript
function onSubmit(data: FormData) {
  createMutation.mutate(data, {
    onSuccess: () => {
      navigate('/admin/organizations');
    },
  });
}
```

(No change needed to `onSubmit` if `data` already includes all fields and the mutation accepts the same shape.)

Add the checkbox field after the operator email field and before the error block:

```tsx
<FormField
  control={form.control}
  name="sendInvite"
  render={({ field }) => (
    <FormItem className="flex items-center gap-3 space-y-0">
      <FormControl>
        <Checkbox
          checked={field.value}
          onCheckedChange={field.onChange}
        />
      </FormControl>
      <FormLabel className="font-normal">
        Send Invite to First Operator
      </FormLabel>
    </FormItem>
  )}
/>
```

- [ ] **Step 4: Run lint**

Run: `pnpm --dir src/web lint`

Expected: No errors.

- [ ] **Step 5: Commit**

```bash
git add src/web/src/features/admin/pages/OrgCreate.tsx src/web/src/features/admin/hooks/useOrganizations.ts
git commit -m "feat: add Send Invite checkbox to create organization form"
```

---

### Task 7: Add invite hook and row action menu to UserList

**Files:**
- Modify: `src/web/src/features/admin/hooks/useUsers.ts`
- Modify: `src/web/src/features/admin/pages/UserList.tsx`

- [ ] **Step 1: Add useInviteUser mutation hook**

Add to `src/web/src/features/admin/hooks/useUsers.ts`:

```typescript
export function useInviteUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<UserListItem>(`/auth/users/${id}/invite`, {}),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
    },
  });
}
```

- [ ] **Step 2: Add row action menu to UserList**

In `src/web/src/features/admin/pages/UserList.tsx`:

Update imports — add `useInviteUser` and the dropdown menu components:

```typescript
import { useUsers, useInviteUser } from '../hooks/useUsers';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Button } from '@/components/ui/button';
import { MoreHorizontal } from 'lucide-react';
```

Add the invite mutation inside the component:

```typescript
const inviteUser = useInviteUser();
```

Add a new `<TableHead>` column header at the end of the header row (after the "Invite Sent" column):

```tsx
<TableHead className="w-[50px]"></TableHead>
```

Add a new `<TableCell>` at the end of each row (after the invite sent cell), before `</TableRow>`:

```tsx
<TableCell className="w-[50px]" onClick={(e) => e.stopPropagation()}>
  <DropdownMenu>
    <DropdownMenuTrigger asChild>
      <Button variant="ghost" size="icon" className="h-8 w-8">
        <MoreHorizontal className="h-4 w-4" />
      </Button>
    </DropdownMenuTrigger>
    <DropdownMenuContent align="end">
      <DropdownMenuItem
        onClick={() => inviteUser.mutate(user.id)}
        disabled={inviteUser.isPending}
      >
        {user.inviteSentAt ? 'Resend Invite' : 'Send Invite'}
      </DropdownMenuItem>
    </DropdownMenuContent>
  </DropdownMenu>
</TableCell>
```

The `onClick={(e) => e.stopPropagation()}` prevents the row click (navigate to detail) from firing when clicking the menu.

- [ ] **Step 3: Run lint**

Run: `pnpm --dir src/web lint`

Expected: No errors.

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/admin/hooks/useUsers.ts src/web/src/features/admin/pages/UserList.tsx
git commit -m "feat: add invite/resend action menu to users list rows"
```

---

### Task 8: Add invite button to UserDetail page

**Files:**
- Modify: `src/web/src/features/admin/pages/UserDetail.tsx`

- [ ] **Step 1: Add invite button to UserDetail**

In `src/web/src/features/admin/pages/UserDetail.tsx`:

Add `useInviteUser` to imports:

```typescript
import { useUsers, useUpdateUser, useInviteUser } from '../hooks/useUsers';
```

Add the mutation inside the component (after the `updateUser` line):

```typescript
const inviteUser = useInviteUser();
```

Add the invite button to the button row. In the `<div className="flex gap-4 pt-2">` section, add after the Deactivate/Activate button:

```tsx
<Button
  variant="outline"
  onClick={() => inviteUser.mutate(user.id)}
  disabled={inviteUser.isPending}
>
  {inviteUser.isPending
    ? 'Sending...'
    : user.inviteSentAt
      ? 'Resend Invite'
      : 'Send Invite'}
</Button>
```

- [ ] **Step 2: Add Invite Sent info to the detail view**

In the `<div className="grid grid-cols-1 gap-4">` section, add after the Email block:

```tsx
<div>
  <p className="text-sm font-medium text-muted-foreground">Invite Sent</p>
  <p className="mt-1">
    {user.inviteSentAt
      ? new Date(user.inviteSentAt).toLocaleDateString(undefined, {
          year: 'numeric',
          month: 'short',
          day: 'numeric',
          hour: 'numeric',
          minute: '2-digit',
        })
      : 'Not sent'}
  </p>
</div>
```

- [ ] **Step 3: Run lint**

Run: `pnpm --dir src/web lint`

Expected: No errors.

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/admin/pages/UserDetail.tsx
git commit -m "feat: add invite/resend button to user detail page"
```

---

### Task 9: Verify full build and run

- [ ] **Step 1: Build backend**

Run: `dotnet build teeforce.slnx`

Expected: Build succeeds.

- [ ] **Step 2: Format backend**

Run: `dotnet format teeforce.slnx`

Expected: No changes needed.

- [ ] **Step 3: Lint frontend**

Run: `pnpm --dir src/web lint`

Expected: No errors.

- [ ] **Step 4: Run all backend tests**

Run: `dotnet test tests/Teeforce.Domain.Tests -v m && dotnet test tests/Teeforce.Api.Tests -v m`

Expected: All pass.

- [ ] **Step 5: Run make dev to verify runtime**

Run: `make dev`

Manually verify:
- Create User form shows "Send Invite" checkbox (unchecked by default)
- Create Organization form shows "Send Invite to First Operator" checkbox (unchecked by default)
- Users list shows three-dot menu on each row with "Send Invite" / "Resend Invite"
- User detail shows invite button and invite sent timestamp

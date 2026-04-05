# User Invite Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace auto-provisioning of AppUser records with an explicit invite flow — admins create users by email, users complete their account on first login.

**Architecture:** Domain model changes (nullable IdentityId, FirstName/LastName, CompleteIdentitySetup with domain events), middleware email-fallback linking via Wolverine InvokeAsync, simplified create/update endpoints, seed admin creation at startup.

**Tech Stack:** .NET 10, EF Core 10, WolverineFx, FluentValidation, xUnit, NSubstitute

---

## File Map

| Action | File | Responsibility |
|--------|------|---------------|
| Modify | `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs` | Remove DisplayName, add FirstName/LastName, nullable IdentityId, CompleteIdentitySetup, raise events |
| Create | `src/backend/Teeforce.Domain/AppUserAggregate/Events/AppUserCreated.cs` | Domain event for user creation |
| Create | `src/backend/Teeforce.Domain/AppUserAggregate/Events/AppUserSetupCompleted.cs` | Domain event for identity linking |
| Create | `src/backend/Teeforce.Domain/AppUserAggregate/Exceptions/IdentityAlreadyLinkedException.cs` | Domain exception for mismatched OID |
| Modify | `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/AppUserConfiguration.cs` | Update schema: nullable IdentityId, drop DisplayName, add FirstName/LastName, filtered unique index |
| Create | `src/backend/Teeforce.Api/Features/Auth/Handlers/CompleteIdentitySetup/Handler.cs` | Wolverine handler for CompleteIdentitySetupCommand |
| Modify | `src/backend/Teeforce.Api/Infrastructure/Auth/AppUserEnrichmentMiddleware.cs` | Email fallback lookup, InvokeAsync linking, 403 no_account response |
| Modify | `src/backend/Teeforce.Api/Features/Auth/AuthEndpoints.cs` | Update DTOs, validators, endpoint logic |
| Modify | `src/backend/Teeforce.Api/Infrastructure/Middleware/DomainExceptionHandler.cs` | Add IdentityAlreadyLinkedException mapping |
| Modify | `src/backend/Teeforce.Api/Program.cs` | Add seed admin startup logic |
| Modify | `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserTests.cs` | Update existing tests, add CompleteIdentitySetup tests |
| Create | `tests/Teeforce.Api.Tests/Features/Auth/Handlers/CompleteIdentitySetupHandlerTests.cs` | Handler unit tests |
| Modify | `tests/Teeforce.Api.Tests/Features/Auth/Validators/CreateUserRequestValidatorTests.cs` | Update for new request shape |
| Modify | `tests/Teeforce.Api.Tests/Auth/AppUserEnrichmentMiddlewareTests.cs` | Update for new middleware flow |

---

### Task 1: Domain Events and Exception

**Files:**
- Create: `src/backend/Teeforce.Domain/AppUserAggregate/Events/AppUserCreated.cs`
- Create: `src/backend/Teeforce.Domain/AppUserAggregate/Events/AppUserSetupCompleted.cs`
- Create: `src/backend/Teeforce.Domain/AppUserAggregate/Exceptions/IdentityAlreadyLinkedException.cs`

- [ ] **Step 1: Create AppUserCreated event**

```csharp
// src/backend/Teeforce.Domain/AppUserAggregate/Events/AppUserCreated.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.AppUserAggregate.Events;

public record AppUserCreated : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid AppUserId { get; init; }
    public required string Email { get; init; }
    public required AppUserRole Role { get; init; }
}
```

- [ ] **Step 2: Create AppUserSetupCompleted event**

```csharp
// src/backend/Teeforce.Domain/AppUserAggregate/Events/AppUserSetupCompleted.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.AppUserAggregate.Events;

public record AppUserSetupCompleted : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid AppUserId { get; init; }
    public required string Email { get; init; }
}
```

- [ ] **Step 3: Create IdentityAlreadyLinkedException**

```csharp
// src/backend/Teeforce.Domain/AppUserAggregate/Exceptions/IdentityAlreadyLinkedException.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.AppUserAggregate.Exceptions;

public class IdentityAlreadyLinkedException()
    : DomainException("This user is already linked to a different identity.");
```

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build teeforce.slnx --verbosity quiet`
Expected: Build succeeded, 0 errors

- [ ] **Step 5: Commit**

```bash
git add src/backend/Teeforce.Domain/AppUserAggregate/Events/ src/backend/Teeforce.Domain/AppUserAggregate/Exceptions/IdentityAlreadyLinkedException.cs
git commit -m "feat(domain): add AppUser domain events and IdentityAlreadyLinkedException"
```

---

### Task 2: Update AppUser Domain Model

**Files:**
- Modify: `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs`

- [ ] **Step 1: Write failing tests for new factory methods and CompleteIdentitySetup**

Add these tests to `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserTests.cs`. First, update the existing tests whose signatures change — `CreateAdmin` and `CreateOperator` no longer take `identityId` or `displayName`, and `IsActive` starts as `false`:

```csharp
// Replace the existing CreateAdmin test
[Fact]
public void CreateAdmin_SetsAdminRoleAndNullOrganizationId()
{
    var user = AppUser.CreateAdmin("admin@benjamingolfco.onmicrosoft.com");

    Assert.NotEqual(Guid.Empty, user.Id);
    Assert.Null(user.IdentityId);
    Assert.Equal("admin@benjamingolfco.onmicrosoft.com", user.Email);
    Assert.Null(user.FirstName);
    Assert.Null(user.LastName);
    Assert.Equal(AppUserRole.Admin, user.Role);
    Assert.Null(user.OrganizationId);
    Assert.False(user.IsActive);
    Assert.True(user.CreatedAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
    Assert.Null(user.LastLoginAt);
    Assert.Contains(user.DomainEvents, e => e is AppUserCreated);
}

// Replace the existing CreateOperator test
[Fact]
public void CreateOperator_SetsOperatorRoleAndOrganizationId()
{
    var orgId = Guid.CreateVersion7();
    var user = AppUser.CreateOperator("jane@example.com", orgId);

    Assert.NotEqual(Guid.Empty, user.Id);
    Assert.Null(user.IdentityId);
    Assert.Equal("jane@example.com", user.Email);
    Assert.Null(user.FirstName);
    Assert.Null(user.LastName);
    Assert.Equal(AppUserRole.Operator, user.Role);
    Assert.Equal(orgId, user.OrganizationId);
    Assert.False(user.IsActive);
    Assert.True(user.CreatedAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
    Assert.Null(user.LastLoginAt);
    Assert.Contains(user.DomainEvents, e => e is AppUserCreated);
}
```

Update all other tests that call old factory signatures. Replace every `AppUser.CreateAdmin("oid", "e@e.com", "Test")` with `AppUser.CreateAdmin("e@e.com")` and every `AppUser.CreateOperator("oid", "e@e.com", "Test", orgId)` with `AppUser.CreateOperator("e@e.com", orgId)`. The `Deactivate` test needs adjustment since users now start inactive — create the user, call `CompleteIdentitySetup` first to make it active, then test `Deactivate`:

```csharp
[Fact]
public void Deactivate_SetsIsActiveFalse()
{
    var user = AppUser.CreateOperator("e@e.com", Guid.CreateVersion7());
    user.CompleteIdentitySetup("oid", "Test", "User");

    user.Deactivate();

    Assert.False(user.IsActive);
}

[Fact]
public void Activate_SetsIsActiveTrue()
{
    var user = AppUser.CreateOperator("e@e.com", Guid.CreateVersion7());
    user.CompleteIdentitySetup("oid", "Test", "User");
    user.Deactivate();

    user.Activate();

    Assert.True(user.IsActive);
}
```

Now add tests for `CompleteIdentitySetup`:

```csharp
// Add using at top of file
using Teeforce.Domain.AppUserAggregate.Events;

[Fact]
public void CompleteIdentitySetup_SetsIdentityAndActivates()
{
    var user = AppUser.CreateAdmin("admin@example.com");
    user.ClearDomainEvents();

    user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");

    Assert.Equal("entra-oid-123", user.IdentityId);
    Assert.Equal("Jane", user.FirstName);
    Assert.Equal("Smith", user.LastName);
    Assert.True(user.IsActive);
    Assert.Contains(user.DomainEvents, e => e is AppUserSetupCompleted);
}

[Fact]
public void CompleteIdentitySetup_SameOid_IsIdempotent()
{
    var user = AppUser.CreateAdmin("admin@example.com");
    user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");
    user.ClearDomainEvents();

    user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");

    Assert.Empty(user.DomainEvents);
    Assert.Equal("entra-oid-123", user.IdentityId);
}

[Fact]
public void CompleteIdentitySetup_DifferentOid_Throws()
{
    var user = AppUser.CreateAdmin("admin@example.com");
    user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");

    Assert.Throws<IdentityAlreadyLinkedException>(
        () => user.CompleteIdentitySetup("different-oid", "Jane", "Smith"));
}
```

Also update `CreateOperator_WithEmptyGuid_Throws` and `AssignToOrganization_WithEmptyGuid_Throws`:

```csharp
[Fact]
public void CreateOperator_WithEmptyGuid_Throws() =>
    Assert.Throws<EmptyOrganizationIdException>(() =>
        AppUser.CreateOperator("e@e.com", Guid.Empty));

[Fact]
public void AssignToOrganization_WithEmptyGuid_Throws()
{
    var user = AppUser.CreateAdmin("e@e.com");

    Assert.Throws<EmptyOrganizationIdException>(() => user.AssignToOrganization(Guid.Empty));
}
```

And `RecordLogin`, `MakeAdmin`, `AssignToOrganization`:

```csharp
[Fact]
public void RecordLogin_UpdatesLastLoginAt()
{
    var user = AppUser.CreateOperator("e@e.com", Guid.CreateVersion7());

    user.RecordLogin();

    Assert.NotNull(user.LastLoginAt);
    Assert.True(user.LastLoginAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
}

[Fact]
public void MakeAdmin_SetsAdminRoleAndClearsOrganizationId()
{
    var orgId = Guid.CreateVersion7();
    var user = AppUser.CreateOperator("e@e.com", orgId);

    user.MakeAdmin();

    Assert.Equal(AppUserRole.Admin, user.Role);
    Assert.Null(user.OrganizationId);
}

[Fact]
public void AssignToOrganization_SetsOperatorRoleAndOrganizationId()
{
    var user = AppUser.CreateAdmin("e@e.com");
    var newOrgId = Guid.CreateVersion7();

    user.AssignToOrganization(newOrgId);

    Assert.Equal(AppUserRole.Operator, user.Role);
    Assert.Equal(newOrgId, user.OrganizationId);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter AppUserTests --verbosity quiet`
Expected: FAIL — `CreateAdmin` and `CreateOperator` don't match new signatures, `CompleteIdentitySetup` doesn't exist

- [ ] **Step 3: Update AppUser entity**

Replace the entire `AppUser.cs` with:

```csharp
// src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs
using Teeforce.Domain.AppUserAggregate.Events;
using Teeforce.Domain.AppUserAggregate.Exceptions;
using Teeforce.Domain.Common;

namespace Teeforce.Domain.AppUserAggregate;

public class AppUser : Entity
{
    public string? IdentityId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public AppUserRole Role { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    private AppUser() { } // EF

    public static AppUser CreateAdmin(string email)
    {
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
        });

        return user;
    }

    public static AppUser CreateOperator(string email, Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new EmptyOrganizationIdException();
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
        });

        return user;
    }

    public void CompleteIdentitySetup(string identityId, string firstName, string lastName)
    {
        if (this.IdentityId is not null && this.IdentityId == identityId)
        {
            return; // Idempotent — already linked to this identity
        }

        if (this.IdentityId is not null)
        {
            throw new IdentityAlreadyLinkedException();
        }

        this.IdentityId = identityId;
        this.FirstName = firstName;
        this.LastName = lastName;
        this.IsActive = true;

        AddDomainEvent(new AppUserSetupCompleted
        {
            AppUserId = this.Id,
            Email = this.Email,
        });
    }

    public void MakeAdmin()
    {
        this.Role = AppUserRole.Admin;
        this.OrganizationId = null;
    }

    public void AssignToOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new EmptyOrganizationIdException();
        }

        this.Role = AppUserRole.Operator;
        this.OrganizationId = organizationId;
    }

    public void RecordLogin() => this.LastLoginAt = DateTimeOffset.UtcNow;

    public void Deactivate() => this.IsActive = false;

    public void Activate() => this.IsActive = true;
}
```

- [ ] **Step 4: Run domain tests to verify they pass**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter AppUserTests --verbosity quiet`
Expected: All tests pass

- [ ] **Step 5: Commit**

```bash
git add src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserTests.cs
git commit -m "feat(domain): update AppUser for invite flow — nullable IdentityId, CompleteIdentitySetup, domain events"
```

---

### Task 3: Update EF Configuration and Domain Exception Handler

**Files:**
- Modify: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/AppUserConfiguration.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/Middleware/DomainExceptionHandler.cs`

- [ ] **Step 1: Update AppUserConfiguration**

Replace the contents of `AppUserConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.OrganizationAggregate;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUsers");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.HasIndex(u => u.IdentityId)
            .IsUnique()
            .HasFilter("[IdentityId] IS NOT NULL");
        builder.Property(u => u.IdentityId).HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.FirstName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.IsActive);
        builder.Property(u => u.CreatedAt);
        builder.Property(u => u.LastLoginAt);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(u => u.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasShadowAuditProperties();
    }
}
```

Key changes: `IdentityId` no longer `.IsRequired()`, unique index has `.HasFilter("[IdentityId] IS NOT NULL")`, `DisplayName` replaced with `FirstName`/`LastName`.

- [ ] **Step 2: Add IdentityAlreadyLinkedException to DomainExceptionHandler**

In `src/backend/Teeforce.Api/Infrastructure/Middleware/DomainExceptionHandler.cs`, add the using and the mapping:

Add to the using block:
```csharp
using Teeforce.Domain.AppUserAggregate.Exceptions;
```

This using already exists (for `EmptyOrganizationIdException`). Add the new exception to the switch expression, mapping it to 409 Conflict:

```csharp
IdentityAlreadyLinkedException => StatusCodes.Status409Conflict,
```

Add this line right after the `EmptyOrganizationIdException` line in the switch.

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build teeforce.slnx --verbosity quiet`
Expected: Build will have errors from `AuthEndpoints.cs` and `AppUserEnrichmentMiddleware.cs` referencing `DisplayName` and old factory signatures. That's expected — those files are updated in Tasks 4 and 5. Verify by checking the errors are only in those files.

- [ ] **Step 4: Commit**

```bash
git add src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/AppUserConfiguration.cs src/backend/Teeforce.Api/Infrastructure/Middleware/DomainExceptionHandler.cs
git commit -m "feat: update EF config for nullable IdentityId and add IdentityAlreadyLinkedException handler"
```

---

### Task 4: Update Auth Endpoints, DTOs, and Validators

**Files:**
- Modify: `src/backend/Teeforce.Api/Features/Auth/AuthEndpoints.cs`
- Modify: `tests/Teeforce.Api.Tests/Features/Auth/Validators/CreateUserRequestValidatorTests.cs`

- [ ] **Step 1: Update validator tests for new request shape**

Replace `tests/Teeforce.Api.Tests/Features/Auth/Validators/CreateUserRequestValidatorTests.cs`:

```csharp
using FluentValidation.TestHelper;
using Teeforce.Api.Features.Auth;

namespace Teeforce.Api.Tests.Features.Auth.Validators;

public class CreateUserRequestValidatorTests
{
    private readonly CreateUserRequestValidator validator = new();

    [Fact]
    public void ValidAdmin_Passes() =>
        this.validator.TestValidate(new CreateUserRequest("admin@example.com", "Admin", null))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void ValidOperator_Passes() =>
        this.validator.TestValidate(new CreateUserRequest("operator@example.com", "Operator", Guid.CreateVersion7()))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void EmptyEmail_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("", "Admin", null))
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Fact]
    public void InvalidRole_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("admin@example.com", "SuperUser", null))
            .ShouldHaveValidationErrorFor(x => x.Role)
            .WithErrorMessage("Invalid role. Must be Admin or Operator.");

    [Fact]
    public void OperatorWithoutOrganizationId_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("op@example.com", "Operator", null))
            .ShouldHaveValidationErrorFor(x => x.OrganizationId)
            .WithErrorMessage("OrganizationId is required for Operator role.");

    [Fact]
    public void AdminWithOrganizationId_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("admin@example.com", "Admin", Guid.CreateVersion7()))
            .ShouldHaveValidationErrorFor(x => x.OrganizationId)
            .WithErrorMessage("Admin users must not have an OrganizationId.");

    [Fact]
    public void OperatorWithEmptyGuid_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("op@example.com", "Operator", Guid.Empty))
            .ShouldHaveValidationErrorFor(x => x.OrganizationId);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Teeforce.Api.Tests --filter CreateUserRequestValidatorTests --verbosity quiet`
Expected: FAIL — `CreateUserRequest` constructor signature doesn't match

- [ ] **Step 3: Update AuthEndpoints.cs**

Update DTOs, validators, and endpoint logic. Key changes:

**Replace the DTOs** at the bottom of `AuthEndpoints.cs`:

```csharp
public sealed record MeResponse(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string Role,
    OrgResponse? Organization,
    List<CourseResponse> Courses,
    List<string> Permissions);

public sealed record OrgResponse(Guid Id, string Name);

public sealed record CourseResponse(Guid Id, string Name);

public sealed record UserListResponse(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string Role,
    Guid? OrganizationId,
    bool IsActive);

public sealed record CreateUserRequest(
    string Email,
    string Role,
    Guid? OrganizationId);

public sealed record UpdateUserRequest(bool? IsActive, string? Role, Guid? OrganizationId);
```

**Replace the CreateUserRequestValidator:**

```csharp
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(r => Enum.TryParse<AppUserRole>(r, ignoreCase: true, out _))
            .WithMessage("Invalid role. Must be Admin or Operator.");
        RuleFor(x => x.OrganizationId)
            .NotNull()
            .When(x => string.Equals(x.Role, "Operator", StringComparison.OrdinalIgnoreCase))
            .WithMessage("OrganizationId is required for Operator role.");
        RuleFor(x => x.OrganizationId)
            .Null()
            .When(x => string.Equals(x.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Admin users must not have an OrganizationId.");
        RuleFor(x => x.OrganizationId)
            .NotEqual(Guid.Empty)
            .When(x => x.OrganizationId is not null);
    }
}
```

**Update the `GetMe` endpoint** — replace the `MeResponse` construction (around line 67):

```csharp
var response = new MeResponse(
    appUser.Id,
    appUser.Email,
    appUser.FirstName,
    appUser.LastName,
    appUser.Role.ToString(),
    org,
    courses,
    currentUser.Permissions.ToList());
```

**Update the `GetUsers` endpoint** — replace the `UserListResponse` projection (around line 84):

```csharp
var users = await db.AppUsers
    .Select(u => new UserListResponse(
        u.Id,
        u.Email,
        u.FirstName,
        u.LastName,
        u.Role.ToString(),
        u.OrganizationId,
        u.IsActive))
    .ToListAsync();
```

**Update the `CreateUser` endpoint** — replace the whole method body:

```csharp
[WolverinePost("/auth/users")]
[Authorize(Policy = "RequireUsersManage")]
public static async Task<IResult> CreateUser(
    CreateUserRequest request,
    [NotBody] ApplicationDbContext db)
{
    var exists = await db.AppUsers.AnyAsync(u => u.Email == request.Email);
    if (exists)
    {
        return Results.Conflict(new { error = "A user with this email already exists." });
    }

    var role = Enum.Parse<AppUserRole>(request.Role, ignoreCase: true);

    var appUser = role == AppUserRole.Admin
        ? AppUser.CreateAdmin(request.Email)
        : AppUser.CreateOperator(request.Email, request.OrganizationId!.Value);

    db.AppUsers.Add(appUser);

    var response = new UserListResponse(
        appUser.Id,
        appUser.Email,
        appUser.FirstName,
        appUser.LastName,
        appUser.Role.ToString(),
        appUser.OrganizationId,
        appUser.IsActive);

    return Results.Created($"/auth/users/{appUser.Id}", response);
}
```

**Update the `UpdateUser` endpoint** — update cache removal and response construction. The `cache.Remove` line references `appUser.IdentityId` which is now nullable. Update to only remove cache when IdentityId is set:

```csharp
if (appUser.IdentityId is not null)
{
    cache.Remove(AppUserEnrichmentMiddleware.CacheKey(appUser.IdentityId));
}

var response = new UserListResponse(
    appUser.Id,
    appUser.Email,
    appUser.FirstName,
    appUser.LastName,
    appUser.Role.ToString(),
    appUser.OrganizationId,
    appUser.IsActive);
```

- [ ] **Step 4: Run validator tests to verify they pass**

Run: `dotnet test tests/Teeforce.Api.Tests --filter CreateUserRequestValidatorTests --verbosity quiet`
Expected: All tests pass

- [ ] **Step 5: Verify full solution compiles**

Run: `dotnet build teeforce.slnx --verbosity quiet`
Expected: Errors only in `AppUserEnrichmentMiddleware.cs` and its tests (updated in Task 5)

- [ ] **Step 6: Commit**

```bash
git add src/backend/Teeforce.Api/Features/Auth/AuthEndpoints.cs tests/Teeforce.Api.Tests/Features/Auth/Validators/CreateUserRequestValidatorTests.cs
git commit -m "feat: update auth endpoints and validators for invite flow — remove IdentityId/DisplayName from requests"
```

---

### Task 5: CompleteIdentitySetup Handler

**Files:**
- Create: `src/backend/Teeforce.Api/Features/Auth/Handlers/CompleteIdentitySetup/Handler.cs`
- Create: `tests/Teeforce.Api.Tests/Features/Auth/Handlers/CompleteIdentitySetupHandlerTests.cs`

- [ ] **Step 1: Write handler test**

```csharp
// tests/Teeforce.Api.Tests/Features/Auth/Handlers/CompleteIdentitySetupHandlerTests.cs
using NSubstitute;
using Teeforce.Api.Features.Auth.Handlers;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.AppUserAggregate.Events;
using Teeforce.Domain.AppUserAggregate.Exceptions;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Tests.Features.Auth.Handlers;

public class CompleteIdentitySetupHandlerTests
{
    private readonly IRepository<AppUser> repository = Substitute.For<IRepository<AppUser>>();

    [Fact]
    public async Task Handle_LinksIdentityAndActivatesUser()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        user.ClearDomainEvents();
        this.repository.GetByIdAsync(user.Id).Returns(user);

        await CompleteIdentitySetupHandler.Handle(
            new CompleteIdentitySetupCommand(user.Id, "entra-oid-123", "Jane", "Smith"),
            this.repository);

        Assert.Equal("entra-oid-123", user.IdentityId);
        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Smith", user.LastName);
        Assert.True(user.IsActive);
        Assert.Contains(user.DomainEvents, e => e is AppUserSetupCompleted);
    }

    [Fact]
    public async Task Handle_UserNotFound_Throws()
    {
        var userId = Guid.NewGuid();
        this.repository.GetByIdAsync(userId).Returns((AppUser?)null);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(
            () => CompleteIdentitySetupHandler.Handle(
                new CompleteIdentitySetupCommand(userId, "oid", "Jane", "Smith"),
                this.repository));

        Assert.Contains(userId.ToString(), ex.Message);
    }

    [Fact]
    public async Task Handle_AlreadyLinkedSameOid_IsIdempotent()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");
        user.ClearDomainEvents();
        this.repository.GetByIdAsync(user.Id).Returns(user);

        await CompleteIdentitySetupHandler.Handle(
            new CompleteIdentitySetupCommand(user.Id, "entra-oid-123", "Jane", "Smith"),
            this.repository);

        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public async Task Handle_AlreadyLinkedDifferentOid_Throws()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");
        this.repository.GetByIdAsync(user.Id).Returns(user);

        await Assert.ThrowsAsync<IdentityAlreadyLinkedException>(
            () => CompleteIdentitySetupHandler.Handle(
                new CompleteIdentitySetupCommand(user.Id, "different-oid", "Jane", "Smith"),
                this.repository));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Teeforce.Api.Tests --filter CompleteIdentitySetupHandlerTests --verbosity quiet`
Expected: FAIL — handler doesn't exist

- [ ] **Step 3: Create the handler**

```csharp
// src/backend/Teeforce.Api/Features/Auth/Handlers/CompleteIdentitySetup/Handler.cs
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Auth.Handlers;

public record CompleteIdentitySetupCommand(Guid AppUserId, string IdentityId, string FirstName, string LastName);

public static class CompleteIdentitySetupHandler
{
    public static async Task Handle(CompleteIdentitySetupCommand command, IRepository<AppUser> repository)
    {
        var user = await repository.GetRequiredByIdAsync(command.AppUserId);

        user.CompleteIdentitySetup(command.IdentityId, command.FirstName, command.LastName);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Teeforce.Api.Tests --filter CompleteIdentitySetupHandlerTests --verbosity quiet`
Expected: All tests pass

- [ ] **Step 5: Commit**

```bash
git add src/backend/Teeforce.Api/Features/Auth/Handlers/CompleteIdentitySetup/Handler.cs tests/Teeforce.Api.Tests/Features/Auth/Handlers/CompleteIdentitySetupHandlerTests.cs
git commit -m "feat: add CompleteIdentitySetup Wolverine handler"
```

---

### Task 6: Update AppUserEnrichmentMiddleware

**Files:**
- Modify: `src/backend/Teeforce.Api/Infrastructure/Auth/AppUserEnrichmentMiddleware.cs`
- Modify: `tests/Teeforce.Api.Tests/Auth/AppUserEnrichmentMiddlewareTests.cs`

- [ ] **Step 1: Update middleware tests**

The middleware tests need significant rewriting. The middleware now:
- Removes seed admin auto-provisioning
- Falls back to email lookup for unlinked users
- Dispatches `CompleteIdentitySetupCommand` via `IMessageBus.InvokeAsync`
- Returns 403 with `{ reason: "no_account" }` for unknown users

Replace `tests/Teeforce.Api.Tests/Auth/AppUserEnrichmentMiddlewareTests.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Teeforce.Api.Features.Auth.Handlers;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.AppUserAggregate;
using Wolverine;

namespace Teeforce.Api.Tests.Auth;

public class AppUserEnrichmentMiddlewareTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static IMemoryCache CreateMemoryCache() =>
        new MemoryCache(Options.Create(new MemoryCacheOptions()));

    private static AppUserEnrichmentMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, NullLoggerFactory.Instance);

    private static DefaultHttpContext CreateAuthenticatedContext(
        string oid, string? email = null, string? givenName = null, string? surname = null)
    {
        var claims = new List<Claim> { new("oid", oid) };
        if (email is not null)
        {
            claims.Add(new Claim("email", email));
        }

        if (givenName is not null)
        {
            claims.Add(new Claim("given_name", givenName));
        }

        if (surname is not null)
        {
            claims.Add(new Claim("family_name", surname));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        var principal = new ClaimsPrincipal(identity);
        return new DefaultHttpContext { User = principal };
    }

    [Fact]
    public async Task UnauthenticatedRequest_PassesThroughWithoutEnrichment()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var bus = Substitute.For<IMessageBus>();
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, db, cache, bus);

        Assert.True(nextCalled);
        Assert.Null(context.User.FindFirst("app_user_id"));
        Assert.Null(context.User.FindFirst("permission"));
    }

    [Fact]
    public async Task AuthenticatedUser_ExistingLinkedAppUser_GetsEnrichedWithClaims()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();
        var org = Teeforce.Domain.OrganizationAggregate.Organization.Create("Acme Golf");
        db.Organizations.Add(org);
        var appUser = AppUser.CreateOperator("op@example.com", org.Id);
        appUser.CompleteIdentitySetup(oid, "Op", "User");
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache, bus);

        Assert.Equal(appUser.Id.ToString(), context.User.FindFirst("app_user_id")?.Value);
        Assert.Equal(org.Id.ToString(), context.User.FindFirst("organization_id")?.Value);
        Assert.Equal("Operator", context.User.FindFirst("role")?.Value);
        Assert.Contains(context.User.FindAll("permission"), c => c.Value == Permissions.AppAccess);
    }

    [Fact]
    public async Task AuthenticatedUser_UnlinkedAppUser_MatchesByEmail_DispatchesSetupCommand()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var bus = Substitute.For<IMessageBus>();

        var appUser = AppUser.CreateAdmin("admin@example.com");
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var oid = Guid.NewGuid().ToString();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid, email: "admin@example.com", givenName: "Jane", surname: "Smith");

        await middleware.InvokeAsync(context, db, cache, bus);

        await bus.Received(1).InvokeAsync(
            Arg.Is<CompleteIdentitySetupCommand>(c =>
                c.AppUserId == appUser.Id &&
                c.IdentityId == oid &&
                c.FirstName == "Jane" &&
                c.LastName == "Smith"),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());

        // Should still enrich claims after dispatching
        Assert.Equal(appUser.Id.ToString(), context.User.FindFirst("app_user_id")?.Value);
    }

    [Fact]
    public async Task AuthenticatedUser_NoAppUser_Returns403WithNoAccountReason()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthenticatedContext(oid, email: "unknown@example.com");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db, cache, bus);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task InactiveLinkedUser_IsEnrichedWithoutPermissions()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();
        var appUser = AppUser.CreateAdmin("inactive@example.com");
        appUser.CompleteIdentitySetup(oid, "Inactive", "User");
        appUser.Deactivate();
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache, bus);

        Assert.True(nextCalled);
        Assert.NotNull(context.User.FindFirst("app_user_id"));
        Assert.NotNull(context.User.FindFirst("role"));
        Assert.Null(context.User.FindFirst("permission"));
    }

    [Fact]
    public async Task InactiveLinkedUser_DoesNotRecordLogin()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();
        var appUser = AppUser.CreateAdmin("inactive@example.com");
        appUser.CompleteIdentitySetup(oid, "Inactive", "User");
        appUser.RecordLogin();
        var loginBefore = appUser.LastLoginAt;
        appUser.Deactivate();
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache, bus);

        Assert.Equal(loginBefore, appUser.LastLoginAt);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Teeforce.Api.Tests --filter AppUserEnrichmentMiddlewareTests --verbosity quiet`
Expected: FAIL — middleware signature and behavior don't match

- [ ] **Step 3: Update the middleware**

Replace `src/backend/Teeforce.Api/Infrastructure/Auth/AppUserEnrichmentMiddleware.cs`:

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Teeforce.Api.Features.Auth.Handlers;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.AppUserAggregate;
using Wolverine;

namespace Teeforce.Api.Infrastructure.Auth;

public class AppUserEnrichmentMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public static string CacheKey(string oid) => $"appuser:{oid}";

    private readonly RequestDelegate next = next;
    private readonly ILogger<AppUserEnrichmentMiddleware> logger = loggerFactory.CreateLogger<AppUserEnrichmentMiddleware>();

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, IMemoryCache cache, IMessageBus bus)
    {
        var oid = context.User?.FindFirst("oid")?.Value
            ?? context.User?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (context.User?.Identity?.IsAuthenticated == true && oid is not null)
        {
            var result = await EnrichFromAppUserAsync(context, db, cache, bus, this.logger, oid);
            if (!result)
            {
                return; // 403 already written
            }
        }

        await this.next(context);
    }

    /// <returns>true if enrichment succeeded (or was skipped for unauthenticated), false if 403 was returned.</returns>
    private static async Task<bool> EnrichFromAppUserAsync(
        HttpContext context, ApplicationDbContext db, IMemoryCache cache, IMessageBus bus,
        ILogger<AppUserEnrichmentMiddleware> logger, string oid)
    {
        var cacheKey = CacheKey(oid);

        if (!cache.TryGetValue(cacheKey, out EnrichmentData? enrichmentData))
        {
            // Fast path: lookup by IdentityId
            var appUser = await db.AppUsers
                .FirstOrDefaultAsync(u => u.IdentityId == oid);

            if (appUser is null)
            {
                // Slow path: first login — match by email
                var email = ExtractEmail(context);

                if (string.IsNullOrEmpty(email))
                {
                    logger.LogWarning("No AppUser for oid {Oid} and no email claim found", oid);
                    return await WriteNoAccountResponse(context);
                }

                appUser = await db.AppUsers
                    .FirstOrDefaultAsync(u => u.Email == email && u.IdentityId == null);

                if (appUser is null)
                {
                    logger.LogWarning("No AppUser for oid {Oid} or email {Email}", oid, email);
                    return await WriteNoAccountResponse(context);
                }

                // Link identity via Wolverine pipeline for proper domain event publishing
                var firstName = context.User?.FindFirst("given_name")?.Value ?? string.Empty;
                var lastName = context.User?.FindFirst("family_name")?.Value ?? string.Empty;

                await bus.InvokeAsync(new CompleteIdentitySetupCommand(appUser.Id, oid, firstName, lastName));

                // Refresh from DB to get updated state
                await db.Entry(appUser).ReloadAsync();
            }

            if (appUser.IsActive)
            {
                appUser.RecordLogin();
            }

            // Save login timestamp outside Wolverine pipeline.
            // RecordLogin() does not raise domain events, so no events are silently lost.
            await db.SaveChangesAsync();

            var permissions = appUser.IsActive
                ? Permissions.GetForRole(appUser.Role)
                : [];

            enrichmentData = new EnrichmentData(
                AppUserId: appUser.Id,
                OrganizationId: appUser.OrganizationId,
                Role: appUser.Role,
                Permissions: permissions);

            cache.Set(cacheKey, enrichmentData, CacheTtl);
        }

        var claimsList = new List<Claim>
        {
            new("app_user_id", enrichmentData!.AppUserId.ToString()),
        };

        if (enrichmentData.OrganizationId.HasValue)
        {
            claimsList.Add(new Claim("organization_id", enrichmentData.OrganizationId.Value.ToString()));
        }

        claimsList.Add(new Claim("role", enrichmentData.Role.ToString()));

        foreach (var permission in enrichmentData.Permissions)
        {
            claimsList.Add(new Claim("permission", permission));
        }

        context.User!.AddIdentity(new ClaimsIdentity(claimsList));
        return true;
    }

    private static string ExtractEmail(HttpContext context) =>
        context.User?.FindFirst("emails")?.Value
        ?? context.User?.FindFirst("email")?.Value
        ?? context.User?.FindFirst(ClaimTypes.Email)?.Value
        ?? context.User?.FindFirst("preferred_username")?.Value
        ?? string.Empty;

    private static async Task<bool> WriteNoAccountResponse(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { reason = "no_account" });
        return false;
    }

    private sealed record EnrichmentData(
        Guid AppUserId,
        Guid? OrganizationId,
        AppUserRole Role,
        IReadOnlyList<string> Permissions);
}
```

- [ ] **Step 4: Run middleware tests to verify they pass**

Run: `dotnet test tests/Teeforce.Api.Tests --filter AppUserEnrichmentMiddlewareTests --verbosity quiet`
Expected: All tests pass

- [ ] **Step 5: Verify full solution compiles**

Run: `dotnet build teeforce.slnx --verbosity quiet`
Expected: Build succeeded, 0 errors

- [ ] **Step 6: Commit**

```bash
git add src/backend/Teeforce.Api/Infrastructure/Auth/AppUserEnrichmentMiddleware.cs tests/Teeforce.Api.Tests/Auth/AppUserEnrichmentMiddlewareTests.cs
git commit -m "feat: update middleware for invite flow — email fallback, InvokeAsync linking, 403 no_account"
```

---

### Task 7: Seed Admin Startup Creation

**Files:**
- Modify: `src/backend/Teeforce.Api/Program.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/Auth/AppUserEnrichmentMiddleware.cs` (remove seed admin params)

- [ ] **Step 1: Add seed admin logic to Program.cs**

Add after the `app.MapWolverineEndpoints(...)` call and before `app.Run()` in `Program.cs`. Find the section near the end of the file.

```csharp
// Seed admin accounts from configuration
await using (var scope = app.Services.CreateAsyncScope())
{
    var authSettings = scope.ServiceProvider.GetRequiredService<IOptions<AuthSettings>>().Value;
    var seedEmails = authSettings.GetSeedAdminEmailsList();
    if (seedEmails.Length > 0)
    {
        var seedDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (var email in seedEmails)
        {
            var exists = await seedDb.AppUsers.AnyAsync(u => u.Email == email);
            if (!exists)
            {
                var admin = AppUser.CreateAdmin(email);
                seedDb.AppUsers.Add(admin);
            }
        }

        await seedDb.SaveChangesAsync();
    }
}
```

Add the necessary usings at the top of `Program.cs` if not already present:
```csharp
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Domain.AppUserAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
```

- [ ] **Step 2: Remove seed admin logic from middleware**

The middleware's `InvokeAsync` method no longer needs `IOptions<AuthSettings> authOptions`. Update the signature:

The middleware `InvokeAsync` signature is already updated in Task 6 to not include `authOptions`. Verify no references to `SeedAdminEmails` remain in the middleware file.

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build teeforce.slnx --verbosity quiet`
Expected: Build succeeded

- [ ] **Step 4: Run all tests**

Run: `dotnet test tests/Teeforce.Domain.Tests --verbosity quiet && dotnet test tests/Teeforce.Api.Tests --verbosity quiet`
Expected: All tests pass

- [ ] **Step 5: Run format**

Run: `dotnet format teeforce.slnx`

- [ ] **Step 6: Commit**

```bash
git add src/backend/Teeforce.Api/Program.cs
git commit -m "feat: move seed admin creation from middleware to app startup"
```

---

### Task 8: Final Verification

- [ ] **Step 1: Run full test suite**

Run: `dotnet test tests/Teeforce.Domain.Tests --verbosity quiet && dotnet test tests/Teeforce.Api.Tests --verbosity quiet`
Expected: All tests pass

- [ ] **Step 2: Run `make dev` to verify runtime startup**

Run: `make dev`
Expected: API starts on :5221 without errors, web starts on :3000

- [ ] **Step 3: Stop dev server and verify clean git state**

Run: `git status`
Expected: Clean working tree, all changes committed

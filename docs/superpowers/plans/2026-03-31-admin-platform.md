# Admin Platform Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Simplify the role model (Owner→Operator, remove Staff/CourseAssignments), build admin CRUD for orgs/courses/users/feature-flags, and add an analytics dashboard.

**Architecture:** Two-role model (Admin + Operator). Admin UI with sidebar nav, CRUD pages using existing shadcn patterns, and analytics endpoints using raw SQL via `Database.SqlQuery<T>()`. Recharts for frontend visualizations.

**Tech Stack:** .NET 10 / EF Core 10, React 19, TypeScript 5.9, TanStack Query, React Hook Form + Zod, shadcn/ui, Recharts, Vitest

**Spec:** `docs/superpowers/specs/2026-03-31-admin-platform-design.md`

---

## File Structure

### Backend — Modified Files

| File | Responsibility |
|------|---------------|
| `src/backend/Teeforce.Domain/AppUserAggregate/AppUserRole.cs` | Rename Owner→Operator, remove Staff |
| `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs` | Remove CourseAssignment methods and collection |
| `src/backend/Teeforce.Api/Auth/AppUserEnrichmentMiddleware.cs` | Remove course_id claims, rename Owner refs |
| `src/backend/Teeforce.Api/Auth/ICurrentUser.cs` | Remove CourseIds property |
| `src/backend/Teeforce.Api/Auth/CurrentUser.cs` | Remove CourseIds implementation |
| `src/backend/Teeforce.Api/Auth/Permissions.cs` | Update role mapping (remove Staff, rename Owner) |
| `src/backend/Teeforce.Api/Auth/CourseAccessAuthorizationHandler.cs` | Delete — EF query filter handles org scoping |
| `src/backend/Teeforce.Api/Features/Auth/AuthEndpoints.cs` | Remove course assignment endpoint, update DTOs, expand UpdateUser |
| `src/backend/Teeforce.Api/Infrastructure/Data/ApplicationDbContext.cs` | Remove CourseAssignment DbSet and config |
| `src/backend/Teeforce.Api/Infrastructure/Middleware/DomainExceptionHandler.cs` | Remove CourseAssignment exception mappings |
| `src/backend/Teeforce.Api/Program.cs` | Remove CourseAccessAuthorizationHandler registration |
| `src/backend/Teeforce.Api/Features/Courses/CourseEndpoints.cs` | Add PUT endpoint, change RequireCourseAccess→RequireUsersManage |
| `src/backend/Teeforce.Api/Features/FeatureFlags/FeatureEndpoints.cs` | Add PUT endpoints for org/course flags |

### Backend — New Files

| File | Responsibility |
|------|---------------|
| `src/backend/Teeforce.Api/Features/Analytics/AnalyticsEndpoints.cs` | All analytics query endpoints |
| `src/backend/Teeforce.Api/Features/Analytics/AnalyticsModels.cs` | Read-model records for analytics results |

### Backend — Deleted Files

| File | Reason |
|------|--------|
| `src/backend/Teeforce.Domain/AppUserAggregate/CourseAssignment.cs` | Staff role removed |
| `src/backend/Teeforce.Domain/AppUserAggregate/Exceptions/CourseAlreadyAssignedException.cs` | No more course assignments |
| `src/backend/Teeforce.Domain/AppUserAggregate/Exceptions/CourseNotAssignedException.cs` | No more course assignments |
| `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/CourseAssignmentConfiguration.cs` | Table being dropped |
| `src/backend/Teeforce.Api/Auth/CourseAccessRequirement.cs` | Policy removed |

### Backend — Test Files Modified

| File | Change |
|------|--------|
| `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserTests.cs` | Remove course assignment tests, update role refs |
| `tests/Teeforce.Api.Tests/Auth/AppUserEnrichmentMiddlewareTests.cs` | Update to Operator, remove course_id claim assertions |
| `tests/Teeforce.Api.Tests/Auth/CourseAccessAuthorizationHandlerTests.cs` | Delete — handler removed |
| `tests/Teeforce.Api.Tests/Auth/PermissionAuthorizationHandlerTests.cs` | Update role references |

### Frontend — Modified Files

| File | Responsibility |
|------|---------------|
| `src/web/src/features/auth/types.ts` | Remove Staff/Owner, add Operator |
| `src/web/src/types/user.ts` | Already uses `string` for role — no change needed |
| `src/web/src/app/router.tsx` | Update RoleRedirect default route |
| `src/web/src/features/admin/index.tsx` | Replace tenant routes with org/user/flag/dashboard routes |
| `src/web/src/components/layout/AdminLayout.tsx` | Update sidebar nav items |
| `src/web/src/lib/query-keys.ts` | Add organizations, users, analytics keys |
| `src/web/src/features/auth/providers/MsalAuthProvider.tsx` | No code change needed (reads role as string) |

### Frontend — New Files

| File | Responsibility |
|------|---------------|
| `src/web/src/features/admin/hooks/useOrganizations.ts` | Org CRUD hooks |
| `src/web/src/features/admin/hooks/useUsers.ts` | User CRUD hooks |
| `src/web/src/features/admin/hooks/useAnalytics.ts` | Analytics query hooks |
| `src/web/src/features/admin/hooks/useFeatureFlags.ts` | Feature flag management hooks |
| `src/web/src/features/admin/pages/Dashboard.tsx` | Analytics dashboard |
| `src/web/src/features/admin/pages/OrgList.tsx` | Organization list |
| `src/web/src/features/admin/pages/OrgCreate.tsx` | Create organization |
| `src/web/src/features/admin/pages/OrgDetail.tsx` | View/edit organization |
| `src/web/src/features/admin/pages/UserList.tsx` | User list |
| `src/web/src/features/admin/pages/UserCreate.tsx` | Create user |
| `src/web/src/features/admin/pages/UserDetail.tsx` | View/edit user |
| `src/web/src/features/admin/pages/CourseDetail.tsx` | View/edit course (new) |
| `src/web/src/features/admin/pages/FeatureFlags.tsx` | Feature flag grid |
| `src/web/src/types/organization.ts` | Organization type |

### Frontend — Deleted Files

| File | Reason |
|------|--------|
| `src/web/src/features/admin/pages/TenantList.tsx` | Replaced by OrgList |
| `src/web/src/features/admin/pages/TenantCreate.tsx` | Replaced by OrgCreate |
| `src/web/src/features/admin/pages/TenantDetail.tsx` | Replaced by OrgDetail |
| `src/web/src/features/admin/hooks/useTenants.ts` | Replaced by useOrganizations |
| `src/web/src/types/tenant.ts` | Replaced by organization type |
| `src/web/src/features/admin/__tests__/TenantList.test.tsx` | Replaced by OrgList tests |
| `src/web/src/features/admin/__tests__/TenantDetail.test.tsx` | Replaced by OrgDetail tests |

---

## Task 1: Role Model — Rename Owner to Operator, Remove Staff

**Files:**
- Modify: `src/backend/Teeforce.Domain/AppUserAggregate/AppUserRole.cs`
- Modify: `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs`
- Delete: `src/backend/Teeforce.Domain/AppUserAggregate/CourseAssignment.cs`
- Delete: `src/backend/Teeforce.Domain/AppUserAggregate/Exceptions/CourseAlreadyAssignedException.cs`
- Delete: `src/backend/Teeforce.Domain/AppUserAggregate/Exceptions/CourseNotAssignedException.cs`
- Modify: `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserTests.cs`

- [ ] **Step 1: Update domain tests for new role model**

Update `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserTests.cs`:

Remove these tests entirely (behavior removed):
- `AssignCourse_AddsAssignment`
- `AssignCourse_DuplicateCourse_ThrowsCourseAlreadyAssignedException`
- `UnassignCourse_RemovesAssignment`
- Any test referencing `CourseAssignments`, `Staff`, or `Owner`

Update remaining tests — replace `AppUserRole.Owner` with `AppUserRole.Operator` and `AppUserRole.Staff` with `AppUserRole.Operator` where used. The `Create` test should use `AppUserRole.Operator` instead of `AppUserRole.Owner`.

Add a new test:

```csharp
[Fact]
public void Create_WithOperatorRole_SetsOrganizationId()
{
    var orgId = Guid.CreateVersion7();
    var user = AppUser.Create("oid-1", "op@test.com", "Operator", AppUserRole.Operator, orgId);

    Assert.Equal(AppUserRole.Operator, user.Role);
    Assert.Equal(orgId, user.OrganizationId);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter AppUserTests -v minimal`
Expected: Compilation errors — `AppUserRole.Operator` doesn't exist yet, `CourseAssignment` methods still referenced.

- [ ] **Step 3: Update AppUserRole enum**

Replace `src/backend/Teeforce.Domain/AppUserAggregate/AppUserRole.cs`:

```csharp
namespace Teeforce.Domain.AppUserAggregate;

public enum AppUserRole
{
    Operator = 1,
    Admin = 2,
}
```

- [ ] **Step 4: Update AppUser aggregate**

Replace `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs`:

```csharp
using Teeforce.Domain.Common;

namespace Teeforce.Domain.AppUserAggregate;

public class AppUser : Entity
{
    public string IdentityId { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public AppUserRole Role { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    private AppUser() { } // EF

    public static AppUser Create(
        string identityId, string email, string displayName,
        AppUserRole role, Guid? organizationId)
    {
        return new AppUser
        {
            Id = Guid.CreateVersion7(),
            IdentityId = identityId,
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            Role = role,
            OrganizationId = organizationId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateRole(AppUserRole role, Guid? organizationId)
    {
        Role = role;
        OrganizationId = organizationId;
    }

    public void RecordLogin() => LastLoginAt = DateTimeOffset.UtcNow;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
```

- [ ] **Step 5: Delete CourseAssignment and its exceptions**

Delete these files:
- `src/backend/Teeforce.Domain/AppUserAggregate/CourseAssignment.cs`
- `src/backend/Teeforce.Domain/AppUserAggregate/Exceptions/CourseAlreadyAssignedException.cs`
- `src/backend/Teeforce.Domain/AppUserAggregate/Exceptions/CourseNotAssignedException.cs`

- [ ] **Step 6: Run domain tests to verify they pass**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter AppUserTests -v minimal`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "refactor: simplify role model — rename Owner to Operator, remove Staff and CourseAssignment"
```

---

## Task 2: Auth Infrastructure — Update Middleware, CurrentUser, Permissions, Handlers

**Files:**
- Modify: `src/backend/Teeforce.Api/Auth/Permissions.cs`
- Modify: `src/backend/Teeforce.Api/Auth/ICurrentUser.cs`
- Modify: `src/backend/Teeforce.Api/Auth/CurrentUser.cs`
- Modify: `src/backend/Teeforce.Api/Auth/AppUserEnrichmentMiddleware.cs`
- Modify: `src/backend/Teeforce.Api/Auth/CourseAccessAuthorizationHandler.cs`
- Delete: `src/backend/Teeforce.Api/Auth/CourseAccessRequirement.cs`
- Delete: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/CourseAssignmentConfiguration.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/Data/ApplicationDbContext.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/Middleware/DomainExceptionHandler.cs`
- Modify: `src/backend/Teeforce.Api/Program.cs`
- Modify: `tests/Teeforce.Api.Tests/Auth/AppUserEnrichmentMiddlewareTests.cs`
- Modify: `tests/Teeforce.Api.Tests/Auth/CourseAccessAuthorizationHandlerTests.cs`
- Modify: `tests/Teeforce.Api.Tests/Auth/PermissionAuthorizationHandlerTests.cs`

- [ ] **Step 1: Update auth unit tests**

Update `tests/Teeforce.Api.Tests/Auth/PermissionAuthorizationHandlerTests.cs`: Replace any `AppUserRole.Owner` or `AppUserRole.Staff` references with `AppUserRole.Operator`.

Update `tests/Teeforce.Api.Tests/Auth/AppUserEnrichmentMiddlewareTests.cs`:
- Remove all assertions about `course_id` claims
- Remove `.Include(u => u.CourseAssignments)` references in test setup
- Change auto-provision test: new users should be created as `AppUserRole.Operator` (not `Staff`)
- Remove `CourseAssignments` from `EnrichmentData` assertions
- Replace `Owner` references with `Operator`

Delete `tests/Teeforce.Api.Tests/Auth/CourseAccessAuthorizationHandlerTests.cs` — the handler is being removed entirely. Course access is enforced by the EF query filter.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "PermissionAuthorizationHandler|AppUserEnrichmentMiddleware" -v minimal`
Expected: Compilation errors from removed types/properties.

- [ ] **Step 3: Update Permissions.cs**

Replace `src/backend/Teeforce.Api/Auth/Permissions.cs`:

```csharp
using Teeforce.Domain.AppUserAggregate;

namespace Teeforce.Api.Auth;

public static class Permissions
{
    public const string AppAccess = "app:access";
    public const string UsersManage = "users:manage";

    private static readonly Dictionary<AppUserRole, string[]> RolePermissions = new()
    {
        [AppUserRole.Admin] = [AppAccess, UsersManage],
        [AppUserRole.Operator] = [AppAccess],
    };

    public static IReadOnlyList<string> GetForRole(AppUserRole role) =>
        RolePermissions.TryGetValue(role, out var permissions) ? permissions : [];
}
```

- [ ] **Step 4: Update ICurrentUser — remove CourseIds**

Replace `src/backend/Teeforce.Api/Auth/ICurrentUser.cs`:

```csharp
namespace Teeforce.Api.Auth;

public interface ICurrentUser
{
    Guid? AppUserId { get; }
    string? IdentityId { get; }
    Guid? OrganizationId { get; }
    IReadOnlyList<string> Permissions { get; }
    bool IsAuthenticated { get; }
}
```

- [ ] **Step 5: Update CurrentUser — remove CourseIds**

Replace `src/backend/Teeforce.Api/Auth/CurrentUser.cs`:

```csharp
using System.Security.Claims;

namespace Teeforce.Api.Auth;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private readonly IHttpContextAccessor httpContextAccessor = httpContextAccessor;
    private ClaimsPrincipal? User => this.httpContextAccessor.HttpContext?.User;

    public Guid? AppUserId
    {
        get
        {
            var claim = User?.FindFirst("app_user_id");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }
    }

    public string? IdentityId => User?.FindFirst("oid")?.Value
        ?? User?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

    public Guid? OrganizationId
    {
        get
        {
            var claim = User?.FindFirst("organization_id");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }
    }

    public IReadOnlyList<string> Permissions =>
        User?.FindAll("permission").Select(c => c.Value).ToList() ?? [];

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
```

- [ ] **Step 6: Update AppUserEnrichmentMiddleware**

Replace `src/backend/Teeforce.Api/Auth/AppUserEnrichmentMiddleware.cs`:

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.AppUserAggregate;

namespace Teeforce.Api.Auth;

public class AppUserEnrichmentMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly RequestDelegate next = next;

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, IMemoryCache cache, IConfiguration configuration)
    {
        var oid = context.User?.FindFirst("oid")?.Value
            ?? context.User?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (context.User?.Identity?.IsAuthenticated == true && oid is not null)
        {
            var seedAdminEmails = configuration.GetValue<string>("Auth:SeedAdminEmails")
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? [];
            await EnrichFromAppUserAsync(context, db, cache, oid, seedAdminEmails);
        }

        await this.next(context);
    }

    private static async Task EnrichFromAppUserAsync(
        HttpContext context, ApplicationDbContext db, IMemoryCache cache, string oid, string[] seedAdminEmails)
    {
        var cacheKey = $"appuser:{oid}";

        if (!cache.TryGetValue(cacheKey, out EnrichmentData? enrichmentData))
        {
            var appUser = await db.AppUsers
                .FirstOrDefaultAsync(u => u.IdentityId == oid);

            if (appUser is null)
            {
                var email = context.User?.FindFirst("emails")?.Value
                    ?? context.User?.FindFirst("email")?.Value
                    ?? context.User?.FindFirst("preferred_username")?.Value
                    ?? string.Empty;
                var name = context.User?.FindFirst("name")?.Value ?? string.Empty;

                var role = seedAdminEmails.Any(e => e.Equals(email, StringComparison.OrdinalIgnoreCase))
                    ? AppUserRole.Admin
                    : AppUserRole.Operator;

                appUser = AppUser.Create(oid, email, name, role, organizationId: null);
                db.AppUsers.Add(appUser);
            }

            if (!appUser.IsActive)
            {
                await db.SaveChangesAsync();
                return;
            }

            appUser.RecordLogin();
            await db.SaveChangesAsync();

            enrichmentData = new EnrichmentData(
                AppUserId: appUser.Id,
                OrganizationId: appUser.OrganizationId,
                Role: appUser.Role,
                Permissions: Permissions.GetForRole(appUser.Role));

            cache.Set(cacheKey, enrichmentData, CacheTtl);
        }

        if (enrichmentData is null)
        {
            return;
        }

        var claimsList = new List<Claim>
        {
            new("app_user_id", enrichmentData.AppUserId.ToString()),
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
    }

    private sealed record EnrichmentData(
        Guid AppUserId,
        Guid? OrganizationId,
        AppUserRole Role,
        IReadOnlyList<string> Permissions);
}
```

- [ ] **Step 7: Delete CourseAccessAuthorizationHandler and CourseAccessRequirement**

Delete both files:
- `src/backend/Teeforce.Api/Auth/CourseAccessAuthorizationHandler.cs`
- `src/backend/Teeforce.Api/Auth/CourseAccessRequirement.cs`

Course access for Operators is enforced by the EF query filter on `Course` (scopes by `OrganizationId`). No separate authorization handler needed — `PermissionAuthorizationHandler` handles all permission checks.

- [ ] **Step 9: Update Program.cs — remove RequireCourseAccess policy**

In `src/backend/Teeforce.Api/Program.cs`, remove:
- The `.AddPolicy("RequireCourseAccess", ...)` line
- The `builder.Services.AddScoped<IAuthorizationHandler, CourseAccessAuthorizationHandler>();` line

The `CourseAccessAuthorizationHandler` is no longer needed as a separate handler — Operator course access is checked via org membership through the EF query filter.

- [ ] **Step 10: Update ApplicationDbContext — remove CourseAssignment DbSet and config**

In `src/backend/Teeforce.Api/Infrastructure/Data/ApplicationDbContext.cs`:
- Remove `public DbSet<CourseAssignment> CourseAssignments => Set<CourseAssignment>();`
- Remove `using Teeforce.Domain.AppUserAggregate;` if no longer needed (keep if `AppUser` DbSet uses it)
- Remove `modelBuilder.ApplyConfiguration(new CourseAssignmentConfiguration());` from `OnModelCreating`

Delete: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/CourseAssignmentConfiguration.cs`

- [ ] **Step 11: Update AppUserConfiguration — remove CourseAssignments navigation**

In `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/AppUserConfiguration.cs`:
- Remove `builder.Navigation(u => u.CourseAssignments).AutoInclude();`

- [ ] **Step 12: Update DomainExceptionHandler — remove CourseAssignment exceptions**

In `src/backend/Teeforce.Api/Infrastructure/Middleware/DomainExceptionHandler.cs`:
- Remove the `CourseAlreadyAssignedException` and `CourseNotAssignedException` switch arms
- Remove the `using Teeforce.Domain.AppUserAggregate.Exceptions;` import

- [ ] **Step 13: Build to verify compilation**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeds. Fix any remaining references to `Staff`, `Owner`, `CourseAssignment`, `CourseIds`, `CourseAccessRequirement`, or `RequireCourseAccess`.

- [ ] **Step 14: Run auth tests to verify they pass**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "PermissionAuthorizationHandler|AppUserEnrichmentMiddleware" -v minimal`
Expected: PASS

- [ ] **Step 15: Commit**

```bash
git add -A && git commit -m "refactor: update auth infrastructure for two-role model (Admin + Operator)"
```

---

## Task 3: Auth Endpoints — Update for New Role Model

**Files:**
- Modify: `src/backend/Teeforce.Api/Features/Auth/AuthEndpoints.cs`

- [ ] **Step 1: Update AuthEndpoints.cs**

Changes:
1. **GetMe**: Remove the `Staff` branch that used `courseAssignments`. Operator gets courses via org:

```csharp
List<CourseResponse> courses;

if (appUser.Role == AppUserRole.Admin)
{
    courses = await db.Courses
        .IgnoreQueryFilters()
        .Select(c => new CourseResponse(c.Id, c.Name))
        .ToListAsync();
}
else if (appUser.OrganizationId.HasValue)
{
    var orgId = appUser.OrganizationId.Value;
    courses = await db.Courses
        .IgnoreQueryFilters()
        .Where(c => c.OrganizationId == orgId)
        .Select(c => new CourseResponse(c.Id, c.Name))
        .ToListAsync();
}
else
{
    courses = [];
}
```

2. **GetUsers**: Remove `.Include(u => u.CourseAssignments)` and `CourseIds` from response.

3. **UpdateUser**: Expand to handle role and org changes:

```csharp
[WolverinePut("/auth/users/{id}")]
[Authorize(Policy = "RequireUsersManage")]
public static async Task<IResult> UpdateUser(
    Guid id,
    UpdateUserRequest request,
    [NotBody] ApplicationDbContext db,
    [NotBody] IMemoryCache cache)
{
    var appUser = await db.AppUsers
        .FirstOrDefaultAsync(u => u.Id == id);

    if (appUser is null)
    {
        return Results.NotFound();
    }

    if (request.IsActive.HasValue)
    {
        if (request.IsActive.Value)
        {
            appUser.Activate();
        }
        else
        {
            appUser.Deactivate();
        }
    }

    if (request.Role is not null && request.OrganizationId is not null)
    {
        if (!Enum.TryParse<AppUserRole>(request.Role, ignoreCase: true, out var newRole))
        {
            return Results.BadRequest(new { error = $"Invalid role: {request.Role}." });
        }

        appUser.UpdateRole(newRole, request.OrganizationId);
    }

    cache.Remove($"appuser:{appUser.IdentityId}");

    var response = new UserListResponse(
        appUser.Id,
        appUser.Email,
        appUser.DisplayName,
        appUser.Role.ToString(),
        appUser.OrganizationId,
        appUser.IsActive);

    return Results.Ok(response);
}
```

4. **Remove UpdateUserCourses endpoint entirely** (the `PUT /auth/users/{id}/courses` method).

5. **Update DTOs**:

```csharp
public sealed record UserListResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    Guid? OrganizationId,
    bool IsActive);

public sealed record UpdateUserRequest(bool? IsActive, string? Role, Guid? OrganizationId);
```

Remove `UpdateUserCoursesRequest`.

- [ ] **Step 2: Build to verify**

Run: `dotnet build teeforce.slnx`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "refactor: update auth endpoints for two-role model, remove course assignment endpoint"
```

---

## Task 4: Update Course Endpoints — Replace RequireCourseAccess

**Files:**
- Modify: `src/backend/Teeforce.Api/Features/Courses/CourseEndpoints.cs`

- [ ] **Step 1: Replace RequireCourseAccess with RequireAppAccess**

In `src/backend/Teeforce.Api/Features/Courses/CourseEndpoints.cs`, change all `[Authorize(Policy = "RequireCourseAccess")]` to `[Authorize(Policy = "RequireAppAccess")]`. The EF query filter on Course already scopes by organization.

- [ ] **Step 2: Add PUT /courses/{courseId} endpoint for editing**

Add to `CourseEndpoints.cs`:

```csharp
[WolverinePut("/courses/{courseId}")]
[Authorize(Policy = "RequireUsersManage")]
public static async Task<IResult> UpdateCourse(
    Guid courseId,
    UpdateCourseRequest request,
    [NotBody] ApplicationDbContext db)
{
    var course = await db.Courses
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(c => c.Id == courseId);

    if (course is null)
    {
        return Results.NotFound();
    }

    course.UpdateDetails(request.Name, request.TimeZoneId);

    return Results.Ok(new CourseResponse(
        course.Id,
        course.Name,
        course.OrganizationId,
        null,
        course.TimeZoneId,
        course.City,
        course.State,
        course.StreetAddress,
        course.ZipCode,
        course.ContactEmail,
        course.ContactPhone,
        course.CreatedAt));
}
```

This requires adding an `UpdateDetails` method to the Course domain entity. Check the existing `Course` entity — if it doesn't have this method, add it:

```csharp
public void UpdateDetails(string name, string timeZoneId)
{
    Name = name.Trim();
    TimeZoneId = timeZoneId;
}
```

Add the request DTO:

```csharp
public sealed record UpdateCourseRequest(string Name, string TimeZoneId);
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build teeforce.slnx`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: add PUT /courses/{courseId} endpoint, replace RequireCourseAccess with RequireAppAccess"
```

---

## Task 5: Feature Flag Management Endpoints

**Files:**
- Modify: `src/backend/Teeforce.Api/Features/FeatureFlags/FeatureEndpoints.cs`
- Modify: `src/backend/Teeforce.Domain/OrganizationAggregate/Organization.cs`

- [ ] **Step 1: Add SetFeatureFlags method to Organization domain entity**

In `src/backend/Teeforce.Domain/OrganizationAggregate/Organization.cs`, add:

```csharp
public void SetFeatureFlags(Dictionary<string, bool> flags)
{
    FeatureFlags = flags;
}
```

Check if Course entity already has a similar method. If not, add to the Course entity:

```csharp
public void SetFeatureFlags(Dictionary<string, bool> flags)
{
    FeatureFlags = flags;
}
```

- [ ] **Step 2: Add feature flag management endpoints**

Add to `src/backend/Teeforce.Api/Features/FeatureFlags/FeatureEndpoints.cs`:

```csharp
[WolverinePut("/organizations/{id}/features")]
[Authorize(Policy = "RequireUsersManage")]
public static async Task<IResult> SetOrgFeatures(
    Guid id,
    SetFeaturesRequest request,
    [NotBody] ApplicationDbContext db)
{
    var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id);
    if (org is null)
    {
        return Results.NotFound();
    }

    org.SetFeatureFlags(request.Flags);

    return Results.Ok(request.Flags);
}

[WolverinePut("/courses/{courseId}/features")]
[Authorize(Policy = "RequireUsersManage")]
public static async Task<IResult> SetCourseFeatures(
    Guid courseId,
    SetFeaturesRequest request,
    [NotBody] ApplicationDbContext db)
{
    var course = await db.Courses
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(c => c.Id == courseId);
    if (course is null)
    {
        return Results.NotFound();
    }

    course.SetFeatureFlags(request.Flags);

    return Results.Ok(request.Flags);
}

public sealed record SetFeaturesRequest(Dictionary<string, bool> Flags);
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build teeforce.slnx`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: add PUT endpoints for org and course feature flag management"
```

---

## Task 6: Organization Edit Endpoint

**Files:**
- Modify: `src/backend/Teeforce.Domain/OrganizationAggregate/Organization.cs`
- Modify: existing org-related endpoints or create in `Features/Auth/AuthEndpoints.cs` or a dedicated file

- [ ] **Step 1: Add UpdateName method to Organization**

In `src/backend/Teeforce.Domain/OrganizationAggregate/Organization.cs`, add:

```csharp
public void UpdateName(string name)
{
    Name = name.Trim();
}
```

- [ ] **Step 2: Add PUT /organizations/{id} endpoint**

Find where organization endpoints live (likely alongside tenant endpoints or auth endpoints). Add:

```csharp
[WolverinePut("/organizations/{id}")]
[Authorize(Policy = "RequireUsersManage")]
public static async Task<IResult> UpdateOrganization(
    Guid id,
    UpdateOrganizationRequest request,
    [NotBody] ApplicationDbContext db)
{
    var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id);
    if (org is null)
    {
        return Results.NotFound();
    }

    org.UpdateName(request.Name);

    return Results.Ok(new OrgResponse(org.Id, org.Name));
}

public sealed record UpdateOrganizationRequest(string Name);
```

Also add a `GET /organizations` endpoint if one doesn't exist:

```csharp
[WolverineGet("/organizations")]
[Authorize(Policy = "RequireUsersManage")]
public static async Task<IResult> GetOrganizations([NotBody] ApplicationDbContext db)
{
    var orgs = await db.Organizations
        .Select(o => new OrgListResponse(
            o.Id,
            o.Name,
            db.Courses.IgnoreQueryFilters().Count(c => c.OrganizationId == o.Id),
            db.AppUsers.Count(u => u.OrganizationId == o.Id),
            o.CreatedAt))
        .ToListAsync();

    return Results.Ok(orgs);
}

[WolverineGet("/organizations/{id}")]
[Authorize(Policy = "RequireUsersManage")]
public static async Task<IResult> GetOrganization(
    Guid id,
    [NotBody] ApplicationDbContext db)
{
    var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id);
    if (org is null)
    {
        return Results.NotFound();
    }

    var courses = await db.Courses
        .IgnoreQueryFilters()
        .Where(c => c.OrganizationId == id)
        .Select(c => new { c.Id, c.Name })
        .ToListAsync();

    var users = await db.AppUsers
        .Where(u => u.OrganizationId == id)
        .Select(u => new { u.Id, u.Email, u.DisplayName, Role = u.Role.ToString(), u.IsActive })
        .ToListAsync();

    return Results.Ok(new { org.Id, org.Name, org.CreatedAt, Courses = courses, Users = users });
}

[WolverinePost("/organizations")]
[Authorize(Policy = "RequireUsersManage")]
public static async Task<IResult> CreateOrganization(
    CreateOrganizationRequest request,
    [NotBody] ApplicationDbContext db)
{
    var org = Organization.Create(request.Name);
    db.Organizations.Add(org);

    return Results.Created($"/organizations/{org.Id}", new OrgResponse(org.Id, org.Name));
}

public sealed record OrgListResponse(Guid Id, string Name, int CourseCount, int UserCount, DateTimeOffset CreatedAt);
public sealed record CreateOrganizationRequest(string Name);
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build teeforce.slnx`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: add organization CRUD endpoints"
```

---

## Task 7: EF Migration

**Files:**
- New migration files (auto-generated)

- [ ] **Step 1: Add EF migration**

Run:
```bash
export PATH="$PATH:/home/aaron/.dotnet/tools"
dotnet ef migrations add SimplifyRoleModelDropCourseAssignments --project src/backend/Teeforce.Api
```

This migration should:
- Rename `Owner` → `Operator` in the `Role` column of `AppUsers` table
- Drop the `CourseAssignments` table

- [ ] **Step 2: Verify migration contents**

Read the generated migration file. It should contain:
- `migrationBuilder.Sql("UPDATE AppUsers SET Role = 'Operator' WHERE Role = 'Owner'");`
- `migrationBuilder.DropTable(name: "CourseAssignments");`

If the SQL update for role rename is missing (EF won't generate it automatically), add it manually at the top of the `Up` method:

```csharp
migrationBuilder.Sql("UPDATE [AppUsers] SET [Role] = 'Operator' WHERE [Role] = 'Owner'");
migrationBuilder.Sql("UPDATE [AppUsers] SET [Role] = 'Operator' WHERE [Role] = 'Staff'");
```

- [ ] **Step 3: Verify no pending model changes**

Run:
```bash
dotnet ef migrations has-pending-model-changes --project src/backend/Teeforce.Api
```
Expected: No pending changes.

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: add migration to drop CourseAssignments and rename Owner/Staff to Operator"
```

---

## Task 8: Analytics Endpoints

**Files:**
- Create: `src/backend/Teeforce.Api/Features/Analytics/AnalyticsModels.cs`
- Create: `src/backend/Teeforce.Api/Features/Analytics/AnalyticsEndpoints.cs`

- [ ] **Step 1: Create analytics read models**

Create `src/backend/Teeforce.Api/Features/Analytics/AnalyticsModels.cs`:

```csharp
namespace Teeforce.Api.Features.Analytics;

public sealed record PlatformSummary(
    int TotalOrganizations,
    int TotalCourses,
    int ActiveUsers,
    int BookingsToday);

public sealed record FillRateResult(
    DateOnly Date,
    int TotalSlots,
    int FilledSlots,
    decimal FillPercentage);

public sealed record BookingTrendResult(
    DateOnly Date,
    int BookingCount);

public sealed record PopularTimeResult(
    TimeOnly Time,
    int BookingCount);

public sealed record WaitlistStatsResult(
    int ActiveEntries,
    int OffersSent,
    int OffersAccepted,
    int OffersRejected);
```

- [ ] **Step 2: Create analytics endpoints**

Create `src/backend/Teeforce.Api/Features/Analytics/AnalyticsEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Wolverine.Http;

namespace Teeforce.Api.Features.Analytics;

public static class AnalyticsEndpoints
{
    [WolverineGet("/admin/analytics/summary")]
    [Authorize(Policy = "RequireUsersManage")]
    public static async Task<IResult> GetSummary([NotBody] ApplicationDbContext db)
    {
        var summary = await db.Database.SqlQuery<PlatformSummary>(
            $"""
            SELECT
                (SELECT COUNT(*) FROM Organizations) AS TotalOrganizations,
                (SELECT COUNT(*) FROM Courses) AS TotalCourses,
                (SELECT COUNT(*) FROM AppUsers WHERE IsActive = 1) AS ActiveUsers,
                (SELECT COUNT(*) FROM Bookings WHERE CAST(CreatedAt AS DATE) = CAST(GETUTCDATE() AS DATE)) AS BookingsToday
            """).FirstOrDefaultAsync();

        return Results.Ok(summary);
    }

    [WolverineGet("/admin/analytics/fill-rates")]
    [Authorize(Policy = "RequireUsersManage")]
    public static async Task<IResult> GetFillRates(
        [NotBody] ApplicationDbContext db,
        [NotBody] Guid? courseId = null,
        [NotBody] int days = 7)
    {
        var results = courseId.HasValue
            ? await db.Database.SqlQuery<FillRateResult>(
                $"""
                SELECT
                    CAST(t.TeeTime AS DATE) AS [Date],
                    SUM(t.SlotsAvailable) AS TotalSlots,
                    SUM(t.SlotsAvailable - t.SlotsRemaining) AS FilledSlots,
                    CASE WHEN SUM(t.SlotsAvailable) = 0 THEN 0
                         ELSE CAST(SUM(t.SlotsAvailable - t.SlotsRemaining) AS DECIMAL(10,2)) / SUM(t.SlotsAvailable) * 100
                    END AS FillPercentage
                FROM TeeTimeOpenings t
                WHERE t.CourseId = {courseId.Value}
                  AND CAST(t.TeeTime AS DATE) >= CAST(DATEADD(DAY, {-days}, GETUTCDATE()) AS DATE)
                  AND t.Status != 'Cancelled'
                GROUP BY CAST(t.TeeTime AS DATE)
                ORDER BY [Date]
                """).ToListAsync()
            : await db.Database.SqlQuery<FillRateResult>(
                $"""
                SELECT
                    CAST(t.TeeTime AS DATE) AS [Date],
                    SUM(t.SlotsAvailable) AS TotalSlots,
                    SUM(t.SlotsAvailable - t.SlotsRemaining) AS FilledSlots,
                    CASE WHEN SUM(t.SlotsAvailable) = 0 THEN 0
                         ELSE CAST(SUM(t.SlotsAvailable - t.SlotsRemaining) AS DECIMAL(10,2)) / SUM(t.SlotsAvailable) * 100
                    END AS FillPercentage
                FROM TeeTimeOpenings t
                WHERE CAST(t.TeeTime AS DATE) >= CAST(DATEADD(DAY, {-days}, GETUTCDATE()) AS DATE)
                  AND t.Status != 'Cancelled'
                GROUP BY CAST(t.TeeTime AS DATE)
                ORDER BY [Date]
                """).ToListAsync();

        return Results.Ok(results);
    }

    [WolverineGet("/admin/analytics/bookings")]
    [Authorize(Policy = "RequireUsersManage")]
    public static async Task<IResult> GetBookingTrends(
        [NotBody] ApplicationDbContext db,
        [NotBody] Guid? courseId = null,
        [NotBody] int days = 30)
    {
        var results = courseId.HasValue
            ? await db.Database.SqlQuery<BookingTrendResult>(
                $"""
                SELECT
                    CAST(CreatedAt AS DATE) AS [Date],
                    COUNT(*) AS BookingCount
                FROM Bookings
                WHERE CourseId = {courseId.Value}
                  AND CAST(CreatedAt AS DATE) >= CAST(DATEADD(DAY, {-days}, GETUTCDATE()) AS DATE)
                  AND Status != 'Rejected'
                GROUP BY CAST(CreatedAt AS DATE)
                ORDER BY [Date]
                """).ToListAsync()
            : await db.Database.SqlQuery<BookingTrendResult>(
                $"""
                SELECT
                    CAST(CreatedAt AS DATE) AS [Date],
                    COUNT(*) AS BookingCount
                FROM Bookings
                WHERE CAST(CreatedAt AS DATE) >= CAST(DATEADD(DAY, {-days}, GETUTCDATE()) AS DATE)
                  AND Status != 'Rejected'
                GROUP BY CAST(CreatedAt AS DATE)
                ORDER BY [Date]
                """).ToListAsync();

        return Results.Ok(results);
    }

    [WolverineGet("/admin/analytics/popular-times")]
    [Authorize(Policy = "RequireUsersManage")]
    public static async Task<IResult> GetPopularTimes(
        [NotBody] ApplicationDbContext db,
        [NotBody] Guid? courseId = null,
        [NotBody] int days = 30)
    {
        var results = courseId.HasValue
            ? await db.Database.SqlQuery<PopularTimeResult>(
                $"""
                SELECT
                    CAST(t.TeeTime AS TIME) AS [Time],
                    SUM(t.SlotsAvailable - t.SlotsRemaining) AS BookingCount
                FROM TeeTimeOpenings t
                WHERE t.CourseId = {courseId.Value}
                  AND CAST(t.TeeTime AS DATE) >= CAST(DATEADD(DAY, {-days}, GETUTCDATE()) AS DATE)
                  AND t.Status != 'Cancelled'
                GROUP BY CAST(t.TeeTime AS TIME)
                HAVING SUM(t.SlotsAvailable - t.SlotsRemaining) > 0
                ORDER BY BookingCount DESC
                """).ToListAsync()
            : await db.Database.SqlQuery<PopularTimeResult>(
                $"""
                SELECT
                    CAST(t.TeeTime AS TIME) AS [Time],
                    SUM(t.SlotsAvailable - t.SlotsRemaining) AS BookingCount
                FROM TeeTimeOpenings t
                WHERE CAST(t.TeeTime AS DATE) >= CAST(DATEADD(DAY, {-days}, GETUTCDATE()) AS DATE)
                  AND t.Status != 'Cancelled'
                GROUP BY CAST(t.TeeTime AS TIME)
                HAVING SUM(t.SlotsAvailable - t.SlotsRemaining) > 0
                ORDER BY BookingCount DESC
                """).ToListAsync();

        return Results.Ok(results);
    }

    [WolverineGet("/admin/analytics/waitlist")]
    [Authorize(Policy = "RequireUsersManage")]
    public static async Task<IResult> GetWaitlistStats(
        [NotBody] ApplicationDbContext db,
        [NotBody] Guid? courseId = null)
    {
        var result = courseId.HasValue
            ? await db.Database.SqlQuery<WaitlistStatsResult>(
                $"""
                SELECT
                    (SELECT COUNT(*) FROM GolferWaitlistEntries e
                     INNER JOIN CourseWaitlists w ON e.CourseWaitlistId = w.Id
                     WHERE w.CourseId = {courseId.Value} AND e.RemovedAt IS NULL) AS ActiveEntries,
                    (SELECT COUNT(*) FROM WaitlistOffers WHERE CourseId = {courseId.Value}) AS OffersSent,
                    (SELECT COUNT(*) FROM WaitlistOffers WHERE CourseId = {courseId.Value} AND Status = 'Accepted') AS OffersAccepted,
                    (SELECT COUNT(*) FROM WaitlistOffers WHERE CourseId = {courseId.Value} AND Status = 'Rejected') AS OffersRejected
                """).FirstOrDefaultAsync()
            : await db.Database.SqlQuery<WaitlistStatsResult>(
                $"""
                SELECT
                    (SELECT COUNT(*) FROM GolferWaitlistEntries WHERE RemovedAt IS NULL) AS ActiveEntries,
                    (SELECT COUNT(*) FROM WaitlistOffers) AS OffersSent,
                    (SELECT COUNT(*) FROM WaitlistOffers WHERE Status = 'Accepted') AS OffersAccepted,
                    (SELECT COUNT(*) FROM WaitlistOffers WHERE Status = 'Rejected') AS OffersRejected
                """).FirstOrDefaultAsync();

        return Results.Ok(result);
    }
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build teeforce.slnx`
Expected: PASS

- [ ] **Step 4: Run `dotnet format teeforce.slnx` to fix style**

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: add analytics endpoints with raw SQL queries"
```

---

## Task 9: Frontend — Role Type Updates and Shared Infrastructure

**Files:**
- Modify: `src/web/src/features/auth/types.ts`
- Modify: `src/web/src/app/router.tsx`
- Modify: `src/web/src/lib/query-keys.ts`
- Create: `src/web/src/types/organization.ts`

- [ ] **Step 1: Update auth types**

Replace `src/web/src/features/auth/types.ts`:

```typescript
export type AppUserRole = 'Admin' | 'Operator';

export interface MeResponse {
  id: string;
  email: string;
  displayName: string;
  role: AppUserRole;
  organization: { id: string; name: string } | null;
  courses: { id: string; name: string }[];
  permissions: string[];
}
```

- [ ] **Step 2: Update RoleRedirect in router**

In `src/web/src/app/router.tsx`, update the `RoleRedirect` component:

```typescript
if (user?.role === 'Admin') {
  return <Navigate to="/admin" replace />;
}
```

(Change from `/admin/tenants` to `/admin` — the dashboard is now the landing page.)

- [ ] **Step 3: Add query keys**

In `src/web/src/lib/query-keys.ts`, add:

```typescript
organizations: {
  all: ['organizations'] as const,
  detail: (id: string) => ['organizations', id] as const,
},
users: {
  all: ['users'] as const,
  detail: (id: string) => ['users', id] as const,
},
analytics: {
  summary: ['analytics', 'summary'] as const,
  fillRates: (courseId?: string) => ['analytics', 'fill-rates', courseId] as const,
  bookings: (courseId?: string) => ['analytics', 'bookings', courseId] as const,
  popularTimes: (courseId?: string) => ['analytics', 'popular-times', courseId] as const,
  waitlist: (courseId?: string) => ['analytics', 'waitlist', courseId] as const,
},
```

- [ ] **Step 4: Create organization type**

Create `src/web/src/types/organization.ts`:

```typescript
export interface Organization {
  id: string;
  name: string;
  courseCount: number;
  userCount: number;
  createdAt: string;
}
```

- [ ] **Step 5: Lint to verify**

Run: `pnpm --dir src/web lint`
Expected: PASS (or minor fixable warnings)

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: update frontend types and query keys for admin platform"
```

---

## Task 10: Frontend — Admin Data Hooks

**Files:**
- Create: `src/web/src/features/admin/hooks/useOrganizations.ts`
- Create: `src/web/src/features/admin/hooks/useUsers.ts`
- Create: `src/web/src/features/admin/hooks/useAnalytics.ts`
- Create: `src/web/src/features/admin/hooks/useFeatureFlags.ts`
- Delete: `src/web/src/features/admin/hooks/useTenants.ts`

- [ ] **Step 1: Create useOrganizations hook**

Create `src/web/src/features/admin/hooks/useOrganizations.ts`:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { Organization } from '@/types/organization';

export function useOrganizations() {
  return useQuery({
    queryKey: queryKeys.organizations.all,
    queryFn: () => api.get<Organization[]>('/organizations'),
  });
}

export function useOrganization(id: string) {
  return useQuery({
    queryKey: queryKeys.organizations.detail(id),
    queryFn: () => api.get<OrganizationDetail>(`/organizations/${id}`),
    enabled: !!id,
  });
}

export function useCreateOrganization() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { name: string }) => api.post<{ id: string; name: string }>('/organizations', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.organizations.all });
    },
  });
}

export function useUpdateOrganization() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...data }: { id: string; name: string }) =>
      api.put<{ id: string; name: string }>(`/organizations/${id}`, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.organizations.all });
    },
  });
}

interface OrganizationDetail {
  id: string;
  name: string;
  createdAt: string;
  courses: { id: string; name: string }[];
  users: { id: string; email: string; displayName: string; role: string; isActive: boolean }[];
}
```

- [ ] **Step 2: Create useUsers hook**

Create `src/web/src/features/admin/hooks/useUsers.ts`:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  role: string;
  organizationId: string | null;
  isActive: boolean;
}

export function useUsers() {
  return useQuery({
    queryKey: queryKeys.users.all,
    queryFn: () => api.get<UserListItem[]>('/auth/users'),
  });
}

export function useCreateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: {
      identityId: string;
      email: string;
      displayName: string;
      role: string;
      organizationId: string | null;
    }) => api.post<UserListItem>('/auth/users', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
    },
  });
}

export function useUpdateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...data }: {
      id: string;
      isActive?: boolean;
      role?: string;
      organizationId?: string | null;
    }) => api.put<UserListItem>(`/auth/users/${id}`, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
    },
  });
}
```

- [ ] **Step 3: Create useAnalytics hook**

Create `src/web/src/features/admin/hooks/useAnalytics.ts`:

```typescript
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

interface PlatformSummary {
  totalOrganizations: number;
  totalCourses: number;
  activeUsers: number;
  bookingsToday: number;
}

interface FillRateResult {
  date: string;
  totalSlots: number;
  filledSlots: number;
  fillPercentage: number;
}

interface BookingTrendResult {
  date: string;
  bookingCount: number;
}

interface PopularTimeResult {
  time: string;
  bookingCount: number;
}

interface WaitlistStatsResult {
  activeEntries: number;
  offersSent: number;
  offersAccepted: number;
  offersRejected: number;
}

export function useSummary() {
  return useQuery({
    queryKey: queryKeys.analytics.summary,
    queryFn: () => api.get<PlatformSummary>('/admin/analytics/summary'),
  });
}

export function useFillRates(courseId?: string, days = 7) {
  const params = new URLSearchParams();
  if (courseId) params.set('courseId', courseId);
  params.set('days', String(days));
  const qs = params.toString();

  return useQuery({
    queryKey: queryKeys.analytics.fillRates(courseId),
    queryFn: () => api.get<FillRateResult[]>(`/admin/analytics/fill-rates?${qs}`),
  });
}

export function useBookingTrends(courseId?: string, days = 30) {
  const params = new URLSearchParams();
  if (courseId) params.set('courseId', courseId);
  params.set('days', String(days));
  const qs = params.toString();

  return useQuery({
    queryKey: queryKeys.analytics.bookings(courseId),
    queryFn: () => api.get<BookingTrendResult[]>(`/admin/analytics/bookings?${qs}`),
  });
}

export function usePopularTimes(courseId?: string, days = 30) {
  const params = new URLSearchParams();
  if (courseId) params.set('courseId', courseId);
  params.set('days', String(days));
  const qs = params.toString();

  return useQuery({
    queryKey: queryKeys.analytics.popularTimes(courseId),
    queryFn: () => api.get<PopularTimeResult[]>(`/admin/analytics/popular-times?${qs}`),
  });
}

export function useWaitlistStats(courseId?: string) {
  const qs = courseId ? `?courseId=${courseId}` : '';

  return useQuery({
    queryKey: queryKeys.analytics.waitlist(courseId),
    queryFn: () => api.get<WaitlistStatsResult>(`/admin/analytics/waitlist${qs}`),
  });
}
```

- [ ] **Step 4: Create useFeatureFlags hook**

Create `src/web/src/features/admin/hooks/useFeatureFlags.ts`:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

export function useOrgFeatures(orgId: string) {
  return useQuery({
    queryKey: ['features', 'org', orgId],
    queryFn: () => api.get<Record<string, boolean>>(`/features`),
    enabled: !!orgId,
  });
}

export function useSetOrgFeatures() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orgId, flags }: { orgId: string; flags: Record<string, boolean> }) =>
      api.put<Record<string, boolean>>(`/organizations/${orgId}/features`, { flags }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['features'] });
    },
  });
}

export function useSetCourseFeatures() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, flags }: { courseId: string; flags: Record<string, boolean> }) =>
      api.put<Record<string, boolean>>(`/courses/${courseId}/features`, { flags }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['features'] });
      void queryClient.invalidateQueries({ queryKey: queryKeys.features.all });
    },
  });
}
```

- [ ] **Step 5: Delete useTenants hook and tenant type**

Delete:
- `src/web/src/features/admin/hooks/useTenants.ts`
- `src/web/src/types/tenant.ts`

- [ ] **Step 6: Lint to verify**

Run: `pnpm --dir src/web lint`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat: add admin data hooks for orgs, users, analytics, feature flags"
```

---

## Task 11: Frontend — Install Recharts

**Files:**
- Modify: `src/web/package.json`

- [ ] **Step 1: Install recharts**

Run: `pnpm --dir src/web add recharts`

- [ ] **Step 2: Commit**

```bash
git add -A && git commit -m "chore: add recharts dependency for analytics dashboard"
```

---

## Task 12: Frontend — Admin Layout and Routes

**Files:**
- Modify: `src/web/src/components/layout/AdminLayout.tsx`
- Modify: `src/web/src/features/admin/index.tsx`
- Delete: `src/web/src/features/admin/pages/TenantList.tsx`
- Delete: `src/web/src/features/admin/pages/TenantCreate.tsx`
- Delete: `src/web/src/features/admin/pages/TenantDetail.tsx`
- Delete: `src/web/src/features/admin/__tests__/TenantList.test.tsx`
- Delete: `src/web/src/features/admin/__tests__/TenantDetail.test.tsx`

- [ ] **Step 1: Update AdminLayout sidebar**

Replace the `SidebarContent` section in `src/web/src/components/layout/AdminLayout.tsx`:

```tsx
<SidebarContent>
  <SidebarMenu>
    <SidebarMenuItem>
      <SidebarMenuButton asChild>
        <NavLink to="/admin" end>
          {({ isActive }) => (
            <span className={isActive ? 'font-semibold' : ''}>Dashboard</span>
          )}
        </NavLink>
      </SidebarMenuButton>
    </SidebarMenuItem>
    <SidebarMenuItem>
      <SidebarMenuButton asChild>
        <NavLink to="/admin/organizations">
          {({ isActive }) => (
            <span className={isActive ? 'font-semibold' : ''}>Organizations</span>
          )}
        </NavLink>
      </SidebarMenuButton>
    </SidebarMenuItem>
    <SidebarMenuItem>
      <SidebarMenuButton asChild>
        <NavLink to="/admin/courses">
          {({ isActive }) => (
            <span className={isActive ? 'font-semibold' : ''}>Courses</span>
          )}
        </NavLink>
      </SidebarMenuButton>
    </SidebarMenuItem>
    <SidebarMenuItem>
      <SidebarMenuButton asChild>
        <NavLink to="/admin/users">
          {({ isActive }) => (
            <span className={isActive ? 'font-semibold' : ''}>Users</span>
          )}
        </NavLink>
      </SidebarMenuButton>
    </SidebarMenuItem>
    <SidebarMenuItem>
      <SidebarMenuButton asChild>
        <NavLink to="/admin/feature-flags">
          {({ isActive }) => (
            <span className={isActive ? 'font-semibold' : ''}>Feature Flags</span>
          )}
        </NavLink>
      </SidebarMenuButton>
    </SidebarMenuItem>
    {(import.meta.env.DEV || import.meta.env.VITE_SHOW_DEV_TOOLS === 'true') && (
      <SidebarMenuItem>
        <SidebarMenuButton asChild>
          <NavLink to="/admin/dev/sms">
            {({ isActive }) => (
              <span className={isActive ? 'font-semibold' : ''}>SMS Log</span>
            )}
          </NavLink>
        </SidebarMenuButton>
      </SidebarMenuItem>
    )}
  </SidebarMenu>
</SidebarContent>
```

- [ ] **Step 2: Update admin routes**

Replace `src/web/src/features/admin/index.tsx`:

```tsx
import { Routes, Route, Navigate } from 'react-router';
import AdminLayout from '@/components/layout/AdminLayout';
import Dashboard from './pages/Dashboard';
import OrgList from './pages/OrgList';
import OrgCreate from './pages/OrgCreate';
import OrgDetail from './pages/OrgDetail';
import CourseList from './pages/CourseList';
import CourseCreate from './pages/CourseCreate';
import CourseDetail from './pages/CourseDetail';
import UserList from './pages/UserList';
import UserCreate from './pages/UserCreate';
import UserDetail from './pages/UserDetail';
import FeatureFlags from './pages/FeatureFlags';
import DevSmsPage from '@/features/dev/pages/DevSmsPage';

export default function AdminFeature() {
  return (
    <Routes>
      <Route element={<AdminLayout />}>
        <Route index element={<Dashboard />} />
        <Route path="organizations" element={<OrgList />} />
        <Route path="organizations/new" element={<OrgCreate />} />
        <Route path="organizations/:id" element={<OrgDetail />} />
        <Route path="courses" element={<CourseList />} />
        <Route path="courses/new" element={<CourseCreate />} />
        <Route path="courses/:id" element={<CourseDetail />} />
        <Route path="users" element={<UserList />} />
        <Route path="users/new" element={<UserCreate />} />
        <Route path="users/:id" element={<UserDetail />} />
        <Route path="feature-flags" element={<FeatureFlags />} />
        {(import.meta.env.DEV || import.meta.env.VITE_SHOW_DEV_TOOLS === 'true') && <Route path="dev/sms" element={<DevSmsPage />} />}
        <Route path="*" element={<Navigate to="/admin" replace />} />
      </Route>
    </Routes>
  );
}
```

- [ ] **Step 3: Delete tenant pages and tests**

Delete:
- `src/web/src/features/admin/pages/TenantList.tsx`
- `src/web/src/features/admin/pages/TenantCreate.tsx`
- `src/web/src/features/admin/pages/TenantDetail.tsx`
- `src/web/src/features/admin/__tests__/TenantList.test.tsx`
- `src/web/src/features/admin/__tests__/TenantDetail.test.tsx`

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: update admin layout and routes for new admin platform"
```

---

## Task 13: Frontend — Admin Pages (Organization CRUD)

**Files:**
- Create: `src/web/src/features/admin/pages/OrgList.tsx`
- Create: `src/web/src/features/admin/pages/OrgCreate.tsx`
- Create: `src/web/src/features/admin/pages/OrgDetail.tsx`

- [ ] **Step 1: Create OrgList page**

Create `src/web/src/features/admin/pages/OrgList.tsx`. Follow the existing `TenantList.tsx` pattern:
- Use `useOrganizations()` hook
- Summary cards: Total Organizations, Total Courses, Total Users
- Table: Name, Courses, Users, Created
- Click row → navigate to `/admin/organizations/{id}`
- "Create Organization" button → `/admin/organizations/new`

- [ ] **Step 2: Create OrgCreate page**

Create `src/web/src/features/admin/pages/OrgCreate.tsx`. Follow `TenantCreate.tsx` pattern:
- Zod schema with `name` field (required, min 1)
- `useCreateOrganization()` mutation
- On success: navigate to `/admin/organizations`

- [ ] **Step 3: Create OrgDetail page**

Create `src/web/src/features/admin/pages/OrgDetail.tsx`. Follow `TenantDetail.tsx` pattern:
- `useOrganization(id)` hook
- Show org name (editable), created date
- Table of courses in this org
- Table of users in this org
- Back button to `/admin/organizations`

- [ ] **Step 4: Lint to verify**

Run: `pnpm --dir src/web lint`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: add organization list, create, and detail pages"
```

---

## Task 14: Frontend — User Management Pages

**Files:**
- Create: `src/web/src/features/admin/pages/UserList.tsx`
- Create: `src/web/src/features/admin/pages/UserCreate.tsx`
- Create: `src/web/src/features/admin/pages/UserDetail.tsx`

- [ ] **Step 1: Create UserList page**

Create `src/web/src/features/admin/pages/UserList.tsx`:
- `useUsers()` hook
- Table: Display Name, Email, Role, Organization, Active, Created
- Active column: badge (green/gray)
- Click row → `/admin/users/{id}`
- "Create User" button → `/admin/users/new`

- [ ] **Step 2: Create UserCreate page**

Create `src/web/src/features/admin/pages/UserCreate.tsx`:
- Zod schema: identityId, email, displayName, role (select: Admin/Operator), organizationId (select from orgs, required for Operator)
- Use `useOrganizations()` to populate org dropdown
- `useCreateUser()` mutation
- On success: navigate to `/admin/users`

- [ ] **Step 3: Create UserDetail page**

Create `src/web/src/features/admin/pages/UserDetail.tsx`:
- Fetch user from `useUsers()` data (filter by id) — or add a dedicated `GET /auth/users/{id}` if needed
- Edit form: role dropdown, org dropdown, active toggle
- `useUpdateUser()` mutation
- Back button to `/admin/users`

- [ ] **Step 4: Lint to verify**

Run: `pnpm --dir src/web lint`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: add user list, create, and detail pages"
```

---

## Task 15: Frontend — Course Detail and Feature Flags Pages

**Files:**
- Create: `src/web/src/features/admin/pages/CourseDetail.tsx`
- Create: `src/web/src/features/admin/pages/FeatureFlags.tsx`
- Modify: `src/web/src/features/admin/pages/CourseList.tsx` (update to use org instead of tenant)

- [ ] **Step 1: Update CourseList to use Organization instead of Tenant**

In `src/web/src/features/admin/pages/CourseList.tsx`:
- Replace "Tenant" column header with "Organization"
- Update data references from `tenantName` to the org name (check what the `/courses` endpoint returns — may need to update the Course type)

- [ ] **Step 2: Create CourseDetail page**

Create `src/web/src/features/admin/pages/CourseDetail.tsx`:
- Fetch course by id (use `useCourses()` filtered, or add a `GET /courses/{id}` hook)
- Show: name, timezone, org, address, contact info
- Edit form for name and timezone
- Back button to `/admin/courses`

- [ ] **Step 3: Create FeatureFlags page**

Create `src/web/src/features/admin/pages/FeatureFlags.tsx`:
- Fetch all organizations and their courses
- Display a grid: rows = feature keys (`sms-notifications`, `dynamic-pricing`, `full-operator-app`), columns = org/course
- Toggle switches for each flag
- Use `useSetOrgFeatures()` and `useSetCourseFeatures()` mutations
- Feature keys can be hardcoded from `FeatureKeys.cs`: `sms-notifications`, `dynamic-pricing`, `full-operator-app`

- [ ] **Step 4: Lint to verify**

Run: `pnpm --dir src/web lint`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: add course detail and feature flags management pages"
```

---

## Task 16: Frontend — Analytics Dashboard

**Files:**
- Create: `src/web/src/features/admin/pages/Dashboard.tsx`

- [ ] **Step 1: Create Dashboard page**

Create `src/web/src/features/admin/pages/Dashboard.tsx`:

```tsx
import { useSummary, useFillRates, useBookingTrends, usePopularTimes, useWaitlistStats } from '../hooks/useAnalytics';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { LineChart, Line, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
```

Layout:
- **Row 1**: Summary cards (4 cards: Total Organizations, Total Courses, Active Users, Bookings Today) — use `useSummary()` hook
- **Row 2**: Fill Rates chart (line chart, last 7 days) — use `useFillRates()` hook
- **Row 3**: Booking Trends (line chart, last 30 days) + Popular Times (bar chart) side by side — use `useBookingTrends()` and `usePopularTimes()`
- **Row 4**: Waitlist Stats cards (Active Entries, Offers Sent, Accepted, Rejected) — use `useWaitlistStats()` hook

Each chart section uses shadcn `Card` with `CardHeader` + `CardTitle` + `CardContent`. Charts use Recharts `ResponsiveContainer` with 100% width.

Handle loading state: skeleton cards while data loads.
Handle empty state: "No data yet" message in chart areas.

- [ ] **Step 2: Lint to verify**

Run: `pnpm --dir src/web lint`
Expected: PASS

- [ ] **Step 3: Run frontend tests**

Run: `pnpm --dir src/web test`
Expected: PASS (existing tests should still pass; CourseList test may need tenant→org update in `CourseList.test.tsx`)

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: add analytics dashboard with summary cards and charts"
```

---

## Task 17: Update Existing Frontend Tests

**Files:**
- Modify: `src/web/src/features/admin/__tests__/CourseList.test.tsx`
- Modify: `src/web/src/features/admin/__tests__/CourseCreate.test.tsx`

- [ ] **Step 1: Update CourseList tests**

In `src/web/src/features/admin/__tests__/CourseList.test.tsx`:
- Update mock data: replace `tenantName` with organization references if the data shape changed
- Update column header assertions: "Tenant" → "Organization" if changed
- Update import paths if hooks changed

- [ ] **Step 2: Update CourseCreate tests**

In `src/web/src/features/admin/__tests__/CourseCreate.test.tsx`:
- Update tenant dropdown references to organization dropdown
- Update mock hooks from `useTenants` to `useOrganizations`

- [ ] **Step 3: Run all frontend tests**

Run: `pnpm --dir src/web test`
Expected: All 26+ test files pass.

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "test: update admin frontend tests for org-based model"
```

---

## Task 18: Full Build Verification

- [ ] **Step 1: Run full backend build**

Run: `dotnet build teeforce.slnx`
Expected: PASS

- [ ] **Step 2: Run dotnet format**

Run: `dotnet format teeforce.slnx`

- [ ] **Step 3: Run all backend tests**

Run: `dotnet test teeforce.slnx -v minimal`
Expected: PASS

- [ ] **Step 4: Run frontend lint and tests**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test`
Expected: PASS

- [ ] **Step 5: Run make dev to smoke test**

Run: `make dev`
Verify: API starts on :5221, Web starts on :3000, no runtime errors.

- [ ] **Step 6: Commit any format fixes**

```bash
git add -A && git commit -m "chore: format fixes"
```

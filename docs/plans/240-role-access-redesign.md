# Role Access Redesign: Claims-Based Course Authorization

## Problem Statement

The `CourseAccessAuthorizationHandler` uses role-based if-statements (switch on `AppUserRole`) with inconsistent resolution strategies per role:

- **Admin**: hardcoded pass-through (no DB query, no claims check)
- **Owner**: DB query at authorization time (`db.Courses.AnyAsync(c => c.Id == courseId && c.OrganizationId == orgId)`)
- **Staff**: claims-based check (reads pre-enriched `course_id` claims)

This creates three problems:

1. **Inconsistency**: Staff access is resolved from enriched claims (fast, no I/O), but Owner access requires a DB query in the authorization handler (slow, couples auth to EF).
2. **Role awareness leaking**: The authorization handler knows about role semantics. Every new role or access pattern requires modifying the handler's switch statement.
3. **Scattered role checks**: `AuthEndpoints.GetMe` also branches on role to determine which courses to return (Admin -> all, Owner -> org courses, Staff -> assigned courses). The `router.tsx` frontend checks `user.role === 'Admin'` for redirect logic.

## Current State

### Enrichment Middleware (`AppUserEnrichmentMiddleware`)

When a user authenticates, the middleware:

1. Loads the `AppUser` with `CourseAssignments` from DB (or cache)
2. Adds these claims to the principal:
   - `app_user_id` -- always
   - `organization_id` -- if the user has one
   - `role` -- the `AppUserRole` enum as a string
   - `permission` -- one claim per permission from `Permissions.GetForRole()`
   - `course_id` -- one claim per `CourseAssignment`

**Key gap**: For Owner users, `CourseAssignments` is typically empty. Owners access courses through their organization, not through explicit assignments. The middleware does not resolve the Owner's org -> courses relationship. This forces `CourseAccessAuthorizationHandler` to do a DB query at auth time.

### CourseAccessAuthorizationHandler

Receives a `courseId` from the route and decides access with a role switch:

- `Admin` -> succeed immediately
- `Owner` -> query DB for course-org relationship
- `Staff` -> check `course_id` claims

### ICurrentUser

Exposes raw claim data: `AppUserId`, `IdentityId`, `OrganizationId`, `Permissions`, `CourseIds`, `IsAuthenticated`. No higher-level accessors. Consumers that need "can this user access course X?" must implement their own logic (or rely on the authorization policy).

### AuthEndpoints.GetMe

Has its own parallel role-switch to determine which courses to return in the response:

```
if (appUser.Role == Admin) -> all courses
else if (appUser.Role == Owner && orgId) -> courses in org
else -> courses from assignments
```

### Frontend (router.tsx)

Checks `user.role === 'Admin'` to decide the default redirect (admin -> `/admin/tenants`, everyone else -> `/operator`). This is a UI routing concern and is acceptable -- it uses the role from the `/auth/me` response, not raw claims.

## Proposed Design

### Core Idea: Resolve accessible courses at enrichment time, not authorization time

The enrichment middleware already resolves Staff's accessible courses from `CourseAssignments`. Extend this to resolve Owner's accessible courses from the organization-course relationship. Admin gets a special marker indicating universal access.

After enrichment, authorization handlers operate purely on claims -- no role switch, no DB query.

### 1. Enrichment Changes (`AppUserEnrichmentMiddleware`)

Change how `CourseIds` is populated in `EnrichmentData`:

```
Role        | Current behavior           | New behavior
------------|----------------------------|----------------------------------
Admin       | CourseAssignments (empty)   | All course IDs from DB
Owner       | CourseAssignments (empty)   | All course IDs where course.OrganizationId == user.OrganizationId
Staff       | CourseAssignments           | CourseAssignments (unchanged)
```

**For Admin users**, add a special claim `course_access:all` instead of enumerating every course ID. This avoids an unbounded query and handles the case where courses are created after the cache is populated.

**For Owner users**, query `db.Courses.Where(c => c.OrganizationId == orgId).Select(c => c.Id)` and add those as `course_id` claims, exactly as Staff courses are added today.

**Updated EnrichmentData construction:**

```csharp
// Pseudocode
var courseIds = role switch
{
    AppUserRole.Admin => [],  // Admin uses course_access:all marker instead
    AppUserRole.Owner when appUser.OrganizationId.HasValue =>
        await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.OrganizationId == appUser.OrganizationId.Value)
            .Select(c => c.Id)
            .ToListAsync(),
    _ => appUser.CourseAssignments.Select(a => a.CourseId).ToList(),
};

var hasUniversalCourseAccess = role == AppUserRole.Admin;
```

**New claim:** `course_access:all` -- a single claim added for Admin users only.

**Updated claims list construction:**

```csharp
if (hasUniversalCourseAccess)
{
    claimsList.Add(new Claim("course_access", "all"));
}

foreach (var courseId in courseIds)
{
    claimsList.Add(new Claim("course_id", courseId.ToString()));
}
```

**Cache implications:** Owner course access is cached for 5 minutes (existing TTL). If a new course is added to the org, the Owner won't see it until cache expires. This is acceptable -- the same constraint already applies to Staff course assignments and permissions. Admin users are unaffected since they use the `course_access:all` marker.

### 2. CourseAccessAuthorizationHandler Changes

Replace the role-based switch with a two-step claims check:

```csharp
// Pseudocode
protected override Task HandleRequirementAsync(context, requirement)
{
    // Step 1: Must have app:access permission
    if (!context.User.FindAll("permission").Any(c => c.Value == Permissions.AppAccess))
        return;

    // Step 2: Extract courseId from route
    if (context.Resource is not HttpContext httpContext) return;
    var courseIdStr = httpContext.Request.RouteValues["courseId"]?.ToString();
    if (!Guid.TryParse(courseIdStr, out var courseId)) return;

    // Step 3: Check universal access OR specific course claim
    var hasUniversalAccess = context.User.FindAll("course_access").Any(c => c.Value == "all");
    if (hasUniversalAccess)
    {
        context.Succeed(requirement);
        return Task.CompletedTask;
    }

    var courseIds = context.User.FindAll("course_id")
        .Select(c => Guid.TryParse(c.Value, out var id) ? id : (Guid?)null)
        .Where(id => id.HasValue)
        .Select(id => id!.Value)
        .ToHashSet();

    if (courseIds.Contains(courseId))
    {
        context.Succeed(requirement);
    }

    return Task.CompletedTask;
}
```

**Key changes:**
- No `ApplicationDbContext` dependency (remove DI injection)
- No `AppUserRole` import
- No role-based switch statement
- Synchronous (no async DB query) -- can return `Task.CompletedTask`
- Two code paths: universal access (Admin) or specific course check (Owner/Staff, uniform)

### 3. ICurrentUser Changes

Add a higher-level accessor and expose the universal access flag:

```csharp
public interface ICurrentUser
{
    Guid? AppUserId { get; }
    string? IdentityId { get; }
    Guid? OrganizationId { get; }
    IReadOnlyList<string> Permissions { get; }
    IReadOnlyList<Guid> CourseIds { get; }
    bool IsAuthenticated { get; }
    bool HasUniversalCourseAccess { get; }  // NEW: true for Admin

    bool CanAccessCourse(Guid courseId);     // NEW: checks universal OR CourseIds.Contains
}
```

**Implementation:**

```csharp
public bool HasUniversalCourseAccess =>
    User?.FindAll("course_access").Any(c => c.Value == "all") ?? false;

public bool CanAccessCourse(Guid courseId) =>
    HasUniversalCourseAccess || CourseIds.Contains(courseId);
```

This lets endpoint code that needs ad-hoc course access checks use `currentUser.CanAccessCourse(id)` without reimplementing the logic.

### 4. AuthEndpoints.GetMe Changes

The `GetMe` endpoint currently branches on role to determine which courses to return. After enrichment resolves courses for all roles, this simplifies:

```csharp
// Before: three-branch role switch
// After:
List<CourseResponse> courses;
if (currentUser.HasUniversalCourseAccess)
{
    courses = await db.Courses
        .IgnoreQueryFilters()
        .Select(c => new CourseResponse(c.Id, c.Name))
        .ToListAsync();
}
else
{
    var courseIds = currentUser.CourseIds;
    courses = await db.Courses
        .IgnoreQueryFilters()
        .Where(c => courseIds.Contains(c.Id))
        .Select(c => new CourseResponse(c.Id, c.Name))
        .ToListAsync();
}
```

This reduces three branches to two, and neither branch checks the role enum. The `HasUniversalCourseAccess` flag is a capability check, not a role check.

**Note:** `GetMe` still returns `appUser.Role.ToString()` in the response -- that's correct. The frontend needs the role for UI routing (Admin -> admin layout). The point is that *access decisions* don't branch on role.

### 5. What Stays the Same

- **`Permissions` system**: unchanged. `PermissionAuthorizationHandler` already works purely from claims.
- **`role` claim**: still enriched. Frontend needs it for UI layout/routing decisions. The distinction is that *authorization handlers* no longer read it.
- **`organization_id` claim**: still enriched. Used by `ApplicationDbContext` query filter for multi-tenant scoping, `TenantIdEnricher` for Serilog, and `CourseEndpoints.CreateCourse` for defaulting the org. None of these are authorization decisions.
- **Frontend role check in `router.tsx`**: stays. This is a UX routing decision (which default page to show), not an access control decision.
- **`DevAuthHandler`**: unchanged.
- **`ScopeAuthorizationHandler`**: unchanged.

## Impact on Tests

### `CourseAccessAuthorizationHandlerTests`

All five tests need updates:

- **`Admin_AlwaysSucceeds`**: change `BuildUser` to add `course_access: all` claim instead of `AppUserRole.Admin` role claim. Remove DB setup (handler no longer needs it).
- **`Owner_CourseInTheirOrg_Succeeds`**: change to use `course_id` claims (like Staff tests). Remove DB setup.
- **`Owner_CourseInDifferentOrg_DoesNotSucceed`**: change to use `course_id` claims without the target course. Remove DB setup.
- **`Staff_AssignedToCourse_Succeeds`**: minimal change (already claims-based). Remove role claim dependency if handler no longer reads it.
- **`Staff_NotAssignedToCourse_DoesNotSucceed`**: same as above.
- **Handler constructor**: no longer takes `ApplicationDbContext`.
- **`BuildUser` helper**: remove role parameter, add optional `hasUniversalAccess` flag.

**Behavioral change justification:** The authorization handler's *behavior* is the same (Admin -> all, Owner -> their courses, Staff -> assigned courses). What changes is *where* the resolution happens (enrichment vs handler). The tests shift from testing "handler queries DB for Owner" to testing "handler checks claims for everyone uniformly." This is a genuine behavior change in the handler (it no longer knows about roles), so test assertion changes are warranted.

### `AppUserEnrichmentMiddlewareTests`

- **`AuthenticatedUser_ExistingAppUser_GetsEnrichedWithClaims`**: this test uses an Owner user. After the change, it should also assert `course_id` claims for courses in the Owner's org (need to add a course to the test setup).
- **New test**: Owner with courses in their org gets `course_id` claims enriched.
- **New test**: Admin gets `course_access: all` claim but no `course_id` claims.
- **New test**: Owner with no org gets empty `course_id` claims.

### `PermissionAuthorizationHandlerTests`

No changes needed -- already claims-based.

### Integration Tests (`CourseAccessIsolationTests`, `AuthEndpointsTests`)

These test end-to-end behavior and should continue passing since the enrichment middleware now does the resolution that the handler used to do. May need minor adjustments if they assert on specific claim shapes.

## Migration Path

This is a single-PR change -- no phased rollout needed:

1. Update `AppUserEnrichmentMiddleware` to resolve Owner courses and add `course_access:all` for Admin
2. Update `EnrichmentData` record to include `HasUniversalCourseAccess` bool
3. Simplify `CourseAccessAuthorizationHandler` to claims-only (remove `ApplicationDbContext` DI)
4. Add `HasUniversalCourseAccess` and `CanAccessCourse()` to `ICurrentUser` / `CurrentUser`
5. Simplify `AuthEndpoints.GetMe` course resolution
6. Update unit tests
7. Verify integration tests pass

No database migration needed. No breaking API changes. The `/auth/me` response shape is unchanged.

## Considerations

### Owner with many courses

An organization with hundreds of courses would add hundreds of `course_id` claims. This is a realistic concern but mirrors what would happen with Staff assigned to many courses. The claims are cached for 5 minutes and are just GUIDs (36 bytes each). For 100 courses that's ~3.6KB of claims -- acceptable.

If this becomes a problem in the future, the fallback is to add an `organization_access:{orgId}` claim pattern (similar to `course_access:all`) and have the handler check org-course membership. But this reintroduces a DB query, so defer until there's evidence of a problem.

### Cache invalidation when courses are added to an org

If a new course is created under an Owner's organization, the Owner won't see it until their enrichment cache expires (5 minutes). This is the same behavior Staff already has when assigned to a new course. The `UpdateUserCourses` endpoint already evicts the cache on assignment changes. A similar cache eviction should happen when a course is created -- this is a pre-existing gap that applies to the query filter (`CurrentOrganizationId`) as well, not introduced by this change.

### Admin course list growth

Admins do NOT get individual `course_id` claims -- they get the `course_access:all` marker. The `GetMe` endpoint still queries the DB for the full course list for Admin users. This is appropriate because `GetMe` is a read endpoint that should reflect current state, while the `course_access:all` marker is an authorization signal.

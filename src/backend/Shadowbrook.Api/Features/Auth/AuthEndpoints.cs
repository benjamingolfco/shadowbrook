using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Shadowbrook.Api.Infrastructure.Auth;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.Auth;

public static class AuthEndpoints
{
    [WolverineGet("/auth/me")]
    [Authorize(Policy = "RequireAppAccess")]
    public static async Task<IResult> GetMe(
        [NotBody] ICurrentUser currentUser,
        [NotBody] ApplicationDbContext db)
    {
        if (currentUser.AppUserId is null)
        {
            return Results.Unauthorized();
        }

        var appUser = await db.AppUsers
            .FirstOrDefaultAsync(u => u.Id == currentUser.AppUserId.Value);

        if (appUser is null)
        {
            return Results.NotFound();
        }

        OrgResponse? org = null;
        if (appUser.OrganizationId.HasValue)
        {
            var organization = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == appUser.OrganizationId.Value);
            if (organization is not null)
            {
                org = new OrgResponse(organization.Id, organization.Name);
            }
        }

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

        var response = new MeResponse(
            appUser.Id,
            appUser.Email,
            appUser.DisplayName,
            appUser.Role.ToString(),
            org,
            courses,
            currentUser.Permissions.ToList());

        return Results.Ok(response);
    }

    [WolverineGet("/auth/users")]
    [Authorize(Policy = "RequireUsersManage")]
    public static async Task<IResult> GetUsers([NotBody] ApplicationDbContext db)
    {
        var users = await db.AppUsers
            .Include(u => u.CourseAssignments)
            .Select(u => new UserListResponse(
                u.Id,
                u.Email,
                u.DisplayName,
                u.Role.ToString(),
                u.OrganizationId,
                u.IsActive,
                u.CourseAssignments.Select(a => a.CourseId).ToList()))
            .ToListAsync();

        return Results.Ok(users);
    }

    [WolverinePost("/auth/users")]
    [Authorize(Policy = "RequireUsersManage")]
    public static async Task<IResult> CreateUser(
        CreateUserRequest request,
        [NotBody] ApplicationDbContext db)
    {
        var exists = await db.AppUsers.AnyAsync(u => u.IdentityId == request.IdentityId);
        if (exists)
        {
            return Results.Conflict(new { error = "A user with this identity ID already exists." });
        }

        if (!Enum.TryParse<AppUserRole>(request.Role, ignoreCase: true, out var role))
        {
            return Results.BadRequest(new { error = $"Invalid role: {request.Role}." });
        }

        var appUser = AppUser.Create(
            request.IdentityId,
            request.Email,
            request.DisplayName,
            role,
            request.OrganizationId);

        db.AppUsers.Add(appUser);

        var response = new UserListResponse(
            appUser.Id,
            appUser.Email,
            appUser.DisplayName,
            appUser.Role.ToString(),
            appUser.OrganizationId,
            appUser.IsActive,
            []);

        return Results.Created($"/auth/users/{appUser.Id}", response);
    }

    [WolverinePut("/auth/users/{id}")]
    [Authorize(Policy = "RequireUsersManage")]
    public static async Task<IResult> UpdateUser(
        Guid id,
        UpdateUserRequest request,
        [NotBody] ApplicationDbContext db,
        [NotBody] IMemoryCache cache)
    {
        var appUser = await db.AppUsers
            .Include(u => u.CourseAssignments)
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

        cache.Remove(AppUserEnrichmentMiddleware.CacheKey(appUser.IdentityId));

        var response = new UserListResponse(
            appUser.Id,
            appUser.Email,
            appUser.DisplayName,
            appUser.Role.ToString(),
            appUser.OrganizationId,
            appUser.IsActive,
            appUser.CourseAssignments.Select(a => a.CourseId).ToList());

        return Results.Ok(response);
    }

    [WolverinePut("/auth/users/{id}/courses")]
    [Authorize(Policy = "RequireUsersManage")]
    public static async Task<IResult> UpdateUserCourses(
        Guid id,
        UpdateUserCoursesRequest request,
        [NotBody] ApplicationDbContext db,
        [NotBody] IMemoryCache cache)
    {
        var appUser = await db.AppUsers
            .Include(u => u.CourseAssignments)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (appUser is null)
        {
            return Results.NotFound();
        }

        // Unassign courses not in the new list
        var toRemove = appUser.CourseAssignments
            .Where(a => !request.CourseIds.Contains(a.CourseId))
            .ToList();

        foreach (var assignment in toRemove)
        {
            appUser.UnassignCourse(assignment.CourseId);
        }

        // Assign courses not already assigned
        foreach (var courseId in request.CourseIds)
        {
            if (!appUser.CourseAssignments.Any(a => a.CourseId == courseId))
            {
                appUser.AssignCourse(courseId);
            }
        }

        cache.Remove(AppUserEnrichmentMiddleware.CacheKey(appUser.IdentityId));

        var response = new UserListResponse(
            appUser.Id,
            appUser.Email,
            appUser.DisplayName,
            appUser.Role.ToString(),
            appUser.OrganizationId,
            appUser.IsActive,
            appUser.CourseAssignments.Select(a => a.CourseId).ToList());

        return Results.Ok(response);
    }
}

public sealed record MeResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    OrgResponse? Organization,
    List<CourseResponse> Courses,
    List<string> Permissions);

public sealed record OrgResponse(Guid Id, string Name);

public sealed record CourseResponse(Guid Id, string Name);

public sealed record UserListResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    Guid? OrganizationId,
    bool IsActive,
    List<Guid> CourseIds);

public sealed record CreateUserRequest(
    string IdentityId,
    string Email,
    string DisplayName,
    string Role,
    Guid? OrganizationId);

public sealed record UpdateUserRequest(bool? IsActive);

public sealed record UpdateUserCoursesRequest(List<Guid> CourseIds);

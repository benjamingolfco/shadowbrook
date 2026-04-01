using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Shadowbrook.Api.Auth;
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

        if (appUser.Role == AppUserRole.Admin)
        {
            courses = await db.Courses
                .IgnoreQueryFilters()
                .Select(c => new CourseResponse(c.Id, c.Name))
                .ToListAsync();
        }
        else if (appUser.Role == AppUserRole.Operator && appUser.OrganizationId.HasValue)
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
            .Select(u => new UserListResponse(
                u.Id,
                u.Email,
                u.DisplayName,
                u.Role.ToString(),
                u.OrganizationId,
                u.IsActive))
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

        var role = Enum.Parse<AppUserRole>(request.Role, ignoreCase: true);

        var appUser = role == AppUserRole.Admin
            ? AppUser.CreateAdmin(request.IdentityId, request.Email, request.DisplayName)
            : AppUser.CreateOperator(request.IdentityId, request.Email, request.DisplayName, request.OrganizationId!.Value);

        db.AppUsers.Add(appUser);

        var response = new UserListResponse(
            appUser.Id,
            appUser.Email,
            appUser.DisplayName,
            appUser.Role.ToString(),
            appUser.OrganizationId,
            appUser.IsActive);

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

        if (request.Role is not null)
        {
            var newRole = Enum.Parse<AppUserRole>(request.Role, ignoreCase: true);

            if (newRole == AppUserRole.Admin)
            {
                appUser.MakeAdmin();
            }
            else
            {
                appUser.AssignToOrganization(request.OrganizationId!.Value);
            }
        }

        cache.Remove(AppUserEnrichmentMiddleware.CacheKey(appUser.IdentityId));

        var response = new UserListResponse(
            appUser.Id,
            appUser.Email,
            appUser.DisplayName,
            appUser.Role.ToString(),
            appUser.OrganizationId,
            appUser.IsActive);

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
    bool IsActive);

public sealed record CreateUserRequest(
    string IdentityId,
    string Email,
    string DisplayName,
    string Role,
    Guid? OrganizationId);

public sealed record UpdateUserRequest(bool? IsActive, string? Role, Guid? OrganizationId);

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.IdentityId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.DisplayName).NotEmpty();
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

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Role)
            .Must(r => Enum.TryParse<AppUserRole>(r!, ignoreCase: true, out _))
            .When(x => x.Role is not null)
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

using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.Services;
using Wolverine.Http;

namespace Teeforce.Api.Features.Auth;

public static class AuthEndpoints
{
    [WolverineGet("/auth/me")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> GetMe(
        [NotBody] IUserContext userContext,
        [NotBody] ApplicationDbContext db)
    {
        if (userContext.AppUserId is null)
        {
            return Results.Unauthorized();
        }

        var appUser = await db.AppUsers
            .FirstOrDefaultAsync(u => u.Id == userContext.AppUserId.Value);

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

        List<OrgResponse>? organizations = null;
        if (appUser.Role == AppUserRole.Admin)
        {
            organizations = await db.Organizations
                .OrderBy(o => o.Name)
                .Select(o => new OrgResponse(o.Id, o.Name))
                .ToListAsync();
        }

        List<CourseResponse> courses;

        if (appUser.Role == AppUserRole.Admin)
        {
            courses = userContext.OrganizationId is { } adminOrgId
                ? await db.Courses
                    .IgnoreQueryFilters()
                    .Where(c => c.OrganizationId == adminOrgId)
                    .Select(c => new CourseResponse(c.Id, c.Name))
                    .ToListAsync()
                : await db.Courses
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
            appUser.FirstName,
            appUser.LastName,
            appUser.Role.ToString(),
            org,
            organizations,
            courses,
            userContext.Permissions.ToList());

        return Results.Ok(response);
    }

    [WolverineGet("/auth/users")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    public static async Task<IResult> GetUsers([NotBody] ApplicationDbContext db)
    {
        var users = await db.AppUsers
            .Select(u => new UserListResponse(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Role.ToString(),
                u.OrganizationId,
                u.IsActive,
                u.InviteSentAt))
            .ToListAsync();

        return Results.Ok(users);
    }

    [WolverinePost("/auth/users")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    public static async Task<IResult> CreateUser(
        CreateUserRequest request,
        [NotBody] ApplicationDbContext db,
        [NotBody] IAppUserEmailUniquenessChecker emailChecker)
    {
        var role = Enum.Parse<AppUserRole>(request.Role, ignoreCase: true);

        var appUser = role == AppUserRole.Admin
            ? await AppUser.CreateAdmin(request.Email, emailChecker, request.SendInvite)
            : await AppUser.CreateOperator(request.Email, request.OrganizationId!.Value, emailChecker, request.SendInvite);

        db.AppUsers.Add(appUser);

        var response = new UserListResponse(
            appUser.Id,
            appUser.Email,
            appUser.FirstName,
            appUser.LastName,
            appUser.Role.ToString(),
            appUser.OrganizationId,
            appUser.IsActive,
            appUser.InviteSentAt);

        return Results.Created($"/auth/users/{appUser.Id}", response);
    }

    [WolverinePut("/auth/users/{id}")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    public static async Task<IResult> UpdateUser(
        Guid id,
        UpdateUserRequest request,
        [NotBody] ApplicationDbContext db)
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

}

public sealed record MeResponse(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string Role,
    OrgResponse? Organization,
    List<OrgResponse>? Organizations,
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
    bool IsActive,
    DateTimeOffset? InviteSentAt);

public sealed record CreateUserRequest(
    string Email,
    string Role,
    Guid? OrganizationId,
    bool SendInvite = false);

public sealed record UpdateUserRequest(bool? IsActive, string? Role, Guid? OrganizationId);

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

using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Auth;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.OrganizationAggregate;
using Shadowbrook.Domain.Services;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.Organizations;

public static class OrganizationEndpoints
{
    [WolverineGet("/organizations")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    public static async Task<IResult> GetOrganizations([NotBody] ApplicationDbContext db)
    {
        var orgs = await db.Organizations
            .Select(o => new OrganizationListResponse(
                o.Id,
                o.Name,
                db.Courses.IgnoreQueryFilters().Count(c => c.OrganizationId == o.Id),
                db.AppUsers.Count(u => u.OrganizationId == o.Id),
                o.CreatedAt))
            .ToListAsync();

        return Results.Ok(orgs);
    }

    [WolverineGet("/organizations/{id}")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    public static async Task<IResult> GetOrganization(Guid id, [NotBody] ApplicationDbContext db)
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
            .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName, Role = u.Role.ToString(), u.IsActive })
            .ToListAsync();

        return Results.Ok(new { org.Id, org.Name, org.CreatedAt, Courses = courses, Users = users });
    }

    [WolverinePost("/organizations")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    public static async Task<IResult> CreateOrganization(
        CreateOrganizationRequest request,
        [NotBody] ApplicationDbContext db,
        [NotBody] IAppUserEmailUniquenessChecker emailChecker)
    {
        var org = Organization.Create(request.Name);
        db.Organizations.Add(org);

        var appUser = await AppUser.CreateOperatorAsync(request.OperatorEmail, org.Id, emailChecker);
        db.AppUsers.Add(appUser);

        return Results.Created($"/organizations/{org.Id}", new OrganizationResponse(org.Id, org.Name));
    }

    [WolverinePut("/organizations/{id}")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
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
        return Results.Ok(new OrganizationResponse(org.Id, org.Name));
    }
}

public sealed record OrganizationListResponse(Guid Id, string Name, int CourseCount, int UserCount, DateTimeOffset CreatedAt);
public sealed record OrganizationResponse(Guid Id, string Name);
public sealed record CreateOrganizationRequest(string Name, string OperatorEmail);
public sealed record UpdateOrganizationRequest(string Name);

public class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OperatorEmail).NotEmpty().EmailAddress();
    }
}

public class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Auth;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.FeatureFlags;

public static class FeatureEndpoints
{
    [WolverineGet("/features")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> GetFeatures(
        Guid? courseId,
        [NotBody] IFeatureService featureService,
        [NotBody] ApplicationDbContext db,
        [NotBody] IUserContext userContext,
        [NotBody] HttpContext httpContext)
    {
        Dictionary<string, bool>? orgFlags = null;
        Dictionary<string, bool>? courseFlags = null;

        if (userContext.OrganizationId is { } orgId)
        {
            orgFlags = await db.Organizations
                .Where(o => o.Id == orgId)
                .Select(o => o.FeatureFlags)
                .FirstOrDefaultAsync();
        }

        if (httpContext.Request.Query.TryGetValue("courseId", out var courseIdStr)
            && Guid.TryParse(courseIdStr, out var cId))
        {
            courseFlags = await db.Courses
                .Where(c => c.Id == cId)
                .Select(c => c.FeatureFlags)
                .FirstOrDefaultAsync();
        }

        return Results.Ok(featureService.GetAllForCourse(orgFlags, courseFlags));
    }

    [WolverinePut("/organizations/{id}/features")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
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
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
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
}

public sealed record SetFeaturesRequest(Dictionary<string, bool> Flags);

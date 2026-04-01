using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.FeatureFlags;

public static class FeatureEndpoints
{
    [WolverineGet("/features")]
    [Authorize(Policy = "RequireAppAccess")]
    public static async Task<IResult> GetFeatures(
        [NotBody] IFeatureService featureService,
        [NotBody] ApplicationDbContext db,
        [NotBody] ICurrentUser currentUser,
        [NotBody] HttpContext httpContext)
    {
        Dictionary<string, bool>? orgFlags = null;
        Dictionary<string, bool>? courseFlags = null;

        if (currentUser.OrganizationId is { } orgId)
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
}

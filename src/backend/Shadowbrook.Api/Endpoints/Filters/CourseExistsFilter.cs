using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;

namespace Shadowbrook.Api.Endpoints.Filters;

public class CourseExistsFilter(ApplicationDbContext db) : IEndpointFilter
{
    private static readonly string[] courseIdRouteKeys = ["courseId", "id"];

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        Guid? courseId = null;
        foreach (var key in courseIdRouteKeys)
        {
            if (context.HttpContext.GetRouteValue(key) is string value && Guid.TryParse(value, out var parsed))
            {
                courseId = parsed;
                break;
            }
        }

        if (courseId is null)
        {
            return Results.BadRequest(new { error = "Invalid course ID." });
        }

        var exists = await db.Courses.AnyAsync(c => c.Id == courseId.Value);
        if (!exists)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        return await next(context);
    }
}

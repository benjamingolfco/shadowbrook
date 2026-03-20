using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Wolverine.Http;

namespace Shadowbrook.Api.Endpoints.Middleware;

public static class CourseExistsMiddleware
{
    public static async Task<IResult?> Before(Guid courseId, [NotBody] ApplicationDbContext db)
    {
        var exists = await db.Courses.AnyAsync(c => c.Id == courseId);
        return exists ? null : Results.NotFound(new { error = "Course not found." });
    }
}

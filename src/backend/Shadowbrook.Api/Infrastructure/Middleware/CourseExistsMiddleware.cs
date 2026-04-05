using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Wolverine.Http;

namespace Shadowbrook.Api.Infrastructure.Middleware;

public static class CourseExistsMiddleware
{
    public static async Task<IResult?> Before(
        Guid courseId,
        [NotBody] ApplicationDbContext db,
        [NotBody] CourseContext courseContext,
        [NotBody] TimeProvider timeProvider)
    {
        var course = await db.Courses
            .Where(c => c.Id == courseId)
            .Select(c => new { c.TimeZoneId })
            .FirstOrDefaultAsync();

        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var today = CourseTime.Today(timeProvider, course.TimeZoneId);
        var now = CourseTime.Now(timeProvider, course.TimeZoneId);
        courseContext.Set(courseId, course.TimeZoneId, today, now);

        return null;
    }
}

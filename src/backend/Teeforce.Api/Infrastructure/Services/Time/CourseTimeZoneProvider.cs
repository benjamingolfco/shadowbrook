using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

public class CourseTimeZoneProvider(ApplicationDbContext db) : ICourseTimeZoneProvider
{
    public async Task<string> GetTimeZoneIdAsync(Guid courseId) =>
        await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == courseId)
            .Select(c => c.TimeZoneId)
            .FirstAsync();
}

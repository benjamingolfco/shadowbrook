using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Api.Infrastructure.Services;

public class CourseTimeZoneProvider(ApplicationDbContext db) : ICourseTimeZoneProvider
{
    public async Task<string> GetTimeZoneIdAsync(Guid courseId) =>
        await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == courseId)
            .Select(c => c.TimeZoneId)
            .FirstAsync();
}

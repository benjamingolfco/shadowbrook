using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.CoursePricingAggregate;

namespace Teeforce.Api.Infrastructure.Repositories;

public class CoursePricingSettingsRepository(ApplicationDbContext db) : ICoursePricingSettingsRepository
{
    public async Task<CoursePricingSettings?> GetByIdAsync(Guid id) =>
        await db.CoursePricingSettings
            .Include(s => s.RateSchedules)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<CoursePricingSettings?> GetByCourseIdAsync(Guid courseId, CancellationToken ct = default) =>
        await db.CoursePricingSettings
            .Include(s => s.RateSchedules)
            .FirstOrDefaultAsync(s => s.CourseId == courseId, ct);

    public void Add(CoursePricingSettings settings) => db.CoursePricingSettings.Add(settings);
}

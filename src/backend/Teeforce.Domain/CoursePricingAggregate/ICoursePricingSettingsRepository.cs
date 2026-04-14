using Teeforce.Domain.Common;

namespace Teeforce.Domain.CoursePricingAggregate;

public interface ICoursePricingSettingsRepository : IRepository<CoursePricingSettings>
{
    void Add(CoursePricingSettings settings);
    Task<CoursePricingSettings?> GetByCourseIdAsync(Guid courseId, CancellationToken ct = default);
}

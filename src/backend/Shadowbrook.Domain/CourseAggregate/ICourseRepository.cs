using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.CourseAggregate;

public interface ICourseRepository : IRepository<Course>
{
    Task<List<Course>> GetByTenantIdAsync(Guid tenantId);
    Task<bool> ExistsByNameAsync(Guid tenantId, string name);
    void Add(Course course);
}

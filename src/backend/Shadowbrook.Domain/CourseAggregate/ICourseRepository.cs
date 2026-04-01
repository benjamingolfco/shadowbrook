using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.CourseAggregate;

public interface ICourseRepository : IRepository<Course>
{
    Task<Course?> GetByIdAsync(Guid id);
    Task<List<Course>> GetByOrganizationIdAsync(Guid organizationId);
    Task<List<Course>> GetByTenantIdAsync(Guid tenantId);
    Task<bool> ExistsByNameAsync(Guid tenantId, string name);
    void Add(Course course);
}

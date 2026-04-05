using Teeforce.Domain.Common;

namespace Teeforce.Domain.CourseAggregate;

public interface ICourseRepository : IRepository<Course>
{
    Task<List<Course>> GetByOrganizationIdAsync(Guid organizationId);
    Task<bool> ExistsByNameAsync(Guid organizationId, string name);
    void Add(Course course);
}

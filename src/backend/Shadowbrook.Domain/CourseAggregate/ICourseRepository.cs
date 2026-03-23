namespace Shadowbrook.Domain.CourseAggregate;

public interface ICourseRepository
{
    Task<Course?> GetByIdAsync(Guid id);
    Task<List<Course>> GetByTenantIdAsync(Guid tenantId);
    Task<bool> ExistsByNameAsync(Guid tenantId, string name);
    void Add(Course course);
}

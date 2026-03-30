namespace Shadowbrook.Domain.CourseAggregate;

public interface ICourseRepository
{
    Task<Course?> GetByIdAsync(Guid id);
    Task<List<Course>> GetByOrganizationIdAsync(Guid organizationId);
    Task<bool> ExistsByNameAsync(Guid organizationId, string name);
    void Add(Course course);
}

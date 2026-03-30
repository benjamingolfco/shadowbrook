using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.CourseAggregate;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class CourseRepository(ApplicationDbContext db) : ICourseRepository
{
    public async Task<Course?> GetByIdAsync(Guid id) =>
        await db.Courses.FirstOrDefaultAsync(c => c.Id == id);

    public async Task<List<Course>> GetByTenantIdAsync(Guid tenantId) =>
        await db.Courses.IgnoreQueryFilters().Where(c => c.OrganizationId == tenantId).ToListAsync();

    public async Task<bool> ExistsByNameAsync(Guid tenantId, string name) =>
        await db.Courses.IgnoreQueryFilters().AnyAsync(c => c.OrganizationId == tenantId && c.Name.ToLower() == name.ToLower());

    public void Add(Course course) => db.Courses.Add(course);
}

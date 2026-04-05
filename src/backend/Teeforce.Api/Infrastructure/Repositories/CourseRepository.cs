using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.CourseAggregate;

namespace Teeforce.Api.Infrastructure.Repositories;

public class CourseRepository(ApplicationDbContext db) : ICourseRepository
{
    public async Task<Course?> GetByIdAsync(Guid id) =>
        await db.Courses.FirstOrDefaultAsync(c => c.Id == id);

    public async Task<List<Course>> GetByOrganizationIdAsync(Guid organizationId) =>
        await db.Courses.IgnoreQueryFilters().Where(c => c.OrganizationId == organizationId).ToListAsync();

    public async Task<bool> ExistsByNameAsync(Guid organizationId, string name) =>
        await db.Courses.IgnoreQueryFilters().AnyAsync(c => c.OrganizationId == organizationId && c.Name.ToLower() == name.ToLower());

    public void Add(Course course) => db.Courses.Add(course);
}

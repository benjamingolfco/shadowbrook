using Microsoft.EntityFrameworkCore;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.TenantAggregate;

namespace Shadowbrook.Api.Infrastructure.Data;

public static class E2ESeedData
{
    private const string TenantOrgName = "E2E Test Golf Group";

    public static async Task EnsureAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.OrganizationName == TenantOrgName);

        if (tenant is null)
        {
            tenant = Tenant.Create(
                TenantOrgName,
                "E2E Admin",
                "e2e@shadowbrook.golf",
                "5550000000");

            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        await EnsureCourseAsync(db, tenant.Id, "Pine Valley Test", "America/New_York");
        await EnsureCourseAsync(db, tenant.Id, "Augusta Test", "America/New_York");
        await EnsureCourseAsync(db, tenant.Id, "Pebble Beach Test", "America/Los_Angeles");
    }

    private static async Task EnsureCourseAsync(
        ApplicationDbContext db,
        Guid tenantId,
        string name,
        string timeZoneId)
    {
        var exists = await db.Courses
            .IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId && c.Name == name);

        if (exists)
        {
            return;
        }

        var course = Course.Create(tenantId, name, timeZoneId);
        db.Courses.Add(course);
        await db.SaveChangesAsync();
    }
}

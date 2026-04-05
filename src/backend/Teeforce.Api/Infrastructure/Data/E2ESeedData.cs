using Microsoft.EntityFrameworkCore;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.OrganizationAggregate;
using Teeforce.Domain.Services;
using Teeforce.Domain.TenantAggregate;

namespace Teeforce.Api.Infrastructure.Data;

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
                "e2e@benjamingolfco.onmicrosoft.com",
                "5550000000");

            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Name == TenantOrgName);

        if (organization is null)
        {
            organization = Organization.Create(TenantOrgName);
            db.Organizations.Add(organization);
            await db.SaveChangesAsync();
        }

        await EnsureCourseAsync(db, organization.Id, "Pine Valley Test", "America/New_York");
        await EnsureCourseAsync(db, organization.Id, "Augusta Test", "America/New_York");
        await EnsureCourseAsync(db, organization.Id, "Pebble Beach Test", "America/Los_Angeles");
        await EnsureCourseAsync(db, organization.Id, "E2E Walkup Course", "Etc/UTC");

        await EnsureAppUsersAsync(db, organization.Id);
    }

    private static async Task EnsureCourseAsync(
        ApplicationDbContext db,
        Guid organizationId,
        string name,
        string timeZoneId)
    {
        var exists = await db.Courses
            .IgnoreQueryFilters()
            .AnyAsync(c => c.OrganizationId == organizationId && c.Name == name);

        if (exists)
        {
            return;
        }

        var course = Course.Create(organizationId, name, timeZoneId);
        db.Courses.Add(course);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureAppUsersAsync(ApplicationDbContext db, Guid organizationId)
    {
        var emailChecker = new SeedEmailChecker(db);

        var adminExists = await db.AppUsers
            .AnyAsync(u => u.Email == "e2e-admin@benjamingolfco.onmicrosoft.com");

        if (!adminExists)
        {
            var admin = await AppUser.CreateAdmin("e2e-admin@benjamingolfco.onmicrosoft.com", emailChecker);
            admin.Activate();
            db.AppUsers.Add(admin);
            await db.SaveChangesAsync();
        }

        var operatorExists = await db.AppUsers
            .AnyAsync(u => u.Email == "e2e-operator@benjamingolfco.onmicrosoft.com");

        if (!operatorExists)
        {
            var operatorUser = await AppUser.CreateOperator(
                "e2e-operator@benjamingolfco.onmicrosoft.com",
                organizationId,
                emailChecker);
            operatorUser.Activate();
            db.AppUsers.Add(operatorUser);
            await db.SaveChangesAsync();
        }
    }

    private sealed class SeedEmailChecker(ApplicationDbContext db) : IAppUserEmailUniquenessChecker
    {
        public async Task<bool> IsEmailInUse(string email, CancellationToken ct = default) =>
            await db.AppUsers.AnyAsync(u => u.Email == email, ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.OrganizationAggregate;
using Teeforce.Domain.Services;

namespace Teeforce.Api.Infrastructure.Data;

/// <summary>
/// Seeds manual-testing accounts and their owning org. Separate from <see cref="E2ESeedData"/>,
/// which is owned by the Playwright automation suite. Rows are created unlinked (no IdentityId);
/// first Entra login matches by email via <c>AppUserClaimsTransformation</c>. If the Entra user
/// does not yet exist, use the admin UI's Send Invite flow to provision it.
/// </summary>
public static class DevSeedData
{
    private const string OrgName = "Benjamin Golf Co";
    private const string AdminEmail = "admin-test@benjamingolfco.onmicrosoft.com";
    private const string PineValleyOperatorEmail = "pine-valley-operator-test@benjamingolfco.onmicrosoft.com";

    public static async Task EnsureAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Name == OrgName);

        if (organization is null)
        {
            organization = Organization.Create(OrgName);
            db.Organizations.Add(organization);
            await db.SaveChangesAsync();
        }

        await EnsureCourseAsync(db, organization.Id, "Pine Valley", "America/New_York");
        await EnsureCourseAsync(db, organization.Id, "Augusta National", "America/New_York");
        await EnsureCourseAsync(db, organization.Id, "Pebble Beach", "America/Los_Angeles");

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

        if (!await db.AppUsers.AnyAsync(u => u.Email == AdminEmail))
        {
            var admin = await AppUser.CreateAdmin(AdminEmail, emailChecker);
            admin.Activate();
            db.AppUsers.Add(admin);
            await db.SaveChangesAsync();
        }

        if (!await db.AppUsers.AnyAsync(u => u.Email == PineValleyOperatorEmail))
        {
            var op = await AppUser.CreateOperator(PineValleyOperatorEmail, organizationId, emailChecker);
            op.Activate();
            db.AppUsers.Add(op);
            await db.SaveChangesAsync();
        }
    }

    private sealed class SeedEmailChecker(ApplicationDbContext db) : IAppUserEmailUniquenessChecker
    {
        public async Task<bool> IsEmailInUse(string email, CancellationToken ct = default) =>
            await db.AppUsers.AnyAsync(u => u.Email == email, ct);
    }
}

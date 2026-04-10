using Microsoft.EntityFrameworkCore;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.OrganizationAggregate;
using Teeforce.Domain.Services;
using Teeforce.Domain.TeeSheetAggregate;
using TeeSheetAggregate = Teeforce.Domain.TeeSheetAggregate.TeeSheet;

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
        var timeProvider = scope.ServiceProvider.GetRequiredService<ITimeProvider>();

        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Name == OrgName);

        if (organization is null)
        {
            organization = Organization.Create(OrgName);
            db.Organizations.Add(organization);
            await db.SaveChangesAsync();
        }

        await EnsureCourseAsync(db, timeProvider, organization.Id, "Pine Valley", "America/New_York");
        await EnsureCourseAsync(db, timeProvider, organization.Id, "Augusta National", "America/New_York");
        await EnsureCourseAsync(db, timeProvider, organization.Id, "Pebble Beach", "America/Los_Angeles");

        await EnsureAppUsersAsync(db, organization.Id);
    }

    private static async Task EnsureCourseAsync(
        ApplicationDbContext db,
        ITimeProvider timeProvider,
        Guid organizationId,
        string name,
        string timeZoneId)
    {
        var existing = await db.Courses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrganizationId == organizationId && c.Name == name);

        Course course;
        if (existing is null)
        {
            course = Course.Create(organizationId, name, timeZoneId);
            course.UpdateTeeTimeSettings(10, new TimeOnly(7, 0), new TimeOnly(17, 0));
            db.Courses.Add(course);
            await db.SaveChangesAsync();
        }
        else
        {
            course = existing;
            if (course.TeeTimeIntervalMinutes is null || course.FirstTeeTime is null || course.LastTeeTime is null)
            {
                course.UpdateTeeTimeSettings(10, new TimeOnly(7, 0), new TimeOnly(17, 0));
                await db.SaveChangesAsync();
            }
        }

        // Seed a published tee sheet for a stable demo date so operator views have data immediately.
        var demoDate = new DateOnly(2026, 6, 1);
        var sheetExists = await db.TeeSheets
            .IgnoreQueryFilters()
            .AnyAsync(s => s.CourseId == course.Id && s.Date == demoDate);

        if (!sheetExists)
        {
            var settings = course.CurrentScheduleDefaults();
            var sheet = TeeSheetAggregate.Draft(course.Id, demoDate, settings, timeProvider);
            sheet.Publish(timeProvider);
            db.TeeSheets.Add(sheet);
            await db.SaveChangesAsync();
        }
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

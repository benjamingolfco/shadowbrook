using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;

namespace Shadowbrook.Api.Infrastructure.Auth;

public class AppUserEnrichmentMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public static string CacheKey(string identityId) => $"appuser:{identityId}";

    private readonly RequestDelegate next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ApplicationDbContext db,
        IMemoryCache cache,
        IConfiguration configuration,
        IHostEnvironment env,
        ILogger<AppUserEnrichmentMiddleware> logger)
    {
        var oid = context.User?.FindFirst("oid")?.Value
            ?? context.User?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (context.User?.Identity?.IsAuthenticated == true && oid is not null)
        {
            var seedAdminEmails = env.IsDevelopment()
                ? configuration.GetValue<string>("Auth:SeedAdminEmails")
                    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    ?? []
                : [];
            var isActive = await EnrichFromAppUserAsync(context, db, cache, oid, seedAdminEmails, logger);

            if (!isActive)
            {
                logger.LogWarning("Deactivated user attempted access. IdentityId: {IdentityId}", oid);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Your account has been deactivated." });
                return;
            }
        }

        await this.next(context);
    }

    private static async Task<bool> EnrichFromAppUserAsync(
        HttpContext context, ApplicationDbContext db, IMemoryCache cache, string oid, string[] seedAdminEmails,
        ILogger<AppUserEnrichmentMiddleware> logger)
    {
        var cacheKey = CacheKey(oid);

        if (!cache.TryGetValue(cacheKey, out EnrichmentData? enrichmentData))
        {
            var appUser = await db.AppUsers
                .Include(u => u.CourseAssignments)
                .FirstOrDefaultAsync(u => u.IdentityId == oid);

            if (appUser is null)
            {
                var email = context.User?.FindFirst("emails")?.Value
                    ?? context.User?.FindFirst("email")?.Value
                    ?? context.User?.FindFirst("preferred_username")?.Value
                    ?? string.Empty;
                var name = context.User?.FindFirst("name")?.Value ?? string.Empty;

                var isSeedAdmin = seedAdminEmails.Any(e => e.Equals(email, StringComparison.OrdinalIgnoreCase));
                var role = isSeedAdmin ? AppUserRole.Admin : AppUserRole.Staff;

                if (isSeedAdmin)
                {
                    logger.LogWarning(
                        "Auto-promoting user {Email} to Admin via SeedAdminEmails (dev only)", email);
                }

                appUser = AppUser.Create(oid, email, name, role, organizationId: null);
                db.AppUsers.Add(appUser);
            }

            if (!appUser.IsActive)
            {
                cache.Set(cacheKey, (EnrichmentData?)null, CacheTtl);
                await db.SaveChangesAsync();
                return false;
            }

            appUser.RecordLogin();
            await db.SaveChangesAsync();

            var hasUniversalCourseAccess = appUser.Role == AppUserRole.Admin;

            var courseIds = appUser.Role == AppUserRole.Owner && appUser.OrganizationId.HasValue
                ? await db.Courses
                    .IgnoreQueryFilters()
                    .Where(c => c.OrganizationId == appUser.OrganizationId.Value)
                    .Select(c => c.Id)
                    .ToListAsync()
                : appUser.Role == AppUserRole.Admin ? [] : appUser.CourseAssignments.Select(a => a.CourseId).ToList();
            enrichmentData = new EnrichmentData(
                AppUserId: appUser.Id,
                OrganizationId: appUser.OrganizationId,
                Role: appUser.Role,
                Permissions: Permissions.GetForRole(appUser.Role),
                CourseIds: courseIds,
                HasUniversalCourseAccess: hasUniversalCourseAccess);

            cache.Set(cacheKey, enrichmentData, CacheTtl);
        }

        if (enrichmentData is null)
        {
            return false;
        }

        var claimsList = new List<Claim>
        {
            new("app_user_id", enrichmentData.AppUserId.ToString()),
        };

        if (enrichmentData.OrganizationId.HasValue)
        {
            claimsList.Add(new Claim("organization_id", enrichmentData.OrganizationId.Value.ToString()));
        }

        claimsList.Add(new Claim("role", enrichmentData.Role.ToString()));

        foreach (var permission in enrichmentData.Permissions)
        {
            claimsList.Add(new Claim("permission", permission));
        }

        if (enrichmentData.HasUniversalCourseAccess)
        {
            claimsList.Add(new Claim("course_access", "all"));
        }

        foreach (var courseId in enrichmentData.CourseIds)
        {
            claimsList.Add(new Claim("course_id", courseId.ToString()));
        }

        context.User!.AddIdentity(new ClaimsIdentity(claimsList));
        return true;
    }

    private sealed record EnrichmentData(
        Guid AppUserId,
        Guid? OrganizationId,
        AppUserRole Role,
        IReadOnlyList<string> Permissions,
        IReadOnlyList<Guid> CourseIds,
        bool HasUniversalCourseAccess);
}

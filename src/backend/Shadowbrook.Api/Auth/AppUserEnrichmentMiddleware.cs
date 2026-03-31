using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;

namespace Shadowbrook.Api.Auth;

public class AppUserEnrichmentMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly RequestDelegate next = next;

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, IMemoryCache cache, IConfiguration configuration)
    {
        var oid = context.User?.FindFirst("oid")?.Value
            ?? context.User?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (context.User?.Identity?.IsAuthenticated == true && oid is not null)
        {
            var seedAdminEmails = configuration.GetValue<string>("Auth:SeedAdminEmails")
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? [];
            await EnrichFromAppUserAsync(context, db, cache, oid, seedAdminEmails);
        }

        await this.next(context);
    }

    private static async Task EnrichFromAppUserAsync(
        HttpContext context, ApplicationDbContext db, IMemoryCache cache, string oid, string[] seedAdminEmails)
    {
        var cacheKey = $"appuser:{oid}";

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

                var role = seedAdminEmails.Any(e => e.Equals(email, StringComparison.OrdinalIgnoreCase))
                    ? AppUserRole.Admin
                    : AppUserRole.Staff;

                appUser = AppUser.Create(oid, email, name, role, organizationId: null);
                db.AppUsers.Add(appUser);
            }

            if (!appUser.IsActive)
            {
                await db.SaveChangesAsync();
                return;
            }

            appUser.RecordLogin();
            await db.SaveChangesAsync();

            enrichmentData = new EnrichmentData(
                AppUserId: appUser.Id,
                OrganizationId: appUser.OrganizationId,
                Role: appUser.Role,
                Permissions: Permissions.GetForRole(appUser.Role),
                CourseIds: appUser.CourseAssignments.Select(a => a.CourseId).ToList());

            cache.Set(cacheKey, enrichmentData, CacheTtl);
        }

        if (enrichmentData is null)
        {
            return;
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

        foreach (var courseId in enrichmentData.CourseIds)
        {
            claimsList.Add(new Claim("course_id", courseId.ToString()));
        }

        context.User!.AddIdentity(new ClaimsIdentity(claimsList));
    }

    private sealed record EnrichmentData(
        Guid AppUserId,
        Guid? OrganizationId,
        AppUserRole Role,
        IReadOnlyList<string> Permissions,
        IReadOnlyList<Guid> CourseIds);
}

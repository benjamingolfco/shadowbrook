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

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, IMemoryCache cache)
    {
        var oid = context.User?.FindFirst("oid")?.Value;

        if (context.User?.Identity?.IsAuthenticated == true && oid is not null)
        {
            await EnrichFromAppUserAsync(context, db, cache, oid);
        }
        else if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantIdHeader))
        {
            // Temporary bridge: read X-Tenant-Id header to preserve compatibility with
            // integration tests that set tenant context via header.
            var tenantIdString = tenantIdHeader.ToString();
            if (Guid.TryParse(tenantIdString, out var tenantId))
            {
                var claims = new[] { new Claim("organization_id", tenantId.ToString()) };
                var identity = new ClaimsIdentity(claims);
                context.User!.AddIdentity(identity);
            }
        }

        await this.next(context);
    }

    private static async Task EnrichFromAppUserAsync(
        HttpContext context, ApplicationDbContext db, IMemoryCache cache, string oid)
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
                    ?? string.Empty;
                var name = context.User?.FindFirst("name")?.Value ?? string.Empty;

                appUser = AppUser.Create(oid, email, name, AppUserRole.Staff, organizationId: null);
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

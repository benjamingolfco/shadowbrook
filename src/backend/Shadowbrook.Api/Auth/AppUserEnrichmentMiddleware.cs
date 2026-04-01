using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;

namespace Shadowbrook.Api.Auth;

public class AppUserEnrichmentMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public static string CacheKey(string oid) => $"appuser:{oid}";

    private readonly RequestDelegate next = next;

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, IMemoryCache cache, IOptions<AuthSettings> authOptions)
    {
        var oid = context.User?.FindFirst("oid")?.Value
            ?? context.User?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (context.User?.Identity?.IsAuthenticated == true && oid is not null)
        {
            var seedAdminEmails = authOptions.Value.GetSeedAdminEmailsList();
            await EnrichFromAppUserAsync(context, db, cache, oid, seedAdminEmails);
        }

        await this.next(context);
    }

    private static async Task EnrichFromAppUserAsync(
        HttpContext context, ApplicationDbContext db, IMemoryCache cache, string oid, string[] seedAdminEmails)
    {
        var cacheKey = CacheKey(oid);

        if (!cache.TryGetValue(cacheKey, out EnrichmentData? enrichmentData))
        {
            var appUser = await db.AppUsers
                .FirstOrDefaultAsync(u => u.IdentityId == oid);

            if (appUser is null)
            {
                var email = context.User?.FindFirst("emails")?.Value
                    ?? context.User?.FindFirst("email")?.Value
                    ?? context.User?.FindFirst("preferred_username")?.Value
                    ?? string.Empty;

                if (!seedAdminEmails.Any(e => e.Equals(email, StringComparison.OrdinalIgnoreCase)))
                {
                    // User not in system and not a seed admin — skip enrichment, auth pipeline will deny
                    return;
                }

                var name = context.User?.FindFirst("name")?.Value ?? string.Empty;
                appUser = AppUser.CreateAdmin(oid, email, name);
                db.AppUsers.Add(appUser);
            }

            if (appUser.IsActive)
            {
                appUser.RecordLogin();
            }

            // This SaveChangesAsync is intentionally outside the Wolverine pipeline.
            // The middleware must persist the AppUser (auto-provision or login timestamp)
            // before endpoint handlers run, so Wolverine's transactional middleware
            // cannot manage this save. This is safe because neither AppUser.CreateAdmin()
            // nor RecordLogin() raise domain events — no events will be silently lost.
            await db.SaveChangesAsync();

            var permissions = appUser.IsActive
                ? Permissions.GetForRole(appUser.Role)
                : [];

            enrichmentData = new EnrichmentData(
                AppUserId: appUser.Id,
                OrganizationId: appUser.OrganizationId,
                Role: appUser.Role,
                Permissions: permissions);

            cache.Set(cacheKey, enrichmentData, CacheTtl);
        }

        var claimsList = new List<Claim>
        {
            new("app_user_id", enrichmentData!.AppUserId.ToString()),
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

        context.User!.AddIdentity(new ClaimsIdentity(claimsList));
    }

    private sealed record EnrichmentData(
        Guid AppUserId,
        Guid? OrganizationId,
        AppUserRole Role,
        IReadOnlyList<string> Permissions);
}

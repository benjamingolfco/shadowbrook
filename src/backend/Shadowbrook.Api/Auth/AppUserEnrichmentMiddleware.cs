using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;

namespace Shadowbrook.Api.Auth;

public class AppUserEnrichmentMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly RequestDelegate next = next;

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, IMemoryCache cache, IOptions<AuthSettings> authOptions, ILogger<AppUserEnrichmentMiddleware> logger)
    {
        var oid = context.User?.FindFirst("oid")?.Value
            ?? context.User?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (context.User?.Identity?.IsAuthenticated == true && oid is not null)
        {
            var seedAdminEmails = authOptions.Value.GetSeedAdminEmailsList();
            var shouldProceed = await EnrichFromAppUserAsync(context, db, cache, oid, seedAdminEmails);

            if (!shouldProceed)
            {
                logger.LogWarning("Inactive user {Oid} denied access", oid);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await this.next(context);
    }

    private static async Task<bool> EnrichFromAppUserAsync(
        HttpContext context, ApplicationDbContext db, IMemoryCache cache, string oid, string[] seedAdminEmails)
    {
        var cacheKey = $"appuser:{oid}";

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
                var name = context.User?.FindFirst("name")?.Value ?? string.Empty;

                var role = seedAdminEmails.Any(e => e.Equals(email, StringComparison.OrdinalIgnoreCase))
                    ? AppUserRole.Admin
                    : AppUserRole.Operator;

                appUser = AppUser.Create(oid, email, name, role, organizationId: null);
                db.AppUsers.Add(appUser);
            }

            if (appUser.IsActive)
            {
                appUser.RecordLogin();
            }

            // This SaveChangesAsync is intentionally outside the Wolverine pipeline.
            // The middleware must persist the AppUser (auto-provision or login timestamp)
            // before endpoint handlers run, so Wolverine's transactional middleware
            // cannot manage this save. This is safe because neither AppUser.Create()
            // nor RecordLogin() raise domain events — no events will be silently lost.
            await db.SaveChangesAsync();

            if (!appUser.IsActive)
            {
                return false;
            }

            var permissions = Permissions.GetForRole(appUser.Role);

            enrichmentData = new EnrichmentData(
                AppUserId: appUser.Id,
                OrganizationId: appUser.OrganizationId,
                Role: appUser.Role,
                Permissions: permissions);

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

        context.User!.AddIdentity(new ClaimsIdentity(claimsList));
        return true;
    }

    private sealed record EnrichmentData(
        Guid AppUserId,
        Guid? OrganizationId,
        AppUserRole Role,
        IReadOnlyList<string> Permissions);
}

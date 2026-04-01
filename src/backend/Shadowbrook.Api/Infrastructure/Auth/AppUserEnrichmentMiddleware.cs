using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Shadowbrook.Api.Features.Auth.Handlers;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;
using Wolverine;

namespace Shadowbrook.Api.Infrastructure.Auth;

public class AppUserEnrichmentMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public static string CacheKey(string oid) => $"appuser:{oid}";

    private readonly RequestDelegate next = next;
    private readonly ILogger<AppUserEnrichmentMiddleware> logger = loggerFactory.CreateLogger<AppUserEnrichmentMiddleware>();

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, IMemoryCache cache, IMessageBus bus)
    {
        var oid = context.User?.FindFirst("oid")?.Value
            ?? context.User?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (context.User?.Identity?.IsAuthenticated == true && oid is not null)
        {
            var result = await EnrichFromAppUserAsync(context, db, cache, bus, this.logger, oid);
            if (!result)
            {
                return; // 403 already written
            }
        }

        await this.next(context);
    }

    private static async Task<bool> EnrichFromAppUserAsync(
        HttpContext context, ApplicationDbContext db, IMemoryCache cache, IMessageBus bus,
        ILogger<AppUserEnrichmentMiddleware> logger, string oid)
    {
        var cacheKey = CacheKey(oid);

        if (!cache.TryGetValue(cacheKey, out EnrichmentData? enrichmentData))
        {
            var appUser = await db.AppUsers
                .FirstOrDefaultAsync(u => u.IdentityId == oid);

            if (appUser is null)
            {
                var email = ExtractEmail(context);

                if (string.IsNullOrEmpty(email))
                {
                    logger.LogWarning("No AppUser for oid {Oid} and no email claim found", oid);
                    return await WriteNoAccountResponse(context);
                }

                appUser = await db.AppUsers
                    .FirstOrDefaultAsync(u => u.Email == email && u.IdentityId == null);

                if (appUser is null)
                {
                    logger.LogWarning("No AppUser for oid {Oid} or email {Email}", oid, email);
                    return await WriteNoAccountResponse(context);
                }

                var firstName = context.User?.FindFirst("given_name")?.Value ?? string.Empty;
                var lastName = context.User?.FindFirst("family_name")?.Value ?? string.Empty;

                await bus.InvokeAsync(new CompleteIdentitySetupCommand(appUser.Id, oid, firstName, lastName));

                await db.Entry(appUser).ReloadAsync();
            }

            if (appUser.IsActive)
            {
                appUser.RecordLogin();
            }

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
        return true;
    }

    private static string ExtractEmail(HttpContext context) =>
        context.User?.FindFirst("emails")?.Value
        ?? context.User?.FindFirst("email")?.Value
        ?? context.User?.FindFirst(ClaimTypes.Email)?.Value
        ?? context.User?.FindFirst("preferred_username")?.Value
        ?? string.Empty;

    private static async Task<bool> WriteNoAccountResponse(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { reason = "no_account" });
        return false;
    }

    private sealed record EnrichmentData(
        Guid AppUserId,
        Guid? OrganizationId,
        AppUserRole Role,
        IReadOnlyList<string> Permissions);
}

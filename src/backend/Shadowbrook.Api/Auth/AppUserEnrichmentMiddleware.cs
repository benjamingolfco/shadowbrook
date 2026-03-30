using System.Security.Claims;

namespace Shadowbrook.Api.Auth;

public class AppUserEnrichmentMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Stub — full implementation in Task 6.
        // Temporary bridge: read X-Tenant-Id header to preserve compatibility with
        // integration tests that set tenant context via header.
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantIdHeader))
        {
            var tenantIdString = tenantIdHeader.ToString();
            if (Guid.TryParse(tenantIdString, out var tenantId))
            {
                var claims = new[] { new Claim("organization_id", tenantId.ToString()) };
                var identity = new ClaimsIdentity(claims);
                context.User.AddIdentity(identity);
            }
        }

        await this.next(context);
    }
}

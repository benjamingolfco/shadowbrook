using System.Security.Claims;

namespace Shadowbrook.Api.Auth;

public class TenantClaimMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate next = next;

    public async Task InvokeAsync(HttpContext context)
    {
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

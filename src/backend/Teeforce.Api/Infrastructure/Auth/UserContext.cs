using System.Security.Claims;

namespace Teeforce.Api.Infrastructure.Auth;

public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private readonly IHttpContextAccessor httpContextAccessor = httpContextAccessor;
    private ClaimsPrincipal? User => this.httpContextAccessor.HttpContext?.User;

    public Guid? AppUserId
    {
        get
        {
            var claim = User?.FindFirst("app_user_id");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }
    }

    public string? IdentityId => User?.FindFirst("oid")?.Value
        ?? User?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

    public Guid? OrganizationId
    {
        get
        {
            // Operators always use their claim-based org ID
            var claim = User?.FindFirst("organization_id");
            if (claim is not null && Guid.TryParse(claim.Value, out var claimOrgId))
            {
                return claimOrgId;
            }

            // Admins can override via header for impersonation
            if (IsAdmin)
            {
                var header = this.httpContextAccessor.HttpContext?.Request.Headers["X-Organization-Id"].FirstOrDefault();
                if (header is not null && Guid.TryParse(header, out var headerOrgId))
                {
                    return headerOrgId;
                }
            }

            return null;
        }
    }

    private bool IsAdmin => User?.FindFirst("role")?.Value == "Admin";

    public IReadOnlyList<string> Permissions =>
        User?.FindAll("permission").Select(c => c.Value).ToList() ?? [];

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool HasPermission(string permission) => Permissions.Contains(permission);
}

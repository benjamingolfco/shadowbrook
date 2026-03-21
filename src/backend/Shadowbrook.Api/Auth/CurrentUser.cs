namespace Shadowbrook.Api.Auth;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private readonly IHttpContextAccessor httpContextAccessor = httpContextAccessor;

    public string? UserId => this.httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

    public Guid? TenantId
    {
        get
        {
            var claim = this.httpContextAccessor.HttpContext?.User.FindFirst("tenant_id");
            if (claim == null)
            {
                return null;
            }

            if (Guid.TryParse(claim.Value, out var tenantId))
            {
                return tenantId;
            }

            return null;
        }
    }

    public bool HasTenantId => TenantId.HasValue;
}

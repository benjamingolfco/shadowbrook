namespace Shadowbrook.Api.Auth;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst("tenant_id");
            if (claim == null)
                return null;

            if (Guid.TryParse(claim.Value, out var tenantId))
                return tenantId;

            return null;
        }
    }

    public bool HasTenantId => TenantId.HasValue;
}

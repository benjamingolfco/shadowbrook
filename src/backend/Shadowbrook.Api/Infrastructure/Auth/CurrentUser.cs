using System.Security.Claims;

namespace Shadowbrook.Api.Infrastructure.Auth;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
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
            var claim = User?.FindFirst("organization_id");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }
    }

    public IReadOnlyList<string> Permissions =>
        User?.FindAll("permission").Select(c => c.Value).ToList() ?? [];

    public IReadOnlyList<Guid> CourseIds =>
        User?.FindAll("course_id")
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList() ?? [];

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool HasUniversalCourseAccess =>
        User?.FindAll("course_access").Any(c => c.Value == "all") ?? false;

    public bool CanAccessCourse(Guid courseId) =>
        HasUniversalCourseAccess || CourseIds.Contains(courseId);
}

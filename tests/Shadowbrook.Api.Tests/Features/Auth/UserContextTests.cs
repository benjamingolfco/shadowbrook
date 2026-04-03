using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shadowbrook.Api.Infrastructure.Auth;

namespace Shadowbrook.Api.Tests.Features.Auth;

public class UserContextTests
{
    private static UserContext CreateContext(
        ClaimsPrincipal? user = null,
        string? orgHeader = null)
    {
        var httpContext = new DefaultHttpContext();
        if (user is not null)
        {
            httpContext.User = user;
        }

        if (orgHeader is not null)
        {
            httpContext.Request.Headers["X-Organization-Id"] = orgHeader;
        }

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return new UserContext(accessor);
    }

    private static ClaimsPrincipal AdminUser(Guid appUserId)
    {
        var claims = new List<Claim>
        {
            new("app_user_id", appUserId.ToString()),
            new("role", "Admin"),
            new("permission", "app:access"),
            new("permission", "users:manage"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static ClaimsPrincipal OperatorUser(Guid appUserId, Guid orgId)
    {
        var claims = new List<Claim>
        {
            new("app_user_id", appUserId.ToString()),
            new("organization_id", orgId.ToString()),
            new("role", "Operator"),
            new("permission", "app:access"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void OrganizationId_AdminWithHeader_ReturnsHeaderValue()
    {
        var targetOrgId = Guid.CreateVersion7();
        var context = CreateContext(
            user: AdminUser(Guid.CreateVersion7()),
            orgHeader: targetOrgId.ToString());

        Assert.Equal(targetOrgId, context.OrganizationId);
    }

    [Fact]
    public void OrganizationId_AdminWithoutHeader_ReturnsNull()
    {
        var context = CreateContext(user: AdminUser(Guid.CreateVersion7()));

        Assert.Null(context.OrganizationId);
    }

    [Fact]
    public void OrganizationId_OperatorWithHeader_IgnoresHeader()
    {
        var realOrgId = Guid.CreateVersion7();
        var fakeOrgId = Guid.CreateVersion7();
        var context = CreateContext(
            user: OperatorUser(Guid.CreateVersion7(), realOrgId),
            orgHeader: fakeOrgId.ToString());

        Assert.Equal(realOrgId, context.OrganizationId);
    }

    [Fact]
    public void OrganizationId_AdminWithInvalidHeader_ReturnsNull()
    {
        var context = CreateContext(
            user: AdminUser(Guid.CreateVersion7()),
            orgHeader: "not-a-guid");

        Assert.Null(context.OrganizationId);
    }

    [Fact]
    public void OrganizationId_OperatorWithClaim_ReturnsClaim()
    {
        var orgId = Guid.CreateVersion7();
        var context = CreateContext(user: OperatorUser(Guid.CreateVersion7(), orgId));

        Assert.Equal(orgId, context.OrganizationId);
    }
}

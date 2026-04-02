using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shadowbrook.Api.Infrastructure.Auth;

namespace Shadowbrook.Api.Tests.Auth;

public class AppUserAuthorizationResultHandlerTests
{
    private static DefaultHttpContext CreateAuthenticatedContext(bool hasAppUserClaim = false)
    {
        var claims = new List<Claim>();
        if (hasAppUserClaim)
        {
            claims.Add(new Claim("app_user_id", Guid.NewGuid().ToString()));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        var principal = new ClaimsPrincipal(identity);
        return new DefaultHttpContext
        {
            User = principal,
            Response = { Body = new MemoryStream() }
        };
    }

    private static PolicyAuthorizationResult BuildForbiddenResult(
        IEnumerable<IAuthorizationRequirement>? failedRequirements = null)
    {
        var failure = AuthorizationFailure.Failed(failedRequirements ?? [new RequireAppUserRequirement()]);
        return PolicyAuthorizationResult.Forbid(failure);
    }

    [Fact]
    public async Task FailedAppUserRequirement_AuthenticatedUser_Returns403WithNoAccountReason()
    {
        var defaultHandler = Substitute.For<IAuthorizationMiddlewareResultHandler>();
        var handler = new AppUserAuthorizationResultHandler(defaultHandler);
        var context = CreateAuthenticatedContext(hasAppUserClaim: false);
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        var authorizeResult = BuildForbiddenResult([new RequireAppUserRequirement()]);

        await handler.HandleAsync(_ => Task.CompletedTask, context, policy, authorizeResult);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("no_account", body);

        await defaultHandler.DidNotReceive().HandleAsync(
            Arg.Any<RequestDelegate>(),
            Arg.Any<HttpContext>(),
            Arg.Any<AuthorizationPolicy>(),
            Arg.Any<PolicyAuthorizationResult>());
    }

    [Fact]
    public async Task FailedOtherRequirement_DelegatesToDefaultHandler()
    {
        var defaultHandler = Substitute.For<IAuthorizationMiddlewareResultHandler>();
        var handler = new AppUserAuthorizationResultHandler(defaultHandler);
        var context = CreateAuthenticatedContext(hasAppUserClaim: false);
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        // A different requirement type failing — not RequireAppUserRequirement
        var otherRequirement = new PermissionRequirement(Permissions.UsersManage);
        var failure = AuthorizationFailure.Failed([otherRequirement]);
        var authorizeResult = PolicyAuthorizationResult.Forbid(failure);

        await handler.HandleAsync(_ => Task.CompletedTask, context, policy, authorizeResult);

        await defaultHandler.Received(1).HandleAsync(
            Arg.Any<RequestDelegate>(),
            Arg.Any<HttpContext>(),
            Arg.Any<AuthorizationPolicy>(),
            Arg.Any<PolicyAuthorizationResult>());
    }
}

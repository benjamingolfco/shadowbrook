using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeOfferAggregate;

namespace Teeforce.Api.Tests.Auth;

public class TeeTimeOfferTokenAuthorizationHandlerTests
{
    private static readonly AppAccessOrOfferTokenRequirement Requirement = new();
    private static readonly ClaimsPrincipal AnonymousUser = new(new ClaimsIdentity());

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal user) =>
        new([Requirement], user, resource: null);

    private static TeeTimeOffer CreatePendingOffer(ITimeProvider timeProvider) =>
        TeeTimeOffer.Create(
            teeTimeId: Guid.NewGuid(),
            golferWaitlistEntryId: Guid.NewGuid(),
            golferId: Guid.NewGuid(),
            groupSize: 2,
            courseId: Guid.NewGuid(),
            date: new DateOnly(2026, 6, 1),
            time: new TimeOnly(9, 0),
            timeProvider: timeProvider);

    [Fact]
    public async Task ValidPendingToken_Succeeds()
    {
        var timeProvider = Substitute.For<ITimeProvider>();
        timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);

        var offer = CreatePendingOffer(timeProvider);

        var repo = Substitute.For<ITeeTimeOfferRepository>();
        repo.GetByTokenAsync(offer.Token, Arg.Any<CancellationToken>()).Returns(offer);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString($"?token={offer.Token}");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var handler = new TeeTimeOfferTokenAuthorizationHandler(accessor, repo);
        var context = CreateContext(AnonymousUser);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Same(offer, httpContext.Items[TeeTimeOfferTokenAuthorizationHandler.OfferItemKey]);
    }

    [Fact]
    public async Task MissingToken_DoesNotSucceed()
    {
        var repo = Substitute.For<ITeeTimeOfferRepository>();
        var httpContext = new DefaultHttpContext();
        // No query string

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var handler = new TeeTimeOfferTokenAuthorizationHandler(accessor, repo);
        var context = CreateContext(AnonymousUser);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task InvalidTokenFormat_DoesNotSucceed()
    {
        var repo = Substitute.For<ITeeTimeOfferRepository>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?token=not-a-guid");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var handler = new TeeTimeOfferTokenAuthorizationHandler(accessor, repo);
        var context = CreateContext(AnonymousUser);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task TokenNotFoundInRepo_DoesNotSucceed()
    {
        var tokenGuid = Guid.NewGuid();
        var repo = Substitute.For<ITeeTimeOfferRepository>();
        repo.GetByTokenAsync(tokenGuid, Arg.Any<CancellationToken>()).Returns((TeeTimeOffer?)null);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString($"?token={tokenGuid}");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var handler = new TeeTimeOfferTokenAuthorizationHandler(accessor, repo);
        var context = CreateContext(AnonymousUser);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task ExpiredOffer_DoesNotSucceed()
    {
        var timeProvider = Substitute.For<ITimeProvider>();
        timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);

        var offer = CreatePendingOffer(timeProvider);
        offer.Expire();

        var repo = Substitute.For<ITeeTimeOfferRepository>();
        repo.GetByTokenAsync(offer.Token, Arg.Any<CancellationToken>()).Returns(offer);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString($"?token={offer.Token}");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var handler = new TeeTimeOfferTokenAuthorizationHandler(accessor, repo);
        var context = CreateContext(AnonymousUser);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task RejectedOffer_DoesNotSucceed()
    {
        var timeProvider = Substitute.For<ITimeProvider>();
        timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);

        var offer = CreatePendingOffer(timeProvider);
        offer.Reject("test");

        var repo = Substitute.For<ITeeTimeOfferRepository>();
        repo.GetByTokenAsync(offer.Token, Arg.Any<CancellationToken>()).Returns(offer);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString($"?token={offer.Token}");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var handler = new TeeTimeOfferTokenAuthorizationHandler(accessor, repo);
        var context = CreateContext(AnonymousUser);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task NullHttpContext_DoesNotSucceed()
    {
        var repo = Substitute.For<ITeeTimeOfferRepository>();
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var handler = new TeeTimeOfferTokenAuthorizationHandler(accessor, repo);
        var context = CreateContext(AnonymousUser);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}

public class AppAccessOrOfferTokenPermissionHandlerTests
{
    private static readonly AppAccessOrOfferTokenRequirement Requirement = new();

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal user) =>
        new([Requirement], user, resource: null);

    [Fact]
    public async Task AuthenticatedWithAppAccess_Succeeds()
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.IsAuthenticated.Returns(true);
        userContext.HasPermission(Permissions.AppAccess).Returns(true);

        var handler = new AppAccessOrOfferTokenPermissionHandler(userContext);
        var user = new ClaimsPrincipal(new ClaimsIdentity([], authenticationType: "test"));
        var context = CreateContext(user);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task AuthenticatedWithoutAppAccess_DoesNotSucceed()
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.IsAuthenticated.Returns(true);
        userContext.HasPermission(Permissions.AppAccess).Returns(false);

        var handler = new AppAccessOrOfferTokenPermissionHandler(userContext);
        var user = new ClaimsPrincipal(new ClaimsIdentity([], authenticationType: "test"));
        var context = CreateContext(user);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task NotAuthenticated_DoesNotSucceed()
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.IsAuthenticated.Returns(false);

        var handler = new AppAccessOrOfferTokenPermissionHandler(userContext);
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = CreateContext(user);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}

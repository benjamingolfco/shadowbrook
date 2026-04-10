using Microsoft.AspNetCore.Authorization;
using Teeforce.Domain.TeeTimeOfferAggregate;

namespace Teeforce.Api.Infrastructure.Auth;

/// <summary>
/// Requirement that is satisfied if the user has AppAccess permission OR a valid offer token.
/// </summary>
public class AppAccessOrOfferTokenRequirement : IAuthorizationRequirement;

/// <summary>
/// Handler that checks for a valid TeeTimeOffer token in the query string.
/// If found, stores the offer on HttpContext.Items for the endpoint to consume.
/// </summary>
public class TeeTimeOfferTokenAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ITeeTimeOfferRepository offerRepository) : AuthorizationHandler<AppAccessOrOfferTokenRequirement>
{
    public const string OfferItemKey = "TeeTimeOffer";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AppAccessOrOfferTokenRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        var tokenString = httpContext.Request.Query["token"].FirstOrDefault();
        if (string.IsNullOrEmpty(tokenString) || !Guid.TryParse(tokenString, out var token))
        {
            return;
        }

        var offer = await offerRepository.GetByTokenAsync(token, httpContext.RequestAborted);
        if (offer is null || offer.Status != TeeTimeOfferStatus.Pending)
        {
            return;
        }

        // Store the offer on HttpContext.Items so the endpoint can read it
        httpContext.Items[OfferItemKey] = offer;
        context.Succeed(requirement);
    }
}

/// <summary>
/// Handler that satisfies the requirement if the user already has AppAccess permission.
/// </summary>
public class AppAccessOrOfferTokenPermissionHandler(IUserContext userContext) : AuthorizationHandler<AppAccessOrOfferTokenRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AppAccessOrOfferTokenRequirement requirement)
    {
        if (userContext.IsAuthenticated && userContext.HasPermission(Permissions.AppAccess))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

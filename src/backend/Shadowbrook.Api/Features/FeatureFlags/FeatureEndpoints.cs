using Microsoft.AspNetCore.Mvc;
using Shadowbrook.Api.Infrastructure.Services;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.FeatureFlags;

public static class FeatureEndpoints
{
    [WolverineGet("/api/features")]
    public static IResult GetFeatures([NotBody] IFeatureService featureService) => Results.Ok(featureService.GetAll());
}

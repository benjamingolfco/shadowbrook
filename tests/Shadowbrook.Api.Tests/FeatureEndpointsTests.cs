using Microsoft.Extensions.Configuration;
using Shadowbrook.Api.Features.FeatureFlags;
using Shadowbrook.Api.Infrastructure.Services;

namespace Shadowbrook.Api.Tests;

public class FeatureEndpointsTests
{
    [Fact]
    public void GetFeatures_ReflectsConfigOverrides()
    {
        // Unit-level test: call endpoint directly with mocked service
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureFlags:sms-notifications"] = "false"
            })
            .Build();
        var service = new FeatureService(config);

        var result = FeatureEndpoints.GetFeatures(service);

        var okResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<Dictionary<string, bool>>>(result);
        Assert.False(okResult.Value!["sms-notifications"]);
        Assert.True(okResult.Value!["dynamic-pricing"]);
    }
}

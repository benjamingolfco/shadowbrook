using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Shadowbrook.Api.Features.FeatureFlags;
using Shadowbrook.Api.Infrastructure.Services;

namespace Shadowbrook.Api.Tests;

public class FeatureEndpointsTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetFeatures_ReturnsOk_WithAllKnownKeys()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/features");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
        Assert.NotNull(result);
        Assert.Contains("sms-notifications", result.Keys);
        Assert.Contains("dynamic-pricing", result.Keys);
        Assert.True(result["sms-notifications"]);
        Assert.True(result["dynamic-pricing"]);
    }

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

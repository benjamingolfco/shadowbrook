using System.Net.Http.Json;

namespace Shadowbrook.Api.IntegrationTests;

[Collection("Integration")]
[IntegrationTest]
public class FeatureEndpointsTests(TestWebApplicationFactory factory) : IAsyncLifetime
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
}

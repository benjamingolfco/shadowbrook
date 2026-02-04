namespace Shadowbrook.Api.Tests;

public class SmokeTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SmokeTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }
}

namespace Shadowbrook.Api.Tests;

public class SmokeTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await this.client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }
}

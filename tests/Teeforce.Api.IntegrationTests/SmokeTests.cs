namespace Teeforce.Api.IntegrationTests;

[Collection("Integration")]
[IntegrationTest]
public class SmokeTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await this.client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }
}

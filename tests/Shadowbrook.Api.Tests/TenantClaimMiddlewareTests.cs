using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;

[Collection("Integration")]
public class TenantClaimMiddlewareTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task WithValidTenantIdHeader_ReturnsTenantId()
    {
        var expectedTenantId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Get, "/debug/current-user");
        request.Headers.Add("X-Tenant-Id", expectedTenantId.ToString());

        var response = await this.client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Equal(expectedTenantId, body!.TenantId);
    }

    [Fact]
    public async Task WithoutTenantIdHeader_ReturnsNullTenantId()
    {
        var response = await this.client.GetAsync("/debug/current-user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.TenantId);
    }

    [Fact]
    public async Task WithInvalidGuidHeader_ReturnsNullTenantIdAnd200Status()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/debug/current-user");
        request.Headers.Add("X-Tenant-Id", "not-a-valid-guid");

        var response = await this.client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.TenantId);
    }

    private record CurrentUserResponse(Guid? TenantId);
}

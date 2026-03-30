using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;

[Collection("Integration")]
[IntegrationTest]
public class TenantClaimMiddlewareTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task WithValidTenantIdHeader_ReturnsOrganizationId()
    {
        var expectedOrganizationId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Get, "/debug/current-user");
        request.Headers.Add("X-Tenant-Id", expectedOrganizationId.ToString());

        var response = await this.client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Equal(expectedOrganizationId, body!.OrganizationId);
    }

    [Fact]
    public async Task WithoutTenantIdHeader_ReturnsNullOrganizationId()
    {
        var response = await this.client.GetAsync("/debug/current-user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.OrganizationId);
    }

    [Fact]
    public async Task WithInvalidGuidHeader_ReturnsNullOrganizationIdAnd200Status()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/debug/current-user");
        request.Headers.Add("X-Tenant-Id", "not-a-valid-guid");

        var response = await this.client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.OrganizationId);
    }

    private record CurrentUserResponse(Guid? OrganizationId);
}

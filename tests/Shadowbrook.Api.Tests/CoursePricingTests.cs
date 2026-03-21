using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;

[Collection("Integration")]
[IntegrationTest]
public class CoursePricingTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> CreateCourse()
    {
        var tenantId = await CreateTestTenantAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Content = JsonContent.Create(new { Name = "Test Course" });
        var response = await this.client.SendAsync(request);
        var course = await response.Content.ReadFromJsonAsync<CourseResponse>();
        return course!.Id;
    }

    private async Task<Guid> CreateTestTenantAsync()
    {
        var response = await this.client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = $"Test Tenant {Guid.NewGuid()}",
            ContactName = "Test Contact",
            ContactEmail = "test@tenant.com",
            ContactPhone = "555-0000"
        });

        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>();
        return tenant!.Id;
    }

    // AC 1-2: Setting and updating pricing
    [Fact]
    public async Task UpdatePricing_InitialSet_ReturnsOkAndPersists()
    {
        var courseId = await CreateCourse();

        var response = await this.client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
        {
            FlatRatePrice = 45.00m
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PricingResponse>();
        Assert.NotNull(body);
        Assert.Equal(45.00m, body!.FlatRatePrice);
    }

    // AC 3: Updates apply immediately
    [Fact]
    public async Task UpdatePricing_UpdateExisting_ReturnsNewPrice()
    {
        var courseId = await CreateCourse();

        await this.client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
        {
            FlatRatePrice = 45.00m
        });

        var updateResponse = await this.client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
        {
            FlatRatePrice = 50.00m
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var body = await updateResponse.Content.ReadFromJsonAsync<PricingResponse>();
        Assert.Equal(50.00m, body!.FlatRatePrice);

        // Verify GET reflects the update
        var getResponse = await this.client.GetAsync($"/courses/{courseId}/pricing");
        var getBody = await getResponse.Content.ReadFromJsonAsync<PricingResponse>();
        Assert.Equal(50.00m, getBody!.FlatRatePrice);
    }

    // AC 5: Validation - zero is valid
    [Fact]
    public async Task UpdatePricing_ZeroPrice_ReturnsOk()
    {
        var courseId = await CreateCourse();

        var response = await this.client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
        {
            FlatRatePrice = 0.00m
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // AC 5: Validation - max valid price
    [Fact]
    public async Task UpdatePricing_MaxValidPrice_ReturnsOk()
    {
        var courseId = await CreateCourse();

        var response = await this.client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
        {
            FlatRatePrice = 10000.00m
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // AC 6: Viewing current pricing
    [Fact]
    public async Task GetPricing_AfterUpdate_ReturnsCurrentPrice()
    {
        var courseId = await CreateCourse();

        await this.client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
        {
            FlatRatePrice = 75.50m
        });

        var response = await this.client.GetAsync($"/courses/{courseId}/pricing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PricingResponse>();
        Assert.Equal(75.50m, body!.FlatRatePrice);
    }

    // AC 6: Viewing when not configured
    [Fact]
    public async Task GetPricing_NotConfigured_ReturnsEmptyObject()
    {
        var courseId = await CreateCourse();

        var response = await this.client.GetAsync($"/courses/{courseId}/pricing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Error case: course not found
    [Fact]
    public async Task UpdatePricing_CourseNotFound_ReturnsNotFound()
    {
        var response = await this.client.PutAsJsonAsync($"/courses/{Guid.NewGuid()}/pricing", new
        {
            FlatRatePrice = 45.00m
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPricing_CourseNotFound_ReturnsNotFound()
    {
        var response = await this.client.GetAsync($"/courses/{Guid.NewGuid()}/pricing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record CourseResponse(Guid Id, string Name);
    private record TenantResponse(Guid Id);
    private record PricingResponse(decimal FlatRatePrice);
}

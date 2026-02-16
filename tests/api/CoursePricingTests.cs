using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;

public class CoursePricingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CoursePricingTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateCourse()
    {
        var tenantId = await CreateTestTenantAsync();
        var response = await _client.PostAsJsonAsync("/courses", new { TenantId = tenantId, Name = "Test Course" });
        var course = await response.Content.ReadFromJsonAsync<CourseResponse>();
        return course!.Id;
    }

    private async Task<Guid> CreateTestTenantAsync()
    {
        var response = await _client.PostAsJsonAsync("/tenants", new
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

        var response = await _client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
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

        await _client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
        {
            FlatRatePrice = 45.00m
        });

        var updateResponse = await _client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
        {
            FlatRatePrice = 50.00m
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var body = await updateResponse.Content.ReadFromJsonAsync<PricingResponse>();
        Assert.Equal(50.00m, body!.FlatRatePrice);

        // Verify GET reflects the update
        var getResponse = await _client.GetAsync($"/courses/{courseId}/pricing");
        var getBody = await getResponse.Content.ReadFromJsonAsync<PricingResponse>();
        Assert.Equal(50.00m, getBody!.FlatRatePrice);
    }

    // AC 4: Validation - negative price
    [Fact]
    public async Task UpdatePricing_NegativePrice_ReturnsBadRequest()
    {
        var courseId = await CreateCourse();

        var response = await _client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
        {
            FlatRatePrice = -10.00m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("greater than or equal to 0", body!.Error);
    }

    // AC 4: Validation - excessively large price
    [Fact]
    public async Task UpdatePricing_ExcessivelyLargePrice_ReturnsBadRequest()
    {
        var courseId = await CreateCourse();

        var response = await _client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
        {
            FlatRatePrice = 10001.00m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("less than or equal to 10000", body!.Error);
    }

    // AC 5: Validation - zero is valid
    [Fact]
    public async Task UpdatePricing_ZeroPrice_ReturnsOk()
    {
        var courseId = await CreateCourse();

        var response = await _client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
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

        var response = await _client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
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

        await _client.PutAsJsonAsync($"/courses/{courseId}/pricing", new
        {
            FlatRatePrice = 75.50m
        });

        var response = await _client.GetAsync($"/courses/{courseId}/pricing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PricingResponse>();
        Assert.Equal(75.50m, body!.FlatRatePrice);
    }

    // AC 6: Viewing when not configured
    [Fact]
    public async Task GetPricing_NotConfigured_ReturnsEmptyObject()
    {
        var courseId = await CreateCourse();

        var response = await _client.GetAsync($"/courses/{courseId}/pricing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Error case: course not found
    [Fact]
    public async Task UpdatePricing_CourseNotFound_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync($"/courses/{Guid.NewGuid()}/pricing", new
        {
            FlatRatePrice = 45.00m
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPricing_CourseNotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/courses/{Guid.NewGuid()}/pricing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record CourseResponse(Guid Id, string Name);
    private record TenantResponse(Guid Id);
    private record PricingResponse(decimal FlatRatePrice);
    private record ErrorResponse(string Error);
}

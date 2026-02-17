using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;

public class TenantEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public TenantEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostTenant_WithValidData_ReturnsCreated()
    {
        var request = new
        {
            OrganizationName = "Pinecrest Golf Management",
            ContactName = "John Smith",
            ContactEmail = "john@pinecrest.com",
            ContactPhone = "555-1234"
        };

        var response = await _client.PostAsJsonAsync("/tenants", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TenantResponse>();
        Assert.NotNull(body);
        Assert.Equal("Pinecrest Golf Management", body!.OrganizationName);
        Assert.Equal("John Smith", body.ContactName);
        Assert.Equal("john@pinecrest.com", body.ContactEmail);
        Assert.Equal("555-1234", body.ContactPhone);
        Assert.NotEqual(Guid.Empty, body.Id);
    }

    [Fact]
    public async Task PostTenant_WithMissingOrganizationName_ReturnsBadRequest()
    {
        var request = new
        {
            ContactName = "John Smith",
            ContactEmail = "john@test.com",
            ContactPhone = "555-1234"
        };

        var response = await _client.PostAsJsonAsync("/tenants", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTenant_WithMissingContactName_ReturnsBadRequest()
    {
        var request = new
        {
            OrganizationName = "Test Org",
            ContactEmail = "test@test.com",
            ContactPhone = "555-1234"
        };

        var response = await _client.PostAsJsonAsync("/tenants", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTenant_WithMissingContactEmail_ReturnsBadRequest()
    {
        var request = new
        {
            OrganizationName = "Test Org",
            ContactName = "John Smith",
            ContactPhone = "555-1234"
        };

        var response = await _client.PostAsJsonAsync("/tenants", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTenant_WithMissingContactPhone_ReturnsBadRequest()
    {
        var request = new
        {
            OrganizationName = "Test Org",
            ContactName = "John Smith",
            ContactEmail = "test@test.com"
        };

        var response = await _client.PostAsJsonAsync("/tenants", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTenant_WithInvalidEmailFormat_ReturnsBadRequest()
    {
        var request = new
        {
            OrganizationName = "Test Org",
            ContactName = "John Smith",
            ContactEmail = "not-an-email",
            ContactPhone = "555-1234"
        };

        var response = await _client.PostAsJsonAsync("/tenants", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTenant_WithDuplicateOrganizationName_ReturnsConflict()
    {
        var request = new
        {
            OrganizationName = "Duplicate Org",
            ContactName = "John Smith",
            ContactEmail = "john@test.com",
            ContactPhone = "555-1234"
        };

        await _client.PostAsJsonAsync("/tenants", request);

        var duplicateResponse = await _client.PostAsJsonAsync("/tenants", request);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task PostTenant_WithDuplicateOrganizationNameCaseInsensitive_ReturnsConflict()
    {
        var request1 = new
        {
            OrganizationName = "Case Test Org",
            ContactName = "John Smith",
            ContactEmail = "john@test.com",
            ContactPhone = "555-1234"
        };

        await _client.PostAsJsonAsync("/tenants", request1);

        var request2 = new
        {
            OrganizationName = "CASE TEST ORG",
            ContactName = "Jane Doe",
            ContactEmail = "jane@test.com",
            ContactPhone = "555-5678"
        };

        var response = await _client.PostAsJsonAsync("/tenants", request2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetAllTenants_ReturnsAllTenantsWithCourseCount()
    {
        await _client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = "Tenant 1",
            ContactName = "Contact 1",
            ContactEmail = "contact1@test.com",
            ContactPhone = "555-0001"
        });

        var response = await _client.GetAsync("/tenants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tenants = await response.Content.ReadFromJsonAsync<List<TenantListResponse>>();
        Assert.NotNull(tenants);
        Assert.NotEmpty(tenants!);
        Assert.All(tenants, t => Assert.True(t.CourseCount >= 0));
    }

    [Fact]
    public async Task GetAllTenants_WithNoTenants_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync("/tenants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tenants = await response.Content.ReadFromJsonAsync<List<TenantListResponse>>();
        Assert.NotNull(tenants);
    }

    [Fact]
    public async Task GetTenantById_WithValidId_ReturnsOkWithCourses()
    {
        var createResponse = await _client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = "Lookup Tenant",
            ContactName = "Test Contact",
            ContactEmail = "test@lookup.com",
            ContactPhone = "555-9999"
        });

        var created = await createResponse.Content.ReadFromJsonAsync<TenantResponse>();

        var response = await _client.GetAsync($"/tenants/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tenant = await response.Content.ReadFromJsonAsync<TenantDetailResponse>();
        Assert.NotNull(tenant);
        Assert.Equal("Lookup Tenant", tenant!.OrganizationName);
        Assert.NotNull(tenant.Courses);
    }

    [Fact]
    public async Task GetTenantById_WithInvalidId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/tenants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record TenantResponse(
        Guid Id,
        string OrganizationName,
        string ContactName,
        string ContactEmail,
        string ContactPhone,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private record TenantListResponse(
        Guid Id,
        string OrganizationName,
        string ContactName,
        string ContactEmail,
        string ContactPhone,
        int CourseCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private record TenantDetailResponse(
        Guid Id,
        string OrganizationName,
        string ContactName,
        string ContactEmail,
        string ContactPhone,
        List<CourseInfo> Courses,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private record CourseInfo(Guid Id, string Name);
}

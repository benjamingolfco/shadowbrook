using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.IntegrationTests;

[Collection("Integration")]
[IntegrationTest]
public class TenantEndpointsTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = factory.CreateClient();
    private readonly TestWebApplicationFactory factory = factory;

    public Task InitializeAsync() => this.factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

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

        var response = await this.client.PostAsJsonAsync("/tenants", request);

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
    public async Task PostTenant_WithDuplicateOrganizationName_ReturnsConflict()
    {
        var request = new
        {
            OrganizationName = "Duplicate Org",
            ContactName = "John Smith",
            ContactEmail = "john@test.com",
            ContactPhone = "555-1234"
        };

        await this.client.PostAsJsonAsync("/tenants", request);

        var duplicateResponse = await this.client.PostAsJsonAsync("/tenants", request);

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

        await this.client.PostAsJsonAsync("/tenants", request1);

        var request2 = new
        {
            OrganizationName = "CASE TEST ORG",
            ContactName = "Jane Doe",
            ContactEmail = "jane@test.com",
            ContactPhone = "555-5678"
        };

        var response = await this.client.PostAsJsonAsync("/tenants", request2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetAllTenants_ReturnsAllTenantsWithCourseCount()
    {
        await this.client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = "Tenant 1",
            ContactName = "Contact 1",
            ContactEmail = "contact1@test.com",
            ContactPhone = "555-0001"
        });

        var response = await this.client.GetAsync("/tenants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tenants = await response.Content.ReadFromJsonAsync<List<TenantListResponse>>();
        Assert.NotNull(tenants);
        Assert.NotEmpty(tenants!);
        Assert.All(tenants, t => Assert.True(t.CourseCount >= 0));
    }

    [Fact]
    public async Task GetAllTenants_WithNoTenants_ReturnsEmptyArray()
    {
        var response = await this.client.GetAsync("/tenants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tenants = await response.Content.ReadFromJsonAsync<List<TenantListResponse>>();
        Assert.NotNull(tenants);
    }

    [Fact]
    public async Task GetTenantById_WithValidId_ReturnsOkWithCourses()
    {
        var createResponse = await this.client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = "Lookup Tenant",
            ContactName = "Test Contact",
            ContactEmail = "test@lookup.com",
            ContactPhone = "555-9999"
        });

        var created = await createResponse.Content.ReadFromJsonAsync<TenantResponse>();

        var response = await this.client.GetAsync($"/tenants/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tenant = await response.Content.ReadFromJsonAsync<TenantDetailResponse>();
        Assert.NotNull(tenant);
        Assert.Equal("Lookup Tenant", tenant!.OrganizationName);
        Assert.NotNull(tenant.Courses);
    }

    [Fact]
    public async Task GetTenantById_WithInvalidId_ReturnsNotFound()
    {
        var response = await this.client.GetAsync($"/tenants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTenantById_WithCourses_ReturnsCourseLocationData()
    {
        // Create tenant
        var createTenantResponse = await this.client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = "Location Test Tenant " + Guid.NewGuid(),
            ContactName = "Test Contact",
            ContactEmail = "test@location.com",
            ContactPhone = "555-0000"
        });
        var tenant = await createTenantResponse.Content.ReadFromJsonAsync<TenantResponse>();

        // Create course with location data
        await this.client.PostAsJsonAsync("/courses", new
        {
            Name = "Location Course",
            TenantId = tenant!.Id,
            City = "Scottsdale",
            State = "AZ",
            TimeZoneId = TestTimeZones.Phoenix
        });

        // Fetch tenant detail
        var response = await this.client.GetAsync($"/tenants/{tenant.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<TenantDetailResponse>();
        Assert.NotNull(detail);
        Assert.Single(detail!.Courses);
        Assert.Equal("Location Course", detail.Courses[0].Name);
        Assert.Equal("Scottsdale", detail.Courses[0].City);
        Assert.Equal("AZ", detail.Courses[0].State);
    }
}

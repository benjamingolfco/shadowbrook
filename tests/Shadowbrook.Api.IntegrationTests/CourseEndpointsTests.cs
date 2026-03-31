using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.IntegrationTests;

[Collection("Integration")]
[IntegrationTest]
public class CourseEndpointsTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly TestWebApplicationFactory factory = factory;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await this.factory.ResetDatabaseAsync();
        await this.factory.SeedTestAdminAsync();
        this.client = this.factory.CreateAuthenticatedClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetAllCourses_ReturnsOk()
    {
        var (tenantId, tenantName) = await CreateTestTenantAsync();
        var response = await this.client.PostAsJsonAsync("/courses", new { Name = "Test Course", OrganizationId = tenantId, TimeZoneId = TestTimeZones.Chicago });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var getResponse = await this.client.GetAsync("/courses");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var courses = await getResponse.Content.ReadFromJsonAsync<List<CourseResponse>>();
        Assert.NotNull(courses);
        Assert.NotEmpty(courses!);
    }

    [Fact]
    public async Task GetAllCourses_IncludesTenantInfo()
    {
        var (tenantId, tenantName) = await CreateTestTenantAsync();
        var createResponse = await this.client.PostAsJsonAsync("/courses", new { Name = "Test Course for Tenant Info", OrganizationId = tenantId, TimeZoneId = TestTimeZones.Chicago });
        var created = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        var getResponse = await this.client.GetAsync("/courses");
        var courses = await getResponse.Content.ReadFromJsonAsync<List<CourseResponse>>();

        Assert.NotNull(courses);
        var course = courses!.First(c => c.Id == created!.Id);
        Assert.NotNull(course.Tenant);
        Assert.Equal(tenantId, course.Tenant!.Id);
        Assert.Equal(tenantName, course.Tenant.OrganizationName);
    }

    [Fact]
    public async Task GetCourseById_WhenExists_ReturnsOk()
    {
        var (tenantId, _) = await CreateTestTenantAsync();
        var createResponse = await this.client.PostAsJsonAsync("/courses", new { Name = "Lookup Course", OrganizationId = tenantId, TimeZoneId = TestTimeZones.Chicago });
        var created = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        var response = await this.client.GetAsync($"/courses/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var course = await response.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.Equal("Lookup Course", course!.Name);
    }

    [Fact]
    public async Task GetCourseById_IncludesTenantInfo()
    {
        var (tenantId, tenantName) = await CreateTestTenantAsync();
        var createResponse = await this.client.PostAsJsonAsync("/courses", new { Name = "Lookup Course with Tenant Info", OrganizationId = tenantId, TimeZoneId = TestTimeZones.Chicago });
        var created = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        var response = await this.client.GetAsync($"/courses/{created!.Id}");
        var course = await response.Content.ReadFromJsonAsync<CourseResponse>();

        Assert.NotNull(course);
        Assert.NotNull(course!.Tenant);
        Assert.Equal(tenantId, course.Tenant!.Id);
        Assert.Equal(tenantName, course.Tenant.OrganizationName);
    }

    [Fact]
    public async Task GetCourseById_WhenNotExists_ReturnsNotFound()
    {
        var response = await this.client.GetAsync($"/courses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostCourse_ReturnsCreated()
    {
        var (tenantId, tenantName) = await CreateTestTenantAsync();

        var response = await this.client.PostAsJsonAsync("/courses", new
        {
            Name = "Braemar Golf Course",
            OrganizationId = tenantId,
            StreetAddress = "6364 John Harris Dr",
            City = "Edina",
            State = "MN",
            ZipCode = "55439",
            ContactEmail = "pro@braemargolf.com",
            ContactPhone = "952-826-6799",
            TimeZoneId = TestTimeZones.Chicago
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(body);
        Assert.Equal("Braemar Golf Course", body!.Name);
        Assert.Equal(TestTimeZones.Chicago, body.TimeZoneId);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.NotNull(body.Tenant);
        Assert.Equal(tenantId, body.Tenant!.Id);
        Assert.Equal(tenantName, body.Tenant.OrganizationName);
    }

    [Fact]
    public async Task PostCourse_DuplicateName_ReturnsConflict()
    {
        var (tenantId, _) = await CreateTestTenantAsync();

        await this.client.PostAsJsonAsync("/courses", new { Name = "Duplicate Course", OrganizationId = tenantId, TimeZoneId = TestTimeZones.Chicago });
        var response = await this.client.PostAsJsonAsync("/courses", new { Name = "Duplicate Course", OrganizationId = tenantId, TimeZoneId = TestTimeZones.Chicago });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostCourse_DuplicateName_CaseInsensitive_ReturnsConflict()
    {
        var (tenantId, _) = await CreateTestTenantAsync();

        await this.client.PostAsJsonAsync("/courses", new { Name = "Pine Valley", OrganizationId = tenantId, TimeZoneId = TestTimeZones.Chicago });
        var response = await this.client.PostAsJsonAsync("/courses", new { Name = "PINE VALLEY", OrganizationId = tenantId, TimeZoneId = TestTimeZones.Chicago });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostCourse_DuplicateName_WithSqlWildcards_ReturnsConflict()
    {
        // Regression: EF.Functions.Like treated % and _ as SQL wildcards, causing false
        // positives/negatives. Equality check via == is correct; collation handles case.
        var (tenantId, _) = await CreateTestTenantAsync();

        await this.client.PostAsJsonAsync("/courses", new { Name = "Pine%Valley", OrganizationId = tenantId, TimeZoneId = TestTimeZones.Chicago });
        var response = await this.client.PostAsJsonAsync("/courses", new { Name = "Pine%Valley", OrganizationId = tenantId, TimeZoneId = TestTimeZones.Chicago });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostCourse_SameNameDifferentTenant_ReturnsCreated()
    {
        var (tenantId1, _) = await CreateTestTenantAsync();
        var (tenantId2, _) = await CreateTestTenantAsync();

        await this.client.PostAsJsonAsync("/courses", new { Name = "Shared Name Course", OrganizationId = tenantId1, TimeZoneId = TestTimeZones.Chicago });
        var response = await this.client.PostAsJsonAsync("/courses", new { Name = "Shared Name Course", OrganizationId = tenantId2, TimeZoneId = TestTimeZones.Chicago });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<(Guid Id, string OrganizationName)> CreateTestTenantAsync()
    {
        var orgName = $"Test Tenant {Guid.NewGuid()}";
        var response = await this.client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = orgName,
            ContactName = "Test Contact",
            ContactEmail = "test@tenant.com",
            ContactPhone = "555-0000"
        });

        var tenant = await response.Content.ReadFromJsonAsync<TenantIdResponse>();
        return (tenant!.Id, orgName);
    }
}

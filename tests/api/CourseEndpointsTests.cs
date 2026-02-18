using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;

public class CourseEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public CourseEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAllCourses_ReturnsOk()
    {
        var (tenantId, tenantName) = await CreateTestTenantAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Content = JsonContent.Create(new { Name = "Test Course" });
        await _client.SendAsync(request);

        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/courses");
        getRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var courses = await response.Content.ReadFromJsonAsync<List<CourseResponse>>();
        Assert.NotNull(courses);
        Assert.NotEmpty(courses!);
    }

    [Fact]
    public async Task GetAllCourses_IncludesTenantInfo()
    {
        var (tenantId, tenantName) = await CreateTestTenantAsync();
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Test Course for Tenant Info" });
        var createResponse = await _client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/courses");
        getRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(getRequest);
        var courses = await response.Content.ReadFromJsonAsync<List<CourseResponse>>();

        Assert.NotNull(courses);
        var course = courses!.First(c => c.Id == created!.Id);
        Assert.NotNull(course.Tenant);
        Assert.Equal(tenantId, course.Tenant.Id);
        Assert.Equal(tenantName, course.Tenant.OrganizationName);
    }

    [Fact]
    public async Task GetCourseById_WhenExists_ReturnsOk()
    {
        var (tenantId, _) = await CreateTestTenantAsync();
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Lookup Course" });
        var createResponse = await _client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/courses/{created!.Id}");
        getRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var course = await response.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.Equal("Lookup Course", course!.Name);
    }

    [Fact]
    public async Task GetCourseById_IncludesTenantInfo()
    {
        var (tenantId, tenantName) = await CreateTestTenantAsync();
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Lookup Course with Tenant Info" });
        var createResponse = await _client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/courses/{created!.Id}");
        getRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(getRequest);
        var course = await response.Content.ReadFromJsonAsync<CourseResponse>();

        Assert.NotNull(course);
        Assert.NotNull(course!.Tenant);
        Assert.Equal(tenantId, course.Tenant.Id);
        Assert.Equal(tenantName, course.Tenant.OrganizationName);
    }

    [Fact]
    public async Task GetCourseById_WhenNotExists_ReturnsNotFound()
    {
        var (tenantId, _) = await CreateTestTenantAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/courses/{Guid.NewGuid()}");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostCourse_ReturnsCreated()
    {
        var (tenantId, tenantName) = await CreateTestTenantAsync();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        httpRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        httpRequest.Content = JsonContent.Create(new
        {
            Name = "Braemar Golf Course",
            StreetAddress = "6364 John Harris Dr",
            City = "Edina",
            State = "MN",
            ZipCode = "55439",
            ContactEmail = "pro@braemargolf.com",
            ContactPhone = "952-826-6799"
        });

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(body);
        Assert.Equal("Braemar Golf Course", body!.Name);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.NotNull(body.Tenant);
        Assert.Equal(tenantId, body.Tenant.Id);
        Assert.Equal(tenantName, body.Tenant.OrganizationName);
    }

    [Fact]
    public async Task PostCourse_WithoutName_ReturnsBadRequest()
    {
        var (tenantId, _) = await CreateTestTenantAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Content = JsonContent.Create(new { StreetAddress = "123 Main St" });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCourse_DuplicateName_ReturnsConflict()
    {
        var (tenantId, _) = await CreateTestTenantAsync();

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request1.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request1.Content = JsonContent.Create(new { Name = "Duplicate Course" });
        await _client.SendAsync(request1);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request2.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request2.Content = JsonContent.Create(new { Name = "Duplicate Course" });
        var response = await _client.SendAsync(request2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostCourse_DuplicateName_CaseInsensitive_ReturnsConflict()
    {
        var (tenantId, _) = await CreateTestTenantAsync();

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request1.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request1.Content = JsonContent.Create(new { Name = "Pine Valley" });
        await _client.SendAsync(request1);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request2.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request2.Content = JsonContent.Create(new { Name = "PINE VALLEY" });
        var response = await _client.SendAsync(request2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostCourse_DuplicateName_WithSqlWildcards_ReturnsConflict()
    {
        // Regression: EF.Functions.Like treated % and _ as SQL wildcards, causing false
        // positives/negatives. Equality check via == is correct; collation handles case.
        var (tenantId, _) = await CreateTestTenantAsync();

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request1.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request1.Content = JsonContent.Create(new { Name = "Pine%Valley" });
        await _client.SendAsync(request1);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request2.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request2.Content = JsonContent.Create(new { Name = "Pine%Valley" });
        var response = await _client.SendAsync(request2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostCourse_SameNameDifferentTenant_ReturnsCreated()
    {
        var (tenantId1, _) = await CreateTestTenantAsync();
        var (tenantId2, _) = await CreateTestTenantAsync();

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request1.Headers.Add("X-Tenant-Id", tenantId1.ToString());
        request1.Content = JsonContent.Create(new { Name = "Shared Name Course" });
        await _client.SendAsync(request1);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request2.Headers.Add("X-Tenant-Id", tenantId2.ToString());
        request2.Content = JsonContent.Create(new { Name = "Shared Name Course" });
        var response = await _client.SendAsync(request2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<(Guid Id, string OrganizationName)> CreateTestTenantAsync()
    {
        var orgName = $"Test Tenant {Guid.NewGuid()}";
        var response = await _client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = orgName,
            ContactName = "Test Contact",
            ContactEmail = "test@tenant.com",
            ContactPhone = "555-0000"
        });

        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>();
        return (tenant!.Id, orgName);
    }

    private record CourseResponse(
        Guid Id,
        string Name,
        string? StreetAddress,
        string? City,
        string? State,
        string? ZipCode,
        string? ContactEmail,
        string? ContactPhone,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        TenantInfo Tenant);

    private record TenantInfo(Guid Id, string OrganizationName);

    private record TenantResponse(Guid Id);
}

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
        await _client.PostAsJsonAsync("/courses", new { Name = "Test Course" });

        var response = await _client.GetAsync("/courses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var courses = await response.Content.ReadFromJsonAsync<List<CourseResponse>>();
        Assert.NotNull(courses);
        Assert.NotEmpty(courses!);
    }

    [Fact]
    public async Task GetCourseById_WhenExists_ReturnsOk()
    {
        var createResponse = await _client.PostAsJsonAsync("/courses", new { Name = "Lookup Course" });
        var created = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        var response = await _client.GetAsync($"/courses/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var course = await response.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.Equal("Lookup Course", course!.Name);
    }

    [Fact]
    public async Task GetCourseById_WhenNotExists_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/courses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostCourse_ReturnsCreated()
    {
        var request = new
        {
            Name = "Braemar Golf Course",
            StreetAddress = "6364 John Harris Dr",
            City = "Edina",
            State = "MN",
            ZipCode = "55439",
            ContactEmail = "pro@braemargolf.com",
            ContactPhone = "952-826-6799"
        };

        var response = await _client.PostAsJsonAsync("/courses", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(body);
        Assert.Equal("Braemar Golf Course", body!.Name);
        Assert.NotEqual(Guid.Empty, body.Id);
    }

    [Fact]
    public async Task PostCourse_WithoutName_ReturnsBadRequest()
    {
        var request = new { StreetAddress = "123 Main St" };

        var response = await _client.PostAsJsonAsync("/courses", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        DateTimeOffset UpdatedAt);
}

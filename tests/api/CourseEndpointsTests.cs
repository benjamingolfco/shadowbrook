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

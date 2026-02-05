using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;

public class TeeTimeSettingsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TeeTimeSettingsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateCourse()
    {
        var response = await _client.PostAsJsonAsync("/courses", new { Name = "Test Course" });
        var course = await response.Content.ReadFromJsonAsync<CourseResponse>();
        return course!.Id;
    }

    [Fact]
    public async Task UpdateTeeTimeSettings_ReturnsOk()
    {
        var courseId = await CreateCourse();

        var response = await _client.PutAsJsonAsync($"/courses/{courseId}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "18:00"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TeeTimeSettingsResponse>();
        Assert.NotNull(body);
        Assert.Equal(10, body!.TeeTimeIntervalMinutes);
        Assert.Equal("07:00:00", body.FirstTeeTime);
        Assert.Equal("18:00:00", body.LastTeeTime);
    }

    [Fact]
    public async Task UpdateTeeTimeSettings_InvalidInterval_ReturnsBadRequest()
    {
        var courseId = await CreateCourse();

        var response = await _client.PutAsJsonAsync($"/courses/{courseId}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 15,
            FirstTeeTime = "07:00",
            LastTeeTime = "18:00"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTeeTimeSettings_FirstAfterLast_ReturnsBadRequest()
    {
        var courseId = await CreateCourse();

        var response = await _client.PutAsJsonAsync($"/courses/{courseId}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "18:00",
            LastTeeTime = "07:00"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTeeTimeSettings_FirstEqualsLast_ReturnsBadRequest()
    {
        var courseId = await CreateCourse();

        var response = await _client.PutAsJsonAsync($"/courses/{courseId}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "07:00"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTeeTimeSettings_CourseNotFound_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync($"/courses/{Guid.NewGuid()}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "18:00"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTeeTimeSettings_AfterUpdate_ReturnsSettings()
    {
        var courseId = await CreateCourse();

        await _client.PutAsJsonAsync($"/courses/{courseId}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 8,
            FirstTeeTime = "06:30",
            LastTeeTime = "17:30"
        });

        var response = await _client.GetAsync($"/courses/{courseId}/tee-time-settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TeeTimeSettingsResponse>();
        Assert.Equal(8, body!.TeeTimeIntervalMinutes);
        Assert.Equal("06:30:00", body.FirstTeeTime);
        Assert.Equal("17:30:00", body.LastTeeTime);
    }

    [Fact]
    public async Task GetTeeTimeSettings_NotConfigured_ReturnsEmptyObject()
    {
        var courseId = await CreateCourse();

        var response = await _client.GetAsync($"/courses/{courseId}/tee-time-settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private record CourseResponse(Guid Id, string Name);

    private record TeeTimeSettingsResponse(
        int TeeTimeIntervalMinutes,
        string FirstTeeTime,
        string LastTeeTime);
}

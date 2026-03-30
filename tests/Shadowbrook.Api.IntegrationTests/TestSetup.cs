using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.IntegrationTests;

public static class TestSetup
{
    public static async Task<Guid> CreateTenantAsync(HttpClient client, string? orgName = null)
    {
        var response = await client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = orgName ?? $"Test Tenant {Guid.NewGuid()}",
            ContactName = "Test Contact",
            ContactEmail = "test@tenant.com",
            ContactPhone = "555-0000"
        });
        response.EnsureSuccessStatusCode();

        var tenant = await response.Content.ReadFromJsonAsync<TenantIdResponse>();
        return tenant!.Id;
    }

    public static async Task<(Guid TenantId, Guid CourseId)> CreateCourseAsync(
        HttpClient client,
        string? courseName = null,
        string timeZoneId = "America/Chicago")
    {
        var tenantId = await CreateTenantAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Content = JsonContent.Create(new
        {
            Name = courseName ?? $"Test Course {Guid.NewGuid()}",
            TimeZoneId = timeZoneId
        });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var course = await response.Content.ReadFromJsonAsync<CourseIdResponse>();
        return (tenantId, course!.Id);
    }

    public static async Task<(Guid TenantId, Guid CourseId)> CreateCourseWithSettingsAsync(
        HttpClient client,
        int intervalMinutes = 10,
        string firstTeeTime = "07:00",
        string lastTeeTime = "17:00")
    {
        var (tenantId, courseId) = await CreateCourseAsync(client);

        await client.PutAsJsonAsync($"/courses/{courseId}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = intervalMinutes,
            FirstTeeTime = firstTeeTime,
            LastTeeTime = lastTeeTime
        });

        return (tenantId, courseId);
    }

    public static async Task<(Guid WaitlistId, string ShortCode)> OpenWaitlistAsync(
        HttpClient client,
        Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/open");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WaitlistResponse>();
        return (body!.Id, body.ShortCode);
    }

    public static async Task<Guid> AddGolferToWaitlistAsync(
        HttpClient client,
        Guid courseId,
        string firstName = "Jane",
        string lastName = "Smith",
        string phone = "555-867-5309",
        int groupSize = 1)
    {
        var response = await client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/entries",
            new { FirstName = firstName, LastName = lastName, Phone = phone, GroupSize = groupSize });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AddGolferResponse>();
        return body!.EntryId;
    }
}

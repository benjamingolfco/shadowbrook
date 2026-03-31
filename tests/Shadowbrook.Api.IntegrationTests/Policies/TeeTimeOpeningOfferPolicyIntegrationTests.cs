using System.Net;
using System.Net.Http.Json;
using Shadowbrook.Api.Features.Waitlist.Endpoints;
using Shadowbrook.Api.Infrastructure.Dev;

namespace Shadowbrook.Api.IntegrationTests.Policies;

[Collection("Integration")]
[IntegrationTest]
public class TeeTimeOpeningOfferPolicyIntegrationTests(TestWebApplicationFactory factory) : IAsyncLifetime
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
    public async Task OpeningCreated_WithGolferOnWaitlist_CreatesOfferAfterGracePeriod()
    {
        // Arrange
        var (_, courseId) = await CreateTestCourseWithSettingsAsync();
        await OpenWaitlistAsync(courseId);
        await AddGolferToWaitlistAsync(courseId);

        // Act — create opening at a time within the golfer's walk-up window (now to now+30min)
        var chicagoNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(TestTimeZones.Chicago));
        var teeTime = new DateTime(chicagoNow.Year, chicagoNow.Month, chicagoNow.Day,
            chicagoNow.Hour, chicagoNow.Minute, 0).AddMinutes(10);

        await CreateTeeTimeOpeningAsync(courseId, teeTime);

        // Assert — poll the dev SMS conversation for the golfer until an offer SMS arrives
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        List<DevSmsMessage>? messages = null;
        while (DateTime.UtcNow < deadline)
        {
            var response = await this.client.GetAsync("/dev/sms/conversations/%2B15558675309");
            response.EnsureSuccessStatusCode();
            messages = await response.Content.ReadFromJsonAsync<List<DevSmsMessage>>();

            if (messages is not null && messages.Any(m => m.Body.Contains("/book/walkup/")))
            {
                break;
            }

            await Task.Delay(500);
        }

        Assert.NotNull(messages);
        Assert.Contains(messages!, m => m.Body.Contains("/book/walkup/"));
    }

    private async Task CreateTeeTimeOpeningAsync(Guid courseId, DateTime teeTime)
    {
        var response = await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/tee-time-openings",
            new CreateTeeTimeOpeningRequest(teeTime, 3));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task OpenWaitlistAsync(Guid courseId)
    {
        var response = await this.client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/open"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task AddGolferToWaitlistAsync(Guid courseId)
    {
        var response = await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/entries",
            new AddGolferToWaitlistRequest("Jane", "Smith", "555-867-5309", 1));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<(Guid TenantId, Guid CourseId)> CreateTestCourseWithSettingsAsync()
    {
        var tenantId = await CreateTestTenantAsync();

        var createResponse = await this.client.PostAsJsonAsync("/courses", new { Name = $"Test Course {Guid.NewGuid()}", TenantId = tenantId, TimeZoneId = TestTimeZones.Chicago });
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        await this.client.PutAsJsonAsync($"/courses/{course!.Id}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "17:00"
        });

        return (tenantId, course.Id);
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

        var tenant = await response.Content.ReadFromJsonAsync<TenantIdResponse>();
        return tenant!.Id;
    }
}

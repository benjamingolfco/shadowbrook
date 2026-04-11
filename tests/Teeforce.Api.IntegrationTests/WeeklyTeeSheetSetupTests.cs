using System.Net;
using System.Net.Http.Json;

namespace Teeforce.Api.IntegrationTests;

[Collection("Integration")]
[IntegrationTest]
[TestCaseOrderer(
    "Teeforce.Api.IntegrationTests.StepOrderer",
    "Teeforce.Api.IntegrationTests")]
public class WeeklyTeeSheetSetupTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly TestWebApplicationFactory factory = factory;
    private HttpClient client = null!;
    private Guid courseId;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SetupCourseWithDefaults()
    {
        await this.factory.ResetDatabaseAsync();
        await this.factory.SeedTestAdminAsync();
        this.client = this.factory.CreateAuthenticatedClient();

        var tenantId = await TestSetup.CreateTenantAsync(this.client);
        var response = await this.client.PostAsJsonAsync("/courses", new
        {
            Name = "Weekly Test Course",
            OrganizationId = tenantId,
            TimeZoneId = TestTimeZones.Chicago,
        });
        var course = await response.Content.ReadFromJsonAsync<CourseIdResponse>();
        this.courseId = course!.Id;

        await this.client.PutAsJsonAsync($"/courses/{this.courseId}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "17:00",
            DefaultCapacity = 4,
        });
    }

    [Fact]
    public async Task Step1_WeeklyStatus_AllNotStarted()
    {
        await SetupCourseWithDefaults();

        var response = await this.client.GetAsync(
            $"/courses/{this.courseId}/tee-sheets/week?startDate=2026-04-13");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WeeklyStatusResponse>();
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 4, 13), result!.WeekStart);
        Assert.Equal(new DateOnly(2026, 4, 19), result.WeekEnd);
        Assert.Equal(7, result.Days.Count);
        Assert.All(result.Days, day => Assert.Equal("notStarted", day.Status));
    }

    [Fact]
    public async Task Step2_BulkDraft_CreatesSheetsForMultipleDates()
    {
        await SetupCourseWithDefaults();

        var response = await this.client.PostAsJsonAsync(
            $"/courses/{this.courseId}/tee-sheets/draft",
            new { Dates = new[] { "2026-04-14", "2026-04-15", "2026-04-16" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BulkDraftResponse>();
        Assert.NotNull(result);
        Assert.Equal(3, result!.TeeSheets.Count);
        Assert.All(result.TeeSheets, item => Assert.NotEqual(Guid.Empty, item.TeeSheetId));
    }

    [Fact]
    public async Task Step3_WeeklyStatus_ShowsMixedStatuses()
    {
        await SetupCourseWithDefaults();

        await this.client.PostAsJsonAsync(
            $"/courses/{this.courseId}/tee-sheets/draft",
            new { Dates = new[] { "2026-04-14", "2026-04-15" } });

        await this.client.PostAsync(
            $"/courses/{this.courseId}/tee-sheets/2026-04-15/publish",
            content: null);

        var response = await this.client.GetAsync(
            $"/courses/{this.courseId}/tee-sheets/week?startDate=2026-04-13");
        var result = await response.Content.ReadFromJsonAsync<WeeklyStatusResponse>();
        Assert.NotNull(result);

        var monday = result!.Days.Single(d => d.Date == new DateOnly(2026, 4, 13));
        Assert.Equal("notStarted", monday.Status);

        var tuesday = result.Days.Single(d => d.Date == new DateOnly(2026, 4, 14));
        Assert.Equal("draft", tuesday.Status);
        Assert.NotNull(tuesday.TeeSheetId);
        Assert.True(tuesday.IntervalCount > 0);

        var wednesday = result.Days.Single(d => d.Date == new DateOnly(2026, 4, 15));
        Assert.Equal("published", wednesday.Status);
    }

    [Fact]
    public async Task Step4_BulkDraft_FailsIfAnyDateAlreadyHasSheet()
    {
        await SetupCourseWithDefaults();

        await this.client.PostAsJsonAsync(
            $"/courses/{this.courseId}/tee-sheets/draft",
            new { Dates = new[] { "2026-04-14" } });

        var response = await this.client.PostAsJsonAsync(
            $"/courses/{this.courseId}/tee-sheets/draft",
            new { Dates = new[] { "2026-04-14", "2026-04-15" } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("2026-04-14", error!.Error);
    }

    [Fact]
    public async Task Step5_BulkDraft_FailsIfScheduleDefaultsNotConfigured()
    {
        await this.factory.ResetDatabaseAsync();
        await this.factory.SeedTestAdminAsync();
        this.client = this.factory.CreateAuthenticatedClient();

        var tenantId = await TestSetup.CreateTenantAsync(this.client);
        var courseResponse = await this.client.PostAsJsonAsync("/courses", new
        {
            Name = "No Defaults Course",
            OrganizationId = tenantId,
            TimeZoneId = TestTimeZones.Chicago,
        });
        var course = await courseResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        var response = await this.client.PostAsJsonAsync(
            $"/courses/{course!.Id}/tee-sheets/draft",
            new { Dates = new[] { "2026-04-14" } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Step6_WeeklyStatus_MissingStartDate_ReturnsBadRequest()
    {
        await SetupCourseWithDefaults();

        var response = await this.client.GetAsync(
            $"/courses/{this.courseId}/tee-sheets/week");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Step7_WeeklyStatus_InvalidDateValue_ReturnsBadRequest()
    {
        await SetupCourseWithDefaults();

        var response = await this.client.GetAsync(
            $"/courses/{this.courseId}/tee-sheets/week?startDate=not-a-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Step8_BulkDraft_EmptyDates_ReturnsValidationError()
    {
        await SetupCourseWithDefaults();

        var response = await this.client.PostAsJsonAsync(
            $"/courses/{this.courseId}/tee-sheets/draft",
            new { Dates = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.IntegrationTests;

[Collection("Integration")]
[IntegrationTest]
public class RemoveWaitlistEntryTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        await factory.SeedTestAdminAsync();
        this.client = factory.CreateAuthenticatedClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Remove_ValidEntry_Returns204()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);
        var addResponse = await PostAddGolferAsync(courseId, new
        {
            FirstName = "Jane",
            LastName = "Smith",
            Phone = "555-867-5309"
        });
        var addBody = await addResponse.Content.ReadFromJsonAsync<AddGolferResponse>();

        var response = await DeleteEntryAsync(courseId, addBody!.EntryId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Remove_EntryNotFound_Returns404()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);

        var response = await DeleteEntryAsync(courseId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Waitlist entry not found.", body!.Error);
    }

    [Fact]
    public async Task Remove_AlreadyRemoved_Returns404()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);
        var addResponse = await PostAddGolferAsync(courseId, new
        {
            FirstName = "Jane",
            LastName = "Smith",
            Phone = "555-867-5309"
        });
        var addBody = await addResponse.Content.ReadFromJsonAsync<AddGolferResponse>();

        await DeleteEntryAsync(courseId, addBody!.EntryId);
        var response = await DeleteEntryAsync(courseId, addBody.EntryId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Waitlist entry not found.", body!.Error);
    }

    [Fact]
    public async Task Remove_EntryDisappearsFromGetToday()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);

        var r1 = await PostAddGolferAsync(courseId, new { FirstName = "Alice", LastName = "A", Phone = "555-111-0001" });
        var r2 = await PostAddGolferAsync(courseId, new { FirstName = "Bob", LastName = "B", Phone = "555-111-0002" });
        var r3 = await PostAddGolferAsync(courseId, new { FirstName = "Carol", LastName = "C", Phone = "555-111-0003" });

        var b1 = await r1.Content.ReadFromJsonAsync<AddGolferResponse>();
        var b2 = await r2.Content.ReadFromJsonAsync<AddGolferResponse>();
        var b3 = await r3.Content.ReadFromJsonAsync<AddGolferResponse>();

        // Remove middle entry
        await DeleteEntryAsync(courseId, b2!.EntryId);

        var todayResponse = await GetTodayAsync(courseId);
        var todayBody = await todayResponse.Content.ReadFromJsonAsync<WaitlistTodayResponse>();

        Assert.Equal(HttpStatusCode.OK, todayResponse.StatusCode);
        Assert.NotNull(todayBody);
        Assert.Equal(2, todayBody!.Entries.Count);
        Assert.Contains(todayBody.Entries, e => e.GolferName == "Alice A");
        Assert.Contains(todayBody.Entries, e => e.GolferName == "Carol C");
        Assert.DoesNotContain(todayBody.Entries, e => e.GolferName == "Bob B");
    }

    [Fact]
    public async Task Remove_PositionsRenumber()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);

        var r1 = await PostAddGolferAsync(courseId, new { FirstName = "Alice", LastName = "A", Phone = "555-111-0001" });
        var r2 = await PostAddGolferAsync(courseId, new { FirstName = "Bob", LastName = "B", Phone = "555-111-0002" });
        var r3 = await PostAddGolferAsync(courseId, new { FirstName = "Carol", LastName = "C", Phone = "555-111-0003" });

        var b1 = await r1.Content.ReadFromJsonAsync<AddGolferResponse>();

        // Remove first entry
        await DeleteEntryAsync(courseId, b1!.EntryId);

        var todayResponse = await GetTodayAsync(courseId);
        var todayBody = await todayResponse.Content.ReadFromJsonAsync<WaitlistTodayResponse>();

        Assert.Equal(HttpStatusCode.OK, todayResponse.StatusCode);
        Assert.NotNull(todayBody);
        Assert.Equal(2, todayBody!.Entries.Count);

        // Verify remaining entries are ordered correctly (position is derived from JoinedAt order)
        Assert.Equal("Bob B", todayBody.Entries[0].GolferName);
        Assert.Equal("Carol C", todayBody.Entries[1].GolferName);
    }

    [Fact]
    public async Task Remove_WrongCourse_Returns404()
    {
        var (_, courseIdA) = await CreateTestCourseAsync();
        var (_, courseIdB) = await CreateTestCourseAsync();

        await PostOpenAsync(courseIdA);
        var addResponse = await PostAddGolferAsync(courseIdA, new
        {
            FirstName = "Jane",
            LastName = "Smith",
            Phone = "555-867-5309"
        });
        var addBody = await addResponse.Content.ReadFromJsonAsync<AddGolferResponse>();

        // Try to delete course A's entry via course B's URL
        var response = await DeleteEntryAsync(courseIdB, addBody!.EntryId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Waitlist entry not found.", body!.Error);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<HttpResponseMessage> DeleteEntryAsync(Guid courseId, Guid entryId)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/courses/{courseId}/walkup-waitlist/entries/{entryId}");
        return await this.client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostAddGolferAsync(Guid courseId, object body) =>
        await this.client.PostAsJsonAsync($"/courses/{courseId}/walkup-waitlist/entries", body);

    private async Task<HttpResponseMessage> PostOpenAsync(Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/open");
        return await this.client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetTodayAsync(Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/courses/{courseId}/walkup-waitlist/today");
        return await this.client.SendAsync(request);
    }

    private async Task<(Guid TenantId, Guid CourseId)> CreateTestCourseAsync()
    {
        var tenantId = await CreateTestTenantAsync();

        var createResponse = await this.client.PostAsJsonAsync("/courses", new { Name = $"Test Course {Guid.NewGuid()}", OrganizationId = tenantId, TimeZoneId = TestTimeZones.Chicago });
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        return (tenantId, course!.Id);
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

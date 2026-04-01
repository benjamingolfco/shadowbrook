using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.OrganizationAggregate;

namespace Shadowbrook.Api.IntegrationTests.Features.Auth;

[Collection("Integration")]
[IntegrationTest]
public class AuthEndpointsTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetMe_AuthenticatedOperator_ReturnsProfileWithOrgAndCourses()
    {
        // Seed org, course, and app user
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var org = Organization.Create("Benjamin Golf Co");
        db.Organizations.Add(org);

        var course = Course.Create(org.Id, "Pine Valley Golf Club", "America/Chicago");
        db.Courses.Add(course);

        var identityId = Guid.NewGuid().ToString();
        var appUser = AppUser.Create(identityId, "operator@example.com", "Jane Smith", AppUserRole.Operator, org.Id);
        db.AppUsers.Add(appUser);

        await db.SaveChangesAsync();

        // Create client with Bearer token set to identityId (DevAuth treats it as `oid`)
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {identityId}");

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(body);
        Assert.Equal(appUser.Id, body!.Id);
        Assert.Equal("operator@example.com", body.Email);
        Assert.Equal("Jane Smith", body.DisplayName);
        Assert.Equal("Operator", body.Role);
        Assert.NotNull(body.Organization);
        Assert.Equal(org.Id, body.Organization!.Id);
        Assert.Equal("Benjamin Golf Co", body.Organization.Name);
        Assert.Single(body.Courses);
        Assert.Equal(course.Id, body.Courses[0].Id);
        Assert.Equal("Pine Valley Golf Club", body.Courses[0].Name);
        Assert.Contains("app:access", body.Permissions);
    }

    [Fact]
    public async Task GetMe_Unauthenticated_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record MeResponse(
        Guid Id,
        string Email,
        string DisplayName,
        string Role,
        OrgResponse? Organization,
        List<CourseResponse> Courses,
        List<string> Permissions);

    private sealed record OrgResponse(Guid Id, string Name);

    private sealed record CourseResponse(Guid Id, string Name);
}

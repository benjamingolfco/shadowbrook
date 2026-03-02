using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shadowbrook.Api.Data;

namespace Shadowbrook.Api.Tests;

/// <summary>
/// Integration tests verifying rate limiting on POST /walkup/verify.
///
/// Uses a factory configured with a tight 2-attempt window (via IConfiguration override)
/// to exercise the 429 path without sending the full 11 requests required by the
/// production limit. Each test sends a unique X-Forwarded-For header so tests get
/// isolated rate limit buckets despite sharing the same factory instance.
/// </summary>
public class WalkUpRateLimitTests : IClassFixture<TightRateLimitFactory>
{
    private readonly TightRateLimitFactory _factory;

    public WalkUpRateLimitTests(TightRateLimitFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task VerifyCode_ExceedsRateLimit_Returns429()
    {
        // Each test uses a unique client IP via X-Forwarded-For to isolate its rate limit bucket.
        // The factory allows 2 requests per window; the 3rd gets 429.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1");

        var response1 = await client.PostAsJsonAsync("/walkup/verify", new { code = "0000" });
        var response2 = await client.PostAsJsonAsync("/walkup/verify", new { code = "0001" });

        // Third request exceeds the 2-permit window
        var response3 = await client.PostAsJsonAsync("/walkup/verify", new { code = "0002" });

        Assert.NotEqual(HttpStatusCode.TooManyRequests, response1.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, response2.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, response3.StatusCode);
    }

    [Fact]
    public async Task VerifyCode_ExceedsRateLimit_ReturnsRetryAfterHeader()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.2");

        await client.PostAsJsonAsync("/walkup/verify", new { code = "1000" });
        await client.PostAsJsonAsync("/walkup/verify", new { code = "1001" });
        var response = await client.PostAsJsonAsync("/walkup/verify", new { code = "1002" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"),
            "429 response should include a Retry-After header");
    }

    [Fact]
    public async Task VerifyCode_ExceedsRateLimit_ReturnsErrorJson()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.3");

        await client.PostAsJsonAsync("/walkup/verify", new { code = "2000" });
        await client.PostAsJsonAsync("/walkup/verify", new { code = "2001" });
        var response = await client.PostAsJsonAsync("/walkup/verify", new { code = "2002" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RateLimitErrorResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body!.Error));
    }

    [Fact]
    public async Task JoinWaitlist_NotRateLimited_ReturnsNon429()
    {
        // /walkup/join must NOT be rate limited — send 3 requests (more than the tight
        // 2-attempt /verify limit on this IP) and confirm we never get 429
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.4");

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/walkup/join", new
            {
                courseWaitlistId = Guid.NewGuid(),
                firstName = "Tiger",
                lastName = "Woods",
                phone = $"612555{i:0000}"
            });
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    private record RateLimitErrorResponse(string Error);
}

/// <summary>
/// WebApplicationFactory with a tight 2-attempt rate limit window configured via
/// UseSetting, which overrides configuration before Program.cs reads GetValue at startup.
/// </summary>
public class TightRateLimitFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting updates the host configuration before Program.cs reads GetValue.
        // ConfigureAppConfiguration does not work here because Program.cs reads the values
        // as local variables during WebApplication.CreateBuilder startup, before that hook runs.
        builder.UseSetting("RateLimiting:WalkUpVerify:PermitLimit", "2");
        builder.UseSetting("RateLimiting:WalkUpVerify:WindowSeconds", "300");

        builder.ConfigureServices(services =>
        {
            // Replace EF Core SQL Server with SQLite in-memory
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            connection.Open();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connection));
        });

        builder.UseEnvironment("Testing");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        return host;
    }
}

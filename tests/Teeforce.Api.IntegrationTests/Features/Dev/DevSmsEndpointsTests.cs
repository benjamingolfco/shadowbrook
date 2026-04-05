using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Api.Infrastructure.Dev;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.GolferAggregate;

namespace Teeforce.Api.IntegrationTests.Features.Dev;

[Collection("Integration")]
[IntegrationTest]
public class DevSmsEndpointsTests(TestWebApplicationFactory factory) : IAsyncLifetime
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
    public async Task DeleteConversation_RemovesAllMessagesForPhoneNumber()
    {
        // Seed two conversations
        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Conversation 1: +15551234567
            db.DevSmsMessages.Add(new DevSmsMessage
            {
                Id = Guid.CreateVersion7(),
                From = "TeeforceGolfClub",
                To = "+15551234567",
                Body = "Test message 1",
                Direction = SmsDirection.Outbound,
                Timestamp = DateTimeOffset.UtcNow.AddHours(-2)
            });
            db.DevSmsMessages.Add(new DevSmsMessage
            {
                Id = Guid.CreateVersion7(),
                From = "+15551234567",
                To = "TeeforceGolfClub",
                Body = "Test reply 1",
                Direction = SmsDirection.Inbound,
                Timestamp = DateTimeOffset.UtcNow.AddHours(-1)
            });

            // Conversation 2: +15559876543
            db.DevSmsMessages.Add(new DevSmsMessage
            {
                Id = Guid.CreateVersion7(),
                From = "TeeforceGolfClub",
                To = "+15559876543",
                Body = "Test message 2",
                Direction = SmsDirection.Outbound,
                Timestamp = DateTimeOffset.UtcNow.AddHours(-3)
            });
            db.DevSmsMessages.Add(new DevSmsMessage
            {
                Id = Guid.CreateVersion7(),
                From = "+15559876543",
                To = "TeeforceGolfClub",
                Body = "Test reply 2",
                Direction = SmsDirection.Inbound,
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-30)
            });

            await db.SaveChangesAsync();
        }

        // Delete conversation 1
        var encodedPhoneNumber = Uri.EscapeDataString("+15551234567");
        var response = await this.client.DeleteAsync($"/dev/sms/conversations/{encodedPhoneNumber}");

        // Assert endpoint returns 204
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify conversation 1 is gone and conversation 2 remains
        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var allMessages = await db.DevSmsMessages.ToListAsync();

            // Conversation 1 must be fully deleted
            Assert.DoesNotContain(allMessages, m => m.From == "+15551234567" || m.To == "+15551234567");

            // Conversation 2 must still exist (both rows)
            var conv2 = allMessages.Where(m => m.From == "+15559876543" || m.To == "+15559876543").ToList();
            Assert.Equal(2, conv2.Count);
        }
    }

    [Fact]
    public async Task DeleteConversation_WithNonexistentPhoneNumber_ReturnsNoContent()
    {
        // Idempotent behavior — no error when phone number doesn't exist
        var encodedPhoneNumber = Uri.EscapeDataString("+15550000000");
        var response = await this.client.DeleteAsync($"/dev/sms/conversations/{encodedPhoneNumber}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetByGolfer_ReturnsConversationForGolfer()
    {
        Guid golferId;

        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Create a golfer
            var golfer = Golfer.Create("+15551112222", "Test", "Golfer");
            db.Golfers.Add(golfer);
            await db.SaveChangesAsync();
            golferId = golfer.Id;

            // Seed SMS messages for this golfer's phone
            db.DevSmsMessages.Add(new DevSmsMessage
            {
                Id = Guid.CreateVersion7(),
                From = DatabaseTextMessageService.SystemPhoneNumber,
                To = "+15551112222",
                Body = "You're booked!",
                Direction = SmsDirection.Outbound,
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10)
            });
            // Also seed a message for a different phone to verify filtering
            db.DevSmsMessages.Add(new DevSmsMessage
            {
                Id = Guid.CreateVersion7(),
                From = DatabaseTextMessageService.SystemPhoneNumber,
                To = "+15559999999",
                Body = "Different golfer",
                Direction = SmsDirection.Outbound,
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5)
            });
            await db.SaveChangesAsync();
        }

        var response = await this.client.GetAsync($"/dev/sms/golfers/{golferId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var messages = await response.Content.ReadFromJsonAsync<DevSmsMessage[]>();
        Assert.NotNull(messages);
        Assert.Single(messages);
        Assert.Equal("You're booked!", messages[0].Body);
    }

    [Fact]
    public async Task GetByGolfer_WithNonexistentGolfer_ReturnsNotFound()
    {
        var response = await this.client.GetAsync($"/dev/sms/golfers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

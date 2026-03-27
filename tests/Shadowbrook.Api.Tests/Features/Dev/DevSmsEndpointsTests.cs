using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Dev;
using Shadowbrook.Api.Infrastructure.Services;

namespace Shadowbrook.Api.Tests.Features.Dev;

[Collection("Integration")]
[IntegrationTest]
public class DevSmsEndpointsTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = factory.CreateClient();
    private readonly TestWebApplicationFactory factory = factory;

    public Task InitializeAsync() => this.factory.ResetDatabaseAsync();
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
                From = "ShadowbrookGolfClub",
                To = "+15551234567",
                Body = "Test message 1",
                Direction = SmsDirection.Outbound,
                Timestamp = DateTimeOffset.UtcNow.AddHours(-2)
            });
            db.DevSmsMessages.Add(new DevSmsMessage
            {
                Id = Guid.CreateVersion7(),
                From = "+15551234567",
                To = "ShadowbrookGolfClub",
                Body = "Test reply 1",
                Direction = SmsDirection.Inbound,
                Timestamp = DateTimeOffset.UtcNow.AddHours(-1)
            });

            // Conversation 2: +15559876543
            db.DevSmsMessages.Add(new DevSmsMessage
            {
                Id = Guid.CreateVersion7(),
                From = "ShadowbrookGolfClub",
                To = "+15559876543",
                Body = "Test message 2",
                Direction = SmsDirection.Outbound,
                Timestamp = DateTimeOffset.UtcNow.AddHours(-3)
            });
            db.DevSmsMessages.Add(new DevSmsMessage
            {
                Id = Guid.CreateVersion7(),
                From = "+15559876543",
                To = "ShadowbrookGolfClub",
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

        // Verify only conversation 2 remains
        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var remaining = await db.DevSmsMessages.ToListAsync();

            Assert.Equal(2, remaining.Count);
            Assert.All(remaining, m =>
                Assert.True(m.From == "+15559876543" || m.To == "+15559876543"));
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
}

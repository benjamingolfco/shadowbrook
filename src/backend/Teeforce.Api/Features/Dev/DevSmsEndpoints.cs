using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;

namespace Shadowbrook.Api.Features.Dev;

public static class DevSmsEndpoints
{
    public static IEndpointRouteBuilder MapDevSmsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/dev/sms").WithTags("Dev SMS");

        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var messages = await db.DevSmsMessages
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();
            return Results.Ok(messages);
        }).WithSummary("List all SMS messages");

        group.MapGet("/conversations/{phoneNumber}", async (string phoneNumber, ApplicationDbContext db) =>
        {
            var decoded = Uri.UnescapeDataString(phoneNumber);
            var messages = await db.DevSmsMessages
                .Where(m => m.From == decoded || m.To == decoded)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
            return Results.Ok(messages);
        }).WithSummary("Get conversation thread for a phone number");

        group.MapGet("/golfers/{golferId:guid}", async (Guid golferId, ApplicationDbContext db) =>
        {
            var phone = await db.Golfers
                .IgnoreQueryFilters()
                .Where(g => g.Id == golferId)
                .Select(g => g.Phone)
                .FirstOrDefaultAsync();

            if (phone is null)
            {
                return Results.NotFound(new { error = "Golfer not found." });
            }

            var messages = await db.DevSmsMessages
                .Where(m => m.From == phone || m.To == phone)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            return Results.Ok(messages);
        }).WithSummary("Get SMS conversation for a golfer by ID");

        group.MapPost("/inbound", async (InboundSmsRequest request, DatabaseTextMessageService smsService) =>
        {
            await smsService.AddInboundAsync(request.FromPhoneNumber, request.Message);
            return Results.Ok();
        }).WithSummary("Simulate an inbound SMS from a golfer");

        group.MapDelete("/", async (ApplicationDbContext db) =>
        {
            await db.DevSmsMessages.ExecuteDeleteAsync();
            return Results.NoContent();
        }).WithSummary("Clear all SMS messages");

        group.MapDelete("/conversations/{phoneNumber}", async (string phoneNumber, ApplicationDbContext db) =>
        {
            var decoded = Uri.UnescapeDataString(phoneNumber);
            await db.DevSmsMessages
                .Where(m => m.From == decoded || m.To == decoded)
                .ExecuteDeleteAsync();
            return Results.NoContent();
        }).WithSummary("Delete all messages for a phone number");

        return app;
    }

    public record InboundSmsRequest(string FromPhoneNumber, string Message);
}

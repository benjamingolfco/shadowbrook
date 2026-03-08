using Shadowbrook.Api.Infrastructure.Services;

namespace Shadowbrook.Api.Endpoints;

public static class DevSmsEndpoints
{
    public static IEndpointRouteBuilder MapDevSmsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/dev/sms").WithTags("Dev SMS");

        group.MapGet("/", () =>
        {
            var messages = InMemoryTextMessageService.GetAll();
            return Results.Ok(messages);
        }).WithSummary("List all SMS messages");

        group.MapGet("/conversations/{phoneNumber}", (string phoneNumber) =>
        {
            var decoded = Uri.UnescapeDataString(phoneNumber);
            var messages = InMemoryTextMessageService.GetByPhone(decoded);
            return Results.Ok(messages);
        }).WithSummary("Get conversation thread for a phone number");

        group.MapPost("/inbound", (InboundSmsRequest request) =>
        {
            InMemoryTextMessageService.AddInbound(request.FromPhoneNumber, request.Message);
            return Results.Created();
        }).WithSummary("Simulate an inbound SMS from a golfer");

        group.MapDelete("/", () =>
        {
            InMemoryTextMessageService.Clear();
            return Results.NoContent();
        }).WithSummary("Clear all SMS messages");

        return app;
    }

    public record InboundSmsRequest(string FromPhoneNumber, string Message);
}

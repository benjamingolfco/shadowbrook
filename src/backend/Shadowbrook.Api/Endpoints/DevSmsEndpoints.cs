using Shadowbrook.Api.Infrastructure.Services;

namespace Shadowbrook.Api.Endpoints;

public static class DevSmsEndpoints
{
    public static IEndpointRouteBuilder MapDevSmsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/dev/sms").WithTags("Dev SMS");

        group.MapGet("/", (InMemoryTextMessageService smsService) =>
        {
            var messages = smsService.GetAll();
            return Results.Ok(messages);
        }).WithSummary("List all SMS messages");

        group.MapGet("/conversations/{phoneNumber}", (string phoneNumber, InMemoryTextMessageService smsService) =>
        {
            var decoded = Uri.UnescapeDataString(phoneNumber);
            var messages = smsService.GetByPhone(decoded);
            return Results.Ok(messages);
        }).WithSummary("Get conversation thread for a phone number");

        group.MapPost("/inbound", (InboundSmsRequest request, InMemoryTextMessageService smsService) =>
        {
            smsService.AddInbound(request.FromPhoneNumber, request.Message);
            return Results.Ok();
        }).WithSummary("Simulate an inbound SMS from a golfer");

        group.MapDelete("/", (InMemoryTextMessageService smsService) =>
        {
            smsService.Clear();
            return Results.NoContent();
        }).WithSummary("Clear all SMS messages");

        return app;
    }

    public record InboundSmsRequest(string FromPhoneNumber, string Message);
}

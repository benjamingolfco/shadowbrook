using Teeforce.Api.Infrastructure.Notifications;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public record WalkupConfirmation(string CourseName, DateOnly Date, TimeOnly Time) : INotification;

public class WalkupConfirmationSmsFormatter : ISmsFormatter<WalkupConfirmation>
{
    public string Format(WalkupConfirmation n) =>
        $"Your tee time at {n.CourseName} on {n.Date:MMMM d} at {n.Time:h:mm tt} is confirmed. See you on the course!";
}

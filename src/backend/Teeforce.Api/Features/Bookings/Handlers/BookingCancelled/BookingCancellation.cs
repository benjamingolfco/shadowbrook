using Teeforce.Api.Infrastructure.Notifications;

namespace Teeforce.Api.Features.Bookings.Handlers;

public record BookingCancellation(string CourseName, DateOnly Date, TimeOnly Time, string? Reason = null) : INotification;

public class BookingCancellationSmsFormatter : ISmsFormatter<BookingCancellation>
{
    public string Format(BookingCancellation n)
    {
        var message = $"Your tee time at {n.CourseName} on {n.Date:MMMM d, yyyy} at {n.Time:h:mm tt} has been cancelled.";
        if (n.Reason is not null)
        {
            message += $" Reason: {n.Reason}";
        }

        return message;
    }
}

using Teeforce.Api.Infrastructure.Notifications;

namespace Teeforce.Api.Features.Bookings.Handlers;

public record BookingConfirmation(string CourseName, DateOnly Date, TimeOnly Time) : INotification;

public class BookingConfirmationSmsFormatter : ISmsFormatter<BookingConfirmation>
{
    public string Format(BookingConfirmation n) =>
        $"You're booked! {n.CourseName} at {n.Time:h:mm tt} on {n.Date:MMMM d, yyyy}. See you on the course!";
}

using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Bookings.Handlers;

public record BookingConfirmation(string CourseName, DateOnly Date, TimeOnly Time) : INotification;

public class BookingConfirmationSmsFormatter : SmsFormatter<BookingConfirmation>
{
    protected override string FormatMessage(BookingConfirmation n) =>
        $"You're booked! {n.CourseName} at {n.Time:h:mm tt} on {n.Date:MMMM d, yyyy}. See you on the course!";
}

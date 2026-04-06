using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Bookings.Handlers;

public record BookingCancellation(string CourseName, DateOnly Date, TimeOnly Time) : INotification;

public class BookingCancellationSmsFormatter : SmsFormatter<BookingCancellation>
{
    protected override string FormatMessage(BookingCancellation n) =>
        $"Your tee time at {n.CourseName} on {n.Date:MMMM d, yyyy} at {n.Time:h:mm tt} has been cancelled.";
}

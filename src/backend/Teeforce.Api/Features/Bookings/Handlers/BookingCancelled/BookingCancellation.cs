using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Bookings.Handlers;

public record BookingCancellation(string CourseName, DateOnly Date, TimeOnly Time) : INotification;

public class BookingCancellationSmsFormatter : ISmsFormatter<BookingCancellation>
{
    public string Format(BookingCancellation n) =>
        $"Your tee time at {n.CourseName} on {n.Date:MMMM d, yyyy} at {n.Time:h:mm tt} has been cancelled.";
}

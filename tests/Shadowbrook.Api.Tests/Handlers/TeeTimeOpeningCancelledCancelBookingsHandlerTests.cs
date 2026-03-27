using Microsoft.Extensions.Logging;
using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class TeeTimeOpeningCancelledCancelBookingsHandlerTests
{
    private readonly IBookingRepository bookingRepository = Substitute.For<IBookingRepository>();
    private readonly ILogger<TeeTimeOpeningCancelledCancelBookingsHandler> logger = Substitute.For<ILogger<TeeTimeOpeningCancelledCancelBookingsHandler>>();
    private readonly TeeTimeOpeningCancelledCancelBookingsHandler handler;

    public TeeTimeOpeningCancelledCancelBookingsHandlerTests()
    {
        this.handler = new TeeTimeOpeningCancelledCancelBookingsHandler(this.bookingRepository, this.logger);
    }

    [Fact]
    public async Task Handle_WhenBookingsExist_RejectsThemAll()
    {
        var evt = new TeeTimeOpeningCancelled
        {
            OpeningId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 1),
            TeeTime = new TimeOnly(9, 0),
        };

        var booking1 = Booking.Create(
            Guid.NewGuid(),
            evt.CourseId,
            Guid.NewGuid(),
            evt.Date,
            evt.TeeTime,
            "Jane Doe",
            2);

        var booking2 = Booking.Create(
            Guid.NewGuid(),
            evt.CourseId,
            Guid.NewGuid(),
            evt.Date,
            evt.TeeTime,
            "John Smith",
            1);

        this.bookingRepository.GetByCourseAndTeeTimeAsync(evt.CourseId, evt.Date, evt.TeeTime, Arg.Any<CancellationToken>())
            .Returns([booking1, booking2]);

        await this.handler.Handle(evt, CancellationToken.None);

        Assert.Equal(BookingStatus.Rejected, booking1.Status);
        Assert.Equal(BookingStatus.Rejected, booking2.Status);
    }

    [Fact]
    public async Task Handle_WhenNoBookingsExist_ReturnsGracefully()
    {
        var evt = new TeeTimeOpeningCancelled
        {
            OpeningId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 1),
            TeeTime = new TimeOnly(9, 0),
        };

        this.bookingRepository.GetByCourseAndTeeTimeAsync(evt.CourseId, evt.Date, evt.TeeTime, Arg.Any<CancellationToken>())
            .Returns([]);

        await this.handler.Handle(evt, CancellationToken.None);

        // Just verify it completes without error - logging is verified manually/observability
        await this.bookingRepository.Received(1).GetByCourseAndTeeTimeAsync(evt.CourseId, evt.Date, evt.TeeTime, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoPendingBookings_ReturnsGracefully()
    {
        var evt = new TeeTimeOpeningCancelled
        {
            OpeningId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 1),
            TeeTime = new TimeOnly(9, 0),
        };

        var booking = Booking.Create(
            Guid.NewGuid(),
            evt.CourseId,
            Guid.NewGuid(),
            evt.Date,
            evt.TeeTime,
            "Jane Doe",
            2);
        booking.RejectBooking(); // Already rejected

        this.bookingRepository.GetByCourseAndTeeTimeAsync(evt.CourseId, evt.Date, evt.TeeTime, Arg.Any<CancellationToken>())
            .Returns([booking]);

        await this.handler.Handle(evt, CancellationToken.None);

        // Just verify it completes without error - booking should still be Rejected
        Assert.Equal(BookingStatus.Rejected, booking.Status);
    }
}

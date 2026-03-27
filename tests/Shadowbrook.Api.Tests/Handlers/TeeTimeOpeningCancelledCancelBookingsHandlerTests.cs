using Microsoft.Extensions.Logging;
using NSubstitute;
using Shadowbrook.Api.Features.Bookings.Handlers;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;
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

    private static TeeTimeOpeningCancelled CreateEvent() => new()
    {
        OpeningId = Guid.NewGuid(),
        CourseId = Guid.NewGuid(),
        Date = new DateOnly(2026, 6, 1),
        TeeTime = new TimeOnly(9, 0),
    };

    private static Booking CreateBooking(TeeTimeOpeningCancelled evt) =>
        Booking.Create(Guid.NewGuid(), evt.CourseId, Guid.NewGuid(), evt.Date, evt.TeeTime, 2);

    [Fact]
    public async Task Handle_WhenPendingAndConfirmedBookings_CancelsAll()
    {
        var evt = CreateEvent();
        var pending = CreateBooking(evt);
        var confirmed = CreateBooking(evt);
        confirmed.Confirm();

        this.bookingRepository.GetByCourseAndTeeTimeAsync(evt.CourseId, Arg.Any<TeeTime>(), Arg.Any<CancellationToken>())
            .Returns([pending, confirmed]);

        await this.handler.Handle(evt, CancellationToken.None);

        Assert.Equal(BookingStatus.Cancelled, pending.Status);
        Assert.Equal(BookingStatus.Cancelled, confirmed.Status);
    }

    [Fact]
    public async Task Handle_WhenNoBookingsExist_LogsWarningAndReturns()
    {
        var evt = CreateEvent();

        this.bookingRepository.GetByCourseAndTeeTimeAsync(evt.CourseId, Arg.Any<TeeTime>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await this.handler.Handle(evt, CancellationToken.None);

        await this.bookingRepository.Received(1).GetByCourseAndTeeTimeAsync(evt.CourseId, Arg.Any<TeeTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOnlyTerminalBookings_DoesNothing()
    {
        var evt = CreateEvent();
        var rejected = CreateBooking(evt);
        rejected.Reject();
        var cancelled = CreateBooking(evt);
        cancelled.Cancel();

        this.bookingRepository.GetByCourseAndTeeTimeAsync(evt.CourseId, Arg.Any<TeeTime>(), Arg.Any<CancellationToken>())
            .Returns([rejected, cancelled]);

        await this.handler.Handle(evt, CancellationToken.None);

        Assert.Equal(BookingStatus.Rejected, rejected.Status);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task Handle_WhenMixOfStates_CancelsOnlyNonTerminal()
    {
        var evt = CreateEvent();
        var pending = CreateBooking(evt);
        var confirmed = CreateBooking(evt);
        confirmed.Confirm();
        var rejected = CreateBooking(evt);
        rejected.Reject();

        this.bookingRepository.GetByCourseAndTeeTimeAsync(evt.CourseId, Arg.Any<TeeTime>(), Arg.Any<CancellationToken>())
            .Returns([pending, confirmed, rejected]);

        await this.handler.Handle(evt, CancellationToken.None);

        Assert.Equal(BookingStatus.Cancelled, pending.Status);
        Assert.Equal(BookingStatus.Cancelled, confirmed.Status);
        Assert.Equal(BookingStatus.Rejected, rejected.Status);
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Teeforce.Api.Features.Bookings.Handlers;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeTimeAggregate;

namespace Teeforce.Api.Tests.Features.Bookings.Handlers;

public class BookingCancelledReleaseTeeTimeClaimHandlerTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public BookingCancelledReleaseTeeTimeClaimHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private TeeTime MakeTeeTimeWithClaim(Guid bookingId, Guid courseId)
    {
        var settings = new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(8, 0), 30, 2);
        var sheet = Teeforce.Domain.TeeSheetAggregate.TeeSheet.Draft(courseId, new DateOnly(2026, 6, 1), settings, this.timeProvider);
        sheet.Publish(this.timeProvider);
        var auth = sheet.AuthorizeBooking();
        return TeeTime.Claim(sheet.Intervals[0], courseId, sheet.Date, auth, bookingId, Guid.NewGuid(), 2, this.timeProvider);
    }

    [Fact]
    public async Task Handle_DirectBooking_ReleasesTheClaim()
    {
        var bookingId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var teeTime = MakeTeeTimeWithClaim(bookingId, courseId);

        var bookings = Substitute.For<IBookingRepository>();
        var teeTimes = Substitute.For<ITeeTimeRepository>();
        var booking = Booking.CreateConfirmed(
            bookingId: bookingId,
            courseId: courseId,
            golferId: Guid.NewGuid(),
            teeTimeId: teeTime.Id,
            date: new DateOnly(2026, 6, 1),
            teeTime: new TimeOnly(7, 0),
            playerCount: 2);
        bookings.GetByIdAsync(bookingId).Returns(booking);
        teeTimes.GetByIdAsync(teeTime.Id).Returns(teeTime);

        await BookingCancelledReleaseTeeTimeClaimHandler.Handle(
            new BookingCancelled { BookingId = bookingId, PreviousStatus = BookingStatus.Confirmed },
            bookings,
            teeTimes,
            this.timeProvider,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Empty(teeTime.Claims);
    }

    [Fact]
    public async Task Handle_WalkUpBooking_NoOps()
    {
        var bookings = Substitute.For<IBookingRepository>();
        var teeTimes = Substitute.For<ITeeTimeRepository>();
        var booking = Booking.CreateConfirmed(
            bookingId: Guid.NewGuid(),
            courseId: Guid.NewGuid(),
            golferId: Guid.NewGuid(),
            teeTimeId: null,
            date: new DateOnly(2026, 6, 1),
            teeTime: new TimeOnly(7, 0),
            playerCount: 2);
        bookings.GetByIdAsync(Arg.Any<Guid>()).Returns(booking);

        await BookingCancelledReleaseTeeTimeClaimHandler.Handle(
            new BookingCancelled { BookingId = booking.Id, PreviousStatus = BookingStatus.Confirmed },
            bookings,
            teeTimes,
            this.timeProvider,
            NullLogger.Instance,
            CancellationToken.None);

        await teeTimes.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Handle_MissingBooking_LogsAndReturns()
    {
        var bookings = Substitute.For<IBookingRepository>();
        var teeTimes = Substitute.For<ITeeTimeRepository>();
        bookings.GetByIdAsync(Arg.Any<Guid>()).Returns((Booking?)null);

        await BookingCancelledReleaseTeeTimeClaimHandler.Handle(
            new BookingCancelled { BookingId = Guid.NewGuid(), PreviousStatus = BookingStatus.Confirmed },
            bookings,
            teeTimes,
            this.timeProvider,
            NullLogger.Instance,
            CancellationToken.None);

        await teeTimes.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }
}

using NSubstitute;
using Shadowbrook.Api.Features.Bookings.Handlers;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class TeeTimeOpeningSlotsClaimedCreateConfirmedBookingHandlerTests
{
    private readonly IBookingRepository bookingRepo = Substitute.For<IBookingRepository>();

    private static TeeTimeOpeningSlotsClaimed BuildEvent(
        Guid? bookingId = null,
        Guid? golferId = null,
        Guid? courseId = null,
        DateOnly? date = null,
        TimeOnly? teeTime = null,
        int groupSize = 2)
    {
        return new TeeTimeOpeningSlotsClaimed
        {
            OpeningId = Guid.NewGuid(),
            BookingId = bookingId ?? Guid.CreateVersion7(),
            GolferId = golferId ?? Guid.NewGuid(),
            CourseId = courseId ?? Guid.NewGuid(),
            Date = date ?? new DateOnly(2026, 3, 25),
            TeeTime = teeTime ?? new TimeOnly(10, 0),
            GroupSize = groupSize
        };
    }

    [Fact]
    public async Task Handle_CreatesConfirmedBookingWithCorrectProperties()
    {
        var bookingId = Guid.CreateVersion7();
        var golferId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 25);
        var teeTime = new TimeOnly(10, 0);

        var evt = BuildEvent(bookingId, golferId, courseId, date, teeTime, groupSize: 2);

        await TeeTimeOpeningSlotsClaimedCreateConfirmedBookingHandler.Handle(evt, this.bookingRepo);

        this.bookingRepo.Received(1).Add(Arg.Is<Booking>(b =>
            b.Id == bookingId &&
            b.CourseId == courseId &&
            b.GolferId == golferId &&
            b.TeeTime.Date == date &&
            b.TeeTime.Time == teeTime &&
            b.PlayerCount == 2 &&
            b.Status == BookingStatus.Confirmed));
    }

    [Fact]
    public async Task Handle_RaisesBookingConfirmedEvent()
    {
        var bookingId = Guid.CreateVersion7();
        var evt = BuildEvent(bookingId: bookingId);

        await TeeTimeOpeningSlotsClaimedCreateConfirmedBookingHandler.Handle(evt, this.bookingRepo);

        this.bookingRepo.Received(1).Add(Arg.Is<Booking>(b =>
            b.DomainEvents.OfType<BookingConfirmed>().Any(e => e.BookingId == bookingId)));
    }

    [Fact]
    public async Task Handle_UsesPreAllocatedBookingId()
    {
        var bookingId = Guid.CreateVersion7();
        var evt = BuildEvent(bookingId: bookingId);

        await TeeTimeOpeningSlotsClaimedCreateConfirmedBookingHandler.Handle(evt, this.bookingRepo);

        this.bookingRepo.Received(1).Add(Arg.Is<Booking>(b => b.Id == bookingId));
    }
}

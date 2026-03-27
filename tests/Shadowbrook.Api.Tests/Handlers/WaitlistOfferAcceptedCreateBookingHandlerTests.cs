using NSubstitute;
using Shadowbrook.Api.Features.Bookings.Handlers;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class WaitlistOfferAcceptedCreateBookingHandlerTests
{
    private readonly IBookingRepository bookingRepo = Substitute.For<IBookingRepository>();

    [Fact]
    public async Task Handle_CreatesConfirmedBookingWithCorrectProperties()
    {
        var bookingId = Guid.CreateVersion7();
        var golferId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 25);
        var teeTime = new TimeOnly(10, 0);

        var evt = new WaitlistOfferAccepted
        {
            WaitlistOfferId = Guid.NewGuid(),
            BookingId = bookingId,
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = golferId,
            GroupSize = 2,
            CourseId = courseId,
            Date = date,
            TeeTime = teeTime
        };

        await WaitlistOfferAcceptedCreateBookingHandler.Handle(evt, this.bookingRepo);

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

        var evt = new WaitlistOfferAccepted
        {
            WaitlistOfferId = Guid.NewGuid(),
            BookingId = bookingId,
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 1,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(10, 0)
        };

        await WaitlistOfferAcceptedCreateBookingHandler.Handle(evt, this.bookingRepo);

        this.bookingRepo.Received(1).Add(Arg.Is<Booking>(b =>
            b.DomainEvents.OfType<BookingConfirmed>().Any(e => e.BookingId == bookingId)));
    }

    [Fact]
    public async Task Handle_UsesPreAllocatedBookingId()
    {
        var bookingId = Guid.CreateVersion7();

        var evt = new WaitlistOfferAccepted
        {
            WaitlistOfferId = Guid.NewGuid(),
            BookingId = bookingId,
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 1,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(10, 0)
        };

        await WaitlistOfferAcceptedCreateBookingHandler.Handle(evt, this.bookingRepo);

        this.bookingRepo.Received(1).Add(Arg.Is<Booking>(b => b.Id == bookingId));
    }
}

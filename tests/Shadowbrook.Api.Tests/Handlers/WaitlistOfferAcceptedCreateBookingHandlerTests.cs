using NSubstitute;
using Shadowbrook.Api.Features.Bookings.Handlers;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class WaitlistOfferAcceptedCreateBookingHandlerTests
{
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly IBookingRepository bookingRepo = Substitute.For<IBookingRepository>();

    [Fact]
    public async Task Handle_CreatesBookingWithCorrectProperties()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 25);
        var teeTime = new TimeOnly(10, 0);

        this.golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        var evt = new WaitlistOfferAccepted
        {
            WaitlistOfferId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = golfer.Id,
            GroupSize = 2,
            CourseId = courseId,
            Date = date,
            TeeTime = teeTime
        };

        await WaitlistOfferAcceptedCreateBookingHandler.Handle(evt, this.golferRepo, this.bookingRepo);

        this.bookingRepo.Received(1).Add(Arg.Is<Booking>(b =>
            b.CourseId == courseId &&
            b.GolferId == golfer.Id &&
            b.TeeTime.Date == date &&
            b.TeeTime.Time == teeTime &&
            b.GolferName == golfer.FullName &&
            b.PlayerCount == 2));
    }

    [Fact]
    public async Task Handle_GolferNotFound_Throws()
    {
        var golferId = Guid.NewGuid();
        this.golferRepo.GetByIdAsync(golferId).Returns((Golfer?)null);

        var evt = new WaitlistOfferAccepted
        {
            WaitlistOfferId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = golferId,
            GroupSize = 1,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(10, 0)
        };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => WaitlistOfferAcceptedCreateBookingHandler.Handle(evt, this.golferRepo, this.bookingRepo));
    }
}

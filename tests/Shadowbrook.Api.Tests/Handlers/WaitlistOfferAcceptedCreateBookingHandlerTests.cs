using NSubstitute;
using Shadowbrook.Api.Features.Bookings.Handlers;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class WaitlistOfferAcceptedCreateBookingHandlerTests
{
    private readonly ITeeTimeOpeningRepository openingRepo = Substitute.For<ITeeTimeOpeningRepository>();
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly IBookingRepository bookingRepo = Substitute.For<IBookingRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public WaitlistOfferAcceptedCreateBookingHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_CreatesBookingWithCorrectProperties()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var opening = TeeTimeOpening.Create(
            Guid.NewGuid(), new DateOnly(2026, 3, 25), new TimeOnly(10, 0), 4, true, this.timeProvider);

        this.openingRepo.GetByIdAsync(opening.Id).Returns(opening);
        this.golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        var evt = new WaitlistOfferAccepted
        {
            WaitlistOfferId = Guid.NewGuid(),
            OpeningId = opening.Id,
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = golfer.Id,
            GroupSize = 2
        };

        await WaitlistOfferAcceptedCreateBookingHandler.Handle(
            evt, this.openingRepo, this.golferRepo, this.bookingRepo);

        this.bookingRepo.Received(1).Add(Arg.Is<Booking>(b =>
            b.CourseId == opening.CourseId &&
            b.GolferId == golfer.Id &&
            b.TeeTime.Date == opening.TeeTime.Date &&
            b.TeeTime.Time == opening.TeeTime.Time &&
            b.GolferName == golfer.FullName &&
            b.PlayerCount == 2));
    }

    [Fact]
    public async Task Handle_OpeningNotFound_Throws()
    {
        var openingId = Guid.NewGuid();
        this.openingRepo.GetByIdAsync(openingId).Returns((TeeTimeOpening?)null);

        var evt = new WaitlistOfferAccepted
        {
            WaitlistOfferId = Guid.NewGuid(),
            OpeningId = openingId,
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 1
        };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => WaitlistOfferAcceptedCreateBookingHandler.Handle(
                evt, this.openingRepo, this.golferRepo, this.bookingRepo));
    }

    [Fact]
    public async Task Handle_GolferNotFound_Throws()
    {
        var opening = TeeTimeOpening.Create(
            Guid.NewGuid(), new DateOnly(2026, 3, 25), new TimeOnly(10, 0), 4, true, this.timeProvider);
        var golferId = Guid.NewGuid();

        this.openingRepo.GetByIdAsync(opening.Id).Returns(opening);
        this.golferRepo.GetByIdAsync(golferId).Returns((Golfer?)null);

        var evt = new WaitlistOfferAccepted
        {
            WaitlistOfferId = Guid.NewGuid(),
            OpeningId = opening.Id,
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = golferId,
            GroupSize = 1
        };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => WaitlistOfferAcceptedCreateBookingHandler.Handle(
                evt, this.openingRepo, this.golferRepo, this.bookingRepo));
    }
}

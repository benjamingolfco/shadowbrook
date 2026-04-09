using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Features.TeeSheet.Handlers;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeOfferAggregate;

namespace Teeforce.Api.Tests.Features.TeeSheet.Handlers;

public class MarkOfferAcceptedHandlerTests
{
    private readonly IBookingRepository bookingRepository = Substitute.For<IBookingRepository>();
    private readonly ITeeTimeOfferRepository offerRepository = Substitute.For<ITeeTimeOfferRepository>();
    private readonly ILogger logger = Substitute.For<ILogger>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public MarkOfferAcceptedHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_BookingWithNoTeeTimeId_Skips()
    {
        var booking = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 6, 1), new TimeOnly(9, 0), 2);
        this.bookingRepository.GetByIdAsync(booking.Id).Returns(booking);

        await MarkOfferAcceptedHandler.Handle(
            new BookingConfirmed { BookingId = booking.Id },
            this.bookingRepository, this.offerRepository, this.logger, CancellationToken.None);

        await this.offerRepository.DidNotReceive()
            .GetPendingByTeeTimeAndGolfer(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoPendingOffer_Skips()
    {
        var bookingId = Guid.NewGuid();
        var teeTimeId = Guid.NewGuid();
        var golferId = Guid.NewGuid();
        var booking = Booking.CreateConfirmed(bookingId, Guid.NewGuid(), golferId, teeTimeId,
            new DateOnly(2026, 6, 1), new TimeOnly(9, 0), 2);
        this.bookingRepository.GetByIdAsync(bookingId).Returns(booking);
        this.offerRepository.GetPendingByTeeTimeAndGolfer(teeTimeId, golferId, Arg.Any<CancellationToken>())
            .Returns((TeeTimeOffer?)null);

        await MarkOfferAcceptedHandler.Handle(
            new BookingConfirmed { BookingId = bookingId },
            this.bookingRepository, this.offerRepository, this.logger, CancellationToken.None);

        // No exception — handler completes normally (direct booking, no offer)
    }

    [Fact]
    public async Task Handle_WithPendingOffer_MarksAccepted()
    {
        var bookingId = Guid.NewGuid();
        var teeTimeId = Guid.NewGuid();
        var golferId = Guid.NewGuid();
        var booking = Booking.CreateConfirmed(bookingId, Guid.NewGuid(), golferId, teeTimeId,
            new DateOnly(2026, 6, 1), new TimeOnly(9, 0), 2);
        this.bookingRepository.GetByIdAsync(bookingId).Returns(booking);

        var offer = TeeTimeOffer.Create(teeTimeId, Guid.NewGuid(), golferId, 2,
            Guid.NewGuid(), new DateOnly(2026, 6, 1), new TimeOnly(9, 0), this.timeProvider);
        this.offerRepository.GetPendingByTeeTimeAndGolfer(teeTimeId, golferId, Arg.Any<CancellationToken>())
            .Returns(offer);

        await MarkOfferAcceptedHandler.Handle(
            new BookingConfirmed { BookingId = bookingId },
            this.bookingRepository, this.offerRepository, this.logger, CancellationToken.None);

        Assert.Equal(TeeTimeOfferStatus.Accepted, offer.Status);
    }
}

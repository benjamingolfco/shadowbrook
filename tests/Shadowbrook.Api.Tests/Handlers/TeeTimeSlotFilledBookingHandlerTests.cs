using NSubstitute;
using Shadowbrook.Api.Features.Bookings;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Tests.Handlers;

public class TeeTimeSlotFilledBookingHandlerTests
{
    private readonly ITeeTimeRequestRepository requestRepo = Substitute.For<ITeeTimeRequestRepository>();
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly IBookingRepository bookingRepo = Substitute.For<IBookingRepository>();

    [Fact]
    public async Task Handle_RequestNotFound_DoesNothing()
    {
        var evt = MakeEvent();
        await Handle(evt);
        bookingRepo.DidNotReceive().Add(Arg.Any<Booking>());
    }

    [Fact]
    public async Task Handle_GolferNotFound_DoesNothing()
    {
        var request = await CreateRequest();
        requestRepo.GetByIdAsync(request.Id).Returns(request);
        var evt = MakeEvent(teeTimeRequestId: request.Id);

        await Handle(evt);
        bookingRepo.DidNotReceive().Add(Arg.Any<Booking>());
    }

    [Fact]
    public async Task Handle_OfferNotFound_DoesNothing()
    {
        var request = await CreateRequest();
        requestRepo.GetByIdAsync(request.Id).Returns(request);
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);
        var evt = MakeEvent(teeTimeRequestId: request.Id, golferId: golfer.Id);

        await Handle(evt);
        bookingRepo.DidNotReceive().Add(Arg.Any<Booking>());
    }

    [Fact]
    public async Task Handle_EntryNotFound_DoesNothing()
    {
        var request = await CreateRequest();
        requestRepo.GetByIdAsync(request.Id).Returns(request);
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        var offer = WaitlistOffer.Create(request.Id, Guid.NewGuid());
        offerRepo.GetByBookingIdAsync(offer.BookingId).Returns(offer);
        // entryRepo.GetByIdAsync not set up — returns null

        var evt = MakeEvent(teeTimeRequestId: request.Id, bookingId: offer.BookingId, golferId: golfer.Id);

        await Handle(evt);
        bookingRepo.DidNotReceive().Add(Arg.Any<Booking>());
    }

    [Fact]
    public async Task Handle_Success_CreatesBooking()
    {
        var request = await CreateRequest();
        requestRepo.GetByIdAsync(request.Id).Returns(request);

        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        var entry = await WaitlistEntryFactory.CreateAsync(golfer, groupSize: 2);
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var offer = WaitlistOffer.Create(request.Id, entry.Id);
        offerRepo.GetByBookingIdAsync(offer.BookingId).Returns(offer);

        var evt = MakeEvent(
            teeTimeRequestId: request.Id,
            bookingId: offer.BookingId,
            golferId: golfer.Id);

        await Handle(evt);

        bookingRepo.Received(1).Add(Arg.Is<Booking>(b =>
            b.Id == offer.BookingId &&
            b.CourseId == request.CourseId &&
            b.GolferId == golfer.Id &&
            b.GolferName == "Jane Smith" &&
            b.PlayerCount == 2));
    }

    private Task Handle(TeeTimeSlotFilled evt) =>
        TeeTimeSlotFilledBookingHandler.Handle(evt, requestRepo, golferRepo, offerRepo, entryRepo, bookingRepo);

    private static TeeTimeSlotFilled MakeEvent(
        Guid? teeTimeRequestId = null,
        Guid? bookingId = null,
        Guid? golferId = null) => new()
    {
        TeeTimeRequestId = teeTimeRequestId ?? Guid.NewGuid(),
        BookingId = bookingId ?? Guid.CreateVersion7(),
        GolferId = golferId ?? Guid.NewGuid()
    };

    private static async Task<TeeTimeRequest> CreateRequest()
    {
        var repo = Substitute.For<ITeeTimeRequestRepository>();
        repo.ExistsAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>()).Returns(false);
        return await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 6, 15), new TimeOnly(9, 0), 2, repo);
    }
}

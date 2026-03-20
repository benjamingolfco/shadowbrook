using NSubstitute;
using Shadowbrook.Api.Features.WaitlistOffers;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class WaitlistOfferAcceptedFillHandlerTests
{
    private readonly ITeeTimeRequestRepository requestRepo = Substitute.For<ITeeTimeRequestRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();

    [Fact]
    public async Task Handle_EntryNotFound_ReturnsNull()
    {
        var evt = MakeEvent();

        var result = await WaitlistOfferAcceptedFillHandler.Handle(evt, requestRepo, entryRepo);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_RequestNotFound_ReturnsFillFailed()
    {
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(entryId: entry.Id);

        var result = await WaitlistOfferAcceptedFillHandler.Handle(evt, requestRepo, entryRepo);

        var failed = Assert.IsType<TeeTimeSlotFillFailed>(result);
        Assert.Equal(evt.TeeTimeRequestId, failed.TeeTimeRequestId);
        Assert.Equal("Tee time request not found.", failed.Reason);
    }

    [Fact]
    public async Task Handle_FillFails_ReturnsFillFailed()
    {
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        // Create a request that's already fulfilled so Fill() returns failure
        var request = await CreateRequest();
        request.Fill(Guid.NewGuid(), request.GolfersNeeded, Guid.CreateVersion7()); // fills all slots
        requestRepo.GetByIdAsync(request.Id).Returns(request);

        var evt = MakeEvent(teeTimeRequestId: request.Id, entryId: entry.Id);

        var result = await WaitlistOfferAcceptedFillHandler.Handle(evt, requestRepo, entryRepo);

        var failed = Assert.IsType<TeeTimeSlotFillFailed>(result);
        Assert.Contains("already been filled", failed.Reason);
    }

    [Fact]
    public async Task Handle_Success_ReturnsSlotFilled()
    {
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid(), groupSize: 1);
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var request = await CreateRequest(golfersNeeded: 2);
        requestRepo.GetByIdAsync(request.Id).Returns(request);

        var evt = MakeEvent(teeTimeRequestId: request.Id, entryId: entry.Id);

        var result = await WaitlistOfferAcceptedFillHandler.Handle(evt, requestRepo, entryRepo);

        var filled = Assert.IsType<TeeTimeSlotFilled>(result);
        Assert.Equal(request.Id, filled.TeeTimeRequestId);
        Assert.Equal(evt.BookingId, filled.BookingId);
        Assert.Equal(evt.GolferId, filled.GolferId);
    }

    private static WaitlistOfferAccepted MakeEvent(
        Guid? teeTimeRequestId = null,
        Guid? entryId = null) => new()
    {
        WaitlistOfferId = Guid.NewGuid(),
        BookingId = Guid.CreateVersion7(),
        TeeTimeRequestId = teeTimeRequestId ?? Guid.NewGuid(),
        GolferWaitlistEntryId = entryId ?? Guid.NewGuid(),
        GolferId = Guid.NewGuid()
    };

    private static async Task<TeeTimeRequest> CreateRequest(int golfersNeeded = 2)
    {
        var repo = Substitute.For<ITeeTimeRequestRepository>();
        repo.ExistsAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>()).Returns(false);
        return await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 6, 15), new TimeOnly(9, 0), golfersNeeded, repo);
    }
}

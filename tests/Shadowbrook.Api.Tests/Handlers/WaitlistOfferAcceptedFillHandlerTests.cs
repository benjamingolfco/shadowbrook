using NSubstitute;
using Shadowbrook.Api.Features.WalkUpWaitlist;
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
    public async Task Handle_EntryNotFound_Throws()
    {
        var evt = MakeEvent();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => WaitlistOfferAcceptedFillHandler.Handle(evt, requestRepo, entryRepo));
    }

    [Fact]
    public async Task Handle_RequestNotFound_Throws()
    {
        var entry = await WaitlistEntryFactory.CreateAsync();
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(entryId: entry.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => WaitlistOfferAcceptedFillHandler.Handle(evt, requestRepo, entryRepo));
    }

    [Fact]
    public async Task Handle_FillFails_RaisesFillFailedDomainEvent()
    {
        var entry = await WaitlistEntryFactory.CreateAsync();
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        // Create a request that's already fulfilled so Fill() raises failure
        var request = await CreateRequest();
        request.Fill(Guid.NewGuid(), request.GolfersNeeded, Guid.CreateVersion7(), Guid.NewGuid()); // fills all slots
        request.ClearDomainEvents();
        requestRepo.GetByIdAsync(request.Id).Returns(request);

        var evt = MakeEvent(teeTimeRequestId: request.Id, entryId: entry.Id);

        await WaitlistOfferAcceptedFillHandler.Handle(evt, requestRepo, entryRepo);

        var domainEvent = Assert.Single(request.DomainEvents);
        var failed = Assert.IsType<TeeTimeSlotFillFailed>(domainEvent);
        Assert.Contains("already been filled", failed.Reason);
        Assert.Equal(evt.WaitlistOfferId, failed.OfferId);
    }

    [Fact]
    public async Task Handle_Success_RaisesSlotFilledDomainEvent()
    {
        var entry = await WaitlistEntryFactory.CreateAsync(groupSize: 1);
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var request = await CreateRequest(golfersNeeded: 2);
        request.ClearDomainEvents();
        requestRepo.GetByIdAsync(request.Id).Returns(request);

        var evt = MakeEvent(teeTimeRequestId: request.Id, entryId: entry.Id);

        await WaitlistOfferAcceptedFillHandler.Handle(evt, requestRepo, entryRepo);

        var filledEvent = request.DomainEvents.OfType<TeeTimeSlotFilled>().SingleOrDefault();
        Assert.NotNull(filledEvent);
        Assert.Equal(request.Id, filledEvent.TeeTimeRequestId);
        Assert.Equal(evt.BookingId, filledEvent.BookingId);
        Assert.Equal(evt.GolferId, filledEvent.GolferId);
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

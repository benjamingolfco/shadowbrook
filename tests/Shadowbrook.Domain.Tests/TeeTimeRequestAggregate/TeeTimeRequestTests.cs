using NSubstitute;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;

namespace Shadowbrook.Domain.Tests.TeeTimeRequestAggregate;

public class TeeTimeRequestTests
{
    private readonly ITeeTimeRequestRepository repository = Substitute.For<ITeeTimeRequestRepository>();

    [Fact]
    public async Task CreateAsync_CreatesRequestAndRaisesEvent()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);
        var teeTime = new TimeOnly(10, 0);

        var request = await TeeTimeRequest.CreateAsync(courseId, date, teeTime, 2, repository);

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal(courseId, request.CourseId);
        Assert.Equal(date, request.Date);
        Assert.Equal(teeTime, request.TeeTime);
        Assert.Equal(2, request.GolfersNeeded);
        Assert.Equal(TeeTimeRequestStatus.Pending, request.Status);

        var domainEvent = Assert.Single(request.DomainEvents);
        var addedEvent = Assert.IsType<TeeTimeRequestAdded>(domainEvent);
        Assert.Equal(request.Id, addedEvent.TeeTimeRequestId);
        Assert.Equal(courseId, addedEvent.CourseId);
        Assert.Equal(date, addedEvent.Date);
        Assert.Equal(teeTime, addedEvent.TeeTime);
        Assert.Equal(2, addedEvent.GolfersNeeded);
    }

    [Fact]
    public async Task CreateAsync_DuplicateTeeTime_Throws()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);
        var teeTime = new TimeOnly(10, 0);

        repository.ExistsAsync(courseId, date, teeTime).Returns(true);

        await Assert.ThrowsAsync<DuplicateTeeTimeRequestException>(
            () => TeeTimeRequest.CreateAsync(courseId, date, teeTime, 2, repository));
    }

    [Fact]
    public async Task CreateAsync_DifferentTeeTimes_Succeeds()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var first = await TeeTimeRequest.CreateAsync(courseId, date, new TimeOnly(10, 0), 2, repository);
        var second = await TeeTimeRequest.CreateAsync(courseId, date, new TimeOnly(11, 0), 3, repository);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Fill_Success_AddsSlotFillAndRaisesEvent()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 4, repository);
        request.ClearDomainEvents();

        var golferId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var result = request.Fill(golferId, groupSize: 2, bookingId, offerId);

        Assert.True(result.Success);
        Assert.Single(request.SlotFills);
        Assert.Equal(2, request.RemainingSlots);

        var domainEvent = Assert.Single(request.DomainEvents);
        var filled = Assert.IsType<TeeTimeSlotFilled>(domainEvent);
        Assert.Equal(request.Id, filled.TeeTimeRequestId);
        Assert.Equal(bookingId, filled.BookingId);
        Assert.Equal(golferId, filled.GolferId);
    }

    [Fact]
    public async Task Fill_GroupTooLarge_RaisesFailedEventAndReturnsFailure()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 2, repository);
        request.ClearDomainEvents();

        var offerId = Guid.NewGuid();
        var result = request.Fill(Guid.NewGuid(), groupSize: 3, Guid.NewGuid(), offerId);

        Assert.False(result.Success);
        Assert.Equal("Your group is too large for the remaining slots.", result.RejectionReason);
        Assert.Empty(request.SlotFills);

        var domainEvent = Assert.Single(request.DomainEvents);
        var failed = Assert.IsType<TeeTimeSlotFillFailed>(domainEvent);
        Assert.Equal(request.Id, failed.TeeTimeRequestId);
        Assert.Equal(offerId, failed.OfferId);
        Assert.Equal("Your group is too large for the remaining slots.", failed.Reason);
    }

    [Fact]
    public async Task Fill_AlreadyFulfilled_RaisesFailedEventAndReturnsFailure()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 1, repository);
        request.Fill(Guid.NewGuid(), groupSize: 1, Guid.NewGuid(), Guid.NewGuid()); // fills all slots
        request.ClearDomainEvents();

        var offerId = Guid.NewGuid();
        var result = request.Fill(Guid.NewGuid(), groupSize: 1, Guid.NewGuid(), offerId);

        Assert.False(result.Success);
        Assert.Equal("This tee time has already been filled.", result.RejectionReason);

        var domainEvent = Assert.Single(request.DomainEvents);
        var failed = Assert.IsType<TeeTimeSlotFillFailed>(domainEvent);
        Assert.Equal(request.Id, failed.TeeTimeRequestId);
        Assert.Equal(offerId, failed.OfferId);
        Assert.Equal("This tee time has already been filled.", failed.Reason);
    }

    [Fact]
    public async Task Fill_ExactFit_MarksFulfilled_RaisesBothEvents()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 2, repository);
        request.ClearDomainEvents();

        request.Fill(Guid.NewGuid(), groupSize: 2, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(TeeTimeRequestStatus.Fulfilled, request.Status);
        Assert.Equal(2, request.DomainEvents.Count);
        Assert.Contains(request.DomainEvents, e => e is TeeTimeSlotFilled);
        Assert.Contains(request.DomainEvents, e => e is TeeTimeRequestFulfilled fulfilled && fulfilled.TeeTimeRequestId == request.Id);
    }

    [Fact]
    public async Task Unfill_RemovesSlotFill_ResetsToPending()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 1, repository);
        var bookingId = Guid.NewGuid();
        request.Fill(Guid.NewGuid(), groupSize: 1, bookingId, Guid.NewGuid());
        Assert.Equal(TeeTimeRequestStatus.Fulfilled, request.Status);

        request.Unfill(bookingId);

        Assert.Equal(TeeTimeRequestStatus.Pending, request.Status);
        Assert.Empty(request.SlotFills);
        Assert.Equal(1, request.RemainingSlots);
    }

    [Fact]
    public async Task Unfill_RaisesSlotUnfilledEvent()
    {
        var golferId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 2, repository);
        request.Fill(golferId, groupSize: 1, bookingId, Guid.NewGuid());
        request.ClearDomainEvents();

        request.Unfill(bookingId);

        var domainEvent = Assert.Single(request.DomainEvents);
        var unfilled = Assert.IsType<TeeTimeSlotUnfilled>(domainEvent);
        Assert.Equal(request.Id, unfilled.TeeTimeRequestId);
        Assert.Equal(bookingId, unfilled.BookingId);
        Assert.Equal(golferId, unfilled.GolferId);
    }

    [Fact]
    public async Task Unfill_UnknownBookingId_RaisesNoEvent()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 2, repository);
        request.ClearDomainEvents();

        request.Unfill(Guid.NewGuid());

        Assert.Empty(request.DomainEvents);
    }

    [Fact]
    public async Task RemainingSlots_CalculatesCorrectly()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 4, repository);

        Assert.Equal(4, request.RemainingSlots);

        request.Fill(Guid.NewGuid(), groupSize: 2, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(2, request.RemainingSlots);

        request.Fill(Guid.NewGuid(), groupSize: 1, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(1, request.RemainingSlots);
    }
}

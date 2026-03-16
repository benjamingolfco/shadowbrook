using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;

namespace Shadowbrook.Domain.Tests.TeeTimeRequestAggregate;

public class TeeTimeRequestTests
{
    private readonly StubTeeTimeRequestRepository repository = new();

    [Fact]
    public async Task CreateAsync_CreatesRequestAndRaisesEvent()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);
        var teeTime = new TimeOnly(10, 0);

        var request = await TeeTimeRequest.CreateAsync(courseId, date, teeTime, 2, this.repository);

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

        this.repository.SetExists(true);

        await Assert.ThrowsAsync<DuplicateTeeTimeRequestException>(
            () => TeeTimeRequest.CreateAsync(courseId, date, teeTime, 2, this.repository));
    }

    [Fact]
    public async Task MarkFulfilled_TransitionsStatusToFulfilled()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 6), new TimeOnly(10, 0), 2, this.repository);

        request.MarkFulfilled();

        Assert.Equal(TeeTimeRequestStatus.Fulfilled, request.Status);
    }

    [Fact]
    public async Task CreateAsync_DifferentTeeTimes_Succeeds()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var first = await TeeTimeRequest.CreateAsync(courseId, date, new TimeOnly(10, 0), 2, this.repository);
        var second = await TeeTimeRequest.CreateAsync(courseId, date, new TimeOnly(11, 0), 3, this.repository);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Fill_Success_AddsSlotFill()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 4, this.repository);

        var result = request.Fill(Guid.NewGuid(), groupSize: 2, Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Single(request.SlotFills);
        Assert.Equal(2, request.RemainingSlots);
    }

    [Fact]
    public async Task Fill_GroupTooLarge_ReturnsFailure()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 2, this.repository);

        var result = request.Fill(Guid.NewGuid(), groupSize: 3, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("Your group is too large for the remaining slots.", result.RejectionReason);
        Assert.Empty(request.SlotFills);
    }

    [Fact]
    public async Task Fill_AlreadyFulfilled_ReturnsFailure()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 1, this.repository);
        request.Fill(Guid.NewGuid(), groupSize: 1, Guid.NewGuid());

        var result = request.Fill(Guid.NewGuid(), groupSize: 1, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("This tee time has already been filled.", result.RejectionReason);
    }

    [Fact]
    public async Task Fill_ExactFit_MarksFulfilled_RaisesEvent()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 2, this.repository);
        request.ClearDomainEvents(); // Clear the TeeTimeRequestAdded event

        request.Fill(Guid.NewGuid(), groupSize: 2, Guid.NewGuid());

        Assert.Equal(TeeTimeRequestStatus.Fulfilled, request.Status);
        var domainEvent = Assert.Single(request.DomainEvents);
        var fulfilled = Assert.IsType<TeeTimeRequestFulfilled>(domainEvent);
        Assert.Equal(request.Id, fulfilled.TeeTimeRequestId);
    }

    [Fact]
    public async Task Unfill_RemovesSlotFill_ResetsToPending()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 1, this.repository);
        var bookingId = Guid.NewGuid();
        request.Fill(Guid.NewGuid(), groupSize: 1, bookingId);
        Assert.Equal(TeeTimeRequestStatus.Fulfilled, request.Status);

        request.Unfill(bookingId);

        Assert.Equal(TeeTimeRequestStatus.Pending, request.Status);
        Assert.Empty(request.SlotFills);
        Assert.Equal(1, request.RemainingSlots);
    }

    [Fact]
    public async Task RemainingSlots_CalculatesCorrectly()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 16), new TimeOnly(10, 0), 4, this.repository);

        Assert.Equal(4, request.RemainingSlots);

        request.Fill(Guid.NewGuid(), groupSize: 2, Guid.NewGuid());
        Assert.Equal(2, request.RemainingSlots);

        request.Fill(Guid.NewGuid(), groupSize: 1, Guid.NewGuid());
        Assert.Equal(1, request.RemainingSlots);
    }

    private class StubTeeTimeRequestRepository : ITeeTimeRequestRepository
    {
        private bool exists;

        public void SetExists(bool value) => this.exists = value;

        public Task<bool> ExistsAsync(Guid courseId, DateOnly date, TimeOnly teeTime)
            => Task.FromResult(this.exists);

        public Task<TeeTimeRequest?> GetByIdAsync(Guid id)
            => Task.FromResult<TeeTimeRequest?>(null);

        public Task<List<TeeTimeRequest>> GetByCourseAndDateAsync(Guid courseId, DateOnly date)
            => Task.FromResult(new List<TeeTimeRequest>());

        public void Add(TeeTimeRequest request) { }

        public Task SaveAsync() => Task.CompletedTask;
    }
}

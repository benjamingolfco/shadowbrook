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
    public async Task CreateAsync_DifferentTeeTimes_Succeeds()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var first = await TeeTimeRequest.CreateAsync(courseId, date, new TimeOnly(10, 0), 2, this.repository);
        var second = await TeeTimeRequest.CreateAsync(courseId, date, new TimeOnly(11, 0), 3, this.repository);

        Assert.NotEqual(first.Id, second.Id);
    }

    private class StubTeeTimeRequestRepository : ITeeTimeRequestRepository
    {
        private bool exists;

        public void SetExists(bool value) => this.exists = value;

        public Task<bool> ExistsAsync(Guid courseId, DateOnly date, TimeOnly teeTime)
            => Task.FromResult(this.exists);

        public Task<List<TeeTimeRequest>> GetByCourseAndDateAsync(Guid courseId, DateOnly date)
            => Task.FromResult(new List<TeeTimeRequest>());

        public void Add(TeeTimeRequest request) { }

        public Task SaveAsync() => Task.CompletedTask;
    }
}

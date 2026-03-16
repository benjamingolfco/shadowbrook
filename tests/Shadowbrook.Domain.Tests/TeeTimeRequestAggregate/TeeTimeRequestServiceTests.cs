using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Domain.Tests.TeeTimeRequestAggregate;

public class TeeTimeRequestServiceTests
{
    private readonly StubTeeTimeRequestRepository teeTimeRequestRepo = new();
    private readonly StubWalkUpWaitlistRepository waitlistRepo = new();

    [Fact]
    public async Task CreateAsync_WhenWaitlistIsOpen_CreatesRequest()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);
        var teeTime = new TimeOnly(10, 0);

        var waitlist = await WalkUpWaitlist.OpenAsync(
            courseId, date, new StubShortCodeGenerator("1234"), this.waitlistRepo);
        this.waitlistRepo.SetOpen(waitlist);

        var service = new TeeTimeRequestService(this.teeTimeRequestRepo, this.waitlistRepo);

        var request = await service.CreateAsync(courseId, date, teeTime, 2);

        Assert.Equal(courseId, request.CourseId);
        Assert.Equal(date, request.Date);
        Assert.Equal(teeTime, request.TeeTime);
        Assert.Equal(2, request.GolfersNeeded);
    }

    [Fact]
    public async Task CreateAsync_WhenNoOpenWaitlist_Throws()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var service = new TeeTimeRequestService(this.teeTimeRequestRepo, this.waitlistRepo);

        await Assert.ThrowsAsync<WaitlistNotOpenForRequestsException>(
            () => service.CreateAsync(courseId, date, new TimeOnly(10, 0), 2));
    }

    [Fact]
    public async Task CreateAsync_WhenWaitlistClosed_Throws()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var waitlist = await WalkUpWaitlist.OpenAsync(
            courseId, date, new StubShortCodeGenerator("1234"), this.waitlistRepo);
        waitlist.Close();
        // Closed waitlist should NOT be returned by GetOpenByCourseDateAsync
        // so we don't set it as open

        var service = new TeeTimeRequestService(this.teeTimeRequestRepo, this.waitlistRepo);

        await Assert.ThrowsAsync<WaitlistNotOpenForRequestsException>(
            () => service.CreateAsync(courseId, date, new TimeOnly(10, 0), 2));
    }

    private class StubShortCodeGenerator(string code) : IShortCodeGenerator
    {
        public Task<string> GenerateAsync(DateOnly date) => Task.FromResult(code);
    }

    private class StubWalkUpWaitlistRepository : IWalkUpWaitlistRepository
    {
        private WalkUpWaitlist? open;

        public void SetOpen(WalkUpWaitlist waitlist) => this.open = waitlist;

        public Task<WalkUpWaitlist?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date)
        {
            if (this.open is not null
                && this.open.CourseId == courseId
                && this.open.Date == date)
            {
                return Task.FromResult<WalkUpWaitlist?>(this.open);
            }

            return Task.FromResult<WalkUpWaitlist?>(null);
        }

        public Task<WalkUpWaitlist?> GetByCourseDateAsync(Guid courseId, DateOnly date)
            => Task.FromResult<WalkUpWaitlist?>(null);

        public Task<WalkUpWaitlist?> GetByIdAsync(Guid id)
            => Task.FromResult<WalkUpWaitlist?>(null);

        public void Add(WalkUpWaitlist waitlist) { }

        public Task SaveAsync() => Task.CompletedTask;
    }

    private class StubTeeTimeRequestRepository : ITeeTimeRequestRepository
    {
        public Task<bool> ExistsAsync(Guid courseId, DateOnly date, TimeOnly teeTime)
            => Task.FromResult(false);

        public Task<TeeTimeRequest?> GetByIdAsync(Guid id)
            => Task.FromResult<TeeTimeRequest?>(null);

        public Task<List<TeeTimeRequest>> GetByCourseAndDateAsync(Guid courseId, DateOnly date)
            => Task.FromResult(new List<TeeTimeRequest>());

        public void Add(TeeTimeRequest request) { }

        public Task SaveAsync() => Task.CompletedTask;
    }
}

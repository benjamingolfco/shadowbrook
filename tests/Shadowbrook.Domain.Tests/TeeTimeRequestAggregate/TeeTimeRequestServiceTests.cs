using NSubstitute;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Domain.Tests.TeeTimeRequestAggregate;

public class TeeTimeRequestServiceTests
{
    private readonly ITeeTimeRequestRepository teeTimeRequestRepo = Substitute.For<ITeeTimeRequestRepository>();
    private readonly IWalkUpWaitlistRepository waitlistRepo = Substitute.For<IWalkUpWaitlistRepository>();
    private readonly IShortCodeGenerator shortCodeGenerator = Substitute.For<IShortCodeGenerator>();

    public TeeTimeRequestServiceTests()
    {
        shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("1234");
    }

    [Fact]
    public async Task CreateAsync_WhenWaitlistIsOpen_CreatesRequest()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);
        var teeTime = new TimeOnly(10, 0);

        var waitlist = await WalkUpWaitlist.OpenAsync(
            courseId, date, shortCodeGenerator, waitlistRepo);
        waitlistRepo.GetOpenByCourseDateAsync(courseId, date)
            .Returns(waitlist);

        var service = new TeeTimeRequestService(teeTimeRequestRepo, waitlistRepo);

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

        // NSubstitute returns null by default — no setup needed

        var service = new TeeTimeRequestService(teeTimeRequestRepo, waitlistRepo);

        await Assert.ThrowsAsync<WaitlistNotOpenForRequestsException>(
            () => service.CreateAsync(courseId, date, new TimeOnly(10, 0), 2));
    }

    [Fact]
    public async Task CreateAsync_WhenWaitlistClosed_Throws()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var waitlist = await WalkUpWaitlist.OpenAsync(
            courseId, date, shortCodeGenerator, waitlistRepo);
        waitlist.Close();
        // Closed waitlist should NOT be returned by GetOpenByCourseDateAsync
        // so we don't configure the repo to return it

        var service = new TeeTimeRequestService(teeTimeRequestRepo, waitlistRepo);

        await Assert.ThrowsAsync<WaitlistNotOpenForRequestsException>(
            () => service.CreateAsync(courseId, date, new TimeOnly(10, 0), 2));
    }
}

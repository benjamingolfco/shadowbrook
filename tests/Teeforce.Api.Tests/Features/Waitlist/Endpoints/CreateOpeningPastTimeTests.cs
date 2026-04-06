using Microsoft.AspNetCore.Http;
using NSubstitute;
using Teeforce.Api.Features.Waitlist.Endpoints;
using Teeforce.Api.Infrastructure;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeOpeningAggregate;

namespace Teeforce.Api.Tests.Features.Waitlist.Endpoints;

public class CreateOpeningPastTimeTests
{
    private readonly ITeeTimeOpeningRepository openingRepo = Substitute.For<ITeeTimeOpeningRepository>();
    private readonly ICourseContext courseContext = Substitute.For<ICourseContext>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    private static readonly DateOnly CourseToday = new(2026, 4, 4);
    // Course clock is 14:00 — minute-truncated
    private static readonly TimeOnly CourseNow = new(14, 0);

    public CreateOpeningPastTimeTests()
    {
        this.courseContext.Today.Returns(CourseToday);
        // Return 14:00:30 — the endpoint truncates to 14:00, so same-minute times are allowed
        this.courseContext.Now.Returns(new TimeOnly(14, 0, 30));
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);

        // Default: no existing opening so the duplicate check does not interfere
        this.openingRepo.GetActiveByCourseTeeTimeAsync(Arg.Any<Guid>(), Arg.Any<TeeTime>())
            .Returns((TeeTimeOpening?)null);
    }

    [Fact]
    public async Task CreateOpening_PastTimeToday_Returns422()
    {
        var courseId = Guid.NewGuid();
        // 09:00 is before course clock of 14:00
        var request = new CreateTeeTimeOpeningRequest(CourseToday.ToDateTime(new TimeOnly(9, 0)), 2);

        var result = await WalkUpWaitlistEndpoints.CreateOpening(
            courseId, request, this.openingRepo, this.courseContext, this.timeProvider);

        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.NotNull(statusCodeResult);
        Assert.Equal(422, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task CreateOpening_FutureTimeToday_DoesNotReturn422()
    {
        var courseId = Guid.NewGuid();
        // 15:00 is after course clock of 14:00
        var request = new CreateTeeTimeOpeningRequest(CourseToday.ToDateTime(new TimeOnly(15, 0)), 2);

        var result = await WalkUpWaitlistEndpoints.CreateOpening(
            courseId, request, this.openingRepo, this.courseContext, this.timeProvider);

        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.Null(statusCodeResult?.StatusCode == 422 ? statusCodeResult : null);
        // Should reach the duplicate check and return Created (no existing opening)
        Assert.NotEqual(422, statusCodeResult?.StatusCode);
    }

    [Fact]
    public async Task CreateOpening_PastDate_Returns422()
    {
        var courseId = Guid.NewGuid();
        // Yesterday at 10:00 is always in the past
        var yesterday = CourseToday.AddDays(-1);
        var request = new CreateTeeTimeOpeningRequest(yesterday.ToDateTime(new TimeOnly(10, 0)), 2);

        var result = await WalkUpWaitlistEndpoints.CreateOpening(
            courseId, request, this.openingRepo, this.courseContext, this.timeProvider);

        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.NotNull(statusCodeResult);
        Assert.Equal(422, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task CreateOpening_SameMinuteAsCourseClock_DoesNotReturn422()
    {
        var courseId = Guid.NewGuid();
        // 14:00:00 tee time when course clock is 14:00:30 — same minute, should be allowed
        var request = new CreateTeeTimeOpeningRequest(CourseToday.ToDateTime(CourseNow), 2);

        var result = await WalkUpWaitlistEndpoints.CreateOpening(
            courseId, request, this.openingRepo, this.courseContext, this.timeProvider);

        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.NotEqual(422, statusCodeResult?.StatusCode);
    }
}

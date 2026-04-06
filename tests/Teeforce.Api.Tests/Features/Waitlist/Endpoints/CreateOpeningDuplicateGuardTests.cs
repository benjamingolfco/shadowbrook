using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Teeforce.Api.Features.Waitlist.Endpoints;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeOpeningAggregate;

namespace Teeforce.Api.Tests.Features.Waitlist.Endpoints;

public class CreateOpeningDuplicateGuardTests
{
    private readonly ITeeTimeOpeningRepository openingRepo = Substitute.For<ITeeTimeOpeningRepository>();
    private readonly ICourseContext courseContext = Substitute.For<ICourseContext>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    // Future tee time used in all duplicate-guard tests — 2099-01-01 10:00 is always in the future
    private static readonly DateTime FutureTeeTime = new(2099, 1, 1, 10, 0, 0);

    public CreateOpeningDuplicateGuardTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);

        // Stub course context so the past-time guard always passes in these tests
        this.courseContext.Today.Returns(new DateOnly(2026, 4, 4));
        this.courseContext.Now.Returns(new TimeOnly(9, 0));
    }

    [Fact]
    public async Task CreateOpening_NoExisting_ReturnsCreated()
    {
        var courseId = Guid.NewGuid();
        var request = new CreateTeeTimeOpeningRequest(FutureTeeTime, 2);

        this.openingRepo.GetActiveByCourseTeeTimeAsync(
            courseId,
            Arg.Is<TeeTime>(t => t.Date == DateOnly.FromDateTime(FutureTeeTime) && t.Time == TimeOnly.FromDateTime(FutureTeeTime)))
            .Returns((TeeTimeOpening?)null);

        var result = await WalkUpWaitlistEndpoints.CreateOpening(courseId, request, this.openingRepo, this.courseContext, this.timeProvider);

        Assert.IsType<Created<WalkUpWaitlistOpeningResponse>>(result);
    }

    [Fact]
    public async Task CreateOpening_ExistingWith2Slots_ReturnsConflictWithIsFullFalse()
    {
        var courseId = Guid.NewGuid();
        var request = new CreateTeeTimeOpeningRequest(FutureTeeTime, 2);

        var existing = TeeTimeOpening.Create(
            courseId,
            DateOnly.FromDateTime(FutureTeeTime),
            TimeOnly.FromDateTime(FutureTeeTime),
            2,
            true,
            this.timeProvider);

        this.openingRepo.GetActiveByCourseTeeTimeAsync(
            courseId,
            Arg.Is<TeeTime>(t => t.Date == DateOnly.FromDateTime(FutureTeeTime) && t.Time == TimeOnly.FromDateTime(FutureTeeTime)))
            .Returns(existing);

        var result = await WalkUpWaitlistEndpoints.CreateOpening(courseId, request, this.openingRepo, this.courseContext, this.timeProvider);

        Assert.IsAssignableFrom<IResult>(result);
        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.NotNull(statusCodeResult);
        Assert.Equal(409, statusCodeResult.StatusCode);

        var valueResult = result as IValueHttpResult;
        Assert.NotNull(valueResult);
        var response = GetAnonymousObjectProperties(valueResult.Value!);

        Assert.Equal("An opening already exists for this time with 2 slot(s). Would you like to add more slots to it?", response["error"]);
        Assert.Equal(2, response["existingSlotsAvailable"]);
        Assert.Equal(2, response["existingSlotsRemaining"]);
        Assert.Equal(existing.Id, response["existingOpeningId"]);
        Assert.Equal(false, response["isFull"]);
    }

    [Fact]
    public async Task CreateOpening_ExistingWith4SlotsRemaining_ReturnsConflictWithIsFullFalse()
    {
        var courseId = Guid.NewGuid();
        var request = new CreateTeeTimeOpeningRequest(FutureTeeTime, 2);

        var existing = TeeTimeOpening.Create(
            courseId,
            DateOnly.FromDateTime(FutureTeeTime),
            TimeOnly.FromDateTime(FutureTeeTime),
            4,
            true,
            this.timeProvider);

        this.openingRepo.GetActiveByCourseTeeTimeAsync(
            courseId,
            Arg.Is<TeeTime>(t => t.Date == DateOnly.FromDateTime(FutureTeeTime) && t.Time == TimeOnly.FromDateTime(FutureTeeTime)))
            .Returns(existing);

        var result = await WalkUpWaitlistEndpoints.CreateOpening(courseId, request, this.openingRepo, this.courseContext, this.timeProvider);

        Assert.IsAssignableFrom<IResult>(result);
        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.NotNull(statusCodeResult);
        Assert.Equal(409, statusCodeResult.StatusCode);

        var valueResult = result as IValueHttpResult;
        Assert.NotNull(valueResult);
        var response = GetAnonymousObjectProperties(valueResult.Value!);

        Assert.Equal("An opening already exists for this time with 4 slot(s). Would you like to add more slots to it?", response["error"]);
        Assert.Equal(4, response["existingSlotsAvailable"]);
        Assert.Equal(4, response["existingSlotsRemaining"]);
        Assert.Equal(existing.Id, response["existingOpeningId"]);
        Assert.Equal(false, response["isFull"]);
    }

    [Fact]
    public async Task CreateOpening_ExistingFullyClaimedOpening_ReturnsConflictWithIsFullTrue()
    {
        var courseId = Guid.NewGuid();
        var request = new CreateTeeTimeOpeningRequest(FutureTeeTime, 2);

        var existing = TeeTimeOpening.Create(
            courseId,
            DateOnly.FromDateTime(FutureTeeTime),
            TimeOnly.FromDateTime(FutureTeeTime),
            2,
            true,
            this.timeProvider);

        // Claim all slots so SlotsRemaining == 0
        var claimResult = existing.TryClaim(Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider);
        Assert.True(claimResult.Success);
        Assert.Equal(0, existing.SlotsRemaining);

        this.openingRepo.GetActiveByCourseTeeTimeAsync(
            courseId,
            Arg.Is<TeeTime>(t => t.Date == DateOnly.FromDateTime(FutureTeeTime) && t.Time == TimeOnly.FromDateTime(FutureTeeTime)))
            .Returns(existing);

        var result = await WalkUpWaitlistEndpoints.CreateOpening(courseId, request, this.openingRepo, this.courseContext, this.timeProvider);

        Assert.IsAssignableFrom<IResult>(result);
        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.NotNull(statusCodeResult);
        Assert.Equal(409, statusCodeResult.StatusCode);

        var valueResult = result as IValueHttpResult;
        Assert.NotNull(valueResult);
        var response = GetAnonymousObjectProperties(valueResult.Value!);

        Assert.Equal("A tee time opening for this time already exists with 2 slots.", response["error"]);
        Assert.Equal(2, response["existingSlotsAvailable"]);
        Assert.Equal(0, response["existingSlotsRemaining"]);
        Assert.Equal(existing.Id, response["existingOpeningId"]);
        Assert.Equal(true, response["isFull"]);
    }

    [Fact]
    public async Task CreateOpening_ExistingWith3SlotsAnd1Remaining_ReturnsConflictWithCorrectRemainingCount()
    {
        var courseId = Guid.NewGuid();
        var request = new CreateTeeTimeOpeningRequest(FutureTeeTime, 2);

        var existing = TeeTimeOpening.Create(
            courseId,
            DateOnly.FromDateTime(FutureTeeTime),
            TimeOnly.FromDateTime(FutureTeeTime),
            3,
            true,
            this.timeProvider);

        // Simulate one slot claimed
        var claimResult = existing.TryClaim(Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider);
        Assert.True(claimResult.Success);
        Assert.Equal(1, existing.SlotsRemaining);

        this.openingRepo.GetActiveByCourseTeeTimeAsync(
            courseId,
            Arg.Is<TeeTime>(t => t.Date == DateOnly.FromDateTime(FutureTeeTime) && t.Time == TimeOnly.FromDateTime(FutureTeeTime)))
            .Returns(existing);

        var result = await WalkUpWaitlistEndpoints.CreateOpening(courseId, request, this.openingRepo, this.courseContext, this.timeProvider);

        Assert.IsAssignableFrom<IResult>(result);
        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.NotNull(statusCodeResult);
        Assert.Equal(409, statusCodeResult.StatusCode);

        var valueResult = result as IValueHttpResult;
        Assert.NotNull(valueResult);
        var response = GetAnonymousObjectProperties(valueResult.Value!);

        Assert.Equal(3, response["existingSlotsAvailable"]);
        Assert.Equal(1, response["existingSlotsRemaining"]);
        Assert.Equal(false, response["isFull"]);
    }

    private static Dictionary<string, object> GetAnonymousObjectProperties(object obj)
    {
        var properties = obj.GetType().GetProperties();
        return properties.ToDictionary(p => p.Name, p => p.GetValue(obj)!);
    }
}

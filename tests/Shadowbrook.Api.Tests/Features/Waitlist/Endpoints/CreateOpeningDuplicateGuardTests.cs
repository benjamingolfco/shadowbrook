using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Endpoints;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Api.Tests.Features.Waitlist.Endpoints;

public class CreateOpeningDuplicateGuardTests
{
    private readonly ITeeTimeOpeningRepository openingRepo = Substitute.For<ITeeTimeOpeningRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public CreateOpeningDuplicateGuardTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CreateOpening_NoExisting_ReturnsCreated()
    {
        var courseId = Guid.NewGuid();
        var teeTime = new DateTime(2026, 3, 31, 10, 0, 0);
        var request = new CreateTeeTimeOpeningRequest(teeTime, 2);

        this.openingRepo.GetActiveByCourseTeeTimeAsync(
            courseId,
            Arg.Is<TeeTime>(t => t.Date == DateOnly.FromDateTime(teeTime) && t.Time == TimeOnly.FromDateTime(teeTime)))
            .Returns((TeeTimeOpening?)null);

        var result = await WalkUpWaitlistEndpoints.CreateOpening(courseId, request, this.openingRepo, this.timeProvider);

        Assert.IsType<Created<WalkUpWaitlistOpeningResponse>>(result);
    }

    [Fact]
    public async Task CreateOpening_ExistingWith2Slots_ReturnsConflictWithIsFullFalse()
    {
        var courseId = Guid.NewGuid();
        var teeTime = new DateTime(2026, 3, 31, 10, 0, 0);
        var request = new CreateTeeTimeOpeningRequest(teeTime, 2);

        var existing = TeeTimeOpening.Create(
            courseId,
            DateOnly.FromDateTime(teeTime),
            TimeOnly.FromDateTime(teeTime),
            2,
            true,
            this.timeProvider);

        this.openingRepo.GetActiveByCourseTeeTimeAsync(
            courseId,
            Arg.Is<TeeTime>(t => t.Date == DateOnly.FromDateTime(teeTime) && t.Time == TimeOnly.FromDateTime(teeTime)))
            .Returns(existing);

        var result = await WalkUpWaitlistEndpoints.CreateOpening(courseId, request, this.openingRepo, this.timeProvider);

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
    public async Task CreateOpening_ExistingWith4Slots_ReturnsConflictWithIsFullTrue()
    {
        var courseId = Guid.NewGuid();
        var teeTime = new DateTime(2026, 3, 31, 10, 0, 0);
        var request = new CreateTeeTimeOpeningRequest(teeTime, 2);

        var existing = TeeTimeOpening.Create(
            courseId,
            DateOnly.FromDateTime(teeTime),
            TimeOnly.FromDateTime(teeTime),
            4,
            true,
            this.timeProvider);

        this.openingRepo.GetActiveByCourseTeeTimeAsync(
            courseId,
            Arg.Is<TeeTime>(t => t.Date == DateOnly.FromDateTime(teeTime) && t.Time == TimeOnly.FromDateTime(teeTime)))
            .Returns(existing);

        var result = await WalkUpWaitlistEndpoints.CreateOpening(courseId, request, this.openingRepo, this.timeProvider);

        Assert.IsAssignableFrom<IResult>(result);
        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.NotNull(statusCodeResult);
        Assert.Equal(409, statusCodeResult.StatusCode);

        var valueResult = result as IValueHttpResult;
        Assert.NotNull(valueResult);
        var response = GetAnonymousObjectProperties(valueResult.Value!);

        Assert.Equal("A tee time opening for this time already exists with 4 slots.", response["error"]);
        Assert.Equal(4, response["existingSlotsAvailable"]);
        Assert.Equal(4, response["existingSlotsRemaining"]);
        Assert.Equal(existing.Id, response["existingOpeningId"]);
        Assert.Equal(true, response["isFull"]);
    }

    [Fact]
    public async Task CreateOpening_ExistingWith3SlotsAnd1Remaining_ReturnsConflictWithCorrectRemainingCount()
    {
        var courseId = Guid.NewGuid();
        var teeTime = new DateTime(2026, 3, 31, 10, 0, 0);
        var request = new CreateTeeTimeOpeningRequest(teeTime, 2);

        var existing = TeeTimeOpening.Create(
            courseId,
            DateOnly.FromDateTime(teeTime),
            TimeOnly.FromDateTime(teeTime),
            3,
            true,
            this.timeProvider);

        // Simulate one slot claimed
        var claimResult = existing.TryClaim(Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider);
        Assert.True(claimResult.Success);
        Assert.Equal(1, existing.SlotsRemaining);

        this.openingRepo.GetActiveByCourseTeeTimeAsync(
            courseId,
            Arg.Is<TeeTime>(t => t.Date == DateOnly.FromDateTime(teeTime) && t.Time == TimeOnly.FromDateTime(teeTime)))
            .Returns(existing);

        var result = await WalkUpWaitlistEndpoints.CreateOpening(courseId, request, this.openingRepo, this.timeProvider);

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

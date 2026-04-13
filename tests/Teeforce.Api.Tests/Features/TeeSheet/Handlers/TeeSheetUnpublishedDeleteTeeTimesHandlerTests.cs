using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Teeforce.Api.Features.TeeSheet.Handlers;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeSheetAggregate.Events;
using Teeforce.Domain.TeeTimeAggregate;
using DomainScheduleSettings = Teeforce.Domain.TeeSheetAggregate.ScheduleSettings;
using DomainTeeSheet = Teeforce.Domain.TeeSheetAggregate.TeeSheet;

namespace Teeforce.Api.Tests.Features.TeeSheet.Handlers;

public class TeeSheetUnpublishedDeleteTeeTimesHandlerTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly ITeeTimeRepository teeTimeRepository = Substitute.For<ITeeTimeRepository>();

    public TeeSheetUnpublishedDeleteTeeTimesHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private TeeSheetUnpublished MakeEvent(Guid teeSheetId) =>
        new()
        {
            TeeSheetId = teeSheetId,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 1),
            UnpublishedAt = this.timeProvider.GetCurrentTimestamp(),
        };

    private TeeTime MakeTeeTime(Guid courseId, Guid bookingId)
    {
        var settings = new DomainScheduleSettings(new TimeOnly(7, 0), new TimeOnly(8, 0), 30, 4);
        var sheet = DomainTeeSheet.Draft(courseId, new DateOnly(2026, 6, 1), settings, this.timeProvider);
        sheet.Publish(this.timeProvider);
        var auth = sheet.AuthorizeBooking();
        return TeeTime.Claim(sheet.Intervals[0], courseId, sheet.Date, auth, bookingId, Guid.NewGuid(), 2, this.timeProvider);
    }

    [Fact]
    public async Task Handle_RemovesAllTeeTimes()
    {
        var teeSheetId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var teeTime1 = MakeTeeTime(courseId, Guid.CreateVersion7());
        var teeTime2 = MakeTeeTime(courseId, Guid.CreateVersion7());

        this.teeTimeRepository.GetByTeeSheetIdAsync(teeSheetId, Arg.Any<CancellationToken>())
            .Returns([teeTime1, teeTime2]);

        var handler = new TeeSheetUnpublishedDeleteTeeTimesHandler(
            this.teeTimeRepository, NullLogger<TeeSheetUnpublishedDeleteTeeTimesHandler>.Instance);
        await handler.Handle(MakeEvent(teeSheetId), CancellationToken.None);

        this.teeTimeRepository.Received(1).Remove(teeTime1);
        this.teeTimeRepository.Received(1).Remove(teeTime2);
    }

    [Fact]
    public async Task Handle_NoTeeTimes_DoesNothing()
    {
        var teeSheetId = Guid.NewGuid();
        this.teeTimeRepository.GetByTeeSheetIdAsync(teeSheetId, Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = new TeeSheetUnpublishedDeleteTeeTimesHandler(
            this.teeTimeRepository, NullLogger<TeeSheetUnpublishedDeleteTeeTimesHandler>.Instance);
        await handler.Handle(MakeEvent(teeSheetId), CancellationToken.None);

        this.teeTimeRepository.DidNotReceive().Remove(Arg.Any<TeeTime>());
    }
}

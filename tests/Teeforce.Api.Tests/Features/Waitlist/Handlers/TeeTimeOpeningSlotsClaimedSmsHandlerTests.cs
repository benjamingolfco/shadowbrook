using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Features.Waitlist.Handlers;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate.Events;

namespace Teeforce.Api.Tests.Features.Waitlist.Handlers;

public class TeeTimeOpeningSlotsClaimedSmsHandlerTests
{
    private readonly ICourseRepository courseRepo = Substitute.For<ICourseRepository>();
    private readonly INotificationService notificationService = Substitute.For<INotificationService>();
    private readonly ILogger logger = Substitute.For<ILogger>();

    private static TeeTimeOpeningSlotsClaimed BuildEvent(
        Guid? golferId = null,
        Guid? courseId = null)
    {
        return new TeeTimeOpeningSlotsClaimed
        {
            OpeningId = Guid.NewGuid(),
            BookingId = Guid.CreateVersion7(),
            GolferId = golferId ?? Guid.NewGuid(),
            CourseId = courseId ?? Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 15),
            TeeTime = new TimeOnly(9, 30),
            GroupSize = 2
        };
    }

    [Fact]
    public async Task Handle_CourseNotFound_NoNotificationAndLogsWarning()
    {
        var evt = BuildEvent();
        this.courseRepo.GetByIdAsync(evt.CourseId).Returns((Course?)null);

        await TeeTimeOpeningSlotsClaimedSmsHandler.Handle(evt, this.courseRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<WalkupConfirmation>(), Arg.Any<CancellationToken>());
        this.logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_Success_SendsNotificationToGolferWithConfirmationContent()
    {
        var golferId = Guid.CreateVersion7();
        var course = Course.Create(Guid.NewGuid(), "Teeforce Golf Club", "America/Chicago");
        var evt = BuildEvent(golferId: golferId, courseId: course.Id);

        this.courseRepo.GetByIdAsync(course.Id).Returns(course);

        await TeeTimeOpeningSlotsClaimedSmsHandler.Handle(evt, this.courseRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.Received(1).Send(
            golferId,
            Arg.Is<WalkupConfirmation>(n => n.CourseName == "Teeforce Golf Club"),
            Arg.Any<CancellationToken>());
    }
}

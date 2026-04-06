using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Teeforce.Api.Features.Waitlist.Handlers;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.CourseWaitlistAggregate;
using Teeforce.Domain.CourseWaitlistAggregate.Events;

namespace Teeforce.Api.Tests.Features.Waitlist.Handlers;

public class GolferJoinedWaitlistNotificationHandlerTests
{
    private readonly ICourseWaitlistRepository courseWaitlistRepository = Substitute.For<ICourseWaitlistRepository>();
    private readonly ICourseRepository courseRepository = Substitute.For<ICourseRepository>();
    private readonly INotificationService notificationService = Substitute.For<INotificationService>();

    private static GolferJoinedWaitlist BuildEvent(Guid waitlistId, Guid golferId) =>
        new()
        {
            GolferWaitlistEntryId = Guid.NewGuid(),
            CourseWaitlistId = waitlistId,
            GolferId = golferId
        };

    [Fact]
    public async Task Handle_Success_SendsNotificationMentioningCourseName()
    {
        var golferId = Guid.CreateVersion7();
        var courseId = Guid.CreateVersion7();
        var waitlistId = Guid.CreateVersion7();

        var course = Course.Create(courseId, "Riverside Links", "America/Chicago");

        var waitlistRepo = Substitute.For<ICourseWaitlistRepository>();
        var timeProvider = Substitute.For<ITimeProvider>();
        timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
        var shortCodeGenerator = Substitute.For<IShortCodeGenerator>();
        shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("XYZ99");
        var waitlist = await WalkUpWaitlist.OpenAsync(
            courseId,
            new DateOnly(2026, 7, 4),
            shortCodeGenerator,
            waitlistRepo,
            timeProvider);
        waitlist.ClearDomainEvents();

        this.courseWaitlistRepository.GetByIdAsync(waitlist.Id).Returns(waitlist);
        this.courseRepository.GetByIdAsync(courseId).Returns(course);

        var evt = BuildEvent(waitlist.Id, golferId);

        await GolferJoinedWaitlistNotificationHandler.Handle(
            evt, this.notificationService, this.courseWaitlistRepository, this.courseRepository, CancellationToken.None);

        await this.notificationService.Received(1).Send(
            golferId,
            Arg.Is<WaitlistJoined>(n => n.CourseName == "Riverside Links"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CourseWaitlistNotFound_ThrowsEntityNotFoundException()
    {
        var waitlistId = Guid.NewGuid();
        var evt = BuildEvent(waitlistId, Guid.NewGuid());

        this.courseWaitlistRepository.GetByIdAsync(waitlistId).Returns((CourseWaitlist?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            GolferJoinedWaitlistNotificationHandler.Handle(
                evt, this.notificationService, this.courseWaitlistRepository, this.courseRepository, CancellationToken.None));

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<WaitlistJoined>(), Arg.Any<CancellationToken>());
    }
}

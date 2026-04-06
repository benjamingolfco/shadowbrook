using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Teeforce.Api.Features.Waitlist.Handlers;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.CourseWaitlistAggregate;
using Teeforce.Domain.CourseWaitlistAggregate.Events;

namespace Teeforce.Api.Tests.Features.Waitlist.Handlers;

public class GolferJoinedWaitlistSmsHandlerTests : IDisposable
{
    private readonly ApplicationDbContext db;
    private readonly INotificationService notificationService = Substitute.For<INotificationService>();

    public GolferJoinedWaitlistSmsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;

        this.db = new ApplicationDbContext(options, userContext: null);
    }

    public void Dispose() => this.db.Dispose();

    private static GolferJoinedWaitlist BuildEvent(Guid waitlistId, Guid golferId) =>
        new()
        {
            GolferWaitlistEntryId = Guid.NewGuid(),
            CourseWaitlistId = waitlistId,
            GolferId = golferId
        };

    private static async Task<(Course course, WalkUpWaitlist waitlist)> SeedCourseAndWaitlistAsync(
        ApplicationDbContext db,
        Guid? courseId = null)
    {
        var course = Course.Create(courseId ?? Guid.NewGuid(), "Riverside Links", "America/Chicago");
        var timeProvider = Substitute.For<ITimeProvider>();
        timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);

        var shortCodeGenerator = Substitute.For<IShortCodeGenerator>();
        shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("XYZ99");

        var waitlistRepo = Substitute.For<ICourseWaitlistRepository>();
        var waitlist = await WalkUpWaitlist.OpenAsync(
            course.Id,
            new DateOnly(2026, 7, 4),
            shortCodeGenerator,
            waitlistRepo,
            timeProvider);

        waitlist.ClearDomainEvents();

        db.Courses.Add(course);
        db.CourseWaitlists.Add(waitlist);
        await db.SaveChangesAsync();

        return (course, waitlist);
    }

    [Fact]
    public async Task Handle_Success_SendsNotificationMentioningCourseName()
    {
        var golferId = Guid.CreateVersion7();
        var (course, waitlist) = await SeedCourseAndWaitlistAsync(this.db);
        var evt = BuildEvent(waitlist.Id, golferId);

        await GolferJoinedWaitlistSmsHandler.Handle(evt, this.notificationService, this.db, CancellationToken.None);

        await this.notificationService.Received(1).Send(
            golferId,
            Arg.Is<GolferJoinedWaitlistNotification>(n => n.CourseName == "Riverside Links"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CourseWaitlistNotFound_ThrowsInvalidOperationException()
    {
        var evt = BuildEvent(Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GolferJoinedWaitlistSmsHandler.Handle(evt, this.notificationService, this.db, CancellationToken.None));

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<GolferJoinedWaitlistNotification>(), Arg.Any<CancellationToken>());
    }
}

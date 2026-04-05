using Microsoft.Extensions.Logging;
using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

namespace Shadowbrook.Api.Tests.Features.Waitlist.Handlers;

public class TeeTimeOpeningSlotsClaimedSmsHandlerTests
{
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly ICourseRepository courseRepo = Substitute.For<ICourseRepository>();
    private readonly ITextMessageService sms = Substitute.For<ITextMessageService>();
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
    public async Task Handle_GolferNotFound_NoSmsAndLogsWarning()
    {
        var evt = BuildEvent();
        this.golferRepo.GetByIdAsync(evt.GolferId).Returns((Golfer?)null);

        await TeeTimeOpeningSlotsClaimedSmsHandler.Handle(evt, this.golferRepo, this.courseRepo, this.sms, this.logger, CancellationToken.None);

        await this.sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        this.logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_CourseNotFound_NoSmsAndLogsWarning()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var evt = BuildEvent(golferId: golfer.Id);

        this.golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);
        this.courseRepo.GetByIdAsync(evt.CourseId).Returns((Course?)null);

        await TeeTimeOpeningSlotsClaimedSmsHandler.Handle(evt, this.golferRepo, this.courseRepo, this.sms, this.logger, CancellationToken.None);

        await this.sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        this.logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_Success_SendsSmsToGolferPhoneWithConfirmationContent()
    {
        var golfer = Golfer.Create("+15559876543", "Bob", "Green");
        var course = Course.Create(Guid.NewGuid(), "Shadowbrook Golf Club", "America/Chicago");
        var evt = BuildEvent(golferId: golfer.Id, courseId: course.Id);

        this.golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);
        this.courseRepo.GetByIdAsync(course.Id).Returns(course);

        await TeeTimeOpeningSlotsClaimedSmsHandler.Handle(evt, this.golferRepo, this.courseRepo, this.sms, this.logger, CancellationToken.None);

        await this.sms.Received(1).SendAsync(
            "+15559876543",
            Arg.Is<string>(m => m.Contains("Shadowbrook Golf Club") && m.Contains("confirmed")),
            Arg.Any<CancellationToken>());
    }
}

using Microsoft.Extensions.Logging;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public record TeeTimeOpeningSlotsClaimedNotification(string CourseName, DateOnly Date, TimeOnly TeeTime) : INotification;

public class TeeTimeOpeningSlotsClaimedNotificationSmsFormatter : SmsFormatter<TeeTimeOpeningSlotsClaimedNotification>
{
    protected override string FormatMessage(TeeTimeOpeningSlotsClaimedNotification n) =>
        $"Your tee time at {n.CourseName} on {n.Date:MMMM d} at {n.TeeTime:h:mm tt} is confirmed. See you on the course!";
}

public static class TeeTimeOpeningSlotsClaimedSmsHandler
{
    public static async Task Handle(
        TeeTimeOpeningSlotsClaimed evt,
        ICourseRepository courseRepository,
        INotificationService notificationService,
        ILogger logger,
        CancellationToken ct)
    {
        var course = await courseRepository.GetByIdAsync(evt.CourseId);

        if (course is null)
        {
            logger.LogWarning("Course {CourseId} not found for TeeTimeOpeningSlotsClaimed event {EventId}, skipping SMS", evt.CourseId, evt.EventId);
            return;
        }

        await notificationService.Send(evt.GolferId, new TeeTimeOpeningSlotsClaimedNotification(course.Name, evt.Date, evt.TeeTime), ct);
    }
}

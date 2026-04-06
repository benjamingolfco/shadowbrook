using Microsoft.Extensions.Logging;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

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

        var message = $"Your tee time at {course.Name} on {evt.Date:MMMM d} at {evt.TeeTime:h:mm tt} is confirmed. See you on the course!";
        await notificationService.Send(evt.GolferId, message, ct);
    }
}

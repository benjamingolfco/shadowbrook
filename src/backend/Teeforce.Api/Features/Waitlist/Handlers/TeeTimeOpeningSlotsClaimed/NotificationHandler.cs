using Microsoft.Extensions.Logging;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public static class TeeTimeOpeningSlotsClaimedNotificationHandler
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
            logger.LogWarning("Course {CourseId} not found for TeeTimeOpeningSlotsClaimed event {EventId}, skipping notification", evt.CourseId, evt.EventId);
            return;
        }

        await notificationService.Send(evt.GolferId, new WalkupConfirmation(course.Name, evt.Date, evt.TeeTime), ct);
    }
}

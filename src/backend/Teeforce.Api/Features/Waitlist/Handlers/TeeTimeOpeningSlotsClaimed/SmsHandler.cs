using Microsoft.Extensions.Logging;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class TeeTimeOpeningSlotsClaimedSmsHandler
{
    public static async Task Handle(
        TeeTimeOpeningSlotsClaimed evt,
        IGolferRepository golferRepository,
        ICourseRepository courseRepository,
        ITextMessageService textMessageService,
        ILogger logger,
        CancellationToken ct)
    {
        var golfer = await golferRepository.GetByIdAsync(evt.GolferId);

        if (golfer is null)
        {
            logger.LogWarning("Golfer {GolferId} not found for TeeTimeOpeningSlotsClaimed event {EventId}, skipping SMS", evt.GolferId, evt.EventId);
            return;
        }

        var course = await courseRepository.GetByIdAsync(evt.CourseId);

        if (course is null)
        {
            logger.LogWarning("Course {CourseId} not found for TeeTimeOpeningSlotsClaimed event {EventId}, skipping SMS", evt.CourseId, evt.EventId);
            return;
        }

        var message = $"Your tee time at {course.Name} on {evt.Date:MMMM d} at {evt.TeeTime:h:mm tt} is confirmed. See you on the course!";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}

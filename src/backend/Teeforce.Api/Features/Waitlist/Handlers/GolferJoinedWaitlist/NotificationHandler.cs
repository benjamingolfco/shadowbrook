using Teeforce.Api.Infrastructure.Notifications;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.CourseWaitlistAggregate;
using Teeforce.Domain.CourseWaitlistAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public static class GolferJoinedWaitlistNotificationHandler
{
    public static async Task Handle(
        GolferJoinedWaitlist domainEvent,
        INotificationService notificationService,
        ICourseWaitlistRepository courseWaitlistRepository,
        ICourseRepository courseRepository,
        CancellationToken ct)
    {
        var waitlist = await courseWaitlistRepository.GetRequiredByIdAsync(domainEvent.CourseWaitlistId);
        var course = await courseRepository.GetRequiredByIdAsync(waitlist.CourseId);

        await notificationService.Send(domainEvent.GolferId, new WaitlistJoined(course.Name), ct);
    }
}

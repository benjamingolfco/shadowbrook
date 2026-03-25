using Shadowbrook.Domain.CourseWaitlistAggregate.Events;
using Shadowbrook.Domain.CourseWaitlistAggregate.Exceptions;

namespace Shadowbrook.Domain.CourseWaitlistAggregate;

public class WalkUpWaitlist : CourseWaitlist
{
    public string ShortCode { get; private set; } = string.Empty;
    public WaitlistStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    private WalkUpWaitlist() { } // EF

    public static async Task<WalkUpWaitlist> OpenAsync(
        Guid courseId, DateOnly date, IShortCodeGenerator shortCodeGenerator,
        ICourseWaitlistRepository repository)
    {
        var existing = await repository.GetByCourseDateAsync(courseId, date);
        if (existing is not null)
        {
            throw new WaitlistAlreadyExistsException(existing.Status);
        }

        var shortCode = await shortCodeGenerator.GenerateAsync(date);
        var now = DateTimeOffset.UtcNow;
        var waitlist = new WalkUpWaitlist
        {
            Id = Guid.CreateVersion7(),
            CourseId = courseId,
            Date = date,
            ShortCode = shortCode,
            Status = WaitlistStatus.Open,
            OpenedAt = now,
            CreatedAt = now
        };

        waitlist.AddDomainEvent(new WalkUpWaitlistOpened { CourseWaitlistId = waitlist.Id });

        return waitlist;
    }

    public void Close()
    {
        if (Status != WaitlistStatus.Open)
        {
            throw new WaitlistNotOpenException();
        }

        Status = WaitlistStatus.Closed;
        ClosedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new WalkUpWaitlistClosed { CourseWaitlistId = Id });
    }

    public void Reopen()
    {
        if (Status != WaitlistStatus.Closed)
        {
            throw new WaitlistNotClosedException();
        }

        Status = WaitlistStatus.Open;
        ClosedAt = null;

        AddDomainEvent(new WalkUpWaitlistReopened { CourseWaitlistId = Id });
    }
}

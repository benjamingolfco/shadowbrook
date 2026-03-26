using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.CourseWaitlistAggregate.Exceptions;

public class WaitlistAlreadyExistsException(WaitlistStatus existingStatus)
    : DomainException(existingStatus == WaitlistStatus.Open
        ? "Walk-up waitlist is already open for today."
        : "Walk-up waitlist was already used today.")
{
    public WaitlistStatus ExistingStatus { get; } = existingStatus;
}

using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.CourseWaitlistAggregate.Exceptions;

public class WaitlistNotClosedException()
    : DomainException("Walk-up waitlist is not closed.");

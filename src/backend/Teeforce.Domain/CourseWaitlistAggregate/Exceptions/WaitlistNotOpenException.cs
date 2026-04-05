using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.CourseWaitlistAggregate.Exceptions;

public class WaitlistNotOpenException()
    : DomainException("Walk-up waitlist is not open.");

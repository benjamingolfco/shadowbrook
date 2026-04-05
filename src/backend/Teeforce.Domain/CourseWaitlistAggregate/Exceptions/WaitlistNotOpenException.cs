using Teeforce.Domain.Common;

namespace Teeforce.Domain.CourseWaitlistAggregate.Exceptions;

public class WaitlistNotOpenException()
    : DomainException("Walk-up waitlist is not open.");

using Teeforce.Domain.Common;

namespace Teeforce.Domain.CourseWaitlistAggregate.Exceptions;

public class WaitlistNotClosedException()
    : DomainException("Walk-up waitlist is not closed.");

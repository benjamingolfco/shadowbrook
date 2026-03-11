using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlistAggregate.Exceptions;

public class WaitlistNotOpenException()
    : DomainException("Walk-up waitlist is not open.");

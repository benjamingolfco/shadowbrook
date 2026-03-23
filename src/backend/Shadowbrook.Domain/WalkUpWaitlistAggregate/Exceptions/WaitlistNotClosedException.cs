using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlistAggregate.Exceptions;

public class WaitlistNotClosedException()
    : DomainException("Walk-up waitlist is not closed.");

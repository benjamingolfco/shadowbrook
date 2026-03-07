using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

public class WaitlistNotOpenException()
    : DomainException("Walk-up waitlist is not open.");

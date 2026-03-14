using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlistAggregate.Exceptions;

public class GolferAlreadyOnWaitlistException(string phone)
    : DomainException($"A golfer with phone {phone} is already on this waitlist.");

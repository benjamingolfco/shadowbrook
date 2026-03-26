using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.CourseWaitlistAggregate.Exceptions;

public class GolferAlreadyOnWaitlistException(string phone)
    : DomainException($"A golfer with phone {phone} is already on this waitlist.");

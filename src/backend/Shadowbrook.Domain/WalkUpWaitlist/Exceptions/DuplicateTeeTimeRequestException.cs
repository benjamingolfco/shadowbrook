using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

public class DuplicateTeeTimeRequestException(TimeOnly teeTime)
    : DomainException($"An active waitlist request already exists for {teeTime:HH:mm}.");

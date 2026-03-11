using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;

public class DuplicateTeeTimeRequestException(TimeOnly teeTime)
    : DomainException($"An active tee time request already exists for {teeTime:HH:mm}.");

using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TeeTimeOpeningAggregate.Exceptions;

public class InvalidSlotsAvailableException()
    : DomainException("Slots available must be at least 1.");

using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeOpeningAggregate.Exceptions;

public class InvalidSlotsAvailableException()
    : DomainException("Slots available must be at least 1.");

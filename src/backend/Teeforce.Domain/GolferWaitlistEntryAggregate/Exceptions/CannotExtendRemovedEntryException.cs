using Teeforce.Domain.Common;

namespace Teeforce.Domain.GolferWaitlistEntryAggregate.Exceptions;

public class CannotExtendRemovedEntryException()
    : DomainException("Cannot extend window on a removed entry.");

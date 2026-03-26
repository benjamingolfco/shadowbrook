using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.GolferWaitlistEntryAggregate.Exceptions;

public class CannotExtendRemovedEntryException()
    : DomainException("Cannot extend window on a removed entry.");

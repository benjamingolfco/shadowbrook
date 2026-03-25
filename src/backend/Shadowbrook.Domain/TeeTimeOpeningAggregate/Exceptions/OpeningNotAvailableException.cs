using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TeeTimeOpeningAggregate.Exceptions;

public class OpeningNotAvailableException(Guid openingId)
    : DomainException($"Tee time opening {openingId} is not available.");

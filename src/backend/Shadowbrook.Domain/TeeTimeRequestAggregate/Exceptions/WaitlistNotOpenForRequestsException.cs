using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;

public class WaitlistNotOpenForRequestsException(DateOnly date)
    : DomainException($"No open walk-up waitlist found for {date:yyyy-MM-dd}. Tee time requests require an open waitlist.");

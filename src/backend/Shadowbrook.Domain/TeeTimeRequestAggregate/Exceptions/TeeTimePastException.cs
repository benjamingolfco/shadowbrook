using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;

public class TeeTimePastException()
    : DomainException("Tee time must be in the future.");

using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

public class TeeTimeRequestNotPendingException(RequestStatus currentStatus)
    : DomainException($"Tee time request cannot be fulfilled because its status is '{currentStatus}'.");

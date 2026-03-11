using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

public class WaitlistOfferNotPendingException(OfferStatus currentStatus)
    : DomainException($"Waitlist offer cannot be updated because its status is '{currentStatus}'.");

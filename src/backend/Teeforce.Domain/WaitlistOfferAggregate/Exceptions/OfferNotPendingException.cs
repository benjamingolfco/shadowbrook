using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

public class OfferNotPendingException()
    : DomainException("This offer is no longer available.");

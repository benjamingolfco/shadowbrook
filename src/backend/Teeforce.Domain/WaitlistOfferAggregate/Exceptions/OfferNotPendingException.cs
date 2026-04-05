using Teeforce.Domain.Common;

namespace Teeforce.Domain.WaitlistOfferAggregate.Exceptions;

public class OfferNotPendingException()
    : DomainException("This offer is no longer available.");

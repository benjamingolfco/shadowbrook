using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

public class OfferAlreadyNotifiedException()
    : DomainException("Offer has already been marked as notified.");

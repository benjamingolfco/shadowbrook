using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

public class OfferSlotsFilledException()
    : DomainException("All slots have been filled.");

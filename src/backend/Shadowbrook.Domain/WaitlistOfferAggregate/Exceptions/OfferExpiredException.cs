using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

public class OfferExpiredException()
    : DomainException("This offer has expired.");

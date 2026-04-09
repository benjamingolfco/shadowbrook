using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeOfferAggregate.Exceptions;

public class OfferNotPendingException()
    : DomainException("This offer is no longer available.");

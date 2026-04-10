using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeOfferAggregate.Exceptions;

public class OfferAlreadyNotifiedException()
    : DomainException("Offer has already been marked as notified.");

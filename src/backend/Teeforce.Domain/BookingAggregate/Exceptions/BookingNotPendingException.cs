using Teeforce.Domain.Common;

namespace Teeforce.Domain.BookingAggregate.Exceptions;

public class BookingNotPendingException(Guid bookingId, BookingStatus currentStatus)
    : DomainException($"Cannot modify booking {bookingId} — status is {currentStatus}, expected Pending.");

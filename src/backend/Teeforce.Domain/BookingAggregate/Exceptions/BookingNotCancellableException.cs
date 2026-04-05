using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.BookingAggregate.Exceptions;

public class BookingNotCancellableException(Guid bookingId, BookingStatus currentStatus)
    : DomainException($"Cannot cancel booking {bookingId} — status is {currentStatus}, only Pending or Confirmed bookings can be cancelled.");

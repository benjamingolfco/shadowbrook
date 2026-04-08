using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class BookingAuthorizationMismatchException(Guid teeTimeId, Guid tokenSheetId, Guid actualSheetId)
    : DomainException($"Booking authorization for sheet {tokenSheetId} does not match tee time {teeTimeId} (sheet {actualSheetId}).")
{
    public Guid TeeTimeId { get; } = teeTimeId;
    public Guid TokenSheetId { get; } = tokenSheetId;
    public Guid ActualSheetId { get; } = actualSheetId;
}

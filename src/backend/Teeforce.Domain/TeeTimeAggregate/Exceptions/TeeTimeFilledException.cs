using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class TeeTimeFilledException(Guid teeTimeId)
    : DomainException($"Tee time {teeTimeId} is fully booked.")
{
    public Guid TeeTimeId { get; } = teeTimeId;
}

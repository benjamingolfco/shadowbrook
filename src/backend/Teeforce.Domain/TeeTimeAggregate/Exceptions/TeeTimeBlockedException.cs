using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class TeeTimeBlockedException(Guid teeTimeId)
    : DomainException($"Tee time {teeTimeId} is blocked.")
{
    public Guid TeeTimeId { get; } = teeTimeId;
}

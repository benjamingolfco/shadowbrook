using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class InsufficientCapacityException(Guid teeTimeId, int requested, int remaining)
    : DomainException($"Tee time {teeTimeId} has {remaining} slots remaining; {requested} requested.")
{
    public Guid TeeTimeId { get; } = teeTimeId;
    public int Requested { get; } = requested;
    public int Remaining { get; } = remaining;
}

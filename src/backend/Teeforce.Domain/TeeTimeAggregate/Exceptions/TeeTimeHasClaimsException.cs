using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class TeeTimeHasClaimsException(Guid teeTimeId, int claimCount)
    : DomainException($"Tee time {teeTimeId} cannot be blocked: it has {claimCount} active claim(s).")
{
    public Guid TeeTimeId { get; } = teeTimeId;
    public int ClaimCount { get; } = claimCount;
}

using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate.Exceptions;

public class TeeSheetNotPublishedException(Guid teeSheetId)
    : DomainException($"Tee sheet {teeSheetId} is not published.")
{
    public Guid TeeSheetId { get; } = teeSheetId;
}

namespace Shadowbrook.Domain.TeeTimeOpeningAggregate;

public record ClaimResult(bool Success, string? Reason = null)
{
    public static ClaimResult Claimed() => new(true);
    public static ClaimResult Rejected(string reason) => new(false, reason);
}

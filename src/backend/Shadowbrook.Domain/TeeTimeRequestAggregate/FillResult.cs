namespace Shadowbrook.Domain.TeeTimeRequestAggregate;

public record FillResult(bool Success, string? RejectionReason = null);

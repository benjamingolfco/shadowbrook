namespace Teeforce.Domain.Common;

public interface INotificationService
{
    Task Send(Guid appUserId, string message, CancellationToken ct = default);
}

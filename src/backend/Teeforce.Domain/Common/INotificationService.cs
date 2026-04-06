namespace Teeforce.Domain.Common;

public interface INotificationService
{
    Task Send<T>(Guid appUserId, T notification, CancellationToken ct = default) where T : INotification;
}

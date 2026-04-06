namespace Teeforce.Domain.Common;

public interface INotificationService
{
    Task Send<T>(Guid appUserId, T notification, CancellationToken ct = default) where T : INotification;

    // TODO(Task 7): Remove after all callers migrate to Send<T>
    Task Send(Guid appUserId, string message, CancellationToken ct = default);
}

namespace Teeforce.Api.Infrastructure.Notifications;

public interface INotificationService
{
    Task Send<T>(Guid appUserId, T notification, CancellationToken ct = default) where T : INotification;
}

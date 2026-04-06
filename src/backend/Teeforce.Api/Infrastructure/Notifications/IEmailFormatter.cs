namespace Teeforce.Api.Infrastructure.Notifications;

public interface IEmailFormatter<in T> where T : INotification
{
    (string Subject, string Body) Format(T notification);
}

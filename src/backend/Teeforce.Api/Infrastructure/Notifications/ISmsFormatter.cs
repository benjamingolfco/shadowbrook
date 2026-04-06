namespace Teeforce.Api.Infrastructure.Notifications;

public interface ISmsFormatter<in T> where T : INotification
{
    string Format(T notification);
}

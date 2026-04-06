namespace Teeforce.Api.Infrastructure.Notifications;

public class DefaultEmailFormatter<T>(
    ISmsFormatter<T> smsFormatter,
    ILogger<DefaultEmailFormatter<T>> logger) : IEmailFormatter<T>
    where T : INotification
{
    public (string Subject, string Body) Format(T notification)
    {
        logger.LogInformation(
            "No dedicated email formatter for {NotificationType}, using SMS text as email body",
            typeof(T).Name);

        var body = smsFormatter.Format(notification);
        return ("Teeforce Notification", body);
    }
}

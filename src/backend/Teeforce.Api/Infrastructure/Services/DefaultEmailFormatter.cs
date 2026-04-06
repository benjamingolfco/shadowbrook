using Microsoft.Extensions.DependencyInjection;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

public class DefaultEmailFormatter(IServiceProvider serviceProvider, ILogger<DefaultEmailFormatter> logger) : IEmailFormatter
{
    public (string Subject, string Body) Format(INotification notification)
    {
        var smsFormatter = serviceProvider.GetRequiredKeyedService<ISmsFormatter>(notification.GetType());

        logger.LogInformation(
            "No dedicated email formatter for {NotificationType}, using SMS text as email body",
            notification.GetType().Name);

        var body = smsFormatter.Format(notification);
        return ("Teeforce Notification", body);
    }
}

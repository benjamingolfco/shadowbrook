using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.Common;
using Wolverine;

namespace Teeforce.Api.Infrastructure.Services;

public class NotificationService(
    ApplicationDbContext db,
    IMessageBus messageBus,
    IServiceProvider serviceProvider,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task Send<T>(Guid appUserId, T notification, CancellationToken ct = default) where T : INotification
    {
        // 1. Resolve contact info — AppUser first, then Golfer fallback
        var appUser = await db.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.Id == appUserId)
            .Select(u => new { u.Phone, u.Email })
            .FirstOrDefaultAsync(ct);

        var phone = appUser?.Phone;
        var email = appUser?.Email;

        // Golfer fallback for phone if AppUser has no phone
        if (string.IsNullOrEmpty(phone))
        {
            phone = await db.Golfers
                .IgnoreQueryFilters()
                .Where(g => g.Id == appUserId)
                .Select(g => g.Phone)
                .FirstOrDefaultAsync(ct);
        }

        // 2. Route and format
        if (!string.IsNullOrEmpty(phone))
        {
            var smsFormatter = serviceProvider.GetRequiredKeyedService<ISmsFormatter>(typeof(T));
            var message = smsFormatter.Format(notification);
            await messageBus.PublishAsync(new DeliverSms(phone, message));
            return;
        }

        if (!string.IsNullOrEmpty(email))
        {
            var emailFormatter = serviceProvider.GetKeyedService<IEmailFormatter>(typeof(T))
                ?? serviceProvider.GetRequiredService<DefaultEmailFormatter>();
            var (subject, body) = emailFormatter.Format(notification);
            await messageBus.PublishAsync(new DeliverEmail(email, subject, body));
            return;
        }

        logger.LogWarning("No contact info found for user {AppUserId}, skipping notification", appUserId);
    }
}

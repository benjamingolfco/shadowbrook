using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

public class NotificationService(
    ISmsSender smsSender,
    IEmailSender emailSender,
    ApplicationDbContext db,
    ILogger<NotificationService> logger) : INotificationService
{
    public Task Send<T>(Guid appUserId, T notification, CancellationToken ct = default) where T : INotification
        => Task.CompletedTask;

    public async Task Send(Guid appUserId, string message, CancellationToken ct = default)
    {
        var phone = await db.Golfers
            .IgnoreQueryFilters()
            .Where(g => g.Id == appUserId)
            .Select(g => g.Phone)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrEmpty(phone))
        {
            await smsSender.Send(phone, message, ct);
            return;
        }

        var email = await db.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.Id == appUserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrEmpty(email))
        {
            await emailSender.Send(email, "Teeforce Notification", message, ct);
            return;
        }

        logger.LogWarning("No contact info found for user {AppUserId}, skipping notification", appUserId);
    }
}

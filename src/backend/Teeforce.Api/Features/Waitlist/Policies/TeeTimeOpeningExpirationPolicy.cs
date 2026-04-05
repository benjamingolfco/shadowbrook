using Microsoft.Extensions.Logging;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Shadowbrook.Api.Features.Waitlist.Policies;

public class TeeTimeOpeningExpirationPolicy : Saga
{
    public Guid Id { get; set; }

    public static async Task<(TeeTimeOpeningExpirationPolicy, TeeTimeOpeningExpirationTimeout)> Start(
        TeeTimeOpeningCreated evt,
        ICourseTimeZoneProvider courseTimeZoneProvider,
        TimeProvider timeProvider)
    {
        var policy = new TeeTimeOpeningExpirationPolicy { Id = evt.OpeningId };

        var timeZoneId = await courseTimeZoneProvider.GetTimeZoneIdAsync(evt.CourseId);
        var teeTimeUtc = CourseTime.ToUtc(evt.Date, evt.TeeTime, timeZoneId);
        var delay = teeTimeUtc - timeProvider.GetUtcNow();
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        var timeout = new TeeTimeOpeningExpirationTimeout(policy.Id, delay);
        return (policy, timeout);
    }

    public ExpireTeeTimeOpening Handle(TeeTimeOpeningExpirationTimeout timeout)
    {
        MarkCompleted();
        return new ExpireTeeTimeOpening(Id);
    }

    public void Handle(
        [SagaIdentityFrom("OpeningId")] TeeTimeOpeningFilled evt) => MarkCompleted();

    public void Handle(
        [SagaIdentityFrom("OpeningId")] TeeTimeOpeningExpired evt) => MarkCompleted();

    public void Handle(
        [SagaIdentityFrom("OpeningId")] TeeTimeOpeningCancelled evt) => MarkCompleted();

    public static void NotFound(TeeTimeOpeningExpirationTimeout timeout, ILogger<TeeTimeOpeningExpirationPolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {MessageType} for {Id}", nameof(TeeTimeOpeningExpirationTimeout), timeout.Id);

    public static void NotFound(
        [SagaIdentityFrom("OpeningId")] TeeTimeOpeningFilled evt,
        ILogger<TeeTimeOpeningExpirationPolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {EventType} for opening {OpeningId}", nameof(TeeTimeOpeningFilled), evt.OpeningId);

    public static void NotFound(
        [SagaIdentityFrom("OpeningId")] TeeTimeOpeningExpired evt,
        ILogger<TeeTimeOpeningExpirationPolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {EventType} for opening {OpeningId}", nameof(TeeTimeOpeningExpired), evt.OpeningId);

    public static void NotFound(
        [SagaIdentityFrom("OpeningId")] TeeTimeOpeningCancelled evt,
        ILogger<TeeTimeOpeningExpirationPolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {EventType} for opening {OpeningId}", nameof(TeeTimeOpeningCancelled), evt.OpeningId);
}

public record TeeTimeOpeningExpirationTimeout(Guid Id, TimeSpan Delay) : TimeoutMessage(Delay);

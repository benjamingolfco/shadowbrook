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

        var timeout = new TeeTimeOpeningExpirationTimeout(delay);
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
}

public record TeeTimeOpeningExpirationTimeout(TimeSpan Delay) : TimeoutMessage(Delay);

public record ExpireTeeTimeOpening(Guid OpeningId);

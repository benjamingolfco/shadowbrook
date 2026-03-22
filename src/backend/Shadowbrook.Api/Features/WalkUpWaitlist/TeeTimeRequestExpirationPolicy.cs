using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Shadowbrook.Api.Features.WalkUpWaitlist;

public class TeeTimeRequestExpirationPolicy : Saga
{
    public Guid Id { get; set; }

    public static (TeeTimeRequestExpirationPolicy, TeeTimeRequestExpirationTimeout) Start(
        TeeTimeRequestAdded evt,
        TimeProvider timeProvider)
    {
        var policy = new TeeTimeRequestExpirationPolicy { Id = evt.TeeTimeRequestId };

        var teeTimeUtc = CourseTime.ToUtc(evt.Date, evt.TeeTime, evt.TimeZoneId);
        var delay = teeTimeUtc - timeProvider.GetUtcNow();
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        var timeout = new TeeTimeRequestExpirationTimeout(evt.TeeTimeRequestId, delay);

        return (policy, timeout);
    }

    public CloseTeeTimeRequest Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] TeeTimeRequestExpirationTimeout timeout)
    {
        MarkCompleted();
        return new CloseTeeTimeRequest(timeout.TeeTimeRequestId);
    }

    public void Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] TeeTimeRequestFulfilled evt) => MarkCompleted();

    public void Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] TeeTimeRequestClosed evt) => MarkCompleted();
}

public record TeeTimeRequestExpirationTimeout(Guid TeeTimeRequestId, TimeSpan Delay) : TimeoutMessage(Delay);

public record CloseTeeTimeRequest(Guid TeeTimeRequestId);

using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Shadowbrook.Api.Features.Bookings.Policies;

public class BookingConfirmationPolicy : Saga
{
    public Guid Id { get; set; }

    public static (BookingConfirmationPolicy, object?) Start(BookingCreated evt)
    {
        var policy = new BookingConfirmationPolicy { Id = evt.BookingId };
        return (policy, null);
    }

    public ConfirmBookingCommand Handle(
        [SagaIdentityFrom("BookingId")] TeeTimeOpeningClaimed evt)
    {
        MarkCompleted();
        return new ConfirmBookingCommand(Id);
    }

    public RejectBookingCommand Handle(
        [SagaIdentityFrom("BookingId")] TeeTimeOpeningClaimRejected evt)
    {
        MarkCompleted();
        return new RejectBookingCommand(Id);
    }
}

public record ConfirmBookingCommand(Guid BookingId);
public record RejectBookingCommand(Guid BookingId);

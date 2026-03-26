using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Shadowbrook.Api.Features.Bookings;

public class BookingConfirmationPolicy : Saga
{
    public Guid Id { get; set; }

    public static BookingConfirmationPolicy? Start(BookingCreated evt)
    {
        if (evt.OpeningId is null)
        {
            return null; // Not a waitlist booking — no confirmation needed
        }

        return new BookingConfirmationPolicy { Id = evt.BookingId };
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

using Shadowbrook.Api.Features.Bookings.Policies;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

namespace Shadowbrook.Api.Tests.Policies;

public class BookingConfirmationPolicyTests
{
    [Fact]
    public void Start_AlwaysCreatesPolicyWithCorrectId()
    {
        var bookingId = Guid.NewGuid();
        var evt = new BookingCreated
        {
            BookingId = bookingId,
            GolferId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(10, 0),
            GroupSize = 2
        };

        var (policy, _) = BookingConfirmationPolicy.Start(evt);

        Assert.Equal(bookingId, policy.Id);
    }

    [Fact]
    public void Handle_TeeTimeOpeningSlotsClaimed_ReturnsConfirmBookingCommandAndMarksCompleted()
    {
        var bookingId = Guid.NewGuid();
        var policy = new BookingConfirmationPolicy { Id = bookingId };

        var result = policy.Handle(new TeeTimeOpeningSlotsClaimed
        {
            OpeningId = Guid.NewGuid(),
            BookingId = bookingId,
            GolferId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(10, 0),
            GroupSize = 1
        });

        Assert.Equal(bookingId, result.BookingId);
        Assert.True(policy.IsCompleted());
    }

    [Fact]
    public void Handle_TeeTimeOpeningSlotsClaimRejected_ReturnsRejectBookingCommandAndMarksCompleted()
    {
        var bookingId = Guid.NewGuid();
        var policy = new BookingConfirmationPolicy { Id = bookingId };

        var result = policy.Handle(new TeeTimeOpeningSlotsClaimRejected
        {
            OpeningId = Guid.NewGuid(),
            BookingId = bookingId,
            GolferId = Guid.NewGuid()
        });

        Assert.Equal(bookingId, result.BookingId);
        Assert.True(policy.IsCompleted());
    }
}

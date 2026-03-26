using NSubstitute;
using Shadowbrook.Api.Features.Bookings.Policies;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class BookingCreatedClaimHandlerTests
{
    private readonly ITeeTimeOpeningRepository openingRepo = Substitute.For<ITeeTimeOpeningRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public BookingCreatedClaimHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    private static BookingCreated MakeEvent(Guid courseId, DateOnly date, TimeOnly time, int groupSize = 1) =>
        new()
        {
            BookingId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            CourseId = courseId,
            Date = date,
            TeeTime = time,
            GroupSize = groupSize
        };

    [Fact]
    public async Task Handle_NoOpeningFound_ReturnsConfirmBookingCommand()
    {
        var date = new DateOnly(2026, 3, 25);
        var time = new TimeOnly(10, 0);
        var evt = MakeEvent(Guid.NewGuid(), date, time);

        this.openingRepo.GetActiveByCourseDateTimeAsync(evt.CourseId, date, time)
            .Returns((TeeTimeOpening?)null);

        var result = await BookingCreatedClaimHandler.Handle(evt, this.openingRepo, this.timeProvider);

        var command = Assert.IsType<ConfirmBookingCommand>(result);
        Assert.Equal(evt.BookingId, command.BookingId);
    }

    [Fact]
    public async Task Handle_OpeningFound_ClaimsAndReturnsNull()
    {
        var date = new DateOnly(2026, 3, 25);
        var time = new TimeOnly(10, 0);
        var opening = TeeTimeOpening.Create(Guid.NewGuid(), date, time, 4, true, this.timeProvider);
        opening.ClearDomainEvents();

        var evt = MakeEvent(opening.CourseId, date, time, groupSize: 2);

        this.openingRepo.GetActiveByCourseDateTimeAsync(opening.CourseId, date, time)
            .Returns(opening);

        var result = await BookingCreatedClaimHandler.Handle(evt, this.openingRepo, this.timeProvider);

        Assert.Null(result);
        Assert.Contains(opening.DomainEvents, e => e is TeeTimeOpeningClaimed);
    }
}

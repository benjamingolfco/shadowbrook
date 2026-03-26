using Microsoft.Extensions.Logging;
using NSubstitute;
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
    private readonly ILogger logger = Substitute.For<ILogger>();

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
    public async Task Handle_NoOpeningFound_LogsAndReturns()
    {
        var date = new DateOnly(2026, 3, 25);
        var time = new TimeOnly(10, 0);
        var evt = MakeEvent(Guid.NewGuid(), date, time);

        this.openingRepo.GetByCourseTeeTimeAsync(evt.CourseId, new TeeTime(date, time))
            .Returns((TeeTimeOpening?)null);

        await BookingCreatedClaimHandler.Handle(evt, this.openingRepo, this.timeProvider, this.logger);

        // No exception, handler returns normally
    }

    [Fact]
    public async Task Handle_OpeningFound_ClaimsOpening()
    {
        var date = new DateOnly(2026, 3, 25);
        var time = new TimeOnly(10, 0);
        var opening = TeeTimeOpening.Create(Guid.NewGuid(), date, time, 4, true, this.timeProvider);
        opening.ClearDomainEvents();

        var evt = MakeEvent(opening.CourseId, date, time, groupSize: 2);

        this.openingRepo.GetByCourseTeeTimeAsync(opening.CourseId, new TeeTime(date, time))
            .Returns(opening);

        await BookingCreatedClaimHandler.Handle(evt, this.openingRepo, this.timeProvider, this.logger);

        Assert.Contains(opening.DomainEvents, e => e is TeeTimeOpeningClaimed);
    }

    [Fact]
    public async Task Handle_OpeningNotOpen_RaisesRejectedEvent()
    {
        var date = new DateOnly(2026, 3, 25);
        var time = new TimeOnly(10, 0);
        var opening = TeeTimeOpening.Create(Guid.NewGuid(), date, time, 1, true, this.timeProvider);
        opening.TryClaim(Guid.NewGuid(), Guid.NewGuid(), groupSize: 1, this.timeProvider); // fills it
        opening.ClearDomainEvents();

        var evt = MakeEvent(opening.CourseId, date, time, groupSize: 1);

        this.openingRepo.GetByCourseTeeTimeAsync(opening.CourseId, new TeeTime(date, time))
            .Returns(opening);

        await BookingCreatedClaimHandler.Handle(evt, this.openingRepo, this.timeProvider, this.logger);

        Assert.Contains(opening.DomainEvents, e => e is TeeTimeOpeningClaimRejected);
    }
}
